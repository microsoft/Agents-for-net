// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

#nullable enable

namespace Microsoft.Agents.Authentication.Msal.Telemetry
{
    public static class AuthenticationMsalTelemetry
    {
        private static readonly Counter<long> AuthTokenRequestTotal = AgentsTelemetry.Meter.CreateCounter<long>(
            AuthenticationMsalTelemetryConstants.MetricAuthTokenRequestTotal, "request");
        private static readonly Histogram<long> AuthTokenRequestDuration = AgentsTelemetry.Meter.CreateHistogram<long>(
            AuthenticationMsalTelemetryConstants.MetricAuthTokenRequestDuration, "ms");

        private static Activity? StartAuthOp(
                string? agenticAppInstanceId = null,
                string? agenticUserId = null,
                AuthTypes? authType = null,
                IList<string>? scopes = null
            )
        {
            TimedActivity timedActivity = AgentsTelemetry.StartTimedActivity(
                AuthenticationMsalTelemetryConstants.ActivityAuthTokenRequest,
                (activity, duration, error) =>
                {
                    AuthTokenRequestTotal.Add(1);
                    AuthTokenRequestDuration.Record(duration);
                }
            );
            var activity = timedActivity.Activity;

            if (activity != null)
            {
                if (agenticAppInstanceId != null)
                {
                    activity.SetTag("agentic.instanceId", agenticAppInstanceId);
                }
                if (agenticUserId != null)
                {
                    activity.SetTag("agentic.userId", agenticUserId);
                }
                if (scopes != null)
                {
                    activity.SetTag("auth.scopes", scopes);
                }
                if (authType != null)
                {
                    activity.SetTag("auth.type", authType.ToString());
                }
            }

            return activity;
        }

        public static IDisposable StartAuthAcquireTokenOnBehalfOf(IList<string> scopes)
        {
            return StartAuthOp(scopes: scopes);
        }

        public static IDisposable StartAuthGetAccessToken(IList<string> scopes, AuthTypes authType)
        {
            return StartAuthOp(scopes: scopes, authType: authType);
        }

        public static IDisposable StartAuthGetAgenticInstanceToken(string agenticAppInstanceId)
        {
            return StartAuthOp(agenticAppInstanceId: agenticAppInstanceId);
        }

        public static IDisposable StartAuthGetAgenticUserToken(string agenticAppInstanceId, string agenticUserId, IList<string> scopes)
        {
            return StartAuthOp(
                agenticUserId: agenticUserId,
                agenticAppInstanceId: agenticAppInstanceId,
                scopes: scopes
            );
        }
    }
}
