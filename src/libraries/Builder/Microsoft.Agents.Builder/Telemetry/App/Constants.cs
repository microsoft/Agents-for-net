// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry.App
{

    internal static class Constants
    {
        internal static readonly string ScopeOnTurn = "agents.app.run";
        internal static readonly string ScopeRouteHandler = "agents.app.route_handler";
        internal static readonly string ScopeBeforeTurn = "agents.app.before_turn";
        internal static readonly string ScopeAfterTurn = "agents.app.after_turn";
        internal static readonly string ScopeDownloadFiles = "agents.app.download_files";

        internal static readonly string MetricTurnCount = "agents.turn.count";
        internal static readonly string MetricTurnErrorCount = "agents.turn.error.count";
        internal static readonly string MetricTurnDuration = "agents.turn.duration";
    }
}