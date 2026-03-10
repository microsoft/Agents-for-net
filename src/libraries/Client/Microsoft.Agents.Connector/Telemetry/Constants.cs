namespace Microsoft.Agents.Connector.Telemetry
{
    internal static class Constants
    {
        /* Activities */
        public static readonly string ActivityConnectorReplyToActivity = "agents.connector.replyToActivity";
        public static readonly string ActivityConnectorSendToConversation = "agents.connector.sendToConversation";
        public static readonly string ActivityConnectorUpdateActivity = "agents.connector.updateActivity";
        public static readonly string ActivityConnectorDeleteActivity = "agents.connector.deleteActivity";
        public static readonly string ActivityConnectorCreateConversation = "agents.connector.createConversation";
        public static readonly string ActivityConnectorGetConversations = "agents.connector.getConversations";
        public static readonly string ActivityConnectorGetConversationMembers = "agents.connector.getConversationMembers";
        public static readonly string ActivityConnectorUploadAttachment = "agents.connector.uploadAttachment";
        public static readonly string ActivityConnectorGetAttachment = "agents.connector.getAttachment";

        /* Metrics */
        public static readonly string MetricConnectorRequestTotal = "agents.connector.request.total";
        public static readonly string MetricConnectorRequestDuration = "agents.connector.request.duration";
    }
}
