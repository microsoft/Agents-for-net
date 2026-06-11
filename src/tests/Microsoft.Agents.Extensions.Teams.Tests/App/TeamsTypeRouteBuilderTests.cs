// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TeamsTypeRouteBuilderTests
    {
        [Fact]
        public void Create_ReturnsNewInstance()
        {
            var builder = TeamsTypeRouteBuilder.Create();

            Assert.NotNull(builder);
            Assert.IsType<TeamsTypeRouteBuilder>(builder);
        }

        [Fact]
        public async Task Build_SetsTeamsChannelId()
        {
            var route = TeamsTypeRouteBuilder.Create()
                .WithType(ActivityTypes.Message)
                .WithHandler((turnContext, turnState, cancellationToken) => Task.CompletedTask, null)
                .Build();

            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
            });

            Assert.Equal(Channels.Msteams, route.ChannelId);
            Assert.True(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public async Task Build_DoesNotMatchNonTeamsChannel()
        {
            var route = TeamsTypeRouteBuilder.Create()
                .WithType(ActivityTypes.Message)
                .WithHandler((turnContext, turnState, cancellationToken) => Task.CompletedTask, null)
                .Build();

            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Directline,
            });

            Assert.False(await route.Selector(mockContext.Object, CancellationToken.None));
        }

        [Fact]
        public void WithHandler_NullHandler_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TeamsTypeRouteBuilder.Create().WithHandler(null, null));
        }

        [Fact]
        public async Task WithHandler_WrapsTurnContext()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
            };
            var mockContext = new Mock<Builder.ITurnContext>();
            mockContext.Setup(c => c.Activity).Returns(activity);

            TeamsTurnContext capturedContext = null;

            var route = TeamsTypeRouteBuilder.Create()
                .WithType(ActivityTypes.Message)
                .WithHandler((turnContext, turnState, cancellationToken) =>
                {
                    capturedContext = turnContext;
                    return Task.CompletedTask;
                }, null)
                .Build();

            await route.Handler(mockContext.Object, Mock.Of<ITurnState>(), CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.IsType<TeamsTurnContext>(capturedContext);
            Assert.Same(activity, capturedContext.Activity);
        }
    }
}
