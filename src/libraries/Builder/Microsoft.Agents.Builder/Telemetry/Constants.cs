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

        public static readonly string ActivityConnectorReplyToActivity = "agents.connector.replyToActivity";
        public static readonly string ActivityConnectorSendToConversation = "agents.connector.sendToConversation";
        public static readonly string ActivityConnectorUpdateActivity = "agents.connector.updateActivity";
        public static readonly string ActivityConnectorDeleteActivity = "agents.connector.deleteActivity";
        public static readonly string ActivityConnectorCreateConversation = "agents.connector.createConversation";
        public static readonly string ActivityConnectorGetConversations = "agents.connector.getConversations";
        public static readonly string ActivityConnectorGetConversationMembers = "agents.connector.getConversationMembers";
        public static readonly string ActivityConnectorUploadAttachment = "agents.connector.uploadAttachment";
        public static readonly string ActivityConnectorGetAttachment = "agents.connector.getAttachment";

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

        public static readonly string MetricConnectorRequestTotal = "agents.connector.request.total";

        // histograms

        public static readonly string MetricTurnDuration = "agents.turn.duration";
        public static readonly string MetricAdapterProcessDuration = "agents.adapter.process.duration";
        public static readonly string MetricConnectorRequestDuration = "agents.connector.request.duration";
    }
}
