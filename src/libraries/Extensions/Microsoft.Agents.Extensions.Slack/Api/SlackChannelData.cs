// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

public class SlackChannelData
{
    public SlackMessage? SlackMessage { get; set; }

    public string ApiToken { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
}
