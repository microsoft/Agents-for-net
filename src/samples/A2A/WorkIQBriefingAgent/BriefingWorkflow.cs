// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WorkIQBriefingAgent;

internal sealed class BriefingWorkflow(IMailSender mailSender, TimeProvider clock)
{
    public void UpdateDraft(BriefingState state, string briefing)
    {
        RequireEditable(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(briefing);
        state.Briefing = briefing;
        state.Revision++;
        ClearApproval(state);
    }

    public void SetRecipients(BriefingState state, string recipients)
    {
        RequireEditable(state);
        string[] addresses = recipients
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (addresses.Length is 0 or > 10 || addresses.Any(address =>
            address.Contains('\r') || address.Contains('\n')
            || !MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Enter up to ten mailbox addresses separated by commas or semicolons.", nameof(recipients));
        }

        // Recipients come only from the user's command, never from generated Work IQ content.
        state.Recipients = addresses;
        ClearApproval(state);
    }

    public string CreateApproval(BriefingState state)
    {
        RequireEditable(state);
        if (string.IsNullOrWhiteSpace(state.Briefing) || state.Recipients.Length == 0)
        {
            throw new InvalidOperationException("A briefing and explicit email recipients are required before approval.");
        }

        state.ApprovalCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
        state.ApprovalFingerprint = Fingerprint(state);
        state.ApprovalExpiresAt = clock.GetUtcNow().AddMinutes(15);
        return state.ApprovalCode;
    }

    public void Cancel(BriefingState state)
    {
        ClearApproval(state);
        state.Delivery = state.Delivery switch
        {
            MailDeliveryState.NotSent => MailDeliveryState.Cancelled,
            MailDeliveryState.Sending => MailDeliveryState.Unknown,
            _ => state.Delivery,
        };
    }

    public async Task<MailDeliveryState> SendAsync(
        BriefingState state,
        string ownerId,
        string approvalCode,
        string accessToken,
        Func<CancellationToken, Task> checkpoint,
        CancellationToken cancellationToken)
    {
        RequireOwner(state, ownerId);
        if (state.Delivery != MailDeliveryState.NotSent)
        {
            // A duplicate confirmation may read an in-flight marker. It must not issue another send.
            return state.Delivery == MailDeliveryState.Sending ? MailDeliveryState.Unknown : state.Delivery;
        }

        if (string.IsNullOrEmpty(state.ApprovalCode)
            || !string.Equals(state.ApprovalCode, approvalCode, StringComparison.OrdinalIgnoreCase)
            || state.ApprovalExpiresAt is not DateTimeOffset expiresAt
            || expiresAt <= clock.GetUtcNow()
            || state.ApprovalFingerprint != Fingerprint(state))
        {
            throw new InvalidOperationException("Approval is missing, expired, or for a different email. Review the current draft again.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var mail = new BriefingMail([.. state.Recipients], state.Subject, state.Briefing);
        ClearApproval(state);
        state.Delivery = MailDeliveryState.Sending;

        // Save BEFORE the external side effect. SDK optimistic concurrency makes a stale approval
        // fail here, before Graph is called. A crash afterwards leaves a marker, not permission to retry.
        await checkpoint(cancellationToken);
        try
        {
            state.Delivery = await mailSender.SendAsync(mail, accessToken, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            // Losing the response does not prove that Exchange rejected the email.
            state.Delivery = MailDeliveryState.Unknown;
        }

        // Preserve the known outcome even if the caller disconnected while Graph was responding.
        using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await checkpoint(saveTimeout.Token);
        return state.Delivery;
    }

    public static void RequireOwner(BriefingState state, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || !string.Equals(state.OwnerId, ownerId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("This briefing belongs to a different signed-in user.");
        }
    }

    private static void RequireEditable(BriefingState state)
    {
        if (state.Delivery != MailDeliveryState.NotSent)
        {
            throw new InvalidOperationException("This email is no longer editable. Check its send outcome before starting another task.");
        }
    }

    private static string Fingerprint(BriefingState state)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new BriefingMail(state.Recipients, state.Subject, state.Briefing))));

    private static void ClearApproval(BriefingState state)
    {
        state.ApprovalCode = null;
        state.ApprovalFingerprint = null;
        state.ApprovalExpiresAt = null;
    }
}
