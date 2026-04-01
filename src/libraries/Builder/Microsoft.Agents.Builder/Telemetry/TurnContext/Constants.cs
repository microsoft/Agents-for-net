using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Telemetry.TurnContext
{
    /// <summary>
    /// Defines the <see cref="System.Diagnostics.Activity"/> names used by the turn-context telemetry scopes.
    /// </summary>
    internal static class Constants
    {
        /// <summary>Activity name for sending activities from within a turn context.</summary>
        internal static readonly string ScopeSendActivities = "agents.turn.send_activities";
    }
}
