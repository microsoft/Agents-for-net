using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class SlackEvent
    {
        public string Type { get; set; }
        public string User { get; set; }
        public string Ts { get; set; }
        public string ClientMsgId { get; set; }
        public string Text { get; set; }
        public string Team { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string EventTs { get; set; } = string.Empty;
        public string ChannelType { get; set; } = string.Empty;

        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
