using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Hosting.AspNetCore.Telemetry
{
    internal static class HostingAspNetCoreTelemetry
    {

        internal static IDisposable? StartGetTaskFromWorkItem(Activity parent)
        {
            Activity? activity = AgentsTelemetry.StartActivity(Scopes.GetTaskFromWorkItem);
            if (activity != null)
            {
                activity.SetParentId(parent.TraceId, parent.SpanId);
            }
            return activity;
        }
    }
}
