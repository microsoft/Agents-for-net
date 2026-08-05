using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Agents.ApiCompat;

public sealed record PackageProject(string ProjectPath, string PackageId);

public static class ProjectDiscovery
{
    private static readonly StringComparer PropertyComparer = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyList<PackageProject> Discover(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var libraryRoot = Path.Combine(repositoryRoot, "src", "libraries");
        if (!Directory.Exists(libraryRoot))
        {
            return Array.Empty<PackageProject>();
        }

        return Directory.EnumerateFiles(libraryRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(ReadProject)
            .Where(project => project is not null)
            .Cast<PackageProject>()
            .OrderBy(project => project.PackageId, StringComparer.Ordinal)
            .ThenBy(project => project.ProjectPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static PackageProject? ReadProject(string projectPath)
    {
        var properties = ReadProperties(projectPath, new HashSet<string>(PropertyComparer));
        if (properties.TryGetValue("IsPackable", out var isPackable) &&
            string.Equals(isPackable, bool.FalseString, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var packageId = GetFirstValue(properties, "PackageId")
            ?? Path.GetFileNameWithoutExtension(projectPath);

        return new(projectPath, packageId);
    }

    private static Dictionary<string, string> ReadProperties(string path, HashSet<string> visitedPaths)
    {
        var fullPath = Path.GetFullPath(path);
        if (!visitedPaths.Add(fullPath))
        {
            return new Dictionary<string, string>(PropertyComparer);
        }

        using var stream = File.OpenRead(fullPath);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            XmlResolver = null,
        });
        var document = XDocument.Load(reader);
        var properties = new Dictionary<string, string>(PropertyComparer);

        if (document.Root is null)
        {
            return properties;
        }

        foreach (var element in document.Root.Elements())
        {
            if (element.Name.LocalName.Equals("Import", StringComparison.Ordinal))
            {
                MergeImportedProperties(fullPath, element, properties, visitedPaths);
                continue;
            }

            if (!element.Name.LocalName.Equals("PropertyGroup", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var property in element.Elements())
            {
                if (!IsTrackedProperty(property.Name.LocalName))
                {
                    continue;
                }

                properties[property.Name.LocalName] = property.Value.Trim();
            }
        }

        return properties;
    }

    private static void MergeImportedProperties(
        string projectPath,
        XElement importElement,
        Dictionary<string, string> properties,
        HashSet<string> visitedPaths)
    {
        var importProject = importElement.Attribute("Project")?.Value;
        var importedPath = ResolveImportPath(projectPath, importProject);
        if (importedPath is null)
        {
            return;
        }

        foreach (var property in ReadProperties(importedPath, visitedPaths))
        {
            properties[property.Key] = property.Value;
        }
    }

    private static string? ResolveImportPath(string projectPath, string? importProject)
    {
        if (string.IsNullOrWhiteSpace(importProject) ||
            importProject.Contains("$(", StringComparison.Ordinal) ||
            importProject.IndexOfAny(['*', '?']) >= 0)
        {
            return null;
        }

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, importProject));
        return File.Exists(fullPath) ? fullPath : null;
    }

    private static string? GetFirstValue(IReadOnlyDictionary<string, string> properties, params string[] names)
    {
        foreach (var name in names)
        {
            if (properties.TryGetValue(name, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsTrackedProperty(string propertyName)
    {
        return PropertyComparer.Equals(propertyName, "IsPackable") ||
               PropertyComparer.Equals(propertyName, "PackageId");
    }
}
