# Teams SSO (non-Agentic) OAuth flow via DotNet AgentApplication

- **Teams** is the Teams backend (SMBA).
- **Agent** is the business logic for the Agent.  The customer's code.
- **AgentApplication** is SDK provided application logic.
- **UserAuthorization** is SDK provided OAuth flow logic.
- **Token Service** is the Azure Token Service.
  - Note that the DotNet Token Service Client caches successful tokens from `GetTokenOrSignInResource`.  While the diagrams indicate requests, cached values would instead be used when available.

## SignedIn
This represents a single-turn token acquisition as a result of the user having already signed into the Token Service in the past. If OBO is configured, the OBO is performed on the token returned by the Token Service prior to setting the Turn Token.

```mermaid
sequenceDiagram
    participant Teams
    participant Agent
    participant AgentApplication
    participant UserAuthorization
    participant TokenService

    %% turn 1
    rect rgba(128, 128, 128, .1)
    Note over Teams,Agent: Turn 1
    Teams->>AgentApplication: Activity
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn
    activate UserAuthorization
    UserAuthorization->>TokenService: GetTokenOrSignInResource()
    activate TokenService
    TokenService-->>UserAuthorization: TokenResponse (200)
    deactivate TokenService
    UserAuthorization->>UserAuthorization: Set Turn Token
    UserAuthorization->>AgentApplication: SignIn (complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route Activity
    activate Agent
    deactivate AgentApplication
    Agent->>UserAuthorization: GetTurnToken
    activate UserAuthorization
    UserAuthorization-->>Agent: Token
    deactivate UserAuthorization
    Agent->>Teams: Activity Response
    deactivate Agent
    end
```

## SignIn, No ConsentRequired
This represents the signin flow, which is a multi-turn operation, where no user consent is required. If OBO is configured, the OBO is performed on the token returned by the Token Service (in `Turn 2`) prior to setting the Turn Token. Note that `Turn 3` is the same flow as `SignedIn` above.

```mermaid
sequenceDiagram
    participant Teams
    participant Agent
    participant AgentApplication
    participant UserAuthorization
    participant TokenService

    %% turn 1
    rect rgba(128, 128, 128, .1)
    Note over Teams,Agent: Turn 1
    Teams->>AgentApplication: Activity
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn
    activate UserAuthorization
    UserAuthorization->>TokenService: GetTokenOrSignInResource()
    activate TokenService
    TokenService-->>UserAuthorization: SignInResource (no token)
    deactivate TokenService
    UserAuthorization->>Teams: OAuthCard
    UserAuthorization->>UserAuthorization: Set FlowState (started, expires)
    UserAuthorization->>UserAuthorization: Store Continuation Activity
    UserAuthorization-->>AgentApplication: SignIn (pending)
    deactivate UserAuthorization
    AgentApplication-->>Teams: (turn ends, no route)
    deactivate AgentApplication
    end

    %% turn 2
    rect rgba(170, 128, 128, .1)
    Note over Teams, Agent: Turn 2 (invoke)
    Teams->>AgentApplication: Invoke(signin/tokenExchange)
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn (continuation)
    activate UserAuthorization
    UserAuthorization->>TokenService: ExchangeToken()
    activate TokenService
    TokenService-->>UserAuthorization: TokenResponse (200)
    deactivate TokenService
    UserAuthorization-->>Teams: InvokeResponse:200
    UserAuthorization->>UserAuthorization: Set Turn Token
    UserAuthorization->>UserAuthorization: Delete FlowState
    UserAuthorization->>UserAuthorization: Async Proactive(Continuation Activity)
    UserAuthorization-->>AgentApplication: SignIn (complete)
    deactivate UserAuthorization
    deactivate AgentApplication
    end

    %% turn 3
    rect rgba(128, 128, 128, .1)
    Note over Agent,Teams: Turn 3 (Proactive Continuation)
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn
    activate UserAuthorization
    UserAuthorization->>TokenService: GetTokenOrSignInResource()
    activate TokenService
    TokenService-->>UserAuthorization: Cached TokenResponse (200)
    deactivate TokenService
    UserAuthorization->>UserAuthorization: Set Turn Token
    UserAuthorization-->>AgentApplication: SignIn (complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route Continuation Activity
    deactivate AgentApplication
    activate Agent
    Agent->>UserAuthorization: GetTurnToken
    activate UserAuthorization
    UserAuthorization-->>Agent: Token
    deactivate UserAuthorization
    Agent->>Teams: Activity Response
    deactivate Agent
    end
```

## SignIn, ConsentRequired
This represents the signin flow where Teams SSO token exchange fails because the user hasn't consented. Teams prompts for consent, then sends a verifyState invoke with a magic code. If OBO is configured, the OBO is performed on the token returned by the Token Service (in `Turn 3`) prior to setting the Turn Token. Note that `Turn 4` is the same flow as `SignedIn` above.

```mermaid
sequenceDiagram
    participant Teams
    participant Agent
    participant AgentApplication
    participant UserAuthorization
    participant TokenService

    %% turn 1
    rect rgba(128, 128, 128, .1)
    Note over Teams,Agent: Turn 1
    Teams->>AgentApplication: Activity
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn
    activate UserAuthorization
    UserAuthorization->>TokenService: GetTokenOrSignInResource()
    activate TokenService
    TokenService-->>UserAuthorization: SignInResource (no token)
    deactivate TokenService
    UserAuthorization->>Teams: OAuthCard
    UserAuthorization->>UserAuthorization: Set FlowState (started, expires)
    UserAuthorization->>UserAuthorization: Store Continuation Activity
    UserAuthorization-->>AgentApplication: SignIn (pending)
    deactivate UserAuthorization
    AgentApplication-->>Teams: (turn ends, no route)
    deactivate AgentApplication
    end

    %% turn 2
    rect rgba(170, 128, 128, .1)
    Note over Teams, Agent: Turn 2 (invoke)
    Teams->>AgentApplication: Invoke(signin/tokenExchange)
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn (continuation)
    activate UserAuthorization
    UserAuthorization->>TokenService: ExchangeToken()
    activate TokenService
    TokenService-->>UserAuthorization: ErrorResponse (ConsentRequired)
    deactivate TokenService
    UserAuthorization-->>Teams: InvokeResponse:412
    UserAuthorization-->>AgentApplication: SignIn (pending)
    deactivate UserAuthorization
    deactivate AgentApplication
    end

    Note over Teams: Teams prompts user for Consent

    %% turn 3
    rect rgba(170, 128, 128, .1)
    Note over Teams, Agent: Turn 3 (invoke)
    Teams->>AgentApplication: Invoke(signin/verifyState)
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn (continuation)
    activate UserAuthorization
    UserAuthorization->>TokenService: GetUserToken(code)
    activate TokenService
    TokenService-->>UserAuthorization: TokenResponse (200)
    deactivate TokenService
    UserAuthorization-->>Teams: InvokeResponse:200
    UserAuthorization->>UserAuthorization: Set Turn Token
    UserAuthorization->>UserAuthorization: Delete FlowState
    UserAuthorization->>UserAuthorization: Async Proactive(Continuation Activity)
    UserAuthorization-->>AgentApplication: SignIn (complete)
    deactivate UserAuthorization
    deactivate AgentApplication
    end

    %% turn 4
    rect rgba(128, 128, 128, .1)
    Note over Agent,Teams: Turn 4 (Proactive Continuation)
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn
    activate UserAuthorization
    UserAuthorization->>TokenService: GetTokenOrSignInResource()
    activate TokenService
    TokenService-->>UserAuthorization: Cached TokenResponse (200)
    deactivate TokenService
    UserAuthorization->>UserAuthorization: Set Turn Token
    UserAuthorization-->>AgentApplication: SignIn (complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route Continuation Activity
    deactivate AgentApplication
    activate Agent
    Agent->>UserAuthorization: GetTurnToken
    activate UserAuthorization
    UserAuthorization-->>Agent: Token
    deactivate UserAuthorization
    Agent->>Teams: Activity Response
    deactivate Agent
    end
```

## SignIn, Exchange failure
This represents a critical failure during token exchange (not ConsentRequired). For example, a misconfigured OAuth connection or a Token Service outage. Teams will not retry after receiving a 400.

```mermaid
sequenceDiagram
    participant Teams
    participant Agent
    participant AgentApplication
    participant UserAuthorization
    participant TokenService

    %% turn 1
    rect rgba(128, 128, 128, .1)
    Note over Teams,Agent: Turn 1
    Teams->>AgentApplication: Activity
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn
    activate UserAuthorization
    UserAuthorization->>TokenService: GetTokenOrSignInResource()
    activate TokenService
    TokenService-->>UserAuthorization: SignInResource (no token)
    deactivate TokenService
    UserAuthorization->>Teams: OAuthCard
    UserAuthorization->>UserAuthorization: Set FlowState (started, expires)
    UserAuthorization->>UserAuthorization: Store Continuation Activity
    UserAuthorization-->>AgentApplication: SignIn (pending)
    deactivate UserAuthorization
    AgentApplication-->>Teams: (turn ends, no route)
    deactivate AgentApplication
    end

    %% turn 2
    rect rgba(170, 128, 128, .1)
    Note over Teams, Agent: Turn 2 (invoke)
    Teams->>AgentApplication: Invoke(signin/tokenExchange)
    activate AgentApplication
    AgentApplication->>UserAuthorization: SignIn (continuation)
    activate UserAuthorization
    UserAuthorization->>TokenService: ExchangeToken()
    activate TokenService
    TokenService-->>UserAuthorization: ErrorResponse (non-consent error)
    deactivate TokenService
    UserAuthorization-->>Teams: InvokeResponse:400
    UserAuthorization->>UserAuthorization: Delete FlowState
    UserAuthorization-->>AgentApplication: SignIn (error)
    deactivate UserAuthorization
    deactivate AgentApplication
    end
```

## Related Source Files

| Component | Path |
|-----------|------|
| UserAuthorization | `src/libraries/Builder/Microsoft.Agents.Builder/App/UserAuth/UserAuthorization.cs` |
| Authentication.Msal | `src/libraries/Authentication/Authentication.Msal/` |
| Token Service Client | `src/libraries/Client/Microsoft.Agents.Connector/` |
| AgentApplication (SignIn orchestration) | `src/libraries/Builder/Microsoft.Agents.Builder/App/AgentApplication.cs` |
