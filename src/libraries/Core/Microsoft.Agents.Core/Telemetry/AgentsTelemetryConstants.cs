using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class TelemetryConstants
    {
        public static readonly string SourceName = "Microsoft.Agents.Builder";
        public static readonly string SourceVersion = "1.0.0";
        public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);
    }
}
