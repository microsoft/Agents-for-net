// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.Tests.Model;
using Moq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class HandlerUtilsTests
    {
        [Fact]
        public async Task WrapHandler_RouteHandler_UsesTeamsTurnContext()
        {
            var turnContext = CreateTurnContext(new Activity { Type = ActivityTypes.Message, ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams });
            ITurnState capturedState = null;
            ITeamsTurnContext capturedContext = null;

            var wrapped = HandlerUtils.WrapHandler((ctx, state, ct) =>
            {
                capturedContext = ctx;
                capturedState = state;
                return Task.CompletedTask;
            });

            var turnState = Mock.Of<ITurnState>();
            await wrapped(turnContext, turnState, CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.IsType<TeamsTurnContext>(capturedContext);
            Assert.Same(turnState, capturedState);
            Assert.Same(turnContext.Activity, capturedContext.Activity);
        }

        [Fact]
        public async Task WrapHandler_HandoffHandler_UsesTeamsTurnContext_AndPassesContinuation()
        {
            var turnContext = CreateTurnContext(new Activity { Type = ActivityTypes.Invoke, Name = "handoff/action", ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams });
            ITurnState capturedState = null;
            ITeamsTurnContext capturedContext = null;
            string capturedContinuation = null;

            var wrapped = HandlerUtils.WrapHandler((ctx, state, continuation, ct) =>
            {
                capturedContext = ctx;
                capturedState = state;
                capturedContinuation = continuation;
                return Task.CompletedTask;
            });

            var turnState = Mock.Of<ITurnState>();
            await wrapped(turnContext, turnState, "continue-token", CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.IsType<TeamsTurnContext>(capturedContext);
            Assert.Same(turnState, capturedState);
            Assert.Equal("continue-token", capturedContinuation);
            Assert.Same(turnContext.Activity, capturedContext.Activity);
        }

        [Fact]
        public async Task WrapHandler_FeedbackLoopHandler_UsesTeamsTurnContext_AndPassesFeedbackData()
        {
            var turnContext = CreateTurnContext(new Activity { Type = ActivityTypes.Invoke, Name = "message/submitAction", ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams });
            ITurnState capturedState = null;
            ITeamsTurnContext capturedContext = null;
            FeedbackData capturedFeedback = null;
            var feedbackData = new FeedbackData { ReplyToId = "reply-id" };

            var wrapped = HandlerUtils.WrapHandler((ctx, state, feedback, ct) =>
            {
                capturedContext = ctx;
                capturedState = state;
                capturedFeedback = feedback;
                return Task.CompletedTask;
            });

            var turnState = Mock.Of<ITurnState>();
            await wrapped(turnContext, turnState, feedbackData, CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.IsType<TeamsTurnContext>(capturedContext);
            Assert.Same(turnState, capturedState);
            Assert.Same(feedbackData, capturedFeedback);
            Assert.Same(turnContext.Activity, capturedContext.Activity);
        }

        private static TurnContext CreateTurnContext(Activity activity)
        {
            var adapter = new NotImplementedAdapter();
            activity.Recipient ??= new ChannelAccount { Id = "recipientId" };
            activity.Conversation ??= new ConversationAccount { Id = "conversationId" };
            activity.From ??= new ChannelAccount { Id = "fromId" };

            var turnContext = new TurnContext(adapter, activity);

            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new AgentApplication(new AgentApplicationOptions(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            turnContext.Services.Set<Proactive>(app.Proactive);
            return turnContext;
        }
    }
}