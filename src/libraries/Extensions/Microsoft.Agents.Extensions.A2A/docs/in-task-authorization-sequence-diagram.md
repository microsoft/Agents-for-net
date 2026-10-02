# A2A In-Task Authorization Sequence Diagram

Shows how an A2A task requests a user token after the task has started. The
standard `Authorization` header continues to authenticate the A2A request,
while `x-a2a-intask-authorization` carries the raw user token for `resumeAuth`.

## Diagram

```mermaid
sequenceDiagram
    autonumber
    participant Client as A2A Client
    participant OAuth as OAuth Authorization Server
    participant Endpoint as A2A Endpoint
    participant Adapter as A2AAdapter
    participant App as AgentApplication
    participant UA as UserAuthorization
    participant A2AUA as A2AUserAuthorization
    participant Exchange as OBO Exchange / IConnections
    participant Route as Protected Agent Route
    participant API as Downstream API
    participant Store as ITaskStore

    Client->>Endpoint: Initial A2A request<br/>Authorization: Bearer endpoint JWT<br/>A2A-Extensions: in-task authorization URI
    Endpoint->>Adapter: Process JSON-RPC or HTTP+JSON request
    Adapter->>App: RunPipelineAsync(message Activity)
    App->>UA: StartOrContinueSignInUserAsync(autoSigninHandlers)
    UA->>A2AUA: SignInUserAsync(Mode = InTask)
    A2AUA->>A2AUA: Verify extension activation
    A2AUA-->>Adapter: Send authorization-required Activity<br/>(request ID, OAuth flow, required scopes)
    Adapter->>Store: Save Task in TASK_STATE_AUTH_REQUIRED
    Adapter-->>Client: Auth-required Task

    Client->>OAuth: Run advertised OAuth flow
    OAuth-->>Client: User access token

    Client->>Endpoint: resumeAuth(taskId, contextId, requestId)<br/>Authorization: Bearer endpoint JWT<br/>x-a2a-intask-authorization: raw user token
    Endpoint->>Adapter: ResumeAuthAsync / resumeAuth
    Adapter->>Adapter: Verify extension, parameters,<br/>Task state, and context ID
    Adapter->>Adapter: Create Event Activity<br/>Value = ResumeAuthEventValue(token, raw Message)
    Adapter->>App: RunPipelineAsync(resumeAuth Event)
    App->>UA: Continue auto sign-in
    UA->>A2AUA: SignInUserAsync(handler)
    A2AUA->>A2AUA: Validate authorization request ID

    opt EnforceRequiredScopes
        A2AUA->>A2AUA: Validate delegated JWT scp claims
    end

    opt OBO scopes configured or requested
        A2AUA->>Exchange: HandleOBO(inbound token, scopes)
        Exchange-->>A2AUA: Downstream TokenResponse
    end

    A2AUA-->>UA: TokenResponse
    UA->>UA: Cache token for the handler and current turn

    opt Another autoSigninHandler requires a token
        UA->>A2AUA: SignInUserAsync(next handler)
        A2AUA-->>Adapter: Send another authorization-required Activity
        Adapter->>Store: Save next TASK_STATE_AUTH_REQUIRED
        Adapter-->>Client: Auth-required Task for next handler
        Note over Client,Adapter: Repeat OAuth acquisition and resumeAuth for the next handler
    end

    UA-->>App: Sign-in complete
    App->>Route: Replay the protected route
    Route->>UA: GetTurnTokenAsync(handlerName)
    UA-->>Route: Current turn token

    opt Route calls a downstream API
        Route->>API: Authorized API request
        API-->>Route: API response
    end

    Route-->>Adapter: Send task status or artifact updates
    Adapter->>Store: Save latest Task
    Adapter-->>Client: Latest Task
```

## Key Components

| Component | Location |
| --- | --- |
| `A2AAdapter` (`resumeAuth` validation and Event Activity conversion) | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2AAdapter.cs` |
| `InTaskAuthorizationExtension` (extension URI and token header) | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/ProtocolExtensions/InTaskAuthorization/InTaskAuthorizationExtension.cs` |
| `ResumeAuthEventValue` and request context models | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/ProtocolExtensions/InTaskAuthorization/InTaskAuthorizationModels.cs` |
| `A2AUserAuthorization` (auth-required response, token validation, and OBO) | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Authorization/A2AUserAuthorization.cs` |
| `UserAuthorization` (per-handler turn-token cache) | `src/libraries/Builder/Microsoft.Agents.Builder/App/UserAuth/UserAuthorization.cs` |

## Important Behavior

- The endpoint JWT and user token are logically separate credentials.
- The `x-a2a-intask-authorization` value is the raw token, without a `Bearer`
  prefix.
- A task can request additional handlers sequentially, allowing more than one
  user token during the task.
- Opaque user tokens are supported when application-specific validation or
  exchange does not require JWT claims.
