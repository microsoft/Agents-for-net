// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System.Diagnostics.Metrics;

namespace Microsoft.Agents.Authentication.Msal.Telemetry
{
    public static class AuthenticationMsalTelemetry
    {
        private static readonly Counter<long> AuthTokenRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            AuthenticationMsalTelemetryConstants.AuthTokenRequestTotalMetricName, "request");
        private static readonly Histogram<long> AuthTokenRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            AuthenticationMsalTelemetryConstants.AuthTokenRequestDurationMetricName, "ms");

        public static TimedActivity StartAuthTokenRequest()
        {
            return AgentsTelemetry.StartTimedActivity(
                AuthenticationMsalTelemetryConstants.AuthTokenRequestOperationName,
                (activity, duration, error) =>
                {
                    AuthTokenRequestTotal.Add(1);
                    AuthTokenRequestDuration.Record(duration);
                }
            );
        }
    }
}
