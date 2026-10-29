using System.Text.RegularExpressions;
using System.Xml.Linq;
using NuGet.Versioning;

namespace Microsoft.Agents.TeamsApiDrift;

public sealed record ComparisonVersions(int SchemaVersion, string FromVersion, string ToVersion, bool SameVersion);

public static class VersionResolver
{
    public static ComparisonVersions Compare(string from, string to)
    {
        if (!NuGetVersion.TryParse(from, out var baseline)) throw new ArgumentException($"Invalid baseline NuGet version: {from}");
        if (!NuGetVersion.TryParse(to, out var candidate)) throw new ArgumentException($"Invalid candidate NuGet version: {to}");
        // NuGet package identity ignores build metadata and normalizes missing parts.
        return new(1, baseline.ToNormalizedString(), candidate.ToNormalizedString(), VersionComparer.VersionRelease.Equals(baseline, candidate));
    }

    public static string Resolve(TextReader reader, string packageId = PackageConstants.PackageId)
    {
        var document = XDocument.Load(reader);
        var version = document.Descendants("PackageVersion")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Include"), packageId, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Version")?.Value;
        if (string.IsNullOrWhiteSpace(version)) throw new InvalidDataException($"PackageVersion for {packageId} was not found.");
        if (!version.StartsWith("$(", StringComparison.Ordinal)) return version;
        var propertyName = version[2..^1];
        var property = document.Descendants(propertyName).FirstOrDefault()?.Value;
        return !string.IsNullOrWhiteSpace(property)
            ? property
            : throw new InvalidDataException($"MSBuild property {propertyName} was not found.");
    }
}

public static class FindingClassifier
{
    internal static KeyValuePair<string?, Capability?> MatchCapability(string symbol, CapabilityDocument document)
    {
        return document.Capabilities
            .Select(item => new
            {
                item.Key,
                item.Value,
                Score = item.Value.UpstreamTypes.Contains(symbol, StringComparer.Ordinal)
                    ? int.MaxValue
                    : item.Value.UpstreamNamespaces.Where(prefix => symbol.StartsWith(prefix + ".", StringComparison.Ordinal) || symbol == prefix).Select(prefix => prefix.Length).DefaultIfEmpty(-1).Max()
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new KeyValuePair<string?, Capability?>(item.Key, item.Value))
            .FirstOrDefault();
    }
}

public static partial class AgentContextBuilder
{
    internal static void RejectSymlinkTraversal(string sourceRoot, string fullPath, string path)
    {
        var current = Path.GetFullPath(sourceRoot);
        RejectSymlink(current, path);
        var relative = Path.GetRelativePath(current, fullPath);
        foreach (var component in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            RejectSymlink(current, path);
        }
    }

    private static void RejectSymlink(string fullPath, string path)
    {
        FileSystemInfo entry = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
        if (entry.LinkTarget is not null)
        {
            throw new InvalidDataException($"Source path contains a symlink: {path}");
        }
    }

    internal static string Redact(string content)
    {
        content = AuthorizationRegex().Replace(content, "$1[REDACTED]");
        return SecretRegex().Replace(content, "$1[REDACTED]");
    }

    [GeneratedRegex("(authorization\\s*[:=]\\s*[\\\"']?)(?:bearer\\s+)?[^\\\"'\\s,}]+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationRegex();
    [GeneratedRegex("((?:clientSecret|apiKey|password|token)\\s*[:=]\\s*[\\\"'])[^\\\"']+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretRegex();
}
