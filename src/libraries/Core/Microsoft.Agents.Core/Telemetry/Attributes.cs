// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Telemetry
{
    public static class Attributes
    {
        public static readonly string ActivityDeliveryMode = "activity.deliveryMode";
        public static readonly string ActivityChannelId = "activity.channelId";
        public static readonly string ActivityId = "activity.id";
        public static readonly string ActivityCount = "activities.count";
        public static readonly string ActivityType = "activity.type";
        public static readonly string AgenticUserId = "agentic.userId";
        public static readonly string AgenticInstanceId = "agentic.instanceId";
        public static readonly string AttachmentId = "attachment.id";
        public static readonly string AuthScopes = "auth.scopes";
        public static readonly string AuthType = "auth.method";

        public static readonly string ConversationId = "conversation.id";
        public static readonly string IsAgenticRequest = "isAgenticRequest";
        public static readonly string NumKeys = "keys.num";
        public static readonly string RouteIsInvoke = "route.isInvoke";
        public static readonly string RouteIsAgentic = "route.isAgentic";

        public static readonly string ServiceUrl = "serviceUrl";

        public static readonly string Unknown = "unknown";
    }
}
