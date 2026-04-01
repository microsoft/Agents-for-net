// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Builder.Telemetry.App.Scopes;
using Microsoft.Agents.Builder.Telemetry.Authorization.Scopes;
using ScopeSendActivities = Microsoft.Agents.Builder.Telemetry.TurnContext.ScopeSendActivities;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry
{
    public class AppAndAuthorizationScopeTests : IDisposable
    {
        private readonly System.Diagnostics.ActivityListener _listener;
        private readonly List<System.Diagnostics.Activity> _startedActivities = new();
        private readonly List<System.Diagnostics.Activity> _stoppedActivities = new();

        public AppAndAuthorizationScopeTests()
        {
            _listener = new System.Diagnostics.ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentsTelemetry.SourceName,
                Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> options) => System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _startedActivities.Add(activity),
                ActivityStopped = activity => _stoppedActivities.Add(activity)
            };
            System.Diagnostics.ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private static ITurnContext CreateTurnContext(
            string type = "message",
            string channelId = "test-channel",
            string conversationId = "conv-1",
            string activityId = "act-1",
            int attachmentCount = 0)
        {
            var attachments = new List<Attachment>();
            for (int i = 0; i < attachmentCount; i++)
                attachments.Add(new Attachment { ContentType = "application/octet-stream" });

            return new TurnContext(new NotImplementedAdapter(), new Activity
            {
                Type = type,
                ChannelId = channelId,
                Id = activityId,
                Conversation = new ConversationAccount { Id = conversationId },
                Recipient = new ChannelAccount { Id = "bot-1" },
                From = new ChannelAccount { Id = "user-1" },
                Attachments = attachments
            });
        }

        #region ScopeOnTurn

        [Fact]
        public void ScopeOnTurn_CreatesActivity_WithCorrectName()
        {
            var ctx = CreateTurnContext();
            using var scope = new ScopeOnTurn(ctx);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.app.run", started.OperationName);
        }

        [Fact]
        public void ScopeOnTurn_Callback_SetsActivityMetadataTags()
        {
            var ctx = CreateTurnContext(type: "message", channelId: "msteams", conversationId: "conv-99", activityId: "act-42");
            var scope = new ScopeOnTurn(ctx);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("message", stopped.GetTagItem(TagNames.ActivityType));
            Assert.Equal("msteams", stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal("conv-99", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("act-42", stopped.GetTagItem(TagNames.ActivityId));
        }

        [Fact]
        public void ScopeOnTurn_Callback_SetsRouteTagsAfterShare()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeOnTurn(ctx);
            scope.Share(routeAuthorized: true, routeMatched: true);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteAuthorized));
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteMatched));
        }

        [Fact]
        public void ScopeOnTurn_Callback_RouteTagsAreNull_WhenShareNotCalled()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeOnTurn(ctx);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.RouteAuthorized));
            Assert.Null(stopped.GetTagItem(TagNames.RouteMatched));
        }

        [Fact]
        public void ScopeOnTurn_SetError_SetsErrorStatus()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeOnTurn(ctx);
            scope.SetError(new InvalidOperationException("turn error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("turn error", stopped.StatusDescription);
        }

        #endregion

        #region ScopeBeforeTurn

        [Fact]
        public void ScopeBeforeTurn_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeBeforeTurn();

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.app.before_turn", started.OperationName);
        }

        [Fact]
        public void ScopeBeforeTurn_SetError_SetsErrorStatus()
        {
            var scope = new ScopeBeforeTurn();
            scope.SetError(new InvalidOperationException("before-turn error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeAfterTurn

        [Fact]
        public void ScopeAfterTurn_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAfterTurn();

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.app.after_turn", started.OperationName);
        }

        [Fact]
        public void ScopeAfterTurn_SetError_SetsErrorStatus()
        {
            var scope = new ScopeAfterTurn();
            scope.SetError(new InvalidOperationException("after-turn error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeRouteHandler

        [Fact]
        public void ScopeRouteHandler_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeRouteHandler(isInvoke: false, isAgentic: false);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.app.route_handler", started.OperationName);
        }

        [Fact]
        public void ScopeRouteHandler_Callback_SetsRouteIsInvokeTag()
        {
            var scope = new ScopeRouteHandler(isInvoke: true, isAgentic: false);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteIsInvoke));
            Assert.Equal(false, stopped.GetTagItem(TagNames.RouteIsAgentic));
        }

        [Fact]
        public void ScopeRouteHandler_Callback_SetsRouteIsAgenticTag()
        {
            var scope = new ScopeRouteHandler(isInvoke: false, isAgentic: true);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(false, stopped.GetTagItem(TagNames.RouteIsInvoke));
            Assert.Equal(true, stopped.GetTagItem(TagNames.RouteIsAgentic));
        }

        #endregion

        #region ScopeDownloadFiles

        [Fact]
        public void ScopeDownloadFiles_CreatesActivity_WithCorrectName()
        {
            var ctx = CreateTurnContext();
            using var scope = new ScopeDownloadFiles(ctx);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.app.download_files", started.OperationName);
        }

        [Fact]
        public void ScopeDownloadFiles_Callback_SetsAttachmentCountTag()
        {
            var ctx = CreateTurnContext(attachmentCount: 3);
            var scope = new ScopeDownloadFiles(ctx);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(3, stopped.GetTagItem(TagNames.AttachmentCount));
        }

        [Fact]
        public void ScopeDownloadFiles_Callback_SetsZeroAttachmentCount_WhenNoAttachments()
        {
            var ctx = CreateTurnContext(attachmentCount: 0);
            var scope = new ScopeDownloadFiles(ctx);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(0, stopped.GetTagItem(TagNames.AttachmentCount));
        }

        #endregion

        #region ScopeAgenticToken

        [Fact]
        public void ScopeAgenticToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAgenticToken("handler-1", null, null);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.authorization.agentic_token", started.OperationName);
        }

        [Fact]
        public void ScopeAgenticToken_Callback_SetsAuthHandlerIdTag()
        {
            var scope = new ScopeAgenticToken("handler-abc", null, null);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("handler-abc", stopped.GetTagItem(TagNames.AuthHandlerId));
        }

        [Fact]
        public void ScopeAgenticToken_Callback_SetsExchangeConnectionTag_WhenProvided()
        {
            var scope = new ScopeAgenticToken("handler-1", "my-connection", null);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("my-connection", stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        [Fact]
        public void ScopeAgenticToken_Callback_SetsAuthScopesTag_WhenProvided()
        {
            var scope = new ScopeAgenticToken("handler-1", null, new[] { "scope-a", "scope-b" });
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("scope-a,scope-b", stopped.GetTagItem(TagNames.AuthScopes));
        }

        #endregion

        #region ScopeAzureBotToken

        [Fact]
        public void ScopeAzureBotToken_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAzureBotToken("handler-1", null, null);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.authorization.azure_bot_token", started.OperationName);
        }

        [Fact]
        public void ScopeAzureBotToken_Callback_SetsAllTags()
        {
            var scope = new ScopeAzureBotToken("handler-xyz", "conn-1", new[] { "s1" });
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("handler-xyz", stopped.GetTagItem(TagNames.AuthHandlerId));
            Assert.Equal("conn-1", stopped.GetTagItem(TagNames.ExchangeConnection));
            Assert.Equal("s1", stopped.GetTagItem(TagNames.AuthScopes));
        }

        #endregion

        #region ScopeAzureBotSignIn

        [Fact]
        public void ScopeAzureBotSignIn_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAzureBotSignIn("handler-1", null, null);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.authorization.azure_bot_sign_in", started.OperationName);
        }

        [Fact]
        public void ScopeAzureBotSignIn_Callback_SetsAuthHandlerIdAndConnectionTags()
        {
            var scope = new ScopeAzureBotSignIn("handler-sign", "oauth-conn", null);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("handler-sign", stopped.GetTagItem(TagNames.AuthHandlerId));
            Assert.Equal("oauth-conn", stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        #endregion

        #region ScopeAzureBotSignOut

        [Fact]
        public void ScopeAzureBotSignOut_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeAzureBotSignOut("handler-1");

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.authorization.azure_bot_sign_out", started.OperationName);
        }

        [Fact]
        public void ScopeAzureBotSignOut_Callback_SetsAuthHandlerIdTag()
        {
            var scope = new ScopeAzureBotSignOut("handler-out");
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("handler-out", stopped.GetTagItem(TagNames.AuthHandlerId));
        }

        [Fact]
        public void ScopeAzureBotSignOut_Callback_DoesNotSetExchangeConnection()
        {
            var scope = new ScopeAzureBotSignOut("handler-1");
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ExchangeConnection));
        }

        #endregion

        #region TurnContext ScopeSendActivities

        [Fact]
        public void TurnContextScopeSendActivities_CreatesActivity_WithCorrectName()
        {
            var ctx = CreateTurnContext();
            using var scope = new ScopeSendActivities(ctx);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.turn.send_activities", started.OperationName);
        }

        [Fact]
        public void TurnContextScopeSendActivities_Callback_SetsConversationIdTag()
        {
            var ctx = CreateTurnContext(conversationId: "conv-send-123");
            var scope = new ScopeSendActivities(ctx);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("conv-send-123", stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void TurnContextScopeSendActivities_SetError_SetsErrorStatus()
        {
            var ctx = CreateTurnContext();
            var scope = new ScopeSendActivities(ctx);
            scope.SetError(new InvalidOperationException("send error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}
