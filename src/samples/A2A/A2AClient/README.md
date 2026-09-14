# A2AClient

`A2AClient` is an interactive console client for an A2A agent. It resolves the
public Agent Card anonymously, selects a JSON-RPC or HTTP+JSON interface, and
uses the same `HttpClient` for task operations in the selected authentication mode.

The Agent Card is data returned by the agent, so its advertised interface URLs are
not trusted blindly. The client only uses an interface on the configured agent
origin (scheme, host, and effective port), and only attaches the Agent API access
token to that origin. A card advertising an interface elsewhere fails before any
credential is sent. Plaintext HTTP is only accepted for loopback addresses, and
automatic redirects are disabled because redirects are followed beneath the
authenticating handler and would bypass the origin check.

## Run the client

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

The current startup options are:

```text
--agent <url>
--auth-mode none|delegated|app
--history
--use-push-notifications
--push-notification-receiver <url>
--help
```

While the client is running, use these interactive commands:

- `:auth none|delegated|app` switches the authentication mode for future requests.
  Changing the mode also drops any task the agent is waiting on, so one caller's
  continuing task is never resumed with another caller's credential.
- `:history on|off` turns task history display on or off.
- `:q` or `quit` exits the console.

A failed send, streaming read, or history read prints a short error and returns to
the prompt instead of ending the session. Ctrl+C still exits.

## Configuration keys

`src\samples\A2A\A2AClient\appsettings.json` contains placeholders for these local client keys:

```json
{
  "A2A": {
    "AgentUrl": "http://localhost:3978/a2a"
  },
  "Authentication": {
    "TenantId": "<tenant-id>",
    "PublicClientId": "<public-client-id>",
    "ConfidentialClientId": "<confidential-client-id>",
    "ConfidentialClientSecret": ""
  }
}
```

The client selects and configures the Device Code flow lazily on the first request in
`:auth delegated` mode, or the Client Credentials flow on the first request in `:auth app` mode.
Cards can advertise neither flow, either flow, or both. Startup and `:auth none` do not require
OAuth metadata or local authentication values. Startup `--auth-mode delegated|app` also resolves
the public card anonymously before attempting acquisition on the first request in that mode.
An unavailable or invalid flow is reported only when a token is requested for it.

The selected card security requirement provides
the Agent API scopes, while the selected OAuth scheme provides the token endpoint and the device
authorization endpoint where applicable. The client rejects any other flow, missing scheme, empty
acquisition scope list, or endpoint that is not absolute HTTPS.

The agent sample advertises a tenanted Client Credentials token endpoint:
`https://login.microsoftonline.com/{{TenantId}}/oauth2/v2.0/token`. Replace `{{TenantId}}`
with the agent's tenant ID using the same manual replacement convention as its existing
`TokenValidation` and connection settings. Entra client credentials cannot use `/organizations/`.
The Device Code token and device authorization endpoints intentionally keep `/organizations/`.

`PublicClientId` and `ConfidentialClientId` identify the client that calls the Agent API:

- `PublicClientId` is used for delegated user authentication (`:auth delegated`) through the
  device-code flow. It does not use a client secret. For `-delegated` testing, this can be a
  separate public-client registration. For `-me`, it must be the App ID of the Agent API
  registration itself so the agent can perform the on-behalf-of exchange.
- `ConfidentialClientId` is used for application-only authentication (`:auth app`) through the
  client-credentials flow. It identifies the confidential client registration whose secret is
  configured in `ConfidentialClientSecret`.

The client reads configuration from `appsettings.json`, `A2ACLIENT_`-prefixed
environment variables, and user secrets.

Keep real secrets out of source control. The committed file should stay on placeholders only.

## Public client setup for delegated testing

Use this flow for `-delegated` and `-me`.

1. Enable **public client flows** in **Authentication** on a Microsoft Entra app registration in the
   same tenant as the agent.
   - For `-delegated` only, this may be a separate public-client registration.
   - For `-me`, it **must be the Agent API registration itself**. The agent exchanges the inbound token
     on behalf of the caller, and the SDK only exchanges a token whose `aud` claim contains the
     application ID that requested it (`azp` for v2 tokens, `appid` for v1). A token acquired by a
     separate registration has `aud` = Agent API and `azp` = console client, so the exchange is refused.
     Also leave the optional `idtyp` claim off delegated tokens for that registration, because a token
     with `idtyp` of `user` is treated as non-exchangeable as well.
   - That Agent API registration must also use `requestedAccessTokenVersion = 2` in its manifest or
     API settings so `api://<agent-client-id>/access_as_user` issues a v2 token with the GUID audience
     expected by the agent's `TokenValidation:Audiences`.
1. In **API permissions**, add the delegated permission for the Agent API scope `api://<agent-client-id>/access_as_user`.
1. Grant consent if your tenant requires it.
1. Set `Authentication:PublicClientId` to that registration's client ID (the Agent API client ID when testing `-me`).
1. Set `Authentication:TenantId` to the tenant ID.
1. Start the client, then run `:auth delegated`.
1. Send `-delegated` to validate delegated passthrough.
1. Send `-me` to validate delegated on-behalf-of exchange to Microsoft Graph `User.Read`.

`-me` requires a user-delegated token for the Agent API. Do not acquire a Microsoft Graph token in the client and send it directly to the agent; the agent performs the OBO exchange itself.

## Confidential client setup for application-token testing

Use this flow for `-app`.

1. Create a **confidential client** Microsoft Entra app registration.
1. In **API permissions**, add the Agent API application permission `A2A.Access`.
1. Grant admin consent for that application permission.
1. Create a client secret for the confidential client.
1. Store the secret locally with user secrets:

   ```powershell
   dotnet user-secrets --project src\samples\A2A\A2AClient\A2AClient.csproj set "Authentication:ConfidentialClientSecret" "<secret>"
   ```

1. Set `Authentication:ConfidentialClientId` to the confidential client's client ID.
1. Set `Authentication:TenantId` to the tenant ID.
1. Start the client, then run `:auth app`.
1. Send `-app`.

Do not commit the secret. The secret belongs in user secrets or another local secret store, not in `appsettings.json`.

## Expected failures and how to interpret them

- `-delegated`, `-me`, or `-app` with `:auth none` fails because the route requires a validated inbound token.
- `-me` with `:auth app` fails because Microsoft Graph OBO requires a delegated user token, not an application token.
- `-me` with a delegated token acquired by a separate public-client registration fails because that token is not exchangeable; see the public client setup above.
- `-app` with `:auth delegated` is rejected by the agent because that route requires an application token.
- A Microsoft Graph token must not be pasted or sent directly to the Agent API. The inbound token must target the Agent API audience.
- An Agent Card whose interface URL is not on the configured agent origin fails at startup, before a token is sent.

Access tokens are attached to requests but are never printed by the sample, including in the error text shown after a failed request.
