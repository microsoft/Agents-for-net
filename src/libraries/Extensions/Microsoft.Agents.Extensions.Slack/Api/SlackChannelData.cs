// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Represents data associated with a slack channel event as provided by Azure Bot Service in the Activity.ChannelData property.
/// </summary>
/// <remarks>This class is typically used to deserialize incoming slack event payloads and to provide access to
/// both standard and custom properties received from slack. Additional properties not explicitly defined are stored in
/// the Properties dictionary.</remarks>
public class SlackChannelData
{
    /// <summary>
    /// Gets or sets the envelope containing the slack message event data.
    /// </summary>
    [JsonPropertyName("SlackMessage")]
    public EventEnvelope EventEnvelope { get; set; }

    /// <summary>
    /// Gets or sets the API authentication token used to authorize response by the agent using <see cref="SlackAgentExtension.CallAsync(Builder.ITurnContext, string, object?, string, System.Threading.CancellationToken)"/> 
    /// or <see cref="SlackApi"/>.
    /// </summary>
    /// <remarks>The API token should be kept secure and not shared publicly. Changing this value may affect
    /// the ability to access protected resources.</remarks>
    public string ApiToken { get; set; }

    /// <summary>
    /// Gets or sets the collection of additional properties not mapped to class members during JSON serialization or
    /// deserialization.
    /// </summary>
    /// <remarks>This property stores any extra JSON fields encountered during deserialization that do not
    /// correspond to a property on the class. When serializing, any key-value pairs in this dictionary will be included
    /// as additional fields in the JSON output. This enables flexible handling of dynamic or unknown data in JSON
    /// payloads.</remarks>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
}
