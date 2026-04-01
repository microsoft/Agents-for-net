using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="TelemetryScope"/> that traces the after-turn middleware pipeline
    /// execution for a turn.
    /// </summary>
    internal class ScopeAfterTurn : TelemetryScope
    {
        public ScopeAfterTurn() : base(Constants.ScopeAfterTurn)
        {
        }
    }
}
