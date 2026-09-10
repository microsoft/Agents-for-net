# A2AClient

`A2AClient` is an interactive console client for an A2A agent. It resolves the
Agent Card, selects a JSON-RPC or HTTP+JSON interface, and uses one authenticated
`HttpClient` for card discovery and task operations.

Run the client with:

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

Use `--auth-mode none|delegated|app` to select the initial authentication mode.
While the client is running, use `:auth none`, `:auth delegated`, or `:auth app`
to switch modes, `:history on|off` to control task history output, and `:q` or
`quit` to exit. Run with `--help` for all startup options.

The committed `appsettings.json` contains placeholders only. Store a confidential
client secret with user secrets or the `A2ACLIENT_Authentication__ConfidentialClientSecret`
environment variable. Access tokens are attached to requests but never printed.
