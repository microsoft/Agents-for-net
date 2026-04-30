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
/// the AdditionalProperties dictionary.</remarks>
public class SlackChannelData
{
    /// <summary>
    /// The event envelope from slack
    /// </summary>
    /// <remarks>
    /// "SlackMessage" is what ABS named this property even though it contains the entire event envelope from slack.  The message
    /// would be in EventEnvelope.event_content.text or EventEnvelop.Get("event").
    /// </remarks>
    [JsonPropertyName("SlackMessage")]
    public EventEnvelope Envelope { get; set; }

    /// <summary>
    /// The Action (Interactive Message) payload from slack.
    /// </summary>
    [JsonPropertyName("Payload")]
    public ActionPayload Payload { get; set; }

    /// <summary>
    /// Gets or sets the API authentication token used to authorize response by the agent using <see cref="SlackAgentExtension.CallAsync(Builder.ITurnContext, string, object?, string, System.Threading.CancellationToken)"/> 
    /// or <see cref="SlackApi"/>.
    /// </summary>
    /// <remarks>The API token should be kept secure and not shared publicly. Changing this value may affect
    /// the ability to access protected resources.</remarks>
    public string ApiToken { get; set; }

    public string Channel => Envelope != null 
        ? Envelope.Get<string>("event.channel") 
        : Payload?.Get<string>("channel");

    public string ThreadTs => Envelope != null 
        ? (Envelope.Get<string>("event.thread_ts") ?? Envelope.Get<string>("event.ts")) 
        : (Payload?.Get<string>("message.thread_ts") ?? Payload?.Get<string>("message.ts"));

    /// <summary>
    /// Gets or sets the collection of additional properties not mapped to class members during JSON serialization or
    /// deserialization.
    /// </summary>
    /// <remarks>This property stores any extra JSON fields encountered during deserialization that do not
    /// correspond to a property on the class. When serializing, any key-value pairs in this dictionary will be included
    /// as additional fields in the JSON output. This enables flexible handling of dynamic or unknown data in JSON
    /// payloads.</remarks>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();
}
