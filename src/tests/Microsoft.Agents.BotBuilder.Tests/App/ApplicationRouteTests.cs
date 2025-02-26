// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.BotBuilder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.BotBuilder.Tests.App
{
    public class ApplicationRouteTests
    {
        [Fact]
        public async Task Test_Application_Route()
        {
            // Arrange
            var activity1 = MessageFactory.Text("hello.1");
            activity1.Recipient = new() { Id = "recipientId" };
            activity1.Conversation = new() { Id = "conversationId" };
            activity1.From = new() { Id = "fromId" };
            activity1.ChannelId = "channelId";
            var activity2 = MessageFactory.Text("hello.2");
            activity2.Recipient = new() { Id = "recipientId" };
            activity2.Conversation = new() { Id = "conversationId" };
            activity2.From = new() { Id = "fromId" };
            activity2.ChannelId = "channelId";
            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var messages = new List<string>();
            app.AddRoute(
                (context, _) =>
                Task.FromResult(string.Equals("hello.1", context.Activity.Text)),
                (context, _, _) =>
                {
                    messages.Add(context.Activity.Text);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(messages);
            Assert.Equal("hello.1", messages[0]);
        }

        [Fact]
        public async Task Test_Application_Routes_Are_Called_InOrder()
        {
            // Arrange
            var activity = MessageFactory.Text("hello.1");
            activity.Recipient = new() { Id = "recipientId" };
            activity.Conversation = new() { Id = "conversationId" };
            activity.From = new() { Id = "fromId" };
            activity.ChannelId = "channelId";
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello", context.Activity.Text)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                false);
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello.1", context.Activity.Text)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                false);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(1, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_Application_InvokeRoute()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.2",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var turnContext2 = new TurnContext(adapter, activity2);

            var app = new Application(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var names = new List<string>();
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke.1", context.Activity.Name)),
                (context, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                },
                true);

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("invoke.1", names[0]);
        }

        [Fact]
        public async Task Test_Application_InvokeRoutes_Are_Called_InOrder()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new Application(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke", context.Activity.Name)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke.1", context.Activity.Name)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                },
                true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(1, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_Application_InvokeRoutes_Are_Called_First()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new Application(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(0, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_Application_No_InvokeRoute_Matched_Fallback_To_Routes()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new Application(new()
            {
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var selectedRoutes = new List<int>();
            app.AddRoute(
                (_, _) => Task.FromResult(false),
                (context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                },
                true);
            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("invoke.1", context.Activity.Name)),
                (context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                },
                false);
            app.AddRoute(
                (_, _) => Task.FromResult(true),
                (context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(selectedRoutes);
            Assert.Equal(1, selectedRoutes[0]);
        }

        [Fact]
        public async Task Test_OnActivity_String_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var types = new List<string>();
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);
        }

        [Fact]
        public async Task Test_OnActivity_Regex_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var types = new List<string>();
            app.OnActivity(new Regex("^message$"), (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);
        }

        [Fact]
        public async Task Test_OnActivity_Function_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Name = "Message",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var types = new List<string>();
            app.OnActivity((context, _) => Task.FromResult(context.Activity?.Name != null), (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);
        }

        [Fact]
        public async Task Test_OnActivity_Multiple_Selectors()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                Name = "Delete",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var types = new List<string>();
            app.OnActivity(new MultipleRouteSelector
            {
                Strings = new[] { ActivityTypes.Invoke },
                Regexes = new[] { new Regex("^message$") },
                RouteSelectors = new RouteSelectorAsync[] { (context, _) => Task.FromResult(context.Activity?.Name != null) },
            },
            (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(3, types.Count);
            Assert.Equal(ActivityTypes.Message, types[0]);
            Assert.Equal(ActivityTypes.MessageDelete, types[1]);
            Assert.Equal(ActivityTypes.Invoke, types[2]);
        }

        [Fact]
        public async Task Test_OnConversationUpdate_MembersAdded()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var names = new List<string>();
            app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, (context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnConversationUpdate_MembersRemoved()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var names = new List<string>();
            app.OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, (context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnConversationUpdate_UnknownEventName()
        {
            // Arrange
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                Name = "1",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var names = new List<string>();
            app.OnConversationUpdate("unknown",
                (context, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnMessage_String_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello a",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Text = "hello b",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var texts = new List<string>();
            app.OnMessage("hello", (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello a", texts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_Regex_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var texts = new List<string>();
            app.OnMessage(new Regex("llo"), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_Function_Selector()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var texts = new List<string>();
            app.OnMessage((context, _) => Task.FromResult(context.Activity?.Text != null), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_Multiple_Selectors()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello a",
                Name = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello world",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var texts = new List<string>();
            app.OnMessage(new MultipleRouteSelector
            {
                Strings = new[] { "world" },
                Regexes = new[] { new Regex("come") },
                RouteSelectors = new RouteSelectorAsync[] { (context, _) => Task.FromResult(context.Activity?.Name != null) },
            },
            (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(3, texts.Count);
            Assert.Equal("hello a", texts[0]);
            Assert.Equal("welcome", texts[1]);
            Assert.Equal("hello world", texts[2]);
        }

        [Fact]
        public async Task Test_OnMessageReactionsAdded()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsAdded = new List<MessageReaction> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var names = new List<string>();
            app.OnMessageReactionsAdded((context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnMessageReactionsRemoved()
        {
            // Arrange
            var activity1 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsRemoved = new List<MessageReaction> { new() },
                Name = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var names = new List<string>();
            app.OnMessageReactionsRemoved((context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);
        }

        [Fact]
        public async Task Test_OnHandoff()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            void CaptureSend(IActivity[] arg)
            {
                activitiesToSend = arg;
            }
            var adapter = new SimpleAdapter(CaptureSend);
            var activity1 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "test" },
                Id = "test",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Event,
                Name = "actionableMessage/executeAction",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var expectedInvokeResponse = new InvokeResponse
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new Application(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var ids = new List<string>();
            app.OnHandoff((turnContext, _, _, _) =>
            {
                ids.Add(turnContext.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Single(ids);
            Assert.Equal("test", ids[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);
        }
    }
}
