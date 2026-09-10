// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class AgentApplicationAttributesTests
{
    private static Mock<ITurnContext> CreateTurnContext(Activity activity)
    {
        var turnContext = new Mock<ITurnContext>();
        turnContext.Setup(c => c.Activity).Returns(activity);
        turnContext.SetupGet(c => c.Services).Returns(new TurnContextStateCollection());
        turnContext
            .Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceResponse());
        return turnContext;
    }

    private static Activity MakeActivity(string type, string text = null) =>
        new() { Type = type, ChannelId = Channels.A2A, Text = text };

    // ---------------------------------------------------------------------------
    // A2AMessageRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task A2AMessageRoute_Any()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(MakeActivity(ActivityTypes.Message));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnAnyMessage", app.calls[0]);
    }

    [Fact]
    public async Task A2AMessageRoute_Text()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(MakeActivity(ActivityTypes.Message, "-test"));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnTest", app.calls[0]);
    }

    [Fact]
    public async Task A2AMessageRoute_Regex()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(MakeActivity(ActivityTypes.Message, "testActivity"));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnRegEx", app.calls[0]);
    }

    [Fact]
    public async Task A2AMessageRoute_DoesNotFire_ForNonA2AChannel()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(new Activity { Type = ActivityTypes.Message, ChannelId = Channels.Msteams });

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Empty(app.calls);
    }

    // ---------------------------------------------------------------------------
    // A2ARouteHandler (IA2ATurnContext) delivery
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task A2AMessageRoute_TypedContext_ReceivesIA2ATurnContext()
    {
        var app = new TypedMessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(new A2AMessageActivity { Type = ActivityTypes.Message, ChannelId = Channels.A2A });

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnTypedMessage", app.calls[0]);
        Assert.IsAssignableFrom<IA2ATurnContext>(app.captured);
        Assert.IsAssignableFrom<IA2AActivity>(app.captured.Activity);
    }
}

// ---------------------------------------------------------------------------
// Test agent apps
// ---------------------------------------------------------------------------

class MessageRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [A2AMessageRoute]
    public Task OnAnyMessage(IA2ATurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAnyMessage"); return Task.CompletedTask; }

    [A2AMessageRoute(text: "-test")]
    public Task OnTest(IA2ATurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnTest"); return Task.CompletedTask; }

    [A2AMessageRoute(textRegex: "test.*")]
    public Task OnRegEx(IA2ATurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnRegEx"); return Task.CompletedTask; }
}

class TypedMessageRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];
    public IA2ATurnContext captured;

    [A2AMessageRoute]
    public Task OnTypedMessage(IA2ATurnContext ctx, ITurnState state, CancellationToken ct)
    {
        calls.Add("OnTypedMessage");
        captured = ctx;
        return Task.CompletedTask;
    }
}
