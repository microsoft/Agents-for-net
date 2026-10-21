extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2AAgentSample::A2AAgent;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AProactiveSampleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MappedStreamingEndpoints_KeepAgentContextUntilResultExecution(bool jsonRpc)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        A2AAgentStartup.ConfigureBuilder(builder);
        await using var app = builder.Build();
        A2AAgentStartup.ConfigureApplication(app);
        await app.StartAsync(timeout.Token);
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };
        var request = new SendMessageRequest
        {
            Message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                Role = Role.User,
                Parts = [new Part { Text = "Streaming hello" }],
            },
        };
        using var body = jsonRpc
            ? Json(new JsonRpcRequest
            {
                Id = "stream-test",
                Method = A2AMethods.SendStreamingMessage,
                Params = JsonSerializer.SerializeToElement(request, A2AJsonUtilities.DefaultOptions),
            })
            : Json(request);
        using var response = await client.PostAsync(jsonRpc ? "/a2a" : "/a2a/message:stream", body, timeout.Token);
        var text = await response.Content.ReadAsStringAsync(timeout.Token);
        Assert.True(response.IsSuccessStatusCode, text);
        Assert.Contains("You said: Streaming hello", text);
        await app.StopAsync(timeout.Token);
    }

    [Fact]
    public async Task SampleStartup_UsesUpstreamPushUrlPolicyWithoutFixedCallback()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "A2AAgent.appsettings.json"));
        A2AAgentStartup.ConfigureBuilder(builder);
        await using var app = builder.Build();
        var policy = app.Services.GetRequiredService<IPushNotificationUrlValidator>();

        var addresses = await policy.ValidateAsync("https://8.8.8.8/notify");

        Assert.Equal(IPAddress.Parse("8.8.8.8"), Assert.Single(addresses));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProactiveHttpEndpoints_PersistTaskUpdatesAndDeliverWebhook(bool stored)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var notifications = Channel.CreateUnbounded<(StreamResponse Event, string Authorization)>();
        var receiverBuilder = WebApplication.CreateSlimBuilder();
        receiverBuilder.Logging.ClearProviders();
        receiverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receiver = receiverBuilder.Build();
        receiver.MapPost("/notify", async (HttpContext context) =>
        {
            var update = await JsonSerializer.DeserializeAsync<StreamResponse>(
                context.Request.Body, A2AJsonUtilities.DefaultOptions, context.RequestAborted);
            await notifications.Writer.WriteAsync((update!, context.Request.Headers.Authorization.ToString()), context.RequestAborted);
        });
        await receiver.StartAsync(ct);
        var callback = Address(receiver) + "/notify";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        A2AAgentStartup.ConfigureBuilder(builder);
        builder.Services.AddSingleton<IPushNotificationUrlValidator>(new LoopbackReceiverUrlValidator(callback));
        await using var app = builder.Build();
        A2AAgentStartup.ConfigureApplication(app);
        await app.StartAsync(ct);
        using var client = new HttpClient { BaseAddress = new Uri(Address(app)) };

        var request = new SendMessageRequest
        {
            Message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                Role = Role.User,
                Parts = [new Part { Text = stored ? "-proactive-store" : "-proactive" }],
            },
        };
        using var initialResponse = await client.PostAsync("/a2a/message:send", Json(request), ct);
        var initialBody = await initialResponse.Content.ReadAsStringAsync(ct);
        Assert.True(initialResponse.IsSuccessStatusCode, initialBody);
        var initial = JsonSerializer.Deserialize<SendMessageResponse>(initialBody, A2AJsonUtilities.DefaultOptions)!;
        Assert.NotNull(initial.Task);
        Assert.Equal(TaskState.InputRequired, initial.Task.Status.State);
        var taskId = initial.Task.Id;
        Assert.NotNull(initial.Task.Status.Message);
        var data = Assert.IsType<JsonElement>(initial.Task.Status.Message.Parts.Single(part => part.Data != null).Data);
        var conversation = ProtocolJsonSerializer.ToObject<Conversation>(data);
        var agent = app.Services.GetRequiredService<MyAgent>();
        Assert.Equal(stored, await agent.Proactive.GetConversationAsync(taskId, ct) != null);

        Task<HttpResponseMessage> TriggerAsync(int count) => stored
            ? client.PostAsync($"/proactive/sendactivity/{taskId}",
                new StringContent($"{{\"type\":\"message\",\"text\":\"External update {count}\"}}", Encoding.UTF8, "application/json"), ct)
            : client.PostAsync("/proactive/continue",
                new StringContent(ProtocolJsonSerializer.ToJson(conversation), Encoding.UTF8, "application/json"), ct);

        using (var unregistered = await TriggerAsync(0))
        {
            Assert.Equal(HttpStatusCode.InternalServerError, unregistered.StatusCode);
            var unchanged = await app.Services.GetRequiredService<ITaskStore>().GetTaskAsync(taskId, ct);
            Assert.Equal(TaskState.InputRequired, unchanged!.Status.State);
        }

        using var registration = await client.PostAsync($"/a2a/tasks/{taskId}/pushNotificationConfigs", Json(new TaskPushNotificationConfig
        {
            Url = callback,
            Authentication = new AuthenticationInfo { Scheme = "bearer", Credentials = "test-push-credential" },
        }), ct);
        Assert.True(registration.IsSuccessStatusCode, await registration.Content.ReadAsStringAsync(ct));

        for (var count = 1; count <= 2; count++)
        {
            using var response = await TriggerAsync(count);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(ct));
            var expected = stored ? $"External update {count}" : $"Proactive continuation {count}";
            (StreamResponse Event, string Authorization) notification;
            do
            {
                notification = await notifications.Reader.ReadAsync(ct);
            }
            while (notification.Event.StatusUpdate?.Status.Message?.Parts.FirstOrDefault()?.Text != expected);
            var received = notification.Event;
            var authentication = AuthenticationHeaderValue.Parse(notification.Authorization);
            Assert.Equal("bearer", authentication.Scheme, ignoreCase: true);
            Assert.Equal("test-push-credential", authentication.Parameter);
            Assert.NotNull(received.StatusUpdate);
            Assert.Equal(taskId, received.StatusUpdate.TaskId);
            Assert.Equal(initial.Task.ContextId, received.StatusUpdate.ContextId);
            if (!stored)
            {
                var continuationData = Assert.IsType<JsonElement>(received.StatusUpdate.Status.Message.Parts.Single(part => part.Data != null).Data);
                Assert.Equal(taskId, continuationData.GetProperty("taskId").GetString());
            }
            var persisted = await app.Services.GetRequiredService<ITaskStore>().GetTaskAsync(taskId, ct);
            Assert.Equal(TaskState.Working, persisted!.Status.State);
            Assert.NotNull(persisted.Status.Message);
            Assert.Equal(expected, persisted.Status.Message.Parts[0].Text);
        }

        await app.StopAsync(ct);
        await receiver.StopAsync(ct);
    }

    private static StringContent Json<T>(T value)
        => new(JsonSerializer.Serialize(value, A2AJsonUtilities.DefaultOptions), Encoding.UTF8, "application/json");

    private static string Address(WebApplication app)
        => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();

    // Restrict the test transport to its ephemeral loopback receiver; the sample uses the upstream policy.
    private sealed class LoopbackReceiverUrlValidator(string callback) : IPushNotificationUrlValidator
    {
        public Task<IReadOnlyList<IPAddress>> ValidateAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(url, callback, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected test push destination.");
            }
            return Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
        }
    }
}
