// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public sealed class OptionalStreamingTimeoutFactAttribute : FactAttribute
    {
        public OptionalStreamingTimeoutFactAttribute()
        {
            if (!string.Equals(
                Environment.GetEnvironmentVariable("XUNITSTREAMINGTIMEOUTTESTENABLED"),
                "1",
                StringComparison.Ordinal))
            {
                Skip = "Set XUNITSTREAMINGTIMEOUTTESTENABLED=1 to run streaming timeout tests.";
            }
        }
    }

    public class StreamingResponseTests
    {
        [Theory]
        [InlineData(Microsoft.Agents.Core.Models.Channels.Webchat, DeliveryModes.Normal)]
        [InlineData(Microsoft.Agents.Core.Models.Channels.Msteams, DeliveryModes.Normal)]
        [InlineData(Microsoft.Agents.Core.Models.Channels.Test, DeliveryModes.Stream)]
        public async Task TestStreamingResponseSuccess(string channelId, string deliveryMode)
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
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("this");
            await WaitForResponses(responses, 1);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await WaitForResponses(responses, 2);
            context.StreamingResponse.QueueTextChunk("test");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.True(responses.Count > 1);

            AssertTyping(responses.First());
            AssertFinal(responses.Last(), "this is a test");
        }

        [Theory]
        [InlineData(Microsoft.Agents.Core.Models.Channels.Test, DeliveryModes.Normal)]
        [InlineData(Microsoft.Agents.Core.Models.Channels.Webchat, DeliveryModes.ExpectReplies)]
        [InlineData(Microsoft.Agents.Core.Models.Channels.Msteams, DeliveryModes.ExpectReplies)]
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
            context.StreamingResponse.QueueTextChunk(" is a ");
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

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("this");
            await WaitForResponses(responses, 1);
            context.StreamingResponse.QueueTextChunk(" is a ");
            await WaitForResponses(responses, 2);
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

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("this");
            context.StreamingResponse.QueueTextChunk(" is a ");
            context.StreamingResponse.QueueTextChunk("test");
            // Wait for the BadArgument fallback to set IsStreamingChannel=false.
            // EndStreamAsync must enter via the non-streaming path to avoid a null-deref
            // when it sends the final message (which has no StreamInfo entity).
            await WaitForAsync(() => !context.StreamingResponse.IsStreamingChannel);

            var result = await context.StreamingResponse.EndStreamAsync();

            // We expect a single Activity, non-Streaming, with the complete Text value
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Single(responses);
            Assert.Equal("this is a test", responses[0].Text);
            Assert.Null(responses[0].GetStreamingEntity());
        }

        [OptionalStreamingTimeoutFact]
        public async Task SendStreamTimedOutNotification_DisablesStreaming_AndFinalResponseIsStillSent()
        {
            const string informativeMessage = "Thinking...";
            const string timeoutNotification = "Streaming timed out. Sending the completed response instead.";
            const string completedText = "Completed response text.";

            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10_000;
            context.StreamingResponse.InitialDelay = 10;

            await context.StreamingResponse.QueueInformativeUpdateAsync(informativeMessage);

            var timedOut = await context.StreamingResponse.SendStreamTimedOutNotification(timeoutNotification);

            Assert.True(timedOut);
            Assert.False(context.StreamingResponse.IsStreamingChannel);
            Assert.Collection(
                responses,
                informativeActivity =>
                {
                    Assert.Equal(informativeMessage, informativeActivity.Text);
                    Assert.Equal(StreamTypes.Informative, informativeActivity.GetStreamingEntity()?.StreamType);
                },
                timeoutActivity =>
                {
                    Assert.Equal(ActivityTypes.Message, timeoutActivity.Type);
                    Assert.Equal(timeoutNotification, timeoutActivity.Text);
                    var timeoutStreamInfo = timeoutActivity.GetStreamingEntity();
                    Assert.NotNull(timeoutStreamInfo);
                    Assert.Equal(StreamTypes.Final, timeoutStreamInfo.StreamType);
                });

            context.StreamingResponse.QueueTextChunk(completedText);
            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Collection(
                responses,
                _ => { },
                _ => { },
                finalActivity =>
                {
                    Assert.Equal(ActivityTypes.Message, finalActivity.Type);
                    Assert.Equal(completedText, finalActivity.Text);
                    Assert.Null(finalActivity.GetStreamingEntity());
                });
        }

        [OptionalStreamingTimeoutFact]
        public async Task ChannelStreamingTimeout_UpdatesCheckpointAndFinalMessage()
        {
            const string completedText = "Completed response text.";

            var sendAttempts = 0;
            var updates = new List<IActivity>();
            var timeoutCheckpointSeen = new TaskCompletionSource<IActivity>(TaskCreationOptions.RunContinuationsAsynchronously);

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Returns<ITurnContext, IActivity[], CancellationToken>((_, _, _) =>
                {
                    Interlocked.Increment(ref sendAttempts);
                    return Task.FromException<ResourceResponse[]>(new ErrorResponseException("stream timeout")
                    {
                        Body = new ErrorResponse(new Error()
                        {
                            Code = "ContentStreamNotAllowed",
                            Message = "Content stream finished due to exceeded streaming time."
                        })
                    });
                });

            adapter
                .Setup(a => a.UpdateActivityAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Returns<ITurnContext, IActivity, CancellationToken>((_, activity, _) =>
                {
                    lock (updates)
                    {
                        updates.Add(activity);
                        if (updates.Count == 1)
                        {
                            timeoutCheckpointSeen.TrySetResult(activity);
                        }
                    }

                    return Task.FromResult(new ResourceResponse(activity.Id));
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk(completedText);

            var checkpointActivity = await WaitForTaskAsync(
                timeoutCheckpointSeen.Task,
                "Expected the timeout checkpoint update to be sent after the streaming send failed.");

            Assert.False(context.StreamingResponse.IsStreamingChannel);
            Assert.Contains(context.StreamingResponse.StreamingTakingTooLongMessage, checkpointActivity.Text);

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Equal(1, sendAttempts);

            IActivity[] updateSnapshot;
            lock (updates)
            {
                updateSnapshot = [.. updates];
            }

            Assert.Collection(
                updateSnapshot,
                timeoutUpdate => Assert.Contains(context.StreamingResponse.StreamingTakingTooLongMessage, timeoutUpdate.Text),
                finalUpdate => Assert.Equal(completedText, finalUpdate.Text));
        }

        [Fact]
        public async Task TestStreamingResponseEndStreamTwice()
        {
            // streaming
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            var result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            // call end again
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);

            // non-streaming
            context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });
            context.StreamingResponse.QueueTextChunk("chunk");
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            // call end again
            result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);
        }

        [Fact]
        public async Task QueueInformativeUpdate_OnStreamingChannel_SendsInformativeActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            context.StreamingResponse.QueueTextChunk("done");

            await context.StreamingResponse.EndStreamAsync();

            var informativeActivity = responses.FirstOrDefault(a => a.GetStreamingEntity()?.StreamType == StreamTypes.Informative);
            Assert.NotNull(informativeActivity);
            Assert.Equal("Thinking...", informativeActivity.Text);
            Assert.Equal(ActivityTypes.Typing, informativeActivity.Type);
        }

        [OptionalStreamingTimeoutFact]
        public async Task M365Copilot_IdleStream_SendsConfiguredWorkingNotice()
        {
            const string startingNotice = "Starting...";
            const string customWorkingNotice = "Still working on your response...";

            var responses = new List<IActivity>();
            var responseLock = new object();
            var workingNoticeSeen = new TaskCompletionSource<IActivity>(TaskCreationOptions.RunContinuationsAsynchronously);

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((_, activities, _) =>
                {
                    foreach (var activity in activities)
                    {
                        lock (responseLock)
                        {
                            responses.Add(activity);
                        }

                        if (activity.GetStreamingEntity()?.StreamType == StreamTypes.Informative
                            && activity.Text == customWorkingNotice)
                        {
                            workingNoticeSeen.TrySetResult(activity);
                        }
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.M365Copilot,
                DeliveryMode = DeliveryModes.Stream
            });
            context.StreamingResponse.Interval = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
            context.StreamingResponse.StreamingTakingTooLongMessage = customWorkingNotice;

            await context.StreamingResponse.QueueInformativeUpdateAsync(startingNotice);
            Assert.True(context.StreamingResponse.IsStreamStarted());

            SetPrivateField(context.StreamingResponse, "_lastInformationalMessageSent", string.Empty);
            SetPrivateField<DateTime?>(context.StreamingResponse, "_lastPassTime", DateTime.UtcNow - TimeSpan.FromSeconds(40));
            InvokePrivateMethod(context.StreamingResponse, "SendIntermediateMessage", new object[] { null });

            var workingNotice = await WaitForTaskAsync(
                workingNoticeSeen.Task,
                "Expected the BizChat keep-alive callback to send the configured working notice.");

            Assert.Equal(ActivityTypes.Typing, workingNotice.Type);
            Assert.Equal(StreamTypes.Informative, workingNotice.GetStreamingEntity()?.StreamType);
            Assert.Equal(customWorkingNotice, workingNotice.Text);

            IActivity[] responseSnapshot;
            lock (responseLock)
            {
                responseSnapshot = [.. responses];
            }

            Assert.Collection(
                responseSnapshot.Take(2),
                firstActivity => Assert.Equal(startingNotice, firstActivity.Text),
                secondActivity => Assert.Equal(customWorkingNotice, secondActivity.Text));

            await WaitForConditionAsync(
                () => !GetPrivateField<bool>(context.StreamingResponse, "_processingTimer"),
                "Expected the keep-alive timer callback to finish before ending the stream.");

            var endStreamTask = Task.Run(() => context.StreamingResponse.EndStreamAsync());
            InvokePrivateMethod(context.StreamingResponse, "SendIntermediateMessage", new object[] { null });

            var result = await WaitForTaskAsync(
                endStreamTask,
                "Expected EndStreamAsync to finish after draining the idle BizChat stream.");

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.False(context.StreamingResponse.IsStreamStarted());
        }

        [OptionalStreamingTimeoutFact]
        public async Task M365Copilot_TimeoutWithoutText_SendsFinalTimeoutMessage()
        {
            const string startingNotice = "Starting...";
            const string customTimeoutMessage = "Custom timeout message.";

            var responses = new List<IActivity>();
            var responseLock = new object();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((_, activities, _) =>
                {
                    lock (responseLock)
                    {
                        foreach (var activity in activities)
                        {
                            responses.Add(activity);
                        }
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.M365Copilot,
                DeliveryMode = DeliveryModes.Stream
            });
            context.StreamingResponse.Interval = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
            context.StreamingResponse.InitialDelay = context.StreamingResponse.Interval;
            context.StreamingResponse.StreamingTakingTooLongMessage = customTimeoutMessage;

            await context.StreamingResponse.QueueInformativeUpdateAsync(startingNotice);
            Assert.True(context.StreamingResponse.IsStreamStarted());

            SetPrivateField(context.StreamingResponse, "M365StreamingTimeout", TimeSpan.Zero);
            InvokePrivateMethod(context.StreamingResponse, "SendIntermediateMessage", new object[] { null });

            await WaitForConditionAsync(
                () => !context.StreamingResponse.IsStreamingChannel,
                "Expected the BizChat timeout callback to disable streaming when no text was buffered.");
            await WaitForConditionAsync(
                () => !GetPrivateField<bool>(context.StreamingResponse, "_processingTimer"),
                "Expected the BizChat timeout callback to finish before ending the stream.");

            var endResult = await WaitForTaskAsync(
                context.StreamingResponse.EndStreamAsync(),
                "Expected EndStreamAsync to finish after BizChat timed out without buffered text.");

            Assert.Equal(StreamingResponseResult.Success, endResult);

            IActivity[] responseSnapshot;
            lock (responseLock)
            {
                responseSnapshot = [.. responses];
            }

            Assert.True(responseSnapshot.Length >= 2, "Expected the BizChat timeout test to capture at least the starting notice and timeout activity.");
            Assert.Equal(startingNotice, responseSnapshot[0].Text);
            Assert.Equal(StreamTypes.Informative, responseSnapshot[0].GetStreamingEntity()?.StreamType);

            var timeoutActivities = responseSnapshot
                .Where(activity => activity.Text == customTimeoutMessage)
                .ToArray();

            Assert.Single(timeoutActivities);
            Assert.Equal(ActivityTypes.Message, timeoutActivities[0].Type);

            var streamInfo = timeoutActivities[0].GetStreamingEntity();
            Assert.NotNull(streamInfo);
            Assert.Equal(StreamTypes.Final, streamInfo.StreamType);
            Assert.Equal(StreamResults.Error, streamInfo.StreamResult);
        }

        [OptionalStreamingTimeoutFact]
        public async Task M365Copilot_TimeoutWithBufferedText_SendsTerminatingActivitiesAndFinalResponse()
        {
            const string startingNotice = "Starting...";
            const string bufferedResult = "Buffered result";
            const string customTimeoutMessage = "Custom timeout message.";

            var responses = new List<IActivity>();
            var responseLock = new object();

            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((_, activities, _) =>
                {
                    lock (responseLock)
                    {
                        foreach (var activity in activities)
                        {
                            responses.Add(activity);
                        }
                    }
                });

            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.M365Copilot,
                DeliveryMode = DeliveryModes.Stream
            });
            context.StreamingResponse.Interval = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
            context.StreamingResponse.InitialDelay = context.StreamingResponse.Interval;
            context.StreamingResponse.StreamingTakingTooLongMessage = customTimeoutMessage;

            await context.StreamingResponse.QueueInformativeUpdateAsync(startingNotice);
            context.StreamingResponse.QueueTextChunk(bufferedResult);
            Assert.True(context.StreamingResponse.IsStreamStarted());

            SetPrivateField(context.StreamingResponse, "M365StreamingTimeout", TimeSpan.Zero);
            InvokePrivateMethod(context.StreamingResponse, "SendIntermediateMessage", new object[] { null });

            await WaitForConditionAsync(
                () => !context.StreamingResponse.IsStreamingChannel,
                "Expected the BizChat timeout callback to disable streaming when text was buffered.");
            await WaitForConditionAsync(
                () => !GetPrivateField<bool>(context.StreamingResponse, "_processingTimer"),
                "Expected the BizChat timeout callback to finish before ending the stream.");

            IActivity[] timeoutSnapshot;
            lock (responseLock)
            {
                timeoutSnapshot = [.. responses];
            }

            Assert.Collection(
                timeoutSnapshot,
                startActivity =>
                {
                    Assert.Equal(startingNotice, startActivity.Text);
                    Assert.Equal(StreamTypes.Informative, startActivity.GetStreamingEntity()?.StreamType);
                },
                timeoutTypingActivity =>
                {
                    Assert.Equal(ActivityTypes.Typing, timeoutTypingActivity.Type);
                    Assert.Contains(customTimeoutMessage, timeoutTypingActivity.Text);
                    var streamInfo = timeoutTypingActivity.GetStreamingEntity();
                    Assert.NotNull(streamInfo);
                    Assert.Equal(StreamTypes.Streaming, streamInfo.StreamType);
                    Assert.Null(streamInfo.StreamResult);
                },
                timeoutFinalActivity =>
                {
                    Assert.Equal(ActivityTypes.Message, timeoutFinalActivity.Type);
                    Assert.Contains(customTimeoutMessage, timeoutFinalActivity.Text);
                    var streamInfo = timeoutFinalActivity.GetStreamingEntity();
                    Assert.NotNull(streamInfo);
                    Assert.Equal(StreamTypes.Final, streamInfo.StreamType);
                    Assert.Equal(StreamResults.Success, streamInfo.StreamResult);
                });

            var result = await WaitForTaskAsync(
                context.StreamingResponse.EndStreamAsync(),
                "Expected EndStreamAsync to finish after BizChat switched to non-streaming delivery.");

            Assert.Equal(StreamingResponseResult.Success, result);

            IActivity[] responseSnapshot;
            lock (responseLock)
            {
                responseSnapshot = [.. responses];
            }

            var finalActivity = responseSnapshot.Last();
            Assert.Equal(ActivityTypes.Message, finalActivity.Type);
            Assert.Equal(bufferedResult, finalActivity.Text);
            Assert.Null(finalActivity.GetStreamingEntity());
        }

        [Fact]
        public async Task QueueInformativeUpdate_OnStreamingChannel_SendsMultipleInformativeActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            // "Thinking..." is sent synchronously above; no wait needed.
            context.StreamingResponse.QueueTextChunk("this");
            await WaitForResponses(responses, 2); // wait for "this" chunk to be flushed
            context.StreamingResponse.QueueTextChunk(" is a ");
            await context.StreamingResponse.QueueInformativeUpdateAsync("Still thinking...");
            await WaitForResponses(responses, 3); // wait for "Still thinking..." informative
            context.StreamingResponse.QueueTextChunk("longer ");
            await WaitForResponses(responses, 4); // wait for chunk containing "longer"
            context.StreamingResponse.QueueTextChunk("test");

            await context.StreamingResponse.EndStreamAsync();

            // Structural assertions: order and presence matter, exact count is timing-dependent
            Assert.True(responses.Count >= 4); // at least 2 informative + 1 streaming + 1 final

            Assert.Contains(responses, a =>
                a.GetStreamingEntity()?.StreamType == StreamTypes.Informative && a.Text == "Thinking...");
            Assert.Contains(responses, a =>
                a.GetStreamingEntity()?.StreamType == StreamTypes.Informative && a.Text == "Still thinking...");
            Assert.Contains(responses, a =>
                a.GetStreamingEntity()?.StreamType == StreamTypes.Streaming);

            var finalActivity = responses.Last();
            Assert.Equal(StreamTypes.Final, finalActivity.GetStreamingEntity()?.StreamType);
            Assert.Equal("this is a longer test", finalActivity.Text);
            Assert.Equal(ActivityTypes.Message, finalActivity.Type);
        }

        [Fact]
        public async Task QueueInformativeUpdate_OnNonStreamingChannel_DoesNotSendActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            context.StreamingResponse.QueueTextChunk("hello");

            await context.StreamingResponse.EndStreamAsync();

            // Non-streaming: no informative activities, only the final message
            Assert.Single(responses);
            Assert.Equal(ActivityTypes.Message, responses[0].Type);
        }

        [Fact]
        public async Task QueueInformativeUpdate_AfterStreamEnded_ThrowsInvalidOperationException()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            await context.StreamingResponse.EndStreamAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.StreamingResponse.QueueInformativeUpdateAsync("too late"));
        }

        [Fact]
        public async Task QueueTextChunk_AfterStreamEnded_ThrowsInvalidOperationException()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.QueueTextChunk("chunk");
            await context.StreamingResponse.EndStreamAsync();

            Assert.Throws<InvalidOperationException>(() =>
                context.StreamingResponse.QueueTextChunk("too late"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void QueueTextChunk_WithEmptyOrNullText_SilentlyIgnored(string text)
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });

            // Should not throw
            context.StreamingResponse.QueueTextChunk(text);
            Assert.Equal("", context.StreamingResponse.Message);
        }

        [Fact]
        public void AddCitation_WithCitationAndPosition_AddsCitationAtPosition()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            var citation = new Citation("Some content", "My Doc", "https://example.com");

            context.StreamingResponse.AddCitation(citation, 1);

            Assert.Single(context.StreamingResponse.Citations);
            Assert.Equal(1, context.StreamingResponse.Citations[0].Position);
            Assert.Equal("My Doc", context.StreamingResponse.Citations[0].Appearance.Name);
            Assert.Equal("https://example.com", context.StreamingResponse.Citations[0].Appearance.Url);
        }

        [Fact]
        public void AddCitations_WithCitationList_AddsAllCitationsInOrder()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            var citations = new List<Citation>
            {
                new("Content 1", "Doc 1", "https://example.com/1"),
                new("Content 2", "Doc 2", "https://example.com/2"),
            };

            context.StreamingResponse.AddCitations(citations);

            Assert.Equal(2, context.StreamingResponse.Citations.Count);
            Assert.Equal(1, context.StreamingResponse.Citations[0].Position);
            Assert.Equal(2, context.StreamingResponse.Citations[1].Position);
        }

        [Fact]
        public void AddCitation_WithClientCitation_AddedToList()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            var citation = new ClientCitation { Position = 5, Appearance = new ClientCitationAppearance { Name = "Test" } };

            context.StreamingResponse.AddCitation(citation);

            Assert.Single(context.StreamingResponse.Citations);
            Assert.Equal(5, context.StreamingResponse.Citations[0].Position);
        }

        [Fact]
        public void AddCitations_WithClientCitationList_AddsAll()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            var citations = new List<ClientCitation>
            {
                new() { Position = 1 },
                new() { Position = 2 },
                new() { Position = 3 },
            };

            context.StreamingResponse.AddCitations(citations);

            Assert.Equal(3, context.StreamingResponse.Citations.Count);
        }

        [Fact]
        public async Task EnableGeneratedByAILabel_True_AddsAIGeneratedContentToFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });
            context.StreamingResponse.EnableGeneratedByAILabel = true;

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var aiEntity = finalActivity.Entities?.OfType<AIEntity>().FirstOrDefault();
            Assert.NotNull(aiEntity);
            Assert.Contains(AIEntity.AdditionalTypeAIGeneratedContent, aiEntity.AdditionalType);
        }

        [Fact]
        public async Task Citations_ReferencedInText_IncludedInFinalMessageAIEntity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });
            context.StreamingResponse.AddCitation(new ClientCitation { Position = 1, Appearance = new ClientCitationAppearance { Name = "Ref Doc" } });

            context.StreamingResponse.QueueTextChunk("See [1] for details.");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var aiEntity = finalActivity.Entities?.OfType<AIEntity>().FirstOrDefault();
            Assert.NotNull(aiEntity);
            Assert.NotNull(aiEntity.Citation);
            Assert.Contains(aiEntity.Citation, c => c.Position == 1);
        }

        [Fact]
        public async Task FinalMessage_CustomActivity_IsUsedAsBase()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            var customFinal = new Activity { Text = "Custom final text" };
            context.StreamingResponse.FinalMessage = customFinal;
            context.StreamingResponse.QueueTextChunk("ignored text");

            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.Equal("Custom final text", finalActivity.Text);
        }

        [Fact]
        public async Task AddAttachment_IncludedInFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });
            var attachment = new Attachment { ContentType = "text/plain", Name = "attachment.txt", Content = "hello" };

            context.StreamingResponse.AddAttachment(attachment);
            context.StreamingResponse.QueueTextChunk("with attachment");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.NotNull(finalActivity.Attachments);
            Assert.Single(finalActivity.Attachments);
            Assert.Same(attachment, finalActivity.Attachments[0]);
        }

        [Fact]
        public async Task SensitivityLabel_SetOnResponse_IncludedInFinalMessageAIEntity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });
            context.StreamingResponse.EnableGeneratedByAILabel = true;
            context.StreamingResponse.SensitivityLabel = new SensitivityUsageInfo { Name = "Confidential" };

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var aiEntity = finalActivity.Entities?.OfType<AIEntity>().FirstOrDefault();
            Assert.NotNull(aiEntity);
            Assert.NotNull(aiEntity.UsageInfo);
            Assert.Equal("Confidential", aiEntity.UsageInfo.Name);
        }

        [Fact]
        public async Task EndStreamAsync_OnStreamingChannel_WithNothingQueued_ReturnsNotStarted()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.NotStarted, result);
        }

        [Fact]
        public async Task EndStreamAsync_OnNonStreamingChannel_WithNothingQueued_SendsNoActivity()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Empty(responses);
        }


        [Fact]
        public async Task ResetAsync_AfterStream_AllowsStreamToBeReused()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            context.StreamingResponse.QueueTextChunk("first");
            await context.StreamingResponse.EndStreamAsync();

            await context.StreamingResponse.ResetAsync();

            context.StreamingResponse.QueueTextChunk("second");
            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Equal(2, responses.Count);
            Assert.Equal("first", responses[0].Text);
            Assert.Equal("second", responses[1].Text);
        }

        [Fact]
        public void QueueTextChunk_AccumulatesMessageText()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            context.StreamingResponse.QueueTextChunk("Hello");
            context.StreamingResponse.QueueTextChunk(", ");
            context.StreamingResponse.QueueTextChunk("World");

            Assert.Equal("Hello, World", context.StreamingResponse.Message);
        }

        [Fact]
        public async Task FeedbackLoopEnabled_OnTeamsChannel_AddsFeedbackLoopToFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            // ExpectReplies keeps _isTeamsChannel=true but IsStreamingChannel=false (no timing delays needed)
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.NotNull(finalActivity.ChannelData);
            var json = JsonSerializer.Serialize(finalActivity.ChannelData);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("default", doc.RootElement.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FeedbackLoopEnabled_WithCustomType_SetsFeedbackLoopTypeOnFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;
            context.StreamingResponse.FeedbackLoopType = "custom";

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var json = JsonSerializer.Serialize(finalActivity.ChannelData);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("custom", doc.RootElement.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FeedbackLoopEnabled_WithExistingChannelData_PreservesExistingProperties()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                DeliveryMode = DeliveryModes.ExpectReplies,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;
            context.StreamingResponse.FinalMessage = new Activity
            {
                ChannelData = new { existingKey = "existingValue" }
            };

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.NotNull(finalActivity.ChannelData);
            var json = JsonSerializer.Serialize(finalActivity.ChannelData);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal("existingValue", doc.RootElement.GetProperty("existingKey").GetString());
            Assert.Equal("default", doc.RootElement.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public async Task FeedbackLoopEnabled_OnNonTeamsChannel_DoesNotAddFeedbackLoop()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity()
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Test
            });
            context.StreamingResponse.FeedbackLoopEnabled = true;

            context.StreamingResponse.QueueTextChunk("hello");
            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            Assert.Null(finalActivity.ChannelData);
        }

        // -------------------------------------------------------------------
        // New tests for previously uncovered behaviors
        // -------------------------------------------------------------------

        [Fact]
        public async Task UpdatesSent_ReturnsCorrectCount()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            // First informative is sent directly (synchronous), so UpdatesSent increments immediately.
            await context.StreamingResponse.QueueInformativeUpdateAsync("update 1");
            Assert.Equal(1, context.StreamingResponse.UpdatesSent());

            // Queue another informative and a text chunk; they are processed by the timer.
            await context.StreamingResponse.QueueInformativeUpdateAsync("update 2");
            context.StreamingResponse.QueueTextChunk("hello");

            await context.StreamingResponse.EndStreamAsync();

            // At minimum: informative1 (already counted) + informative2 + one chunk
            Assert.True(context.StreamingResponse.UpdatesSent() >= 3);
        }

        [Fact]
        public async Task StreamingMessages_HaveIncreasingSequenceNumbers()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            await context.StreamingResponse.QueueInformativeUpdateAsync("thinking");
            context.StreamingResponse.QueueTextChunk("chunk 1");
            await WaitForResponses(responses, 2); // wait for "chunk 1" to be flushed
            context.StreamingResponse.QueueTextChunk(" chunk 2");

            await context.StreamingResponse.EndStreamAsync();

            var typingActivities = responses.Where(a => a.Type == ActivityTypes.Typing).ToList();
            Assert.True(typingActivities.Count >= 2);

            var sequences = typingActivities
                .Select(a => a.GetStreamingEntity()?.StreamSequence ?? 0)
                .ToList();

            for (int i = 1; i < sequences.Count; i++)
            {
                Assert.True(sequences[i] > sequences[i - 1],
                    $"Expected sequence[{i}]={sequences[i]} > sequence[{i - 1}]={sequences[i - 1]}");
            }
        }

        [Fact]
        public void Directline_Channel_IsStreamingChannel()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Directline });

            Assert.True(context.StreamingResponse.IsStreamingChannel);
            Assert.Equal(500, context.StreamingResponse.Interval);
        }

        [Fact]
        public async Task QueueTextChunk_WhenCanceled_IsSilentlyIgnored()
        {
            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Throws(new ErrorResponseException("user cancelled")
                {
                    Body = new ErrorResponse(new Error() { Code = "ContentStreamNotAllowed" })
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("trigger cancel");

            // EndStreamAsync blocks on _queueEmpty.Set(), which is called after _canceled is set.
            // This is the deterministic synchronization point — no fixed delay needed.
            var result = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.UserCancelled, result);

            // _canceled=true is now guaranteed. QueueTextChunk must return silently (not throw).
            context.StreamingResponse.QueueTextChunk("after cancel");
        }

        [Fact]
        public async Task SendActivity_NonErrorResponseException_ReturnsError()
        {
            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("unexpected error"));

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("trigger error");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Error, result);
        }

        [Fact]
        public async Task SendActivity_BadArgumentWrongMessage_CancelsStream()
        {
            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Throws(new ErrorResponseException("bad argument")
                {
                    Body = new ErrorResponse(new Error()
                    {
                        Code = "BadArgument",
                        Message = "some unrelated error"
                    })
                });

            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("trigger");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Error, result);
            // IsStreamingChannel must NOT be disabled — only the specific "streaming api is not enabled" message triggers that
            Assert.True(context.StreamingResponse.IsStreamingChannel);
        }

        [Fact]
        public async Task FinalMessage_WithExistingStreamInfo_GetsStripped()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            // FinalMessage carries a bogus StreamInfo with the wrong type
            context.StreamingResponse.FinalMessage = new Activity
            {
                Text = "Final answer",
                Entities = [new StreamInfo { StreamType = StreamTypes.Informative }]
            };
            context.StreamingResponse.QueueTextChunk("start stream");

            await context.StreamingResponse.EndStreamAsync();

            var finalActivity = responses.Last();
            var streamInfos = finalActivity.Entities.OfType<StreamInfo>().ToList();
            // Exactly one StreamInfo on the final activity, and it must be Final
            Assert.Single(streamInfos);
            Assert.Equal(StreamTypes.Final, streamInfos[0].StreamType);
        }

        [Fact]
        public async Task NonStreaming_WithOnlyFinalMessage_SendsFinalMessage()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            context.StreamingResponse.FinalMessage = new Activity { Text = "Only final" };

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.Single(responses);
            Assert.Equal("Only final", responses[0].Text);
            Assert.Equal(ActivityTypes.Message, responses[0].Type);
            Assert.Null(responses[0].GetStreamingEntity());
        }

        [Fact]
        public async Task Streaming_FinalMessage_SendsIt()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            // Start the stream via an informative update, then set only a FinalMessage (no text chunks).
            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");
            context.StreamingResponse.FinalMessage = new Activity { Text = "Final answer" };

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            var finalActivity = responses.Last();
            Assert.Equal("Final answer", finalActivity.Text);
            Assert.Equal(ActivityTypes.Message, finalActivity.Type);
            var streamInfo = finalActivity.GetStreamingEntity();
            Assert.NotNull(streamInfo);
            Assert.Equal(StreamTypes.Final, streamInfo.StreamType);
        }

        [Fact]
        public async Task EmptyMessage_NoFinalMessage_UsesPlaceholder()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            // Start the stream so UpdatesSent > 0, but queue no text and set no FinalMessage.
            await context.StreamingResponse.QueueInformativeUpdateAsync("Thinking...");

            var result = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result);
            var finalActivity = responses.Last();
            Assert.Equal("No text was streamed", finalActivity.Text);
            Assert.Equal(ActivityTypes.Message, finalActivity.Type);
        }

        [Fact]
        public async Task ResetAsync_OnStreamingChannel_AllowsReuse()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.QueueTextChunk("first message");
            var result1 = await context.StreamingResponse.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result1);

            await context.StreamingResponse.ResetAsync();

            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;
            context.StreamingResponse.QueueTextChunk("second message");
            var result2 = await context.StreamingResponse.EndStreamAsync();

            Assert.Equal(StreamingResponseResult.Success, result2);
            var lastFinal = responses.Last();
            Assert.Equal(ActivityTypes.Message, lastFinal.Type);
            Assert.Equal("second message", lastFinal.Text);
        }

        [Fact]
        public async Task ResetAsync_ClearsAllState()
        {
            var responses = new List<IActivity>();
            var adapter = CreateMockAdapter(responses);
            var context = new TurnContext(adapter.Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat });
            context.StreamingResponse.Interval = 10;
            context.StreamingResponse.InitialDelay = 10;

            context.StreamingResponse.FinalMessage = new Activity { Text = "custom" };
            context.StreamingResponse.EnableGeneratedByAILabel = true;
            context.StreamingResponse.SensitivityLabel = new SensitivityUsageInfo { Name = "Sensitive" };
            context.StreamingResponse.AddCitation(new ClientCitation { Position = 1 });
            context.StreamingResponse.AddAttachment(new Attachment { ContentType = "text/plain", Name = "attachment.txt", Content = "payload" });
            context.StreamingResponse.QueueTextChunk("some text");
            await context.StreamingResponse.EndStreamAsync();

            await context.StreamingResponse.ResetAsync();

            Assert.Equal("", context.StreamingResponse.Message);
            Assert.Null(context.StreamingResponse.FinalMessage);
            Assert.Equal(false, context.StreamingResponse.EnableGeneratedByAILabel);
            Assert.Null(context.StreamingResponse.SensitivityLabel);
            Assert.Empty(context.StreamingResponse.Citations);
            Assert.Null(context.StreamingResponse.StreamId);
            Assert.Equal(0, context.StreamingResponse.UpdatesSent());

            context.StreamingResponse.QueueTextChunk("after reset");
            await context.StreamingResponse.EndStreamAsync();
            var postResetFinal = responses.Last();
            Assert.True(postResetFinal.Attachments == null || postResetFinal.Attachments.Count == 0);
        }

        [Fact]
        public void AddCitations_Citation_WithPreExisting_ContinuesPositioning()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            context.StreamingResponse.AddCitations(new List<Citation>
            {
                new("Content 1", "Doc 1", "https://example.com/1"),
            });

            context.StreamingResponse.AddCitations(new List<Citation>
            {
                new("Content 2", "Doc 2", "https://example.com/2"),
                new("Content 3", "Doc 3", "https://example.com/3"),
            });

            Assert.Equal(3, context.StreamingResponse.Citations.Count);
            Assert.Equal(1, context.StreamingResponse.Citations[0].Position);
            Assert.Equal(2, context.StreamingResponse.Citations[1].Position);
            Assert.Equal(3, context.StreamingResponse.Citations[2].Position);
        }

        [Fact]
        public void AddCitations_EmptyList_IsNoOp()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            context.StreamingResponse.AddCitations(new List<Citation>());

            Assert.Empty(context.StreamingResponse.Citations);
        }

        [Fact]
        public void AddClientCitations_EmptyList_IsNoOp()
        {
            var context = new TurnContext(new Mock<IChannelAdapter>().Object, new Activity() { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Test });

            context.StreamingResponse.AddCitations(new List<ClientCitation>());

            Assert.Empty(context.StreamingResponse.Citations);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Polls until <paramref name="condition"/> returns true or the timeout elapses.
        /// Avoids fixed <see cref="Task.Delay"/> calls that are unreliable under CI load.
        /// </summary>
        private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(5);
        }

        private static Task WaitForResponses(List<IActivity> responses, int minCount, int timeoutMs = 5000)
            => WaitForAsync(() => responses.Count >= minCount, timeoutMs);

        private static async Task<T> WaitForTaskAsync<T>(Task<T> task, string failureMessage, int timeoutMs = 5000)
        {
            await WaitForAsync(() => task.IsCompleted, timeoutMs);
            Assert.True(task.IsCompleted, failureMessage);
            return await task;
        }

        private static async Task WaitForConditionAsync(Func<bool> condition, string failureMessage, int timeoutMs = 5000)
        {
            await WaitForAsync(condition, timeoutMs);
            Assert.True(condition(), failureMessage);
        }

        private static void SetPrivateField<T>(IStreamingResponse response, string fieldName, T value)
        {
            var field = response.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(response, value);
        }

        private static T GetPrivateField<T>(IStreamingResponse response, string fieldName)
        {
            var field = response.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (T)field.GetValue(response);
        }

        private static void InvokePrivateMethod(IStreamingResponse response, string methodName, params object[] arguments)
        {
            var method = response.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(response, arguments);
        }

        private static Mock<IChannelAdapter> CreateMockAdapter(List<IActivity> responses)
        {
            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity[], CancellationToken>((_, activities, _) =>
                {
                    foreach (var activity in activities)
                    {
                        responses.Add(activity);
                    }
                });
            return adapter;
        }

        private static Mock<IChannelAdapter> CreateMockAdapter(
            List<IActivity> sentActivities,
            List<IActivity> updatedActivities)
        {
            var adapter = CreateMockAdapter(sentActivities);
            adapter
                .Setup(a => a.UpdateActivityAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<ITurnContext, IActivity, CancellationToken>((_, activity, _) => updatedActivities.Add(activity))
                .ReturnsAsync((ITurnContext _, IActivity activity, CancellationToken _) => new ResourceResponse(activity.Id));
            return adapter;
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
