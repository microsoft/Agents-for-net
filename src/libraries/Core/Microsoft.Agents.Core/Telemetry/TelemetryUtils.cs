using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class TelemetryUtils
    {
        public static readonly string Unknown = "unknown";

        public static string FormatScopes(IEnumerable<string>? scopes)
        {
            if (scopes == null || !scopes.Any())
            {
                return Unknown;
            }
            return string.Join(",", scopes);
        }
    }
}
