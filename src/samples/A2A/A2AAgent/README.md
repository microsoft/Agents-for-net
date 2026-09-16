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
- The [A2AClient sample](../A2AClient/README.md) or another A2A client.
- For the authenticated `-me` skill, a Microsoft Entra application configured as
  described in [Configure the authenticated sample](#configure-the-authenticated-sample).

## What the sample demonstrates

| Input | Behavior | Authentication |
| --- | --- | --- |
| Any ordinary text | Echoes the text and completes the A2A task. | Anonymous |
| `-stream` | Streams informative and text updates with a citation. | Anonymous |
| `-multi` | Starts a task that remains `input-required`; send `end` to complete it. | Anonymous |
| `-a2a` | Sends a native A2A message through `IA2ATurnContext.Client`. | Anonymous |
| `-me` | Exchanges the delegated Agent API token for Microsoft Graph `User.Read` and returns the caller's profile. | Delegated OAuth |

The A2A endpoints are available at `http://localhost:3978/a2a` by default.

## Run anonymously

Start the agent:

```powershell
dotnet run --project src\samples\A2A\A2AAgent\A2AAgent.csproj
```

In another terminal, start the sample client:

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

Send ordinary text, `-stream`, or `-multi`. The shipped placeholders leave token
validation disabled during local development, so the anonymous scenarios work
without Microsoft Entra configuration.

## Configure the authenticated sample

The `-me` skill uses a single `graph` `A2AUserAuthorization` handler. The handler:

1. advertises a delegated Device Code flow and Agent API scope in the Agent Card;
2. requires that scope for the `-me` skill;
3. exchanges the validated inbound token through `ServiceConnection`; and
4. requests Microsoft Graph `User.Read`.

### Register the Agent API application

1. Create a single-tenant Microsoft Entra app registration for the Agent API.
1. Set `requestedAccessTokenVersion` to `2`.
1. In **Expose an API**, publish
   `api://<agent-client-id>/access_as_user`.
1. Add the delegated Microsoft Graph permission `User.Read`.
1. Grant the consent required by your tenant.
1. Enable public client flows so the sample client can use Device Code
   authentication.
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
    "DefaultHandlerName": "graph",
    "AutoSignin": false,
    "Handlers": {
      "graph": {
        "Type": "A2AUserAuthorization",
        "Settings": {
          "SecuritySchemeName": "delegated",
          "OAuthFlows": {
            "DeviceCode": {
              "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
              "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
              "Scopes": {
                "api://{{ClientId}}/access_as_user": "Access the A2A Agent API as the signed-in user."
              }
            }
          },
          "RequiredScopes": [
            "api://{{ClientId}}/access_as_user"
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
```

`A2AAgentStartup` enables token validation in Development only after
`TokenValidation:Audiences` contains real GUIDs. The A2A transport remains
anonymous; the `-me` route opts into the `graph` handler through
`autoSigninHandlers`.

### Configure and run the client

Configure the [A2AClient sample](../A2AClient/README.md) with:

- `Authentication:TenantId` set to the tenant ID; and
- `Authentication:PublicClientId` set to the Agent API client ID.

The Agent API registration itself must be used as the public client for this
sample so the inbound token satisfies the SDK's OBO exchange checks.

Start the client without `--auth-mode` and send:

```text
-me
```

The client matches the advertised skill example and selects its delegated
requirement automatically. Use `:auth delegated` to force delegated mode during
manual testing.

Expected failures:

- `-me` with `:auth none` does not provide the required token.
- `-me` with `:auth app` cannot perform a user-delegated Graph OBO exchange.
- A token acquired by a different public-client registration is not exchangeable
  by this sample.
- A Microsoft Graph token cannot be sent directly to the Agent API because its
  audience is incorrect.

## Further reading

- [Develop A2A agents with the Microsoft 365 Agents SDK](A2A-DEVELOPER-GUIDE.md)
- [A2AClient sample](../A2AClient/README.md)
- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
