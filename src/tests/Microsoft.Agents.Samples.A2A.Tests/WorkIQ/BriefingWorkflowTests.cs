extern alias WorkIQBriefingSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WorkIQBriefingSample::WorkIQBriefingAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class BriefingWorkflowTests
{
    [Fact]
    public async Task Send_RequiresApprovalOfTheExactEmail()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SendAsync(state, "tenant:user", "unapproved", "graph-token", Checkpoint, CancellationToken.None));

        Assert.Empty(sender.Messages);
        Assert.Equal(MailDeliveryState.NotSent, state.Delivery);
    }

    [Fact]
    public async Task Send_CheckpointsBeforeSendingAndUsesTheReviewedRecipientsAndContent()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);
        var checkpoints = new List<MailDeliveryState>();

        var result = await workflow.SendAsync(state, "tenant:user", code, "graph-token", ct =>
        {
            checkpoints.Add(state.Delivery);
            if (state.Delivery == MailDeliveryState.Sending)
            {
                Assert.Null(state.ApprovalCode);
                Assert.Empty(sender.Messages);
            }
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(MailDeliveryState.Accepted, result);
        Assert.Equal([MailDeliveryState.Sending, MailDeliveryState.Accepted], checkpoints);
        var sent = Assert.Single(sender.Messages);
        Assert.Equal(["reviewer@example.com"], sent.Recipients);
        Assert.Equal("Meeting briefing", sent.Subject);
        Assert.Equal("# Contoso\nOpen commitment: send the proposal.", sent.Body);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangingDraftOrRecipients_InvalidatesThePreviousApproval(bool changeDraft)
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var previousCode = workflow.CreateApproval(state);
        if (changeDraft)
        {
            workflow.UpdateDraft(state, "Updated briefing");
        }
        else
        {
            workflow.SetRecipients(state, "someone-else@example.com");
        }

        var nextCode = workflow.CreateApproval(state);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SendAsync(state, "tenant:user", previousCode, "graph-token", Checkpoint, CancellationToken.None));

        Assert.NotEqual(previousCode, nextCode);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task ChangingPayloadWithoutRegeneratingApproval_CannotSend()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);
        state.Subject = "A different subject";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None));

        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task AnotherUser_CannotApproveTheTaskOwnersMail()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            workflow.SendAsync(state, "tenant:another-user", code, "graph-token", Checkpoint, CancellationToken.None));

        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredOrMissingApprovalDeadline_CannotSend(bool missingDeadline)
    {
        var sender = new RecordingMailSender();
        var clock = new TestClock();
        var workflow = new BriefingWorkflow(sender, clock);
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);
        clock.Now += TimeSpan.FromHours(1);
        if (missingDeadline)
        {
            state.ApprovalExpiresAt = null;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None));

        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task DuplicateApproval_DoesNotSendAnotherEmail()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);

        await workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None);
        var result = await workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None);

        Assert.Equal(MailDeliveryState.Accepted, result);
        Assert.Single(sender.Messages);
    }

    [Fact]
    public async Task FailedCheckpoint_PreventsTheExternalSend()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SendAsync(state, "tenant:user", code, "graph-token",
                ct => throw new InvalidOperationException("Concurrent state update."), CancellationToken.None));

        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task LostSendResponse_IsRecordedAsUnknownAndIsNotAutomaticallyRetried()
    {
        var sender = new RecordingMailSender { Failure = new HttpRequestException("Connection lost after request.") };
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);

        var result = await workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None);
        var repeated = await workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None);

        Assert.Equal(MailDeliveryState.Unknown, result);
        Assert.Equal(MailDeliveryState.Unknown, repeated);
        Assert.Single(sender.Messages);
    }

    [Fact]
    public async Task CancelledReview_CannotSendUsingItsPreviousApproval()
    {
        var sender = new RecordingMailSender();
        var workflow = new BriefingWorkflow(sender, new TestClock());
        var state = PrepareDraft(workflow);
        var code = workflow.CreateApproval(state);
        workflow.Cancel(state);

        var result = await workflow.SendAsync(state, "tenant:user", code, "graph-token", Checkpoint, CancellationToken.None);

        Assert.Equal(MailDeliveryState.Cancelled, result);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData("reviewer@example.com; not-an-address")]
    [InlineData("reviewer@example.com\r\nBcc: other@example.com")]
    [InlineData("")]
    public void InvalidRecipients_DoNotBecomeAnApprovedEmail(string recipients)
    {
        var workflow = new BriefingWorkflow(new RecordingMailSender(), new TestClock());
        var state = new BriefingState();

        Assert.Throws<ArgumentException>(() => workflow.SetRecipients(state, recipients));
        Assert.Empty(state.Recipients);
        Assert.Null(state.ApprovalCode);
    }

    private static BriefingState PrepareDraft(BriefingWorkflow workflow)
    {
        var state = new BriefingState { OwnerId = "tenant:user" };
        workflow.UpdateDraft(state, "# Contoso\nOpen commitment: send the proposal.");
        workflow.SetRecipients(state, "reviewer@example.com");
        return state;
    }

    private static Task Checkpoint(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-17T15:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingMailSender : IMailSender
    {
        public List<BriefingMail> Messages { get; } = [];
        public Exception? Failure { get; init; }

        public Task<MailDeliveryState> SendAsync(BriefingMail mail, string accessToken, CancellationToken cancellationToken)
        {
            Messages.Add(mail);
            return Failure is null
                ? Task.FromResult(MailDeliveryState.Accepted)
                : Task.FromException<MailDeliveryState>(Failure);
        }
    }
}
