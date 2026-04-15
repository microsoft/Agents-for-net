using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class SlackChannelData
    {
        public SlackMessage SlackMessage { get; set; }

        public string ApiToken { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
