// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Kiota.Http.HttpClientLibrary.Middleware.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App;

public class GraphTransportTests
{
    [Fact]
    public void Graph_TransportOverrideIsOptIn()
    {
        var connections = new Mock<IConnections>();
        connections.Setup(value => value.GetConnection("graph")).Returns(new Mock<IAccessTokenProvider>().Object);
        var teams = new TeamsAgentExtension(new AgentApplication(new AgentApplicationOptions((IStorage)null)
        {
            Connections = connections.Object,
            HttpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict).Object
        }));

        // An arbitrary factory may auto-follow redirects; Graph must retain its native default transport.
        using var graph = teams.GetAppGraphClientForConnection("graph");
    }

    [Theory]
    [InlineData("extension")]
    [InlineData("connection")]
    [InlineData("context")]
    [InlineData("context-connection")]
    public async Task AppGraph_UsesInjectedTransportAndNationalCloudScopes(string entryPoint)
    {
        var factory = new RecordingFactory();
        string resource = null;
        IList<string> scopes = null;
        var provider = new Mock<IAccessTokenProvider>();
        provider.Setup(value => value.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>()))
            .Callback<string, IList<string>, bool>((target, requested, _) => { resource = target; scopes = requested; })
            .ReturnsAsync("synthetic-token");
        var connections = new Mock<IConnections>();
        connections.Setup(value => value.GetTokenProvider(It.IsAny<ClaimsIdentity>(), It.IsAny<IActivity>()))
            .Returns(provider.Object);
        connections.Setup(value => value.GetConnection("graph")).Returns(provider.Object);
        var app = new AgentApplication(new AgentApplicationOptions((IStorage)null)
        {
            Connections = connections.Object
        });
        var teams = new TeamsAgentExtension(app) { GraphHttpMessageHandlerFactory = factory.CreateHandler };
        using var context = new TurnContext(new TestAdapter(), new Activity { ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams });
        context.Services.Set(teams);
        context.Services.Set<IConnections>(connections.Object);
        var wrapped = new TeamsTurnContext(context);
        using var graph = entryPoint switch
        {
            "connection" => teams.GetAppGraphClientForConnection("graph", "https://graph.microsoft.us/v1.0"),
            "context" => wrapped.GetAppGraphClient("https://graph.microsoft.us/v1.0"),
            "context-connection" => wrapped.GetAppGraphClientForConnection("graph", "https://graph.microsoft.us/v1.0"),
            _ => teams.GetAppGraphClient(context, "https://graph.microsoft.us/v1.0")
        };

        // Guard before sending: the pre-feature implementation must not reach a real Graph endpoint.
        Assert.Equal(1, factory.HandlerCount);
        var user = await graph.Users["test-user"].GetAsync();

        Assert.Equal("Synthetic User", user.DisplayName);
        Assert.Equal("https://graph.microsoft.us/v1.0/users/test-user", factory.Handler.RequestUri);
        Assert.Equal("Bearer synthetic-token", factory.Handler.Authorization);
        Assert.Contains("graph-dotnet", factory.Handler.SdkVersion);
        Assert.Equal("https://graph.microsoft.us", resource);
        Assert.Equal(new[] { "https://graph.microsoft.us/.default" }, scopes);
        graph.Dispose();
        Assert.True(factory.Handler.Disposed);
    }

    [Fact]
    public async Task AppGraph_RetainsGraphRetryMiddleware()
    {
        var factory = new RecordingFactory { FailFirst = true };
        var provider = new Mock<IAccessTokenProvider>();
        provider.Setup(value => value.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>()))
            .ReturnsAsync("synthetic-token");
        var connections = new Mock<IConnections>();
        connections.Setup(value => value.GetConnection("graph")).Returns(provider.Object);
        var teams = new TeamsAgentExtension(new AgentApplication(new AgentApplicationOptions((IStorage)null)
        {
            Connections = connections.Object
        }))
        { GraphHttpMessageHandlerFactory = factory.CreateHandler };
        using var graph = teams.GetAppGraphClientForConnection("graph");

        Assert.Equal(1, factory.HandlerCount);
        var user = await graph.Users["test-user"].GetAsync(request =>
            request.Options.Add(new RetryHandlerOption { Delay = 0, MaxRetry = 1 }));

        Assert.Equal("Synthetic User", user.DisplayName);
        Assert.Equal(2, factory.Handler.CallCount);
    }

    [Fact]
    public async Task AppGraph_RetriesSimulatedServiceFailureThroughRealGraphMiddleware()
    {
        using var http = new HttpSimulation();
        http.ExpectSequence(HttpMethod.Get, "https://graph.microsoft.com/v1.0/users/test-user")
            .Respond(HttpStatusCode.ServiceUnavailable, response =>
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero))
            .RespondJson(new { id = "test-user", displayName = "Recovered User" });
        var provider = new Mock<IAccessTokenProvider>();
        provider.Setup(value => value.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>()))
            .ReturnsAsync("synthetic-token");
        var connections = new Mock<IConnections>();
        connections.Setup(value => value.GetConnection("graph")).Returns(provider.Object);
        var teams = new TeamsAgentExtension(new AgentApplication(new AgentApplicationOptions((IStorage)null)
        {
            Connections = connections.Object
        }))
        {
            GraphHttpMessageHandlerFactory = () => http.CreateHandler()
        };
        using var graph = teams.GetAppGraphClientForConnection("graph");

        var user = await graph.Users["test-user"].GetAsync(request =>
            request.Options.Add(new RetryHandlerOption { Delay = 0, MaxRetry = 1 }));

        Assert.Equal("Recovered User", user.DisplayName);
        http.Verify();
    }

    [Fact]
    public async Task AppGraph_RetryRetainsBodyWhenInnerPipelineReplacesContent()
    {
        var factory = new RecordingFactory { FailFirst = true, RewriteContent = true };
        var provider = new Mock<IAccessTokenProvider>();
        provider.Setup(value => value.GetAccessTokenAsync(It.IsAny<string>(), It.IsAny<IList<string>>()))
            .ReturnsAsync("synthetic-token");
        var connections = new Mock<IConnections>();
        connections.Setup(value => value.GetConnection("graph")).Returns(provider.Object);
        var teams = new TeamsAgentExtension(new AgentApplication(new AgentApplicationOptions((IStorage)null)
        {
            Connections = connections.Object
        }))
        { GraphHttpMessageHandlerFactory = factory.CreateHandler };
        using var graph = teams.GetAppGraphClientForConnection("graph");

        Assert.Equal(1, factory.HandlerCount);
        var user = await graph.Users.PostAsync(new Microsoft.Graph.Models.User { DisplayName = "Synthetic User" },
            request => request.Options.Add(new RetryHandlerOption { Delay = 0, MaxRetry = 1 }));

        Assert.Equal("test-user", user.Id);
        Assert.Equal(2, factory.Handler.RequestBodies.Count);
        Assert.All(factory.Handler.RequestBodies, body =>
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("Synthetic User", json.RootElement.GetProperty("displayName").GetString());
        });
    }

    private sealed class RecordingFactory
    {
        internal RecordingHandler Handler { get; private set; }
        internal int HandlerCount { get; private set; }
        internal bool FailFirst { get; set; }
        internal bool RewriteContent { get; set; }

        internal HttpMessageHandler CreateHandler()
        {
            HandlerCount++;
            Handler = new RecordingHandler(FailFirst, RewriteContent);
            return Handler;
        }
    }

    private sealed class RecordingHandler(bool failFirst, bool rewriteContent) : HttpMessageHandler
    {
        internal string RequestUri { get; private set; }
        internal string Authorization { get; private set; }
        internal string SdkVersion { get; private set; }
        internal int CallCount { get; private set; }
        internal bool Disposed { get; private set; }
        internal List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            SdkVersion = request.Headers.TryGetValues("SdkVersion", out var versions) ? string.Join(",", versions) : "";
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                RequestBodies.Add(body);
                if (rewriteContent)
                {
                    request.Content.Dispose();
                    request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                }
            }
            var response = new HttpResponseMessage(failFirst && CallCount == 1
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"test-user","displayName":"Synthetic User"}""",
                    System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            response.Headers.TryAddWithoutValidation("Retry-After", "0");
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
