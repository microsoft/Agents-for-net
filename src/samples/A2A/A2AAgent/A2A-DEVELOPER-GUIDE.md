# Develop A2A agents with the Microsoft 365 Agents SDK

`Microsoft.Agents.Extensions.A2A` lets an existing Agents SDK application expose
Agent2Agent (A2A) endpoints while continuing to use `AgentApplication` routing,
middleware, state, storage, and user authorization. Like other channel
extensions, it presents requests as Activity Protocol activities: application
code receives an `ITurnContext` and responds with `SendActivityAsync`.

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
1. `AgentApplication:A2A:AgentCard` supplies application-owned metadata;
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
- use `AgentApplication:A2A:AgentCard` for descriptive metadata.

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
        "IconUrl": "https://example.com/weather-agent/icon.png"
      }
    }
  }
}
```

The descriptive settings are `Name`, `Description`, `DocumentationUrl`,
`IconUrl`, and `Provider`. Security schemes and their agent-level or skill-level
requirements are covered later under [OAuth](#oauth).

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

Treat A2A as another channel, not as a separate application programming model.
Existing routes can use `ITurnContext`, `MessageFactory`, turn state, and the
normal send APIs without constructing native A2A messages:

```csharp
await turnContext.SendActivityAsync(
    "Checking the forecast.",
    cancellationToken: cancellationToken);
```

The extension translates that activity into an A2A task-status update containing
the response text. Use `IA2ATurnContext` only when a route needs A2A-specific
information or [native response control](#advanced-respond-with-the-native-a2a-api).

An inbound A2A message is converted to an Activity Protocol activity and passed
through the normal `AgentApplication` pipeline:

- the activity type is `ActivityTypes.Message`;
- `Activity.ChannelId` is `a2a`;
- text parts are appended to `Activity.Text`;
- other parts become attachments; and
- `Activity.ChannelData` contains the original A2A message.

The extension maps outbound activities back to A2A:

- `ActivityTypes.Message` becomes A2A task-status content, normally in the
  `working` state;
- attachments, entities, and `Activity.Value` become A2A parts;
- `ActivityTypes.EndOfConversation` completes, fails, or cancels the task; and
- `ITurnContext.StreamingResponse` produces streaming task updates and artifacts.

All current Agents SDK interactions occur in the context of an A2A task. The
task ID is used as `Activity.Conversation.Id`, so conversation state is scoped to
the task, not to the broader A2A context ID.

For multi-turn behavior, send a response with
`Activity.InputHint == InputHints.ExpectingInput`. This moves the A2A task to
`input-required` and lets the client supply the next message on that task.
A normal message does not complete the task. Send
`ActivityTypes.EndOfConversation` when the task is complete:

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

Ending `StreamingResponse` finishes that response stream; it does not by itself
complete the A2A task. Send `EndOfConversation` when no further work or input is
expected.

## Continue an A2A task proactively

Use the same `AgentApplication.Proactive` APIs described in
[Proactive messaging in the Agents SDK (.NET)](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/proactive-overview?pivots=dotnet).
That article covers conversation storage, internal and external triggers, and
the general proactive API. The requirements below are specific to A2A.

### Prepare the task and push destination

A2A proactive delivery continues an **existing task**; it does not create a
new task or conversation. For work performed after the original request ends:

1. The A2A client starts a task and registers a push destination, either through
   `SendMessageConfiguration.TaskPushNotificationConfig` on its request or
   through the A2A push-notification configuration operations.
1. The agent captures the task's `Conversation`, or stores it with
   `Proactive.StoreConversationAsync` and keeps the returned conversation ID.
1. The agent leaves the task nonterminal. For example, an
   `InputHints.ExpectingInput` response leaves it `input-required` and allows
   the initial request to return.
1. A background worker or external trigger sends an activity or invokes a
   continuation handler for that task. The extension uses the upstream
   `a2a-dotnet` task-continuation and push-delivery pipeline.

The client supplies the callback URL and any credentials the receiver requires;
the agent does not configure one global callback destination. The destination
must pass the server's push URL validation. Production callbacks require HTTPS
and a permitted public destination. The [A2AAgent sample](README.md) shows the
explicit loopback opt-in for local development.

Completed, canceled, failed, and missing tasks cannot be continued. An
out-of-request continuation also requires an active push registration; merely
storing a `Conversation` is not enough. Do not send `EndOfConversation` until all
planned proactive work for the task is finished.

### Store a conversation and send later

Capture the conversation during an A2A turn:

```csharp
[A2ASkill(
    name: "Subscribe to updates",
    description: "Keeps a task open for proactive updates.",
    tags: "notifications",
    examples: "-subscribe",
    text: "-subscribe")]
private async Task OnSubscribeAsync(
    IA2ATurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    string conversationId = await Proactive.StoreConversationAsync(
        turnContext,
        cancellationToken);

    await turnContext.SendActivityAsync(
        MessageFactory.Text(
            $"Subscribed. Conversation ID: {conversationId}",
            inputHint: InputHints.ExpectingInput),
        cancellationToken: cancellationToken);
}
```

Later, send a single activity using the stored ID. The `Channels.A2A` overload
selects the registered A2A adapter rather than the host's default channel:

```csharp
await agent.Proactive.SendActivityAsync(
    Channels.A2A,
    conversationId,
    MessageFactory.Text("Your report is ready."),
    cancellationToken);
```

For application logic that needs turn state and middleware, define a
continuation handler on the agent:

```csharp
using Microsoft.Agents.Builder.App.Proactive;

[ContinueConversation]
public async Task OnReportReadyAsync(
    ITurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    await turnContext.SendActivityAsync(
        new Activity
        {
            Type = ActivityTypes.EndOfConversation,
            Code = EndOfConversationCodes.CompletedSuccessfully,
            Text = "Your report is ready.",
        },
        cancellationToken: cancellationToken);
}
```

Invoke it instead of the single-activity send when the report is ready:

```csharp
await agent.Proactive.ContinueConversationAsync(
    Channels.A2A,
    conversationId,
    agent.OnReportReadyAsync,
    cancellationToken: cancellationToken);
```

The continuation loads and saves the task's conversation state and runs the
selected handler; it does not rerun message-route selection. A2A-specific
`[ContinueConversation]` handlers can also declare an `IA2ATurnContext`
parameter when they need native A2A access.

If you manage conversation storage yourself, capture `new Conversation(turnContext)`
and use the overloads accepting that complete `Conversation` instead of a stored
ID. Its channel identifies the A2A adapter. Do not retain the original
`ITurnContext`, `IA2ATurnContext.Client`, request context, or event queue for
background work; proactive APIs create the appropriate new turn.

A proactive turn has no new inbound A2A request token. Continuation handlers can
declare `autoSignInHandlers`, but the normal proactive signed-in-token policy
still applies; declaring a handler does not supply or refresh a credential.

### Trigger proactive work over HTTP

Map the standard proactive endpoints in addition to the A2A endpoints:

```csharp
app.MapA2AApplicationEndpoints();
app.MapAgentProactiveEndpoints<MyAgent>(requireAuth: true);
```

An external service can post an Activity to
`/proactive/sendactivity/{conversationId}`, or invoke the default
`[ContinueConversation]` handler with
`/proactive/continue/{conversationId}`. Named handlers use
`/proactive/continue/{key}/{conversationId}`.

The ID-based routes require a previously stored `Conversation`. The body-based
`/proactive/sendactivity` and `/proactive/continue[/{key}]` routes accept a
complete `Conversation` without that lookup, using the request formats in the
general proactive guide. Both forms select the adapter from the conversation's
channel and retain A2A's existing-task and push-registration requirements.
The `/proactive/create` operation is not supported for A2A.

Authenticate and authorize external triggers, including which tasks each caller
may update. Trigger authorization, authentication of incoming A2A requests, and
credentials sent to a push receiver are separate concerns. Do not expose
anonymous development triggers or receivers publicly.

### Persist push-notification registrations

Proactive A2A applications have three distinct storage responsibilities:

| Store | Persists |
| --- | --- |
| Agents SDK `IStorage` | Turn state and conversations captured by `Proactive.StoreConversationAsync`. |
| A2A `ITaskStore` | A2A tasks, status, history, and artifacts. |
| A2A `IPushNotificationStore` | Per-task callback destinations, credentials, and registration generations. |

The default A2A task and push-registration stores are in memory. Replacing
`IStorage` alone does not persist either of them. A durable push registration
also cannot recover a task that was lost from an in-memory task store.

`Microsoft.Agents.Extensions.A2A.Storage.BlobPushNotificationStore` is an
opt-in `IPushNotificationStore` implementation. It encrypts and authenticates
each task's registration document with ASP.NET Core Data Protection, uses hashed
blob names, and uses conditional ETag writes to prevent lost updates.

Before registering it, provision a **private Blob container** and configure
an `IDataProtectionProvider` with a **durable, externally protected key ring**
and stable application name. Keep the keys separate from registration records;
`AddDataProtection()` alone is not a production key-management policy.
Ephemeral or unavailable keys prevent recovery after restart.

For example, after agent registration and before `builder.Build()`:

```csharp
using A2A;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Agents.Extensions.A2A.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

// Configure the host's durable, protected Data Protection key ring first.
builder.Services.AddSingleton<IPushNotificationStore>(services =>
    new BlobPushNotificationStore(
        new BlobContainerClient(
            new Uri("https://<account>.blob.core.windows.net/a2a-push"),
            new DefaultAzureCredential()),
        services.GetRequiredService<IDataProtectionProvider>(),
        scope: "trusted-agent-scope"));
```

Use a least-privilege storage identity. The store does not create containers and
requires HTTPS; HTTP loopback storage such as Azurite requires the explicit
development option `BlobPushNotificationStoreOptions.AllowInsecureLoopback`.
This storage option is separate from the push callback URL policy.

Use **one active host and one live singleton store per trusted authorization
scope**. Derive `scope` from trusted host configuration, never from an inbound
tenant field or task ID. The scope isolates protected records; it does not
authorize callers. Keep task and push storage within the same authorization
boundary and authorize registration operations in the host.

Shared blobs and keys do not provide distributed task coordination, registration
invalidation, or cancellation. Persisting registrations does not make queued
delivery or retries survive a restart and does not provide a transactional
outbox. Store registration replaces persistence for the configured push pipeline;
it is not, by itself, push enablement.

`BlobPushNotificationStoreOptions` defaults to 100 registrations per task,
64 KiB per serialized configuration, 8 MiB per encrypted task document, and five
conditional write attempts. Options are captured at construction. Storage,
decryption, and exhausted write-retry failures are surfaced rather than treated
as an empty store.

Plan quotas, retention, credential rotation, and cleanup. Delete registrations
with version-conditional deletion before removing their task, and retain an
authorized cleanup path for orphaned records. Retain the Data Protection keys
needed for live registrations and backups; reconcile deletions and revocations
before restoring old records. Do not log decrypted registrations or credentials.

A2A does not define a push-credential refresh protocol. The client must renew
credentials through new registration generations and remove old ones. The store
does not refresh them, and delivery using stale credentials can fail.

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

**Prefer in-task authorization for OAuth.** It fits the
`AgentApplication.UserAuthorization` model, keeps user tokens separate from
endpoint authentication, and supports multiple token handlers and opaque
provider tokens.

**The A2A client must implement and activate the
[A2A In-Task Authorization Extension](https://github.com/microsoft/Agents/blob/main/specs/a2a/extensions/in-task-authorization/v1.md).**
Support for standard A2A alone is not sufficient. If the client does not support
that extension, use **Agent Card authorization** (`Mode: RequestToken`) and
design around its more limited, single-request-token model. Configuring
`Mode: InTask` on the agent does not add support to the client or automatically
fall back to Agent Card authorization.

| Method | Use when | Tradeoffs |
| --- | --- | --- |
| In-task authorization (preferred) | The client supports the in-task authorization extension and the route needs user tokens during a task. | Can request multiple handlers and carry opaque tokens separately from the endpoint credential. Requires explicit client extension support. |
| Agent Card authorization (fallback) | The client does not support in-task authorization and must discover OAuth from the standard Agent Card. | More broadly interoperable, but each request carries one authorization token, and the built-in path expects a JWT that ASP.NET Core can validate. |

### In-task authorization

Set the handler mode explicitly to `InTask` (the default is `RequestToken`):

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

The client activates the extension by sending its URI in the `A2A-Extensions`
header on both the initiating request and the corresponding `auth-response`
request. When a route needs the handler, the task transitions to
`TASK_STATE_AUTH_REQUIRED` and publishes an authorization request ID, the
handler's `OAuthFlows`, and `RequiredScopes` in task status metadata.

The client acquires the token and calls `auth-response` with the same task and
context IDs and the authorization request ID. The standard `Authorization`
header continues to carry the JWT that authenticates the A2A request; the
acquired user token is sent separately as the raw
`x-a2a-intask-authorization` header value, without a `Bearer` prefix.

The A2A extension processes `auth-response` as an event Activity and makes the
submitted credential available to the matching in-task authorization handler,
not in the Activity payload. It resumes the interrupted
`AgentApplication.UserAuthorization` operation without requiring the client to
resend the original message. A route with multiple `autoSigninHandlers` can
complete the handlers separately, allowing multiple tokens during one A2A task.

Because the user token is separate from request authentication, it can be an
opaque provider token. `GetTurnTokenAsync` returns it unchanged when no OBO
scopes are configured. Provider-specific exchange and authorization rules still
apply: built-in `EnforceRequiredScopes` supports delegated JWT `scp` claims,
and a downstream OBO exchange must support the supplied token. See the
[extension specification](https://github.com/microsoft/Agents/blob/main/specs/a2a/extensions/in-task-authorization/v1.md)
for the activation, correlation, transport, and credential-handling contract.

### Agent Card authorization

Use Agent Card authorization when the client cannot support in-task
authorization. The Agent Card advertises the OAuth scheme, and
`A2ASkillAttribute.AutoSignInHandlers` associates a handler with the skill's
security requirement. The client acquires the required token before invoking
the skill and sends that token in the standard HTTP `Authorization` header.

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
token while retaining a JWT for A2A request authentication. If the route must
accept an opaque token or obtain multiple tokens, choose a client that supports
in-task authorization; Agent Card authorization does not remove those limits.

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

## Authorization setting reference

| Setting | Meaning | When omitted |
| --- | --- | --- |
| `Mode` | `RequestToken` uses the credential associated with the A2A request. `InTask` emits an auth-required task status and accepts the acquired credential through `auth-response`. | Defaults to `RequestToken`. |
| `SecuritySchemeName` | Identifies the Agent Card scheme used by a `RequestToken` handler. With `OAuthFlows`, the handler defines that scheme inline. Without `OAuthFlows`, it references an existing scheme contributed through configuration or `IAgentCardHandler`. | With inline `OAuthFlows`, defaults to the handler name. Without inline flows, the handler contributes no Agent Card security metadata. |
| `OAuthFlows` | Defines one OAuth flow. In `RequestToken` mode it can define an inline Agent Card scheme. In `InTask` mode it is returned in auth-required task metadata. | In `RequestToken` mode, an explicit `SecuritySchemeName` references an existing Agent Card scheme. |
| `RequiredScopes` | Scopes placed in a generated Agent Card requirement for `RequestToken`, or in auth-required task metadata for `InTask`. When `EnforceRequiredScopes` is enabled, the same list is validated against the delegated JWT's `scp` claim before OBO. | Defaults to all scope keys advertised by `OAuthFlows`. An explicit empty array disables inferred requirements. |
| `EnforceRequiredScopes` | Requires every `RequiredScopes` value in the delegated JWT's `scp` claim. Microsoft Entra resource-qualified scopes also match by their final permission segment, such as `access_as_user`. In `InTask` mode, configured `OBOScopes` are required so the client-supplied token is validated by a trusted exchange before the route runs. | Defaults to `false`. Application tokens, opaque tokens, and provider-specific authorization require application logic. |
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

## Advanced: Respond with the native A2A API

Prefer `ITurnContext.SendActivityAsync` and `StreamingResponse` for responses
that should work across channels. For A2A-only behavior, an A2A route can use
`IA2ATurnContext.Client` to access the underlying `a2a-dotnet` primitives:

| Property | Use |
| --- | --- |
| `RequestContext` | Read the current task ID, context ID, and incoming message. An out-of-request proactive continuation has no incoming message. |
| `EventQueue` | Publish native messages, task-status updates, and artifact updates. |
| `TaskStore` | Retrieve persisted A2A task state. Updating storage directly is not a substitute for publishing response events. |

For example, send a native completed-task status with a response message:

```csharp
[A2AMessageRoute("-native")]
private async Task OnNativeAsync(
    IA2ATurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    var client = turnContext.Client;
    var request = client.RequestContext;
    var message = new A2A.Message
    {
        MessageId = Guid.NewGuid().ToString("N"),
        Role = A2A.Role.Agent,
        TaskId = request.TaskId,
        ContextId = request.ContextId,
        Parts = [new A2A.Part { Text = "Completed with the native A2A API." }],
    };

    await client.EventQueue.EnqueueStatusUpdateAsync(
        new A2A.TaskStatusUpdateEvent
        {
            TaskId = request.TaskId,
            ContextId = request.ContextId,
            Status = new()
            {
                State = A2A.TaskState.Completed,
                Timestamp = DateTimeOffset.UtcNow,
                Message = message,
            },
        },
        cancellationToken);
}
```

Use `EventQueue.EnqueueMessageAsync` for native messages and
`EventQueue.EnqueueArtifactUpdateAsync` for artifacts. Preserve the current
task and context IDs and explicitly publish the appropriate task status:
enqueuing a message or the last artifact chunk does not itself complete the
task.

Native events enter the same A2A task persistence, streaming, and push-delivery
pipeline, but bypass Activity-to-A2A response conversion and
`ITurnContext.OnSendActivities` hooks. The example completes the task natively,
so it must not also send an `EndOfConversation` activity for that completion.
Avoid emitting the same response through both APIs.

The client wrapper, request context, and event queue are valid only for the
current turn. Do not cache them or use them from deferred work after the turn
ends. Use the [proactive APIs](#continue-an-a2a-task-proactively) to resume a
task instead. Keep native response code in A2A-specific routes so that shared
handlers remain channel-independent.

## Current preview limitations

- The server extension implements the Agents SDK in-task authorization
  extension; arbitrary third-party A2A protocol extensions are not yet
  dynamically registered.
- The mapped A2A transports are JSON-RPC and HTTP+JSON, not gRPC. Extended Agent
  Cards are not supported.
- Proactive sends and continuations require an existing nonterminal task and,
  outside an active turn, a registered push destination. A2A does not support
  `CreateConversation`, `UpdateActivity`, or `DeleteActivity`.
- Task continuation and push-delivery coordination are process-local. Persistent
  push registrations do not provide multi-active-host coordination or durable
  delivery across restarts; see [push-registration storage](#persist-push-notification-registrations).
- Message-only interactions are not supported; all current interactions use an
  A2A task.
- `ITurnState.User` requires a usable user identity. Application tokens and
  anonymous requests do not provide one.
- Multiple A2A agents in one host are not supported; requests are routed to the
  registered `IAgent`.

## Further reading

- [A2AAgent sample](README.md)
- [A2ACli sample](../A2ACli/README.md)
- [A2A protocol specification](https://a2a-protocol.org/latest/specification/)
