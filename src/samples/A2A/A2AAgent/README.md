# A2AAgent Sample

This sample shows how to add A2A support to an `AgentApplication`, including
anonymous routes and one route-scoped delegated OAuth flow that reads the
signed-in user's Microsoft Graph profile.

> Note that this is a preview version of A2A support and is likely to change.

## Overview of A2A in Agents SDK

- SDK agents can add support to an existing agent in order to participate in an A2A multi-agent scenario.
- Messages sent via A2A are handled in the same `AgentApplication` as other channels. This allows the SDK developer to reuse existing functionality and stack knowledge.
- The `Microsoft.Agents.Extensions.A2A` package enables support for A2A requests and response handling:
  - A2A `Task` state handling and persistence via `IStorage`
  - SSE and polling

## Not supported in this version

- A2A
  - Push notifications. This will most likely be handled via `Adapter.ContinueConversation`.
  - Extensions. These would be useful to support knowledge about Agents SDK payloads such as streamed responses, AI citations, or Adaptive Cards responses.
- SDK
  - Not implemented:
    - `Adapter.ContinueConversation`
    - `Adapter.CreateConversation`
    - `Adapter.UpdateActivity`
    - `Adapter.DeleteActivity`
  - `Message` responses. All interactions create an A2A `Task` (see details below).
  - `ITurnState.UserState` will not function as expected because the sample currently lacks a unique A2A user ID.
- Multiple A2A agents in the same host are not supported. All A2A requests are routed to the registered `IAgent` with a single A2A adapter.

## Prerequisites

- A .NET SDK that can build `net10.0` projects

## Running this sample

- This sample accepts anonymous requests out of the box.
- By default, it responds to A2A requests on `http://localhost:3978/a2a`.

Start the agent with:

```powershell
dotnet run --project src\samples\A2A\A2AAgent\A2AAgent.csproj
```

In another terminal, run the sample client:

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

Send any normal message to verify the anonymous echo flow.

## Configure Microsoft Entra ID for authenticated routes

`src\samples\A2A\A2AAgent\appsettings.json` keeps global `AutoSignin` disabled:

```json
"AgentApplication": {
  "UserAuthorization": {
    "AutoSignin": false
  }
}
```

The sample opts into authentication for one route:

| Route | Handler | What it validates |
| --- | --- | --- |
| `-me` | `graph` | Validates an inbound delegated Agent API token, exchanges it for Microsoft Graph `User.Read`, and returns the user's profile. |

### How token validation is enabled

`AddAgentAuthorization` disables authentication in the Development environment by default, and
`Properties\launchSettings.json` runs the sample as Development. `A2AAgentStartup` therefore enables
token validation in Development as well, as soon as `TokenValidation:Audiences` contains real client
IDs instead of the shipped `{{ClientId}}` placeholder:

```csharp
builder.AddAgentAuthorization(
    b => b.AddAgentAspNetAuthentication(),
    forceEnable: !builder.Environment.IsDevelopment() || IsTokenValidationConfigured(builder.Configuration));
```

Two consequences are worth knowing:

- With placeholders in place, `dotnet run` behaves exactly as before: no authentication scheme is
  registered and every route is anonymous.
- Once the placeholders are replaced, inbound bearer tokens are validated. The A2A endpoints are still
  mapped with `requireAuth: false`, so echo, `-multi`, `-stream`, and `-a2a` stay anonymous while
  `-me` obtains the validated token through its route handler. The Activity
  Protocol endpoint mapped by `MapDefaultAgentEndpoints` does require authorization.

### 1. Register the Agent API application

1. Create a **single-tenant** Microsoft Entra app registration for the agent API.
1. In the app registration **Manifest** or **API settings**, set `requestedAccessTokenVersion` to `2` so the delegated scope issues a v2 access token whose `aud` is the API's GUID, which matches `TokenValidation:Audiences`.
1. In **Expose an API**, publish the delegated scope `api://<agent-client-id>/access_as_user`.
1. Add the delegated Microsoft Graph permission `User.Read`.
1. Grant the tenant consent required by your environment.
1. Enable public client flows so the sample A2A client can use Device Code authentication.
1. Create the client secret or other credential used by `Connections:ServiceConnection` for OBO.
1. Update `src\samples\A2A\A2AAgent\appsettings.json` so `TokenValidation:Audiences` contains the Agent API client ID and `TokenValidation:TenantId` contains the tenant ID.
1. Update `Connections:ServiceConnection:Settings:AuthorityEndpoint`, `ClientId`, and the local credential values for the same app registration.

The `graph` handler owns the delegated OAuth scheme that it contributes to the Agent Card and
configures the downstream Graph exchange:

```json
"graph": {
  "Type": "A2AUserAuthorization",
  "Settings": {
    "SecuritySchemeName": "delegated",
    "OAuthFlows": {
      "DeviceCode": {
        "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
        "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
        "Scopes": {
          "api://<agent-client-id>/access_as_user":
            "Access the A2A Agent API as the signed-in user."
        }
      }
    },
    "RequiredScopes": [
      "api://<agent-client-id>/access_as_user"
    ],
    "OBOConnectionName": "ServiceConnection",
    "OBOScopes": [
      "User.Read"
    ]
  }
}
```

`OAuthFlows.DeviceCode.Scopes` and `RequiredScopes` are intentionally separate:

- `Scopes` is the OAuth scheme's catalog of scopes available from the authorization server.
- `RequiredScopes` is the subset required by the handler or generated skill.
- `OBOScopes`, when used, identifies scopes for a downstream service and is not advertised as
  an inbound Agent Card requirement.

Available scopes are not automatically treated as required scopes. If `RequiredScopes` is omitted,
the generated security requirement contains an empty scope list, and the SDK does not automatically
enforce a scope claim at runtime.

The route associates its generated skill with the handler:

```csharp
[A2ASkill(
    name: "Microsoft Graph profile",
    description: "Reads the delegated caller profile from Microsoft Graph.",
    tags: "a2a, sample, authentication, graph",
    text: "-me",
    autoSigninHandlers: "graph")]
private async Task OnGraphAsync(
    IA2ATurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    var graphToken = await UserAuthorization.GetTurnTokenAsync(
        turnContext,
        "graph",
        cancellationToken);

    A2ATokenIdentity.RequireDelegated(turnContext.Identity);
    var profile = await _graphClient.GetMeAsync(graphToken, cancellationToken);
}
```

Agent Card composition merges the skill metadata from `[A2ASkill]` with the handler's configured
scheme and `RequiredScopes`. OAuth endpoints and scopes therefore remain in configuration instead
of being hard-coded in route code. At the beginning of the turn, the handler exchanges the inbound
Agent API token for a Graph token because `OBOScopes` contains `User.Read`;
`GetTurnTokenAsync` returns that Graph token to the route.

The OBO exchange requires the inbound token to be exchangeable. The sample client must acquire its
delegated token using the Agent API registration itself, so the token's audience and authorized-party
claims satisfy the SDK's exchange checks. Configure the client's `Authentication:PublicClientId` with
the Agent API client ID.

### Optional authorization policy

`AuthorizationPolicy` is the optional name of an ASP.NET Core authorization policy associated with
the handler:

```json
"AuthorizationPolicy": "EmployeesOnly"
```

It defaults to `null`. This preview records the name as handler metadata but does not automatically
evaluate the policy. Adding the setting does not secure a route by itself; apply and evaluate the
policy in the ASP.NET Core host or route code.

### Other OAuth variations

The introductory sample intentionally does not configure these variations.

#### Delegated token without OBO

A handler without `OBOConnectionName` or `OBOScopes` returns the validated inbound Agent API token
unchanged. This is useful when route code needs the caller's token or identity but does not call a
downstream service:

```json
"delegated": {
  "Type": "A2AUserAuthorization",
  "Settings": {
    "SecuritySchemeName": "delegated",
    "OAuthFlows": {
      "DeviceCode": {
        "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
        "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
        "Scopes": {
          "api://<agent-client-id>/access_as_user":
            "Access the A2A Agent API as the signed-in user."
        }
      }
    },
    "RequiredScopes": [
      "api://<agent-client-id>/access_as_user"
    ]
  }
}
```

#### Application token

An application token is used when a service or another agent calls the A2A agent without a signed-in
user:

```json
"application": {
  "Type": "A2AUserAuthorization",
  "Settings": {
    "SecuritySchemeName": "application",
    "OAuthFlows": {
      "ClientCredentials": {
        "TokenUrl": "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token",
        "Scopes": {
          "api://<agent-client-id>/.default":
            "Access the A2A Agent API with assigned application permissions."
        }
      }
    },
    "RequiredScopes": [
      "api://<agent-client-id>/.default"
    ]
  }
}
```

For Microsoft Entra, `.default` is sent to the token endpoint. The resulting token normally contains
application permissions in its `roles` claim rather than a delegated `scp` claim. Route code must
validate the required application role. Because the token represents an application rather than a
person, it does not provide a user identity for `ITurnState.User`.

#### Shared Agent Card schemes

Larger applications can define reusable schemes under
`AgentApplication:A2A:AgentCard:SecuritySchemes` and set a handler's `SecuritySchemeName` to the
shared name without configuring `OAuthFlows`. With `OAuthFlows`, `SecuritySchemeName` names the
inline scheme defined by the handler; without flows, it references the existing Agent Card scheme.
This avoids repeating OAuth endpoint metadata when several handlers use the same inbound scheme.
It is an advanced alternative to the self-contained handler used by this sample.

### Manual route check

After replacing `{{ClientId}}` and `{{TenantId}}` in `appsettings.json`, configure the sample client
for delegated authentication:

```text
:auth delegated
```

Send `-me`. The request proves that the agent accepts a delegated token whose audience is the Agent
API, exchanges it for a Graph token with `User.Read`, and returns the signed-in user's profile.

Expected failures:

- `-me` with `:auth none` fails because the route requires a validated token.
- `-me` with `:auth app` fails because OBO requires a delegated user token.
- `-me` with a delegated token acquired by a different public-client registration fails because the
  token is not exchangeable by this sample's OBO connection.
- A Microsoft Graph token must not be sent directly to the Agent API; its audience is incorrect.

## Adding A2A support to an existing SDK agent

For normal A2A agent authoring, import the root A2A namespace:

```csharp
using Microsoft.Agents.Extensions.A2A;
```

Add a responsibility-specific A2A namespace only when the agent directly uses an
API from that area.

1. Add a package dependency for `Microsoft.Agents.Extensions.A2A`.

The A2A adapter is registered automatically when the application calls `AddAgent`.

1. Add the A2A endpoints in `Program.cs`:

   ```csharp
   app.MapA2AApplicationEndpoints();
   ```

   This helper maps the well-known Agent Card plus the JSON-RPC and HTTP+JSON
   A2A endpoints, using `/a2a` by default.

1. It is recommended that your `AgentApplication` add the `Agent` and `A2ASkill` attributes (see the sample). Not doing so will work for development purposes, but `AgentCard` properties such as `Name`, `Description`, `Version`, and `Skills` will be defaulted.

## Overview of A2A to Activity Protocol (and back)

1. An inbound A2A `Message` is converted to `Activity` and passed to your `AgentApplication`.
   - `Message` -> `ActivityTypes.Message`
      - The `Activity.ChannelId` is `"a2a"`.
      - `TextPart` objects are appended to `Activity.Text`.
      - Other `Part` types are added as `Attachments`.
      - `Activity.ChannelData` is the A2A `Task` instance (cast `Activity.ChannelData` to `AgentTask`).
   - `tasks/cancel` -> `ActivityTypes.EndOfConversation`
     - The A2A task will already be in a terminal state, so any `ITurnContext.Send*` calls will be ignored.
     - Do any needed conversation cleanup as you normally would when receiving `EndOfConversation`.
     - Example handler for `EndOfConversation`:
       ```csharp
       OnActivity(ActivityTypes.EndOfConversation, (turnContext, turnState, ct) =>
       {
          turnState.Conversation.ClearState();
          return Task.CompletedTask;
       }
       ```
   - You can use your `AgentApplication.OnMessage` route as you normally would. If you need to handle A2A messages differently, you can use something like this to add a new message route in your `AgentApplication`:

      ```csharp
      OnMessage((turnContext, turnState, ct) =>
          {
              return Task.FromResult(turnContext.Activity.ChannelId == Channels.A2A);
          },
          OnA2AMessageAsync
      );
      ```
1. Outbound activities sent via `ITurnContext.Send*`
    1. `ActivityTypes.Message`
        1. `Activity.Text` -> `TextPart`
        1. `Attachments` -> `FilePart` for each
        1. `Activity.Entities` are included as `DataPart` for each, with schema in `DataPart.Metadata`.
        1. `Activity.Value` -> `DataPart` with schema in `Metadata`
    1. `ActivityTypes.EndOfConversation`
        1. `Activity.Value` is retained as an `Artifact` with the name `Result` on the `AgentTask`.
        1. Any `Activity.Text` is included in `Task.Status.Message`.
        1. The A2A task is moved to a terminal state using these `Activity.Code` values:
           - `EndOfConversationCodes.Error` -> `TaskState.Failed`
           - `EndOfConversationCodes.UserCancelled` -> `TaskState.Canceled`
           - Anything else -> `TaskState.Completed`
    1. Streaming responses
        1. The streaming response results in an `Artifact` on the A2A task.
        1. `StreamingResponse.QueueInformative` sets `Task.Status.Message`.
        1. The AI citation entity, if it exists, is included as a `DataPart` in the artifact.
    1. Other activity types are ignored.
    1. Be explicit with `Activity.InputHint`. This is required for A2A multi-turn behavior (see below).

## Turn concepts in A2A and Activity Protocol

### Single vs. multi-turn

1. Activity Protocol
   1. Multi-turn by default using `conversationId`, but with no enforced concept of "ended". It is a perpetual chat.
      1. `Activity.Conversation.Id` indicates which conversation a message belongs to, and the agent uses this for state.
   1. `EndOfConversation` sent by either side signals that the conversation is complete. When sent by the agent, it can contain a completion value in `Activity.Value` and a result in `Activity.Code`.
   1. `EndOfConversation` is used in the SDK when communicating with another agent over the Activity Protocol to indicate that the conversation is over, with an optional result.
   1. Subsequent messages to a `conversationId` could still be acted on unless the agent uses its own state to reject them.

1. A2A
   1. A2A has two concepts of interaction:
      1. Exchange of `Message` values between client and server
      1. A2A `Task` (`AgentTask` in the SDK)
   1. An interaction can start with only `Message` exchange, but can transition to a `Task`.
   1. Once a task is created, no individual message payload is sent outside the task; messages are sent via `Task.Status.Message`.
   1. Multi-turn continues while `Task.Status.State == "input-required"`.
   1. Once a task is terminal (`TaskState.Completed`, `TaskState.Failed`, or `TaskState.Canceled`), it is immutable and can no longer be acted on.

### Current handling in `Microsoft.Agents.Extensions.A2A`

1. Everything is in the context of an A2A `Task`. Agents SDK does not currently support the notion of a message interaction that later transitions to a task.
1. The SDK uses `taskId` as its conversation ID (`Activity.Conversation.Id`).
   1. This keeps state per task even within the same A2A `contextId`.
   1. While the SDK maintains `contextId` per A2A expectations, this is not currently used at the `AgentApplication` level. An SDK agent can still access the full task via `Activity.ChannelData`.
1. The SDK concept of "end of conversation" is quite similar to "task is complete".
1. SDK responses with `Activity.InputHint == InputHints.ExpectingInput` result in `Task.Status.State == InputRequired`.
   1. Ideally this should be the last activity sent in a turn.
1. The SDK agent must explicitly send `ActivityTypes.EndOfConversation` to complete a task:
   1. `Activity.Code` sets `Task.Status.State`
      1. `EndOfConversationCodes.Error` => `TaskState.Failed`
      1. `EndOfConversationCodes.UserCancelled` => `TaskState.Canceled`
      1. anything else => `TaskState.Completed`
   1. `Activity.Value` is added as an artifact.
   1. `Activity.Text` sets `Task.Status.Message`.
   1. `EndOfConversation` should be the last activity sent. Subsequent `ITurnContext.Send*` calls will be ignored because the task is terminal.
1. This may not be the final handling:
   1. Agents like `EchoAgent` will never complete because they never emit an `EndOfConversation`.
   1. Agent developers need to be aware of properties like `Activity.InputHint` and explicit `EndOfConversation` handling.
   1. Knowing when a task is complete can be a challenge.

## Interacting with this sample

- Sending `-multi` demonstrates a multi-turn interaction. Send `end` to complete the task.
- Sending `-stream` demonstrates `ITurnContext.StreamingResponse`.
- Sending any other input echoes the text back.
  - This differs from the `EmptyAgent` sample because every A2A interaction is an A2A `Task`, so each echo response is sent as an `EndOfConversation`.

## Further reading

To learn more about building agents, see the [Microsoft 365 Agents SDK](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/).
