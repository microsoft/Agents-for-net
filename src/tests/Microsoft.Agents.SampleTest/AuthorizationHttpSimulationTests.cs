// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AutoSignIn;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class AutoSignInHttpSimulationTests
{
    [Fact]
    public async Task DefaultSignInTokenIsUsedForDisplayNameAndEcho()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, AuthorizationHttpFixtures.GraphMeUrl)
            .WithHeader("Authorization", "Bearer synthetic-auto-token")
            .RespondJson(new { displayName = "Ada Lovelace", id = "profile-id" });

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send("hello").AssertReply("**Ada Lovelace said:** hello").StartTestAsync();
        }

        http.Verify();
    }

    [Fact]
    public async Task MeRouteUsesItsOwnTokenForDetailedProfile()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, AuthorizationHttpFixtures.GraphMeUrl)
            .WithHeader("Authorization", "Bearer synthetic-auto-token")
            .RespondJson(new { displayName = "Ada Lovelace", id = "profile-id" });
        http.Expect(HttpMethod.Get, AuthorizationHttpFixtures.GraphMeUrl)
            .WithHeader("Authorization", "Bearer synthetic-me-token")
            .RespondJson(new { displayName = "Ada Lovelace", jobTitle = "Engineer", mail = "ada@example.test", id = "profile-id" });

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send("-me")
                .AssertReply($"Name: Ada Lovelace{Environment.NewLine}Job Title: Engineer{Environment.NewLine}Email: ada@example.test")
                .StartTestAsync();
        }

        http.Verify();
    }

    [Theory]
    [InlineData("forbidden")]
    [InlineData("malformed")]
    [InlineData("network")]
    public async Task GraphFailureRetainsTheExistingSignOutGuidance(string outcome)
    {
        using var http = new HttpSimulation();
        AuthorizationHttpFixtures.ArrangeFailure(http, outcome);

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send("hello")
                .AssertReplyContains("Failed to get user information from Graph API")
                .StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    private static AgentTestHost CreateHost(HttpSimulation http) =>
        HttpSampleHost.Create(http, (options, services) =>
        {
            options.UserAuthorization = AuthorizationHttpFixtures.CreateAuthorization(
                services.GetRequiredService<IStorage>(), "auto", "me");
            return new AuthAgent(options);
        });
}

public class CopilotConnectorHttpSimulationTests
{
    [Fact]
    public async Task ConnectorUserGetsTheGraphDisplayName()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Get, AuthorizationHttpFixtures.GraphMeUrl)
            .WithHeader("Authorization", "Bearer synthetic-user-token")
            .RespondJson(new { displayName = "Grace Hopper", id = "profile-id" });

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send("hello").AssertReply("Hi, Grace Hopper").StartTestAsync();
        }

        http.Verify();
    }

    [Theory]
    [InlineData("forbidden")]
    [InlineData("malformed")]
    [InlineData("network")]
    public async Task GraphFailureRetainsUnknownNameFallback(string outcome)
    {
        using var http = new HttpSimulation();
        AuthorizationHttpFixtures.ArrangeFailure(http, outcome);

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send("hello").AssertReply("Hi, Unknown").StartTestAsync();
        }

        http.Verify();
    }

    [Fact]
    public async Task NonConnectorRecipientDoesNotCallGraph()
    {
        using var http = new HttpSimulation();
        await using (var host = HttpSampleHost.Create(http,
            (options, _) => new CopilotStudioAgentConnector.MyAgent(options), signIn: true))
        {
            await host.CreateTestFlow().Send("hello").StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }
        http.Verify();
    }

    private static AgentTestHost CreateHost(HttpSimulation http)
    {
        var host = HttpSampleHost.Create(http,
            (options, _) => new CopilotStudioAgentConnector.MyAgent(options), signIn: true);
        host.Adapter.Conversation.Agent.Role = RoleTypes.ConnectorUser;
        return host;
    }
}

internal static class AuthorizationHttpFixtures
{
    internal const string GraphMeUrl = "https://graph.microsoft.com/v1.0/me";

    internal static void ArrangeFailure(HttpSimulation http, string outcome)
    {
        var request = http.Expect(HttpMethod.Get, GraphMeUrl);
        switch (outcome)
        {
            case "forbidden":
                request.RespondJson(new { error = new { code = "Authorization_RequestDenied", message = "Access denied" } }, HttpStatusCode.Forbidden);
                break;
            case "malformed":
                request.RespondText("{not json}", "application/json");
                break;
            case "network":
                request.Throw(() => new HttpRequestException("Graph transport unavailable"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    internal static UserAuthorizationOptions CreateAuthorization(IStorage storage, string defaultHandler, params string[] otherHandlers)
    {
        var handlers = new List<IUserAuthorization> { CreateHandler(defaultHandler) };
        handlers.AddRange(Array.ConvertAll(otherHandlers, CreateHandler));
        return new UserAuthorizationOptions(NullLoggerFactory.Instance, storage, new Mock<IConnections>().Object, handlers.ToArray())
        {
            DefaultHandlerName = defaultHandler,
            AutoSignIn = UserAuthorizationOptions.AutoSignInOnForAny
        };
    }

    internal static IUserAuthorization CreateHandler(string name)
    {
        var handler = new Mock<IUserAuthorization>();
        handler.SetupGet(value => value.Name).Returns(name);
        handler.Setup(value => value.SignInUserAsync(
            It.IsAny<ITurnContext>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Token(name));
        handler.Setup(value => value.GetRefreshedUserTokenAsync(
            It.IsAny<ITurnContext>(), It.IsAny<string>(), It.IsAny<IList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Token(name));
        return handler.Object;
    }

    private static TokenResponse Token(string name) => new()
    {
        Token = $"synthetic-{name}-token",
        Expiration = DateTimeOffset.UtcNow.AddHours(1)
    };
}
