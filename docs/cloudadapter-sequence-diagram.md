# CloudAdapter Pipeline Sequence Diagram

Shows the interaction between `CloudAdapter.ProcessAsync`, the middleware pipeline, `ITurnContext.SendActivity`, and `IAgent` — including both response paths.

## Response Paths

- **Normal delivery**: `HostResponseAsync` returns `false` → response sent via `ConnectorClient` (Azure Bot Service pushes to client)
- **Stream delivery**: `HostResponseAsync` returns `true` → response queued into `ChannelResponseQueue` → HTTP thread writes SSE events directly to `HttpResponse.Body`

## Diagram

```mermaid
sequenceDiagram
    participant Client
    participant CloudAdapter
    participant ChannelResponseQueue
    participant ProcessActivity as ProcessActivityAsync
    participant MiddlewareSet
    participant Middleware as Middleware[0..N]
    participant IAgent
    participant TurnContext
    participant AdapterBase as ChannelServiceAdapterBase
    participant ConnectorClient
    participant HttpResponse

    Client->>CloudAdapter: POST /api/messages

    alt DeliveryMode == Stream (SSE)
        Note over CloudAdapter: Blocking SSE path (Invoke and ExpectReplies are also synchronous but non-SSE)
        CloudAdapter->>ChannelResponseQueue: StartHandlerForRequest(requestId)
        CloudAdapter->>HttpResponse: ResponseBegin()<br/>(Content-Type: text/event-stream)
        CloudAdapter-->>ProcessActivity: fire-and-forget task

        par Background: agent processing
            ProcessActivity->>TurnContext: new TurnContext(adapter, activity)
            ProcessActivity->>MiddlewareSet: RunPipelineAsync(context, agent.OnTurnAsync)

            loop Middleware chain (recursive)
                MiddlewareSet->>Middleware: OnTurnAsync(context, next)
                Middleware->>MiddlewareSet: await next()
            end

            MiddlewareSet->>IAgent: OnTurnAsync(context)

            Note over IAgent: Agent does work, sends replies

            IAgent->>TurnContext: SendActivityAsync(activity)
            TurnContext->>TurnContext: ApplyConversationReference()<br/>Run OnSendActivities callbacks
            TurnContext->>AdapterBase: SendActivitiesAsync(activities[])

            AdapterBase->>AdapterBase: HostResponseAsync(incomingActivity, outActivity)
            Note over AdapterBase: DeliveryMode == Stream → returns true
            AdapterBase->>ChannelResponseQueue: SendActivitiesAsync(requestId, activities)
            ChannelResponseQueue-->>AdapterBase: return true

        and HTTP thread: consuming response queue
            CloudAdapter->>ChannelResponseQueue: HandleResponsesAsync(requestId, writer.OnResponse)
            loop Until queue complete
                ChannelResponseQueue-->>CloudAdapter: activity
                CloudAdapter->>HttpResponse: OnResponse() writes SSE event<br/>"event: activity\r\ndata: {...}\r\n\r\n"
                HttpResponse->>Client: SSE chunk (streamed)
            end
        end

        ProcessActivity-->>ChannelResponseQueue: CompleteHandlerForRequest(requestId)
        ChannelResponseQueue-->>CloudAdapter: HandleResponsesAsync returns
        CloudAdapter->>HttpResponse: ResponseEnd()<br/>(writes invokeResponse event if Invoke)
        HttpResponse->>Client: final SSE event / close

    else DeliveryMode == Normal (default)
        Note over CloudAdapter: Fire-and-forget path
        CloudAdapter->>CloudAdapter: QueueBackgroundActivity(activity)
        CloudAdapter->>Client: 202 Accepted (immediate return)

        Note over ProcessActivity: Background worker picks up activity
        ProcessActivity->>TurnContext: new TurnContext(adapter, activity)
        ProcessActivity->>MiddlewareSet: RunPipelineAsync(context, agent.OnTurnAsync)

        loop Middleware chain (recursive)
            MiddlewareSet->>Middleware: OnTurnAsync(context, next)
            Middleware->>MiddlewareSet: await next()
        end

        MiddlewareSet->>IAgent: OnTurnAsync(context)

        IAgent->>TurnContext: SendActivityAsync(activity)
        TurnContext->>TurnContext: ApplyConversationReference()<br/>Run OnSendActivities callbacks
        TurnContext->>AdapterBase: SendActivitiesAsync(activities[])

        AdapterBase->>AdapterBase: HostResponseAsync(incomingActivity, outActivity)
        Note over AdapterBase: DeliveryMode == Normal → returns false
        AdapterBase->>ConnectorClient: ReplyToActivityAsync(activity)
        ConnectorClient->>Client: POST to serviceUrl/conversations/.../activities
    end
```

## Key Components

| Component | Location |
|-----------|----------|
| `CloudAdapter` | `src/libraries/Hosting/AspNetCore/CloudAdapter.cs` |
| `ChannelResponseQueue` | `src/libraries/Hosting/AspNetCore/ChannelResponseQueue.cs` |
| `ActivityResponseHandler` (SSE writer) | `src/libraries/Hosting/AspNetCore/ActivityResponseHandler.cs` |
| `ChannelServiceAdapterBase` | `src/libraries/Builder/Microsoft.Agents.Builder/ChannelServiceAdapterBase.cs` |
| `TurnContext` | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| `MiddlewareSet` | `src/libraries/Builder/Microsoft.Agents.Builder/MiddlewareSet.cs` |

## ChannelResponseQueue — Producer/Consumer Bridge

In the Stream path, `ChannelResponseQueue` acts as a thread-safe bridge between the background agent processing thread and the HTTP response thread:

- **Producer**: background agent thread calls `SendActivitiesAsync()` → writes activities to an unbounded `Channel<IActivity>`
- **Consumer**: HTTP request thread calls `HandleResponsesAsync()` → reads activities and passes them to `ActivityResponseHandler.OnResponse()`, which writes SSE events to `HttpResponse.Body`
- **Completion**: when `ProcessActivityAsync` finishes, it calls `CompleteHandlerForRequest()` → closes the channel writer → consumer loop exits → `ResponseEnd()` is called
