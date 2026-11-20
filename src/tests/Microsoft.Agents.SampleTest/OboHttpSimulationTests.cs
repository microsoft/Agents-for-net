// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OBOAuthorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class OboHttpSimulationTests
{
    private const string StartUrl = "https://copilot.example.test/copilotstudio/dataverse-backed/authenticated/bots/fixture/conversations?api-version=2022-03-01-preview";
    private const string ContinueUrl = "https://copilot.example.test/copilotstudio/dataverse-backed/authenticated/bots/fixture/conversations/remote-42?api-version=2022-03-01-preview";

    [Fact]
    public async Task StartsOnAnEventThenReusesTheCopilotConversationForLaterQuestions()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, StartUrl)
            .ForClient("mcs")
            .WithHeader("Authorization", "Bearer synthetic-exchanged-token")
            .WithJsonBody(body => body.GetProperty("emitStartConversationEvent").GetBoolean())
            .RespondJson(new
            {
                conversationId = "remote-42",
                activities = new object[]
                {
                    new { type = "typing", conversation = new { id = "remote-42" } },
                    new { type = "message", text = "Welcome from Copilot", conversation = new { id = "remote-42" } }
                }
            });
        http.Expect(HttpMethod.Post, ContinueUrl)
            .ForClient("mcs")
            .WithHeader("Authorization", "Bearer synthetic-exchanged-token")
            .WithJsonBody(body =>
                body.GetProperty("activity").GetProperty("text").GetString() == "Where is Seattle?" &&
                body.GetProperty("activity").GetProperty("conversation").GetProperty("id").GetString() == "remote-42")
            .RespondJson(new
            {
                activities = new[] { new { type = "message", text = "In Washington.", conversation = new { id = "remote-42" } } }
            });

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow()
                .Send(new Activity { Type = ActivityTypes.Event, Name = "start" })
                .AssertReply("Welcome from Copilot")
                .Send("Where is Seattle?")
                .AssertReply("In Washington.")
                .Send("-signout")
                .AssertReply("You have signed out")
                .StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopilotFailurePropagatesWithoutInventingAReply(bool networkFault)
    {
        using var http = new HttpSimulation();
        var start = http.Expect(HttpMethod.Post, StartUrl);
        if (networkFault)
        {
            start.Throw(() => new HttpRequestException("Copilot transport unavailable"));
        }
        else
        {
            start.RespondJson(new { error = new { code = "Forbidden", message = "Access denied" } }, HttpStatusCode.Forbidden);
        }

        await using (var host = CreateHost(http))
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => host.CreateTestFlow().Send("hello").StartTestAsync());
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    private static AgentTestHost CreateHost(HttpSimulation http)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["CopilotStudioAgent:DirectConnectUrl"] = StartUrl,
            ["CopilotStudioAgent:Cloud"] = "Prod"
        }).Build();

        return HttpSampleHost.Create(http, (options, services) =>
        {
            var authorization = new Mock<IUserAuthorization>();
            authorization.SetupGet(handler => handler.Name).Returns("mcs");
            authorization.Setup(handler => handler.SignInUserAsync(
                It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new TokenResponse { Token = "synthetic-exchangeable-token", IsExchangeable = true });
            authorization.Setup(handler => handler.GetRefreshedUserTokenAsync(
                It.IsAny<ITurnContext>(), It.IsAny<string>(),
                It.Is<IList<string>>(scopes => scopes.SequenceEqual(new[] { "https://api.powerplatform.com/.default" })),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new TokenResponse { Token = "synthetic-exchanged-token" });
            options.UserAuthorization = new UserAuthorizationOptions(
                NullLoggerFactory.Instance, services.GetRequiredService<IStorage>(), new Mock<IConnections>().Object, authorization.Object)
            {
                DefaultHandlerName = "mcs"
            };
            return OboAgentFactory.Create(options, options.HttpClientFactory, configuration);
        });
    }
}
