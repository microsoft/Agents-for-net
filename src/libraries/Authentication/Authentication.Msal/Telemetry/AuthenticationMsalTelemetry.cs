// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

#nullable enable

namespace Microsoft.Agents.Authentication.Msal.Telemetry
{
    public static class AuthenticationMsalTelemetry
    {
        private static readonly Counter<long> AuthTokenRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            AuthenticationMsalTelemetryConstants.AuthTokenRequestTotalMetricName, "request");
        private static readonly Histogram<long> AuthTokenRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            AuthenticationMsalTelemetryConstants.AuthTokenRequestDurationMetricName, "ms");

        public static TimedActivity StartAuthTokenRequest(
                string? tenantId = null,
                string? agenticAppInstanceId = null,
                string? agenticUserId = null,
                IList<string>? scopes = null
            )
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(
                AuthenticationMsalTelemetryConstants.AuthTokenRequestOperationName,
                (activity, duration, error) =>
                {
                    AuthTokenRequestTotal.Add(1);
                    AuthTokenRequestDuration.Record(duration);
                }
            );

            if (agenticAppInstanceId != null)
            {
                timedActivity.Activity?.SetTag("agentic.instanceId", agenticAppInstanceId);
            }
            if (agenticUserId != null)
            {
                timedActivity.Activity?.SetTag("agentic.userId", agenticUserId);
            }
            if (scopes != null)
            {
                timedActivity.Activity?.SetTag("auth.scopes", scopes);
            }

            return timedActivity;
        }
    }
}
