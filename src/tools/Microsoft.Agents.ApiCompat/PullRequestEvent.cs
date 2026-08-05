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
        var pullRequest = root.GetProperty("pull_request");
        var labels = pullRequest.GetProperty("labels")
            .EnumerateArray()
            .Select(label => label.GetProperty("name").GetString() ?? string.Empty)
            .Where(name => name.Length > 0)
            .ToArray();

        var runIdText = Environment.GetEnvironmentVariable("GITHUB_RUN_ID")
            ?? throw new InvalidDataException("GITHUB_RUN_ID is required.");

        return new(
            long.Parse(runIdText, CultureInfo.InvariantCulture),
            pullRequest.GetProperty("number").GetInt32(),
            pullRequest.GetProperty("base").GetProperty("ref").GetString()
                ?? throw new InvalidDataException("PR base ref is missing."),
            pullRequest.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty,
            labels);
    }
}
