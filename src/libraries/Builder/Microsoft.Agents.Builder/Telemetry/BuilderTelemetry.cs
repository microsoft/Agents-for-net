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

        private static Dictionary<string, string> ExtractAttributesFromActivityModel(Core.Models.IActivity activityModel)
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>();
            attributes["activity.type"] = activityModel.Type;
            attributes["agent.is_agentic"] = activityModel.IsAgenticRequest().ToString();
            if (activityModel.From != null)
            {
                attributes["from.id"] = activityModel.From.Id;
            }
            if (activityModel.Recipient != null)
            {
                attributes["recipient.id"] = activityModel.Recipient.Id;
            }
            if (activityModel.Conversation != null)
            {
                attributes["conversation.id"] = activityModel.Conversation.Id;
            }
            attributes["channel_id"] = activityModel.ChannelId;
            attributes["message.text.length"] = activityModel.Text?.Length.ToString() ?? "0";
            return attributes;
        }

        public static void SetTagsFromActivityModel(Activity activity, Core.Models.IActivity activityModel)
        {
            var attributes = ExtractAttributesFromActivityModel(activityModel);
            foreach (var kvp in attributes)
            {
                activity?.SetTag(kvp.Key, kvp.Value);
            }
        }

        public static Activity? StartActivity(string name, Core.Models.IActivity? activityModel = null)
        {
            var activity = AgentsTelemetry.ActivitySource.StartActivity(name);
            if (activity != null)
            {
                SetTagsFromActivityModel(activity, activityModel ?? new Core.Models.Activity());
            }
            return activity;
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

        public static TimedActivity StartAdapterProcess()
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(
                Scopes.AdapterProcess,
                (activity, duration, exception) =>
                {
                    AdapterProcessDuration.Record(duration);
                });
            return timedActivity;
        }

        public static void UpdateAdapterProcess(TimedActivity timedActivity, Core.Models.IActivity activityModel)
        {
            Activity? activity = timedActivity.Activity;
            if (activity != null)
            {
                SetTagsFromActivityModel(activity, activityModel);
                activity.SetTag(Attributes.ActivityType, activityModel.Type);
                activity.SetTag(Attributes.ActivityChannelId, activityModel.ChannelId);
                activity.SetTag(Attributes.ActivityDeliveryMode, activityModel.DeliveryMode);
                activity.SetTag(Attributes.ConversationId, activityModel.Conversation?.Id);
                activity.SetTag(Attributes.IsAgenticRequest, activityModel.IsAgenticRequest());
            }
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

        public static IDisposable StartAdapterCreateConnectorClient(string serviceUrl, IList<string>? scopes, bool isAgenticRequest)
        {
            Activity? activity = StartActivity(Scopes.AdapterCreateConnectorClient);
            if (activity != null)
            {
                activity.SetTag(Attributes.ServiceUrl, serviceUrl);
                activity.SetTag(Attributes.IsAgenticRequest, isAgenticRequest);

                if (scopes != null)
                {
                    activity.SetTag(Attributes.AuthScopes, string.Join(",", scopes));
                }
                else
                {
                    activity.SetTag(Attributes.AuthScopes, "");
                }
            }
            return activity;
        }

        /* AgentApplication activities */

        public static TimedActivity StartAppRun(ITurnContext turnContext)
        {
            TimedActivity timedActivity = StartTimedActivity(
                Scopes.AppRun,
                turnContext.Activity,
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
            return StartActivity(Scopes.AppRouteHandler, turnContext.Activity);
        }

        public static IDisposable StartAppBeforeTurn(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppBeforeTurn, turnContext.Activity);
        }

        public static IDisposable StartAppAfterTurn(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppAfterTurn, turnContext.Activity);
        }

        public static IDisposable StartAppDownloadFiles(ITurnContext turnContext)
        {
            return StartActivity(Scopes.AppDownloadFiles, turnContext.Activity);
        }

        /* TurnContext */

        public static IDisposable StartTurnContextSendActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Scopes.TurnSendActivity, turnContext.Activity);
        }

        public static IDisposable StartTurnContextUpdateActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Scopes.TurnUpdateActivity, turnContext.Activity);
        }

        public static IDisposable StartTurnContextDeleteActivity(ITurnContext turnContext)
        {
            return BuilderTelemetry.StartActivity(Scopes.TurnDeleteActivity, turnContext.Activity);
        }
    }
}