// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Authentication.Telemetry
{
    public static class Scopes
    {
        public static readonly string GetAccessToken = "agents.auth.getAccessToken";
        public static readonly string AcquireTokenOnBehalfOf = "agents.auth.acquireTokenOnBehalfOf";
        public static readonly string GetAgenticInstanceToken = "agents.auth.getAgenticInstanceToken";
        public static readonly string GetAgenticUserToken = "agents.auth.getAgenticUserToken";
    }
}
