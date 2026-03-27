// Licensed under the MIT License.

// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Builder.Telemetry.ChannelAdapter.Scopes;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry.ChannelAdapter.Scopes
{
    public class ScopeTests : IDisposable
    {
        private readonly System.Diagnostics.ActivityListener _listener;
        private readonly List<System.Diagnostics.Activity> _startedActivities = new List<System.Diagnostics.Activity>();
        private readonly List<System.Diagnostics.Activity> _stoppedActivities = new List<System.Diagnostics.Activity>();

        public ScopeTests()
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

        private static IActivity CreateTestActivity(
            string type = "message",
            string channelId = "test-channel",
            string conversationId = "conv-1",
            string deliveryMode = "normal",
            string activityId = "act-1",
            string recipientId = "bot-1",
            string recipientRole = null)
        {
            return new Core.Models.Activity
            {
                Type = type,
                ChannelId = channelId,
                Id = activityId,
                DeliveryMode = deliveryMode,
                Conversation = new ConversationAccount { Id = conversationId },
                Recipient = new ChannelAccount { Id = recipientId, Role = recipientRole }
            };
        }

        #region ScopeProcess

        [Fact]
        public void ScopeProcess_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeProcess();

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.adapter.process", started.OperationName);
        }

        [Fact]
        public void ScopeProcess_Callback_SetsTags_WhenActivityIsShared()
        {
            var activity = CreateTestActivity(
                type: "message",
                channelId: "msteams",
                conversationId: "conv-123",
                deliveryMode: "expectReplies");

            var scope = new ScopeProcess();
            scope.Share(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var tags = stopped.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("message", tags[TagNames.ActivityType]);
            Assert.Equal("msteams", tags[TagNames.ActivityChannelId]);
            Assert.Equal("expectReplies", tags[TagNames.ActivityDeliveryMode]);
            Assert.Equal("conv-123", tags[TagNames.ConversationId]);
        }

        [Fact]
        public void ScopeProcess_Callback_SetsIsAgenticTag_ForNonAgenticRequest()
        {
            var activity = CreateTestActivity(recipientRole: "user");

            var scope = new ScopeProcess();
            scope.Share(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var isAgentic = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.IsAgentic).Value;
            Assert.Equal("False", isAgentic);
        }

        [Fact]
        public void ScopeProcess_Callback_SetsIsAgenticTag_ForAgenticRequest()
        {
            var activity = CreateTestActivity(recipientRole: RoleTypes.AgenticUser);

            var scope = new ScopeProcess();
            scope.Share(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var isAgentic = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.IsAgentic).Value;
            Assert.Equal("True", isAgentic);
        }

        [Fact]
        public void ScopeProcess_Callback_DoesNotThrow_WhenNoActivityShared()
        {
            var scope = new ScopeProcess();
            // Don't call Share - should still dispose without error
            scope.Dispose();
        }

        #endregion

        #region ScopeSendActivities

        [Fact]
        public void ScopeSendActivities_CreatesActivity_WithCorrectName()
        {
            var activities = new IActivity[] { CreateTestActivity() };
            using var scope = new ScopeSendActivities(activities);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.adapter.send_activities", started.OperationName);
        }

        [Fact]
        public void ScopeSendActivities_Callback_SetsCountTag()
        {
            var activities = new IActivity[]
            {
                CreateTestActivity(type: "message"),
                CreateTestActivity(type: "typing"),
                CreateTestActivity(type: "event")
            };

            var scope = new ScopeSendActivities(activities);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var count = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.ActivityCount).Value;
            Assert.Equal("3", count);
        }

        [Fact]
        public void ScopeSendActivities_Callback_SetsConversationId_FromFirstActivity()
        {
            var activities = new IActivity[]
            {
                CreateTestActivity(channelId: "webchat"),
                CreateTestActivity(channelId: "msteams")
            };

            var scope = new ScopeSendActivities(activities);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var conversationId = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.ConversationId).Value;
            Assert.Equal("webchat", conversationId);
        }

        [Fact]
        public void ScopeSendActivities_Callback_SetsUnknown_WhenNoActivities()
        {
            var activities = Array.Empty<IActivity>();

            var scope = new ScopeSendActivities(activities);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var conversationId = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.ConversationId).Value;
            Assert.Equal(TelemetryUtils.Unknown, conversationId);

            var count = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.ActivityCount).Value;
            Assert.Equal("0", count);
        }

        #endregion

        #region ScopeUpdateActivity

        [Fact]
        public void ScopeUpdateActivity_CreatesActivity_WithCorrectName()
        {
            var activity = CreateTestActivity();
            using var scope = new ScopeUpdateActivity(activity);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.adpater.update_activity", started.OperationName);
        }

        [Fact]
        public void ScopeUpdateActivity_Callback_SetsTags()
        {
            var activity = CreateTestActivity(activityId: "act-42", conversationId: "conv-99");

            var scope = new ScopeUpdateActivity(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var tags = stopped.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("act-42", tags[TagNames.ActivityId]);
            Assert.Equal("conv-99", tags[TagNames.ConversationId]);
        }

        #endregion

        #region ScopeDeleteActivity

        [Fact]
        public void ScopeDeleteActivity_CreatesActivity_WithCorrectName()
        {
            var activity = CreateTestActivity();
            using var scope = new ScopeDeleteActivity(activity);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.adapter.delete_activity", started.OperationName);
        }

        [Fact]
        public void ScopeDeleteActivity_Callback_SetsTags()
        {
            var activity = CreateTestActivity(type: "event", conversationId: "conv-del");

            var scope = new ScopeDeleteActivity(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var tags = stopped.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("event", tags[TagNames.ActivityType]);
            Assert.Equal("conv-del", tags[TagNames.ConversationId]);
        }

        #endregion

        #region ScopeContinueConversation

        [Fact]
        public void ScopeContinueConversation_CreatesActivity_WithCorrectName()
        {
            var activity = CreateTestActivity();
            using var scope = new ScopeContinueConversation(activity);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.adapter.continue_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeContinueConversation_Callback_SetsTags()
        {
            var activity = CreateTestActivity(
                recipientId: "app-123",
                conversationId: "conv-456",
                recipientRole: "user");

            var scope = new ScopeContinueConversation(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var tags = stopped.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("app-123", tags[TagNames.AppId]);
            Assert.Equal("conv-456", tags[TagNames.ConversationId]);
            Assert.Equal("False", tags[TagNames.IsAgentic]);
        }

        [Fact]
        public void ScopeContinueConversation_Callback_SetsIsAgentic_ForAgenticIdentity()
        {
            var activity = CreateTestActivity(recipientRole: RoleTypes.AgenticIdentity);

            var scope = new ScopeContinueConversation(activity);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var isAgentic = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.IsAgentic).Value;
            Assert.Equal("True", isAgentic);
        }

        #endregion

        #region ScopeCreateConnectorClient

        [Fact]
        public void ScopeCreateConnectorClient_CreatesActivity()
        {
            using var scope = new ScopeCreateConnectorClient("https://smba.trafficmanager.net/", new[] { "scope1" }, false);

            Assert.Single(_startedActivities);
        }

        [Fact]
        public void ScopeCreateConnectorClient_Callback_SetsTags()
        {
            var scopes = new List<string> { "https://api.botframework.com/.default", "openid" };

            var scope = new ScopeCreateConnectorClient("https://service.url/", scopes, true);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var tags = stopped.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal("https://service.url/", tags[TagNames.ServiceUrl]);
            Assert.Equal("https://api.botframework.com/.default,openid", tags[TagNames.AuthScopes]);
            Assert.Equal("True", tags[TagNames.IsAgentic]);
        }

        [Fact]
        public void ScopeCreateConnectorClient_Callback_SetsUnknownScopes_WhenNull()
        {
            var scope = new ScopeCreateConnectorClient("https://service.url/", null, false);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var tags = stopped.Tags.ToDictionary(t => t.Key, t => t.Value);
            Assert.Equal(TelemetryUtils.Unknown, tags[TagNames.AuthScopes]);
            Assert.Equal("False", tags[TagNames.IsAgentic]);
        }

        #endregion

        #region ScopeCreateUserTokenClient

        [Fact]
        public void ScopeCreateUserTokenClient_CreatesActivity()
        {
            using var scope = new ScopeCreateUserTokenClient("https://token.endpoint/");

            Assert.Single(_startedActivities);
        }

        [Fact]
        public void ScopeCreateUserTokenClient_Callback_SetsTokenServiceEndpointTag()
        {
            var scope = new ScopeCreateUserTokenClient("https://token.botframework.com/");
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            var endpoint = stopped.Tags.FirstOrDefault(t => t.Key == TagNames.TokenServiceEndpoint).Value;
            Assert.Equal("https://token.botframework.com/", endpoint);
        }

        #endregion

        #region Error handling across scopes

        [Fact]
        public void ScopeProcess_SetError_SetsErrorStatus()
        {
            var scope = new ScopeProcess();
            scope.Share(CreateTestActivity());
            scope.SetError(new InvalidOperationException("process error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("process error", stopped.StatusDescription);
        }

        [Fact]
        public void ScopeSendActivities_SetError_SetsErrorStatus()
        {
            var activities = new IActivity[] { CreateTestActivity() };
            var scope = new ScopeSendActivities(activities);
            scope.SetError(new InvalidOperationException("send error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        [Fact]
        public void ScopeDeleteActivity_SetError_SetsErrorStatus()
        {
            var scope = new ScopeDeleteActivity(CreateTestActivity());
            scope.SetError(new InvalidOperationException("delete error"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}
