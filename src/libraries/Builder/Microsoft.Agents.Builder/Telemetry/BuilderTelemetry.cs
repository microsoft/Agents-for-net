// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry
{

    public static class BuilderTelemetry
    {

        /* AgentApplication metrics */

        private static readonly Counter<long> TurnTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            BuilderTelemetryConstants.AgentTurnTotalMetricName, "turn");
        private static readonly Counter<long> TurnErrors = AgentsTelemetry.Meter.CreateCounter<long>(
            BuilderTelemetryConstants.AgentTurnErrorsMetricName, "turn");
        private static readonly Histogram<long> TurnDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            BuilderTelemetryConstants.AgentTurnDurationMetricName, "ms");

        /* ChannelAdapter metrics */

        private static readonly Counter<long> AdapterProcessTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            BuilderTelemetryConstants.AdapterProcessTotalMetricName, "request");
        private static readonly Histogram<long> AdapterProcessDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            BuilderTelemetryConstants.AdapterProcessDurationMetricName, "ms");

        /* ConnectorClient metrics */

        private static readonly Counter<long> ConnectorRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            BuilderTelemetryConstants.ConnectorRequestTotalMetricName, "request");
        private static readonly Histogram<long> ConnectorRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            BuilderTelemetryConstants.ConnectorRequestDurationMetricName, "ms");

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

        public static Activity? StartActivity(string name, ITurnContext turnContext)
        {
            var activity = AgentsTelemetryConstants.ActivitySource.StartActivity(name);
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

        public static TimedActivity StartTimedActivity(
            string operationName,
            ITurnContext? turnContext,
            Action<Activity?, long, Exception?>? callback = null
            )
        {
            var activity = StartActivity(operationName, turnContext);
            return new TimedActivity(activity, callback);
        }

        public static TimedActivity StartAgentTurnOperation(ITurnContext turnContext)
        {
            return StartTimedActivity(
                BuilderTelemetryConstants.AgentTurnOperationName,
                turnContext,
                callback: (activity, duration, error) =>
                {
                    TurnTotal.Add(1);
                    TurnDuration.Record(duration);
                    if (error !=  null)
                    {
                        TurnErrors.Add(1);
                    }
                }
            );
        }

        public static TimedActivity StartAdapterProcessOperation()
        {
            return StartTimedActivity(
                BuilderTelemetryConstants.AdapterProcessOperationName,
                null,
                callback: (activity, duration, error) =>
                {
                    AdapterProcessTotal.Add(1);
                    AdapterProcessDuration.Record(duration);
                }
            );
        }

        public static TimedActivity StartConnectorRequestOperation(string operationType, ITurnContext turnContext)
        {
            string operationName = String.Format(BuilderTelemetryConstants.ConnectorRequestOperationNameFormat, operationType);
            return StartTimedActivity(
                operationName,
                turnContext,
                callback: (activity, duration, error) =>
                {
                    ConnectorRequestTotal.Add(1);
                    ConnectorRequestDuration.Record(duration);
                }
            );
        }
    }
}