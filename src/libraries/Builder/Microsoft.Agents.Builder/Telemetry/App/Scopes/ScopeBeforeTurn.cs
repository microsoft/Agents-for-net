// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;

namespace Microsoft.Agents.Builder.Telemetry.App.Scopes
{
    /// <summary>
    /// A <see cref="Microsoft.Agents.Core.Telemetry.TelemetryScope"/> that traces the before-turn middleware pipeline
    /// execution for a turn.
    /// </summary>
    internal class ScopeBeforeTurn : TelemetryScope
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Builder.Telemetry.App.Scopes.ScopeBeforeTurn"/> class.
        /// </summary>
        public ScopeBeforeTurn() : base(Constants.ScopeBeforeTurn)
        {
        }
    }
}