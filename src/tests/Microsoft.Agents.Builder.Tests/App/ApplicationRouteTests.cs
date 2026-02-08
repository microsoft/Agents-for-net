// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Builders;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class ApplicationRouteTests
    {
        [Fact]
        public async Task Test_RouteList_RouteRank()
        {
            List<string> values = [];
            RouteList routes = new();

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("1"); return Task.CompletedTask; })
                    .WithOrderRank(2)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("2"); return Task.CompletedTask; })
                    .WithOrderRank(0)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("3"); return Task.CompletedTask; })
                    .WithOrderRank(1)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("4"); return Task.CompletedTask; })
                    .WithOrderRank(1)
                    .Build()
            );

            foreach ( var route in routes.Enumerate())
            {
                await route.Handler(null, null, CancellationToken.None);
            }

            Assert.Equal(4, values.Count);
            Assert.Equal("2", values[0]);
            Assert.Equal("3", values[1]);
            Assert.Equal("4", values[2]);
            Assert.Equal("1", values[3]);
        }

        [Fact]
        public async Task Test_RouteList_ByAgenticThenInvokeThenRank()
        {
            List<string> values = [];
            RouteList routes = new();

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("2"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("1"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("3"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.First)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("invoke"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsAgentic()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("agenticInvoke2"); return Task.CompletedTask; })
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsAgentic()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("agenticInvoke1"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.First)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsAgentic()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("agentic"); return Task.CompletedTask; })
                    .Build()
            );


            foreach (var route in routes.Enumerate())
            {
                await route.Handler(null, null, CancellationToken.None);
            }

            Assert.Equal(7, values.Count);
            Assert.Equal("agenticInvoke1", values[0]);
            Assert.Equal("agenticInvoke2", values[1]);
            Assert.Equal("invoke", values[2]);
            Assert.Equal("agentic", values[3]);
            Assert.Equal("3", values[4]);
            Assert.Equal("2", values[5]);
            Assert.Equal("1", values[6]);
        }

        [Fact]
        public async Task Test_RouteList_ByInvokeThenByRank()
        {
            List<string> values = [];
            RouteList routes = new();

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("2"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.Unspecified)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("1"); return Task.CompletedTask; })
                    .WithOrderRank(0)
                    .Build()
            );

            routes.AddRoute(
                RouteBuilder.Create()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("invoke1"); return Task.CompletedTask; })
                    .WithOrderRank(RouteRank.Last)
                    .Build()
            );
            routes.AddRoute(
                RouteBuilder.Create()
                    .AsInvoke()
                    .WithSelector((turnContext, CancellationToken) => { return Task.FromResult(true); })
                    .WithHandler((turnContext, turnState, CancellationToken) => { values.Add("invoke2"); return Task.CompletedTask; })
                    .WithOrderRank(0)
                    .Build()
            );

            foreach (var route in routes.Enumerate())
            {
                await route.Handler(null, null, CancellationToken.None);
            }

            Assert.Equal(4, values.Count);
            Assert.Equal("invoke2", values[0]);
            Assert.Equal("invoke1", values[1]);
            Assert.Equal("1", values[2]);
            Assert.Equal("2", values[3]);
        }

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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var messages = new List<string>();

            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello.1", context.Activity.Text)),
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
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

            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                StartTypingTimer = false,
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var types = new List<string>();
            var agenticTypes = new List<string>();

            // Agentic
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                agenticTypes.Add(context.Activity.Type);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Non-agentic
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);

            Assert.Single(agenticTypes);
            Assert.Equal(ActivityTypes.Message, agenticTypes[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var types = new List<string>();
            var agenticTypes = new List<string>();

            // Agentic
            app.OnActivity(new Regex("^message$"), (context, _, _) =>
            {
                agenticTypes.Add(context.Activity.Type);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Non-agentic
            app.OnActivity(new Regex("^message$"), (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);

            Assert.Single(agenticTypes);
            Assert.Equal(ActivityTypes.Message, agenticTypes[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Name = "Message",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var types = new List<string>();
            var agenticTypes = new List<string>();

            // agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnActivity((context, _) => Task.FromResult(context.Activity?.Name != null), (context, _, _) =>
            {
                agenticTypes.Add(context.Activity.Type);
                return Task.CompletedTask;
            }, isAgenticOnly: true);
#pragma warning restore CS0618 // Type or member is obsolete

            // non-agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnActivity((context, _) => Task.FromResult(context.Activity?.Name != null), (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(types);
            Assert.Equal(ActivityTypes.Message, types[0]);

            Assert.Single(agenticTypes);
            Assert.Equal(ActivityTypes.Message, agenticTypes[0]);
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var types = new List<string>();
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnActivity(new MultipleRouteSelector
            {
                Strings = new[] { ActivityTypes.Invoke },
                Regexes = new[] { new Regex("^message$") },
                RouteSelectors = new RouteSelector[] { (context, _) => Task.FromResult(context.Activity?.Name != null) },
            },
            (context, _, _) =>
            {
                types.Add(context.Activity.Type);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersAdded = new List<ChannelAccount> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, (context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, (context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                MembersRemoved = new List<ChannelAccount> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, (context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, (context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
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
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "HELLO",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new Activity
            {
                Type = ActivityTypes.Event,
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var texts = new List<string>();
            var agenticTexts = new List<string>();

            // agentic
            app.OnMessage("hello", (context, _, _) =>
            {
                agenticTexts.Add(context.Activity.Text);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessage("hello", (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Equal(2, texts.Count);
            Assert.Single(agenticTexts);
        }

        [Fact]
        public async Task Test_OnMessage_StringWord_Selector()
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
                Type = ActivityTypes.Message,
                Text = "i say hello b",
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var texts = new List<string>();
            app.OnMessage(new Regex(@"\bhello\b"), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(2, texts.Count);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "agentic hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var texts = new List<string>();
            var agenticTexts = new List<string>();

            // agentic
            app.OnMessage(new Regex("llo"), (context, _, _) =>
            {
                agenticTexts.Add(context.Activity.Text);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessage(new Regex("llo"), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);

            Assert.Single(agenticTexts);
            Assert.Equal("agentic hello", agenticTexts[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "agentic hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var texts = new List<string>();
            var agenticTexts = new List<string>();

            // agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnMessage((context, _) => Task.FromResult(context.Activity?.Text != null), (context, _, _) =>
            {
                agenticTexts.Add(context.Activity.Text);
                return Task.CompletedTask;
            }, isAgenticOnly: true);
#pragma warning restore CS0618 // Type or member is obsolete

            // non-agentic
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnMessage((context, _) => Task.FromResult(context.Activity?.Text != null), (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(texts);
            Assert.Equal("hello", texts[0]);

            Assert.Single(agenticTexts);
            Assert.Equal("agentic hello", agenticTexts[0]);
        }

        [Fact]
        public async Task Test_OnMessage_Multiple_Selectors()
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
                Name = "hello",
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
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });
            var texts = new List<string>();
#pragma warning disable CS0618 // Type or member is obsolete
            app.OnMessage(new MultipleRouteSelector
            {
                Strings = new[] { "hello" },
                Regexes = new[] { new Regex(@"\bworld\b") },
                RouteSelectors = new RouteSelector[] { (context, _) => Task.FromResult(context.Activity?.Name != null) },
            },
            (context, _, _) =>
            {
                texts.Add(context.Activity.Text);
                return Task.CompletedTask;
            });
#pragma warning restore CS0618 // Type or member is obsolete

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);

            // Assert
            Assert.Equal(3, texts.Count);
            Assert.Equal("hello", texts[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsAdded = new List<MessageReaction> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnMessageReactionsAdded((context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessageReactionsAdded((context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.MessageReaction,
                ReactionsRemoved = new List<MessageReaction> { new() },
                Name = "agentic1",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            var names = new List<string>();
            var agenticNames = new List<string>();

            // agentic
            app.OnMessageReactionsRemoved((context, _, _) =>
            {
                agenticNames.Add(context.Activity.Name);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessageReactionsRemoved((context, _, _) =>
            {
                names.Add(context.Activity.Name);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(names);
            Assert.Equal("1", names[0]);

            Assert.Single(agenticNames);
            Assert.Equal("agentic1", agenticNames[0]);
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
            var agenticActivity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                Value = new { Continuation = "test" },
                Id = "agentic test",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var expectedInvokeResponse = new InvokeResponse
            {
                Status = 200
            };
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext1);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });


            var ids = new List<string>();
            var agenticIds = new List<string>();

            // agentic
            app.OnHandoff((turnContext, _, _, _) =>
            {
                agenticIds.Add(turnContext.Activity.Id);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnHandoff((turnContext, _, _, _) =>
            {
                ids.Add(turnContext.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);
            await app.OnTurnAsync(turnContext3, CancellationToken.None);
            await app.OnTurnAsync(agenticTurnContext, CancellationToken.None);

            // Assert
            Assert.Single(ids);
            Assert.Equal("test", ids[0]);
            Assert.NotNull(activitiesToSend);
            Assert.Single(activitiesToSend);
            Assert.Equal("invokeResponse", activitiesToSend[0].Type);
            Assert.Equivalent(expectedInvokeResponse, activitiesToSend[0].Value);

            Assert.Single(agenticIds);
            Assert.Equal("agentic test", agenticIds[0]);
        }
    }
}
