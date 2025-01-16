// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    public class BotStateSetTests
    {
        [Fact]
        public void BotStateSet_Properties()
        {
            var storage = new MemoryStorage();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var stateSet = new BotStateSet(userState, convState);

            Assert.Equal(2, stateSet.BotStates.Count);

            Assert.IsType<UserState>(stateSet.BotStates[nameof(UserState)]);
            Assert.IsType<ConversationState>(stateSet.BotStates[nameof(ConversationState)]);
        }

        [Fact]
        public async Task BotStateSet_LoadAsync()
        {
            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();
            {
                // setup userstate
                var userState = new UserState(storage);

                // setup convState
                var convState = new ConversationState(storage);

                var stateSet = new BotStateSet(userState, convState);

                Assert.Equal(2, stateSet.BotStates.Count);

                var userCount = await userState.GetPropertyAsync(turnContext, "userCount", () => 0, default);
                Assert.Equal(0, userCount);
                var convCount = await convState.GetPropertyAsync(turnContext, "convCount", () => 0, default);
                Assert.Equal(0, convCount);

                await userState.SetPropertyAsync(turnContext, "userCount", 10, default);
                await convState.SetPropertyAsync(turnContext, "convCount", 20, default);

                await stateSet.SaveAllChangesAsync(turnContext);
            }

            {
                // setup userstate
                var userState = new UserState(storage);

                // setup convState
                var convState = new ConversationState(storage);

                var stateSet = new BotStateSet(userState, convState);

                await stateSet.LoadAllAsync(turnContext);

                var userCount = await userState.GetPropertyAsync(turnContext, "userCount", () => 0, default);
                Assert.Equal(10, userCount);
                var convCount = await convState.GetPropertyAsync(turnContext, "convCount", () => 0, default);
                Assert.Equal(20, convCount);
            }
        }

        [Fact]
        public async Task BotStateSet_ReturnsDefaultForNullValueType()
        {
            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var stateSet = new BotStateSet(userState, convState);

            Assert.Equal(2, stateSet.BotStates.Count);

            var userObject = await userState.GetPropertyAsync<string>(turnContext, "userStateObject", () => null, default);
            Assert.Null(userObject);

            // Ensure we also get null on second attempt
            userObject = await userState.GetPropertyAsync<string>(turnContext, "userStateObject", () => null, default);
            Assert.Null(userObject);

            var convObject = await convState.GetPropertyAsync<string>(turnContext, "convStateObject", () => null, default);
            Assert.Null(convObject);

            // Ensure we also get null on second attempt
            convObject = await convState.GetPropertyAsync<string>(turnContext, "convStateObject", () => null, default);
            Assert.Null(convObject);
        }

        [Fact]
        public async Task BotStateSet_SaveAsync()
        {
            var storage = new MemoryStorage();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var stateSet = new BotStateSet(userState, convState);

            Assert.Equal(2, stateSet.BotStates.Count);
            var context = TestUtilities.CreateEmptyContext();
            await stateSet.LoadAllAsync(context);

            var userCount = await userState.GetPropertyAsync(context, "userCount", () => 0, default);
            Assert.Equal(0, userCount);
            var convCount = await convState.GetPropertyAsync(context, "convCount", () => 0, default);
            Assert.Equal(0, convCount);

            await userState.SetPropertyAsync(context, "userCount", 10, default);
            await convState.SetPropertyAsync(context, "convCount", 20, default);

            await stateSet.SaveAllChangesAsync(context);

            userCount = await userState.GetPropertyAsync(context, "userCount", () => 0, default);
            Assert.Equal(10, userCount);

            convCount = await convState.GetPropertyAsync(context, "convCount", () => 0, default);
            Assert.Equal(20, convCount);
        }

        internal class SomeComplexType
        {
            public string PropA { get; set; }

            public int PropB { get; set; }
        }
    }
}
