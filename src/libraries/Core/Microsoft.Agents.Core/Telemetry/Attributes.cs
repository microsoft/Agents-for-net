// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Telemetry
{
    public static class Attributes // TODO: SpanAttributes
    {
        public static readonly string ActivityDeliveryMode = "activity.delivery_mode";
        public static readonly string ActivityChannelId = "activity.channel_id";
        public static readonly string ActivityId = "activity.id";
        public static readonly string ActivityCount = "activities.count";
        public static readonly string ActivityType = "activity.type";
        public static readonly string AgenticUserId = "agentic.user_id";
        public static readonly string AgenticInstanceId = "agentic.instance_id";
        public static readonly string AttachmentId = "attachment.id";
        public static readonly string AuthScopes = "auth.scopes";
        public static readonly string AuthType = "auth.method";

        public static readonly string ConversationId = "conversation.id";
        public static readonly string IsAgenticRequest = "is_agentic_request";
        public static readonly string NumKeys = "keys.num";
        public static readonly string RouteIsInvoke = "route.is_invoke";
        public static readonly string RouteIsAgentic = "route.is_agentic";

        public static readonly string ServiceUrl = "service_url";

        public static readonly string Unknown = "unknown";
    }
}
