using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class SlackMessage
    {
        public string Token { get; set; }

        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("context_team_id")]
        public string ContextTeamId { get; set; }

        [JsonPropertyName("context_enterprise_id")]
        public string ContextEnterpriseId { get; set; }

        [JsonPropertyName("api_app_id")]
        public string ApiAppId { get; set; }

        public SlackEvent Event { get; set; }

       public string Type { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("event_time")]
        public int EventTime { get; set; }

        [JsonPropertyName("is_ext_shared_channel")]
        public bool IsExtSharedChannel { get; set; }

        [JsonPropertyName("event_context")]
        public string EventContext { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
