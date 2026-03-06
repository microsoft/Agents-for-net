using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Agents.Builder.Telemetry
{
    public static class Activities
    {

        /* Adapter activities */

        public static IDisposable StartAdapterProcess(Core.Models.Activity activityModel)
        {
            return AgentsBuilderTelemetry.StartTimedActivity(Constants.ActivityAdapterProcess, activityModel);
        }

        public static IDisposable StartAdapterSendActivities(IList<Core.Models.Activity> activityModels)
        {
            return AgentsBuilderTelemetry.StartTimedActivity(Constants.ActivityAdapterProcess, activityModels);
        }

        public static IDisposable StartActivityAdapterUpdateActivity(Core.Models.Activity activityModel)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAdapterUpdateActivity, activityModel);
        }

        public static IDisposable StartAdapterDelete(Core.Models.Activity activityModel)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAdapterDeleteActivity, activityModel);
        }

        public static IDisposable StartAdapterContinueConversation(Core.Models.Activity activityModel)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAdapterContinueConversation, activityModel);
        }

        /* AgentApplication activities */

        public static IDisposable StartAppRun(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAppRun, turnContext);
        }

        public static IDisposable StartAppRouteHandler(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAppRouteHandler, turnContext);
        }

        public static IDisposable StartAppBeforeTurn(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAppBeforeTurn, turnContext);
        }

        public static IDisposable StartAppAfterTurn(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAppAfterTurn, turnContext);
        }

        public static IDisposable StartAppDownloadFiles(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityAppDownloadFiles, turnContext);
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

        /* Storage */

        private static Activity? StartStorageOp(string activityName, int numKeys)
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(activityName);
            Activity? activity = timedActivity.Activity;

            activity?.SetTag(Core.Telemetry.Constants.AttrNumKeys, numKeys);

            return activity;
        }

        public static IDisposable StartStorageRead(int numKeys)
        {
            return StartStorageOp(Constants.ActivityStorageRead, numKeys);
        }

        public static IDisposable StartStorageWrite(int numKeys)
        {
            return StartStorageOp(Constants.ActivityStorageWrite, numKeys);
        }

        public static IDisposable StartStorageDelete(int numKeys)
        {
            return StartStorageOp(Constants.ActivityStorageDelete, numKeys);
        }

        /* TurnContext */

        public static IDisposable StartTurnContextSendActivity(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityTurnSendActivity, turnContext);
        }

        public static IDisposable StartTurnContextUpdateActivity(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityTurnUpdateActivity, turnContext);
        }

        public static IDisposable StartTurnContextDeleteActivity(ITurnContext turnContext)
        {
            return AgentsBuilderTelemetry.StartActivity(Constants.ActivityTurnDeleteActivity, turnContext);
        }
    }
}