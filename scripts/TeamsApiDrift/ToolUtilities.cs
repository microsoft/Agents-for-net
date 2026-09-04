using System.Text.Json;

namespace Microsoft.Agents.TeamsApiDrift;

internal sealed class Arguments
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public Arguments(IEnumerable<string> args)
    {
        var values = args.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var argument = values[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown argument: {argument}");
            }

            if (index + 1 >= values.Length || values[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                _flags.Add(argument);
                continue;
            }

            if (!_values.TryGetValue(argument, out var optionValues))
            {
                optionValues = [];
                _values[argument] = optionValues;
            }

            optionValues.Add(values[++index]);
        }
    }

    public bool HasFlag(string name) => _flags.Contains(name);

    public string? Optional(string name) => _values.TryGetValue(name, out var values) ? values[^1] : null;

    public string Required(string name) => Optional(name) ?? throw new ArgumentException($"{name} requires a value.");

    public IReadOnlyList<string> Many(string name) => _values.TryGetValue(name, out var values) ? values : [];
}

internal static class ToolJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static T Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Input file was not found.", path);
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Could not deserialize {path}.");
    }

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options) + Environment.NewLine);
    }

    public static string OutputFile(string output, string defaultName, string extension)
    {
        var fullPath = Path.GetFullPath(output);
        return string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : Path.Combine(fullPath, defaultName);
    }
}

internal static class Paths
{
    public static string Normalize(string path) => path.Replace('\\', '/');

    public static bool IsContainedBy(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
