// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

#nullable enable

namespace Microsoft.Agents.Authentication.Telemetry
{
    public static class AuthenticationTelemetry
    {
        private static readonly Counter<long> AuthTokenRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            Metrics.TokenRequestCount, "request");
        private static readonly Histogram<long> AuthTokenRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            Metrics.TokenRequestDuration, "ms");

        public static TimedActivity StartAuthOp(
                string activityName,
                string? agentAppInstanceId = null,
                string? agenticUserId = null,
                IEnumerable<string>? scopes = null
            )
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(
                activityName,
                (activity, duration, error) =>
                {
                    AuthTokenRequestTotal.Add(1);
                    AuthTokenRequestDuration.Record(duration);
                }
            );
            var activity = timedActivity.Activity;

            if (activity != null)
            {
                if (agentAppInstanceId != null)
                {
                    activity.SetTag("agentic.instanceId", agentAppInstanceId);
                }
                if (agenticUserId != null)
                {
                    activity.SetTag("agentic.userId", agenticUserId);
                }
                if (scopes != null)
                {
                    activity.SetTag("auth.scopes", scopes);
                }
            }

            return timedActivity;
        }

        public static IDisposable StartAuthAcquireTokenOnBehalfOf(IEnumerable<string> scopes)
        {
            return StartAuthOp(Scopes.AcquireTokenOnBehalfOf, scopes: scopes);
        }

        public static IDisposable StartAuthGetAccessToken(IEnumerable<string> scopes)
        {
            return StartAuthOp(Scopes.AcquireTokenOnBehalfOf, scopes: scopes);
        }

        public static IDisposable StartAuthGetAgenticInstanceToken(string agentAppInstanceId)
        {
            return StartAuthOp(Scopes.GetAgenticInstanceToken, agentAppInstanceId: agentAppInstanceId);
        }

        public static IDisposable StartAuthGetAgenticUserToken(string agentAppInstanceId, string agenticUserId, IEnumerable<string> scopes)
        {
            return StartAuthOp(
                Scopes.GetAgenticUserToken,
                agenticUserId: agenticUserId,
                agentAppInstanceId: agentAppInstanceId,
                scopes: scopes
            );
        }
    }
}
