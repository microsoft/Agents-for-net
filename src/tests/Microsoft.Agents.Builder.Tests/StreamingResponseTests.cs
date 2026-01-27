// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseTests
    {
        [Theory]
        [InlineData(Channels.Webchat, DeliveryModes.Normal, 600)]
        [InlineData(Channels.Msteams, DeliveryModes.Normal, 1200)]
        [InlineData(Channels.Test, DeliveryModes.Stream, 150)]
        public async Task TestStreamingResponseSuccess(string channelId, string deliveryMode, int delay)
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, DeliveryMode = deliveryMode, ChannelId = channelId });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(delay);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.True(responses.Count > 1);

            AssertTyping(responses.First());
            AssertFinal(responses.Last(), "this is a test");
        }

        [Theory]
        [InlineData(Channels.Test, DeliveryModes.Normal)]
        [InlineData(Channels.Webchat, DeliveryModes.ExpectReplies)]
        [InlineData(Channels.Msteams, DeliveryModes.ExpectReplies)]
        public async Task TestNonStreamingResponseSuccess(string channelId, string deliveryMode)
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, DeliveryMode = deliveryMode, ChannelId = channelId });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            // We expect a single Activity, non-Streaming, with the complete Text value
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Single(responses);
            Assert.Equal("this is a test", responses[0].Text);
            Assert.Null(responses[0].GetStreamingEntity());
        }

        [Fact]
        public async Task TestStreamingResponseUserCancel()
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }

                    if (responses.Count == 2)
                    {
                        throw new ErrorResponseException("user cancelled")
                        {
                            Body = new ErrorResponse(new Error()
                            {
                                Code = "ContentStreamNotAllowed"
                            })
                        };
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.UserCancelled, result);
            Assert.Equal(2, responses.Count);
            AssertTyping(responses[0]);
            AssertTyping(responses[1]);
        }

        [Fact]
        public async Task TestStreamingResponseStreamingFallback()
        {
            var responses = new List<IActivity>();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((context, activities, ct) =>
                {
                    var entity = activities[0].GetStreamingEntity();
                    if (entity == null)
                    {
                        responses.Add(activities[0]);
                        return;
                    }

                    throw new ErrorResponseException("fallback")
                    {
                        Body = new ErrorResponse(new Error()
                        {
                            Code = "BadArgument",
                            Message = "streaming api is not enabled"
                        })
                    };
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });

            context.StreamingResponse.QueueTextChunk("this");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await Task.Delay(600);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            // We expect a single Activity, non-Streaming, with the complete Text value
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Single(responses);
            Assert.Equal("this is a test", responses[0].Text);
            Assert.Null(responses[0].GetStreamingEntity());
        }

        [Fact]
        public async Task TestStreamingResponseEndStreamTwice()
        {
            // streaming
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            var result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            // call end again
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);

            // non-streaming
            context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Channels.Test });
            context.StreamingResponse.QueueTextChunk("chunk");
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            // call end again
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);
        }

        private static void AssertTyping(IActivity activity)
        {
            var streamingEntity = activity.GetStreamingEntity();
            Assert.NotNull(streamingEntity);
            Assert.Equal(StreamTypes.Streaming, streamingEntity.StreamType);
        }

        private static void AssertFinal(IActivity activity, string expectedText)
        {
            var streamingEntity = activity.GetStreamingEntity();
            Assert.NotNull(streamingEntity);
            Assert.Equal(StreamResults.Success, streamingEntity.StreamResult);
            Assert.Equal(expectedText, activity.Text);
        }
    }
}
