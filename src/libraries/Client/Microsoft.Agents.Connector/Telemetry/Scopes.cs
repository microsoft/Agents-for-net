// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Connector.Telemetry
{
    public static class Scopes
    {
        public static readonly string ReplyToActivity = "agents.connector.replyToActivity";
        public static readonly string SendToConversation = "agents.connector.sendToConversation";
        public static readonly string UpdateActivity = "agents.connector.updateActivity";
        public static readonly string DeleteActivity = "agents.connector.deleteActivity";
        public static readonly string CreateConversation = "agents.connector.createConversation";
        public static readonly string GetConversations = "agents.connector.getConversations";
        public static readonly string GetConversationMembers = "agents.connector.getConversationMembers";
        public static readonly string UploadAttachment = "agents.connector.uploadAttachment";
        public static readonly string GetAttachment = "agents.connector.getAttachment";
    }
}
