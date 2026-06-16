// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class TurnContextWrapperTests
    {

        [Fact]
        public void Constructor_ShouldThrowOnNullTurnContext()
        {
            Assert.Throws<ArgumentNullException>(() => new TestTurnContextWrapper((TurnContext)null));
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var context = new TestTurnContextWrapper(new TurnContext(new SimpleAdapter(), new Activity()));
            Assert.NotNull(context);
        }

        [Fact]
        public void Wrapper_ShouldExposeInnerTurnContextProperties()
        {
            var adapter = new SimpleAdapter();
            var activity = TestMessage.Message();
            var innerContext = new TurnContext(adapter, activity);
            innerContext.StackState.Set("key", "value");
            innerContext.Services.Set("service", "test-service");

            var context = new TestTurnContextWrapper(innerContext);

            Assert.Same(adapter, context.Adapter);
            Assert.Same(activity, context.Activity);
            Assert.Same(innerContext.StackState, context.StackState);
            Assert.Same(innerContext.Services, context.Services);
            Assert.Same(innerContext.StreamingResponse, context.StreamingResponse);
            Assert.False(context.Responded);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldSetRespondedAsTrue()
        {
            var context = new TestTurnContextWrapper(new TurnContext(new SimpleAdapter(), new Activity()));
            Assert.False(context.Responded);

            var response = await context.SendActivityAsync(TestMessage.Message("testtest"));

            Assert.True(context.Responded);
            Assert.Equal("testtest", response.Id);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldCallOnSendBeforeDelivery()
        {
            var context = new TestTurnContextWrapper(new TurnContext(new SimpleAdapter(), new Activity()));

            var count = 0;
            var returnedContext = context.OnSendActivities(async (innerContext, activities, next) =>
            {
                Assert.NotNull(activities);
                count = activities.Count;
                return await next();
            });

            await context.SendActivityAsync(TestMessage.Message());

            Assert.Same(context, returnedContext);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldInterceptAndMutateOnSend()
        {
            var foundIt = false;

            void ValidateResponses(IActivity[] activities)
            {
                Assert.NotNull(activities);
                Assert.Single(activities);
                Assert.Equal("changed", activities[0].Id);
                foundIt = true;
            }

            var adapter = new SimpleAdapter(ValidateResponses);
            var context = new TestTurnContextWrapper(new TurnContext(adapter, new Activity()));

            context.OnSendActivities(async (innerContext, activities, next) =>
            {
                Assert.NotNull(activities);
                Assert.Single(activities);
                Assert.Equal("1234", activities[0].Id);
                activities[0].Id = "changed";
                return await next();
            });

            await context.SendActivityAsync(TestMessage.Message());

            Assert.True(foundIt);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldCallOnUpdateBeforeDelivery()
        {
            var foundActivity = false;

            void ValidateUpdate(IActivity activity)
            {
                Assert.NotNull(activity);
                Assert.Equal("1234", activity.Id);
                foundActivity = true;
            }

            var adapter = new SimpleAdapter(ValidateUpdate);
            var context = new TestTurnContextWrapper(new TurnContext(adapter, new Activity()));

            var wasCalled = false;
            var returnedContext = context.OnUpdateActivity(async (innerContext, activity, next) =>
            {
                Assert.NotNull(activity);
                wasCalled = true;
                return await next();
            });

            await context.UpdateActivityAsync(TestMessage.Message());

            Assert.Same(context, returnedContext);
            Assert.True(wasCalled);
            Assert.True(foundActivity);
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldInterceptAndMutateOnDelete()
        {
            var adapterCalled = false;

            void ValidateDelete(ConversationReference reference)
            {
                Assert.Equal("mutated", reference.ActivityId);
                adapterCalled = true;
            }

            var adapter = new SimpleAdapter(ValidateDelete);
            var context = new TestTurnContextWrapper(new TurnContext(adapter, new Activity()));

            var returnedContext = context.OnDeleteActivity(async (innerContext, conversationReference, next) =>
            {
                Assert.NotNull(conversationReference);
                Assert.Equal("1234", conversationReference.ActivityId);
                conversationReference.ActivityId = "mutated";
                await next();
            });

            await context.DeleteActivityAsync("1234");

            Assert.Same(context, returnedContext);
            Assert.True(adapterCalled);
        }

        [Fact]
        public async Task TraceActivityAsync_ShouldNotUpdateContextResponded()
        {
            var context = new TestTurnContextWrapper(new TurnContext(new SimpleAdapter(), TestMessage.Message()));

            await context.TraceActivityAsync("Message with trace");

            Assert.False(context.Responded);
        }

        [Fact]
        public async Task SendActivityAsync_ShouldThrowAfterInnerContextDispose()
        {
            var innerContext = new TurnContext(new SimpleAdapter(), new Activity());
            var context = new TestTurnContextWrapper(innerContext);
            innerContext.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => context.SendActivityAsync("hello"));
        }

        private sealed class TestTurnContextWrapper : TurnContextWrapper
        {
            public TestTurnContextWrapper(ITurnContext turnContext)
                : base(turnContext)
            {
            }
        }
    }
}
