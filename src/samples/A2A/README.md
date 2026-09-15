# A2A samples

The A2A samples live under `src\samples\A2A`.

| Sample | Project | Purpose |
| --- | --- | --- |
| [A2AAgent](A2AAgent/README.md) | `src\samples\A2A\A2AAgent\A2AAgent.csproj` | Hosts the A2A sample agent and demonstrates anonymous routes plus delegated OBO to Microsoft Graph. |
| [A2ATCKAgent](A2ATCKAgent/README.md) | `src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj` | Hosts the endpoint used for A2A TCK compatibility testing. |
| [A2AClient](A2AClient/README.md) | `src\samples\A2A\A2AClient\A2AClient.csproj` | Interactive console client that supports anonymous, delegated, and application-token authentication modes. |

## Quick start

1. Start the sample agent:

   ```powershell
   dotnet run --project src\samples\A2A\A2AAgent\A2AAgent.csproj
   ```

1. In another terminal, start the interactive client:

   ```powershell
   dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
   ```

1. Send a normal message to verify the anonymous echo flow.

1. For Microsoft Entra ID setup and authenticated route testing, follow:
   - [A2AAgent setup and route behavior](A2AAgent/README.md)
   - [A2AClient auth modes and manual commands](A2AClient/README.md)

## Manual authentication scenarios

| Scenario | Client mode | Request to send | Expected result |
| --- | --- | --- | --- |
| Anonymous echo | `:auth none` | any normal text | The sample echoes the text and completes the task. |
| Delegated OBO to Graph | `:auth delegated` | `-me` | The agent exchanges the inbound Agent API token for Microsoft Graph `User.Read` and returns the signed-in user's display name and UPN. |

The inbound access token must target the Agent API (`api://<agent-client-id>/access_as_user`).
Do not send a Microsoft Graph token directly to the A2A endpoint.
