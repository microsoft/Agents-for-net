// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.A2A.JsonRpc;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// This is the structure of the JSON object found in the data field of each Server-Sent Event 
/// sent by the server for a message/stream request or tasks/resubscribe request.
/// </summary>
public sealed class SendStreamingMessageResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; } = "2.0";

    /// <summary>
    /// Matches the id from the originating tasks/sendSubscribe or tasks/resubscribe TaskSendParams.
    /// </summary>
    [JsonPropertyName("id")]
    public required JsonRpcId Id { get; set; }

    /// <summary>
    /// The event payload.
    /// </summary>
    /// <code>
    /// Either:
    /// <see cref="Message"/>
    /// OR <see cref="Task"/>
    /// OR <see cref="TaskStatusUpdateEvent"/>
    /// OR <see cref="TaskArtifactUpdateEvent"/>
    /// </code>
    [JsonPropertyName("result")]
    public required object Result { get; set; }
}