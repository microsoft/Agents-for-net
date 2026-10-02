# StreamingResponse Sequence Diagram

Shows how `StreamingResponse` delivers chunked intermediate messages on a timer interval, giving the UX of a streamed message. When complete, a final `ActivityTypes.Message` is sent with the full text and optional attachments/citations.

## Participants

- **Agent** — The customer's business logic (or an LLM client producing chunks).
- **TurnContext** — Per-turn context; exposes `StreamingResponse` property.
- **StreamingResponse** — SDK-provided chunked message delivery (internal class).
- **Timer** — Internal `System.Threading.Timer` that fires at `Interval` ms.
- **Channel** — The upstream channel (Teams, M365 Copilot, WebChat, DirectLine, or a `DeliveryMode.Stream` transport).

## Channel Defaults

| Channel | Interval | StreamId | Notes |
|---------|----------|----------|-------|
| Teams | 1000ms | Assigned by first response | MUST use Activity.Id from first send |
| WebChat / DirectLine | 500ms | Random GUID | |
| DeliveryModes.Stream SSE | 100ms | Random GUID | |
| M365 Copilot with DeliveryModes.Stream | 100ms | Random GUID | Adds inactivity notices and a channel-specific timeout fallback |
| Other / ExpectReplies | N/A | N/A | Non-streaming; buffers text, sends single final message |

## Streaming Channel Flow

```mermaid
sequenceDiagram
    participant Agent
    participant TurnContext
    participant StreamingResponse
    participant Timer
    participant Channel

    Note over Agent,Channel: Stream lifecycle: QueueInformativeUpdate → QueueTextChunk* → EndStream

    %% Informative update (optional, starts stream)
    Agent->>TurnContext: turnContext.StreamingResponse
    TurnContext-->>Agent: IStreamingResponse

    Agent->>StreamingResponse: QueueInformativeUpdateAsync("Searching...")
    activate StreamingResponse
    StreamingResponse->>StreamingResponse: Create Typing Activity<br/>(StreamType=Informative, seq=1)
    StreamingResponse->>TurnContext: SendActivityAsync(informativeActivity)
    TurnContext->>Channel: Activity (Typing + StreamInfo)
    Channel-->>TurnContext: ResourceResponse (Activity.Id)
    StreamingResponse->>StreamingResponse: StreamId = response.Id (Teams)<br/>or pre-assigned GUID
    StreamingResponse->>Timer: Start(Interval)
    deactivate StreamingResponse

    %% Text chunks arrive from LLM
    loop LLM produces tokens
        Agent->>StreamingResponse: QueueTextChunk("partial text...")
        activate StreamingResponse
        StreamingResponse->>StreamingResponse: Message += text<br/>FormatCitationsResponse()<br/>_messageUpdated = true
        deactivate StreamingResponse
    end

    %% Timer fires — sends one intermediate message per interval
    loop Timer fires every Interval ms
        Timer->>StreamingResponse: SendIntermediateMessage (timer callback)
        activate StreamingResponse
        StreamingResponse->>StreamingResponse: QueueNextChunkActivity()<br/>(snapshot Message, seq++)
        StreamingResponse->>StreamingResponse: Dequeue one activity
        StreamingResponse->>TurnContext: SendActivityAsync(typingActivity)
        TurnContext->>Channel: Activity (Typing + StreamInfo.Streaming + full Message so far)
        Channel-->>TurnContext: ResourceResponse
        StreamingResponse->>Timer: Restart(Interval)
        deactivate StreamingResponse
    end

    %% Agent signals end of stream
    Agent->>StreamingResponse: EndStreamAsync()
    activate StreamingResponse
    StreamingResponse->>StreamingResponse: _ended = true
    StreamingResponse->>StreamingResponse: Wait for queue drain<br/>(WaitOne with EndStreamTimeout)

    Note over Timer,StreamingResponse: Timer continues sending<br/>queued items until empty

    Timer->>StreamingResponse: Final timer tick (queue empty + _ended)
    StreamingResponse->>StreamingResponse: _queueEmpty.Set()<br/>StopStream()

    StreamingResponse->>StreamingResponse: CreateFinalMessage()<br/>(StreamType=Final, full text,<br/>citations, AI entity, feedback loop)
    StreamingResponse->>TurnContext: SendActivityAsync(finalMessage)
    TurnContext->>Channel: Activity (Message + StreamInfo.Final)
    Channel-->>TurnContext: ResourceResponse
    StreamingResponse-->>Agent: StreamingResponseResult.Success
    deactivate StreamingResponse
```

## Non-Streaming Channel Flow

For channels that don't support intermediate messages (`IsStreamingChannel = false`), or `DeliveryMode.ExpectReplies`:

```mermaid
sequenceDiagram
    participant Agent
    participant StreamingResponse
    participant TurnContext
    participant Channel

    Agent->>StreamingResponse: QueueInformativeUpdateAsync("...")
    Note over StreamingResponse: IsStreamingChannel=false → no-op return

    loop LLM produces tokens
        Agent->>StreamingResponse: QueueTextChunk("text...")
        StreamingResponse->>StreamingResponse: Message += text (buffer only, no timer)
    end

    Agent->>StreamingResponse: EndStreamAsync()
    activate StreamingResponse
    StreamingResponse->>StreamingResponse: CreateFinalMessage() with buffered Message
    StreamingResponse->>TurnContext: SendActivityAsync(finalMessage)
    TurnContext->>Channel: Activity (Message, no StreamInfo entities)
    Channel-->>TurnContext: ResourceResponse
    StreamingResponse-->>Agent: StreamingResponseResult.Success
    deactivate StreamingResponse
```

## Error Handling: User Cancellation, Timeout & Teams Errors

```mermaid
sequenceDiagram
    participant StreamingResponse
    participant TurnContext
    participant Channel

    StreamingResponse->>TurnContext: SendActivityAsync(intermediateActivity)
    TurnContext->>Channel: Activity
    Channel-->>TurnContext: ErrorResponseException

    alt ContentStreamNotAllowed + timeout message
        StreamingResponse->>StreamingResponse: _streamTimedOut = true<br/>IsStreamingChannel = false<br/>send timeout checkpoint
        Note over StreamingResponse: EndStreamAsync updates the existing<br/>stream activity with the final message
    else ContentStreamNotAllowed (user canceled)
        StreamingResponse->>StreamingResponse: _userCanceled = true<br/>_canceled = true<br/>StopStream()
        Note over StreamingResponse: EndStreamAsync returns UserCancelled
    else BadArgument + "streaming api is not enabled"
        StreamingResponse->>StreamingResponse: IsStreamingChannel = false
        Note over StreamingResponse: Disables streaming for this turn<br/>Does NOT cancel
    else Other error
        StreamingResponse->>StreamingResponse: _canceled = true<br/>StopStream()
        Note over StreamingResponse: EndStreamAsync returns Error
    end
```

## M365 Copilot Keep-Alive and Timeout

M365 Copilot uses the normal `DeliveryMode.Stream` setup, plus safeguards for its client timeout behavior.

```mermaid
sequenceDiagram
    participant Timer
    participant StreamingResponse
    participant TurnContext
    participant M365Copilot as M365 Copilot

    loop One-shot timer callbacks
        Timer->>StreamingResponse: SendIntermediateMessage()
        StreamingResponse->>StreamingResponse: calculate elapsed time

        alt Total stream time reaches 1 minute 45 seconds
            StreamingResponse->>TurnContext: SendActivityAsync(timeout activity/activity pair)
            TurnContext->>M365Copilot: terminate visible stream
            StreamingResponse->>StreamingResponse: StopStream()<br/>IsStreamingChannel = false
            Note over StreamingResponse: Later EndStreamAsync sends the<br/>final content as a normal message
        else Queue empty and more than 35 seconds since<br/>stream start or the last informative update
            StreamingResponse->>StreamingResponse: queue last informative text<br/>or StreamingTakingTooLongMessage
            StreamingResponse->>TurnContext: SendActivityAsync(informative typing activity)
            TurnContext->>M365Copilot: keep-alive update
        else Normal buffered update
            StreamingResponse->>TurnContext: SendActivityAsync(intermediate activity)
            TurnContext->>M365Copilot: latest full text
        end
    end
```

## Key Implementation Details

- **Timer is one-shot, re-armed after each send** — prevents overlapping sends if `SendActivityAsync` takes longer than `Interval` (e.g., MSAL token refresh).
- **InitialDelay** (default 250ms) — used for the first `QueueTextChunk` if no informative was sent, enabling faster stream start.
- **Message accumulates** — each intermediate message sends the FULL text so far (not a delta). Channel displays latest as replacement.
- **StreamId** — Teams assigns it from first response; WebChat/DirectLine/DeliveryModes.Stream SSE uses a pre-generated GUID set on every outgoing `Activity.Id`.
- **EndStreamTimeout** — defaults to 2 minutes. If queue doesn't drain in time, returns `Timeout`.
- **ResetAsync** — allows reusing `StreamingResponse` for multiple streams in the same turn (waits for current stream to end first).
- **Client timeout fallback** — a Teams timeout error switches to non-streaming mode and updates the existing stream activity with the final message. The M365 Copilot proactive timeout path terminates the visible stream and later sends the final content as a normal message.
- **M365 keep-alive clock** — the 35-second check is measured from stream start or the last informative update while the queue is empty. Ordinary text chunks and intermediate text sends do not reset this clock.
- **No final payload means no send** — on a non-streaming channel, `EndStreamAsync` sends only when text, a `FinalMessage`, or prior updates exist.

## Related Source Files

| Component | Path |
|-----------|------|
| StreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` |
| IStreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponse.cs` |
| TurnContext (exposes StreamingResponse) | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| StreamInfo entity | `src/libraries/Core/Microsoft.Agents.Core/Models/StreamInfo.cs` |
| StreamTypes constants | `src/libraries/Core/Microsoft.Agents.Core/Models/StreamTypes.cs` |
