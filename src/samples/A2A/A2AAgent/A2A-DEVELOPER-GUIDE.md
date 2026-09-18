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
                  "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                  "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                  "Scopes": {
                    "api://<agent-app-id>/weather.read": "Read weather data."
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

## OAuth and Agent Card security

An A2A client uses the Agent Card to discover how to acquire an Agent API token.
ASP.NET Core validates the incoming token. `A2AUserAuthorization` then makes the
validated request token available through the same `AgentApplication.UserAuthorization`
API used by other channels and can perform an OBO exchange for a downstream API.
Because `A2AUserAuthorization` is defined by an extension assembly, each handler
configuration must set `Assembly` to `Microsoft.Agents.Extensions.A2A`. Only
handlers defined by `Microsoft.Agents.Builder` can omit `Assembly`.

These responsibilities are separate:

1. **Agent Card metadata** tells a client which scheme and scopes to use.
1. **ASP.NET Core authentication** validates the inbound credential.
1. **Route authorization** enforces the claims, scopes, roles, or policies
   required by the application.
1. **User authorization handlers** provide the request token or exchange it for
   a downstream token.

The following scenarios progress from the simplest configuration to a centrally
managed Agent Card.

### Scenario 1: Request authorization inside a task

This is the recommended starting point and the pattern used by the `A2AAgent`
sample. A single `A2AUserAuthorization` handler contains the runtime OAuth
request and OBO settings:

```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "graph",
      "AutoSignin": false,
      "Handlers": {
        "graph": {
          "Assembly": "Microsoft.Agents.Extensions.A2A",
          "Type": "A2AUserAuthorization",
          "Settings": {
            "Mode": "InTask",
            "OAuthFlows": {
              "DeviceCode": {
                "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                "Scopes": {
                  "api://<agent-app-id>/access_as_user": "Access the agent as the signed-in user."
                }
              }
            },
            "RequiredScopes": [
              "api://<agent-app-id>/access_as_user"
            ],
            "EnforceRequiredScopes": true,
            "OBOConnectionName": "ServiceConnection",
            "OBOScopes": [
              "User.Read"
            ]
          }
        }
      }
    }
  }
}
```

Attach the handler to a route through `autoSigninHandlers`:

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

`Mode: InTask` advertises the optional Agents SDK in-task authorization
extension but does not add an OAuth security scheme or requirement to the Agent
Card. When the route needs a token, the task transitions to
`TASK_STATE_AUTH_REQUIRED` and includes `OAuthFlows` plus `RequiredScopes` in
the task status metadata. The client acquires the credential and calls
`resumeAuth`; the SDK then replays the banked activity.

`EnforceRequiredScopes` is optional and defaults to `false`; when enabled,
`A2AUserAuthorization` validates the task-scoped delegated JWT before OBO and
requires every configured
`RequiredScopes` value to appear in the token's `scp` claim. For Microsoft
Entra resource-qualified scope URIs, the handler compares the final permission
value such as `access_as_user`. `OBOScopes` is not advertised to the client;
`GetTurnTokenAsync` returns the downstream Graph token after the exchange.

### Scenario 2: One handler defines a scheme and another references it

Use this pattern when handlers accept the same inbound credential but require
different scopes or use different runtime token settings.

Handler `agent-read` defines the shared scheme. Handler `profile-read` uses the
same `SecuritySchemeName` without `OAuthFlows`, so it references the scheme
already contributed by `agent-read`:

```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "AutoSignin": false,
      "Handlers": {
        "agent-read": {
          "Assembly": "Microsoft.Agents.Extensions.A2A",
          "Type": "A2AUserAuthorization",
          "Settings": {
            "SecuritySchemeName": "delegated",
            "OAuthFlows": {
              "DeviceCode": {
                "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                "Scopes": {
                  "api://<agent-app-id>/agent.read": "Read agent data.",
                  "api://<agent-app-id>/profile.read": "Read profile data."
                }
              }
            },
            "RequiredScopes": [
              "api://<agent-app-id>/agent.read"
            ]
          }
        },
        "profile-read": {
          "Assembly": "Microsoft.Agents.Extensions.A2A",
          "Type": "A2AUserAuthorization",
          "Settings": {
            "SecuritySchemeName": "delegated",
            "RequiredScopes": [
              "api://<agent-app-id>/profile.read"
            ],
            "OBOConnectionName": "ServiceConnection",
            "OBOScopes": [
              "User.Read"
            ]
          }
        }
      }
    }
  }
}
```

Routes can reference either handler:

```csharp
[A2ASkill(
    name: "Agent data",
    tags: "a2a,data",
    text: "-data",
    autoSigninHandlers: "agent-read")]
private Task OnAgentDataAsync(...) { }

[A2ASkill(
    name: "Profile",
    tags: "a2a,profile",
    text: "-profile",
    autoSigninHandlers: "profile-read")]
private Task OnProfileAsync(...) { }
```

The Agent Card contains one `delegated` security scheme. Each generated skill
requirement contains the scopes from its referenced handler. The handler names
remain distinct runtime authorization configurations even though both
requirements point to the same Agent Card scheme.

### Scenario 3: Define the Agent Card scheme catalog separately

Use this pattern when the application centrally manages its Agent Card and
multiple handlers only need to reference published schemes.

Define the scheme under `AgentApplication:A2A:AgentCard:SecuritySchemes`:

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
                  "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                  "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                  "Scopes": {
                    "api://<agent-app-id>/agent.read": "Read agent data.",
                    "api://<agent-app-id>/profile.read": "Read profile data."
                  }
                }
              }
            }
          }
        }
      }
    },
    "UserAuthorization": {
      "AutoSignin": false,
      "Handlers": {
        "agent-read": {
          "Assembly": "Microsoft.Agents.Extensions.A2A",
          "Type": "A2AUserAuthorization",
          "Settings": {
            "SecuritySchemeName": "delegated",
            "RequiredScopes": [
              "api://<agent-app-id>/agent.read"
            ]
          }
        },
        "profile-read": {
          "Assembly": "Microsoft.Agents.Extensions.A2A",
          "Type": "A2AUserAuthorization",
          "Settings": {
            "SecuritySchemeName": "delegated",
            "RequiredScopes": [
              "api://<agent-app-id>/profile.read"
            ]
          }
        }
      }
    }
  }
}
```

Neither handler defines `OAuthFlows`; `SecuritySchemeName` references the
existing Agent Card scheme. Agent Card composition fails if a generated
requirement references a scheme that the card does not define.

## Authorization setting reference

| Setting | Meaning | When omitted |
| --- | --- | --- |
| `Mode` | `RequestToken` uses the credential already associated with the A2A request. `InTask` emits an auth-required task status and accepts the procured credential through `resumeAuth`. | Defaults to `RequestToken` for compatibility. |
| `SecuritySchemeName` | Identifies the Agent Card scheme used by the handler. With `OAuthFlows`, the handler defines that scheme inline. Without `OAuthFlows`, it references an existing scheme. | A handler with no name contributes no Agent Card security metadata and cannot generate a protected requirement. |
| `OAuthFlows` | Defines one OAuth flow. In `RequestToken` mode it can define an inline Agent Card scheme. In `InTask` mode it is returned only in auth-required task metadata. | In `RequestToken` mode, `SecuritySchemeName` is treated as a reference to an existing scheme. |
| `RequiredScopes` | Scopes placed in a generated Agent Card requirement for `RequestToken`, or in auth-required task metadata for `InTask`. When `EnforceRequiredScopes` is enabled, the same list is validated against the delegated JWT's `scp` claim before OBO. | The requirement contains an empty scope list. `EnforceRequiredScopes` cannot be enabled until at least one non-empty value is configured. |
| `EnforceRequiredScopes` | Enables built-in delegated JWT runtime enforcement. Every configured `RequiredScopes` value must appear in the original validated inbound token's `scp` claim before OBO; Microsoft Entra resource-qualified scopes also match by final permission segment such as `access_as_user`. | Defaults to `false`, so `RequiredScopes` remains Agent Card metadata only. Application tokens (`roles`) and opaque tokens require provider-specific or application-specific authorization. |
| `OBOConnectionName` | Connection used for an OBO exchange. | The default connection selected for the turn is used when an exchange is requested. |
| `OBOScopes` | Downstream scopes requested during OBO. | No OBO exchange occurs and the validated inbound request token is returned unchanged. |

### `Scopes`, `RequiredScopes`, and `OBOScopes`

These settings describe different stages:

- `OAuthFlows.<flow>.Scopes` is the catalog of scopes available from the
  authorization server.
- `RequiredScopes` is the subset placed in the Agent Card requirement for the
  agent or skill.
- `EnforceRequiredScopes` optionally validates that subset against the original
  trusted saved inbound delegated JWT before OBO. Every configured
  `RequiredScopes` value must appear in the token's `scp` claim, and Microsoft
  Entra resource-qualified scope URIs may match by their final permission value
  such as `access_as_user`.
- `OBOScopes` is requested from a downstream service after the request reaches
  the agent.

Available scopes are not automatically required. Inferring all advertised
scopes could cause clients to request excessive permissions. The SDK also does
not infer `RequiredScopes` from that catalog. `EnforceRequiredScopes` defaults
to `false`, preserving compatibility for metadata-only handlers. When enabled,
the built-in check supports delegated JWT `scp` claims only; Microsoft Entra
Client Credentials `roles` claims and opaque-token authorization remain
provider-specific or application-specific concerns.

### Delegated and application tokens

A delegated token represents a signed-in user. It can provide the user identity
needed by user-scoped state and can be exchanged through OBO for a downstream
API.

An application token represents a service, workload, or another agent. For
Microsoft Entra Client Credentials, the acquisition scope is commonly
`api://<agent-app-id>/.default`, and the resulting token carries application
permissions in `roles` rather than delegated permissions in `scp`. It does not
naturally provide a person identity for `ITurnState.User`.

`EnforceRequiredScopes` does not authorize these application tokens because it
checks delegated JWT `scp` values only. Applications using opaque tokens
likewise need provider-specific or application-specific authorization.

Inbound application authentication is different from an agent using
`MsalAuth` for an outbound call:

- inbound A2A application token: caller to A2A agent;
- outbound client credentials: agent to downstream service; and
- outbound OBO: agent acting for an inbound user to downstream service.

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
