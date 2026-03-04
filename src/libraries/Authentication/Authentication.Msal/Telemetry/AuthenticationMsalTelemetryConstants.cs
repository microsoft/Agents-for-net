// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Authentication.Msal.Telemetry
{
    public static class AuthenticationMsalTelemetryConstants
    {
        public static readonly string AuthTokenRequestOperationName = "auth token request";
        public static readonly string AuthTokenRequestDurationMetricName = "agents.auth.request.duration";
        public static readonly string AuthTokenRequestTotalMetricName = "agents.auth.request.total";
    }
}
