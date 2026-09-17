# A2AAgent sample

This sample adds Agent2Agent (A2A) support to an `AgentApplication` and keeps the public surface intentionally small: it advertises only `-me` and `-issues`.

> [!IMPORTANT]
> `Microsoft.Agents.Extensions.A2A` is in preview. Its APIs and configuration may change before final release.

`Program.cs` is the sample's only composition root. It registers the ASP.NET Core authentication pipeline, the Entra token-validation settings, the GitHub bearer handler, the Microsoft Graph HTTP client, and the route dependencies used by `MyAgent`.

For extension concepts and Agent Card generation details, see the [A2A developer guide](A2A-DEVELOPER-GUIDE.md).

## What the sample demonstrates

| Input | Behavior | Authentication |
| --- | --- | --- |
| `-me` | Exchanges the delegated Agent API token for Microsoft Graph `User.Read` and returns the caller's display name plus mail or UPN. | Delegated Entra Device Code |
| `-issues` | Validates the caller's opaque GitHub token, requires the exact `repo` scope, and returns a bounded list of assigned open issues. | Delegated GitHub Device Flow |

The A2A endpoints are available at `http://localhost:3978/a2a` by default.

## Authentication model

- The Agent Card and transport stay anonymous.
- Each protected route opts into its own `autoSigninHandlers` value.
- `-me` uses the `graph` handler. The client acquires `api://<agent-client-id>/access_as_user`, the server validates that delegated Agent API token, and `A2AUserAuthorization` exchanges it through `ServiceConnection` for Microsoft Graph `User.Read`.
- `-issues` uses the `github` handler. The client acquires a GitHub device-flow token for `repo`, the server validates the opaque bearer token, and the route reuses that same validated token to query assigned issues.

## Run the sample

Start the agent:

```powershell
dotnet run --project src\samples\A2A\A2AAgent\A2AAgent.csproj
```

In another terminal, start the sample client:

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

Start the client without `--auth-mode` so it stays Agent Card-driven. Send `-me` or `-issues` and let the client select the advertised delegated requirement for that skill. `--auth-mode` and `:auth` remain manual testing overrides.

## Configure Microsoft Entra for `-me`

Use one Microsoft Entra app registration for both the Agent API resource and the device-code sign-in the client uses for `-me`.

1. Create or choose an app registration in your tenant.
2. Set `requestedAccessTokenVersion` to `2`.
3. Under **Expose an API**, publish `api://<agent-client-id>/access_as_user`.
4. Add the delegated Microsoft Graph permission `User.Read`.
5. Grant the consent required by your tenant.
6. Under **Authentication** > **Advanced settings**, set **Allow public client flows** to **Yes** so the client can use Device Code.
7. Create the client secret or other credential used by `Connections:ServiceConnection` for OBO.

In this sample, `<agent-client-id>` means the **A2A Agent API app registration client ID**. It is the same ID used by `TokenValidation:Audiences`, the same ID that owns `access_as_user`, and the same ID the client must use as `Authentication:PublicClientId` for `-me`. It is not a Microsoft Graph app ID and it is not a separate A2AClient app registration ID.

The committed `appsettings.json` stays on placeholders only:

```json
"UserAuthorization": {
  "DefaultHandlerName": "graph",
  "AutoSignin": false,
  "Handlers": {
    "graph": {
      "Assembly": "Microsoft.Agents.Extensions.A2A",
      "Type": "A2AUserAuthorization",
      "Settings": {
        "SecuritySchemeName": "delegated",
        "OAuthFlows": {
          "DeviceCode": {
            "DeviceAuthorizationUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
            "TokenUrl": "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
            "Scopes": {
              "api://<agent-client-id>/access_as_user": "Access the A2A Agent API as the signed-in user."
            }
          }
        },
        "RequiredScopes": [
          "api://<agent-client-id>/access_as_user"
        ],
        "EnforceRequiredScopes": true,
        "OBOConnectionName": "ServiceConnection",
        "OBOScopes": [
          "User.Read"
        ]
      }
    },
    "github": {
      "Assembly": "Microsoft.Agents.Extensions.A2A",
      "Type": "A2AUserAuthorization",
      "Settings": {
        "SecuritySchemeName": "github",
        "OAuthFlows": {
          "DeviceCode": {
            "DeviceAuthorizationUrl": "https://github.com/login/device/code",
            "TokenUrl": "https://github.com/login/oauth/access_token",
            "Scopes": {
              "repo": "Read the signed-in user's assigned open GitHub issues."
            }
          }
        },
        "RequiredScopes": [
          "repo"
        ]
      }
    }
  }
}
```

## Configure GitHub for `-issues`

1. Create a GitHub OAuth App.
2. Enable Device Flow for that OAuth App.
3. Configure `Authentication:GitHubClientId` in `A2AClient` with the OAuth App client ID.
4. Sign in through the client by sending `-issues`.

The agent-side sample does not start GitHub OAuth itself. The client acquires the device-flow token, the server validates the incoming bearer token, and the route uses the original validated token for the GitHub API call.

## Why the sample defaults to Agent Card device code

The sample intentionally exercises the OAuth metadata advertised in the Agent Card. It does not default to Azure Bot Service Generic OAuth or a browser broker because that would require Azure Bot-specific resource setup, connection names, user identity state, and callback/sign-in handling that are not represented in the Agent Card this sample publishes.

## Expected failures

- `-me` with `:auth none` does not provide the required Agent API token.
- `-me` with `:auth app` cannot complete a user-delegated Microsoft Graph OBO exchange.
- `-issues` with `:auth none` does not provide the required GitHub token.
- `-issues` with a token that lacks the exact `repo` scope is rejected before the route queries GitHub.

## Further reading

- [Develop A2A agents with the Microsoft 365 Agents SDK](A2A-DEVELOPER-GUIDE.md)
- [A2AClient sample](../A2AClient/README.md)
- [Microsoft 365 Agents SDK documentation](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/)
