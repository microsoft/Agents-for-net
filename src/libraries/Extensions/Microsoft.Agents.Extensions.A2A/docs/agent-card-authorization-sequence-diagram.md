# A2A Agent Card Authorization Sequence Diagram

Shows how Agent Card OAuth metadata is composed, how an A2A client acquires the
advertised token, and how the validated request token enters the standard
`AgentApplication.UserAuthorization` pipeline.

## Diagram

```mermaid
sequenceDiagram
    autonumber
    participant Client as A2A Client
    participant CardEndpoint as Agent Card Endpoint
    participant Composer as A2AAgentCardComposer
    participant Config as IConfiguration
    participant App as AgentApplication
    participant OAuth as OAuth Authorization Server
    participant Auth as ASP.NET Core Authentication
    participant Adapter as A2AAdapter
    participant UA as UserAuthorization
    participant A2AUA as A2AUserAuthorization
    participant Exchange as OBO Exchange / IConnections
    participant Route as Protected Agent Route
    participant API as Downstream API

    Client->>CardEndpoint: GET /.well-known/agent-card.json
    CardEndpoint->>Adapter: ProcessAgentCardAsync()
    Adapter->>Adapter: Create host Agent Card defaults
    Adapter->>Composer: ComposeAsync(hostDefaults, agent)
    Composer->>Config: Read AgentApplication:A2A:AgentCard
    Composer->>Config: Resolve UserAuthorization.Handlers
    Composer->>Composer: Add inline OAuth schemes and global requirement
    Composer->>App: Read A2ASkill registrations
    Composer->>Composer: Associate autoSigninHandlers<br/>with skill security requirements

    opt Agent implements IAgentCardHandler
        Composer->>App: GetAgentCard(composedCard)
        App-->>Composer: Customized Agent Card
    end

    Composer->>Composer: Validate schemes, requirements, and scopes
    Composer-->>Adapter: Final Agent Card
    Adapter-->>Client: Agent Card with OAuth scheme<br/>and skill security requirement

    Client->>OAuth: Run advertised OAuth flow<br/>for required scopes
    OAuth-->>Client: JWT access token

    Client->>Auth: A2A request<br/>Authorization: Bearer JWT
    Auth->>Auth: Validate JWT and save access_token
    Auth->>Adapter: Invoke authenticated A2A endpoint
    Adapter->>Adapter: A2ARequestAuthentication.Create(request)<br/>captures validated access token
    Adapter->>App: RunPipelineAsync(message Activity)
    App->>UA: StartOrContinueSignInUserAsync(autoSigninHandlers)
    UA->>A2AUA: GetRefreshedUserTokenAsync(handler)
    A2AUA->>A2AUA: Read validated request token

    opt EnforceRequiredScopes
        A2AUA->>A2AUA: Validate delegated JWT scp claims
    end

    opt OBO scopes configured or requested
        A2AUA->>Exchange: HandleOBO(inbound token, scopes)
        Exchange-->>A2AUA: Downstream TokenResponse
    end

    A2AUA-->>UA: TokenResponse
    UA->>UA: Cache token for the handler and current turn
    UA-->>App: Sign-in complete
    App->>Route: Execute protected route
    Route->>UA: GetTurnTokenAsync(handlerName)
    UA-->>Route: Inbound or exchanged token

    opt Route calls a downstream API
        Route->>API: Authorized API request
        API-->>Route: API response
    end

    Route-->>Adapter: Send task status or artifact updates
    Adapter-->>Client: A2A response
```

## Key Components

| Component | Location |
| --- | --- |
| Agent Card endpoint registration | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AServiceExtensions.cs` |
| `A2AAdapter.ProcessAgentCardAsync` | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2AAdapter.cs` |
| `A2AAgentCardComposer` | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AgentCard/A2AAgentCardComposer.cs` |
| `A2AAuthorizationMetadata` | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Authorization/A2AAuthorizationMetadata.cs` |
| `A2ARequestAuthentication` | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Authorization/A2ARequestAuthentication.cs` |
| `A2AUserAuthorization` | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Authorization/A2AUserAuthorization.cs` |
| `A2ASkillAttribute` (`autoSigninHandlers`) | `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2ASkillAttribute.cs` |

## Important Behavior

- The handler's inline OAuth flow contributes the Agent Card security scheme;
  `SecuritySchemeName` defaults to the handler name.
- `A2ASkillAttribute.AutoSignInHandlers` associates the authorization handler
  with the skill's security requirement.
- The request carries one authorization token. The built-in path requires a
  token that ASP.NET Core can validate and expose to the A2A handler.
- Use in-task authorization when a route needs multiple user tokens or an
  opaque provider token that cannot use the JWT request-authentication path.
