# Develop A2A agents with the Microsoft 365 Agents SDK

`Microsoft.Agents.Extensions.A2A` lets an existing Agents SDK application expose
Agent2Agent (A2A) endpoints while continuing to use `AgentApplication` routing,
middleware, state, storage, and user authorization.

> [!IMPORTANT]
> The A2A extension is in preview. This guide is the repository source for
> developer guidance that is expected to move to Microsoft Learn before the
> extension exits preview.

The extension uses `a2a-dotnet` for the native protocol surface. It is intended
for developers who want one Agents SDK application to participate in A2A
scenarios, not as a replacement for using `a2a-dotnet` directly to build a
standalone A2A server.

## Add A2A to an existing agent

Add a package reference to `Microsoft.Agents.Extensions.A2A`, import the root
namespace, and annotate the agent:

```csharp
using Microsoft.Agents.Extensions.A2A;

[Agent(name: "MyAgent", description: "Agent with A2A support")]
[A2AExtension]
[AgentInterface(A2AAgentTransportProtocol.JsonRpc, "/a2a")]
[AgentInterface(A2AAgentTransportProtocol.HttpJson, "/a2a")]
public partial class MyAgent : AgentApplication
{
}
```

`[A2AExtension]` generates the `A2AExtension` property used by the fluent routing
API. The `Agent` and `AgentInterface` attributes provide metadata used to compose
the Agent Card.

Register the agent normally and map the A2A endpoints:

```csharp
builder.AddAgentDefaults()
    .AddAgent<MyAgent>();

WebApplication app = builder.Build();
app.UseAgents();
app.MapDefaultAgentEndpoints();
app.MapA2AApplicationEndpoints();
```

`MapA2AApplicationEndpoints` maps the well-known Agent Card and the configured
JSON-RPC and HTTP+JSON interfaces. The default path is `/a2a`.

## Generate the Agent Card

The extension composes the Agent Card from several sources:

1. the A2A host supplies protocol-required values, including the endpoint,
   supported interfaces, capabilities, and protocol version;
1. `AgentApplication:A2A:AgentCard` supplies application-owned descriptive
   metadata and reusable security schemes;
1. `AgentApplication:UserAuthorization:Handlers` contributes OAuth schemes and
   security requirements;
1. `A2ASkill` registrations contribute skills and their route-level security
   requirements;
1. `IAgentCardHandler`, when implemented, receives the composed card for final
   customization; and
1. the extension validates the final card before returning it to the client.

For normal development:

- use `A2ASkill` attributes or the equivalent fluent API to define skills;
- use `AgentApplication:UserAuthorization:Handlers` to define OAuth behavior and
  associate security requirements with routes; and
- use `AgentApplication:A2A:AgentCard` for descriptive metadata and for a shared
  security-scheme catalog.

These mechanisms keep the advertised Agent Card aligned with the routes and
authorization handlers that the application actually uses.

### Define Agent Card metadata in appsettings

The application can configure the application-owned portions of the Agent Card
under `AgentApplication:A2A:AgentCard`:

```json
{
  "AgentApplication": {
    "A2A": {
      "AgentCard": {
        "Name": "Weather agent",
        "Description": "Provides current conditions and forecasts.",
        "DocumentationUrl": "https://example.com/weather-agent",
        "IconUrl": "https://example.com/weather-agent/icon.png",
        "SecuritySchemes": {
          "delegated": {
            "OAuth2SecurityScheme": {
              "Flows": {
                "DeviceCode": {
                  "DeviceAuthorizationUrl": "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/devicecode",
                  "TokenUrl": "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token",
                  "Scopes": {
                    "api://botid-<agent-app-id>/weather.read": "Read weather data."
                  }
                }
              }
            }
          }
        }
      }
    }
  }
}
```

The supported application-owned settings are `Name`, `Description`,
`DocumentationUrl`, `IconUrl`, `Provider`, and `SecuritySchemes`. The OAuth
section later in this guide shows how authorization handlers reference a
configured security scheme and add agent-level or skill-level requirements.

Do not configure host-owned properties such as `Version`, `SupportedInterfaces`,
`Endpoint`, `Endpoints`, `Url`, or `ProtocolVersion`. The extension rejects
these settings because the host must generate values that match the endpoints
and protocol surface it serves.

### Use `IAgentCardHandler` as a failsafe

Implement `Microsoft.Agents.Extensions.A2A.AgentCard.IAgentCardHandler` only when
the required final customization cannot be expressed through attributes, the
fluent skill API, authorization handlers, or appsettings:

```csharp
using Microsoft.Agents.Extensions.A2A.AgentCard;

public partial class MyAgent : AgentApplication, IAgentCardHandler
{
    public Task<A2A.AgentCard> GetAgentCard(A2A.AgentCard agentCard)
    {
        agentCard.Version =
            typeof(MyAgent).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        return Task.FromResult(agentCard);
    }
}
```

The handler runs after the host, appsettings, authorization handlers, and
`A2ASkill` registrations have contributed their values. Mutate and return the
provided card rather than constructing a replacement that loses generated
endpoints, capabilities, skills, or security metadata. The extension validates
the returned card, including security-scheme references and required OAuth
scopes.

Treat this interface as an escape hatch, not the standard Agent Card authoring
model. In particular, prefer `A2ASkill` for skills and
`UserAuthorization.Handlers` for OAuth so runtime behavior and advertised
metadata remain correlated.

## Advertise and implement skills

`A2ASkill` is the recommended way to define skills. It combines Agent Card skill
metadata with the route that implements the skill:

```csharp
[A2ASkill(
    name: "Weather",
    description: "Gets the weather for a city.",
    tags: "weather,forecast",
    examples: "What is the weather in Seattle?",
    textRegex: "(?i)weather")]
private Task OnWeatherAsync(
    IA2ATurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    // Handle the request.
}
```

The equivalent fluent API is available through the generated extension:

```csharp
A2AExtension.Skill("Weather", skill => skill
    .WithName("Weather")
    .WithDescription("Gets the weather for a city.")
    .WithTags("weather", "forecast")
    .WithExamples("What is the weather in Seattle?")
    .OnMessage(new Regex("(?i)weather"), OnWeatherAsync));
```

Skill names, descriptions, tags, and examples help A2A clients decide which
remote agent and skill may satisfy a request. They do not force the server to use
that skill. The server still evaluates its registered routes when it receives the
message.

Use `A2AMessageRoute` or ordinary `AgentApplication` routing for A2A routes that
should not be advertised as Agent Card skills.

## How A2A maps to AgentApplication

An inbound A2A message is converted to an Activity Protocol activity and passed
through the normal `AgentApplication` pipeline:

- the activity type is `ActivityTypes.Message`;
- `Activity.ChannelId` is `a2a`;
- text parts are appended to `Activity.Text`;
- other parts become attachments; and
- `Activity.ChannelData` contains the A2A task.

The extension maps outbound activities back to A2A:

- `ActivityTypes.Message` becomes A2A message or task-status content;
- attachments, entities, and `Activity.Value` become A2A parts;
- `ActivityTypes.EndOfConversation` completes, fails, or cancels the task; and
- `ITurnContext.StreamingResponse` produces streaming task updates and artifacts.

All current Agents SDK interactions occur in the context of an A2A task. The
task ID is used as `Activity.Conversation.Id`, so conversation state is scoped to
the task.

For multi-turn behavior, send a response with
`Activity.InputHint == InputHints.ExpectingInput`. This moves the A2A task to
`input-required`. Send `ActivityTypes.EndOfConversation` when the task is
complete:

```csharp
await turnContext.SendActivityAsync(
    new Activity
    {
        Type = ActivityTypes.EndOfConversation,
        Code = EndOfConversationCodes.CompletedSuccessfully,
        Text = "Completed.",
        Value = result,
    },
    cancellationToken: cancellationToken);
```

`EndOfConversationCodes.Error` maps to a failed task,
`EndOfConversationCodes.UserCancelled` maps to a canceled task, and other codes
map to a completed task.

Use `IA2ATurnContext.Client` when a route needs the native A2A request context,
event queue, or task store.

## OAuth

A2A OAuth uses the same `AgentApplication.UserAuthorization` model as other
channels:

1. Define named handlers under
   `AgentApplication:UserAuthorization:Handlers`.
1. Associate one or more handlers with a route by setting
   `autoSigninHandlers` on `A2ASkillAttribute` or the equivalent fluent route.
1. Retrieve the handler's token with `UserAuthorization.GetTurnTokenAsync`.
1. Use `UserAuthorization.ExchangeTurnTokenAsync` when the route needs to
   explicitly exchange a delegated token for a downstream resource.

`A2AUserAuthorization` is defined by the A2A extension, so each handler must set
`Assembly` to `Microsoft.Agents.Extensions.A2A`. The handler can obtain the
credential from either the authenticated A2A request or the A2A in-task
authorization extension.

### Choose an authorization method

| Method | Use when | Tradeoffs |
| --- | --- | --- |
| Agent Card authorization | The client should discover OAuth from the standard Agent Card and send the token with the request. | Most compatible with the A2A specification and other A2A implementations. Each request carries one authorization token, and the built-in path expects a JWT that ASP.NET Core can validate. |
| In-task authorization | The route may need one or more user tokens after the A2A task has started. | Requires support for the Agents SDK A2A in-task authorization extension. It matches `AgentApplication.UserAuthorization` more closely, can request multiple handlers during one task, and can carry opaque tokens. |

### Agent Card authorization

Agent Card authorization is the most interoperable option. The Agent Card
advertises the OAuth scheme, and `A2ASkillAttribute.AutoSignInHandlers`
associates a handler with the skill's security requirement. The client acquires
the required token before invoking the skill and sends that token in the
standard HTTP `Authorization` header.

#### Simple delegated authorization

The handler can define its OAuth scheme inline:

```json
"graph": {
  "Assembly": "Microsoft.Agents.Extensions.A2A",
  "Type": "A2AUserAuthorization",
  "Settings": {
    "OAuthFlows": {
      "DeviceCode": {
        "DeviceAuthorizationUrl": "https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/devicecode",
        "TokenUrl": "https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/token",
        "Scopes": {
          "api://botid-{{ClientId}}/access_as_user": "Access the A2A Agent API as the signed-in user."
        }
      }
    },
    "EnforceRequiredScopes": true,
    "OBOConnectionName": "ServiceConnection",
    "OBOScopes": [
      "User.Read"
    ]
  }
}
```

When an inline flow is present, `SecuritySchemeName` defaults to the handler
name (`graph`) and `RequiredScopes` defaults to every key in the flow's
`Scopes` dictionary. Set `RequiredScopes` explicitly to select a subset, or set
it to an empty array to advertise no required scopes.

Associate the handler with the skill and retrieve the token through the normal
user-authorization API:

```csharp
[A2ASkill(
    name: "Microsoft Graph profile",
    description: "Reads the signed-in user's profile.",
    tags: "a2a,profile,graph",
    examples: "-me",
    text: "-me",
    autoSigninHandlers: "graph")]
private async Task OnGraphAsync(
    IA2ATurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    string graphToken = await UserAuthorization.GetTurnTokenAsync(
        turnContext,
        "graph",
        cancellationToken);

    GraphProfile profile = await _graphClient.GetMeAsync(
        graphToken,
        cancellationToken);
}
```

Because `OBOScopes` is configured, `GetTurnTokenAsync` exchanges the inbound
delegated token and returns a Microsoft Graph token. A route can instead call
`ExchangeTurnTokenAsync` to provide the connection or downstream scopes at
runtime.

#### Interoperability and opaque tokens

This method is closest to the A2A specification, but it provides only one token
per request. The token advertised by the selected Agent Card security scheme is
also the token sent in the request's `Authorization` header.

The built-in request-token path relies on ASP.NET Core authentication to
validate that credential and on JWT claims for optional scope enforcement.
Opaque access tokens, such as tokens issued by providers including GitHub or
LinkedIn, cannot be decoded or validated by this JWT-based path. There is also
no second standard request header in which to carry a separate opaque user
token while retaining a JWT for A2A request authentication. Use in-task
authorization when the route must accept an opaque token or obtain multiple
tokens.

#### Advanced Agent Card schemes

`A2AUserAuthorizationSettings.SecuritySchemeName` can reference OAuth security
information defined separately from the handler. This is useful when the
application centrally owns the Agent Card or multiple handlers reference one
published scheme.

Define the scheme under
`AgentApplication:A2A:AgentCard:SecuritySchemes`, then configure the handler
without `OAuthFlows`:

```json
{
  "AgentApplication": {
    "A2A": {
      "AgentCard": {
        "SecuritySchemes": {
          "delegated": {
            "OAuth2SecurityScheme": {
              "Flows": {
                "DeviceCode": {
                  "DeviceAuthorizationUrl": "https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/devicecode",
                  "TokenUrl": "https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/token",
                  "Scopes": {
                    "api://botid-{{ClientId}}/access_as_user": "Access the A2A Agent API as the signed-in user."
                  }
                }
              }
            }
          }
        }
      }
    },
    "UserAuthorization": {
      "Handlers": {
        "graph": {
          "Assembly": "Microsoft.Agents.Extensions.A2A",
          "Type": "A2AUserAuthorization",
          "Settings": {
            "SecuritySchemeName": "delegated",
            "RequiredScopes": [
              "api://botid-{{ClientId}}/access_as_user"
            ]
          }
        }
      }
    }
  }
}
```

`SecuritySchemeName` must match a scheme in the composed Agent Card.
Applications that generate or modify the catalog in code can provide the same
scheme through `IAgentCardHandler`. Treat `IAgentCardHandler` as a final
composition hook: preserve the endpoint, capabilities, skills, and security
metadata already contributed by the host, configuration, handlers, and
`A2ASkillAttribute` registrations.

### In-task authorization

In-task authorization requires client and server support for the Agents SDK A2A
in-task authorization extension. Set the handler mode to `InTask`:

```json
"graph": {
  "Assembly": "Microsoft.Agents.Extensions.A2A",
  "Type": "A2AUserAuthorization",
  "Settings": {
    "Mode": "InTask",
    "OAuthFlows": {
      "DeviceCode": {
        "DeviceAuthorizationUrl": "https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/devicecode",
        "TokenUrl": "https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/token",
        "Scopes": {
          "User.Read": "Read the signed-in user's profile."
        }
      }
    }
  }
}
```

When a route needs the handler, the task transitions to
`TASK_STATE_AUTH_REQUIRED` and publishes the handler's `OAuthFlows` and
`RequiredScopes` in task status metadata. The client acquires the token and
calls `resumeAuth`. The standard `Authorization` header continues to carry the
JWT that authenticates the A2A request; the acquired user token is sent
separately as the raw `x-a2a-intask-authorization` header value.

The A2A extension converts `resumeAuth` to an event Activity whose value
contains both the access token and the raw A2A message, then resumes the normal
`AgentApplication.UserAuthorization` pipeline. A route with multiple
`autoSigninHandlers` can complete the handlers separately, allowing multiple
tokens during one A2A task.

Because the user token is separate from request authentication, it can be an
opaque provider token. `GetTurnTokenAsync` returns it unchanged when no OBO
scopes are configured. Provider-specific exchange and authorization rules still
apply: built-in `EnforceRequiredScopes` supports delegated JWT `scp` claims,
and a downstream OBO exchange must support the supplied token.

## Authorization setting reference

| Setting | Meaning | When omitted |
| --- | --- | --- |
| `Mode` | `RequestToken` uses the credential associated with the A2A request. `InTask` emits an auth-required task status and accepts the acquired credential through `resumeAuth`. | Defaults to `RequestToken`. |
| `SecuritySchemeName` | Identifies the Agent Card scheme used by a `RequestToken` handler. With `OAuthFlows`, the handler defines that scheme inline. Without `OAuthFlows`, it references an existing scheme contributed through configuration or `IAgentCardHandler`. | With inline `OAuthFlows`, defaults to the handler name. Without inline flows, the handler contributes no Agent Card security metadata. |
| `OAuthFlows` | Defines one OAuth flow. In `RequestToken` mode it can define an inline Agent Card scheme. In `InTask` mode it is returned in auth-required task metadata. | In `RequestToken` mode, an explicit `SecuritySchemeName` references an existing Agent Card scheme. |
| `RequiredScopes` | Scopes placed in a generated Agent Card requirement for `RequestToken`, or in auth-required task metadata for `InTask`. When `EnforceRequiredScopes` is enabled, the same list is validated against the delegated JWT's `scp` claim before OBO. | Defaults to all scope keys advertised by `OAuthFlows`. An explicit empty array disables inferred requirements. |
| `EnforceRequiredScopes` | Requires every `RequiredScopes` value in the delegated JWT's `scp` claim. Microsoft Entra resource-qualified scopes also match by their final permission segment, such as `access_as_user`. | Defaults to `false`. Application tokens, opaque tokens, and provider-specific authorization require application logic. |
| `OBOConnectionName` | Connection used for an OBO exchange. | The default connection selected for the turn is used when an exchange is requested. |
| `OBOScopes` | Downstream scopes requested during OBO. | No automatic OBO exchange occurs, and the inbound token is returned unchanged. |

### `Scopes`, `RequiredScopes`, and `OBOScopes`

These settings describe different stages:

- `OAuthFlows.<flow>.Scopes` advertises the scopes available from the
  authorization server.
- `RequiredScopes` identifies the scopes required by the Agent Card skill or
  in-task authorization request. When omitted, it includes every advertised
  OAuth flow scope.
- `EnforceRequiredScopes` optionally validates every required scope against a
  delegated JWT's `scp` claim before OBO.
- `OBOScopes` identifies scopes requested from a downstream service after the
  request reaches the agent.

An application token represents a service, workload, or another agent. For
Microsoft Entra Client Credentials, the acquisition scope is commonly
`api://botid-<agent-app-id>/.default`, and the resulting token carries application
permissions in `roles` rather than delegated permissions in `scp`.
`EnforceRequiredScopes` does not authorize these tokens because it validates
delegated `scp` claims only.

## How clients use skill security

An A2A client can compare a user's request with the skills advertised by known
Agent Cards. Once it predicts a server and skill, it can inspect that skill's
security requirements before sending the request.

The sample client uses a lightweight deterministic matcher:

1. exact comparison with advertised skill examples;
1. weighted term overlap across the skill name, description, tags, and examples;
1. agent-level plus selected-skill security requirements; and
1. Device Code or Client Credentials selection from the referenced scheme.

The client sends the original input unchanged. Skill selection is a client-side
prediction and does not constrain server routing. Tied matches are not guessed,
and task continuations retain the selection made when the task began.

After the client selects a skill, it resolves both Agent Card authorization and
in-task authorization through the same local provider catalog. Advertised
scheme names identify the remote security entry, but they do not select local
provider or registration IDs. Local matching is deterministic over the
advertised flow, metadata URL, and endpoints, and equal-rank provider matches
fail explicitly instead of falling back to an arbitrary choice.

## Current preview limitations

- The server extension implements the Agents SDK in-task authorization
  extension; arbitrary third-party A2A protocol extensions are not yet
  dynamically registered.
- A2A push notifications are not implemented by the server extension.
- `Adapter.ContinueConversation`, `Adapter.CreateConversation`,
  `Adapter.UpdateActivity`, and `Adapter.DeleteActivity` are not implemented for
  A2A.
- Message-only interactions are not supported; all current interactions use an
  A2A task.
- `ITurnState.User` requires a usable user identity. Application tokens and
  anonymous requests do not provide one.
- Multiple A2A agents in one host are not supported; requests are routed to the
  registered `IAgent`.

## Design history and further reading

- [A2AAgent sample](README.md)
- [A2AClient sample](../A2AClient/README.md)
- [Request-token OAuth and OBO design](https://github.com/microsoft/Agents-for-net/issues/981)
- [Agent Card generation and skill correlation design](https://github.com/microsoft/Agents-for-net/issues/1010)
- [A2A protocol specification](https://a2a-protocol.org/latest/specification/)
