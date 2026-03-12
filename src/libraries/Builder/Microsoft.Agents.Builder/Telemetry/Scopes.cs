// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.Telemetry
{
    public static class Scopes
    {
        public static readonly string AdapterProcess = "agents.adapter.process";
        public static readonly string AdapterSendActivities = "agents.adapter.sendActivities";
        public static readonly string AdapterUpdateActivity = "agents.adapter.updateActivity";
        public static readonly string AdapterDeleteActivity = "agents.adapter.deleteActivity";
        public static readonly string AdapterContinueConversation = "agents.adapter.continueConversation";
        public static readonly string AdapterCreateConnectorClient = "agents.adapter.createConnectorClient";

        public static readonly string AppRun = "agents.app.run";
        public static readonly string AppRouteHandler = "agents.app.routeHandler";
        public static readonly string AppBeforeTurn = "agents.app.beforeTurn";
        public static readonly string AppAfterTurn = "agents.app.afterTurn";
        public static readonly string AppDownloadFiles = "agents.app.downloadFiles";

        public static readonly string TurnSendActivity = "agents.turn.sendActivity";
        public static readonly string TurnUpdateActivity = "agents.turn.updateActivity";
        public static readonly string TurnDeleteActivity = "agents.turn.deleteActivity";
    }
}
