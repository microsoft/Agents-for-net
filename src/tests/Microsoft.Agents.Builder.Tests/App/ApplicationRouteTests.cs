// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProactiveApp = Microsoft.Agents.Builder.App.Proactive.Proactive;
using System;
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
        public void Test_RouteList_Count_Empty()
        {
            RouteList routes = new();
            Assert.Equal(0, routes.FormatRouteList().Count);
        }

        [Fact]
        public void Test_RouteList_Count_WithRoutes()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler((ctx, ts, ct) => Task.CompletedTask)
                .Build());
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler((ctx, ts, ct) => Task.CompletedTask)
                .Build());
            Assert.Equal(2, routes.FormatRouteList().Count);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_Empty()
        {
            RouteList routes = new();
            string result = routes.FormatRouteList().Formatted;
            Assert.Equal("[]", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_NoFlags()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("\"Index\":0", result);
            Assert.Contains("\"Handler\":\"ApplicationRouteTests.MyNamedHandler\"", result);
            Assert.Contains("\"Flags\":\"None\"", result);
            Assert.Contains($"\"Rank\":{RouteRank.Unspecified}", result);
            Assert.Contains("\"Channel\":\"*\"", result);
            Assert.DoesNotContain("OAuthHandlers", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_InvokeAndAgenticFlags()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsInvoke(true)
                .AsAgentic(true)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("\"Flags\":\"Invoke,Agentic\"", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_NonTerminalFlag()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsNonTerminal()
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("\"Flags\":\"NonTerminal\"", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_WithChannel()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithChannelId(Channels.Msteams)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("\"Channel\":\"msteams\"", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_MultipleRoutes_OrderedByIndex()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsInvoke(true)
                .Build());
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            // Invoke route comes first (higher priority)
            int idx0 = result.IndexOf("\"Index\":0");
            int idx1 = result.IndexOf("\"Index\":1");
            Assert.True(idx0 >= 0);
            Assert.True(idx1 >= 0);
            Assert.True(idx0 < idx1);
            Assert.Contains("\"Flags\":\"Invoke\"", result);
        }

        private static Task MyNamedHandler(ITurnContext ctx, ITurnState state, CancellationToken ct)
            => Task.CompletedTask;

        [Fact]
        public void Test_RouteList_FormatRouteList_WithOAuthHandlers()
        {
            var turnContext = new TurnContext(new NotImplementedAdapter(), MessageFactory.Text("test"));
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithOAuthHandlers(new[] { "graph", "sharepoint" })
                .Build());

            string result = routes.FormatRouteList(turnContext).Formatted;

            Assert.Contains("\"OAuthHandlers\":[\"graph\",\"sharepoint\"]", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_WithSingleOAuthHandler()
        {
            var turnContext = new TurnContext(new NotImplementedAdapter(), MessageFactory.Text("test"));
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithOAuthHandlers(new[] { "graph" })
                .Build());

            string result = routes.FormatRouteList(turnContext).Formatted;

            Assert.Contains("\"OAuthHandlers\":[\"graph\"]", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_NullChannelId_LogsWildcard()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("\"Channel\":\"*\"", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_WithCustomRank()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithOrderRank(100)
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.Contains("\"Rank\":100", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_AllProperties()
        {
            var turnContext = new TurnContext(new NotImplementedAdapter(), MessageFactory.Text("test"));
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .AsInvoke(true)
                .AsAgentic(true)
                .WithChannelId(Channels.Msteams)
                .WithOrderRank(500)
                .WithOAuthHandlers(new[] { "graph" })
                .Build());

            var (count, result) = routes.FormatRouteList(turnContext);

            Assert.Equal(1, count);
            Assert.Contains("\"Handler\":\"ApplicationRouteTests.MyNamedHandler\"", result);
            Assert.Contains("\"Flags\":\"Invoke,Agentic\"", result);
            Assert.Contains("\"Channel\":\"msteams\"", result);
            Assert.Contains("\"Rank\":500", result);
            Assert.Contains("\"OAuthHandlers\":[\"graph\"]", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_ContextDependentOAuthHandlers()
        {
            var activity = MessageFactory.Text("test");
            activity.CallerId = "urn:botframework:aadappid:some-app-id";
            var turnContext = new TurnContext(new NotImplementedAdapter(), activity);
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithOAuthHandlers(context => context.Activity.CallerId != null
                    ? new[] { "agenticGraph" }
                    : new[] { "graph" })
                .Build());

            string result = routes.FormatRouteList(turnContext).Formatted;

            Assert.Contains("\"OAuthHandlers\":[\"agenticGraph\"]", result);
        }

        [Fact]
        public void Test_RouteList_FormatRouteList_OAuthHandlers_OmittedWithoutContext()
        {
            RouteList routes = new();
            routes.AddRoute(RouteBuilder.Create()
                .WithSelector((ctx, ct) => Task.FromResult(true))
                .WithHandler(MyNamedHandler)
                .WithOAuthHandlers(new[] { "graph" })
                .Build());

            string result = routes.FormatRouteList().Formatted;

            Assert.DoesNotContain("OAuthHandlers", result);
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
            var activity1 = new MessageActivity("hello.1");
            activity1.Recipient = new() { Id = "recipientId" };
            activity1.Conversation = new() { Id = "conversationId" };
            activity1.From = new() { Id = "fromId" };
            activity1.ChannelId = "channelId";
            var activity2 = new MessageActivity("hello.2");
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

            app.AddRoute(MessageRouteBuilder.Create()
                .WithText("hello.1")
                .WithHandler((context, _, _) =>
                {
                    messages.Add(context.Activity.Text);
                    return Task.CompletedTask;
                })
                .Build());

            // Act
            await app.OnTurnAsync(turnContext1, CancellationToken.None);
            await app.OnTurnAsync(turnContext2, CancellationToken.None);

            // Assert
            Assert.Single(messages);
            Assert.Equal("hello.1", messages[0]);
        }

        [Fact]
        public async Task Test_Application_Route_CanResolveServicesFromTurnContext()
        {
            // Arrange
            var activity = MessageFactory.Text("hello.services");
            activity.Recipient = new() { Id = "recipientId" };
            activity.Conversation = new() { Id = "conversationId" };
            activity.From = new() { Id = "fromId" };
            activity.ChannelId = "channelId";
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            var turnState = await TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            ITurnState resolvedTurnState = null;
            AdaptiveCard resolvedAdaptiveCards = null;
            ProactiveApp resolvedProactive = null;

            var app = new AgentApplication(new(() => turnState)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            app.AddRoute(
                (context, _) => Task.FromResult(string.Equals("hello.services", context.Activity.Text)),
                (context, state, _) =>
                {
                    resolvedTurnState = context.Services.Get<ITurnState>();
                    resolvedAdaptiveCards = context.Services.Get<AdaptiveCard>();
                    resolvedProactive = context.Services.Get<ProactiveApp>();

                    Assert.Same(state, resolvedTurnState);
                    return Task.CompletedTask;
                },
                false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Same(turnState, resolvedTurnState);
            Assert.Same(app.AdaptiveCards, resolvedAdaptiveCards);
            Assert.Same(app.Proactive, resolvedProactive);
        }

        [Fact]
        public void Test_Application_SetTurnContextServices_SetsCoreServices()
        {
            // Arrange
            var activity = MessageFactory.Text("hello.services.core");
            activity.Recipient = new() { Id = "recipientId" };
            activity.Conversation = new() { Id = "conversationId" };
            activity.From = new() { Id = "fromId" };
            activity.ChannelId = "channelId";

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            ITurnState turnState = new TurnState();
            var app = new AgentApplication(new AgentApplicationOptions((IStorage)null)
            {
                StartTypingTimer = false,
            });

            // Act
            app.SetTurnContextServices(turnContext, turnState);

            // Assert
            Assert.Same(turnState, turnContext.Services.Get<ITurnState>());
            Assert.Same(app.AdaptiveCards, turnContext.Services.Get<AdaptiveCard>());
            Assert.Same(app.Proactive, turnContext.Services.Get<ProactiveApp>());
            Assert.Null(turnContext.Services.Get<UserAuthorization>());
        }

        [Fact]
        public void Test_Application_SetTurnContextServices_SetsUserAuthorization_WhenConfigured()
        {
            // Arrange
            var activity = MessageFactory.Text("hello.services.auth");
            activity.Recipient = new() { Id = "recipientId" };
            activity.Conversation = new() { Id = "conversationId" };
            activity.From = new() { Id = "fromId" };
            activity.ChannelId = "channelId";

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, activity);
            ITurnState turnState = new TurnState();
            var connections = new Moq.Mock<IConnections>();
            var handler = new Moq.Mock<Microsoft.Agents.Builder.UserAuth.IUserAuthorization>();
            handler.SetupGet(h => h.Name).Returns("test");

            var app = new AgentApplication(new AgentApplicationOptions((IStorage)new MemoryStorage())
            {
                StartTypingTimer = false,
                UserAuthorization = new UserAuthorizationOptions(
                    NullLoggerFactory.Instance,
                    new MemoryStorage(),
                    connections.Object,
                    handler.Object)
                {
                    AutoSignIn = UserAuthorizationOptions.AutoSignInOff,
                    DefaultHandlerName = "test"
                }
            });

            // Act
            app.SetTurnContextServices(turnContext, turnState);

            // Assert
            Assert.Same(turnState, turnContext.Services.Get<ITurnState>());
            Assert.Same(app.AdaptiveCards, turnContext.Services.Get<AdaptiveCard>());
            Assert.Same(app.Proactive, turnContext.Services.Get<ProactiveApp>());
            Assert.Same(app.UserAuthorization, turnContext.Services.Get<UserAuthorization>());
        }

        [Fact]
        public async Task Test_Application_Routes_Are_Called_InOrder()
        {
            // Arrange
            var activity = new MessageActivity("hello.1");
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
            app.AddRoute(MessageRouteBuilder.Create()
                .WithText("hello")
                .WithHandler((context, _, _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                })
                .Build());
            app.AddRoute(MessageRouteBuilder.Create()
                .WithText("hello.1")
                .WithHandler((context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                })
                .Build());
            app.AddRoute(MessageRouteBuilder.Create()
                .WithHandler((context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity1 = new InvokeActivity {
                Name = "invoke.1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new InvokeActivity {
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
            app.AddRoute(InvokeRouteBuilder.Create()
                .WithName("invoke.1")
                .WithHandler((context, _, _) =>
                {
                    names.Add(context.Activity.Name);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity = new InvokeActivity {
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
            app.OnInvoke("invoke", (context, _, _) =>
            {
                selectedRoutes.Add(0);
                return Task.CompletedTask;
            });
            app.OnInvoke("invoke.1", (context, _, _) =>
            {
                selectedRoutes.Add(1);
                return Task.CompletedTask;
            });
            app.AddRoute(InvokeRouteBuilder.Create()
                .WithHandler((ITurnContext<IInvokeActivity> context, ITurnState _, CancellationToken _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity = new InvokeActivity {
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
            app.AddRoute(InvokeRouteBuilder.Create()
                .WithHandler((ITurnContext<IInvokeActivity> context, ITurnState _, CancellationToken _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                })
                .Build());
            app.AddRoute(RouteBuilder.Create()
                .WithSelector((_, _) => Task.FromResult(true))
                .WithHandler((context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity = new InvokeActivity {
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
            app.AddRoute(InvokeRouteBuilder.Create()
                .WithSelector((_, _) => Task.FromResult(false))
                .WithHandler((ITurnContext<IInvokeActivity> context, ITurnState _, CancellationToken _) =>
                {
                    selectedRoutes.Add(0);
                    return Task.CompletedTask;
                })
                .Build());
            app.AddRoute(RouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(
                    context.Activity is IInvokeActivity invokeActivity
                    && string.Equals("invoke.1", invokeActivity.Name)))
                .WithHandler((context, _, _) =>
                {
                    selectedRoutes.Add(1);
                    return Task.CompletedTask;
                })
                .Build());
            app.AddRoute(RouteBuilder.Create()
                .WithSelector((_, _) => Task.FromResult(true))
                .WithHandler((context, _, _) =>
                {
                    selectedRoutes.Add(2);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity1 = new MessageActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new InvokeActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new MessageActivity {
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
            var activity1 = new MessageActivity {
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
            var agenticActivity = new MessageActivity {
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
            var activity1 = new MessageActivity {
                Id = "Message",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new InvokeActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new MessageActivity {
                Id = "Message",
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
            app.AddRoute(RouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(context.Activity?.Id != null))
                .WithHandler((context, _, _) =>
                {
                    agenticTypes.Add(context.Activity.Type);
                    return Task.CompletedTask;
                })
                .AsAgentic(true)
                .Build());

            // non-agentic
            app.AddRoute(RouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(context.Activity?.Id != null))
                .WithHandler((context, _, _) =>
                {
                    types.Add(context.Activity.Type);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity1 = new MessageActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                Id = "Delete",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new InvokeActivity {
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
            app.AddRoute(RouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(
                    context.Activity.IsType(ActivityTypes.Invoke)
                    || new Regex("^message$").IsMatch(context.Activity.Type ?? string.Empty)
                    || context.Activity?.Id != null))
                .WithHandler((context, _, _) =>
                {
                    types.Add(context.Activity.Type);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity1 = new ConversationUpdateActivity {
                MembersAdded = new List<ChannelAccount> { new() },
                Id = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new ConversationUpdateActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new InvokeActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new ConversationUpdateActivity {
                MembersAdded = new List<ChannelAccount> { new() },
                Id = "agentic1",
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
                agenticNames.Add(context.Activity.Id);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, (context, _, _) =>
            {
                names.Add(context.Activity.Id);
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
            var activity1 = new ConversationUpdateActivity {
                MembersRemoved = new List<ChannelAccount> { new() },
                Id = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity2 = new ConversationUpdateActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var activity3 = new InvokeActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };
            var agenticActivity = new ConversationUpdateActivity {
                MembersRemoved = new List<ChannelAccount> { new() },
                Id = "agentic1",
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
                agenticNames.Add(context.Activity.Id);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnConversationUpdate(ConversationUpdateEvents.MembersRemoved, (context, _, _) =>
            {
                names.Add(context.Activity.Id);
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
            var activity = new ConversationUpdateActivity {
                Id = "1",
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
                    names.Add(context.Activity.Id);
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
            var activity1 = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new MessageActivity {
                Text = "HELLO",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new EventActivity("hello") {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var nullTextActivity = new MessageActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };

            var adapter = new NotImplementedAdapter();
            var turnContext1 = new TurnContext(adapter, activity1);
            var turnContext2 = new TurnContext(adapter, activity2);
            var turnContext3 = new TurnContext(adapter, activity3);
            var agenticTurnContext = new TurnContext(adapter, agenticActivity);
            var nullTextContext = new TurnContext(adapter, nullTextActivity);
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
            await app.OnTurnAsync(nullTextContext, CancellationToken.None);

            // Assert
            Assert.Equal(2, texts.Count);
            Assert.Single(agenticTexts);
        }

        [Fact]
        public async Task Test_OnMessage_StringWord_Selector()
        {
            // Arrange
            var activity1 = new MessageActivity {
                Text = "hello a",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new MessageActivity {
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new MessageActivity {
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
            var activity1 = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new MessageActivity {
                Text = "welcome",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new InvokeActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new MessageActivity {
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
            var activity1 = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new InvokeActivity {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new MessageActivity {
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
            app.AddRoute(MessageRouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(((IMessageActivity)context.Activity).Text != null))
                .WithHandler((context, _, _) =>
                {
                    agenticTexts.Add(context.Activity.Text);
                    return Task.CompletedTask;
                })
                .AsAgentic(true)
                .Build());

            // non-agentic
            app.AddRoute(MessageRouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(((IMessageActivity)context.Activity).Text != null))
                .WithHandler((context, _, _) =>
                {
                    texts.Add(context.Activity.Text);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity1 = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new MessageActivity {
                Text = "welcome",
                Id = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new MessageActivity {
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
            app.AddRoute(MessageRouteBuilder.Create()
                .WithSelector((context, _) => Task.FromResult(
                    string.Equals(((IMessageActivity)context.Activity).Text, "hello", StringComparison.OrdinalIgnoreCase)
                    || (context.Activity is IMessageActivity messageActivity && messageActivity.Text != null && new Regex(@"\bworld\b").IsMatch(messageActivity.Text))
                    || context.Activity?.Id != null))
                .WithHandler((context, _, _) =>
                {
                    texts.Add(context.Activity.Text);
                    return Task.CompletedTask;
                })
                .Build());

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
            var activity1 = new MessageReactionActivity
            {
                ReactionsAdded = new List<MessageReaction> { new() },
                Id = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new MessageReactionActivity
            {
                Id = "2",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new MessageActivity()
            {
                Id = "3",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new MessageReactionActivity
            {
                ReactionsAdded = new List<MessageReaction> { new() },
                Id = "agentic1",
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
                agenticNames.Add(context.Activity.Id);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessageReactionsAdded((context, _, _) =>
            {
                names.Add(context.Activity.Id);
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
            var activity1 = new MessageReactionActivity
            {
                ReactionsRemoved = new List<MessageReaction> { new() },
                Id = "1",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new MessageReactionActivity
            {
                Id = "2",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new MessageActivity()
            {
                Id = "3",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new MessageReactionActivity
            {
                ReactionsRemoved = new List<MessageReaction> { new() },
                Id = "agentic1",
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
                agenticNames.Add(context.Activity.Id);
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // non-agentic
            app.OnMessageReactionsRemoved((context, _, _) =>
            {
                names.Add(context.Activity.Id);
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
            var activity1 = new InvokeActivity {
                Name = "handoff/action",
                Value = new { Continuation = "test" },
                Id = "test",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity2 = new EventActivity("actionableMessage/executeAction") {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var activity3 = new InvokeActivity {
                Name = "composeExtension/queryLink",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId"
            };
            var agenticActivity = new InvokeActivity {
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
            Assert.Equivalent(expectedInvokeResponse, ((IInvokeResponseActivity)activitiesToSend[0]).Value);

            Assert.Single(agenticIds);
            Assert.Equal("agentic test", agenticIds[0]);
        }

        [Fact]
        public async Task Test_AgenticRoute_BeatsNonAgenticRoute_ForAgenticRequest()
        {
            // Arrange - An agentic message should be handled by the agentic route,
            // not by a competing non-agentic route registered first.
            var agenticActivity = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Register non-agentic route first
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "nonAgentic";
                return Task.CompletedTask;
            }, isAgenticOnly: false);

            // Register agentic route second
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "agentic";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert - agentic route wins due to route ordering priority
            Assert.Equal("agentic", handlerCalled);
        }

        [Fact]
        public async Task Test_TwoAgenticRoutes_SameRank_FirstMatchingWins()
        {
            // Arrange - Two agentic routes with the same rank; first matching selector wins.
            var agenticActivity = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Both agentic, same rank, both match - first registered wins
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "first";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "second";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal("first", handlerCalled);
        }

        [Fact]
        public async Task Test_NonAgenticRequest_SkipsAgenticRoutes()
        {
            // Arrange - A regular (non-agentic) message must not match an agentic-only route
            // and should fall through to the non-agentic handler.
            var normalActivity = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, normalActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Agentic route registered first (higher priority in ordering)
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "agentic";
                return Task.CompletedTask;
            }, isAgenticOnly: true);

            // Non-agentic route registered second
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "nonAgentic";
                return Task.CompletedTask;
            }, isAgenticOnly: false);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert - non-agentic request should skip the agentic route
            Assert.Equal("nonAgentic", handlerCalled);
        }

        [Fact]
        public async Task Test_AgentExtension_AddRoute_PropagatesAgenticFlag()
        {
            // Arrange - Verify that isAgenticOnly=true flows through AgentExtension.AddRoute
            // and results in the route being ordered above non-agentic routes.
            // This is the pattern used by A365 Notifications and other extensions.
            var agenticActivity = new MessageActivity {
                Text = "hello",
                Recipient = new() { Id = "recipientId", Role = RoleTypes.AgenticUser },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "testChannel",
            };

            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, agenticActivity);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
            });

            string handlerCalled = null;

            // Register non-agentic route directly on the app first
            app.OnActivity(ActivityTypes.Message, (context, _, _) =>
            {
                handlerCalled = "nonAgentic";
                return Task.CompletedTask;
            }, isAgenticOnly: false);

            // Register agentic route through the extension model (mimics A365 Notifications pattern)
            var extension = new TestExtension("testChannel");
            extension.AddRoute(
                app,
                RouteBuilder.Create()
                    .WithSelector((context, _) => Task.FromResult(context.Activity.Type == ActivityTypes.Message))
                    .WithHandler((context, _, _) =>
                    {
                        handlerCalled = "extensionAgentic";
                        return Task.CompletedTask;
                    })
                    .AsAgentic(true)
                    .Build());

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert - extension's agentic route wins due to ordering priority
            Assert.Equal("extensionAgentic", handlerCalled);
        }

        private class TestExtension : AgentExtension
        {
            public TestExtension(string channelId)
            {
                ChannelId = new ChannelId(channelId);
            }
        }

        [Fact]
        public async Task Test_AgentApplication_LogsRouteList_WhenDebugEnabled()
        {
            // Arrange: create a logger that captures debug messages
            var logMessages = new List<string>();
            var loggerFactory = LoggerFactory.Create(builder =>
                builder
                    .AddProvider(new CaptureLoggerProvider(logMessages))
                    .SetMinimumLevel(LogLevel.Debug));

            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new MessageActivity("hi")
            {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });

            var options = new AgentApplicationOptions((IStorage)null, loggerFactory)
            {
                StartTypingTimer = false,
            };
            var app = new AgentApplication(options);
            app.OnActivity(ActivityTypes.Message, (ctx, ts, ct) => Task.CompletedTask);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert: at least one debug message contains route evaluation order text
            Assert.Contains(logMessages, m => m.Contains("route evaluation order"));
        }

        [Fact]
        public async Task Test_AgentApplication_DoesNotLogRouteList_WhenDebugDisabled()
        {
            var logMessages = new List<string>();
            var loggerFactory = LoggerFactory.Create(builder =>
                builder
                    .AddProvider(new CaptureLoggerProvider(logMessages))
                    .SetMinimumLevel(LogLevel.Information));

            var adapter = new SimpleAdapter();
            var turnContext = new TurnContext(adapter, new MessageActivity("hi")
            {
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelId = "channelId",
            });

            var options = new AgentApplicationOptions((IStorage)null, loggerFactory)
            {
                StartTypingTimer = false,
            };
            var app = new AgentApplication(options);
            app.OnActivity(ActivityTypes.Message, (ctx, ts, ct) => Task.CompletedTask);

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.DoesNotContain(logMessages, m => m.Contains("route evaluation order"));
        }

        private class CaptureLoggerProvider(List<string> messages) : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new CaptureLogger(messages);
            public void Dispose() { }
        }

        private class CaptureLogger(List<string> messages) : ILogger
        {
            private static readonly IDisposable _noopScope = new NoopDisposable();
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => _noopScope;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception exception, Func<TState, Exception, string> formatter)
                => messages.Add(formatter(state, exception));
            private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
        }
    }
}
