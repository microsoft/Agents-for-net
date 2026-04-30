// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    public class ActionPayload : SlackModel
    {
        public string type { get; set; }
        public string channel { get; set; }
        public object message { get; set; }
        public object actions { get; set; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
