using Microsoft.Agents.Core.Telemetry;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry
{
    public static class ConnectorTelemetry
    {

        private static readonly Counter<long> ConnectorRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricConnectorRequestTotal, "request");
        
        private static readonly Histogram<long> ConnectorRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Constants.MetricConnectorRequestDuration, "ms");

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
                    activity.SetTag(Core.Telemetry.Constants.AttrConversationId, conversationId);
                }
                if (activityId != null)
                {
                    activity.SetTag(Core.Telemetry.Constants.AttrActivityId, activityId);
                }
            }
            return timedActivity;
        }

        public static IDisposable StartConnectorReplyToActivity(string conversationId, string activityId)
        {
            return StartConnectorOp(Constants.ActivityConnectorReplyToActivity, conversationId, activityId);
        }

        public static IDisposable StartConnectorSendToConversation(string conversationId, string activityId)
        {
            return StartConnectorOp(Constants.ActivityConnectorSendToConversation, conversationId, activityId);
        }

        public static IDisposable StartConnectorUpdateActivity(string conversationId, string activityId)
        {
            return StartConnectorOp(Constants.ActivityConnectorUpdateActivity, conversationId, activityId);
        }

        public static IDisposable StartConnectorDeleteActivity(string conversationId, string activityId)
        {
            return StartConnectorOp(Constants.ActivityConnectorDeleteActivity, conversationId, activityId);
        }

        public static IDisposable StartConnectorCreateConversation()
        {
            return StartConnectorOp(Constants.ActivityConnectorCreateConversation);
        }

        public static IDisposable StartConnectorGetConversations()
        {
            return StartConnectorOp(Constants.ActivityConnectorGetConversations);
        }

        public static IDisposable StartConnectorGetConversationMembers()
        {
            return StartConnectorOp(Constants.ActivityConnectorGetConversationMembers);
        }

        public static IDisposable StartConnectorUploadAttachment(string conversationId)
        {
            return StartConnectorOp(Constants.ActivityConnectorUploadAttachment, conversationId);
        }

        public static IDisposable StartConnectorGetAttachment(string conversationId, string activityId, string attachmentId)
        {
            TimedActivity timedActivity = StartConnectorOp(Constants.ActivityConnectorGetAttachment, conversationId, activityId);
            timedActivity.Activity?.SetTag(Core.Telemetry.Constants.AttrAttachmentId, attachmentId);
            return timedActivity;
        }
    }
}
