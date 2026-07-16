# Detailed OAuth Internal Sequence Diagram

Shows the detailed internal flow from `AgentApplication` through `UserAuthorization`, `UserAuthorizationDispatcher`, `AzureBotUserAuthorization`, `OAuthFlow`, and `IUserTokenClient`. This complements the high-level `teams-sso-sequence-diagram.md` with implementation-level detail.

## Participants

- **AgentApplication** — SDK entry point; calls `UserAuthorization.StartOrContinueSignInUserAsync` each turn.
- **UserAuthorization** — Manages sign-in state (continuation activity, active handler), caches tokens, handles OBO exchange dispatch.
- **UserAuthorizationDispatcher** — Routes to named `IUserAuthorization` instances (loaded from config or DI).
- **AzureBotUserAuthorization** — Implements `IUserAuthorization` for Azure Bot Token Service. Manages `FlowState` in `IStorage`.
- **OAuthFlow** — Low-level protocol logic: `BeginFlowAsync` / `ContinueFlowAsync`. Handles OAuthCard sending, token exchange, magic code, and invoke responses.
- **UserTokenClientWrapper** — Static façade over `IUserTokenClient` (resolved from `ITurnContext.Services`).
- **IUserTokenClient** — Interface to Azure Token Service (implemented by connector layer).
- **IStorage** — Persists `FlowState` (flow started, expires, continue count) and sign-in state.
- **OBOExchange** — Base class for On-Behalf-Of token exchange via `IConnections`.

## Flow 1: First Turn — Token Cached (Already Signed In)

```mermaid
sequenceDiagram
    participant App as AgentApplication
    participant UA as UserAuthorization
    participant Disp as UserAuthorizationDispatcher
    participant ABUA as AzureBotUserAuthorization
    participant Flow as OAuthFlow
    participant Wrapper as UserTokenClientWrapper
    participant Client as IUserTokenClient
    participant Storage as IStorage

    App->>UA: StartOrContinueSignInUserAsync(turnContext, turnState)
    activate UA
    UA->>UA: GetSignInStateAsync() → no active handler
    UA->>UA: AutoSignIn selector → true

    UA->>Disp: SignUserInAsync(context, handlerName, forceSignIn=true)
    activate Disp
    Disp->>ABUA: SignInUserAsync(context, forceSignIn=true)
    activate ABUA

    ABUA->>ABUA: IsValidActivity() → true (Message)
    ABUA->>ABUA: GetFlowStateAsync()
    ABUA->>Storage: ReadAsync(["oauth/{name}/{channel}/{conv}/flowState"])
    Storage-->>ABUA: FlowState { FlowStarted=false }

    Note over ABUA: FlowStarted=false → OnGetOrStartFlowAsync

    ABUA->>Flow: BeginFlowAsync(context, null)
    activate Flow
    Flow->>Wrapper: GetTokenOrSignInResourceAsync(context, connectionName)
    Wrapper->>Client: GetTokenOrSignInResourceAsync(connectionName, activity)
    Client-->>Wrapper: TokenOrSignInResourceResponse { TokenResponse ≠ null }
    Wrapper-->>Flow: response.TokenResponse
    Flow-->>ABUA: TokenResponse
    deactivate Flow

    Note over ABUA: Token found → no flow started

    ABUA->>ABUA: SaveFlowStateAsync (unchanged)
    ABUA->>ABUA: HandleOBO(context, token, ...)
    activate ABUA
    Note over ABUA: If OBOScopes configured:<br/>AcquireTokenOnBehalfOf()
    ABUA-->>ABUA: TokenResponse (OBO or original)
    deactivate ABUA

    ABUA-->>Disp: TokenResponse
    deactivate ABUA
    Disp-->>UA: SignInResponse { Status=Complete, TokenResponse }
    deactivate Disp

    UA->>UA: DeleteSignInStateAsync()
    UA->>UA: CacheToken(handlerName, tokenResponse)
    UA-->>App: true (sign-in complete)
    deactivate UA

    Note over App: Proceeds to route Activity to Agent handler
```

## Flow 2: First Turn — Token Not Cached (Flow Starts)

```mermaid
sequenceDiagram
    participant App as AgentApplication
    participant UA as UserAuthorization
    participant Disp as UserAuthorizationDispatcher
    participant ABUA as AzureBotUserAuthorization
    participant Flow as OAuthFlow
    participant Wrapper as UserTokenClientWrapper
    participant Client as IUserTokenClient
    participant Storage as IStorage
    participant Channel

    App->>UA: StartOrContinueSignInUserAsync(turnContext, turnState)
    activate UA
    UA->>UA: GetSignInStateAsync() → no active handler
    UA->>UA: AutoSignIn selector → true

    UA->>Disp: SignUserInAsync(context, handlerName, forceSignIn=true)
    activate Disp
    Disp->>ABUA: SignInUserAsync(context, forceSignIn=true)
    activate ABUA

    ABUA->>ABUA: IsValidActivity() → true
    ABUA->>Storage: ReadAsync(flowState key)
    Storage-->>ABUA: FlowState { FlowStarted=false }

    ABUA->>Flow: BeginFlowAsync(context, null)
    activate Flow
    Flow->>Wrapper: GetTokenOrSignInResourceAsync(context, connectionName)
    Wrapper->>Client: GetTokenOrSignInResourceAsync(connectionName, activity)
    Client-->>Wrapper: TokenOrSignInResourceResponse { TokenResponse=null, SignInResource }
    Wrapper-->>Flow: response (no token)

    Note over Flow: No token → send OAuthCard

    Flow->>Flow: SendOAuthCardAsync(context, signInResource)
    Flow->>Channel: SendActivityAsync(OAuthCard attachment)
    Flow-->>ABUA: null (no token)
    deactivate Flow

    ABUA->>ABUA: FlowState.FlowStarted = true<br/>FlowState.FlowExpires = now + Timeout
    ABUA->>Storage: WriteAsync(flowState)
    ABUA-->>Disp: null
    deactivate ABUA
    Disp-->>UA: SignInResponse { Status=Pending }
    deactivate Disp

    UA->>UA: signInState.ContinuationActivity = turnContext.Activity
    UA->>UA: signInState.ActiveHandler = handlerName
    UA->>UA: SetSignInStateAsync(context, signInState)
    UA-->>App: false (sign-in pending)
    deactivate UA

    Note over App: Turn ends — no route executed
```

## Flow 3: Continuation Turn — Token Exchange (signin/tokenExchange)

```mermaid
sequenceDiagram
    participant App as AgentApplication
    participant UA as UserAuthorization
    participant Disp as UserAuthorizationDispatcher
    participant ABUA as AzureBotUserAuthorization
    participant Flow as OAuthFlow
    participant Wrapper as UserTokenClientWrapper
    participant Client as IUserTokenClient
    participant Storage as IStorage
    participant Channel

    Note over App: Teams sends Invoke(signin/tokenExchange)

    App->>UA: StartOrContinueSignInUserAsync(turnContext, turnState)
    activate UA
    UA->>UA: GetSignInStateAsync() → ActiveHandler = handlerName
    Note over UA: flowContinuation = true

    UA->>Disp: SignUserInAsync(context, handlerName, forceSignIn=false)
    activate Disp
    Disp->>ABUA: SignInUserAsync(context, forceSignIn=false)
    activate ABUA

    ABUA->>ABUA: IsValidActivity() → true (Invoke + tokenExchange)
    ABUA->>Storage: ReadAsync(flowState key)
    Storage-->>ABUA: FlowState { FlowStarted=true, FlowExpires }

    Note over ABUA: FlowStarted=true → OnContinueFlow

    ABUA->>Flow: ContinueFlowAsync(context, expires)
    activate Flow
    Flow->>Flow: HasTimedOut() → false
    Flow->>Flow: RecognizeTokenAsync(context)

    Note over Flow: IsTokenExchangeRequestInvoke = true

    Flow->>Flow: Deserialize TokenExchangeInvokeRequest
    Flow->>Wrapper: ExchangeTokenAsync(context, connectionName, exchangeRequest)
    Wrapper->>Client: ExchangeTokenAsync(userId, connectionName, channelId, request)

    alt Exchange succeeds
        Client-->>Wrapper: TokenResponse
        Wrapper-->>Flow: TokenResponse
        Flow->>Channel: SendInvokeResponseAsync(200, { Id, ConnectionName })
        Flow-->>ABUA: OAuthFlowResult.Complete(tokenResponse)
    else ConsentRequired (ErrorResponseException)
        Client-->>Wrapper: throws ErrorResponseException (ConsentRequired)
        Wrapper-->>Flow: throws
        Flow->>Channel: SendInvokeResponseAsync(412, { FailureDetail })
        Flow-->>ABUA: OAuthFlowResult.Pending
    else Critical error
        Client-->>Wrapper: throws ErrorResponseException (other)
        Wrapper-->>Flow: throws
        Flow->>Channel: SendInvokeResponseAsync(400, { FailureDetail })
        Flow-->>ABUA: throws ErrorResponseException
    end
    deactivate Flow

    alt Complete
        ABUA->>ABUA: FlowState.FlowStarted = false
        ABUA->>Storage: WriteAsync(flowState)
        ABUA->>ABUA: HandleOBO(context, token)
        ABUA-->>Disp: TokenResponse
        Disp-->>UA: SignInResponse { Status=Complete }

        UA->>UA: DeleteSignInStateAsync()
        UA->>UA: CacheToken(handlerName, tokenResponse)

        Note over UA: ContinuationActivity ≠ current Activity

        UA->>App: ProcessProactiveAsync(continuationActivity)
        UA-->>App: false (current Invoke turn ends)
    else Pending (ConsentRequired)
        ABUA->>Storage: WriteAsync(flowState — unchanged)
        ABUA-->>Disp: null
        Disp-->>UA: SignInResponse { Status=Pending }
        UA-->>App: false (waiting for consent)
    else Error
        ABUA->>ABUA: throws
        Disp-->>UA: SignInResponse { Status=Error, Cause }
        UA->>UA: DeleteSignInStateAsync()
        UA->>UA: Call _userSignInFailureHandler
        UA-->>App: false
    end
    deactivate ABUA
    deactivate Disp
    deactivate UA
```

## Flow 4: Continuation Turn — Verify State (signin/verifyState with magic code)

```mermaid
sequenceDiagram
    participant Flow as OAuthFlow
    participant Wrapper as UserTokenClientWrapper
    participant Client as IUserTokenClient
    participant Channel

    Note over Flow: RecognizeTokenAsync — IsVerificationInvoke = true

    Flow->>Flow: Extract magic code from Activity.Value["state"]

    alt Code = "cancelledByUser"
        Flow->>Channel: SendInvokeResponseAsync(200, null)
        Flow-->>Flow: OAuthFlowResult.UserCancelled
    else Valid magic code
        Flow->>Wrapper: GetUserTokenAsync(context, connectionName, magicCode)
        Wrapper->>Client: GetUserTokenAsync(userId, connectionName, channelId, magicCode)
        alt Token returned
            Client-->>Wrapper: TokenResponse
            Wrapper-->>Flow: TokenResponse
            Flow->>Channel: SendInvokeResponseAsync(200, null)
            Flow-->>Flow: OAuthFlowResult.Complete(tokenResponse)
        else null (invalid code)
            Client-->>Wrapper: null
            Wrapper-->>Flow: null
            Flow->>Channel: SendInvokeResponseAsync(404, null)
            Flow-->>Flow: OAuthFlowResult.Pending
        else Exception
            Flow->>Channel: SendInvokeResponseAsync(500, null)
            Flow-->>Flow: OAuthFlowResult.Pending
        end
    end
```

## Flow 5: GetTurnTokenAsync (Agent retrieves cached token)

```mermaid
sequenceDiagram
    participant Agent
    participant UA as UserAuthorization
    participant Disp as UserAuthorizationDispatcher
    participant ABUA as AzureBotUserAuthorization
    participant Wrapper as UserTokenClientWrapper
    participant Client as IUserTokenClient

    Agent->>UA: GetTurnTokenAsync(turnContext, handlerName)
    activate UA
    UA->>UA: Find cached HandlerToken

    alt Token not expired & not exchangeable & not an agentic request
        UA-->>Agent: token.Token
    else Token expired or exchangeable
        UA->>Disp: Get(handlerName)
        Disp-->>UA: IUserAuthorization instance
        UA->>ABUA: GetRefreshedUserTokenAsync(context, exchangeConn, scopes)
        activate ABUA
        ABUA->>Wrapper: GetUserTokenAsync(context, connectionName, null)
        Wrapper->>Client: GetUserTokenAsync(userId, connectionName, channelId, null)
        Client-->>Wrapper: TokenResponse
        Wrapper-->>ABUA: TokenResponse
        ABUA->>ABUA: HandleOBO(context, token, ...)
        ABUA-->>UA: TokenResponse (refreshed/exchanged)
        deactivate ABUA
        UA->>UA: CacheToken(handlerName, response)
        UA-->>Agent: response.Token
    end
    deactivate UA
```

## Key Classes & Responsibilities

| Class | Responsibility | Path |
|-------|---------------|------|
| `UserAuthorization` | Sign-in state machine, token cache, proactive continuation | `src/libraries/Builder/Microsoft.Agents.Builder/App/UserAuth/UserAuthorization.cs` |
| `UserAuthorizationDispatcher` | Named handler registry, type loading from config | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/UserAuthorizationDispatcher.cs` |
| `AzureBotUserAuthorization` | `IUserAuthorization` for Azure Token Service, FlowState persistence | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/AzureBotUserAuthorization.cs` |
| `OAuthFlow` | Protocol-level: OAuthCard, token exchange, magic code, invoke responses | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/OAuthFlow.cs` |
| `UserTokenClientWrapper` | Static façade over `IUserTokenClient` from turn services | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/UserTokenClientWrapper.cs` |
| `IUserTokenClient` | Azure Token Service interface (GetToken, Exchange, SignOut) | `src/libraries/Client/Microsoft.Agents.Connector/IUserTokenClient.cs` |
| `OBOExchange` | On-Behalf-Of exchange via `IConnections` / `IOBOExchange` | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/OBOExchange.cs` |
| `FlowState` | Persisted state: FlowStarted, FlowExpires, ContinueCount | `src/libraries/Builder/Microsoft.Agents.Builder/UserAuth/TokenService/AzureBotUserAuthorization.cs` |

## Storage Keys

- **FlowState**: `oauth/{handlerName}/{channelId}/{conversationId}/flowState`
- **SignInState**: Stored by `UserAuthorization` in configured `IStorage` under `oauth/{channelId}/{userId}/userAuthorizationState`

## Important Behaviors

- **AutoSignIn**: If enabled (default), `StartOrContinueSignInUserAsync` is called every turn before route matching. If the user has a cached token, this is a fast path (single Token Service call).
- **Continuation Activity**: When a multi-turn flow starts, the original user message is banked. After sign-in completes on a different turn (Invoke), the banked activity is replayed via `ProcessProactiveAsync`.
- **Invoke Response Codes**: `200` = success, `412` = consent required (Teams retries with consent), `400` = critical failure (Teams stops), `404` = invalid magic code, `500` = retriable error.
- **Timeout**: `OAuthSettings.Timeout` (default from `OAuthSettings.DefaultTimeoutValue`). After expiry, all invokes return errors.
- **InvalidSignInRetryMax**: Non-tokenExchange continuations (e.g., bad magic codes) are retried up to this limit before throwing `AuthExceptionReason.InvalidSignIn`.
- **OBO**: Performed after every successful token acquisition (BeginFlow cached token, ContinueFlow token, GetRefreshedUserToken). Uses `IConnections` to resolve an `IOBOExchange` provider.
