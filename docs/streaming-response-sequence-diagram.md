# Streaming Response Sequence Diagrams

`ITurnContext.StreamingResponse` provides one API for Activity Protocol streaming and channel-native
streaming implementations. `StreamingResponseBase` owns buffering, sequencing, the asynchronous interval
loop, end/reset behavior, and error actions. `StreamingResponse` provides the shared Activity Protocol
format, while channel specializations provide defaults, metadata, and error handling.

## Response Selection

`ChannelServiceAdapterBase` checks the full channel ID first (for example, `msteams:COPILOT`) and then the
parent channel ID. A discovered factory is instantiated through dependency injection and cached by factory
type. If discovery or creation fails, `TurnContext` uses its built-in response:

- `M365CopilotStreamingResponse` for `msteams:COPILOT`.
- `TeamsStreamingResponse` for Teams and other Teams subchannels.
- `StreamingResponse` for all other channels.

```mermaid
sequenceDiagram
    participant Adapter as ChannelServiceAdapterBase
    participant Catalog as StreamingResponseFactoryCatalog
    participant DI as IServiceProvider
    participant Context as TurnContext
    participant Factory as IStreamingResponseFactory

    Adapter->>Catalog: Lookup full channel ID
    alt No full-channel registration
        Adapter->>Catalog: Lookup parent channel ID
    end

    alt Factory type registered
        Adapter->>DI: ActivatorUtilities.CreateInstance(factoryType)
        DI-->>Adapter: Factory or cached failure
        alt Factory available
            Adapter->>Factory: Create(turnContext)
            Factory-->>Adapter: Channel-specific IStreamingResponse
            Adapter->>Context: SetStreamingResponse(response)
        else Factory unavailable or Create throws
            Note over Adapter,Context: Log warning and retain built-in response
        end
    else No factory registered
        Note over Context: Lazy built-in response selection
    end

    Context-->>Adapter: Channel-specific built-in response
```

## Channel Defaults

| Channel | Implementation | Interval | Stream identity | Special behavior |
|---|---|---:|---|---|
| Teams | `TeamsStreamingResponse` | 1000 ms | First response `Activity.Id` | Teams-specific errors and feedback metadata |
| M365 Copilot (`msteams:COPILOT`) | `M365CopilotStreamingResponse` | 1000 ms | First response `Activity.Id` | Teams behavior plus 35-second idle notice and 105-second streaming cutoff |
| WebChat / DirectLine | `StreamingResponse` | 500 ms | Pre-generated GUID | Full accumulated text per update |
| `DeliveryModes.Stream` | `StreamingResponse` | 100 ms | Pre-generated GUID | Activity Protocol streaming over the host transport |
| Slack | `SlackStreamingResponse` | 200 ms | Slack stream plus local GUID | Appends text deltas through Slack `chat.*Stream` APIs |
| Other / `ExpectReplies` | `StreamingResponse` | N/A | N/A | Buffers and sends one normal final message |

`InitialDelay` defaults to 250 ms. Slack can override `Interval` and `InitialDelay` through
`Slack:Streaming` configuration.

## Shared `StreamingResponseBase` Loop

The previous `System.Threading.Timer` callback is replaced by a single asynchronous `Task.Delay` loop.
Only one send can be active at a time. Informative updates and buffered text snapshots share one FIFO queue.

```mermaid
sequenceDiagram
    participant Agent
    participant Response as StreamingResponseBase
    participant Worker as Task.Delay loop
    participant Hook as Channel implementation

    Agent->>Response: QueueTextChunk(text)
    Response->>Response: Append to Message<br/>TransformBufferedText<br/>Mark message updated
    Response->>Worker: Start with InitialDelay if not running

    loop Until stopped
        Worker->>Worker: Task.Delay(dueTime)
        Worker->>Hook: OnBeforeSendIntervalAsync(queueEmpty)
        Hook-->>Worker: Continue or stop
        Worker->>Response: Snapshot accumulated Message if updated
        alt Pending informative or text item
            Worker->>Hook: SendInformativeAsync or SendChunkAsync
            Hook-->>Worker: Send completed
            Worker->>Hook: OnSendCompleted
            Worker->>Worker: Next delay = Interval
        else End requested and queue empty
            Worker->>Response: StopStream
            Worker->>Response: Signal queue drained
        else No pending work
            Worker->>Worker: Poll again in 200 ms
        end
    end
```

`StopStream` runs before the drain signal is set, so `EndStreamAsync` cannot return while
`IsStreamStarted()` still reports `true`.

## Teams Flow

Teams assigns the stream ID. The first successful send returns an `Activity.Id`; every later streaming
activity uses that value as both `Activity.Id` and `StreamInfo.StreamId`.

```mermaid
sequenceDiagram
    participant Agent
    participant Context as TurnContext
    participant Response as TeamsStreamingResponse
    participant Worker as Task.Delay loop
    participant Teams

    Agent->>Response: QueueInformativeUpdateAsync("Searching...")
    Response->>Context: Send Typing + Informative, sequence 1
    Context->>Teams: Activity without StreamId
    Teams-->>Context: ResourceResponse(Activity.Id)
    Context-->>Response: ResourceResponse
    Response->>Response: StreamId = Activity.Id
    Response->>Worker: Start at Interval

    loop LLM produces text
        Agent->>Response: QueueTextChunk(text)
        Response->>Response: Accumulate full Message
        Worker->>Response: Snapshot latest full Message
        Response->>Context: Send Typing + Streaming<br/>Activity.Id = StreamId
        Context->>Teams: Full accumulated text
        Teams-->>Context: ResourceResponse
    end

    Agent->>Response: EndStreamAsync()
    Response->>Response: Mark ended and wait for drain
    Worker->>Response: Drain pending item<br/>StopStream<br/>Signal drained
    Response->>Context: Send Message + Final<br/>full text, citations, AI metadata, feedback
    Context->>Teams: Final activity using StreamId
    Teams-->>Context: ResourceResponse
    Response-->>Agent: Success
```

## M365 Copilot Flow

`M365CopilotStreamingResponse` derives from `TeamsStreamingResponse`, retaining Teams stream identity,
error handling, and final-message metadata while adding separate lifecycle requirements:

- The 105-second cutoff begins after the first successful send.
- Successful informative and text sends reset the 35-second inactivity clock.
- An idle keep-alive is sent directly by the current loop iteration. It reuses the most recent informative
  text, or `StreamingTakingTooLongMessage` if none exists.
- At the cutoff, the streaming transport is closed and the response falls back to normal delivery. Content
  generation can continue, and `EndStreamAsync` later sends the complete buffered response as a normal message.

```mermaid
sequenceDiagram
    participant Agent
    participant Context as TurnContext
    participant Response as M365CopilotStreamingResponse
    participant Worker as Task.Delay loop
    participant Copilot as M365 Copilot

    Agent->>Response: QueueInformativeUpdateAsync or QueueTextChunk
    Response->>Context: First streaming activity
    Context->>Copilot: Activity
    Copilot-->>Context: ResourceResponse(Activity.Id)
    Context-->>Response: Successful response
    Response->>Response: Set StreamId<br/>Set stream start and last activity time

    loop While streaming is enabled
        Worker->>Response: OnBeforeSendIntervalAsync(queueEmpty)

        alt Elapsed time is at least 105 seconds
            alt No answer text buffered
                Response->>Context: Message + Final + Error<br/>timeout notice
                Context->>Copilot: Close stream with no partial answer
            else Answer text buffered
                Response->>Context: Typing + Streaming<br/>partial text and timeout notice
                Context->>Copilot: Preserve latest partial answer
                Response->>Context: Message + Final + Success<br/>partial text and timeout notice
                Context->>Copilot: Close stream
            end
            Response->>Response: FallbackToNonStreaming<br/>Stop loop and signal drain
        else Queue empty and idle for more than 35 seconds
            Response->>Context: Send Typing + Informative directly
            Context->>Copilot: Previous informative text or working notice
            Copilot-->>Context: ResourceResponse
            Response->>Response: Reset last activity time
        else Normal pending text or informative update
            Response->>Context: Send next queued activity
            Context->>Copilot: Streaming update
            Copilot-->>Context: ResourceResponse
            Response->>Response: Reset last activity time
        end
    end

    Agent->>Response: Continue QueueTextChunk calls if generation is still running
    Note over Response: After timeout, text is buffered without intermediate sends
    Agent->>Response: EndStreamAsync()
    Response->>Context: Send complete buffered text as normal Message
    Context->>Copilot: Non-streaming final response
    Response-->>Agent: Success
```

If the agent finishes before the cutoff, `EndStreamAsync` follows the normal Teams finalization path and
sends a `StreamTypes.Final` activity.

## WebChat, DirectLine, and `DeliveryModes.Stream`

These channels use the same accumulated-text behavior as Teams, but the response creates a GUID before the
first send instead of adopting the first `ResourceResponse.Id`.

```mermaid
sequenceDiagram
    participant Agent
    participant Response as StreamingResponse
    participant Context as TurnContext
    participant Channel

    Response->>Response: Pre-generate StreamId GUID
    loop Text generation
        Agent->>Response: QueueTextChunk(text)
        Response->>Context: Typing + Streaming<br/>full accumulated text and StreamId
        Context->>Channel: Intermediate activity
    end
    Agent->>Response: EndStreamAsync()
    Response->>Context: Message + Final
    Context->>Channel: Final activity
    Response-->>Agent: Success
```

## Slack Native Streaming

The Slack extension registers `SlackStreamingResponseFactory` for the Slack channel. The factory is
discovered automatically and creates `SlackStreamingResponse` with its `SlackApi` dependency.

Unlike Activity Protocol channels, Slack appends only the new text delta.

```mermaid
sequenceDiagram
    participant Agent
    participant Response as SlackStreamingResponse
    participant Worker as StreamingResponseBase loop
    participant Slack as Slack Web API

    Agent->>Response: QueueInformativeUpdateAsync("Working...")
    Response->>Slack: chat.startStream
    Slack-->>Response: Message timestamp
    Response->>Slack: chat.appendStream(TaskUpdateChunk InProgress)

    loop LLM produces text
        Agent->>Response: QueueTextChunk(text)
        Worker->>Response: SendChunkAsync(full buffered text)
        Response->>Response: Compute unsent delta from sent length
        Response->>Slack: chat.appendStream(MarkdownTextChunk delta)
    end

    Agent->>Response: EndStreamAsync()
    Worker->>Response: Drain pending deltas
    Response->>Slack: chat.appendStream(TaskUpdateChunk Complete)
    Response->>Slack: chat.stopStream(optional feedback blocks)
    Response->>Response: Clear local stream and StreamId
    Response-->>Agent: Success

    Note over Response,Slack: Completion and send-error paths always attempt chat.stopStream
```

## Non-Streaming Flow

For unsupported channels and `DeliveryMode.ExpectReplies`, informative updates are ignored, text is buffered,
and no interval loop starts.

```mermaid
sequenceDiagram
    participant Agent
    participant Response as StreamingResponse
    participant Context as TurnContext
    participant Channel

    Agent->>Response: QueueInformativeUpdateAsync("...")
    Note over Response: No-op because IsStreamingChannel is false

    loop Text generation
        Agent->>Response: QueueTextChunk(text)
        Response->>Response: Accumulate Message only
    end

    Agent->>Response: EndStreamAsync()
    Response->>Context: Send normal Message with buffered text
    Context->>Channel: One final activity without StreamInfo
    Response-->>Agent: Success
```

## Error Actions

`StreamingResponseBase` asks the implementation to map a send exception to one of three actions:

| Action | Base behavior |
|---|---|
| `Continue` | Drop the failed send and continue the interval loop |
| `FallbackToNonStreaming` | Disable intermediate streaming, stop the loop, and allow a normal final message |
| `Cancel` | Stop the loop; `EndStreamAsync` returns `Error` or `UserCancelled` |

`TeamsStreamingResponse` applies these Teams service policies:

- `ContentStreamNotAllowed` caused by user cancellation returns `UserCancelled`.
- `ContentStreamNotAllowed` with the exceeded-time message updates the existing activity and falls back to
  non-streaming delivery.
- `BadArgument` with `streaming api is not enabled` falls back to non-streaming delivery.
- Other failures cancel the stream.

`StreamingResponse`, used by WebChat, DirectLine, and `DeliveryModes.Stream`, treats send failures as
transport failures and cancels the stream.

Slack attempts `chat.stopStream` before returning `Cancel`, so a remote Slack message is not intentionally
left in progress after an append failure.

## Key Implementation Details

- The interval loop uses `Task.Delay`, not `System.Threading.Timer`.
- Only one send runs at a time; the next delay begins after the current send completes.
- Informative updates and text snapshots are sent in FIFO order.
- Activity Protocol intermediate messages contain the full accumulated text; Slack sends only the new delta.
- `EndStreamTimeout` defaults to two minutes.
- `ResetAsync` ends an active stream, clears buffered and synchronization state, restores shared defaults,
  and invokes the implementation-specific reset hook to restore channel defaults and stream identity.
- Factory lookup checks the full channel ID before the parent ID, enabling subchannel specializations.
- Factory instantiation failures are cached without caching the absence of a registration, so later-loaded
  extension assemblies can still be discovered.

## Related Source Files

| Component | Path |
|---|---|
| Shared streaming loop | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs` |
| Activity Protocol implementation | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` |
| Teams specialization | `src/libraries/Builder/Microsoft.Agents.Builder/TeamsStreamingResponse.cs` |
| M365 Copilot specialization | `src/libraries/Builder/Microsoft.Agents.Builder/M365CopilotStreamingResponse.cs` |
| Streaming response contract | `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponse.cs` |
| Factory contract and discovery | `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs` |
| Factory catalog | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryCatalog.cs` |
| Adapter factory selection | `src/libraries/Builder/Microsoft.Agents.Builder/ChannelServiceAdapterBase.cs` |
| Turn response property | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| Slack implementation | `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/Api/SlackStreamingResponse.cs` |
| Stream entity model | `src/libraries/Core/Microsoft.Agents.Core/Models/StreamInfo.cs` |
