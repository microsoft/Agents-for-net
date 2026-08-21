// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.Config;
using Microsoft.Agents.Extensions.MSTeams.FileConsents;
using Microsoft.Agents.Extensions.MSTeams.Messages;
using Microsoft.Agents.Extensions.MSTeams.Channels;
using Microsoft.Agents.Extensions.MSTeams.Teams;
using Microsoft.Agents.Extensions.MSTeams.Tests.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class TeamsRouteBuilderTests
    {
        [Fact]
        public void TeamsFeedbackRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = TeamsFeedbackRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsFeedbackRouteBuilder>(builder);
        }

        [Fact]
        public void TeamsFeedbackRouteBuilder_Build_SetsTeamsChannelAndInvokeFlag()
        {
            var route = TeamsFeedbackRouteBuilder.Create()
                .WithChannelId(Microsoft.Agents.Core.Models.Channels.Outlook)
                .WithHandler((turnContext, turnState, feedbackData, cancellationToken) => Task.CompletedTask)
                .Build();

            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, route.ChannelId);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void TeamsFeedbackRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsFeedbackRouteBuilder.Create().WithHandler(null));
        }

        [Fact]
        public async Task TeamsFeedbackRouteBuilder_WithHandler_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var teamsActivity = CreateActivity(
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
            var outlookActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Outlook,
                name: "message/submitAction",
                value: ProtocolJsonSerializer.ToJsonElements(new
                {
                    actionName = "feedback",
                    actionValue = new
                    {
                        reaction = "like",
                        feedback = "ignored"
                    }
                }));

            var app = CreateApp(CreateTurnContext(adapter, teamsActivity));
            var contexts = new List<ITeamsTurnContext>();
            var replyToIds = new List<string>();

            app.AddRoute(TeamsFeedbackRouteBuilder.Create()
                .WithHandler((turnContext, turnState, feedbackData, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    replyToIds.Add(feedbackData.ReplyToId);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsActivity), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookActivity), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(replyToIds);
            Assert.Equal("reply-id", replyToIds[0]);
            Assert.Single(sentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivities[0].Type);
            Assert.Equivalent(new InvokeResponse { Status = 200 }, sentActivities[0].Value);
        }

        [Fact]
        public async Task TeamsFeedbackRouteBuilder_WithHandler_DoesNotSelectNonFeedbackActivity()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: "message/submitAction",
                value: ProtocolJsonSerializer.ToJsonElements(new
                {
                    actionName = "not-feedback",
                    actionValue = new
                    {
                        reaction = "like"
                    }
                }));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsFeedbackRouteBuilder.Create()
                .WithHandler((turnContext, turnState, feedbackData, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public void TeamsHandoffRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = TeamsHandoffRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsHandoffRouteBuilder>(builder);
        }

        [Fact]
        public void TeamsHandoffRouteBuilder_Build_SetsTeamsChannelAndInvokeFlag()
        {
            var route = TeamsHandoffRouteBuilder.Create()
                .WithChannelId(Microsoft.Agents.Core.Models.Channels.Outlook)
                .WithHandler((turnContext, turnState, continuation, cancellationToken) => Task.CompletedTask)
                .Build();

            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, route.ChannelId);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
        }

        [Fact]
        public void TeamsHandoffRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsHandoffRouteBuilder.Create().WithHandler(null));
        }

        [Fact]
        public async Task TeamsHandoffRouteBuilder_WithHandler_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var teamsActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: "handoff/action",
                value: new { Continuation = "continue-token" });
            var outlookActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Outlook,
                name: "handoff/action",
                value: new { Continuation = "ignored-token" });

            var app = CreateApp(CreateTurnContext(adapter, teamsActivity));
            var contexts = new List<ITeamsTurnContext>();
            var continuations = new List<string>();

            app.AddRoute(TeamsHandoffRouteBuilder.Create()
                .WithHandler((turnContext, turnState, continuation, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    continuations.Add(continuation);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsActivity), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookActivity), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(continuations);
            Assert.Equal("continue-token", continuations[0]);
            Assert.Single(sentActivities);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivities[0].Type);
            Assert.Equivalent(new InvokeResponse { Status = 200 }, sentActivities[0].Value);
        }

        [Fact]
        public async Task TeamsHandoffRouteBuilder_WithHandler_DoesNotSelectWrongInvokeName()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: "handoff/ignored",
                value: new { Continuation = "ignored-token" });

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsHandoffRouteBuilder.Create()
                .WithHandler((turnContext, turnState, continuation, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public void TeamsMessageRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = TeamsMessageRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsMessageRouteBuilder>(builder);
        }

        [Fact]
        public void TeamsMessageRouteBuilder_Build_SetsTeamsChannel()
        {
            var route = TeamsMessageRouteBuilder.Create()
                .WithChannelId(Microsoft.Agents.Core.Models.Channels.Outlook)
                .WithHandler((turnContext, turnState, cancellationToken) => Task.CompletedTask)
                .Build();

            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, route.ChannelId);
        }

        [Fact]
        public void TeamsMessageRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsMessageRouteBuilder.Create().WithHandler(null));
        }

        [Fact]
        public async Task TeamsMessageRouteBuilder_WithText_String_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");
            var teamsMiss = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "goodbye");
            var outlookMatch = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Outlook, text: "hello");

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var texts = new List<string>();

            app.AddRoute(TeamsMessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    texts.Add(turnContext.Activity.Text);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);
        }

        [Fact]
        public async Task TeamsMessageRouteBuilder_WithText_String_DoesNotSelectNonMatchingText()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "goodbye");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsMessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task TeamsMessageRouteBuilder_WithText_Regex_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "status 200");
            var teamsMiss = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");
            var outlookMatch = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Outlook, text: "status 200");

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var texts = new List<string>();

            app.AddRoute(TeamsMessageRouteBuilder.Create()
                .WithText(new Regex("^status \\d+$"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    texts.Add(turnContext.Activity.Text);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(texts);
            Assert.Equal("status 200", texts[0]);
        }

        [Fact]
        public async Task TeamsMessageRouteBuilder_WithText_Regex_DoesNotSelectNonMatchingText()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsMessageRouteBuilder.Create()
                .WithText(new Regex("^status \\d+$"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public void TeamsEventRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = TeamsEventRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsEventRouteBuilder>(builder);
        }

        [Fact]
        public void TeamsEventRouteBuilder_Build_SetsTeamsChannel()
        {
            var route = TeamsEventRouteBuilder.Create()
                .WithChannelId(Microsoft.Agents.Core.Models.Channels.Outlook)
                .WithHandler((turnContext, turnState, cancellationToken) => Task.CompletedTask)
                .Build();

            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, route.ChannelId);
        }

        [Fact]
        public void TeamsEventRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsEventRouteBuilder.Create().WithHandler(null));
        }

        [Fact]
        public async Task TeamsEventRouteBuilder_WithName_String_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "ping");
            var teamsMiss = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "ignored");
            var outlookMatch = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Outlook, name: "ping");

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var names = new List<string>();

            app.AddRoute(TeamsEventRouteBuilder.Create()
                .WithName("ping")
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    names.Add(turnContext.Activity.Name);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(names);
            Assert.Equal("ping", names[0]);
        }

        [Fact]
        public async Task TeamsEventRouteBuilder_WithName_String_DoesNotSelectNonMatchingName()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "ignored");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsEventRouteBuilder.Create()
                .WithName("ping")
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task TeamsEventRouteBuilder_WithName_Regex_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "status:ok");
            var teamsMiss = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "ping");
            var outlookMatch = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Outlook, name: "status:ok");

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var names = new List<string>();

            app.AddRoute(TeamsEventRouteBuilder.Create()
                .WithName(new Regex("^status:"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    names.Add(turnContext.Activity.Name);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(names);
            Assert.Equal("status:ok", names[0]);
        }

        [Fact]
        public async Task TeamsEventRouteBuilder_WithName_Regex_DoesNotSelectNonMatchingName()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "ping");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsEventRouteBuilder.Create()
                .WithName(new Regex("^status:"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public void TeamsConversationUpdateRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = TeamsConversationUpdateRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsConversationUpdateRouteBuilder>(builder);
        }

        [Fact]
        public void TeamsConversationUpdateRouteBuilder_Build_SetsTeamsChannel()
        {
            var route = TeamsConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((turnContext, turnState, cancellationToken) => Task.CompletedTask)
                .Build();

            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, route.ChannelId);
        }

        [Fact]
        public void TeamsConversationUpdateRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsConversationUpdateRouteBuilder.Create().WithHandler(null));
        }

        [Fact]
        public async Task TeamsConversationUpdateRouteBuilder_WithUpdateEvent_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            teamsMatch.MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } };
            var teamsMiss = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            teamsMiss.MembersRemoved = new List<ChannelAccount> { new ChannelAccount { Id = "user2" } };
            var outlookMatch = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Outlook);
            outlookMatch.MembersAdded = new List<ChannelAccount> { new ChannelAccount { Id = "user3" } };

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var memberCounts = new List<int>();

            app.AddRoute(TeamsConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    memberCounts.Add(turnContext.Activity.MembersAdded.Count);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(memberCounts);
            Assert.Equal(1, memberCounts[0]);
        }

        [Fact]
        public async Task TeamsConversationUpdateRouteBuilder_WithUpdateEvent_DoesNotSelectNonMatchingUpdateEvent()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            nonMatchingActivity.MembersRemoved = new List<ChannelAccount> { new ChannelAccount { Id = "user1" } };

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(ConversationUpdateEvents.MembersAdded)
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task TeamsConversationUpdateRouteBuilder_WithSelector_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams, text: "selector");
            var teamsMiss = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams, text: "ignored");
            var outlookMatch = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Outlook, text: "selector");

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var texts = new List<string>();

            app.AddRoute(TeamsConversationUpdateRouteBuilder.Create()
                .WithSelector((context, cancellationToken) => Task.FromResult(context.Activity.Text == "selector"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    texts.Add(turnContext.Activity.Text);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(texts);
            Assert.Equal("selector", texts[0]);
        }

        [Fact]
        public async Task TeamsConversationUpdateRouteBuilder_WithSelector_DoesNotSelectWhenSelectorReturnsFalse()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams, text: "ignored");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsConversationUpdateRouteBuilder.Create()
                .WithSelector((context, cancellationToken) => Task.FromResult(false))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public void TeamsTypeRouteBuilder_Create_ReturnsNewInstance()
        {
            var builder = TeamsTypeRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsTypeRouteBuilder>(builder);
        }

        [Fact]
        public void TeamsTypeRouteBuilder_Build_SetsTeamsChannel()
        {
            var route = TeamsTypeRouteBuilder.Create()
                .WithChannelId(Microsoft.Agents.Core.Models.Channels.Outlook)
                .WithHandler((turnContext, turnState, cancellationToken) => Task.CompletedTask)
                .Build();

            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, route.ChannelId);
        }

        [Fact]
        public void TeamsTypeRouteBuilder_WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsTypeRouteBuilder.Create().WithHandler(null));
        }

        [Fact]
        public async Task TeamsTypeRouteBuilder_WithType_String_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");
            var teamsMiss = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "hello");
            var outlookMatch = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Outlook, text: "hello");

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var types = new List<string>();

            app.AddRoute(TeamsTypeRouteBuilder.Create()
                .WithType(ActivityTypes.Message)
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    types.Add(turnContext.Activity.Type);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);
        }

        [Fact]
        public async Task TeamsTypeRouteBuilder_WithType_String_DoesNotSelectNonMatchingType()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.Event, Microsoft.Agents.Core.Models.Channels.Msteams, name: "hello");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsTypeRouteBuilder.Create()
                .WithType(ActivityTypes.Message)
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task TeamsTypeRouteBuilder_WithType_Regex_UsesTeamsTurnContext_AndMatchesTeamsOnly()
        {
            var adapter = new NotImplementedAdapter();
            var teamsMatch = CreateActivity("invoke/custom", Microsoft.Agents.Core.Models.Channels.Msteams);
            var teamsMiss = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");
            var outlookMatch = CreateActivity("invoke/custom", Microsoft.Agents.Core.Models.Channels.Outlook);

            var app = CreateApp(CreateTurnContext(adapter, teamsMatch));
            var contexts = new List<ITeamsTurnContext>();
            var types = new List<string>();

            app.AddRoute(TeamsTypeRouteBuilder.Create()
                .WithType(new Regex("^invoke/.+"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    types.Add(turnContext.Activity.Type);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMatch), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, teamsMiss), CancellationToken.None);
            await app.OnTurnAsync(CreateTurnContext(adapter, outlookMatch), CancellationToken.None);

            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
            Assert.Single(types);
            Assert.Equal("invoke/custom", types[0]);
        }

        [Fact]
        public async Task TeamsTypeRouteBuilder_WithType_Regex_DoesNotSelectNonMatchingType()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.Message, Microsoft.Agents.Core.Models.Channels.Msteams, text: "hello");

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamsTypeRouteBuilder.Create()
                .WithType(new Regex("^invoke/.+"))
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task ChannelUpdateRouteBuilder_ForChannelCreated_DoesNotSelectDifferentChannelEvent()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            nonMatchingActivity.ChannelData = new Microsoft.Teams.Api.ChannelData
            {
                EventType = Microsoft.Teams.Api.Activities.ConversationUpdateActivity.EventType.ChannelDeleted,
                Channel = new Microsoft.Teams.Api.Channel { Id = "channel1" }
            };

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(ChannelUpdateRouteBuilder.Create()
                .ForChannelCreated()
                .WithHandler((turnContext, turnState, channel, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task TeamUpdateRouteBuilder_ForTeamArchived_DoesNotSelectDifferentTeamEvent()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.ConversationUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            nonMatchingActivity.ChannelData = new Microsoft.Teams.Api.ChannelData
            {
                EventType = Microsoft.Teams.Api.Activities.ConversationUpdateActivity.EventType.TeamDeleted,
                Team = new Microsoft.Teams.Api.Team { Id = "team1" }
            };

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(TeamUpdateRouteBuilder.Create()
                .ForTeamArchived()
                .WithHandler((turnContext, turnState, team, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task MessageEditRouteBuilder_DoesNotSelectDifferentMessageEvent()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.MessageUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            nonMatchingActivity.ChannelData = new Microsoft.Teams.Api.ChannelData
            {
                EventType = "undeleteMessage"
            };

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITurnContext>();

            app.AddRoute(MessageEditRouteBuilder.Create()
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task MessageDeleteRouteBuilder_DoesNotSelectDifferentMessageEvent()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.MessageDelete, Microsoft.Agents.Core.Models.Channels.Msteams);
            nonMatchingActivity.ChannelData = new Microsoft.Teams.Api.ChannelData
            {
                EventType = "unknown"
            };

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITurnContext>();

            app.AddRoute(MessageDeleteRouteBuilder.Create()
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task MessageUndeleteRouteBuilder_DoesNotSelectDifferentMessageEvent()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(ActivityTypes.MessageUpdate, Microsoft.Agents.Core.Models.Channels.Msteams);
            nonMatchingActivity.ChannelData = new Microsoft.Teams.Api.ChannelData
            {
                EventType = "editMessage"
            };

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITurnContext>();

            app.AddRoute(MessageUndeleteRouteBuilder.Create()
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task ExecuteActionRouteBuilder_DoesNotSelectDifferentInvokeName()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.Query,
                value: ProtocolJsonSerializer.ToJsonElements(new Microsoft.Teams.Api.O365.ConnectorCardActionQuery()));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(ExecuteActionRouteBuilder.Create()
                .WithHandler((turnContext, turnState, query, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public async Task ReadReceiptRouteBuilder_DoesNotSelectDifferentEventName()
        {
            var adapter = new NotImplementedAdapter();
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Event,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: "ignored",
                value: ProtocolJsonSerializer.ToJsonElements(new { lastReadMessageId = "1" }));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(ReadReceiptRouteBuilder.Create()
                .WithHandler((turnContext, turnState, data, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
        }

        [Fact]
        public async Task FileConsentAcceptRouteBuilder_DoesNotSelectDifferentAction()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent,
                value: ProtocolJsonSerializer.ToJsonElements(new Microsoft.Teams.Api.FileConsentCardResponse { Action = Microsoft.Teams.Api.Action.Decline }));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(FileConsentAcceptRouteBuilder.Create()
                .WithHandler((turnContext, turnState, response, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public async Task FileConsentDeclineRouteBuilder_DoesNotSelectDifferentAction()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent,
                value: ProtocolJsonSerializer.ToJsonElements(new Microsoft.Teams.Api.FileConsentCardResponse { Action = Microsoft.Teams.Api.Action.Accept }));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(FileConsentDeclineRouteBuilder.Create()
                .WithHandler((turnContext, turnState, response, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.CompletedTask;
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public async Task ConfigFetchRouteBuilder_DoesNotSelectDifferentInvokeName()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Submit,
                value: ProtocolJsonSerializer.ToJsonElements(new { key = "value" }));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(ConfigFetchRouteBuilder.Create()
                .WithHandler((turnContext, turnState, configData, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.FromResult<Microsoft.Teams.Api.Config.ConfigResponse>(null);
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
        }

        [Fact]
        public async Task ConfigSubmitRouteBuilder_DoesNotSelectDifferentInvokeName()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var nonMatchingActivity = CreateActivity(
                ActivityTypes.Invoke,
                Microsoft.Agents.Core.Models.Channels.Msteams,
                name: Microsoft.Teams.Api.Activities.Invokes.Name.Configs.Fetch,
                value: ProtocolJsonSerializer.ToJsonElements(new { key = "value" }));

            var app = CreateApp(CreateTurnContext(adapter, nonMatchingActivity));
            var contexts = new List<ITeamsTurnContext>();

            app.AddRoute(ConfigSubmitRouteBuilder.Create()
                .WithHandler((turnContext, turnState, configData, cancellationToken) =>
                {
                    contexts.Add(turnContext);
                    return Task.FromResult<Microsoft.Teams.Api.Config.ConfigResponse>(null);
                })
                .Build());

            await app.OnTurnAsync(CreateTurnContext(adapter, nonMatchingActivity), CancellationToken.None);

            Assert.Empty(contexts);
            Assert.Empty(sentActivities);
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

        private static AgentApplication CreateApp(ITurnContext referenceTurnContext)
        {
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(referenceTurnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            return app;
        }
    }
}