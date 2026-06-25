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

namespace Microsoft.Agents.Extensions.Slack.Tests;

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

    private static Activity SlackActivity(string type) =>
        new() { Type = type, ChannelId = Channels.Slack };

    // ---------------------------------------------------------------------------
    // SlackActivityRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackActivityRoute_ExactType()
    {
        var app = new ActivityRouteTypeApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(SlackActivity(ActivityTypes.Event));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnEvent", app.calls[0]);
    }

    [Fact]
    public async Task SlackActivityRoute_Regex()
    {
        var app = new ActivityRouteRegexApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(SlackActivity(ActivityTypes.Invoke));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnEventOrInvoke", app.calls[0]);
    }

    [Fact]
    public async Task SlackActivityRoute_Any_FiresWhenNoOtherRouteMatches()
    {
        var app = new ActivityRouteAnyApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(SlackActivity("customActivityType"));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnAny", app.calls[0]);
    }

    [Fact]
    public async Task SlackActivityRoute_DoesNotFire_ForNonSlackChannel()
    {
        var app = new ActivityRouteTypeApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(new Activity { Type = ActivityTypes.Event, ChannelId = Channels.Msteams });

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Empty(app.calls);
    }

    // ---------------------------------------------------------------------------
    // SlackInstallationUpdateRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackInstallationUpdateRoute_FiresOnInstallationUpdate()
    {
        var app = new InstallationUpdateRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(SlackActivity(ActivityTypes.InstallationUpdate));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnInstallationUpdate", app.calls[0]);
    }

    // ---------------------------------------------------------------------------
    // SlackMessageRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackMessageRoute_Any()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(SlackActivity(ActivityTypes.Message));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnAnyMessage", app.calls[0]);
    }

    [Fact]
    public async Task SlackMessageRoute_Text()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.Message);
        activity.Text = "-test";
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnTest", app.calls[0]);
    }

    [Fact]
    public async Task SlackMessageRoute_Regex()
    {
        var app = new MessageRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.Message);
        activity.Text = "testActivity";
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnRegEx", app.calls[0]);
    }

    // ---------------------------------------------------------------------------
    // SlackEventRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackEventRoute_ExactName()
    {
        var app = new EventRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.Event);
        activity.Name = "myEvent";
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnMyEvent", app.calls[0]);
    }

    [Fact]
    public async Task SlackEventRoute_NameRegex()
    {
        var app = new EventRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.Event);
        activity.Name = "mySpecialEvent";
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnRegexEvent", app.calls[0]);
    }

    [Fact]
    public async Task SlackEventRoute_Any_FiresWhenNoNamedRouteMatches()
    {
        var app = new EventRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.Event);
        activity.Name = "unknownEvent";
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnAnyEvent", app.calls[0]);
    }

    // ---------------------------------------------------------------------------
    // SlackConversationUpdateRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackConversationUpdateRoute_Any()
    {
        var app = new ConversationUpdateRouteApp(new AgentApplicationOptions((IStorage)null));
        var turnContext = CreateTurnContext(SlackActivity(ActivityTypes.ConversationUpdate));

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnAnyConversationUpdate", app.calls[0]);
    }

    [Fact]
    public async Task SlackConversationUpdateRoute_EventName()
    {
        var app = new ConversationUpdateEventApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.ConversationUpdate);
        activity.MembersAdded = new List<ChannelAccount> { new() { Id = "user1" } };
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnMembersAddedEvent", app.calls[0]);
    }

    // ---------------------------------------------------------------------------
    // SlackMembersAddedRouteAttribute / SlackMembersRemovedRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackMembersAddedRoute_FiresOnMembersAdded()
    {
        var app = new MembersAddedRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.ConversationUpdate);
        activity.MembersAdded = new List<ChannelAccount> { new() { Id = "user1" } };
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnMembersAdded", app.calls[0]);
    }

    [Fact]
    public async Task SlackMembersRemovedRoute_FiresOnMembersRemoved()
    {
        var app = new MembersRemovedRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.ConversationUpdate);
        activity.MembersRemoved = new List<ChannelAccount> { new() { Id = "user1" } };
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnMembersRemoved", app.calls[0]);
    }

    // ---------------------------------------------------------------------------
    // SlackFeedbackLoopRouteAttribute
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SlackFeedbackLoopRoute_FiresOnFeedback()
    {
        var app = new FeedbackLoopRouteApp(new AgentApplicationOptions((IStorage)null));
        var activity = SlackActivity(ActivityTypes.Invoke);
        activity.Name = "message/submitAction";
        activity.Value = new Dictionary<string, object>
        {
            { "actionName", "feedback" },
            { "actionValue", new Dictionary<string, object> { { "reaction", "like" } } }
        };
        var turnContext = CreateTurnContext(activity);

        await app.OnTurnAsync(turnContext.Object, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnFeedback", app.calls[0]);
    }
}

// ---------------------------------------------------------------------------
// Test agent apps
// ---------------------------------------------------------------------------

class ActivityRouteTypeApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackActivityRoute(ActivityTypes.Event)]
    public Task OnEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEvent"); return Task.CompletedTask; }
}

class ActivityRouteRegexApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackActivityRoute(typeRegex: "event|invoke")]
    public Task OnEventOrInvoke(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEventOrInvoke"); return Task.CompletedTask; }
}

class ActivityRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackActivityRoute(ActivityTypes.Event)]
    public Task OnEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnEvent"); return Task.CompletedTask; }

    [SlackActivityRoute]
    public Task OnAny(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAny"); return Task.CompletedTask; }
}

class InstallationUpdateRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackInstallationUpdateRoute]
    public Task OnInstallationUpdate(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnInstallationUpdate"); return Task.CompletedTask; }
}

class MessageRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackMessageRoute]
    public Task OnAnyMessage(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAnyMessage"); return Task.CompletedTask; }

    [SlackMessageRoute(text: "-test")]
    public Task OnTest(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnTest"); return Task.CompletedTask; }

    [SlackMessageRoute(textRegex: "test.*")]
    public Task OnRegEx(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnRegEx"); return Task.CompletedTask; }
}

class EventRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackEventRoute(name: "myEvent")]
    public Task OnMyEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMyEvent"); return Task.CompletedTask; }

    [SlackEventRoute(nameRegex: "my.*Event")]
    public Task OnRegexEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnRegexEvent"); return Task.CompletedTask; }

    [SlackEventRoute]
    public Task OnAnyEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAnyEvent"); return Task.CompletedTask; }
}

class ConversationUpdateRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackConversationUpdateRoute]
    public Task OnAnyConversationUpdate(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnAnyConversationUpdate"); return Task.CompletedTask; }
}

class ConversationUpdateEventApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackConversationUpdateRoute(eventName: ConversationUpdateEvents.MembersAdded)]
    public Task OnMembersAddedEvent(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMembersAddedEvent"); return Task.CompletedTask; }
}

class MembersAddedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackMembersAddedRoute]
    public Task OnMembersAdded(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMembersAdded"); return Task.CompletedTask; }
}

class MembersRemovedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackMembersRemovedRoute]
    public Task OnMembersRemoved(ITurnContext ctx, ITurnState state, CancellationToken ct) { calls.Add("OnMembersRemoved"); return Task.CompletedTask; }
}

class FeedbackLoopRouteApp(AgentApplicationOptions options) : AgentApplication(options)
{
    public List<string> calls = [];

    [SlackFeedbackLoopRoute]
    public Task OnFeedback(ITurnContext ctx, ITurnState state, FeedbackData data, CancellationToken ct) { calls.Add("OnFeedback"); return Task.CompletedTask; }
}
