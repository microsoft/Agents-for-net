# Teams Task Modules (Dialogs)

This Agent has been created using [Microsoft 365 Agents SDK](https://github.com/microsoft/agents). It demonstrates how to use Teams **task modules** (also called dialogs in TeamsJS v2.x) — modal windows that bots can open to collect structured user input.

## What This Sample Demonstrates

- **Simple form dialog** — A single-step Adaptive Card with a Name input field
- **Webpage dialog** — A URL-based dialog loading an HTML form (Name + Email) from the bot's own endpoint
- **Multi-step form** — A two-step dialog flow: step 1 collects Name, step 2 collects Email. Uses `Response.WithCard()` to advance steps without closing the dialog (equivalent to `ContinueTask`).
- **Mixed example** — Placeholder showing how to combine approaches in a workflow
- **Launcher card** — Sending an Adaptive Card from a message handler with `TaskFetchAction` buttons

## Prerequisites

- Microsoft Teams is installed and you have an account (not a guest account)
- [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) (for Webpage Dialog, so Teams can reach your localhost)
- [M365 developer account](https://docs.microsoft.com/en-us/microsoftteams/platform/concepts/build-and-test/prepare-your-o365-tenant) or Teams account with app install permissions

## Running This Sample

1. [Create an Azure Bot](https://aka.ms/AgentsSDK-CreateBot)
   - Add the Teams Channel
   - Record the Application ID, Tenant ID, and Client Secret

1. Configure `appsettings.json`:

   ```json
   "TokenValidation": {
     "Audiences": ["{{ClientId}}"]
   },
   "Connections": {
     "ServiceConnection": {
       "Settings": {
         "AuthType": "ClientSecret",
         "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
         "ClientId": "{{ClientId}}",
         "ClientSecret": "{{ClientSecret}}"
       }
     }
   }
   ```

   Replace `{{ClientId}}`, `{{TenantId}}`, and `{{ClientSecret}}` with your Azure Bot values.

1. Start a dev tunnel on port 3978:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

1. For the **Webpage Dialog**, update the URL in `TaskModulesAgent.OnWebpageDialogFetchAsync`:

   ```csharp
   var url = "https://<your-tunnel-id>.devtunnels.ms/tabs/dialog-form";
   ```

1. Run the sample:

   ```bash
   dotnet run --project src/samples/Teams/TaskModules/TaskModules.csproj
   ```

1. Configure your Azure Bot's Messaging Endpoint to `https://<your-tunnel-id>.devtunnels.ms/api/messages`

1. Install the bot in Teams and send it any message — it will respond with the dialog launcher card.

## How It Works

When the user sends a message, the bot responds with an Adaptive Card showing four buttons. Each button uses a `TaskFetchAction` that triggers a `task/fetch` invoke activity routed by `[FetchRoute("verb")]` to the appropriate handler.

Form submissions inside the dialogs trigger `task/submit` invoke activities routed by `[SubmitRoute("verb")]` to their handlers.

The multi-step form demonstrates the `ContinueTask` pattern: the step-1 submit handler returns a new `Response.WithCard(step2Card)` instead of closing the dialog, keeping it open with fresh content for step 2.

## Project Structure

```
TaskModules/
├── Program.cs              # ASP.NET Core host setup
├── AspNetExtensions.cs     # Local JWT token validation extension
├── TaskModulesAgent.cs     # Agent logic: all route handlers + HTML form content
├── appsettings.json        # Configuration (auth, connections)
└── Properties/
    └── launchSettings.json # Dev launch profile
```
