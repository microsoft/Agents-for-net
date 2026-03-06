// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Builder.Telemetry
{
    public static class BuilderTelemetry
    {

        /* AgentApplication metrics */

        private static readonly Counter<long> TurnTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTurnTotal, "turn");
        private static readonly Counter<long> TurnErrors = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricTurnErrors, "turn");
        private static readonly Histogram<long> TurnDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Constants.MetricTurnDuration, "ms");

        /* ChannelAdapter metrics */

        private static readonly Histogram<long> AdapterProcessDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Constants.MetricAdapterProcessDuration, "ms");
        
        /* ConnectorClient metrics */

        private static readonly Counter<long> ConnectorRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Constants.MetricConnectorRequestTotal, "request");
        private static readonly Histogram<long> ConnectorRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Constants.MetricConnectorRequestDuration, "ms");

        private static Dictionary<string, string> ExtractAttributesFromTurnContext(ITurnContext turnContext)
            {
                Dictionary<string, string> attributes = new Dictionary<string, string>();
                attributes["activity.type"] = turnContext.Activity.Type;
                attributes["agent.is_agentic"] = turnContext.IsAgenticRequest().ToString();
                if (turnContext.Activity.From != null)
                {
                    attributes["from.id"] = turnContext.Activity.From.Id;
                }
                if (turnContext.Activity.Recipient != null)
                {
                    attributes["recipient.id"] = turnContext.Activity.Recipient.Id;
                }
                if (turnContext.Activity.Conversation != null)
                {
                    attributes["conversation.id"] = turnContext.Activity.Conversation.Id;
                }
                attributes["channel_id"] = turnContext.Activity.ChannelId;
                attributes["message.text.length"] = turnContext.Activity.Text?.Length.ToString() ?? "0";
                return attributes;
            }

        public static Activity? StartActivity(string name, ITurnContext? turnContext)
        {
            var activity = AgentsTelemetry.ActivitySource.StartActivity(name);
            if (turnContext != null)
            {
                var attributes = ExtractAttributesFromTurnContext(turnContext);
                foreach (var kvp in attributes)
                {
                    activity?.SetTag(kvp.Key, kvp.Value);
                }
            }
            return activity;
        }

        public static Activity? StartActivity(string name, Core.Models.IActivity? activityModel)
        {
            var activity = AgentsTelemetry.ActivitySource.StartActivity(name);
            if (activity != null)
            {
                // Add any relevant tags from the Core.Models.Activity if needed
            }
            return activity;
        }

        public static TimedActivity StartTimedActivity(
            string operationName,
            ITurnContext? turnContext,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            var activity = StartActivity(operationName, turnContext);
            return new TimedActivity(activity, callback);
        }

        public static TimedActivity StartTimedActivity(
            string operationName,
            Core.Models.IActivity? activityModel,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            var activity = StartActivity(operationName, activityModel);
            return new TimedActivity(activity, callback);
        }

        /* Activity Starter methods */

        public static IDisposable StartAdapterProcess(Core.Models.IActivity activityModel)
        {
            return StartTimedActivity(Constants.ActivityAdapterProcess, activityModel);
        }

        public static IDisposable StartAdapterSendActivities(Core.Models.IActivity[] activityModels)
        {
            return AgentsTelemetry.StartTimedActivity(Constants.ActivityAdapterProcess);
        }

        public static IDisposable StartAdapterUpdateActivity(Core.Models.IActivity activityModel)
        {
            return StartActivity(Constants.ActivityAdapterUpdateActivity, activityModel);
        }

        public static IDisposable StartAdapterDeleteActivity(Core.Models.IActivity activityModel)
        {
            return StartActivity(Constants.ActivityAdapterDeleteActivity, activityModel);
        }

        public static IDisposable StartAdapterContinueConversation(Core.Models.IActivity activityModel)
        {
            return StartActivity(Constants.ActivityAdapterContinueConversation, activityModel);
        }

        /* AgentApplication activities */

        public static IDisposable StartAppRun(ITurnContext turnContext)
        {
            return StartActivity(Constants.ActivityAppRun, turnContext);
        }

        public static IDisposable StartAppRouteHandler(ITurnContext turnContext)
        {
            return StartActivity(Constants.ActivityAppRouteHandler, turnContext);
        }

        public static IDisposable StartAppBeforeTurn(ITurnContext turnContext)
        {
            return StartActivity(Constants.ActivityAppBeforeTurn, turnContext);
        }

        public static IDisposable StartAppAfterTurn(ITurnContext turnContext)
        {
            return StartActivity(Constants.ActivityAppAfterTurn, turnContext);
        }

        public static IDisposable StartAppDownloadFiles(ITurnContext turnContext)
        {
            return StartActivity(Constants.ActivityAppDownloadFiles, turnContext);
        }

        /* ConnectorClient */

        private static Activity? StartConnectorOp(string activityName, string? conversationId = null, string? activityId = null)
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
            Activity activity = timedActivity.Activity;
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
            return activity;
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
            Activity? activity = StartConnectorOp(Constants.ActivityConnectorGetAttachment, conversationId, activityId);
            activity?.SetTag(Core.Telemetry.Constants.AttrAttachmentId, attachmentId);
            return activity;
        }

        /* TurnContext */

        public static IDisposable StartTurnContextSendActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Constants.ActivityTurnSendActivity, turnContext);
        }

        public static IDisposable StartTurnContextUpdateActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Constants.ActivityTurnUpdateActivity, turnContext);
        }

        public static IDisposable StartTurnContextDeleteActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Constants.ActivityTurnDeleteActivity, turnContext);
        }
    }
}