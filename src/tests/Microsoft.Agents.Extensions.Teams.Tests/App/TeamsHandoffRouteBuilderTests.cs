// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TeamsHandoffRouteBuilderTests
    {
        [Fact]
        public void Create_ReturnsNewInstance()
        {
            var builder = TeamsHandoffRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsHandoffRouteBuilder>(builder);
        }

        [Fact]
        public async Task Build_SetsTeamsChannelIdAndInvokeFlag()
        {
            var route = TeamsHandoffRouteBuilder.Create()
                .WithHandler((turnContext, turnState, continuation, cancellationToken) => Task.CompletedTask, null)
                .Build();

            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                ChannelId = Channels.Msteams,
            });

            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.True(route.Flags.HasFlag(RouteFlags.Invoke));
            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task Build_DoesNotMatchNonTeamsChannel()
        {
            var route = TeamsHandoffRouteBuilder.Create()
                .WithHandler((turnContext, turnState, continuation, cancellationToken) => Task.CompletedTask, null)
                .Build();

            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                ChannelId = Channels.Directline,
            });

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public void WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsHandoffRouteBuilder.Create().WithHandler(null, null));
        }

        [Fact]
        public async Task WithHandler_WrapsTurnContextAndSendsInvokeResponse()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = "handoff/action",
                ChannelId = Channels.Msteams,
                Value = new { Continuation = "continuation-token" },
            };
            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(activity);

            IActivity sentActivity = null;
            mockContext.Setup(c => c.SendActivityAsync(It.IsAny<IActivity>(), It.IsAny<CancellationToken>()))
                .Callback<IActivity, CancellationToken>((invokeResponse, cancellationToken) => sentActivity = invokeResponse)
                .ReturnsAsync(new ResourceResponse());

            TeamsTurnContext capturedContext = null;
            string capturedContinuation = null;

            var route = TeamsHandoffRouteBuilder.Create()
                .WithHandler((turnContext, turnState, continuation, cancellationToken) =>
                {
                    capturedContext = turnContext;
                    capturedContinuation = continuation;
                    return Task.CompletedTask;
                }, null)
                .Build();

            await route.Handler(mockContext.Object, Mock.Of<ITurnState>(), CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.IsType<TeamsTurnContext>(capturedContext);
            Assert.Same(activity, capturedContext.Activity);
            Assert.Equal("continuation-token", capturedContinuation);
            Assert.NotNull(sentActivity);
            Assert.Equal(ActivityTypes.InvokeResponse, sentActivity.Type);
        }
    }
}
