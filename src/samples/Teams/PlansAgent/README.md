# Teams Plans (experimental)

This sample demonstrates application-controlled Teams Plan progress, step
revisions, an example source link, ordinary/streamed completion, stop, and error.
It uses **fixed demonstration data and text; no AI resource is required**.

The Plan is a progress display, not an AI planner or workflow engine. The
application chooses the two steps and increments their individual versions.
Short, fixed pauses make the progress visible in Teams; they do not represent
real work. The sample does not retrieve the linked documentation or perform
inference, and its output is not labeled AI-generated.
See the [Plan feature guide](../../../libraries/Extensions/Microsoft.Agents.Extensions.MSTeams/docs/teams-plans.md)
for saved bindings and transport/recovery details.

Compared with upstream PlanBot, this sample includes blocked progress and a
source icon as well as the four lifecycle commands. It does not start delayed
background jobs; saved-binding restoration is covered by SDK tests and the
feature guide rather than keeping a turn context or client alive in the sample.

## Prerequisites

- .NET 10 SDK.
- A registered single-tenant Azure Bot with its Teams channel enabled, its Entra
  app ID/tenant ID/client secret, and permission to install a custom Teams app.
- A Teams client/tenant with the experimental Plans feature available. Rendering
  and preview availability are controlled by Teams.
- A reachable HTTPS messaging endpoint, such as a
  [dev tunnel](https://learn.microsoft.com/azure/developer/dev-tunnels/get-started).

## Configure and run

Create `appsettings.Development.json` in this sample's directory with these local
overrides. This file is git-ignored and loaded automatically by the Development
launch profile. Keep the tracked `appsettings.json` unchanged.

```json
{
  "TokenValidation": {
    "Audiences": ["<bot-app-id>"],
    "TenantId": "<tenant-id>"
  },
  "Connections": {
    "ServiceConnection": {
      "Settings": {
        "AuthorityEndpoint": "https://login.microsoftonline.com/<tenant-id>",
        "ClientId": "<bot-app-id>",
        "ClientSecret": "<bot-client-secret>"
      }
    }
  }
}
```

Equivalent environment variables use double underscores, for example
`Connections__ServiceConnection__Settings__ClientSecret`. Do not put credentials
in tracked files.

From the repository root, run the app and expose its HTTP port in another
terminal:

```powershell
$project = ".\src\samples\Teams\PlansAgent\PlansAgent.csproj"
dotnet run --project $project --launch-profile PlansAgent
```

```powershell
devtunnel host -p 3978 --allow-anonymous
```

Set the Azure Bot messaging endpoint to `https://<tunnel-host>/api/messages`.
The app **requires incoming JWT authentication even in Development**. Anonymous
tunnel access is necessary for the channel to reach it, not permission to
disable token validation.

Copy the `appManifest` files to a local packaging directory. Replace every
`<<AAD_APP_CLIENT_ID>>` with your bot app ID and `<<BOT_DOMAIN>>` with the tunnel
hostname; customize the developer URLs. Zip `manifest.json`, `color.png`, and
`outline.png` at the ZIP root, then install/update that package in Teams.
Do not commit a personalized manifest or generated ZIP.

In the bot's personal chat, send:

> complete-stream

In a channel or group chat, mention the bot. Send `help` for a short description.
Targeted/private prompts are rejected with a private explanation instead of
starting a conversation-wide Plan.

## Automated sample tests

The sample tests use `Microsoft.Agents.Builder.Testing.AgentTestHost` and
`TestFlow` with an in-memory connector boundary. They exercise real routing,
versioned progress, source metadata, normal/streamed completion, stop/error,
help, and targeted-prompt rejection without credentials or Azure resources:

```powershell
dotnet test .\src\tests\Microsoft.Agents.SampleTest\Microsoft.Agents.SampleTest.csproj --filter FullyQualifiedName~TeamsPlansAgentTests
```

## Manual validation only

Builds and resource-free sample/SDK tests can run in CI. The following live checks
require your registered bot and Teams; **they are not pipeline tests**.

| Check | Expected observation |
| --- | --- |
| `complete` | `Prepare example` starts active and `Deliver example` starts proposed, both at version 1. Preparation version 2 becomes blocked, then version 3 completes with an example source and Text icon. Delivery version 2 becomes active, then version 3 completes before one ordinary final message completes the Plan. |
| `complete-stream` | The same revisions appear, with fixed text streamed while delivery is active. The actual final streamed message completes the Plan without an AI-generated label. |
| `stop` | The Plan starts, then explicitly stops. No completion message is sent. |
| `error` | Preparation version 2 reports a step error, then the application explicitly reports a Plan-level error. No completion message is sent. |
| `help` or an unrecognized command | A command list is returned without starting a Plan. |
| Validate in a supported channel thread or 1:1 reply chain | Progress remains tied to the originating prompt and the final answer uses its conversation/thread destination. |

All sends and display pauses use the turn's cancellation token. There is no
sample-specific work timeout.
The `stop` command ends its demonstration explicitly; it is not a Teams UI Stop
button. Service failures and uncertain completion are not retried automatically.
`PlanConflict` requires reconciliation, not automatic retry.

The SDK tests simulate service timeout rejections and assert final POST recovery,
Core metadata preservation, conflict propagation, and duplicate prevention.
Do not wait ten minutes or expect a Teams UI Stop button to reproduce those
transport cases. Agents Playground can inspect payloads but is not proof of
Teams Plan rendering.
