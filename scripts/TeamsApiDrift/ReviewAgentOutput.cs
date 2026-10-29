using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.TeamsApiDrift;

internal sealed record AgentResponse(string Content, string? ObservedModel, string? SessionId);

internal sealed record AgentMetadata(int SchemaVersion, string RequestedModel, string CliVersion);

internal static class ReviewAgentOutput
{
    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? Text(JsonElement element) => element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static bool HasParent(JsonElement element) => Property(element, "parentToolCallId").ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null) &&
        Text(Property(element, "parentToolCallId")) != "";

    // Copilot CLI emits completed message snapshots and a terminal result.
    // Never concatenate deltas or fall back to a draft after a tool request.
    internal static AgentResponse Extract(string text)
    {
        var events = text.TrimStart('\uFEFF').Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            using var document = JsonDocument.Parse(line);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Copilot output must contain JSON event objects.");
            }

            return document.RootElement.Clone();
        }).ToArray();
        var results = events.Where(item => Text(Property(item, "type")) == "result").ToArray();

        if (results.Length != 1 || events.Length == 0 || Text(Property(events[^1], "type")) != "result" ||
            Property(results[0], "exitCode").ValueKind != JsonValueKind.Number || !Property(results[0], "exitCode").TryGetInt32(out var status) || status != 0)
        {
            throw new InvalidDataException("Copilot output requires exactly one successful terminal result.");
        }

        JsonElement finalMessage = default;

        foreach (var item in events[..^1])
        {
            var data = Property(item, "data");

            if (HasParent(item) || HasParent(data))
            {
                continue;
            }

            var type = Text(Property(item, "type"));

            if (type == "session.error")
            {
                throw new InvalidDataException("Copilot session reported an error.");
            }

            if (type == "tool.execution_start")
            {
                finalMessage = default;
            }

            if (type != "assistant.message")
            {
                continue;
            }

            var requests = Property(data, "toolRequests");
            var phase = Property(data, "phase");
            finalMessage = !string.IsNullOrWhiteSpace(Text(Property(data, "content"))) &&
                (requests.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || requests.ValueKind == JsonValueKind.Array && requests.GetArrayLength() == 0) &&
                (phase.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || Text(phase) is "" or "final_answer") ? data : default;
        }

        if (finalMessage.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Copilot output has no completed final assistant response.");
        }

        return new(Text(Property(finalMessage, "content"))!, Text(Property(finalMessage, "model")), Text(Property(results[0], "sessionId")));
    }

    internal static AgentMetadata WriteMetadata(string path, string model, string version, string? summaryPath)
    {
        if (string.IsNullOrWhiteSpace(model) || model == "auto")
        {
            throw new InvalidDataException("An explicit COPILOT_MODEL is required.");
        }

        var cliVersion = version.Split('\n')[0].Trim();

        if (cliVersion.Length == 0)
        {
            throw new InvalidDataException("Copilot CLI version is missing.");
        }

        var metadata = new AgentMetadata(1, model, cliVersion);
        ToolJson.Write(path, metadata);
        Console.WriteLine($"Summary: Review model (requested): {model}; CLI: {cliVersion}");

        if (!string.IsNullOrEmpty(summaryPath))
        {
            File.AppendAllText(summaryPath, $"\n### Implementation review agent\n\n- Requested model: `{model}`\n- CLI: `{cliVersion}`\n");
        }

        return metadata;
    }

    internal static AgentResponse WriteResponse(string input, string output, string? summaryPath)
    {
        var response = Extract(File.ReadAllText(input));
        File.WriteAllText(output, response.Content, new UTF8Encoding(false));

        // Explicit nulls distinguish unavailable provenance from inferred defaults.
        File.WriteAllText(output + ".metadata.json", JsonSerializer.Serialize(new { response.ObservedModel, response.SessionId },
            new JsonSerializerOptions(ToolJson.Options) { DefaultIgnoreCondition = JsonIgnoreCondition.Never }) + "\n");
        Console.WriteLine($"Summary: Captured final assistant response; observed model: {response.ObservedModel ?? "not reported by CLI"}.");

        if (!string.IsNullOrEmpty(summaryPath) && !string.IsNullOrEmpty(response.ObservedModel))
        {
            File.AppendAllText(summaryPath, $"\n- Model reported in final response event: `{response.ObservedModel}`\n");
        }

        return response;
    }
}
