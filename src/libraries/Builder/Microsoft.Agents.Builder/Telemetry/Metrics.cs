// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry
{
    public static class Metrics
    {
        // counters

        public static readonly string ActivitiesReceived = "agents.activities.received";
        public static readonly string ActivitiesSent = "agents.activities.sent";
        public static readonly string ActivitiesUpdated = "agents.activities.updated";
        public static readonly string ActivitiesDeleted = "agents.activities.deleted";

        public static readonly string TurnTotal = "agents.turn.total";
        public static readonly string TurnErrors = "agents.turn.errors";

        // histograms

        public static readonly string TurnDuration = "agents.turn.duration";
        public static readonly string AdapterProcessDuration = "agents.adapter.process.duration";
    }
}
