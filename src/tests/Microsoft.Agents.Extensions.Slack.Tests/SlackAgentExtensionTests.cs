// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Storage;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackAgentExtensionTests
{
    private static (AgentApplication app, Mock<IHttpClientFactory> httpFactory) CreateApplication()
    {
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var options = new AgentApplicationOptions(new MemoryStorage())
        {
            StartTypingTimer = false,
            RemoveRecipientMention = false,
            HttpClientFactory = mockHttpFactory.Object
        };

        return (new TestSlackAgent(options), mockHttpFactory);
    }

    private static IActivity CreateSlackMessageActivity(string text = "hello")
    {
        return new Activity(
            type: ActivityTypes.Message,
            text: text,
            channelId: Microsoft.Agents.Core.Models.Channels.Slack,
            recipient: new() { Id = "botId" },
            conversation: new() { Id = "conversationId" },
            from: new() { Id = "userId" }
        );
    }

    private static IActivity CreateNonSlackMessageActivity(string text = "hello")
    {
        return new Activity(
            type: ActivityTypes.Message,
            text: text,
            channelId: Microsoft.Agents.Core.Models.Channels.Msteams,
            recipient: new() { Id = "botId" },
            conversation: new() { Id = "conversationId" },
            from: new() { Id = "userId" }
        );
    }

    private static IActivity CreateSlackEventActivity(string name = "app_mention")
    {
        return new Activity(
            type: ActivityTypes.Event,
            channelId: Microsoft.Agents.Core.Models.Channels.Slack,
            recipient: new() { Id = "botId" },
            conversation: new() { Id = "conversationId" },
            from: new() { Id = "userId" }
        )
        { Name = name };
    }

    private static IActivity CreateNonSlackEventActivity(string name = "app_mention")
    {
        return new Activity(
            type: ActivityTypes.Event,
            channelId: Microsoft.Agents.Core.Models.Channels.Msteams,
            recipient: new() { Id = "botId" },
            conversation: new() { Id = "conversationId" },
            from: new() { Id = "userId" }
        )
        { Name = name };
    }

    private class TestSlackAgent : AgentApplication
    {
        public TestSlackAgent(AgentApplicationOptions options) : base(options) { }
    }

    private class NotImplementedAdapter : ChannelAdapter
    {
        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void Constructor_SetsChannelId_ToSlack()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        Assert.Equal(Microsoft.Agents.Core.Models.Channels.Slack, ext.ChannelId);
    }

    [Fact]
    public async Task OnBeforeTurn_RegistersSlackApi_ForSlackActivity()
    {
        var (app, _) = CreateApplication();
        _ = new SlackAgentExtension(app);

        var activity = CreateSlackMessageActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        var slackApi = turnContext.Services.Get<SlackApi>();
        Assert.NotNull(slackApi);
    }

    [Fact]
    public async Task OnBeforeTurn_DoesNotRegisterSlackApi_ForNonSlackActivity()
    {
        var (app, _) = CreateApplication();
        _ = new SlackAgentExtension(app);

        var activity = CreateNonSlackMessageActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        var slackApi = turnContext.Services.Get<SlackApi>();
        Assert.Null(slackApi);
    }

    [Fact]
    public void OnMessage_NoParams_ReturnsThis()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext.OnMessage((ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnMessage_WithText_ReturnsThis()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext.OnMessage("hello", (ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnMessage_WithRegex_ReturnsThis()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext.OnMessage(new Regex("hello.*"), (ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnEvent_NoParams_ReturnsThis()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext.OnEvent((ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnEvent_WithName_ReturnsThis()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext.OnEvent("app_mention", (ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnEvent_WithRegex_ReturnsThis()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext.OnEvent(new Regex("app_.*"), (ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnMessage_SupportsChaining()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext
            .OnMessage((ctx, state, ct) => Task.CompletedTask)
            .OnMessage("hi", (ctx, state, ct) => Task.CompletedTask)
            .OnMessage(new Regex("bye"), (ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public void OnEvent_SupportsChaining()
    {
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var result = ext
            .OnEvent((ctx, state, ct) => Task.CompletedTask)
            .OnEvent("reaction_added", (ctx, state, ct) => Task.CompletedTask)
            .OnEvent(new Regex("reaction_.*"), (ctx, state, ct) => Task.CompletedTask);

        Assert.Same(ext, result);
    }

    [Fact]
    public async Task CallAsync_DelegatesToSlackApiFromServices()
    {
        var handler = new TestDelegatingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var slackApi = new SlackApi(mockHttpFactory.Object);

        var activity = CreateSlackMessageActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
        turnContext.Services.Set(slackApi);

        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);

        var response = await ext.CallAsync(turnContext, "chat.postMessage", new { channel = "C123", text = "hi" }, "xoxb-token");

        Assert.NotNull(response);
        Assert.True(response.ok);
    }

    [Fact]
    public async Task OnMessage_Handler_InvokedForSlackMessage()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnMessage((ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackMessageActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.True(handlerInvoked);
    }

    [Fact]
    public async Task OnMessage_Handler_NotInvokedForNonSlackMessage()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnMessage((ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateNonSlackMessageActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task OnEvent_Handler_InvokedForSlackEvent()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnEvent((ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackEventActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.True(handlerInvoked);
    }

    [Fact]
    public async Task OnEvent_Handler_NotInvokedForNonSlackEvent()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnEvent((ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateNonSlackEventActivity();
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task OnMessage_WithText_MatchesCorrectly()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnMessage("hello", (ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackMessageActivity("hello");
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.True(handlerInvoked);
    }

    [Fact]
    public async Task OnMessage_WithText_DoesNotMatchDifferentText()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnMessage("hello", (ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackMessageActivity("goodbye");
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task OnMessage_WithRegex_MatchesPattern()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnMessage(new Regex(@"^hello\s"), (ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackMessageActivity("hello world");
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.True(handlerInvoked);
    }

    [Fact]
    public async Task OnEvent_WithName_MatchesCorrectly()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnEvent("app_mention", (ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackEventActivity("app_mention");
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.True(handlerInvoked);
    }

    [Fact]
    public async Task OnEvent_WithName_DoesNotMatchDifferentEvent()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnEvent("app_mention", (ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackEventActivity("reaction_added");
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.False(handlerInvoked);
    }

    [Fact]
    public async Task OnEvent_WithRegex_MatchesPattern()
    {
        var handlerInvoked = false;
        var (app, _) = CreateApplication();
        var ext = new SlackAgentExtension(app);
        ext.OnEvent(new Regex(@"^app_"), (ctx, state, ct) =>
        {
            handlerInvoked = true;
            return Task.CompletedTask;
        });

        var activity = CreateSlackEventActivity("app_mention");
        var turnContext = new TurnContext(new NotImplementedAdapter(), activity);

        await app.OnTurnAsync(turnContext, CancellationToken.None);

        Assert.True(handlerInvoked);
    }

    private class TestDelegatingHandler : DelegatingHandler
    {
        private readonly HttpResponseMessage _response;

        public TestDelegatingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
