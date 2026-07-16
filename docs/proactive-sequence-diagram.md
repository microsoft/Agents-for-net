# Proactive Messaging Sequence Diagram

Shows how agents can initiate messages to users outside the normal request/response flow. Proactive messaging enables notifications, scheduled alerts, and external-trigger scenarios using stored conversation references.

## Participants

- **External Caller** — An external system (webhook, timer, API client) that triggers proactive messaging via HTTP.
- **HttpProactive** — ASP.NET Core endpoint handler that processes proactive HTTP requests.
- **AgentApplication** — The customer's agent, exposing `Proactive` property.
- **Proactive** — SDK class that manages conversation storage and proactive operations.
- **IStorage** — Persistence layer for conversation references (MemoryStorage, Blob, CosmosDb).
- **ChannelAdapter** — Adapter that creates turn contexts and sends activities to the channel.
- **Channel** — The upstream channel (Teams, WebChat, DirectLine, etc.).

## Flow 1: Store Conversation Reference

Before proactive messaging can work, the conversation must be stored during a normal user-initiated turn.

```mermaid
sequenceDiagram
    participant User
    participant Channel
    participant ChannelAdapter
    participant AgentApplication
    participant Proactive
    participant IStorage

    User->>Channel: Send message
    Channel->>ChannelAdapter: POST /api/messages (Activity)
    ChannelAdapter->>AgentApplication: OnTurnAsync(turnContext)
    AgentApplication->>Proactive: StoreConversationAsync(turnContext)
    Proactive->>Proactive: Extract Claims + ConversationReference
    Proactive->>IStorage: WriteAsync(key, Conversation)
    IStorage-->>Proactive: stored
    Proactive-->>AgentApplication: conversationId
    AgentApplication->>Channel: SendActivityAsync("Conversation stored")
```

## Flow 2: SendActivity via HTTP (with stored conversationId)

An external system sends an activity to a previously stored conversation.

```mermaid
sequenceDiagram
    participant ExternalCaller as External Caller
    participant HttpProactive
    participant Proactive
    participant IStorage
    participant ChannelAdapter
    participant Channel

    ExternalCaller->>HttpProactive: POST /proactive/sendactivity/{conversationId}<br/>Body: Activity (JSON)
    HttpProactive->>Proactive: GetConversationWithThrowAsync(conversationId)
    Proactive->>IStorage: ReadAsync(key)
    IStorage-->>Proactive: Conversation (Claims + Reference)
    Proactive-->>HttpProactive: Conversation

    HttpProactive->>Proactive: SendActivityAsync(adapter, conversation, activity)
    activate Proactive
    Proactive->>ChannelAdapter: ContinueConversationAsync(identity, reference, callback)
    activate ChannelAdapter
    ChannelAdapter->>ChannelAdapter: Create TurnContext from reference
    ChannelAdapter->>Proactive: callback(turnContext)
    Proactive->>ChannelAdapter: turnContext.SendActivityAsync(activity)
    ChannelAdapter->>Channel: Activity
    Channel-->>ChannelAdapter: ResourceResponse
    deactivate ChannelAdapter
    Proactive-->>HttpProactive: ResourceResponse
    deactivate Proactive

    HttpProactive-->>ExternalCaller: 200 OK + ResourceResponse
```

## Flow 3: ContinueConversation via HTTP

An external system triggers a registered `[ContinueConversation]` handler, which runs full agent logic (state, token handling) within the stored conversation context.

```mermaid
sequenceDiagram
    participant ExternalCaller as External Caller
    participant HttpProactive
    participant Proactive
    participant IStorage
    participant ChannelAdapter
    participant AgentApplication
    participant UserAuthorization
    participant Channel

    ExternalCaller->>HttpProactive: POST /proactive/continue/{conversationId}?key=value
    HttpProactive->>Proactive: GetConversationWithThrowAsync(conversationId)
    Proactive->>IStorage: ReadAsync(key)
    IStorage-->>Proactive: Conversation
    Proactive-->>HttpProactive: Conversation

    HttpProactive->>HttpProactive: Build continuation Event activity<br/>(include query params as Value)
    HttpProactive->>Proactive: ContinueConversationAsync(adapter, conversation,<br/>routeHandler, tokenHandlers, continuationActivity)

    activate Proactive
    Proactive->>ChannelAdapter: ProcessProactiveAsync(identity, continuationActivity, callback)
    activate ChannelAdapter
    ChannelAdapter->>ChannelAdapter: Create TurnContext

    ChannelAdapter->>Proactive: callback(turnContext)
    Proactive->>Proactive: Load TurnState

    alt autoSignInHandlers specified
        Proactive->>UserAuthorization: GetSignedInTokensAsync(turnContext, handlers)
        UserAuthorization-->>Proactive: allAcquired (true/false)
        alt not all signed in & FailOnUnsignedInConnections
            Proactive-->>HttpProactive: throw UserNotSignedIn
        end
    end

    Proactive->>AgentApplication: routeHandler(turnContext, turnState)
    AgentApplication->>Channel: SendActivityAsync(...)
    Channel-->>AgentApplication: ResourceResponse
    Proactive->>Proactive: Save TurnState
    deactivate ChannelAdapter
    deactivate Proactive

    HttpProactive-->>ExternalCaller: 200 OK
```

## Flow 4: CreateConversation via HTTP

Creates a new conversation (e.g., 1:1 with a user in Teams) and optionally continues into it.

```mermaid
sequenceDiagram
    participant ExternalCaller as External Caller
    participant HttpProactive
    participant Proactive
    participant ChannelAdapter
    participant Channel
    participant IStorage

    ExternalCaller->>HttpProactive: POST /proactive/create<br/>Body: CreateConversationBody (JSON)
    HttpProactive->>HttpProactive: Extract claims from request or body.AgentClientId

    HttpProactive->>Proactive: CreateConversationAsync(adapter, createOptions,<br/>routeHandler, tokenHandlers, activityFactory)
    activate Proactive

    Proactive->>ChannelAdapter: CreateConversationAsync(identity, channelId,<br/>serviceUrl, audience, parameters)
    ChannelAdapter->>Channel: Create conversation
    Channel-->>ChannelAdapter: ConversationReference (new conversation)
    ChannelAdapter-->>Proactive: ConversationReference

    Proactive->>Proactive: new Conversation(identity, reference)

    opt StoreConversation = true
        Proactive->>IStorage: WriteAsync(key, Conversation)
    end

    opt ContinueConversation = true
        Proactive->>ChannelAdapter: ProcessProactiveAsync(identity, activity, callback)
        ChannelAdapter->>Proactive: callback(turnContext)
        Proactive->>Proactive: OnTurnAsync (state + handler)
        Proactive->>Channel: SendActivityAsync(...)
    end

    deactivate Proactive
    HttpProactive-->>ExternalCaller: 200 OK + Conversation
```

## Flow 5: In-Code Proactive (no HTTP endpoint)

An agent triggers proactive messaging from within its own turn logic (e.g., notifying another conversation).

```mermaid
sequenceDiagram
    participant AgentApplication
    participant Proactive
    participant IStorage
    participant ChannelAdapter
    participant Channel

    AgentApplication->>Proactive: ContinueConversationAsync(adapter, conversationId,<br/>handler, tokenHandlers)
    Proactive->>IStorage: ReadAsync(key)
    IStorage-->>Proactive: Conversation

    Proactive->>Proactive: Build continuation Event activity
    Proactive->>ChannelAdapter: ProcessProactiveAsync(identity, activity, callback)
    activate ChannelAdapter
    ChannelAdapter->>ChannelAdapter: Create TurnContext
    ChannelAdapter->>Proactive: callback(turnContext)
    Proactive->>Proactive: Load TurnState + token check
    Proactive->>AgentApplication: handler(turnContext, turnState)
    AgentApplication->>Channel: SendActivityAsync(...)
    Channel-->>AgentApplication: ResourceResponse
    Proactive->>Proactive: Save TurnState
    deactivate ChannelAdapter
```

## HTTP Endpoint Summary

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/proactive/sendactivity/{conversationId}` | POST | Send an activity to a stored conversation by ID |
| `/proactive/sendactivity` | POST | Send an activity with a `SendToConversationBody` body containing `Conversation` + `Activity` |
| `/proactive/continue/{conversationId}` | POST | Continue a stored conversation using the default `[ContinueConversation]` handler |
| `/proactive/continue/{key}/{conversationId}` | POST | Continue using a named handler (e.g., `/continue/ext/{id}`) |
| `/proactive/continue` | POST | Continue with a full `Conversation` object in body |
| `/proactive/continue/{key}` | POST | Continue (named) with a full `Conversation` object in body |
| `/proactive/create` | POST | Create a new conversation and optionally continue into it |
| `/proactive/create/{key}` | POST | Create using a named handler |

## Key Implementation Details

- **Conversation** — A record containing `ConversationReference` + `Claims` (JWT claims for identity reconstruction). Serializable for storage.
- **ConversationBuilder** — Fluent builder for manually constructing `Conversation` instances without an existing `ITurnContext`.
- **ProcessProactiveAsync** vs **ContinueConversationAsync** — `ProcessProactiveAsync` creates a full turn pipeline (middleware, state); `ContinueConversationAsync` (on adapter) is simpler and only provides a TurnContext callback.
- **Token Handling** — `[ContinueConversation(autoSignInHandlers: "me")]` automatically retrieves user tokens during proactive turns. If the user hasn't signed in, `UserNotSignedIn` is thrown.
- **Exception Capture** — Exceptions inside the proactive callback are captured via `ExceptionDispatchInfo` and re-thrown after the adapter completes, since they would otherwise be lost.
- **Query Parameters** — HTTP continue endpoints pass query string values as `Activity.Value` with `ValueType = "application/vnd.microsoft.activity.continueconversation+json"`.
- **Storage Key** — Conversations are stored under `proactive/conversations/{conversationId}`.

## Related Source Files

| Component | Path |
|-----------|------|
| Proactive | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/Proactive.cs` |
| Conversation | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/Conversation.cs` |
| ConversationBuilder | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ConversationBuilder.cs` |
| ContinueConversationAttribute | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ContinueConversationAttribute.cs` |
| ContinueConversationRoute | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ContinueConversationRoute.cs` |
| CreateConversationOptions | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/CreateConversationOptions.cs` |
| CreateConversationOptionsBuilder | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/CreateConversationOptionsBuilder.cs` |
| ProactiveOptions | `src/libraries/Builder/Microsoft.Agents.Builder/App/Proactive/ProactiveOptions.cs` |
| HttpProactive (endpoint handler) | `src/libraries/Hosting/AspNetCore/HttpProactive.cs` |
| AgentEndpointExtensions (MapAgentProactiveEndpoints) | `src/libraries/Hosting/AspNetCore/AgentEndpointExtensions.cs` |
| Proactive Sample | `src/samples/Proactive/ProactiveAgent.cs` |
| Proactive Sample Startup | `src/samples/Proactive/Program.cs` |
