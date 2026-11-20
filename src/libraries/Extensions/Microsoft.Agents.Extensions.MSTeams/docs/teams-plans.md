# Teams Plans (experimental)

`Microsoft.Agents.Extensions.MSTeams` provides native Plan output through
`ITeamsTurnContext.CreatePlan` or the static `TeamsAgentExtension.CreatePlan`
factory. The helper and its Core-derived `planInfo` entity are marked
`ExperimentalTeamsPlan`; explicitly opt in only where used.
The [deterministic sample](../../../../samples/Teams/PlansAgent/README.md) demonstrates
progress, streamed completion, stop/error commands, and manual Teams validation
without requiring an AI resource.

A Plan is structured progress for the original user prompt. It does not create
or execute a workflow, infer progress, provide approval, or cancel application
work. The application performs retrieval, model calls, and other work, then
reports what actually happened.

## Lifecycle

| Method | Output | Result |
| --- | --- | --- |
| `turnContext.CreatePlan()` | None; captures the incoming activity's own ID and full conversation ID | `TeamsPlan` |
| `StartAsync(steps)` | POST typing with `planType: "start"` | No chat message result |
| `UpdateAsync(changedSteps)` | POST typing with `planType: "continue"` | No chat message result |
| `StopAsync()` | POST typing with `planType: "stop"` | Does not cancel application work |
| `ErrorAsync()` | POST typing with `planType: "error"` | Distinct from marking one step as error |
| `CompleteAsync(textOrActivity)` | POST message with `planType: "complete"` | Core `ResourceResponse` |
| `CompleteStreamingAsync()` | Ends the current writer using a final completion POST | `StreamingResponseResult`; delivery failures propagate |

`Sent` is raised after successful delivery. Its `Response` is null for typing
acknowledgements and contains the Core response for completion.

All Plan sends, including progress and streamed completion, traverse the turn's
`OnSendActivities` pipeline. Hooks can transform, replace, add, or suppress
activities, and `TranscriptLoggerMiddleware` observes the outgoing Plan
activities. A Teams-owned hook restores the saved original-prompt binding before
delivery; outer hooks can observe that binding after awaiting `next`.
If a hook suppresses delivery without returning a response, `Sent` is not raised
and `CompleteAsync` returns an empty `ResourceResponse`; a writer ending result
alone is not proof of remote Plan acknowledgement.

The helper uses `TeamsTurnContext` and the existing context send APIs. A Core
`ITurnContext` passed to the static factory is wrapped by the extension; custom
contexts need only support the ordinary send and send-hook APIs. There is no new
Builder send capability. Ordinary Plan output goes through the adapter's normal
reply-to-activity POST. Only fresh chat-thread completion is intercepted for the
new `replyChains` REST endpoint. The interceptor is registered at send time so
previously registered hooks can inspect or suppress that request. Its private
per-send scope does not capture later sends or another Plan's binding.
Hosted `Stream` / `ExpectReplies` delivery still uses the normal adapter.

## Application-controlled steps

In a Teams handler with an `ITeamsTurnContext`, create the helper directly from
the turn context:

```csharp
#pragma warning disable ExperimentalTeamsPlan
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Teams.Apps.Schema.Entities;

var plan = turnContext.CreatePlan();
PlanStep[] steps =
[
    new() { Id = "lookup", Version = 1, Title = "Find the reference", Status = PlanStepStatuses.Active },
    new() { Id = "answer", Version = 1, Title = "Write the answer", Status = PlanStepStatuses.Proposed }
];
await plan.StartAsync(steps, cancellationToken);

// Perform the actual lookup before reporting it complete.
steps[0].Status = PlanStepStatuses.Completed;
steps[0].Version++;
steps[1].Status = PlanStepStatuses.Active;
steps[1].Version++;
await plan.UpdateAsync(steps, cancellationToken);
```

For a Core `ITurnContext`, use `TeamsAgentExtension.CreatePlan(turnContext)`
instead. This static factory does not require an extension instance.

Each step has a stable `Id` and its own application-managed integer `Version`.
Increment that step's version for every status, title, description, or source
change. Statuses are `proposed`, `active`, `completed`, `blocked`, and `error`.
A proposed step is not an approval mechanism.

An optional `Sources` list uses upstream `StepSource` values with a title, URL,
and optional citation icon. Include only sources actually used by the
application. Updates contain only the supplied steps; omitted steps are not
removed or reset. Marking all steps completed does not send the answer.

For ordinary completion, pass a Core message or text to `CompleteAsync`. A Core
message can already contain cards, AI metadata, citations, or a completion
entity; content is preserved without mutating the caller's activity. A typing
activity, a targeted message, or a non-complete Plan operation is rejected.
Compose the message before calling `CompleteAsync`; the helper supplies the
completion entity and saved prompt binding.

The extension reuses upstream non-activity Plan step/status/source/binding types.
It never uses upstream activity DTOs or the upstream `Plan` sender.

## Streamed completion

Compose text and metadata with the existing writer APIs, then use the Plan's
ending method instead of the default `EndStreamAsync`:

```csharp
await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Writing...", cancellationToken);

// Queue text as the application produces it.
turnContext.StreamingResponse.QueueTextChunk(answerText);
await plan.CompleteStreamingAsync(cancellationToken);
```

Use `AddAttachment`, `AddCitation`, and the label properties for final content.
No new use of `IStreamingResponse.FinalMessage` is required. The actual composed
final activity retains those fields, feedback metadata, and the original
prompt/thread binding.

Builder's [custom final-delivery overload](../../../../../docs/streaming-response-sequence-diagram.md#custom-final-delivery)
is a general-purpose capability: only final delivery is replaceable. Ordinary
send/update hooks cannot reliably distinguish a timeout checkpoint from a final
message after streaming has been disabled. The overload avoids that ambiguity
without adding Teams or Plan branches to Builder. The original streaming API,
channel timeouts, intermediate delivery, and non-Plan recovery are unchanged.

A Teams streaming timeout on an intermediate send can still produce the
writer's normal PUT checkpoint. **That PUT does not complete a Plan.** The final
completion is a separate POST. If the streamed final POST itself receives the
recognized `ContentStreamNotAllowed` / streaming-timeout rejection, MSTeams makes
one ordinary final POST without streaming identifiers, retaining the composed
content. It does not retry arbitrary errors or lost connections.

HTTP 409 / `PlanConflict` propagates through the SDK error model. Reconcile
service/application state rather than retrying blindly. Repeating
`CompleteStreamingAsync` on an ended writer returns `AlreadyEnded` without a
second completion. Explicitly calling `CompleteAsync` again is a new request,
not an idempotent retry.

Applications must stop/cancel their own work before reporting stop/error and
must not complete failed work in a `finally` block. The sample uses explicit
stop/error commands and the turn's cancellation token, without a separate work
timeout.

## Upstream comparison and limits

The [wire-comparison tests](../../../../tests/Microsoft.Agents.Extensions.MSTeams.Tests/TeamsPlanParityTests.cs)
execute the actual referenced `Microsoft.Teams.Apps` Plan helper and streaming
writer against an in-memory HTTP service, then compare their serialized output
with the native helper. They cover lifecycle operations, step revisions,
descriptions, sources/icons, prompt binding, conversation/chat completion routes,
and informative/content/final streaming metadata.

This is **Plan-contract equivalence, not byte-for-byte writer equivalence**.
Native ordinary sends use the existing adapter's reply-to-activity POST, while
the upstream Plan helper uses a conversation POST. Native destinations are
asserted independently; endpoint equality is not claimed. The Plan JSON contract
and original-prompt binding are compared with the real upstream implementation.
Core includes its normal addressing envelope and empty collections, adds its
shared final `streamResult`, and may order entities differently. The upstream
writer also duplicates intermediate `streaminfo` fields into legacy channel
data; the Core writer uses the canonical entity. These known differences are
explicitly checked/normalized in the comparison, not silently ignored.

The existing Core writer's scheduling and initial-send routing are retained.
The tests do not establish parity with the upstream writer's first-chunk
reply-chain placement or prove live Teams rendering. General reply-placement
integration is separate from the Plan completion binding described here.
On a service timeout, upstream requires the application to send a separate
completion; the native helper performs that single final POST for the recognized
rejection. Neither retries conflicts or ambiguous failures.

## Persisting the original binding

Serialize `plan.Options` with `ProtocolJsonSerializer` alongside the application's
durable job state and step versions. It contains the full conversation ID,
original prompt's own activity ID, and optional chat/root reply-chain IDs.
Do not substitute an inbound `ReplyToId`, a response ID, an encrypted chat ID,
or an ID inferred by removing a conversation suffix.

For delayed work, use the existing authenticated proactive/continuation
facilities to create a fresh turn for the saved conversation. Supply the saved
`Microsoft.Teams.Apps.PlanOptions` to `freshContext.CreatePlan(savedOptions)` for
an `ITeamsTurnContext`, or to the static
`TeamsAgentExtension.CreatePlan(freshContext, savedOptions)` for a Core
`ITurnContext`.
The helper validates the conversation and uses that fresh turn's service-scoped
Core connector transport. The options alone do not restore identity, a service
URL, credentials, workflow state, or completed steps.

Ordinary Plan sends retain the full conversation ID and original prompt binding
through the adapter's reply-to-activity route. A fresh completion in a chat reply
chain uses the saved chat/root REST route; a channel completion keeps the full
conversation ID, including its thread suffix. An established stream completes
through the adapter using its existing stream ID, without a fresh reply-chain
message. This does not enable a general threading API. Targeted Plans are unsupported.

The presence of chat/root metadata alone does not mean that a prompt is inside
a thread. A top-level chat message whose own ID equals the root uses the normal
adapter path, not the reply-chain REST endpoint.
Card-button messages, reactions, and message actions use the message they act on
when determining placement, while the Plan's prompt binding remains the incoming
activity's own ID.

Never persist a live Plan, turn context, HTTP client, access token, or API client.
