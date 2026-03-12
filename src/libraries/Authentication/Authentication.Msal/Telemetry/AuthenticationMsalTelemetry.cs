using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Authentication.Telemetry;
using Microsoft.Agents.Core.Telemetry;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Authentication.Msal.Telemetry
{
    public static class AuthenticationMsalTelemetry
    {
        public static IDisposable StartAuthGetAccessToken(IEnumerable<string> scopes, AuthTypes authType)
        {
            TimedActivity timedActivity = AuthenticationTelemetry.StartAuthOp(
                Scopes.AcquireTokenOnBehalfOf,
                scopes: scopes);

            var activity = timedActivity.Activity;

            if (activity != null)
            {
                activity.SetTag("auth.type", authType.ToString());
            }

            return timedActivity;
        }
    }
}
