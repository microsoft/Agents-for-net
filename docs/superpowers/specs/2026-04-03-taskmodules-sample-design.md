# TaskModules Sample Design

**Date:** 2026-04-03
**Status:** Approved
**Source:** `D:\mcs-sdk\teams.net\Samples\Samples.Dialogs`
**Destination:** `src/samples/Teams/TaskModules/`

## Overview

Port the `Samples.Dialogs` project from the teams.net SDK to the Agents SDK. The sample demonstrates Teams **task modules** (dialogs) — modal windows that bots can open to collect user input. Four dialog types are demonstrated: a simple adaptive card form, a URL-based HTML webpage dialog, a multi-step adaptive card form, and a mixed example placeholder.

## Project Structure

```
src/samples/Teams/TaskModules/
├── TaskModules.csproj
├── Program.cs
├── AspNetExtensions.cs
├── TaskModulesAgent.cs
├── appsettings.json
├── Properties/
│   └── launchSettings.json
└── README.md
```

## Architecture

### Hosting (Program.cs)

Mirrors `MessageExtensions/Program.cs` exactly:

```csharp
builder.AddAgentApplicationOptions();
builder.AddAgent<TaskModulesAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

app.UseAuthentication();
app.UseAuthorization();
app.MapAgentRootEndpoint();
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

// Serve HTML form for the webpage dialog
app.MapGet("/tabs/dialog-form", () =>
    Results.Content(TaskModulesAgent.GetDialogFormHtml(), "text/html"));

// Dev-only: hardcode port 3978
if (app.Environment.IsDevelopment())
    app.Urls.Add("http://localhost:3978");
```

### Agent Class (TaskModulesAgent.cs)

```csharp
[TeamsExtension]
public partial class TaskModulesAgent(AgentApplicationOptions options) : AgentApplication(options)
```

Uses attribute-based routing — consistent with `MessageExtensionsAgent`. `AspNetExtensions.cs` is copied from MessageExtensions (it defines the sample-local `AddAgentAspNetAuthentication` extension method, as this method is not part of the shared library).

### Route Methods

| Method | Attribute | Return | Description |
|---|---|---|---|
| `OnMessageAsync` | `[MessageRoute]` | `Task` | Sends launcher Adaptive Card with 4 buttons |
| `OnSimpleFormFetchAsync` | `[FetchRoute("simple_form")]` | `Task<Response>` | Returns card: one Name text input |
| `OnWebpageDialogFetchAsync` | `[FetchRoute("webpage_dialog")]` | `Task<Response>` | Returns URL to `/tabs/dialog-form` |
| `OnMultiStepFormFetchAsync` | `[FetchRoute("multi_step_form")]` | `Task<Response>` | Returns step-1 card (Name input, submit verb = `multi_step_form`) |
| `OnMixedExampleFetchAsync` | `[FetchRoute("mixed_example")]` | `Task<Response>` | Placeholder card or URL |
| `OnSimpleFormSubmitAsync` | `[SubmitRoute("simple_form")]` | `Task<Response>` | Sends "Hi {name}!" message; returns `Response.WithMessage(...)` |
| `OnWebpageDialogSubmitAsync` | `[SubmitRoute("webpage_dialog")]` | `Task<Response>` | Sends name+email confirmation; returns `Response.WithMessage(...)` |
| `OnMultiStepFormStep1SubmitAsync` | `[SubmitRoute("multi_step_form")]` | `Task<Response>` | Receives Name from step-1; returns step-2 card (Email input, name in hidden data, submit verb = `multi_step_form_2`) via `Response.WithCard(...)` |
| `OnMultiStepFormStep2SubmitAsync` | `[SubmitRoute("multi_step_form_2")]` | `Task<Response>` | Receives Name + Email; sends final confirmation; returns `Response.WithMessage(...)` |
| `GetDialogFormHtml()` | `public static string` | `string` | Returns HTML string for webpage dialog (Name + Email form) |

### Multi-Step Dialog Flow

The multi-step form uses one FetchRoute and two SubmitRoutes:

1. User clicks "Multi-Step Form" button → `task/fetch` → `[FetchRoute("multi_step_form")]` → returns step-1 card with Name input. The card's submit action data includes `{"verb": "multi_step_form"}`.
2. User fills in Name and submits → `task/submit` → `[SubmitRoute("multi_step_form")]` → reads Name from submitted data, builds step-2 card (Email input + hidden Name field, submit verb = `"multi_step_form_2"`), returns `Response.WithCard(step2Card)` — equivalent to ContinueTask.
3. User fills in Email and submits → `task/submit` → `[SubmitRoute("multi_step_form_2")]` → reads Name + Email, sends confirmation message, returns `Response.WithMessage(...)` to close dialog.

Note: `task/fetch` is only triggered by the initial dialog open (launcher card button). All subsequent submissions within an open dialog are `task/submit` activities handled by SubmitRoute handlers.

### Verb Conventions

- **Verb field**: Uses SDK default `"verb"` (not source's `"opendialogtype"`/`"submissiontype"`)
- Adaptive card action data includes `{"verb": "<dialog-type>"}` for FetchRoute dispatch
- HTML form's submit JavaScript passes `{ verb: "webpage_dialog", name: ..., email: ... }`

### Adaptive Card Building

Uses `Microsoft.Teams.Cards.AdaptiveCard` C# object model (same as MessageExtensions), not raw JSON strings.

Launcher card has 4 `Action.Execute` buttons (type `task/fetch`), each with appropriate `verb` value.

### HTML Form (GetDialogFormHtml)

Inline C# string (like `GetSettingsHtml()` in MessageExtensions). Contains:
- Name and Email text inputs
- Bootstrap 4 CSS for styling
- Teams JS SDK (`MicrosoftTeams.min.js`)
- Submit calls `microsoftTeams.tasks.submitTask({ verb: "webpage_dialog", name, email })`

## Configuration

### appsettings.json

Same structure as MessageExtensions:

```json
{
  "TokenValidation": {
    "Enabled": false,
    "Audiences": ["{{ClientId}}"],
    "TenantId": "{{TenantId}}"
  },
  "AgentApplication": {
    "StartTypingTimer": false,
    "RemoveRecipientMention": false,
    "NormalizeMentions": false
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
  },
  "ConnectionsMap": [{"ServiceUrl": "*", "Connection": "ServiceConnection"}],
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### TaskModules.csproj

Target `net8.0`. Project references:
- `Microsoft.Agents.Extensions.Teams`
- `Microsoft.Agents.Hosting.AspNetCore`
- `Microsoft.Agents.Authentication.Msal`
- `Microsoft.Agents.Core.Analyzers` (analyzer, no output assembly)
- `Microsoft.Agents.Extensions.Teams.Analyzers` (analyzer, no output assembly)

Package references:
- `Microsoft.Teams.Cards`

## What This Sample Demonstrates

1. **Simple form dialog**: Adaptive Card with a single text input, opened via task/fetch
2. **Webpage dialog**: URL-based dialog loading an HTML form from the bot's own endpoint
3. **Multi-step form**: Dialog flow across two steps using `Response.WithCard()` to advance
4. **Mixed example**: Placeholder showing URL-based dialog alternative
5. **Launcher card**: Sending an Adaptive Card from a message handler with action buttons

## Files Not Ported

- `Properties/launchSettings.TEMPLATE.json` — not needed; configuration is in `appsettings.json`
- Embedded resource (`Web/**`) — HTML is served inline via C# string, like `GetSettingsHtml()`
- DevTools plugin reference — not part of the Agents SDK pattern
