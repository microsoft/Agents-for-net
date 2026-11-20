// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class ConversationSessionHttpSimulationTests
{
    private const string StartUrl = "https://copilot.example.test/copilotstudio/dataverse-backed/authenticated/bots/fixture/conversations?api-version=2022-03-01-preview";

    [Fact]
    public async Task SessionStartsAndSendsThroughTheRealCopilotClientWithoutLaunchingTheUi()
    {
        using var http = new HttpSimulation();
        using var services = CreateServices(http);
        ConversationSession session = CreateSession(services);
        var published = new List<(ActivityDirection, string)>();
        session.ActivityPublished += (_, args) => published.Add((args.Direction, args.Activity.Text));

        RespondActivities(http.Expect(HttpMethod.Post, StartUrl)
            .ForClient("mcs")
            .WithHeader("Authorization", "Bearer synthetic-copilot-token")
            .WithHeader("x-ms-conversation-id", session.ConversationId)
            .WithJsonBody(body => body.GetProperty("emitStartConversationEvent").GetBoolean() &&
                body.GetProperty("conversationId").GetString() == session.ConversationId),
            session.ConversationId, "Welcome");
        RespondActivities(http.Expect(HttpMethod.Post, ExecuteUrl(session))
            .ForClient("mcs")
            .WithHeader("Authorization", "Bearer synthetic-copilot-token")
            .WithJsonBody(body => body.GetProperty("activity").GetProperty("text").GetString() == "hello" &&
                body.GetProperty("activity").GetProperty("conversation").GetProperty("id").GetString() == session.ConversationId),
            session.ConversationId, "Hello from Copilot");

        await session.StartAsync(CancellationToken.None);
        await session.SendAsync("hello", CancellationToken.None);

        Assert.Equal(
            [(ActivityDirection.Inbound, "Welcome"), (ActivityDirection.Outbound, "hello"), (ActivityDirection.Inbound, "Hello from Copilot")],
            published);
        http.Verify();
    }

    [Theory]
    [InlineData("forbidden")]
    [InlineData("malformed")]
    [InlineData("empty")]
    public async Task ServiceAndPayloadFailuresRaiseFailedAndAllowAnotherSend(string outcome)
    {
        using var http = new HttpSimulation();
        using var services = CreateServices(http);
        ConversationSession session = CreateSession(services);
        var failures = new List<Exception>();
        var busy = new List<bool>();
        var inbound = new List<string>();
        session.Failed += (_, exception) => failures.Add(exception);
        session.BusyChanged += (_, value) => busy.Add(value);
        session.ActivityPublished += (_, args) =>
        {
            if (args.Direction == ActivityDirection.Inbound)
            {
                inbound.Add(args.Activity.Text);
            }
        };
        var exchange = http.ExpectSequence(HttpMethod.Post, ExecuteUrl(session));
        switch (outcome)
        {
            case "forbidden":
                exchange.RespondJson(new { error = new { code = "Forbidden", message = "Access denied" } }, HttpStatusCode.Forbidden);
                break;
            case "malformed":
                exchange.RespondText("{not json}", "application/json");
                break;
            case "empty":
                exchange.RespondText("null", "application/json");
                break;
        }
        RespondActivities(exchange, session.ConversationId, "Recovered");

        Exception failure = await Record.ExceptionAsync(() => session.SendAsync("first", CancellationToken.None));

        if (outcome == "forbidden")
        {
            Assert.IsType<HttpRequestException>(failure);
            Assert.Contains("Forbidden", failure.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.IsAssignableFrom<JsonException>(failure);
        }
        Assert.Same(failure, Assert.Single(failures));
        Assert.Empty(inbound);
        Assert.Equal([true, false], busy);

        await session.SendAsync("retry", CancellationToken.None);

        Assert.Equal(["Recovered"], inbound);
        Assert.Equal([true, false, true, false], busy);
        http.Verify();
    }

    [Fact]
    public async Task CancellationReachesHttpAndResetsBusyWithoutRaisingFailed()
    {
        using var http = new HttpSimulation();
        using var services = CreateServices(http);
        ConversationSession session = CreateSession(services);
        var failures = new List<Exception>();
        var busy = new List<bool>();
        session.Failed += (_, exception) => failures.Add(exception);
        session.BusyChanged += (_, value) => busy.Add(value);
        var exchange = http.ExpectSequence(HttpMethod.Post, ExecuteUrl(session)).WaitForCancellation();
        RespondActivities(exchange, session.ConversationId, "After cancellation");
        using var cancellation = new CancellationTokenSource();

        Task pending = session.SendAsync("cancel me", cancellation.Token);
        await exchange.Received.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await session.SendAsync("next", CancellationToken.None);

        Assert.Empty(failures);
        Assert.Equal([true, false, true, false], busy);
        http.Verify();
    }

    private static ServiceProvider CreateServices(HttpSimulation http)
    {
        var services = new ServiceCollection();
        services.AddHttpSimulation(http);
        services.AddHttpClient("mcs");
        return services.BuildServiceProvider();
    }

    private static ConversationSession CreateSession(IServiceProvider services)
    {
        var client = new CopilotClient(
            new ConnectionSettings { DirectConnectUrl = StartUrl, Cloud = PowerPlatformCloud.Prod },
            services.GetRequiredService<IHttpClientFactory>(),
            _ => Task.FromResult("synthetic-copilot-token"),
            NullLogger.Instance,
            "mcs");
        return new ConversationSession(new CopilotConversationClient(client), new ActivityJournal(), new ActivityInterpreter());
    }

    private static string ExecuteUrl(ConversationSession session) =>
        $"https://copilot.example.test/copilotstudio/dataverse-backed/authenticated/bots/fixture/conversations/{session.ConversationId}?api-version=2022-03-01-preview";

    private static void RespondActivities(HttpExpectation exchange, string conversationId, string text) =>
        exchange.Respond(HttpStatusCode.OK, response =>
        {
            string data = "event: activity\ndata: " + JsonSerializer.Serialize(new
            {
                type = "message",
                id = "response-1",
                text,
                conversation = new { id = conversationId },
                from = new { id = "copilot", role = "bot" }
            }) + "\n\n";
            response.Content = new StringContent(data, Encoding.UTF8);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        });
}
