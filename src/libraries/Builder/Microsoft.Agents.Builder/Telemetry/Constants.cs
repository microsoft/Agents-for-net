// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry
{
    public static class Constants
    {

        /* Activity names */

        public static readonly string ActivityAdapterProcess = "agents.adapter.process";
        public static readonly string ActivityAdapterSendActivities = "agents.adapter.sendActivities";
        public static readonly string ActivityAdapterUpdateActivity = "agents.adapter.updateActivity";
        public static readonly string ActivityAdapterDeleteActivity = "agents.adapter.deleteActivity";
        public static readonly string ActivityAdapterContinueConversation = "agents.adapter.continueConversation";
        public static readonly string ActivityAdapterCreateConnectorClient = "agents.adapter.createConnectorClient";

        public static readonly string ActivityAppRun = "agents.app.run";
        public static readonly string ActivityAppRouteHandler = "agents.app.routeHandler";
        public static readonly string ActivityAppBeforeTurn = "agents.app.beforeTurn";
        public static readonly string ActivityAppAfterTurn = "agents.app.afterTurn";
        public static readonly string ActivityAppDownloadFiles = "agents.app.downloadFiles";

        public static readonly string ActivityTurnSendActivity = "agents.turn.sendActivity";
        public static readonly string ActivityTurnUpdateActivity = "agents.turn.updateActivity";
        public static readonly string ActivityTurnDeleteActivity = "agents.turn.deleteActivity";

        /* Metric names */

        // counters

        public static readonly string MetricActivitiesReceived = "agents.activities.received";
        public static readonly string MetricActivitiesSent = "agents.activities.sent";
        public static readonly string MetricActivitiesUpdated = "agents.activities.updated";
        public static readonly string MetricActivitiesDeleted = "agents.activities.deleted";

        public static readonly string MetricTurnTotal = "agents.turn.total";
        public static readonly string MetricTurnErrors = "agents.turn.errors";

        // histograms

        public static readonly string MetricTurnDuration = "agents.turn.duration";
        public static readonly string MetricAdapterProcessDuration = "agents.adapter.process.duration";
    }
}
