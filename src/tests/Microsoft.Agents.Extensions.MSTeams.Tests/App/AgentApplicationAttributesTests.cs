// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.Tests.Model;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class AgentApplicationAttributesTests
    {
        [Fact]
        public async Task TeamsActivityRouteAttribute_ExactType()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams);
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsActivityRouteTypeApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnEvent", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsActivityRouteAttribute_Regex()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.Invoke, Microsoft.Agents.Core.Models.Channels.Msteams);
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsActivityRouteRegexApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnEventOrInvoke", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsActivityRouteAttribute_Any_FiresWhenNoOtherRouteMatches()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity("customActivityType", Microsoft.Agents.Core.Models.Channels.Msteams);
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsActivityRouteAnyApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnAny", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsInstallationUpdateRouteAttribute_FiresOnInstallationUpdate()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.InstallationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsInstallationUpdateRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnInstallationUpdate", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsMessageRouteAttribute_Text()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hi");
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsMessageRouteTextApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnHi", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsEventRouteAttribute_NameRegex()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "mySpecialEvent");
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsEventRouteRegexApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnMyRegexEvent", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsConversationUpdateRouteAttribute_Any()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsConversationUpdateRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnAnyConversationUpdate", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsMembersAddedRouteAttribute_FiresOnMembersAdded()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            activity.MembersAdded = [new ChannelAccount { Id = "user1" }];
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsMembersAddedRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnMembersAdded", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsMembersRemovedRouteAttribute_FiresOnMembersRemoved()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            activity.MembersRemoved = [new ChannelAccount { Id = "user1" }];
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsMembersRemovedRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnMembersRemoved", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsHandoffRouteAttribute_FiresOnHandoff_AndSendsInvokeResponse()
        {
            IActivity[] sentActivities = null;
            void CaptureSend(IActivity[] activities)
            {
                sentActivities = activities;
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var activity = CreateActivity(ActivityTypes.Invoke, Microsoft.Agents.Core.Models.Channels.Msteams, name: "handoff/action", value: new { Continuation = "continue-token" });
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsHandoffRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnHandoff", app.Calls[0]);
            Assert.Equal("continue-token", app.Continuation);
            Assert.NotNull(sentActivities);
            Assert.Single(sentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivities[0].Type);
        }

        [Fact]
        public async Task TeamsFeedbackLoopRouteAttribute_FiresOnFeedback_AndSendsInvokeResponse()
        {
            IActivity[] sentActivities = null;
            void CaptureSend(IActivity[] activities)
            {
                sentActivities = activities;
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var activity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: "message/submitAction",
                replyToId: "reply-id",
                value: ProtocolJsonSerializer.ToJsonElements(new
                {
                    actionName = "feedback",
                    actionValue = new
                    {
                        reaction = "like",
                        feedback = "great"
                    }
                }));
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsFeedbackLoopRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnFeedback", app.Calls[0]);
            Assert.Equal("reply-id", app.ReplyToId);
            Assert.NotNull(sentActivities);
            Assert.Single(sentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivities[0].Type);
        }

        [Fact]
        public async Task TeamsEndOfConversationRouteAttribute_FiresOnEndOfConversation()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.EndOfConversation, Microsoft.Agents.Core.Models.Channels.Msteams);
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsEndOfConversationRouteApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnEndOfConversation", app.Calls[0]);
        }

        [Fact]
        public async Task TeamsRouteAttribute_StaticSignInHandlers_AppConstructsAndRouteFires()
        {
            var adapter = new NotImplementedAdapter();
            var activity = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");
            var turnContext = CreateTurnContext(adapter, activity);
            var app = new TeamsStaticSignInHandlersApp(CreateOptions(turnContext));

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.Single(app.Calls);
            Assert.Equal("OnMessageWithStaticHandlers", app.Calls[0]);
        }

        private static TurnContext CreateTurnContext(IChannelAdapter adapter, Activity activity)
        {
            return new TurnContext(adapter, activity);
        }

        private static Activity CreateActivity(string type, string channelId, string name = null, string text = null, string replyToId = null, object value = null)
        {
            return new Activity
            {
                Type = type,
                ChannelId = channelId,
                Name = name,
                Text = text,
                ReplyToId = replyToId,
                Value = value,
                Recipient = new ChannelAccount { Id = "recipientId" },
                Conversation = new ConversationAccount { Id = "conversationId" },
                From = new ChannelAccount { Id = "fromId" }
            };
        }

        private static AgentApplicationOptions CreateOptions(ITurnContext referenceTurnContext)
        {
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(referenceTurnContext);
            return new AgentApplicationOptions(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            };
        }
    }

    class TeamsActivityRouteTypeApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsActivityRoute(ActivityTypes.Event)]
        public Task OnEvent(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnEvent");
            return Task.CompletedTask;
        }
    }

    class TeamsActivityRouteRegexApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsActivityRoute(typeRegex: "event|invoke")]
        public Task OnEventOrInvoke(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnEventOrInvoke");
            return Task.CompletedTask;
        }
    }

    class TeamsActivityRouteAnyApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsActivityRoute(ActivityTypes.Event)]
        public Task OnEvent(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnEvent");
            return Task.CompletedTask;
        }

        [TeamsActivityRoute]
        public Task OnAny(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnAny");
            return Task.CompletedTask;
        }
    }

    class TeamsInstallationUpdateRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsInstallationUpdateRoute]
        public Task OnInstallationUpdate(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnInstallationUpdate");
            return Task.CompletedTask;
        }
    }

    class TeamsMessageRouteTextApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsMessageRoute(text: "hi")]
        public Task OnHi(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnHi");
            return Task.CompletedTask;
        }
    }

    class TeamsEventRouteRegexApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsEventRoute(nameRegex: "my.*Event")]
        public Task OnMyRegexEvent(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnMyRegexEvent");
            return Task.CompletedTask;
        }
    }

    class TeamsConversationUpdateRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsConversationUpdateRoute]
        public Task OnAnyConversationUpdate(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnAnyConversationUpdate");
            return Task.CompletedTask;
        }
    }

    class TeamsMembersAddedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsMembersAddedRoute]
        public Task OnMembersAdded(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnMembersAdded");
            return Task.CompletedTask;
        }
    }

    class TeamsMembersRemovedRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsMembersRemovedRoute]
        public Task OnMembersRemoved(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnMembersRemoved");
            return Task.CompletedTask;
        }
    }

    class TeamsHandoffRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];
        public string Continuation { get; private set; }

        [TeamsHandoffRoute]
        public Task OnHandoff(ITeamsTurnContext ctx, ITurnState state, string continuation, CancellationToken ct)
        {
            Calls.Add("OnHandoff");
            Continuation = continuation;
            return Task.CompletedTask;
        }
    }

    class TeamsFeedbackLoopRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];
        public string ReplyToId { get; private set; }

        [TeamsFeedbackLoopRoute]
        public Task OnFeedback(ITeamsTurnContext ctx, ITurnState state, FeedbackData feedbackData, CancellationToken ct)
        {
            Calls.Add("OnFeedback");
            ReplyToId = feedbackData.ReplyToId;
            return Task.CompletedTask;
        }
    }

    class TeamsEndOfConversationRouteApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        [TeamsEndOfConversationRoute]
        public Task OnEndOfConversation(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnEndOfConversation");
            return Task.CompletedTask;
        }
    }

    class TeamsStaticSignInHandlersApp(AgentApplicationOptions options) : AgentApplication(options)
    {
        public List<string> Calls = [];

        public static string[] GetSignInHandlers(ITurnContext _) => ["handler1", "handler2"];

        [TeamsMessageRoute(text: "hello", autoSignInHandlers: nameof(GetSignInHandlers))]
        public Task OnMessageWithStaticHandlers(ITeamsTurnContext ctx, ITurnState state, CancellationToken ct)
        {
            Calls.Add("OnMessageWithStaticHandlers");
            return Task.CompletedTask;
        }
    }
}