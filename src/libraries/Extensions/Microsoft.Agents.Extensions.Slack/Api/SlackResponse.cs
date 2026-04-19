// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Represents a response from the Slack Web API.
/// </summary>
/// <remarks>
/// <para>
/// Named properties cover the fields common to every Slack API response. Any additional
/// fields returned by a specific method are accessible via the <see cref="AdditionalProperties"/>
/// dictionary or via dot-notation path using <see cref="SlackModel.Get{T}"/> and
/// <see cref="SlackModel.TryGet{T}"/>.
/// </para>
/// <para>
/// <code>
/// SlackResponse r = await slackApi.CallAsync("chat.postMessage", options, token);
/// string channel = r.Get&lt;string&gt;("channel");
/// string msgTs   = r.Get&lt;string&gt;("message.ts");
/// </code>
/// </para>
/// </remarks>
public class SlackResponse : SlackModel
{
    /// <summary>Whether the API call succeeded.</summary>
    public bool ok { get; set; }

    /// <summary>Error code returned by Slack when <see cref="ok"/> is <see langword="false"/>.</summary>
    public string? error { get; set; }

    /// <summary>Warning code returned alongside a successful response.</summary>
    public string? warning { get; set; }

    /// <summary>Message timestamp, present on methods that create or update messages.</summary>
    public string? ts { get; set; }

    /// <summary>Gets or sets the metadata associated with the response.</summary>
    public string? response_metadata { get; set; }

    /// <summary>Catch-all for any response fields not explicitly modelled above.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
