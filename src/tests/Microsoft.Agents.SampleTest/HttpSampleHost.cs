// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.SampleTest;

internal static class HttpSampleHost
{
    internal const string ServiceUrl = "https://channel.example.test/";

    internal static AgentTestHost Create(
        HttpSimulation http,
        Func<AgentApplicationOptions, IServiceProvider, IAgent> createAgent,
        string channel = Channels.Test,
        bool signIn = false,
        Action<IServiceCollection> configure = null)
    {
        var host = AgentTestHost.Create(builder =>
        {
            builder.Services.AddHttpSimulation(http);
            builder.Services.AddSingleton<IStorage, MemoryStorage>();
            configure?.Invoke(builder.Services);
            builder.Services.AddTransient<IAgent>(services =>
            {
                var storage = services.GetRequiredService<IStorage>();
                var options = new AgentApplicationOptions(storage, NullLoggerFactory.Instance)
                {
                    HttpClientFactory = services.GetRequiredService<IHttpClientFactory>(),
                    StartTypingTimer = false
                };
                if (signIn)
                {
                    options.UserAuthorization = CreateAuthorization(storage);
                }
                return createAgent(options, services);
            });
        });
        host.Adapter.Conversation = new ConversationReference
        {
            ChannelId = channel,
            ServiceUrl = ServiceUrl,
            User = new ChannelAccount("user-id", "Incoming User"),
            Agent = new ChannelAccount("agent-id", "Agent"),
            Conversation = new ConversationAccount(id: "conversation-id")
            {
                ConversationType = "channel",
                TenantId = "tenant-id"
            }
        };
        host.Adapter.Use(new RestClientsMiddleware(new SimulationClientFactory(http)));
        return host;
    }

    internal static Activity TeamsMessage(string text) => new()
    {
        Type = ActivityTypes.Message,
        Text = text,
        ChannelId = Channels.Msteams,
        ChannelData = JsonSerializer.SerializeToElement(new { tenant = new { id = "tenant-id" }, team = new { id = "team-id" } })
    };

    internal static Activity TeamsInvoke(string name, object value) => new()
    {
        Type = ActivityTypes.Invoke,
        Name = name,
        ChannelId = Channels.Msteams,
        Value = value
    };

    private static UserAuthorizationOptions CreateAuthorization(IStorage storage)
    {
        var handler = new Mock<IUserAuthorization>();
        handler.SetupGet(value => value.Name).Returns("user");
        handler.Setup(value => value.SignInUserAsync(
            It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(),
            It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenResponse(token: "synthetic-user-token"));
        handler.Setup(value => value.GetRefreshedUserTokenAsync(
            It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TokenResponse(token: "synthetic-user-token"));
        return new UserAuthorizationOptions(
            NullLoggerFactory.Instance, storage, new Mock<IConnections>().Object, handler.Object)
        {
            DefaultHandlerName = "user",
            AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
        };
    }

    internal sealed class SimulationClientFactory(HttpSimulation simulation) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => simulation.CreateClient(name);
    }

    private sealed class RestClientsMiddleware(IHttpClientFactory clients) : IMiddleware
    {
        public async Task OnTurnAsync(ITurnContext context, NextDelegate next, CancellationToken cancellationToken)
        {
            using var connector = new RestConnectorClient(
                new Uri(ServiceUrl), clients, () => Task.FromResult("synthetic-agent-token"));
            using var tokens = new RestUserTokenClient(
                "agent-id", new Uri("https://tokens.example.test/"), clients,
                () => Task.FromResult("synthetic-agent-token"));
            context.Services.Set<IConnectorClient>(connector);
            context.Services.Set<IUserTokenClient>(tokens);
            await next(cancellationToken);
        }
    }
}
