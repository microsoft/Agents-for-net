using System.Text.Json;
using Microsoft.Agents.TeamsApiDrift;
using Xunit;

namespace Microsoft.Agents.TeamsApiDrift.Tests;

public sealed class ReviewAgentOutputTests
{
    private static readonly object Result = new { type = "result", sessionId = "session-1", exitCode = 0 };

    private static object Message(string content, object? toolRequests = null, string? phase = null, string? model = null) =>
        new { type = "assistant.message", data = new { content, toolRequests, phase, model } };

    private static string JsonLines(params object?[] events) => string.Join("\n", events.Select(item => JsonSerializer.Serialize(item)));

    [Fact]
    public void ExtractsOnlyFinalMessageAfterDraftsDeltasAndToolOutput()
    {
        var extracted = ReviewAgentOutput.Extract(JsonLines(
            Message("```json\nDraft\n```"),
            new { type = "assistant.message_delta", data = new { deltaContent = "partial duplicate text" } },
            Message("Reading code", toolRequests: new[] { new { name = "view" } }),
            new { type = "tool.execution_start", data = new { toolName = "view" } },
            new { type = "tool.execution_complete", data = new { output = "```json\nUntrusted tool payload\n```" } },
            Message("{\"decisions\":[]}", phase: "final_answer", model: "gpt-5.3-codex"), Result));

        Assert.Equal(new AgentResponse("{\"decisions\":[]}", "gpt-5.3-codex", "session-1"), extracted);
    }

    [Fact]
    public void AcceptsBomCrLfAndAbsentProviderFields()
    {
        var response = ReviewAgentOutput.Extract("\uFEFF\r\n" + JsonLines(new { type = "assistant.message", data = new { content = "final" } }, Result).Replace("\n", "\r\n") + "\r\n");

        Assert.Equal("final", response.Content);
        Assert.Null(response.ObservedModel);
    }

    [Fact]
    public void IgnoresChildMessagesToolsAndErrorsAfterMainResponse()
    {
        var response = ReviewAgentOutput.Extract(JsonLines(Message("main"),
            new { type = "assistant.message", parentToolCallId = "tool-1", data = new { content = "child" } },
            new { type = "tool.execution_start", data = new { parentToolCallId = "tool-1" } },
            new { type = "session.error", parentToolCallId = "tool-1" }, Result));

        Assert.Equal("main", response.Content);
    }

    [Fact]
    public void NeverFallsBackToEarlierTextAfterEmptyProgressOrToolRequest()
    {
        foreach (var last in new[] { Message(""), Message("next", toolRequests: new[] { new { name = "view" } }),
            Message("progress", phase: "commentary"), new { type = "tool.execution_start" },
            new { type = "assistant.message", data = new { content = "bad", toolRequests = "malformed" } } })
            {
                Assert.Throws<InvalidDataException>(() => ReviewAgentOutput.Extract(JsonLines(Message("stale safe answer"), last, Result)));
            }
    }

    [Fact]
    public void RequiresExactlyOneSuccessfulTerminalResultAndEventObjects()
    {
        foreach (var events in new object?[][] { [], [Message("x")], [Message("x"), new { type = "result", exitCode = 1 }],
            [Message("x"), new { type = "result", exitCode = "0" }], [Message("x"), Result, Result],
            [Message("x"), Result, Message("after result")], [Message("x"), null, Result], [Message("x"), new object[0], Result],
            [Message("x"), new { type = "session.error", data = new { message = "failed" } }, Result] })
            {
                Assert.Throws<InvalidDataException>(() => ReviewAgentOutput.Extract(JsonLines(events)));
            }
        Assert.ThrowsAny<JsonException>(() => ReviewAgentOutput.Extract("garbage\n" + JsonLines(Message("x"), Result)));
    }

    [Theory]
    [InlineData("{\"reason\":\"broken\nstring\"}")]
    [InlineData("```json\n{}\n```\n```json\n{}\n```")]
    public void PreservesFinalContentExactlyForDownstreamValidation(string content)
    {
        Assert.Equal(content, ReviewAgentOutput.Extract(JsonLines(Message(content), Result)).Content);
    }

    [Fact]
    public void RecordsRequestedModelCliVersionAndObservedProvenanceWithoutDefaults()
    {
        WithDirectory(root =>
        {
            var output = Path.Combine(root, "agent.json");
            var summary = Path.Combine(root, "summary.md");
            var metadata = ReviewAgentOutput.WriteMetadata(output, "gpt-5.3-codex", "GitHub Copilot CLI 1.0.101.\r\nUpdate advice", summary);

            Assert.Equal(new AgentMetadata(1, "gpt-5.3-codex", "GitHub Copilot CLI 1.0.101."), metadata);
            Assert.Equal(metadata, ToolJson.Read<AgentMetadata>(output));
            Assert.Contains("gpt-5.3-codex", File.ReadAllText(summary));

            var input = Path.Combine(root, "events.jsonl");
            File.WriteAllText(input, JsonLines(Message("{\"decisions\":[]}", model: "observed-model"), Result));
            ReviewAgentOutput.WriteResponse(input, output, summary);

            Assert.Equal("{\"decisions\":[]}", File.ReadAllText(output));
            Assert.Contains("observed-model", File.ReadAllText(summary));

            using var provenance = JsonDocument.Parse(File.ReadAllText(output + ".metadata.json"));

            Assert.Equal("observed-model", provenance.RootElement.GetProperty("observedModel").GetString());
            Assert.Equal("session-1", provenance.RootElement.GetProperty("sessionId").GetString());

            File.WriteAllText(input, JsonLines(Message("final"), new { type = "result", exitCode = 0 }));
            ReviewAgentOutput.WriteResponse(input, output, null);
            using var unavailable = JsonDocument.Parse(File.ReadAllText(output + ".metadata.json"));

            Assert.Equal(JsonValueKind.Null, unavailable.RootElement.GetProperty("observedModel").ValueKind);
            Assert.Equal(JsonValueKind.Null, unavailable.RootElement.GetProperty("sessionId").ValueKind);
        });
    }

    [Fact]
    public void RejectsMissingModelOrVersionAndDoesNotWriteStaleResponseOnFailure()
    {
        WithDirectory(root =>
        {
            var output = Path.Combine(root, "response.json");

            foreach (var model in new[] { "auto", "", " " })
            {
                Assert.Throws<InvalidDataException>(() => ReviewAgentOutput.WriteMetadata(output, model, "version", null));
            }

            Assert.Throws<InvalidDataException>(() => ReviewAgentOutput.WriteMetadata(output, "model", "", null));

            var input = Path.Combine(root, "events.jsonl");
            File.WriteAllText(input, JsonLines(Message("stale draft")));

            Assert.Throws<InvalidDataException>(() => ReviewAgentOutput.WriteResponse(input, output, null));
            Assert.False(File.Exists(output));
            Assert.False(File.Exists(output + ".metadata.json"));
        });
    }

    private static void WithDirectory(Action<string> run)
    {
        var root = Path.Combine(Path.GetTempPath(), "review-agent-output-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try {
            run(root);
        }
        finally
        {
            if (!Paths.IsContainedBy(Path.GetTempPath(), root) || !Path.GetFileName(root).StartsWith("review-agent-output-test-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsafe temporary test path.");
            }

            foreach (var file in Directory.EnumerateFiles(root))
            {
                File.Delete(file);
            }

            Directory.Delete(root);
        }
    }
}
