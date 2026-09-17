# A2A samples

The A2A samples live under `src\samples\A2A`.

| Sample | Project | Purpose |
| --- | --- | --- |
| [A2AAgent](A2AAgent/README.md) | `src\samples\A2A\A2AAgent\A2AAgent.csproj` | Hosts the two-provider A2A sample agent. It advertises only `-me` and `-issues`, keeps discovery anonymous, and enforces OAuth at the route level. |
| [A2ATCKAgent](A2ATCKAgent/README.md) | `src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj` | Hosts the endpoint used for A2A TCK compatibility testing. |
| [A2AClient](A2AClient/README.md) | `src\samples\A2A\A2AClient\A2AClient.csproj` | Interactive console client that resolves the Agent Card anonymously, predicts `-me` or `-issues`, and starts the matching delegated device-code flow. Application mode remains available as a client testing override for other agents. |

## Quick start

1. Start the sample agent:

   ```powershell
   dotnet run --project src\samples\A2A\A2AAgent\A2AAgent.csproj
   ```

2. In another terminal, start the interactive client:

   ```powershell
   dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
   ```

3. After applying your local Entra or GitHub configuration, send one of the protected sample requests:
   - `-me`
   - `-issues`

4. For setup details and placeholder guidance, follow:
   - [A2AAgent setup and route behavior](A2AAgent/README.md)
   - [A2AClient auth selection and manual overrides](A2AClient/README.md)

## Sample behavior

| Input | Behavior | Authentication |
| --- | --- | --- |
| `-me` | Exchanges the delegated Agent API token for Microsoft Graph `User.Read` and returns the caller's profile. | Delegated Entra Device Code |
| `-issues` | Validates the caller's opaque GitHub token and returns assigned open issues. | Delegated GitHub Device Flow |

The sample agent keeps the Agent Card and transport anonymous. Only the `-me` and `-issues` routes opt into `autoSigninHandlers`, so the client discovers the card without credentials and then runs the OAuth flow advertised for the selected skill.

The inbound access token for `-me` must target the Agent API scope `api://<agent-client-id>/access_as_user`. Do not send a Microsoft Graph token directly to the A2A endpoint.
