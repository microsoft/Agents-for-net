// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry
{
    public static class ConnectorTelemetry
    {

        private static readonly Counter<long> ConnectorRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Metrics.RequestTotal, "request");
        
        private static readonly Histogram<long> ConnectorRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Metrics.RequestDuration, "ms");

        /* ConnectorClient */

        private static TimedActivity StartConnectorOp(string activityName, string? conversationId = null, string? activityId = null)
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(
                activityName,
                (a, d, e) =>
                {
                    if (e != null)
                    {
                        a?.SetTag("error", true);
                        a?.SetTag("error.message", e.Message);
                    }
                });
            Activity? activity = timedActivity.Activity;
            if (activity != null)
            {
                if (conversationId != null)
                {
                    activity.SetTag(Attributes.ConversationId, conversationId);
                }
                if (activityId != null)
                {
                    activity.SetTag(Attributes.ActivityId, activityId);
                }
            }
            return timedActivity;
        }

        public static IDisposable StartConnectorReplyToActivity(string conversationId, string activityId)
        {
            return StartConnectorOp(Scopes.ReplyToActivity, conversationId, activityId);
        }

        public static IDisposable StartConnectorSendToConversation(string conversationId, string activityId)
        {
            return StartConnectorOp(Scopes.SendToConversation, conversationId, activityId);
        }

        public static IDisposable StartConnectorUpdateActivity(string conversationId, string activityId)
        {
            return StartConnectorOp(Scopes.UpdateActivity, conversationId, activityId);
        }

        public static IDisposable StartConnectorDeleteActivity(string conversationId, string activityId)
        {
            return StartConnectorOp(Scopes.DeleteActivity, conversationId, activityId);
        }

        public static IDisposable StartConnectorCreateConversation()
        {
            return StartConnectorOp(Scopes.CreateConversation);
        }

        public static IDisposable StartConnectorGetConversations()
        {
            return StartConnectorOp(Scopes.GetConversations);
        }

        public static IDisposable StartConnectorGetConversationMembers()
        {
            return StartConnectorOp(Scopes.GetConversationMembers);
        }

        public static IDisposable StartConnectorUploadAttachment(string conversationId)
        {
            return StartConnectorOp(Scopes.UploadAttachment, conversationId);
        }

        public static IDisposable StartConnectorGetAttachment(string conversationId, string activityId, string attachmentId)
        {
            TimedActivity timedActivity = StartConnectorOp(Scopes.GetAttachment, conversationId, activityId);
            timedActivity.Activity?.SetTag(Attributes.AttachmentId, attachmentId);
            return timedActivity;
        }
    }
}
