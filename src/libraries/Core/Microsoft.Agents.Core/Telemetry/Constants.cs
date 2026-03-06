using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class Constants
    {
        public static readonly string SourceName = "Microsoft.Agents.Builder";
        public static readonly string SourceVersion = "1.0.0";

        // Common attributes

        public static readonly string AttrActivityDeliveryMode = "activity.deliveryMode";
        public static readonly string AttrActivityChannelId = "activity.channelId";
        public static readonly string AttrActivityId = "activity.id";
        public static readonly string AttrActivityCount = "activities.count";
        public static readonly string AttrActivityType = "activity.type";
        public static readonly string AttrAgenticUserId = "agentic.userId";
        public static readonly string AttrAgenticInstanceId = "agentic.instanceId";
        public static readonly string AttrAttachmentId = "attachment.id";
        public static readonly string AttrAuthScopes = "auth.scopes";
        public static readonly string AttrAuthType = "auth.method";

        public static readonly string AttrConversationId = "conversation.id";
        public static readonly string AttrIsAgenticRequest = "isAgenticRequest";
        public static readonly string AttrNumKeys = "keys.num";
        public static readonly string AttrRouteIsInvoke = "route.isInvoke";
        public static readonly string AttrRouteIsAgentic = "route.isAgentic";

        public static readonly string AttrServiceUrl = "serviceUrl";

        public static readonly string UNKNOWN = "unknown";
    }
}
