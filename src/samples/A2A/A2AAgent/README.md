# A2AAgent Sample

This sample shows how to add A2A support to an `AgentApplication`, including
anonymous routes plus route-scoped delegated, OBO, and application-token
authorization.

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

The sample opts into authentication per route:

| Route | Handler | What it validates |
| --- | --- | --- |
| `-delegated` | `delegated` | Validates an inbound delegated Agent API token and echoes identity claims. |
| `-me` | `graph` | Validates an inbound delegated Agent API token, then performs OBO to Microsoft Graph `User.Read`. |
| `-app` | `app` | Validates an inbound application token. Application identities are not users, so this route does not use `ITurnState.User`. |

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
  `-delegated`, `-me`, and `-app` obtain the validated token through their route handlers. The Activity
  Protocol endpoint mapped by `MapDefaultAgentEndpoints` does require authorization.

### 1. Register the Agent API application

1. Create a **single-tenant** Microsoft Entra app registration for the agent API.
1. In the app registration **Manifest** or **API settings**, set `requestedAccessTokenVersion` to `2` so the delegated scope issues a v2 access token whose `aud` is the API's GUID, which matches `TokenValidation:Audiences`.
1. In **Expose an API**, publish the delegated scope `api://<agent-client-id>/access_as_user`.
1. Add an app role named `A2A.Access` and allow the `Applications` member type.
1. Add the delegated Microsoft Graph permission `User.Read`.
1. Grant the tenant consent required by your environment.
1. Create the client secret or other credential used by `Connections:ServiceConnection`.
1. Update `src\samples\A2A\A2AAgent\appsettings.json` so `TokenValidation:Audiences` contains the Agent API client ID and `TokenValidation:TenantId` contains the tenant ID.
1. Update `Connections:ServiceConnection:Settings:AuthorityEndpoint`, `ClientId`, and the credential values for the same Agent API registration.

For the current sample, the relevant keys are:

```json
"TokenValidation": {
  "Audiences": [
    "<agent-client-id>"
  ],
  "TenantId": "<tenant-id>"
},
"Connections": {
  "ServiceConnection": {
    "Settings": {
      "AuthType": "ClientSecret",
      "AuthorityEndpoint": "https://login.microsoftonline.com/<tenant-id>",
      "ClientId": "<agent-client-id>",
      "ClientSecret": "<local-secret>",
      "Scopes": [
        "https://api.botframework.com/.default"
      ]
    }
  }
}
```

Do not commit a real secret or token. Keep placeholders in the repo and store the live value locally.

### 2. Registration requirement for the OBO route

`-me` exchanges the inbound token on behalf of the caller. The SDK only exchanges a token that
`AgentClaims.IsExchangeableToken` accepts, which requires the token's `aud` claim to contain the
application ID that requested it (`azp` for v2 tokens, `appid` for v1). A delegated token acquired by a
*separate* public-client registration has `aud` = Agent API and `azp` = console client, so it is not
exchangeable and `-me` fails with "token is not exchangeable".

For `-me` to work:

- acquire the delegated token with the **Agent API registration itself** — enable public client flows on
  that registration and set the client's `Authentication:PublicClientId` to the Agent API client ID; and
- do not add the optional `idtyp` claim to delegated tokens for that registration, because a token with
  `idtyp` of `user` is also treated as non-exchangeable.

`-delegated` and `-app` do not perform OBO, so they work with a separate console client registration.

### 3. Manual route checks

After the Agent API app registration is in place and the client is configured:

- `:auth delegated` + `-delegated` proves the agent accepts a delegated token whose audience is the Agent API.
- `:auth delegated` + `-me` proves the agent can exchange that inbound user token on behalf of the caller for Microsoft Graph `User.Read`.
- `:auth app` + `-app` proves the agent can validate an application token without treating the caller as a user.

The inbound token must target the Agent API, never Microsoft Graph directly. `-me` relies on the agent's `graph` authorization handler to do the OBO exchange after token validation.

### 4. Expected failures

- `-delegated`, `-me`, or `-app` with `:auth none` fails because the route requires a validated token.
- `-me` with `:auth app` fails because OBO requires a delegated user token.
- `-me` with a delegated token from a separate public-client registration fails because that token is not exchangeable (see the registration requirement above).
- `-app` with `:auth delegated` is rejected because the route requires an application token.
- A Microsoft Graph token must not be pasted or sent directly to the Agent API.

## Adding A2A support to an existing SDK agent

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

### Current handling in `Microsoft.Agents.Hosting.A2A`

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
