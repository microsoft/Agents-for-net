// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.Telemetry.Proactive.Scopes;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.TestSupport;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class ProactiveScopeTests : TelemetryScopeTestBase
    {
        private static IActivity CreateTestActivity(
            string type = "message",
            string channelId = "test-channel")
        {
            return new Activity
            {
                Type = type,
                ChannelId = channelId,
                Conversation = new ConversationAccount { Id = "conv-1" }
            };
        }

        private static CreateConversationOptions CreateOptions(
            string channelId = "msteams",
            int memberCount = 2)
        {
            var members = new List<ChannelAccount>();
            for (int i = 0; i < memberCount; i++)
            {
                members.Add(new ChannelAccount($"user-{i + 1}", $"User {i + 1}"));
            }

            return new CreateConversationOptions
            {
                ChannelId = channelId,
                Parameters = new ConversationParameters
                {
                    Members = members
                }
            };
        }

        #region ScopeCreateConversation

        [Fact]
        public void ScopeCreateConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeCreateConversation(CreateOptions());

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.proactive.create_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeCreateConversation_Callback_SetsTags()
        {
            var scope = new ScopeCreateConversation(CreateOptions(channelId: "webchat", memberCount: 3));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("webchat", stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal(3, stopped.GetTagItem(TagNames.MembersCount));
        }

        [Fact]
        public void ScopeCreateConversation_Callback_SetsZeroMembersCount_WhenNoMembers()
        {
            var scope = new ScopeCreateConversation(CreateOptions(channelId: "webchat", memberCount: 0));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("webchat", stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal(0, stopped.GetTagItem(TagNames.MembersCount));
        }

        [Fact]
        public void ScopeCreateConversation_Callback_DoesNotThrow_WhenOptionsIsNull()
        {
            var scope = new ScopeCreateConversation(null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal(0, stopped.GetTagItem(TagNames.MembersCount));
        }

        [Fact]
        public void ScopeCreateConversation_Callback_DoesNotThrow_WhenParametersIsNull()
        {
            var scope = new ScopeCreateConversation(new CreateConversationOptions
            {
                ChannelId = "webchat",
                Parameters = null
            });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("webchat", stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal(0, stopped.GetTagItem(TagNames.MembersCount));
        }

        [Fact]
        public void ScopeCreateConversation_Callback_DoesNotThrow_WhenMembersIsNull()
        {
            var scope = new ScopeCreateConversation(new CreateConversationOptions
            {
                ChannelId = "webchat",
                Parameters = new ConversationParameters
                {
                    Members = null
                }
            });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("webchat", stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal(0, stopped.GetTagItem(TagNames.MembersCount));
        }

        [Fact]
        public void ScopeCreateConversation_Callback_SetsNullChannelId_WhenChannelIdIsNull()
        {
            var scope = new ScopeCreateConversation(new CreateConversationOptions
            {
                ChannelId = null,
                Parameters = new ConversationParameters
                {
                    Members = new List<ChannelAccount>()
                }
            });
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ActivityChannelId));
            Assert.Equal(0, stopped.GetTagItem(TagNames.MembersCount));
        }

        [Fact]
        public void ScopeCreateConversation_SetError_SetsErrorStatus()
        {
            var scope = new ScopeCreateConversation(CreateOptions());
            scope.SetError(new InvalidOperationException("create conversation error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("create conversation error", stopped.StatusDescription);
        }

        #endregion

        #region ScopeStoreConversation

        [Fact]
        public void ScopeStoreConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeStoreConversation("conv-store");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.proactive.store_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeStoreConversation_Callback_SetsConversationIdTag()
        {
            var scope = new ScopeStoreConversation("conv-store");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-store", stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void ScopeStoreConversation_Callback_SetsNullConversationIdTag_WhenConversationIdIsNull()
        {
            var scope = new ScopeStoreConversation(null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void ScopeStoreConversation_SetError_SetsErrorStatus()
        {
            var scope = new ScopeStoreConversation("conv-store");
            scope.SetError(new InvalidOperationException("store conversation error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeGetConversation

        [Fact]
        public void ScopeGetConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeGetConversation("conv-123");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.proactive.get_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeGetConversation_Callback_SetsTags_WhenConversationFoundIsShared()
        {
            var scope = new ScopeGetConversation("conv-123");
            scope.Share(true);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal(true, stopped.GetTagItem(TagNames.ConversationFound));
        }

        [Fact]
        public void ScopeGetConversation_Callback_SetsTags_WhenConversationNotFoundIsShared()
        {
            var scope = new ScopeGetConversation("conv-123");
            scope.Share(false);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal(false, stopped.GetTagItem(TagNames.ConversationFound));
        }

        [Fact]
        public void ScopeGetConversation_Callback_ConversationFoundIsNull_WhenShareNotCalled()
        {
            var scope = new ScopeGetConversation("conv-123");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-123", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Null(stopped.GetTagItem(TagNames.ConversationFound));
        }

        [Fact]
        public void ScopeGetConversation_Callback_SetsNullConversationIdTag_WhenConversationIdIsNull()
        {
            var scope = new ScopeGetConversation(null);
            scope.Share(true);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal(true, stopped.GetTagItem(TagNames.ConversationFound));
        }

        [Fact]
        public void ScopeGetConversation_SetError_SetsErrorStatus()
        {
            var scope = new ScopeGetConversation("conv-123");
            scope.SetError(new InvalidOperationException("get conversation error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeDeleteConversation

        [Fact]
        public void ScopeDeleteConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeDeleteConversation("conv-delete");

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.proactive.delete_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeDeleteConversation_Callback_SetsConversationIdTag()
        {
            var scope = new ScopeDeleteConversation("conv-delete");
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-delete", stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void ScopeDeleteConversation_Callback_SetsNullConversationIdTag_WhenConversationIdIsNull()
        {
            var scope = new ScopeDeleteConversation(null);
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ConversationId));
        }

        [Fact]
        public void ScopeDeleteConversation_SetError_SetsErrorStatus()
        {
            var scope = new ScopeDeleteConversation("conv-delete");
            scope.SetError(new InvalidOperationException("delete conversation error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeSendActivity

        [Fact]
        public void ScopeSendActivity_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeSendActivity("conv-send", CreateTestActivity());

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.proactive.send_activity", started.OperationName);
        }

        [Fact]
        public void ScopeSendActivity_Callback_SetsTags()
        {
            var scope = new ScopeSendActivity("conv-send", CreateTestActivity(type: "event", channelId: "msteams"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-send", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("event", stopped.GetTagItem(TagNames.ActivityType));
            Assert.Equal("msteams", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        [Fact]
        public void ScopeSendActivity_Callback_SetsNullActivityTags_WhenActivityPropertiesAreNull()
        {
            var scope = new ScopeSendActivity("conv-send", CreateTestActivity(type: null, channelId: null));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-send", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Null(stopped.GetTagItem(TagNames.ActivityType));
            Assert.Null(stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        [Fact]
        public void ScopeSendActivity_Callback_SetsNullConversationIdTag_WhenConversationIdIsNull()
        {
            var scope = new ScopeSendActivity(null, CreateTestActivity(type: "event", channelId: "msteams"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("event", stopped.GetTagItem(TagNames.ActivityType));
            Assert.Equal("msteams", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        [Fact]
        public void ScopeSendActivity_SetError_SetsErrorStatus()
        {
            var scope = new ScopeSendActivity("conv-send", CreateTestActivity());
            scope.SetError(new InvalidOperationException("send activity error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeContinueConversation

        [Fact]
        public void ScopeContinueConversation_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeContinueConversation("conv-continue", CreateTestActivity());

            var started = Assert.Single(StartedActivities);
            Assert.Equal("agents.proactive.continue_conversation", started.OperationName);
        }

        [Fact]
        public void ScopeContinueConversation_Callback_SetsTags()
        {
            var scope = new ScopeContinueConversation("conv-continue", CreateTestActivity(type: "event", channelId: "directline"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-continue", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("event", stopped.GetTagItem(TagNames.ActivityType));
            Assert.Equal("directline", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        [Fact]
        public void ScopeContinueConversation_Callback_SetsNullActivityTags_WhenActivityPropertiesAreNull()
        {
            var scope = new ScopeContinueConversation("conv-continue", CreateTestActivity(type: null, channelId: null));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal("conv-continue", stopped.GetTagItem(TagNames.ConversationId));
            Assert.Null(stopped.GetTagItem(TagNames.ActivityType));
            Assert.Null(stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        [Fact]
        public void ScopeContinueConversation_Callback_SetsNullConversationIdTag_WhenConversationIdIsNull()
        {
            var scope = new ScopeContinueConversation(null, CreateTestActivity(type: "event", channelId: "directline"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Null(stopped.GetTagItem(TagNames.ConversationId));
            Assert.Equal("event", stopped.GetTagItem(TagNames.ActivityType));
            Assert.Equal("directline", stopped.GetTagItem(TagNames.ActivityChannelId));
        }

        [Fact]
        public void ScopeContinueConversation_SetError_SetsErrorStatus()
        {
            var scope = new ScopeContinueConversation("conv-continue", CreateTestActivity());
            scope.SetError(new InvalidOperationException("continue conversation error"));
            scope.Dispose();

            var stopped = Assert.Single(StoppedActivities);
            Assert.Equal(System.Diagnostics.ActivityStatusCode.Error, stopped.Status);
        }

        #endregion
    }
}