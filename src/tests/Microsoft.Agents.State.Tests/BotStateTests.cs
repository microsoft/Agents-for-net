// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Testing;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Moq;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    public class BotStateTests
    {
        [Fact]
        public async Task State_EmptyName()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await Assert.ThrowsAsync<ArgumentException>(() => userState.GetPropertyAsync<string>(context, string.Empty, () => string.Empty, default));
        }

        [Fact]
        public async Task State_NullName()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await Assert.ThrowsAsync<ArgumentNullException>(() => userState.GetPropertyAsync<string>(context, null, () => string.Empty, default));
        }

        [Fact]
        public async Task State_WriteAsyncStoreItem()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var memory = new MemoryStorage(dictionary: dictionary);

            var changes = new Dictionary<string, object>()
            {
                { "customState", new CustomState() },
            };
            await memory.WriteAsync(changes, CancellationToken.None);
            var result = await memory.ReadAsync(new string[] { "customState" }, CancellationToken.None);

            Assert.Equal("0", dictionary["customState"]["ETag"].ToString());
            Assert.Equal("0", (result["customState"] as CustomState).ETag);
        }

        [Fact]
        public async Task MakeSureStorageNotCalledNoChangesAsync()
        {
            // Mock a storage provider, which counts read/writes
            var storeCount = 0;
            var readCount = 0;
            var dictionary = new Dictionary<string, object>();
            var mock = new Mock<IStorage>();
            mock.Setup(ms => ms.WriteAsync(It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => storeCount++);
            mock.Setup(ms => ms.ReadAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(result: (IDictionary<string, object>)dictionary))
                .Callback(() => readCount++);

            // Arrange
            var userState = new UserState(mock.Object);
            var context = TestUtilities.CreateEmptyContext();

            // Act
            Assert.Equal(0, storeCount);
            await userState.SaveChangesAsync(context);
            await userState.SetPropertyAsync(context, "propertyA", "hello", default);
            Assert.Equal(1, readCount);       // Initial save bumps count
            Assert.Equal(0, storeCount);       // Initial save bumps count
            await userState.SetPropertyAsync(context, "propertyA", "there", default);
            Assert.Equal(0, storeCount);       // Set on property should not bump
            await userState.SaveChangesAsync(context);
            Assert.Equal(1, storeCount);       // Explicit save should bump
            var valueA = await userState.GetPropertyAsync(context, "propertyA", () => string.Empty, default);
            Assert.Equal("there", valueA);
            Assert.Equal(1, storeCount);       // Gets should not bump
            await userState.SaveChangesAsync(context);
            Assert.Equal(1, storeCount);
            await userState.DeletePropertyAsync(context, "propertyA", default);   // Delete alone no bump
            Assert.Equal(1, storeCount);
            await userState.SaveChangesAsync(context);  // Save when dirty should bump
            Assert.Equal(2, storeCount);
            Assert.Equal(1, readCount);
            await userState.SaveChangesAsync(context);  // Save not dirty should not bump
            Assert.Equal(2, storeCount);
            Assert.Equal(1, readCount);
        }

        [Fact]
        public async Task State_SetNoLoad()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.SetPropertyAsync(context, "propertyA", "hello", default);
        }

        [Fact]
        public async Task State_MultipleLoads()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            await userState.LoadAsync(context);
        }

        [Fact]
        public async Task State_GetNoLoadWithDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var valueA = await userState.GetPropertyAsync(context, "propertyA", () => "Default!", default);
            Assert.Equal("Default!", valueA);
        }

        [Fact]
        public async Task State_GetNoLoadNoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var valueA = await userState.GetPropertyAsync<string>(context, "propertyA", null, default);

            // Assert
            Assert.Null(valueA);
        }

        [Fact]
        public async Task State_POCO_NoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var value = await userState.GetPropertyAsync<TestPocoState>(context, "test", null, default);

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public async Task State_bool_NoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var value = await userState.GetPropertyAsync<bool>(context, "test", null, default);

            // Assert
            Assert.False(value);
        }

        [Fact]
        public async Task State_int_NoDefault()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var value = await userState.GetPropertyAsync<int>(context, "test", null, default);

            // Assert
            Assert.Equal(0, value);
        }

        [Fact]
        public async Task State_SetAfterSave()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            await userState.SetPropertyAsync(context, "property-a", "hello", default);
            await userState.SetPropertyAsync(context, "property-b", "world", default);
            await userState.SaveChangesAsync(context);

            await userState.SetPropertyAsync(context, "property-a", "hello2", default);
        }

        [Fact]
        public async Task State_MultipleSave()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            await userState.SetPropertyAsync(context, "property-a", "hello", default);
            await userState.SetPropertyAsync(context, "property-b", "world", default);
            await userState.SaveChangesAsync(context);

            await userState.SetPropertyAsync(context, "property-a", "hello2", default);
            await userState.SaveChangesAsync(context);
            var valueA = await userState.GetPropertyAsync<string>(context, "property-a", null, default);
            Assert.Equal("hello2", valueA);
        }

        [Fact]
        public async Task LoadSetSave()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.LoadAsync(context);
            await userState.SetPropertyAsync(context, "property-a", "hello", default);
            await userState.SetPropertyAsync(context, "property-b", "world", default);
            await userState.SaveChangesAsync(context);

            // Assert
            var obj = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello", obj["property-a"].ToString());
            Assert.Equal("world", obj["property-b"].ToString());
        }

        [Fact]
        public async Task LoadSetSaveTwice()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState.LoadAsync(context);
            await userState.SetPropertyAsync(context, "property-a", "hello", default);
            await userState.SetPropertyAsync(context, "property-b", "world", default);
            await userState.SetPropertyAsync(context, "property-c", "test", default);
            await userState.SaveChangesAsync(context);

            // Assert
            var obj = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello", obj["property-a"].ToString());
            Assert.Equal("world", obj["property-b"].ToString());

            // Act 2
            var userState2 = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState2.LoadAsync(context);
            await userState2.SetPropertyAsync(context, "property-a", "hello-2", default);
            await userState2.SetPropertyAsync(context, "property-b", "world-2", default);
            await userState2.SaveChangesAsync(context);

            // Assert 2
            var obj2 = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello-2", obj2["property-a"].ToString());
            Assert.Equal("world-2", obj2["property-b"].ToString());
            Assert.Equal("test", obj2["property-c"].ToString());
        }

        [Fact]
        public async Task LoadSaveDelete()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var context = TestUtilities.CreateEmptyContext();

            // Act
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState.LoadAsync(context);
            await userState.SetPropertyAsync(context, "property-a", "hello", default);
            await userState.SetPropertyAsync(context, "property-b", "world", default);
            await userState.SaveChangesAsync(context);

            // Assert
            var obj = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello", obj["property-a"].ToString());
            Assert.Equal("world", obj["property-b"].ToString());

            // Act 2
            var userState2 = new UserState(new MemoryStorage(dictionary: dictionary));

            await userState2.LoadAsync(context);
            await userState2.SetPropertyAsync(context, "property-a", "hello-2", default);
            await userState2.DeletePropertyAsync(context, "property-b", default);
            await userState2.SaveChangesAsync(context);

            // Assert 2
            var obj2 = dictionary["EmptyContext/users/empty@empty.context.org"];
            Assert.Equal("hello-2", obj2["property-a"].ToString());
            Assert.Null(obj2["property-b"]);
        }

        [Fact]
        public async Task State_DoNOTRememberContextState()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_DoNOTRememberContextState"));

            await new TestFlow(adapter, (context, cancellationToken) =>
            {
                var obj = context.TurnState.Get<UserState>();
                Assert.Null(obj);
                return Task.CompletedTask;
            })
            .Send("set value")
            .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberIStoreItemUserState()
        {
            var userState = new UserState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberIStoreItemUserState"))
                .Use(new AutoSaveStateMiddleware(userState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                {
                    var state = await userState.GetPropertyAsync(context, "test", () => new TestState(), default);
                    Assert.NotNull(state);
                    switch (context.Activity.Text)
                    {
                        case "set value":
                            state.Value = "test";
                            await context.SendActivityAsync("value saved");
                            break;
                        case "get value":
                            await context.SendActivityAsync(state.Value);
                            break;
                    }
                })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberPocoUserState()
        {
            var userState = new UserState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("tate_RememberPocoUserState"))
                .Use(new AutoSaveStateMiddleware(userState));
            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var testPocoState = await userState.GetPropertyAsync(context, "testPoco", () => new TestPocoState(), cancellationToken);
                        Assert.NotNull(userState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                testPocoState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(testPocoState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberIStoreItemConversationState()
        {
            var userState = new UserState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberIStoreItemConversationState"))
                .Use(new AutoSaveStateMiddleware(userState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var conversationState = await userState.GetPropertyAsync(context, "test", () => new TestState(), cancellationToken);
                        Assert.NotNull(conversationState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversationState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversationState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberPocoConversationState()
        {
            var userState = new UserState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberPocoConversationState"))
                .Use(new AutoSaveStateMiddleware(userState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var conversationState = await userState.GetPropertyAsync(context, "testPoco", () => new TestPocoState(), cancellationToken);
                        Assert.NotNull(conversationState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversationState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversationState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RememberPocoPrivateConversationState()
        {
            var privateConversationState = new PrivateConversationState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RememberPocoPrivateConversationState"))
                .Use(new AutoSaveStateMiddleware(privateConversationState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var conversationState = await privateConversationState.GetPropertyAsync(context, "testPoco", () => new TestPocoState(), cancellationToken);
                        Assert.NotNull(conversationState);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversationState.Value = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversationState.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "test")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_CustomStateManagerTest()
        {
            var testGuid = Guid.NewGuid().ToString();
            var customState = new CustomKeyState(new MemoryStorage());

            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_CustomStateManagerTest"))
                .Use(new AutoSaveStateMiddleware(customState));

            await new TestFlow(adapter, async (context, cancellationToken) =>
                    {
                        var test = await customState.GetPropertyAsync(context, "test", () => new TestPocoState(), cancellationToken);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                test.Value = testGuid;
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(test.Value);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", testGuid.ToString())
                .StartTestAsync();
        }

        [Fact]
        public async Task State_RoundTripTypedObject()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_RoundTripTypedObject"))
                .Use(new AutoSaveStateMiddleware(convoState));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var conversation = await convoState.GetPropertyAsync(context, "typed", () => new TypedObject(), cancellationToken);
                        Assert.NotNull(conversation);
                        switch (context.Activity.Text)
                        {
                            case "set value":
                                conversation.Name = "test";
                                await context.SendActivityAsync("value saved");
                                break;
                            case "get value":
                                await context.SendActivityAsync(conversation.GetType().Name);
                                break;
                        }
                    })
                .Test("set value", "value saved")
                .Test("get value", "TypedObject")
                .StartTestAsync();
        }

        [Fact]
        public async Task State_UseBotStateDirectly()
        {
            var adapter = new TestAdapter(TestAdapter.CreateConversation("State_UseBotStateDirectly"));

            await new TestFlow(
                adapter,
                async (context, cancellationToken) =>
                    {
                        var botStateManager = new TestBotState(new MemoryStorage());

                        // read initial state object
                        await botStateManager.LoadAsync(context);

                        var customState = await botStateManager.GetPropertyAsync(context, "test", () => new CustomState(), cancellationToken);

                        // this should be a 'new CustomState' as nothing is currently stored in storage
                        Assert.NotNull(customState);
                        Assert.IsType<CustomState>(customState);
                        Assert.Null(customState.CustomString);

                        // amend property and write to storage
                        customState.CustomString = "test";
                        await botStateManager.SaveChangesAsync(context);

                        customState.CustomString = "asdfsadf";

                        // read into context again
                        await botStateManager.LoadAsync(context, force: true);

                        customState = await botStateManager.GetPropertyAsync(context, "test", () => new CustomState(), cancellationToken);

                        // check object read from value has the correct value for CustomString
                        Assert.Equal("test", customState.CustomString);
                    })
                .Send(new Activity() { Type = ActivityTypes.ConversationUpdate })
                .StartTestAsync();
        }

        [Fact]
        public async Task UserState_BadFromThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();
            context.Activity.From = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => userState.GetPropertyAsync<TestPocoState>(context, "test", null, default));
        }

        [Fact]
        public async Task ConversationState_BadConverationThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new ConversationState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();
            context.Activity.Conversation = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => userState.GetPropertyAsync<TestPocoState>(context, "test", null, default));
        }

        [Fact]
        public async Task PrivateConversationState_BadActivityFromThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new PrivateConversationState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();
            context.Activity.Conversation = null;
            context.Activity.From = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => userState.GetPropertyAsync<TestPocoState>(context, "test", null, default));
        }

        [Fact]
        public async Task PrivateConversationState_BadActivityConversationThrows()
        {
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new PrivateConversationState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();
            context.Activity.Conversation = null;
            await Assert.ThrowsAsync<InvalidOperationException>(() => userState.GetPropertyAsync<TestPocoState>(context, "test", null, default));
        }

        [Fact]
        public async Task ClearAndSave()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());

            // Turn 0
            var botState1 = new ConversationState(storage);
            (await botState1
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState(), default)).Value = "test-value";
            await botState1.SaveChangesAsync(turnContext);

            // Turn 1
            var botState2 = new ConversationState(storage);
            var value1 = (await botState2
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState { Value = "default-value" }, default)).Value;

            Assert.Equal("test-value", value1);

            // Turn 2
            var botState3 = new ConversationState(storage);
            await botState3.ClearStateAsync(turnContext);
            await botState3.SaveChangesAsync(turnContext);

            // Turn 3
            var botState4 = new ConversationState(storage);
            var value2 = (await botState4
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState { Value = "default-value" }, default)).Value;

            Assert.Equal("default-value", value2);
        }

        [Fact]
        public async Task BotStateDelete()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());

            // Turn 0
            var botState1 = new ConversationState(storage);
            (await botState1
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState(), default)).Value = "test-value";
            await botState1.SaveChangesAsync(turnContext);

            // Turn 1
            var botState2 = new ConversationState(storage);
            var value1 = (await botState2
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState { Value = "default-value" }, default)).Value;

            Assert.Equal("test-value", value1);

            // Turn 2
            var botState3 = new ConversationState(storage);
            await botState3.DeleteStateAsync(turnContext);

            // Turn 3
            var botState4 = new ConversationState(storage);
            var value2 = (await botState4
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState { Value = "default-value" }, default)).Value;

            Assert.Equal("default-value", value2);
        }

        [Fact]
        public async Task BotStateGet()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());

            // This was changed from ConversationSate to TestBotState
            // because TestBotState has a context service key
            // that is different from the name of its type
            var botState = new TestBotState(storage);
            (await botState
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState(), default)).Value = "test-value";

            var json = JsonObject.Create(botState.Get(turnContext));

            Assert.Equal("test-value", json["test-name"]["value"].ToString());
        }

        [Fact]
        public async Task BotStateGetCachedState()
        {
            var turnContext = TestUtilities.CreateEmptyContext();
            turnContext.Activity.Conversation = new ConversationAccount { Id = "1234" };

            var storage = new MemoryStorage(dictionary: new Dictionary<string, JsonObject>());
            var botState = new TestBotState(storage);

            (await botState
                .GetPropertyAsync(turnContext, "test-name", () => new TestPocoState(), default)).Value = "test-value";

            var cache = botState.GetCachedState(turnContext);

            Assert.NotNull(cache);

            Assert.Same(cache, botState.GetCachedState(turnContext));
        }

        [Fact]
        public async Task State_ForceIsNoOpWithoutCachedBotState()
        {
            // Arrange
            var dictionary = new Dictionary<string, JsonObject>();
            var userState = new UserState(new MemoryStorage(dictionary: dictionary));
            var context = TestUtilities.CreateEmptyContext();

            // Act
            await userState.SaveChangesAsync(context, true);
        }

        [Fact]
        public async Task State_ForceCallsSaveWithoutCachedBotStateChanges()
        {
            // Mock a storage provider, which counts writes
            var storeCount = 0;
            var dictionary = new Dictionary<string, object>();
            var mock = new Mock<IStorage>();
            mock.Setup(ms => ms.WriteAsync(It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => storeCount++);
            mock.Setup(ms => ms.ReadAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(result: (IDictionary<string, object>)dictionary));

            // Arrange
            var userState = new UserState(mock.Object);
            var context = TestUtilities.CreateEmptyContext();

            // Act
            // Set initial value and save
            await userState.SetPropertyAsync(context, "propertyA", "test", default);
            await userState.SaveChangesAsync(context);

            // Assert
            Assert.Equal(1, storeCount);

            // Saving without changes and wthout force does NOT call .WriteAsync
            await userState.SaveChangesAsync(context);
            Assert.Equal(1, storeCount);

            // Forcing save without changes DOES call .WriteAsync
            await userState.SaveChangesAsync(context, true);
            Assert.Equal(2, storeCount);
        }

        public class TypedObject
        {
            public string Name { get; set; }
        }

        public class TestBotState : BotState
        {
            public TestBotState(IStorage storage)
                : base(storage, $"BotState:{typeof(BotState).Namespace}.{typeof(BotState).Name}")
            {
            }

            protected override string GetStorageKey(ITurnContext turnContext) => $"botstate/{turnContext.Activity.ChannelId}/{turnContext.Activity.Conversation.Id}/{typeof(BotState).Namespace}.{typeof(BotState).Name}";
        }

        public class CustomState : IStoreItem
        {
            public string CustomString { get; set; }

            public string ETag { get; set; }
        }

        public class CustomKeyState : BotState
        {
            public const string PropertyName = "Microsoft.Bot.Builder.Tests.CustomKeyState";

            public CustomKeyState(IStorage storage)
                : base(storage, PropertyName)
            {
            }

            protected override string GetStorageKey(ITurnContext turnContext) => "CustomKey";
        }
    }
}
