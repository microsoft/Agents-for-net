using System.Globalization;
using System.Text.Json;

namespace Microsoft.Agents.ApiCompat;

public sealed record PullRequestEvent(
    long RunId,
    int Number,
    string BaseRef,
    string Body,
    IReadOnlyList<string> Labels)
{
    public static PullRequestEvent Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var pullRequest = RequireObjectProperty(root, "pull_request", "pull_request");
        var labels = RequireArrayProperty(pullRequest, "labels", "pull_request.labels")
            .EnumerateArray()
            .Select(RequireLabelName)
            .ToArray();

        var runIdText = Environment.GetEnvironmentVariable("GITHUB_RUN_ID")
            ?? throw new InvalidDataException("GITHUB_RUN_ID is required.");

        return new(
            long.Parse(runIdText, CultureInfo.InvariantCulture),
            RequireInt32Property(pullRequest, "number", "pull_request.number"),
            RequireObjectProperty(pullRequest, "base", "pull_request.base")
                .GetStringProperty("ref", "pull_request.base.ref"),
            pullRequest.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty,
            labels);
    }

    private static string RequireLabelName(JsonElement label)
    {
        if (label.ValueKind != JsonValueKind.Object
            || !label.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("pull_request.labels[].name is required and must be a string.");
        }

        return name.GetString()!;
    }

    private static JsonElement RequireObjectProperty(JsonElement parent, string propertyName, string fieldPath)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{fieldPath} is required and must be an object.");
        }

        return property;
    }

    private static JsonElement RequireArrayProperty(JsonElement parent, string propertyName, string fieldPath)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{fieldPath} is required and must be an array.");
        }

        return property;
    }

    private static int RequireInt32Property(JsonElement parent, string propertyName, string fieldPath)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value))
        {
            throw new InvalidDataException($"{fieldPath} is required and must be an integer.");
        }

        return value;
    }
}

internal static class JsonElementExtensions
{
    public static string GetStringProperty(this JsonElement parent, string propertyName, string fieldPath)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{fieldPath} is required and must be a string.");
        }

        return property.GetString()!;
    }
}
