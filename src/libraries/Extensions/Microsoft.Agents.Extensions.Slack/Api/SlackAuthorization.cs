// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Represents one authorization entry in a Slack Events API callback envelope.
/// See https://docs.slack.dev/apis/events-api/#callback-field
/// </summary>
public class SlackAuthorization
{
    /// <summary>Enterprise org ID, or null for non-Enterprise Grid installations.</summary>
    public string enterprise_id { get; set; }

    /// <summary>Workspace ID.</summary>
    public string team_id { get; set; }

    /// <summary>User ID that determines visibility scope for the installation.</summary>
    public string user_id { get; set; }

    /// <summary>Whether this authorization is for a bot user.</summary>
    public bool is_bot { get; set; }

    /// <summary>Whether this is an Enterprise Grid org-wide installation.</summary>
    public bool is_enterprise_install { get; set; }
}
