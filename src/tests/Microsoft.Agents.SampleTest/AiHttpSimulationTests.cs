// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.AI.OpenAI;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class StreamingHttpSimulationTests
{
    [Fact]
    public async Task OpenAiSseChunksBecomeOneFinalPoemOnANonStreamingChannel()
    {
        using var http = new HttpSimulation();
        AiHttpFixtures.ExpectCompletion(http)
            .WithJsonBody(body => body.GetProperty("stream").GetBoolean() &&
                body.GetProperty("messages")[1].GetProperty("content").GetString().Contains("Apollo", StringComparison.Ordinal))
            .RespondText(AiHttpFixtures.Stream("Apollo sings.\n\n", "Daylight follows."), "text/event-stream");
        using var transport = http.CreateClient();
        ChatClient chat = AiHttpFixtures.CreateOpenAi(transport).GetChatClient("fixture");

        await using (var host = HttpSampleHost.Create(http, (options, _) => new StreamingMessageAgent.StreamingAgent(options, chat)))
        {
            await host.CreateTestFlow().Send("poem").AssertReply("Apollo sings.\n\nDaylight follows.").StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task MalformedSseAfterContentStillFinalizesThePartialPoem()
    {
        using var http = new HttpSimulation();
        AiHttpFixtures.ExpectCompletion(http)
            .RespondText(AiHttpFixtures.Chunk("Partial poem", role: "assistant") + "data: {not json}\n\n", "text/event-stream");
        using var transport = http.CreateClient();
        ChatClient chat = AiHttpFixtures.CreateOpenAi(transport).GetChatClient("fixture");

        await using (var host = HttpSampleHost.Create(http, (options, _) => new StreamingMessageAgent.StreamingAgent(options, chat)))
        {
            await Assert.ThrowsAnyAsync<JsonException>(() => host.CreateTestFlow().Send("poem").StartTestAsync());
            Assert.Equal("Partial poem", host.Adapter.GetNextReply()?.Text);
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task ModelServiceFailureIsNotRetriedOrTurnedIntoAPoem()
    {
        using var http = new HttpSimulation();
        AiHttpFixtures.ExpectCompletion(http).RespondJson(AiHttpFixtures.ServiceError, HttpStatusCode.ServiceUnavailable);
        using var transport = http.CreateClient();
        ChatClient chat = AiHttpFixtures.CreateOpenAi(transport).GetChatClient("fixture");

        await using (var host = HttpSampleHost.Create(http, (options, _) => new StreamingMessageAgent.StreamingAgent(options, chat)))
        {
            var failure = await Assert.ThrowsAsync<ClientResultException>(() => host.CreateTestFlow().Send("poem").StartTestAsync());
            Assert.Equal(503, failure.Status);
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }
}

public class WeatherHttpSimulationTests
{
    [Fact]
    public async Task CompleteOpenAiDocumentProducesDeterministicWeatherText()
    {
        using var http = new HttpSimulation();
        AiHttpFixtures.ExpectCompletion(http)
            .WithJsonBody(body => body.GetProperty("response_format").GetProperty("type").GetString() == "json_object")
            .RespondJson(AiHttpFixtures.Completion("""{"contentType":"Text","content":"Seattle: 18 C, sunny."}"""));
        using var transport = http.CreateClient();
        Kernel kernel = CreateKernel(transport);

        await using (var host = HttpSampleHost.Create(http, (options, _) => new WeatherAgent.MyAgent(options, kernel)))
        {
            await host.CreateTestFlow().Send("Seattle forecast").AssertReply("Seattle: 18 C, sunny.").StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task ModelCardIsParsedAndDeliveredAsTheFinalAttachment()
    {
        const string card = """{"type":"AdaptiveCard","version":"1.5","body":[{"type":"TextBlock","text":"Seattle: 18 C"}]}""";
        using var http = new HttpSimulation();
        AiHttpFixtures.ExpectCompletion(http)
            .RespondJson(AiHttpFixtures.Completion(JsonSerializer.Serialize(new { contentType = "AdaptiveCard", content = card })));
        using var transport = http.CreateClient();
        Kernel kernel = CreateKernel(transport);

        await using (var host = HttpSampleHost.Create(http, (options, _) => new WeatherAgent.MyAgent(options, kernel)))
        {
            await host.CreateTestFlow().Send("Seattle forecast as a card").AssertReplySatisfies(reply =>
            {
                var attachment = Assert.Single(reply.Attachments);
                Assert.Equal(ContentTypes.AdaptiveCard, attachment.ContentType);
                using var document = JsonDocument.Parse(attachment.Content.ToString());
                Assert.Equal("AdaptiveCard", document.RootElement.GetProperty("type").GetString());
                Assert.Equal("Seattle: 18 C", document.RootElement.GetProperty("body")[0].GetProperty("text").GetString());
                return Task.CompletedTask;
            }).StartTestAsync();
        }

        http.Verify();
    }

    [Fact]
    public async Task MalformedModelContentRequestsACorrectedCompletion()
    {
        using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
        http.ExpectSequence(HttpMethod.Post, AiHttpFixtures.CompletionUrl)
            .RespondJson(AiHttpFixtures.Completion("not json"))
            .RespondJson(AiHttpFixtures.Completion("""{"contentType":"Text","content":"Corrected Seattle forecast."}"""));
        using var transport = http.CreateClient();
        Kernel kernel = CreateKernel(transport);

        await using (var host = HttpSampleHost.Create(http, (options, _) => new WeatherAgent.MyAgent(options, kernel)))
        {
            await host.CreateTestFlow().Send("Seattle forecast").AssertReply("Corrected Seattle forecast.").StartTestAsync();
        }

        http.Verify();
        using var retry = JsonDocument.Parse(http.Exchanges[1].Body);
        var messages = retry.RootElement.GetProperty("messages");
        Assert.Contains("That response did not match the expected format", messages[messages.GetArrayLength() - 1].GetProperty("content").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModelHttpFailurePropagatesWithoutALiveJudgeOrWeatherService()
    {
        using var http = new HttpSimulation();
        AiHttpFixtures.ExpectCompletion(http).RespondJson(AiHttpFixtures.ServiceError, HttpStatusCode.ServiceUnavailable);
        using var transport = http.CreateClient();
        Kernel kernel = CreateKernel(transport);

        await using (var host = HttpSampleHost.Create(http, (options, _) => new WeatherAgent.MyAgent(options, kernel)))
        {
            var failure = await Record.ExceptionAsync(() => host.CreateTestFlow().Send("Seattle forecast").StartTestAsync());
            Assert.NotNull(failure);
            Assert.IsNotType<HttpSimulationException>(failure);
            Assert.Contains("503", failure.ToString(), StringComparison.Ordinal);
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    private static Kernel CreateKernel(HttpClient transport) =>
        Kernel.CreateBuilder().AddAzureOpenAIChatCompletion("fixture", AiHttpFixtures.CreateOpenAi(transport)).Build();
}

internal static class AiHttpFixtures
{
    internal const string CompletionUrl = "https://ai.example.test/openai/deployments/fixture/chat/completions?api-version=2024-10-21";

    internal static object ServiceError => new
    {
        error = new { message = "Model unavailable", type = "server_error", param = (string)null, code = "service_unavailable" }
    };

    internal static HttpExpectation ExpectCompletion(HttpSimulation http) =>
        http.Expect(HttpMethod.Post, CompletionUrl).WithHeader("api-key", "synthetic-openai-key");

    internal static AzureOpenAIClient CreateOpenAi(HttpClient transport) => new(
        new Uri("https://ai.example.test/"),
        new ApiKeyCredential("synthetic-openai-key"),
        new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
        {
            Transport = new HttpClientPipelineTransport(transport),
            RetryPolicy = new ClientRetryPolicy(0)
        });

    internal static object Completion(string content) => new
    {
        id = "chatcmpl-fixture",
        @object = "chat.completion",
        created = 1700000000,
        model = "fixture",
        choices = new[] { new { index = 0, message = new { role = "assistant", content, refusal = (string)null }, finish_reason = "stop" } },
        usage = new { prompt_tokens = 20, completion_tokens = 10, total_tokens = 30 }
    };

    internal static string Stream(params string[] chunks) =>
        Chunk("", role: "assistant") +
        string.Concat(Array.ConvertAll(chunks, chunk => Chunk(chunk))) +
        "data: {\"id\":\"chatcmpl-fixture\",\"object\":\"chat.completion.chunk\",\"created\":1700000000,\"model\":\"fixture\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
        "data: [DONE]\n\n";

    internal static string Chunk(string content, string role = null) =>
        "data: " + JsonSerializer.Serialize(new
        {
            id = "chatcmpl-fixture",
            @object = "chat.completion.chunk",
            created = 1700000000,
            model = "fixture",
            choices = new[] { new { index = 0, delta = new { role, content }, finish_reason = (string)null } }
        }) + "\n\n";
}
