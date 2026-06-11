// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TeamsFeedbackRouteBuilderTests
    {
        [Fact]
        public void Create_ReturnsNewInstance()
        {
            var builder = TeamsFeedbackRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsFeedbackRouteBuilder>(builder);
        }

        [Fact]
        public async Task Build_SetsTeamsChannelIdAndInvokeFlag()
        {
            var route = TeamsFeedbackRouteBuilder.Create()
                .WithHandler((turnContext, turnState, feedbackData, cancellationToken) => Task.CompletedTask, null)
                .Build();

            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                ChannelId = Channels.Msteams,
                Value = new JsonObject
                {
                    ["actionName"] = "feedback",
                },
            });

            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task Build_DoesNotMatchNonTeamsChannel()
        {
            var route = TeamsFeedbackRouteBuilder.Create()
                .WithHandler((turnContext, turnState, feedbackData, cancellationToken) => Task.CompletedTask, null)
                .Build();

            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                ChannelId = Channels.Directline,
                Value = new JsonObject
                {
                    ["actionName"] = "feedback",
                },
            });

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public void WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsFeedbackRouteBuilder.Create().WithHandler(null, null));
        }

        [Fact]
        public async Task WithHandler_WrapsTurnContextAndSendsInvokeResponse()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "message/submitAction",
                ChannelId = Channels.Msteams,
                ReplyToId = "reply-to-id",
                Value = new JsonObject
                {
                    ["actionName"] = "feedback",
                    ["actionValue"] = new JsonObject
                    {
                        ["reaction"] = "like",
                        ["feedback"] = "Great response!",
                    },
                },
            };
            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(activity);

            IActivity sentActivity = null;
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((invokeResponse, cancellationToken) => sentActivity = invokeResponse)
                .ReturnsAsync(new ResourceResponse());

            TeamsTurnContext capturedContext = null;
            FeedbackData capturedFeedbackData = null;

            var route = TeamsFeedbackRouteBuilder.Create()
                .WithHandler((turnContext, turnState, feedbackData, cancellationToken) =>
                {
                    capturedContext = turnContext;
                    capturedFeedbackData = feedbackData;
                    return Task.CompletedTask;
                }, null)
                .Build();

            await route.Handler(mockContext.Object, Mock.Of<ITurnState>(), CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.IsType<TeamsTurnContext>(capturedContext);
            Assert.Same(activity, capturedContext.Activity);
            Assert.NotNull(capturedFeedbackData);
            Assert.Equal("feedback", capturedFeedbackData.ActionName);
            Assert.NotNull(capturedFeedbackData.ActionValue);
            Assert.Equal("like", capturedFeedbackData.ActionValue.Reaction);
            Assert.Equal("Great response!", capturedFeedbackData.ActionValue.Feedback);
            Assert.Equal("reply-to-id", capturedFeedbackData.ReplyToId);
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivity.Type);
        }
    }
}
