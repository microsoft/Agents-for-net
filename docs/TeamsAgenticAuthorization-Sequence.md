# TeamsAgenticAuthorization — Sequence Diagrams

- **Teams** is the Teams backend (SMBA).
- **Agent** is the customer's agent business logic.
- **AgentApplication** is the SDK-provided application framework.
- **UserAuthorization** is the SDK-provided OAuth orchestration layer.
- **TeamsAgenticAuth** is `TeamsAgenticAuthorization`, the `IUserAuthorization` implementation for agentic mode.
- **MSAL** is the Microsoft Authentication Library (in-process, with token cache).
- **Azure AD** is the Microsoft Entra ID authorization endpoint.
- **Callback** is the bot-hosted `/auth/callback` HTTP endpoint (`TeamsAgenticOAuthCallbackEndpoint`).

## Why This Exists

In Teams **agentic mode**, the standard OAuthCard is not accepted by Teams. Agent blueprint app registrations cannot perform interactive OAuth (`response_type=code` returns AADSTS82018). This flow uses a separate regular app registration for interactive OAuth, with the bot hosting its own callback endpoint for the authorization code exchange.

## Already Signed In (MSAL cache hit)

After a successful interactive flow, MSAL caches the access and refresh tokens. Subsequent turns use `AcquireTokenSilent` to return a cached or silently-refreshed token without user interaction.

```mermaid
sequenceDiagram
    participant Teams
    participant AgentApplication
    participant UserAuthorization
    participant TeamsAgenticAuth
    participant MSAL
    participant Agent

    rect rgba(128, 128, 128, .1)
    Note over Teams,Agent: Turn N (user previously signed in)
    Teams->>AgentApplication: Activity (Message)
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>TeamsAgenticAuth: SignInUserAsync
    activate TeamsAgenticAuth
    TeamsAgenticAuth->>MSAL: AcquireTokenSilent(scopes, account)
    activate MSAL
    MSAL-->>TeamsAgenticAuth: AuthenticationResult (cached/refreshed)
    deactivate MSAL
    TeamsAgenticAuth-->>UserAuthorization: TokenResponse
    deactivate TeamsAgenticAuth
    UserAuthorization->>UserAuthorization: Cache token for turn
    UserAuthorization-->>AgentApplication: true (complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route Activity
    activate Agent
    Agent->>UserAuthorization: GetTurnTokenAsync
    UserAuthorization-->>Agent: Token
    Agent->>Teams: Response Activity
    deactivate Agent
    deactivate AgentApplication
    end
```

## First Sign In — Full Interactive Flow

This is the multi-turn interactive flow when no cached token exists.

```mermaid
sequenceDiagram
    participant Teams
    participant AgentApplication
    participant UserAuthorization
    participant TeamsAgenticAuth
    participant MSAL
    participant AzureAD as Azure AD
    participant Callback
    participant Agent

    %% Turn 1: Silent fails, send adaptive card
    rect rgba(128, 128, 128, .1)
    Note over Teams,Agent: Turn 1 — Start interactive flow
    Teams->>AgentApplication: Activity (Message)
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>TeamsAgenticAuth: SignInUserAsync(forceSignIn: true)
    activate TeamsAgenticAuth
    TeamsAgenticAuth->>MSAL: AcquireTokenSilent(scopes, account)
    activate MSAL
    MSAL-->>TeamsAgenticAuth: null (no cached account)
    deactivate MSAL
    TeamsAgenticAuth->>TeamsAgenticAuth: Generate PKCE (verifier + challenge)
    TeamsAgenticAuth->>TeamsAgenticAuth: Generate state nonce
    TeamsAgenticAuth->>TeamsAgenticAuth: Store OAuthCallbackState in IStorage
    Note right of TeamsAgenticAuth: CodeVerifier, ConversationReference,<br/>ConnectionName, Scopes, RedirectUri
    TeamsAgenticAuth->>Teams: Adaptive Card with Azure AD authorize URL
    Note right of TeamsAgenticAuth: URL includes client_id (OAuth app),<br/>redirect_uri, PKCE challenge, state nonce
    TeamsAgenticAuth->>TeamsAgenticAuth: Set FlowState (started, expires)
    TeamsAgenticAuth-->>UserAuthorization: null (pending)
    deactivate TeamsAgenticAuth
    UserAuthorization->>UserAuthorization: Store ContinuationActivity
    UserAuthorization-->>AgentApplication: false (pending)
    deactivate UserAuthorization
    AgentApplication-->>Teams: (turn ends)
    deactivate AgentApplication
    end

    %% User authenticates in browser
    rect rgba(200, 200, 128, .1)
    Note over Teams,AzureAD: User clicks Sign In button
    Teams->>AzureAD: Browser opens authorize URL
    AzureAD->>AzureAD: User authenticates + consents
    AzureAD->>Callback: GET /auth/callback?code=...&state=nonce
    activate Callback
    Callback->>Callback: Look up OAuthCallbackState by nonce
    Callback->>MSAL: AcquireTokenByAuthorizationCode(code, PKCE verifier)
    activate MSAL
    MSAL->>AzureAD: Exchange code for tokens
    AzureAD-->>MSAL: access_token + refresh_token
    MSAL->>MSAL: Cache tokens (in-memory)
    MSAL-->>Callback: AuthenticationResult
    deactivate MSAL
    Callback->>Callback: Delete OAuthCallbackState from IStorage
    Callback->>Callback: Build signin/verifyState invoke with token
    Callback->>AgentApplication: ProcessActivityAsync (local, not to channel)
    deactivate Callback
    end

    %% Turn 2: verifyState invoke processed locally
    rect rgba(170, 128, 128, .1)
    Note over AgentApplication,Agent: Turn 2 — Invoke processed locally
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>TeamsAgenticAuth: SignInUserAsync (continuation)
    activate TeamsAgenticAuth
    TeamsAgenticAuth->>MSAL: AcquireTokenSilent(scopes, account)
    activate MSAL
    MSAL-->>TeamsAgenticAuth: AuthenticationResult (just cached)
    deactivate MSAL
    TeamsAgenticAuth-->>UserAuthorization: TokenResponse
    deactivate TeamsAgenticAuth
    UserAuthorization->>UserAuthorization: Cache token, delete sign-in state
    UserAuthorization->>UserAuthorization: ProcessProactive(ContinuationActivity)
    UserAuthorization-->>AgentApplication: false (proactive dispatched)
    deactivate UserAuthorization
    deactivate AgentApplication
    end

    %% Turn 3: Proactive continuation
    rect rgba(128, 128, 128, .1)
    Note over Agent,Teams: Turn 3 — Proactive Continuation
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>TeamsAgenticAuth: SignInUserAsync
    activate TeamsAgenticAuth
    TeamsAgenticAuth->>MSAL: AcquireTokenSilent(scopes, account)
    activate MSAL
    MSAL-->>TeamsAgenticAuth: AuthenticationResult (cached)
    deactivate MSAL
    TeamsAgenticAuth-->>UserAuthorization: TokenResponse
    deactivate TeamsAgenticAuth
    UserAuthorization->>UserAuthorization: Cache token
    UserAuthorization-->>AgentApplication: true (complete)
    deactivate UserAuthorization
    AgentApplication->>Agent: Route ContinuationActivity
    activate Agent
    Agent->>UserAuthorization: GetTurnTokenAsync
    UserAuthorization-->>Agent: Token
    Agent->>Teams: Response Activity
    deactivate Agent
    deactivate AgentApplication
    end
```

## Sign In Failure (code exchange fails)

When the OAuth callback fails to exchange the authorization code (e.g., expired code, misconfigured app), a `signin/failure` invoke is sent through the local pipeline.

```mermaid
sequenceDiagram
    participant Teams
    participant AgentApplication
    participant UserAuthorization
    participant TeamsAgenticAuth
    participant MSAL
    participant AzureAD as Azure AD
    participant Callback
    participant Agent

    Note over Teams,AzureAD: User clicked Sign In, authenticated with Azure AD

    rect rgba(200, 128, 128, .1)
    Note over Callback,Agent: Callback — code exchange fails
    AzureAD->>Callback: GET /auth/callback?code=...&state=nonce
    activate Callback
    Callback->>Callback: Look up OAuthCallbackState
    Callback->>MSAL: AcquireTokenByAuthorizationCode(code, verifier)
    activate MSAL
    MSAL->>AzureAD: Exchange code
    AzureAD-->>MSAL: Error (e.g., invalid_grant)
    MSAL-->>Callback: throws MsalServiceException
    deactivate MSAL
    Callback->>Callback: Delete OAuthCallbackState from IStorage
    Callback->>Callback: Build signin/failure invoke with error
    Callback->>AgentApplication: ProcessActivityAsync (local)
    deactivate Callback
    end

    rect rgba(170, 128, 128, .1)
    Note over AgentApplication,Agent: Turn — failure invoke processed
    activate AgentApplication
    AgentApplication->>UserAuthorization: StartOrContinueSignInUserAsync
    activate UserAuthorization
    UserAuthorization->>TeamsAgenticAuth: SignInUserAsync (continuation)
    activate TeamsAgenticAuth
    TeamsAgenticAuth->>TeamsAgenticAuth: OnContinueFlow → IsSignInFailureInvoke
    TeamsAgenticAuth-->>UserAuthorization: throws AuthException(InvalidSignIn)
    deactivate TeamsAgenticAuth
    UserAuthorization->>UserAuthorization: Reset state, delete flow
    UserAuthorization->>Agent: OnUserSignInFailure handler
    activate Agent
    Agent->>Teams: Error message to user
    deactivate Agent
    UserAuthorization-->>AgentApplication: false (error handled)
    deactivate UserAuthorization
    deactivate AgentApplication
    end
```

## Sign Out

```mermaid
sequenceDiagram
    participant Agent
    participant UserAuthorization
    participant TeamsAgenticAuth
    participant MSAL

    rect rgba(128, 128, 128, .1)
    Agent->>UserAuthorization: SignOutUserAsync
    activate UserAuthorization
    UserAuthorization->>UserAuthorization: Delete cached turn token
    UserAuthorization->>TeamsAgenticAuth: SignOutUserAsync
    activate TeamsAgenticAuth
    TeamsAgenticAuth->>TeamsAgenticAuth: Delete FlowState from IStorage
    TeamsAgenticAuth->>MSAL: GetAccountAsync(homeAccountId)
    activate MSAL
    MSAL-->>TeamsAgenticAuth: IAccount
    deactivate MSAL
    TeamsAgenticAuth->>MSAL: RemoveAsync(account)
    activate MSAL
    MSAL->>MSAL: Evict tokens from cache
    MSAL-->>TeamsAgenticAuth: (done)
    deactivate MSAL
    TeamsAgenticAuth-->>UserAuthorization: (done)
    deactivate TeamsAgenticAuth
    UserAuthorization-->>Agent: (done)
    deactivate UserAuthorization
    end

    Note over Agent: Next turn will require full interactive sign-in
```
