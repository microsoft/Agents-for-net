# A2AClient

`A2AClient` is an interactive console client for an A2A agent. It resolves the
Agent Card, selects a JSON-RPC or HTTP+JSON interface, and uses one authenticated
`HttpClient` for card discovery and task operations.

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
- `:history on|off` turns task history display on or off.
- `:q` or `quit` exits the console.

## Configuration keys

`src\samples\A2A\A2AClient\appsettings.json` contains placeholders for these exact keys:

```json
{
  "A2A": {
    "AgentUrl": "http://localhost:3978/a2a"
  },
  "Authentication": {
    "TenantId": "<tenant-id>",
    "PublicClientId": "<public-client-id>",
    "ConfidentialClientId": "<confidential-client-id>",
    "ConfidentialClientSecret": "",
    "AgentDelegatedScope": "api://<agent-client-id>/access_as_user",
    "AgentApplicationScope": "api://<agent-client-id>/.default"
  }
}
```

The client reads configuration from `appsettings.json`, `A2ACLIENT_`-prefixed
environment variables, and user secrets.

Keep real secrets out of source control. The committed file should stay on placeholders only.

## Public client setup for delegated testing

Use this flow for `-delegated` and `-me`.

1. Create a **public client** Microsoft Entra app registration in the same tenant as the agent.
1. In **Authentication**, enable public client flows.
1. In **API permissions**, add the delegated permission for the Agent API scope `api://<agent-client-id>/access_as_user`.
1. Grant consent if your tenant requires it.
1. Set `Authentication:PublicClientId` to the public client application's client ID.
1. Set `Authentication:TenantId` to the tenant ID.
1. Set `Authentication:AgentDelegatedScope` to `api://<agent-client-id>/access_as_user`.
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
1. Set `Authentication:AgentApplicationScope` to `api://<agent-client-id>/.default`.
1. Start the client, then run `:auth app`.
1. Send `-app`.

Do not commit the secret. The secret belongs in user secrets or another local secret store, not in `appsettings.json`.

## Expected failures and how to interpret them

- `-me` with `:auth none` fails because the route requires a validated inbound token before it can perform OBO.
- `-me` with `:auth app` fails because Microsoft Graph OBO requires a delegated user token, not an application token.
- `-app` with `:auth delegated` may authenticate, but it is the wrong scenario and must not be interpreted as application identity.
- A Microsoft Graph token must not be pasted or sent directly to the Agent API. The inbound token must target the Agent API audience.

Access tokens are attached to requests but are never printed by the sample.
