// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace WorkIQBriefingAgent;

internal sealed class BriefingState
{
    public string OwnerId { get; set; } = string.Empty;
    public string Request { get; set; } = string.Empty;
    public string Briefing { get; set; } = string.Empty;
    public string Subject { get; set; } = "Meeting briefing";
    public string[] Recipients { get; set; } = [];
    public int Revision { get; set; }

    // These identify the delegated Work IQ conversation, not the outer A2A task.
    public string? WorkIqTaskId { get; set; }
    public string? WorkIqContextId { get; set; }
    public string? WorkIqStatus { get; set; }
    public bool WorkIqNeedsInput { get; set; }

    public string? ApprovalCode { get; set; }
    public string? ApprovalFingerprint { get; set; }
    public DateTimeOffset? ApprovalExpiresAt { get; set; }
    public MailDeliveryState Delivery { get; set; }
}

internal enum MailDeliveryState
{
    NotSent,
    Sending,
    Accepted,
    Rejected,
    Unknown,
    Cancelled,
}
