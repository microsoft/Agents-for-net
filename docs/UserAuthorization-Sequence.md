# UserAuthorization and IUserAuthorization — High Level Sequence

- **Teams** is the Teams backend (or any channel).
- **Agent** is the customer's agent business logic.
- **AgentApplication** is the SDK-provided application framework (routes, middleware).
- **UserAuthorization** is the SDK-provided OAuth orchestration layer (`App.UserAuth.UserAuthorization`).
- **IUserAuthorization** is the pluggable auth handler interface (e.g., `AzureBotUserAuthorization`, `TeamsAgenticAuthorization`).

## Architecture

```
AgentApplication
  └── UserAuthorization (orchestrator)
        └── IUserAuthorizationDispatcher
              └── IUserAuthorization (handler per config entry)
                    ├── AzureBotUserAuthorization (Token Service)
                    ├── TeamsAgenticAuthorization (bot-hosted OAuth)
                    └── ConnectorUserAuthorization (Connector)
```

`UserAuthorization` manages:
- Auto sign-in decision (via `AutoSignInSelector`)
- Flow state persistence (active handler, continuation activity)
- Token caching for the turn
- Sign-in failure handling
- Proactive continuation after multi-turn flows

`IUserAuthorization` implementations handle:
- Acquiring tokens (interactive or silent)
- Provider-specific protocols (Token Service, MSAL, etc.)
- Sign-out / cache invalidation

## Auto SignIn — Token Available (single turn)

```mermaid
sequenceDiagram
    participant Channel
    participant AgentApplication
    participant UserAuthorization
    participant IUserAuthorization
    participant Agent

    rect rgba(128, 128, 128, .1)
    Note over Channel,Agent: Turn 1
    Channel->>AgentApplication: Activity (Message)
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>UserAuthorization: AutoSignIn? → true
    UserAuthorization->>IUserAuthorization: SignInUserAsync(forceSignIn: true)
    activate IUserAuthorization
    IUserAuthorization-->>UserAuthorization: TokenResponse
    deactivate IUserAuthorization
    UserAuthorization->>UserAuthorization: Cache token for turn
    UserAuthorization-->>AgentApplication: true (sign-in complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route Activity
    activate Agent
    Agent->>UserAuthorization: GetTurnTokenAsync
    UserAuthorization-->>Agent: Token
    Agent->>Channel: Response Activity
    deactivate Agent
    deactivate AgentApplication
    end
```

## Auto SignIn — Multi-Turn Flow (token not available)

```mermaid
sequenceDiagram
    participant Channel
    participant AgentApplication
    participant UserAuthorization
    participant IUserAuthorization
    participant Agent

    %% Turn 1: Start flow
    rect rgba(128, 128, 128, .1)
    Note over Channel,Agent: Turn 1
    Channel->>AgentApplication: Activity (Message)
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>UserAuthorization: AutoSignIn? → true
    UserAuthorization->>IUserAuthorization: SignInUserAsync(forceSignIn: true)
    activate IUserAuthorization
    IUserAuthorization->>Channel: Sign-in prompt (OAuthCard / AdaptiveCard)
    IUserAuthorization-->>UserAuthorization: null (pending)
    deactivate IUserAuthorization
    UserAuthorization->>UserAuthorization: Store ContinuationActivity, ActiveHandler
    UserAuthorization-->>AgentApplication: false (pending, don't route)
    deactivate UserAuthorization
    AgentApplication-->>Channel: (turn ends)
    deactivate AgentApplication
    end

    Note over Channel: User completes sign-in

    %% Turn 2: Continue flow
    rect rgba(170, 128, 128, .1)
    Note over Channel,Agent: Turn 2 (Invoke or callback-triggered)
    Channel->>AgentApplication: Invoke / Activity
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>UserAuthorization: ActiveHandler exists → continuation
    UserAuthorization->>IUserAuthorization: SignInUserAsync(forceSignIn: false)
    activate IUserAuthorization
    IUserAuthorization-->>UserAuthorization: TokenResponse
    deactivate IUserAuthorization
    UserAuthorization->>UserAuthorization: Cache token, delete sign-in state
    UserAuthorization->>UserAuthorization: ProcessProactive(ContinuationActivity)
    UserAuthorization-->>AgentApplication: false (proactive dispatched)
    deactivate UserAuthorization
    AgentApplication-->>Channel: InvokeResponse / (turn ends)
    deactivate AgentApplication
    end

    %% Turn 3: Proactive continuation
    rect rgba(128, 128, 128, .1)
    Note over Agent,Channel: Turn 3 (Proactive Continuation)
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>IUserAuthorization: SignInUserAsync
    activate IUserAuthorization
    IUserAuthorization-->>UserAuthorization: TokenResponse (cached/silent)
    deactivate IUserAuthorization
    UserAuthorization->>UserAuthorization: Cache token
    UserAuthorization-->>AgentApplication: true (complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route ContinuationActivity
    activate Agent
    Agent->>UserAuthorization: GetTurnTokenAsync
    UserAuthorization-->>Agent: Token
    Agent->>Channel: Response Activity
    deactivate Agent
    deactivate AgentApplication
    end
```

## Sign-In Failure

```mermaid
sequenceDiagram
    participant Channel
    participant AgentApplication
    participant UserAuthorization
    participant IUserAuthorization
    participant Agent

    rect rgba(170, 128, 128, .1)
    Note over Channel,Agent: Turn N (error during flow)
    Channel->>AgentApplication: Invoke / Activity
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>IUserAuthorization: SignInUserAsync
    activate IUserAuthorization
    IUserAuthorization-->>UserAuthorization: throws AuthException
    deactivate IUserAuthorization
    UserAuthorization->>UserAuthorization: Reset state, delete flow
    UserAuthorization->>Agent: OnUserSignInFailure handler
    activate Agent
    Agent->>Channel: Error message
    deactivate Agent
    UserAuthorization-->>AgentApplication: false (error handled)
    deactivate UserAuthorization
    AgentApplication-->>Channel: (turn ends)
    deactivate AgentApplication
    end
```
