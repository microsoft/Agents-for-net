// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TeamsAppExtensionsTests
    {
        [Fact]
        public async Task Test_OnActivity_String_And_Regex_UseTeamsTurnContext()
        {
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var app = CreateApp(turnContext1, turnContext1, turnContext2, turnContext3);
            var routes = new List<string>();
            var contexts = new List<ITeamsTurnContext>();

            Assert.Same(app, app.OnTeamsActivity(ActivityTypes.Message, (turnContext, _, _) =>
            {
                routes.Add("string");
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            Assert.Same(app, app.OnTeamsActivity(new Regex($"^{ActivityTypes.MessageDelete}$"), (turnContext, _, _) =>
            {
                routes.Add("regex");
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            Assert.Equal(new[] { "string", "regex" }, routes);
            Assert.Equal(2, contexts.Count);
            Assert.All(contexts, context => Assert.IsType<TeamsTurnContext>(context));
        }

        [Fact]
        public async Task Test_OnConversationUpdate_UsesTeamsTurnContext()
        {
            var activity1 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "member-added",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                Name = "not-matched",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var app = CreateApp(turnContext1, turnContext1, turnContext2);
            var names = new List<string>();
            var contexts = new List<ITeamsTurnContext>();

            Assert.Same(app, app.OnTeamsConversationUpdate(ConversationUpdateEvents.MembersAdded, (turnContext, _, _) =>
            {
                names.Add(turnContext.Activity.Name);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            Assert.Single(names);
            Assert.Equal("member-added", names[0]);
            Assert.Single(contexts);
            Assert.IsType<TeamsTurnContext>(contexts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_String_And_Regex_UseTeamsTurnContext()
        {
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "status 200",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "goodbye",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var app = CreateApp(turnContext1, turnContext1, turnContext2, turnContext3);
            var messages = new List<string>();
            var contexts = new List<ITeamsTurnContext>();

            Assert.Same(app, app.OnTeamsMessage("hello", (turnContext, _, _) =>
            {
                messages.Add(turnContext.Activity.Text);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            Assert.Same(app, app.OnTeamsMessage(new Regex("^status \\d+$"), (turnContext, _, _) =>
            {
                messages.Add(turnContext.Activity.Text);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            Assert.Equal(new[] { "hello", "status 200" }, messages);
            Assert.Equal(2, contexts.Count);
            Assert.All(contexts, context => Assert.IsType<TeamsTurnContext>(context));
        }

        [Fact]
        public async Task Test_OnEvent_Overloads_UseTeamsTurnContext()
        {
            var activity1 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "ping",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "status:ok",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "custom",
                Text = "selector",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity4 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "ignored",
                Text = "ignored",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnContext4 = new TurnContext(adapter, activity4);
            var app = CreateApp(turnContext1, turnContext1, turnContext2, turnContext3, turnContext4);
            var names = new List<string>();
            var contexts = new List<ITeamsTurnContext>();

            Assert.Same(app, app.OnTeamsEvent("ping", (turnContext, _, _) =>
            {
                names.Add(turnContext.Activity.Name);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            Assert.Same(app, app.OnTeamsEvent(new Regex("^status:"), (turnContext, _, _) =>
            {
                names.Add(turnContext.Activity.Name);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            Assert.Same(app, app.OnTeamsEvent((context, _) => Task.FromResult(context.Activity.Text == "selector"), (turnContext, _, _) =>
            {
                names.Add(turnContext.Activity.Name);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(turnContext4, CancellationToken.None);

            Assert.Equal(new[] { "ping", "status:ok", "custom" }, names);
            Assert.Equal(3, contexts.Count);
            Assert.All(contexts, context => Assert.IsType<TeamsTurnContext>(context));
        }

        [Fact]
        public async Task Test_OnMessageReactions_UseTeamsTurnContext()
        {
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = Channels.Msteams,
                Name = "added",
                ReactionsAdded = new List<MessageReaction> { new() },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = Channels.Msteams,
                Name = "removed",
                ReactionsRemoved = new List<MessageReaction> { new() },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = Channels.Msteams,
                Name = "ignored",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var app = CreateApp(turnContext1, turnContext1, turnContext2, turnContext3);
            var names = new List<string>();
            var contexts = new List<ITeamsTurnContext>();

            Assert.Same(app, app.OnTeamsMessageReactionsAdded((turnContext, _, _) =>
            {
                names.Add(turnContext.Activity.Name);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            Assert.Same(app, app.OnTeamsMessageReactionsRemoved((turnContext, _, _) =>
            {
                names.Add(turnContext.Activity.Name);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            Assert.Equal(new[] { "added", "removed" }, names);
            Assert.Equal(2, contexts.Count);
            Assert.All(contexts, context => Assert.IsType<TeamsTurnContext>(context));
        }

        [Fact]
        public async Task Test_OnHandoff_And_OnFeedbackLoop_UseTeamsTurnContext()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var handoffActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "continue-token" },
                Id = "handoff-id",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var feedbackActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                ReplyToId = "reply-id",
                Value = ProtocolJsonSerializer.ToJsonElements(new
                {
                    actionName = "feedback",
                    actionValue = new
                    {
                        reaction = "like",
                        feedback = "great"
                    }
                }),
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var ignoredActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var handoffTurnContext = new TurnContext(adapter, handoffActivity);
            var feedbackTurnContext = new TurnContext(adapter, feedbackActivity);
            var ignoredTurnContext = new TurnContext(adapter, ignoredActivity);
            var app = CreateApp(handoffTurnContext, handoffTurnContext, feedbackTurnContext, ignoredTurnContext);
            var continuations = new List<string>();
            var feedbackReplies = new List<string>();
            var contexts = new List<ITeamsTurnContext>();

            Assert.Same(app, app.OnTeamsHandoff((turnContext, _, continuation, _) =>
            {
                continuations.Add(continuation);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            Assert.Same(app, app.OnTeamsFeedbackLoop((turnContext, _, feedbackData, _) =>
            {
                feedbackReplies.Add(feedbackData.ReplyToId);
                contexts.Add(turnContext);
                return Task.CompletedTask;
            }));

            await app.OnTurnAsync(handoffTurnContext, CancellationToken.None);
            await app.OnTurnAsync(feedbackTurnContext, CancellationToken.None);
            await app.OnTurnAsync(ignoredTurnContext, CancellationToken.None);

            Assert.Single(continuations);
            Assert.Equal("continue-token", continuations[0]);
            Assert.Single(feedbackReplies);
            Assert.Equal("reply-id", feedbackReplies[0]);
            Assert.Equal(2, contexts.Count);
            Assert.All(contexts, context => Assert.IsType<TeamsTurnContext>(context));
            Assert.Equal(2, sentActivities.Count);
            Assert.All(sentActivities, activity =>
            {
                Assert.Equal("invokeResponse", activity.Type);
                Assert.Equivalent(new InvokeResponse { Status = 200 }, activity.Value);
            });
        }

        [Fact]
        public async Task Test_OnTeamsActivity_IgnoresNonMsteamsChannelId()
        {
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = "webchat",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsActivity(ActivityTypes.Message, (turnContext, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsConversationUpdate_IgnoresNonMsteamsChannelId()
        {
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "member-added",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = "webchat",
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "member-added",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsConversationUpdate(ConversationUpdateEvents.MembersAdded, (turnContext, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsMessage_IgnoresNonMsteamsChannelId()
        {
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = "webchat",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsMessage("hello", (turnContext, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsEvent_IgnoresNonMsteamsChannelId()
        {
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "ping",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "ping",
                ChannelId = "webchat",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsEvent("ping", (turnContext, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsMessageReactionsAdded_IgnoresNonMsteamsChannelId()
        {
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = Channels.Msteams,
                ReactionsAdded = new List<MessageReaction> { new() },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = "webchat",
                ReactionsAdded = new List<MessageReaction> { new() },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsMessageReactionsAdded((turnContext, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsMessageReactionsRemoved_IgnoresNonMsteamsChannelId()
        {
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = Channels.Msteams,
                ReactionsRemoved = new List<MessageReaction> { new() },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ChannelId = "webchat",
                ReactionsRemoved = new List<MessageReaction> { new() },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var adapter = new NotImplementedAdapter();
            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsMessageReactionsRemoved((turnContext, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsHandoff_IgnoresNonMsteamsChannelId()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "continue-token" },
                Id = "handoff-id",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "continue-token" },
                Id = "handoff-id",
                ChannelId = "webchat",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsHandoff((turnContext, _, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        [Fact]
        public async Task Test_OnTeamsFeedbackLoop_IgnoresNonMsteamsChannelId()
        {
            var sentActivities = new List<IActivity>();
            void CaptureSend(IActivity[] activities)
            {
                sentActivities.AddRange(activities);
            }

            var adapter = new SimpleAdapter(CaptureSend);
            var feedbackValue = ProtocolJsonSerializer.ToJsonElements(new
            {
                actionName = "feedback",
                actionValue = new
                {
                    reaction = "like",
                    feedback = "great"
                }
            });
            var teamsActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                ReplyToId = "reply-id",
                Value = feedbackValue,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var otherActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                ReplyToId = "reply-id",
                Value = feedbackValue,
                ChannelId = "webchat",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };

            var teamsTurnContext = new TurnContext(adapter, teamsActivity);
            var otherTurnContext = new TurnContext(adapter, otherActivity);
            var app = CreateApp(teamsTurnContext, teamsTurnContext, otherTurnContext);
            var handled = new List<string>();

            app.OnTeamsFeedbackLoop((turnContext, _, _, _) =>
            {
                handled.Add(turnContext.Activity.ChannelId);
                return Task.CompletedTask;
            });

            await app.OnTurnAsync(teamsTurnContext, CancellationToken.None);
            await app.OnTurnAsync(otherTurnContext, CancellationToken.None);

            Assert.Single(handled);
            Assert.Equal(Channels.Msteams, handled[0]);
        }

        private static AgentApplication CreateApp(ITurnContext referenceTurnContext, params ITurnContext[] turnContexts)
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