using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class TagNames
    {
        public static readonly string ActivityDeliveryMode = "activity.delivery_mode";
        public static readonly string ActivityChannelId = "activity.channel_id";
        public static readonly string ActivityId = "activity.id";
        public static readonly string ActivityCount = "activities.count";
        public static readonly string ActivityType = "activity.type";

        public static readonly string AgenticUserId = "agentic.user_id";
        public static readonly string AgenticInstanceId = "agentic.instance_id";

        public static readonly string AppId = "agent.app_id";

        public static readonly string AttachmentId = "activity.attachment.id";
        public static readonly string AttachmentCount = "activity.attachments.count";

        public static readonly string AuthHandlerId = "auth.handler.id";    
        public static readonly string AuthMethod = "auth.method";
        public static readonly string AuthScopes = "auth.scopes";
        public static readonly string AuthSuccess = "auth.success";
        
        public static readonly string ConnectionName = "auth.connection.name";
        public static readonly string ConversationId = "activity.conversation.id";

        public static readonly string HttpMethod = "http.method";
        public static readonly string HttpStatusCode = "http.status_code";

        public static readonly string IsAgentic = "is_agentic_request";
        
        public static readonly string KeyCount = "storage.keys.count";

        public static readonly string Operation = "operation";

        public static readonly string RouteAuthorized = "route.authorized";
        public static readonly string RouteIsInvoke = "route.is_invoke";
        public static readonly string RouteIsAgentic = "route.is_agentic";
        public static readonly string RouteMatched = "route.matched";

        public static readonly string ServiceUrl = "service_url";
        public static readonly string StorageOperation = "storage.operation";
        
        public static readonly string TokenServiceEndpoint = "agents.token_service.endpoint";

        public static readonly string UserId = "user.id";
        
        public static readonly string ViewId = "view.id";
    }
}
