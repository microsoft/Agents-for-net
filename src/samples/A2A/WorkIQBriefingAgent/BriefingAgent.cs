// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ActivitySource = System.Diagnostics.ActivitySource;

namespace WorkIQBriefingAgent;

[Agent(name: "Work IQ Briefing Agent", description: "Research, review, and explicitly approve a real briefing email.")]
[A2AExtension]
[AgentInterface(A2AAgentTransportProtocol.JsonRpc, "/a2a")]
internal partial class BriefingAgent : AgentApplication
{
    private static readonly ActivitySource Source = new("WorkIQBriefingAgent");
    private readonly BriefingWorkflow _workflow;
    private readonly WorkIqClient _workIq;
    private readonly ILogger<BriefingAgent> _logger;

    public BriefingAgent(AgentApplicationOptions options, BriefingWorkflow workflow, WorkIqClient workIq, ILogger<BriefingAgent> logger)
        : base(options)
    {
        _workflow = workflow;
        _workIq = workIq;
        _logger = logger;
        if (options.UserAuthorization is not null)
        {
            UserAuthorization.OnUserSignInFailure((context, state, handler, response, activity, ct) =>
                context.SendActivityAsync(new Activity
                {
                    Type = ActivityTypes.EndOfConversation,
                    Code = EndOfConversationCodes.Error,
                    Text = $"Authorization for '{handler}' failed. Check the sample's delegated permissions, consent, and OBO configuration.",
                }, cancellationToken: ct));
        }
    }

    [A2ASkill(name: "Meeting briefing", tags: "briefing,research,email", examples: "Prepare a briefing for my Contoso meeting", autoSigninHandlers: "workiq,mail")]
    private async Task OnBriefingAsync(IA2ATurnContext context, ITurnState turnState, CancellationToken cancellationToken)
    {
        var owner = SignedInUser.GetId(new ClaimsPrincipal(context.Identity));
        // A2A maps its task ID to Conversation.Id, so this state belongs to this task, not the entire user.
        var state = turnState.Conversation.GetValue("briefing", () => new BriefingState { OwnerId = owner });
        BriefingWorkflow.RequireOwner(state, owner);
        var input = context.Activity.Text?.Trim() ?? string.Empty;
        using var operation = Source.StartActivity("briefing.turn");
        operation?.SetTag("a2a.task.id", context.Activity.Conversation.Id);
        operation?.SetTag("briefing.revision", state.Revision);

        try
        {
            if (input.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                await CancelWorkAsync(context, turnState, state, cancellationToken);
                await CompleteAsync(context, "Task cancelled. Cancellation cannot recall an email whose send already started.",
                    EndOfConversationCodes.UserCancelled, cancellationToken);
                return;
            }

            if (input.Equals("send", StringComparison.OrdinalIgnoreCase) || input.StartsWith("send ", StringComparison.OrdinalIgnoreCase))
            {
                // This token targets Graph. The Work IQ token must never be used to call sendMail.
                var mailToken = await UserAuthorization.GetTurnTokenAsync(context, "mail", cancellationToken);
                var outcome = await _workflow.SendAsync(state, owner, input.Length > 4 ? input[5..].Trim() : string.Empty,
                    mailToken, ct => turnState.Conversation.SaveChangesAsync(context, cancellationToken: ct), cancellationToken);
                _logger.LogInformation("Briefing task {TaskId} send outcome: {Outcome}.", context.Activity.Conversation.Id, outcome);
                var message = outcome switch
                {
                    MailDeliveryState.Accepted => "Microsoft Graph accepted the email for sending. Check Sent Items and confirm receipt; delivery is not guaranteed.",
                    MailDeliveryState.Rejected => "Graph rejected the send. No automatic retry was made. Check permissions and input before starting a new task.",
                    MailDeliveryState.Cancelled => "The task was cancelled; the sample did not initiate a send.",
                    _ => "The send outcome is unknown. Check Sent Items and the recipient before retrying. This task will not send the email again.",
                };
                await CompleteAsync(context, message,
                    outcome == MailDeliveryState.Accepted ? EndOfConversationCodes.CompletedSuccessfully : EndOfConversationCodes.Error,
                    cancellationToken);
                return;
            }

            if (input.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
            {
                _workflow.SetRecipients(state, input[3..]);
                await ShowReviewAsync(context, state, cancellationToken);
                return;
            }

            if (input.Equals("review", StringComparison.OrdinalIgnoreCase))
            {
                await ShowReviewAsync(context, state, cancellationToken);
                return;
            }

            if (input.Length == 0 || input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await AskAsync(context, "Describe the meeting or customer to research. Then use 'to <mailbox>', 'revise <instructions>', 'review', 'send <approval-code>', or 'cancel'.", cancellationToken);
                return;
            }

            bool resume = input.Equals("resume", StringComparison.OrdinalIgnoreCase);
            if (resume && state.WorkIqTaskId is null)
            {
                await AskAsync(context, "There is no known Work IQ task to resume. Describe the briefing you want.", cancellationToken);
                return;
            }

            if (string.IsNullOrEmpty(state.Request))
            {
                state.Request = input;
            }
            await ResearchAsync(context, turnState, state, input, resume, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // Validation errors keep the conversation open; they never fall through into a send.
            await AskAsync(context, exception.Message, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or A2AException or JsonException or IOException)
        {
            // Do not log prompts, mailbox content, tokens, or remote response bodies.
            _logger.LogWarning("Briefing task {TaskId} failed ({ErrorType}).", context.Activity.Conversation.Id, exception.GetType().Name);
            await AskAsync(context, "The service request failed. Check consent and configuration. Use 'resume' to observe a known Work IQ task without resubmitting it, or 'cancel'.", cancellationToken);
        }
    }

    private async Task ResearchAsync(IA2ATurnContext context, ITurnState turnState, BriefingState state,
        string input, bool resume, CancellationToken cancellationToken)
    {
        if (state.Delivery != MailDeliveryState.NotSent)
        {
            throw new InvalidOperationException("This task already attempted a send. Check its outcome before starting another task.");
        }

        // Invalidate approval before making any asynchronous revision request.
        state.ApprovalCode = null;
        state.ApprovalFingerprint = null;
        state.ApprovalExpiresAt = null;
        await turnState.Conversation.SaveChangesAsync(context, cancellationToken: cancellationToken);
        var workIqToken = await UserAuthorization.GetTurnTokenAsync(context, "workiq", cancellationToken);
        using var research = Source.StartActivity("briefing.research");
        string? lastStatus = null;

        async Task ProgressAsync(WorkIqProgress update, CancellationToken ct)
        {
            bool changed = state.WorkIqTaskId != update.TaskId || state.WorkIqContextId != update.ContextId;
            state.WorkIqTaskId = update.TaskId ?? state.WorkIqTaskId;
            state.WorkIqContextId = update.ContextId ?? state.WorkIqContextId;
            state.WorkIqStatus = update.Status;
            research?.SetTag("workiq.task.id", state.WorkIqTaskId);
            if (changed)
            {
                // Persist correlation as soon as Work IQ supplies it, so a later turn can observe/cancel that task.
                await turnState.Conversation.SaveChangesAsync(context, cancellationToken: ct);
            }
            if (lastStatus != update.Status)
            {
                lastStatus = update.Status;
                await context.StreamingResponse.QueueInformativeUpdateAsync($"Work IQ: {update.Status}", ct);
            }
            if (update.TextDelta.Length > 0)
            {
                context.StreamingResponse.QueueTextChunk(update.TextDelta);
            }
        }

        WorkIqReply reply;
        try
        {
            context.StreamingResponse.EnableGeneratedByAILabel = true;
            await context.StreamingResponse.QueueInformativeUpdateAsync("Researching the briefing with Work IQ...", cancellationToken);
            var prompt = "Research and draft a concise meeting briefing with recent discussions, commitments, risks, and source links when available. "
                + "Research and drafting only: do not send email or make changes to Microsoft 365 resources. "
                + "Return the briefing text, not instructions for sending it.\n\nUser request:\n" + input;
            reply = resume
                ? await _workIq.ObserveAsync(state.WorkIqTaskId!, workIqToken, ProgressAsync, cancellationToken)
                : await _workIq.RunAsync(prompt, state.WorkIqContextId,
                    state.WorkIqNeedsInput ? state.WorkIqTaskId : null, workIqToken, ProgressAsync, cancellationToken);
        }
        finally
        {
            await context.StreamingResponse.EndStreamAsync(cancellationToken);
        }

        state.WorkIqTaskId = reply.TaskId;
        state.WorkIqContextId = reply.ContextId;
        state.WorkIqNeedsInput = reply.RequiresInput;
        if (reply.RequiresInput)
        {
            await AskAsync(context, reply.Text, cancellationToken);
            return;
        }

        _workflow.UpdateDraft(state, reply.Text);
        await ShowReviewAsync(context, state, cancellationToken);
    }

    private async Task ShowReviewAsync(IA2ATurnContext context, BriefingState state, CancellationToken cancellationToken)
    {
        if (state.WorkIqNeedsInput || string.IsNullOrWhiteSpace(state.Briefing))
        {
            await AskAsync(context, "First describe the briefing you need, or answer Work IQ's clarification question.", cancellationToken);
            return;
        }

        // Use the outer task IDs here. Work IQ's IDs belong only to the delegated task.
        await context.Client.EventQueue.EnqueueArtifactUpdateAsync(new TaskArtifactUpdateEvent
        {
            TaskId = context.Client.RequestContext.TaskId,
            ContextId = context.Client.RequestContext.ContextId,
            LastChunk = true,
            Artifact = new Artifact
            {
                ArtifactId = "briefing",
                Name = "briefing.md",
                Parts = [new Part { Text = state.Briefing, MediaType = "text/markdown" }],
            },
        }, cancellationToken);

        if (state.Recipients.Length == 0)
        {
            await AskAsync(context, $"Briefing revision {state.Revision}:\n\n{state.Briefing}\n\n"
                + "Enter 'to <test-mailbox@example.com>' to choose recipients, 'revise <instructions>' to revise, or 'cancel'.",
                cancellationToken);
            return;
        }

        var approval = _workflow.CreateApproval(state);
        await AskAsync(context, $"Review revision {state.Revision}. This sends a REAL email from your signed-in mailbox.\n"
            + $"To: {string.Join("; ", state.Recipients)}\nSubject: {state.Subject}\n\n{state.Briefing}\n\n"
            + $"Approve this exact email within 15 minutes by replying: send {approval}\n"
            + "Use 'revise <instructions>' or 'to <mailbox>' to change it, 'review' for a fresh approval, or 'cancel'.",
            cancellationToken);
    }

    [EndOfConversationRoute(autoSignInHandlers: "workiq")]
    private async Task OnCancelAsync(ITurnContext context, ITurnState turnState, CancellationToken cancellationToken)
    {
        var state = turnState.Conversation.GetValue<BriefingState>("briefing");
        if (state is not null)
        {
            BriefingWorkflow.RequireOwner(state, SignedInUser.GetId(new ClaimsPrincipal(context.Identity)));
            await CancelWorkAsync(context, turnState, state, cancellationToken);
        }
    }

    private async Task CancelWorkAsync(ITurnContext context, ITurnState turnState, BriefingState state, CancellationToken cancellationToken)
    {
        _workflow.Cancel(state);
        await turnState.Conversation.SaveChangesAsync(context, cancellationToken: cancellationToken);
        if (state.WorkIqTaskId is not null && state.WorkIqStatus is "Working" or "Submitted" or "InputRequired")
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var token = await UserAuthorization.GetTurnTokenAsync(context, "workiq", timeout.Token);
                await _workIq.CancelAsync(state.WorkIqTaskId, token, timeout.Token);
            }
            catch (Exception exception) when (exception is A2AException or HttpRequestException or OperationCanceledException)
            {
                _logger.LogWarning("Cancellation of Work IQ task {TaskId} could not be confirmed ({ErrorType}).",
                    state.WorkIqTaskId, exception.GetType().Name);
            }
        }
    }

    private static Task AskAsync(ITurnContext context, string text, CancellationToken cancellationToken)
        // Returning input-required ends this turn, but leaves the A2A task open for the next answer/approval.
        => context.SendActivityAsync(MessageFactory.Text(text, inputHint: InputHints.ExpectingInput), cancellationToken);

    private static Task CompleteAsync(ITurnContext context, string text, string code, CancellationToken cancellationToken)
        => context.SendActivityAsync(new Activity { Type = ActivityTypes.EndOfConversation, Code = code, Text = text },
            cancellationToken: cancellationToken);
}
