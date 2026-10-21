# A2AAgent sample

This sample adds Agent2Agent (A2A) support to an `AgentApplication`. It demonstrates
anonymous skills, streaming, multi-turn tasks, native A2A access, and one
route-scoped delegated OAuth flow that reads the signed-in user's Microsoft Graph
profile.

> [!IMPORTANT]
> `Microsoft.Agents.Extensions.A2A` is in preview. Its APIs and configuration may
> change before final release.

For extension concepts, Agent Card generation, protocol mapping, and advanced
OAuth configuration, see the [A2A developer guide](A2A-DEVELOPER-GUIDE.md).

## Prerequisites

- A .NET SDK that can build `net10.0` projects.
- The [A2ACli sample](../A2ACli/README.md) or another A2A client.
- For the authenticated Graph profile skills, a Microsoft Entra application configured as
  described in [Configure the authenticated sample](#configure-the-authenticated-sample).

## What the sample demonstrates

| Input | Behavior | Authentication |
| --- | --- | --- |
| Any ordinary text | Echoes the text and completes the A2A task. | Anonymous |
| `-stream` | Streams informative and text updates with a citation. | Anonymous |
| `-multi` | Starts a task that remains `input-required`; send `end` to complete it. | Anonymous |
| `-proactive-store` | Stores a conversation for externally triggered task updates. | Anonymous |
| `-proactive` | Returns an unstored conversation for full proactive continuations. | Anonymous |
| `-a2a` | Sends a native A2A message through `IA2ATurnContext.Client`. | Anonymous |
| `-me-agentcard` | Acquires a delegated Agent API token before the request, exchanges it for Microsoft Graph `User.Read`, and returns the caller's profile. | Agent Card OAuth |
| `-me-intask` | Requests a delegated Agent API token after the task starts, exchanges it for Microsoft Graph `User.Read`, and returns the caller's profile. | In-Task OAuth |

The A2A endpoints are available at `http://localhost:3978/a2a` by default.

## Run anonymously

Start the agent:

```powershell
dotnet run --project src\samples\A2A\A2AAgent\A2AAgent.csproj
```

In another terminal, start the sample client:

```powershell
dotnet run --project src\samples\A2A\A2ACli\A2ACli.csproj -- --agent http://localhost:3978/a2a
```

Send ordinary text, `-stream`, or `-multi`. The shipped placeholders leave token
validation disabled during local development, so the anonymous scenarios work
without Microsoft Entra configuration.

## Continue a task proactively

The following instructions use one A2ACli instance to send messages and receive
local push notifications, plus Bash-compatible curl commands to trigger proactive
updates. No tunnel or separate push registration request is needed.

### Enable loopback callbacks in Development

Use an `a2a-dotnet` build that includes `PushNotificationUrlOptions.AllowLoopback`.
The option defaults to `false`; Development does not automatically enable it.
In `A2AAgentStartup.ConfigureBuilder`, add this registration before
`builder.AddAgentDefaults()`:

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton(new global::A2A.PushNotificationUrlOptions
    {
        AllowLoopback = true,
        AllowedHosts = ["localhost", "127.0.0.1", "::1"],
    });
}
```

This permits HTTP/HTTPS callbacks to the allowed loopback hosts only during
Development. Every DNS answer for `localhost` must be loopback; other private
destinations remain blocked, and connection-time validation still applies.
Outside Development, the default HTTPS/public-address policy remains in effect.

Start or restart A2AAgent as described above and leave it running. Its launch
profile selects Development.

> [!WARNING]
> A2ACli's receiver does not authenticate notifications. Use this opt-in only for
> trusted local development with nonsensitive data: callback registrations can
> target other services on the server's loopback interface. Do not expose the
> receiver or anonymous agent endpoints publicly. Production receivers must
> authenticate the sending agent and use HTTPS.

### Start A2ACli with push notifications

In another terminal, from the `A2ACli` project directory, start the client:

```bash
dotnet run -- \
  --agent http://localhost:3978/a2a \
  --auth-mode none \
  --use-push-notifications \
  --push-notification-receiver http://localhost:5000
```

The CLI listens at `http://localhost:5000/notify`, advertises that callback in
`SendMessageConfiguration.TaskPushNotificationConfig` on outgoing messages, and
prints notifications in the same terminal. A2AAgent and A2ACli must run on the same
machine/network namespace; `localhost` inside a container refers to that container.

### Create the task

Press Enter at `Use streaming responses? (y/N):`, send `-proactive-store`, and press Enter at
`Attachment path (press enter to skip):`. These proactive skills are anonymous; no
Entra client ID, client secret, or sign-in is needed.

Sending `-proactive-store` creates a task and stores its `Conversation` with
`Proactive.StoreConversationAsync`. The response text includes the task ID.
A2A registers the callback supplied by A2ACli's initial request against that task;
there is no need to create `push-config.json` or call the push registration endpoint.
A2ACli intentionally omits push credentials for its anonymous local receiver.

There is no server-side callback destination setting: the client supplies
the destination and credentials, and `A2AServer.ContinueTaskAsync` enforces the active
task registration and delivers updates. Outbound push credentials for other receivers
are separate from authentication of incoming agent requests.

### Trigger an update and view it in A2ACli

In another terminal, use the task ID from the client to trigger an update:

```bash
taskId='<task ID from the client>'
curl --fail-with-body --request POST \
  "http://localhost:3978/proactive/sendactivity/$taskId" \
  --header 'Content-Type: application/json' \
  --data '{"type":"message","text":"An external update"}'
```

The same A2ACli instance prints `Push notification =>` followed by the
serialized update containing `An external update`. No new task is created. Leave
the CLI running for further updates.

### Continue without a stored Conversation

For the unstored path, exit A2ACli with `:q` and restart it with the same
push-notification command above, so the next message starts a new task instead of
resuming the previous one. Press Enter at the streaming prompt, send `-proactive`,
and skip the attachment prompt. Its initial request registers the local callback
against the new task automatically.

A2ACli displays message text, not the initial response's data parts. To obtain the
unstored Conversation, retrieve the task with curl:

```bash
taskId='<new task ID from the client>'
curl --fail-with-body \
  "http://localhost:3978/a2a/tasks/$taskId" \
  --output task.json
```

Copy the Conversation object from the data part in `status.message.parts` to a local
`conversation.json` file. Copy only the `data` object's contents, not the surrounding
part or entire task. Then:

```bash
curl --fail-with-body --request POST \
  http://localhost:3978/proactive/continue \
  --header 'Content-Type: application/json' \
  --data-binary @conversation.json
```

This invokes the `[ContinueConversation]` handler with middleware and persisted task conversation
state. Repeating it produces `Proactive continuation 1`, `Proactive continuation 2`, and so on in
the CLI's push notifications. The Conversation is not stored by this skill; the complete
body is used on every call.

Both skills leave their initial task `input-required` so the ordinary CLI request can finish;
proactive updates move it to `working`. Continuation requires an existing nonterminal task with
an active push registration. Completed, canceled, or failed tasks cannot be continued.
`CreateConversation`, `UpdateActivity`, and `DeleteActivity` are not supported by A2A.
Task storage, push registrations, and conversation storage in this sample are in memory and are
lost on restart. Task continuation coordination is process-local, not a distributed lock.
The proactive HTTP endpoints require authentication outside Development. Do not expose anonymous
Development triggers publicly; production hosts must authorize which callers may update each task.

## View telemetry

The sample exports Agents SDK and `a2a-dotnet` traces, metrics, and logs over
OTLP. Run the standalone
[.NET Aspire dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone)
before starting the agent to inspect telemetry from the `A2A` and
`A2A.AspNetCore` activity sources.

## Configure the authenticated sample

The Graph profile skills use two `A2AUserAuthorization` handlers with the same
Device Code flow, Agent API scope, scope enforcement, and Microsoft Graph OBO
exchange:

- `graph-agentcard` uses `RequestToken`. Its OAuth scheme and the
  `-me-agentcard` skill requirement are published in the Agent Card. The client
  acquires the token before sending the request and uses the standard
  `Authorization` header.
- `graph-intask` uses `InTask`. Invoking `-me-intask` without a task credential
  returns `TASK_STATE_AUTH_REQUIRED`; the client acquires the token and calls
  `auth-response` with the raw token in `x-a2a-intask-authorization`. The normal
  `Authorization` header remains available for the request-authentication JWT.

Both handlers validate the delegated JWT against every configured
`RequiredScopes` value before OBO and exchange the validated Agent API token
through `ServiceConnection` for Microsoft Graph `User.Read`.

### Register the Agent API application

1. Create a single-tenant Microsoft Entra app registration for the Agent API.
1. Set `requestedAccessTokenVersion` to `2`.
1. In **Expose an API**, publish
   `api://botid-<agent-client-id>/access_as_user`.
1. Add the delegated Microsoft Graph permission `User.Read`.
1. Grant the consent required by your tenant.
1. Under **Authentication** > **Advanced settings**, set **Allow public client
   flows** to **Yes** so the sample client can use Device Code authentication.
   This sets `isFallbackPublicClient` to `true` in the app manifest. If it
   remains disabled, Entra returns `AADSTS7000218` asking for a
   `client_assertion` or `client_secret`.
1. Create the client secret or other credential used by
   `Connections:ServiceConnection` for OBO.

### Configure the agent

In `appsettings.json`:

- replace `{{ClientId}}` in `TokenValidation:Audiences` and the Agent API scope;
- replace `{{TenantId}}` in `TokenValidation:TenantId` and the connection authority;
- configure `Connections:ServiceConnection` for the Agent API application; and
- place real secrets in user secrets or another secure configuration provider, not
  in the committed settings file.

The relevant authorization configuration is:

```json
"AgentApplication": {
  "UserAuthorization": {
    "DefaultHandlerName": "graph-intask",
    "AutoSignin": false,
    "Handlers": {
      "graph-agentcard": {
        "Assembly": "Microsoft.Agents.Extensions.A2A",
        "Type": "A2AUserAuthorization",
        "Settings": {
          "Mode": "RequestToken",
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
      },
      "graph-intask": {
        "Assembly": "Microsoft.Agents.Extensions.A2A",
        "Type": "A2AUserAuthorization",
        "Settings": {
          "Mode": "InTask",
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
    }
  }
}
```

In `OAuthFlows.DeviceCode.Scopes`, `{{ClientId}}` is the application (client)
ID of the **A2AAgent's Entra app registration**. It is
the same app registration where `access_as_user` is defined under **Expose an
API**, and the same ID used by `TokenValidation:Audiences`. It is not the
A2ACli's client ID or a Microsoft Graph application ID. The resulting Agent
API scope URI is:
`api://botid-<A2AAgent-client-id>/access_as_user`.

`A2AAgentStartup` enables token validation in Development only after
`TokenValidation:Audiences` contains real GUIDs.

Because `RequiredScopes` is omitted, it defaults to every key in
`OAuthFlows.DeviceCode.Scopes`. `EnforceRequiredScopes` makes
`A2AUserAuthorization` validate the request token for `-me-agentcard` or the
credential submitted to `auth-response` for `-me-intask` before OBO. Every
required scope must appear in the token's `scp` claim; for Microsoft Entra
resource-qualified scope URIs, the handler compares the final permission value
such as `access_as_user`. The option is disabled by default and does not support
opaque tokens or application-role validation. In `InTask` mode, enabling it
requires configured `OBOScopes` so the client-supplied token is validated by
the exchange before the protected route runs.

### Configure and run the client

Configure the [A2ACli sample](../A2ACli/README.md) with:

- `Authentication:Providers:entra:Registrations:delegated:ClientId` set to the
  Agent API client ID.

The Agent API registration itself must be used as the public client for this
sample so the inbound token satisfies the SDK's OBO exchange checks.
Configure the tenant ID in this agent's `DeviceAuthorizationUrl` and `TokenUrl`;
the client uses the endpoints advertised by the authorization metadata. The
Agent Card's scheme name does not select local client configuration; the client
matches its local provider catalog from the advertised flow and endpoints.

Start the client without `--auth-mode`. To exercise standard Agent Card
authorization, send:

```text
-me-agentcard
```

The client matches the advertised skill, acquires the Agent API token from its
security requirement, and sends the token in the request's `Authorization`
header.

To exercise the Agents SDK In-Task authorization extension, send:

```text
-me-intask
```

The client receives the task-scoped OAuth flow in the auth-required status,
acquires an Agent API token, and calls `auth-response` with that token in the
`x-a2a-intask-authorization` header. The normal `Authorization` header remains
reserved for the JWT that authenticates the A2A request.

Expected failures:

- A token acquired by a different public-client registration is not exchangeable
  by this sample.
- A Microsoft Graph token cannot be sent directly to the Agent API because its
  audience is incorrect.

## Further reading

- [Develop A2A agents with the Microsoft 365 Agents SDK](A2A-DEVELOPER-GUIDE.md)
- [A2ACli sample](../A2ACli/README.md)
- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
