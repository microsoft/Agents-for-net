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
            Metrics.TurnTotal, "turn");
        private static readonly Counter<long> TurnErrors = AgentsTelemetry.Meter.CreateCounter<long>(
            Metrics.TurnErrors, "turn");
        private static readonly Histogram<long> TurnDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Metrics.TurnDuration, "ms");

        /* ChannelAdapter metrics */

        private static readonly Histogram<long> AdapterProcessDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Metrics.AdapterProcessDuration, "ms");
        
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
            Activity? activity = AgentsTelemetry.ActivitySource.StartActivity(name);
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

        public static Activity? StartActivity(string name, Core.Models.IActivity? activityModel = null)
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
            Activity? activity = StartActivity(operationName, turnContext);
            return new TimedActivity(activity, callback);
        }

        public static TimedActivity StartTimedActivity(
            string operationName,
            Core.Models.IActivity? activityModel = null,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            Activity? activity = StartActivity(operationName, activityModel);
            return new TimedActivity(activity, callback);
        }

        /* Activity Starter methods */

        public static IDisposable StartAdapterProcess(Core.Models.IActivity activityModel)
        {
            TimedActivity timedActivity = StartTimedActivity(
                Scopes.AdapterProcess,
                activityModel,
                (activity, duration, exception) =>
                {
                    AdapterProcessDuration.Record(duration);
                });
            Activity? activity = timedActivity.Activity;
            if (activity != null)
            {
                activity.SetTag(Attributes.ActivityType, activityModel.Type);
                activity.SetTag(Attributes.ActivityChannelId, activityModel.ChannelId);
                activity.SetTag(Attributes.ActivityDeliveryMode, activityModel.DeliveryMode);
                activity.SetTag(Attributes.ConversationId, activityModel.Conversation?.Id);
                activity.SetTag(Attributes.IsAgenticRequest, activityModel.IsAgenticRequest());
            }
            return timedActivity;
        }

        public static IDisposable StartAdapterSendActivities(Core.Models.IActivity[] activityModels)
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(Scopes.AdapterSendActivities);
            Activity? activity = timedActivity.Activity;
            if (activity != null)
            {
                activity.SetTag(Attributes.ActivityCount, activityModels.Length);
                if (activityModels.Length > 0)
                {
                    activity.SetTag(Attributes.ConversationId, activityModels[0].Conversation?.Id);
                }
            }
            return timedActivity;
        }

        public static IDisposable StartAdapterUpdateActivity(Core.Models.IActivity activityModel)
        {
            var timedActivity = AgentsTelemetry.StartTimedActivity(Scopes.AdapterUpdateActivity);
            var activity = timedActivity.Activity;
            if (activity != null)
            {
                activity.SetTag(Attributes.ActivityId, activityModel.Id);
                activity.SetTag(Attributes.ConversationId, activityModel.Conversation?.Id);
            }
            return timedActivity;
        }

        public static IDisposable StartAdapterDeleteActivity(Core.Models.IActivity activityModel)
        {
            Activity? activity = StartActivity(Scopes.AdapterDeleteActivity);
            if (activity != null)
            {
                activity.SetTag(Attributes.ActivityId, activityModel.Id);
                activity.SetTag(Attributes.ConversationId, activityModel.Conversation?.Id);
            }
            return activity;
        }

        public static IDisposable StartAdapterContinueConversation(Core.Models.IActivity activityModel)
        {
            Activity? activity = StartActivity(Scopes.AdapterContinueConversation, activityModel);
            if (activity != null)
            {
                activity.SetTag(Attributes.ConversationId, activityModel.Conversation?.Id);
                activity.SetTag(Attributes.IsAgenticRequest, activityModel.IsAgenticRequest());
            }
            return activity;
        }

        public static IDisposable StartAdapterCreateConnectorClient(string serviceUrl, IEnumerable<string> scopes, bool isAgenticRequest)
        {
            Activity? activity = StartActivity(Scopes.AdapterCreateConnectorClient);
            if (activity != null)
            {
                activity.SetTag(Attributes.ServiceUrl, serviceUrl);
                activity.SetTag(Attributes.AuthScopes, string.Join(",", scopes));
                activity.SetTag(Attributes.IsAgenticRequest, isAgenticRequest.ToString());
            }
            return activity;
        }

        /* AgentApplication activities */

        public static TimedActivity StartAppRun(ITurnContext turnContext)
        {
            TimedActivity timedActivity = StartTimedActivity(
                Scopes.AppRun,
                turnContext,
                (activity, duration, exception) =>
                {
                    TurnDuration.Record(duration);
                    if (exception != null)
                    {
                        TurnErrors.Add(1);
                    }
                    else
                    {
                        TurnTotal.Add(1);
                    }

                });

            Activity? activity = timedActivity.Activity;
            if (activity != null)
            {
                activity.SetTag(Attributes.ActivityType, turnContext.Activity.Type);
                activity.SetTag(Attributes.ActivityId, turnContext.Activity.Id);
            }
            return timedActivity;
        }

        public static void UpdateAppRun(TimedActivity timedActivity, bool routeAuthorized, bool routeMatched)
        {
            Activity? activity = timedActivity.Activity;
            if (activity != null)
            {
                activity.SetTag("route.authorized", routeAuthorized.ToString());
                activity.SetTag("route.matched", routeMatched.ToString());
            }
        }

        public static IDisposable StartAppRouteHandler(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppRouteHandler, turnContext);
        }

        public static IDisposable StartAppBeforeTurn(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppBeforeTurn, turnContext);
        }

        public static IDisposable StartAppAfterTurn(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppAfterTurn, turnContext);
        }

        public static IDisposable StartAppDownloadFiles(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppDownloadFiles, turnContext);
        }

        /* TurnContext */

        public static IDisposable StartTurnContextSendActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Scopes.TurnSendActivity, turnContext);
        }

        public static IDisposable StartTurnContextUpdateActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Scopes.TurnUpdateActivity, turnContext);
        }

        public static IDisposable StartTurnContextDeleteActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Scopes.TurnDeleteActivity, turnContext);
        }
    }
}