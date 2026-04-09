# TaskModules Teams Sample Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create `src/samples/Teams/TaskModules/` — a Teams SDK sample that demonstrates task module dialogs (simple form, webpage URL dialog, multi-step form, mixed example) using attribute-based routing, mirroring the MessageExtensions sample structure.

**Architecture:** `[TeamsExtension]` partial class `TaskModulesAgent : AgentApplication` with `[FetchRoute]` and `[SubmitRoute]` attribute-decorated methods; one `[MessageRoute]` method sends the launcher Adaptive Card. `Program.cs` is a near-copy of `MessageExtensions/Program.cs`. `AspNetExtensions.cs` is copied verbatim from MessageExtensions. HTML dialog form served inline via `GetDialogFormHtml()` static method at `/tabs/dialog-form`.

**Tech Stack:** .NET 8, `Microsoft.Teams.Cards`, `Microsoft.Agents.Extensions.Teams`, `Microsoft.Agents.Hosting.AspNetCore`, `Microsoft.Agents.Authentication.Msal`, source generators via `Microsoft.Agents.Core.Analyzers` and `Microsoft.Agents.Extensions.Teams.Analyzers`.

**Source reference:** `D:\mcs-sdk\teams.net\Samples\Samples.Dialogs\Program.cs`
**Style reference:** `src/samples/Teams/MessageExtensions/`

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `src/samples/Teams/TaskModules/TaskModules.csproj` | Create | Project targeting net8.0 |
| `src/samples/Teams/TaskModules/appsettings.json` | Create | Configuration (auth, connections) |
| `src/samples/Teams/TaskModules/Properties/launchSettings.json` | Create | Dev launch profile (port 3978) |
| `src/samples/Teams/TaskModules/AspNetExtensions.cs` | Create | Copy from MessageExtensions — local `AddAgentAspNetAuthentication` extension |
| `src/samples/Teams/TaskModules/Program.cs` | Create | ASP.NET Core host setup |
| `src/samples/Teams/TaskModules/TaskModulesAgent.cs` | Create | All route handlers + HTML static method |
| `src/samples/Teams/TaskModules/README.md` | Create | Setup and usage documentation |

---

## Task 1: Create project scaffold files

**Files:**
- Create: `src/samples/Teams/TaskModules/TaskModules.csproj`
- Create: `src/samples/Teams/TaskModules/appsettings.json`
- Create: `src/samples/Teams/TaskModules/Properties/launchSettings.json`

- [ ] **Step 1: Create `TaskModules.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Teams.Cards" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\libraries\Authentication\Authentication.Msal\Microsoft.Agents.Authentication.Msal.csproj" />
    <ProjectReference Include="..\..\..\libraries\Extensions\Microsoft.Agents.Extensions.Teams\Microsoft.Agents.Extensions.Teams.csproj" />
    <ProjectReference Include="..\..\..\libraries\Hosting\AspNetCore\Microsoft.Agents.Hosting.AspNetCore.csproj" />
    <ProjectReference Include="..\..\..\libraries\Core\Microsoft.Agents.Core.Analyzers\Microsoft.Agents.Core.Analyzers.csproj" ReferenceOutputAssembly="false" OutputItemType="Analyzer" PrivateAssets="all" />
    <ProjectReference Include="..\..\..\libraries\Extensions\Microsoft.Agents.Extensions.Teams.Analyzers\Microsoft.Agents.Extensions.Teams.Analyzers.csproj" ReferenceOutputAssembly="false" OutputItemType="Analyzer" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create `appsettings.json`**

```json
{
  "TokenValidation": {
    "Enabled": false,
    "Audiences": [
      "{{ClientId}}" // this is the Client ID used for the Azure Bot
    ],
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
        "AuthType": "ClientSecret", // valid values: Microsoft.Agents.Authentication.Msal.Model.AuthTypes
        "AuthorityEndpoint": "https://login.microsoftonline.com/{{TenantId}}",
        "ClientId": "{{ClientId}}", // Client ID used for the Azure Bot
        "ClientSecret": "{{ClientSecret}}" // Client Secret used for the connection
      }
    }
  },
  "ConnectionsMap": [
    {
      "ServiceUrl": "*",
      "Connection": "ServiceConnection"
    }
  ],

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 3: Create `Properties/launchSettings.json`**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "TaskModules": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "hotReloadEnabled": false,
      "applicationUrl": "http://localhost:3978",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/samples/Teams/TaskModules/TaskModules.csproj src/samples/Teams/TaskModules/appsettings.json src/samples/Teams/TaskModules/Properties/launchSettings.json
git commit -m "feat(samples): add TaskModules project scaffold"
```

---

## Task 2: Copy AspNetExtensions.cs

**Files:**
- Create: `src/samples/Teams/TaskModules/AspNetExtensions.cs`

`AspNetExtensions.cs` is sample-local (the method is not in the shared library). Copy it verbatim from MessageExtensions.

- [ ] **Step 1: Copy file**

```bash
cp src/samples/Teams/MessageExtensions/AspNetExtensions.cs \
   src/samples/Teams/TaskModules/AspNetExtensions.cs
```

- [ ] **Step 2: Commit**

```bash
git add src/samples/Teams/TaskModules/AspNetExtensions.cs
git commit -m "feat(samples): add AspNetExtensions to TaskModules"
```

---

## Task 3: Create Program.cs

**Files:**
- Create: `src/samples/Teams/TaskModules/Program.cs`

- [ ] **Step 1: Create `Program.cs`**

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using TaskModules;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<TaskModulesAgent>();

// Register IStorage. For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra. Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/messages").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

// Map GET "/tabs/dialog-form" to return the HTML for the webpage dialog,
// which is defined in TaskModulesAgent.GetDialogFormHtml().
app.MapGet("/tabs/dialog-form", () => Results.Content(TaskModulesAgent.GetDialogFormHtml(), "text/html"));

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing.
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
```

- [ ] **Step 2: Commit**

```bash
git add src/samples/Teams/TaskModules/Program.cs
git commit -m "feat(samples): add TaskModules Program.cs"
```

---

## Task 4: Create TaskModulesAgent.cs

**Files:**
- Create: `src/samples/Teams/TaskModules/TaskModulesAgent.cs`

The agent class uses `[TeamsExtension]` which triggers the source generator to produce a `Teams` property of type `TeamsAgentExtension`. All route attributes are processed by `Microsoft.Agents.Extensions.Teams.Analyzers`.

Key API notes:
- `[FetchRoute("verb")]` matches `task/fetch` invoke activities where `activity.Value.data.verb == "verb"`
- `[SubmitRoute("verb")]` matches `task/submit` invoke activities where `activity.Value.data.verb == "verb"`
- `data.Data` at runtime is a `System.Text.Json.JsonElement` — use `TryGetProperty` to extract values
- `Response` (factory class) = `Microsoft.Agents.Extensions.Teams.App.TaskModules.Response`
- Return type = `Microsoft.Teams.Api.TaskModules.Response`
- `TaskFetchAction(Dictionary<string, object?> extraData)` creates an Adaptive Card button that opens a task module
- `SubmitActionData.NonSchemaProperties` carries custom key-value data (like `verb`) merged with form inputs on submit
- Launcher card sent as `Microsoft.Agents.Core.Models.Attachment` via `MessageFactory.Attachment()`
- Task module card sent as `Microsoft.Teams.Api.Attachment` via `Response.WithCard()`

- [ ] **Step 1: Create `TaskModulesAgent.cs`**

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Teams.Cards;
using Microsoft.Teams.Common;
using System.Text.Json;

namespace TaskModules;

[TeamsExtension]
public partial class TaskModulesAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    // ─── Message Handler ──────────────────────────────────────────────────────

    /// <summary>
    /// Responds to every incoming message with a launcher Adaptive Card containing
    /// four buttons, one per dialog type. Each button uses TaskFetchAction which
    /// triggers a task/fetch invoke when clicked, opening the corresponding dialog.
    /// </summary>
    [MessageRoute]
    public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard([
            new TextBlock("Teams Task Modules (Dialogs) Demo")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Large
            },
            new TextBlock("Choose a dialog type to open:")
            {
                Wrap = true,
                IsSubtle = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Actions = [
                new TaskFetchAction(new Dictionary<string, object?> { ["verb"] = "simple_form" })
                {
                    Title = "Simple Form"
                },
                new TaskFetchAction(new Dictionary<string, object?> { ["verb"] = "webpage_dialog" })
                {
                    Title = "Webpage Dialog"
                },
                new TaskFetchAction(new Dictionary<string, object?> { ["verb"] = "multi_step_form" })
                {
                    Title = "Multi-Step Form"
                },
                new TaskFetchAction(new Dictionary<string, object?> { ["verb"] = "mixed_example" })
                {
                    Title = "Mixed Example"
                }
            ]
        };

        // Launcher card is sent as a regular message — use Core Attachment type.
        var attachment = new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = card
        };
        return turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
    }

    // ─── Fetch Route Handlers (task/fetch) ───────────────────────────────────

    /// <summary>
    /// Opens a simple single-step Adaptive Card dialog with a Name field.
    /// Submit verb: "simple_form" → handled by OnSimpleFormSubmitAsync.
    /// </summary>
    [FetchRoute("simple_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormFetchAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard([
            new TextBlock("Simple Form") { Weight = TextWeight.Bolder, Size = TextSize.Large },
            new TextInput
            {
                Id = "name",
                Label = "Your Name",
                Placeholder = "Enter your name",
                IsRequired = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Actions = [
                new SubmitAction
                {
                    Title = "Submit",
                    Data = new Union<string, SubmitActionData>(new SubmitActionData
                    {
                        NonSchemaProperties = { ["verb"] = "simple_form" }
                    })
                }
            ]
        };

        return Task.FromResult(Response.WithCard(
            new Microsoft.Teams.Api.Attachment(card),
            "Simple Form",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    /// <summary>
    /// Opens a URL-based dialog loading the HTML form at /tabs/dialog-form.
    /// The HTML form submits with verb "webpage_dialog" → OnWebpageDialogSubmitAsync.
    /// NOTE: Update the URL to your dev tunnel or production URL in production.
    /// </summary>
    [FetchRoute("webpage_dialog")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogFetchAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        // For dev: hardcoded to localhost:3978. In production, read from configuration.
        var url = "http://localhost:3978/tabs/dialog-form";

        return Task.FromResult(Response.WithUrl(
            url,
            "Webpage Dialog",
            height: 500,
            width: 800));
    }

    /// <summary>
    /// Opens step 1 of a multi-step form (Name input).
    /// Submit verb: "multi_step_form" → OnMultiStepFormStep1SubmitAsync,
    /// which returns step 2 via Response.WithCard (ContinueTask pattern).
    /// </summary>
    [FetchRoute("multi_step_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFormFetchAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard([
            new TextBlock("Step 1 of 2") { Weight = TextWeight.Bolder, Size = TextSize.Large },
            new TextBlock("Enter your name to continue:") { Wrap = true, IsSubtle = true },
            new TextInput
            {
                Id = "name",
                Label = "Your Name",
                Placeholder = "Enter your name",
                IsRequired = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Actions = [
                new SubmitAction
                {
                    Title = "Next →",
                    Data = new Union<string, SubmitActionData>(new SubmitActionData
                    {
                        NonSchemaProperties = { ["verb"] = "multi_step_form" }
                    })
                }
            ]
        };

        return Task.FromResult(Response.WithCard(
            new Microsoft.Teams.Api.Attachment(card),
            "Multi-Step Form",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    /// <summary>
    /// Placeholder demonstrating a mixed (card + URL) dialog pattern.
    /// </summary>
    [FetchRoute("mixed_example")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMixedExampleFetchAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard([
            new TextBlock("Mixed Example") { Weight = TextWeight.Bolder, Size = TextSize.Large },
            new TextBlock("This demonstrates a placeholder for combining Adaptive Card " +
                         "and URL-based dialogs in a workflow.")
            {
                Wrap = true,
                IsSubtle = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json"
        };

        return Task.FromResult(Response.WithCard(
            new Microsoft.Teams.Api.Attachment(card),
            "Mixed Example",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    // ─── Submit Route Handlers (task/submit) ─────────────────────────────────

    /// <summary>
    /// Handles submission of the simple form.
    /// Reads "name" from submitted data, sends a greeting, closes the dialog.
    /// </summary>
    [SubmitRoute("simple_form")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormSubmitAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var name = ExtractString(data.Data, "name") ?? "Unknown";
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form!", cancellationToken: cancellationToken);
        return Response.WithMessage($"Form submitted by {name}.");
    }

    /// <summary>
    /// Handles submission of the webpage dialog HTML form.
    /// Reads "name" and "email" from submitted data, sends confirmation, closes the dialog.
    /// </summary>
    [SubmitRoute("webpage_dialog")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogSubmitAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var name = ExtractString(data.Data, "name") ?? "Unknown";
        var email = ExtractString(data.Data, "email") ?? "No email provided";
        await turnContext.SendActivityAsync($"Hi {name}! We received your email: {email}", cancellationToken: cancellationToken);
        return Response.WithMessage($"Thank you, {name}!");
    }

    /// <summary>
    /// Handles step-1 submission of the multi-step form.
    /// Reads "name", then returns step-2 card with name carried in hidden submit data.
    /// Equivalent to ContinueTask — the dialog stays open and shows the new card.
    /// </summary>
    [SubmitRoute("multi_step_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFormStep1SubmitAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var name = ExtractString(data.Data, "name") ?? "Unknown";

        var card = new AdaptiveCard([
            new TextBlock($"Step 2 of 2 — Hello, {name}!") { Weight = TextWeight.Bolder, Size = TextSize.Large },
            new TextBlock("Please enter your email address:") { Wrap = true, IsSubtle = true },
            new TextInput
            {
                Id = "email",
                Label = "Email",
                Placeholder = "Enter your email",
                IsRequired = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            Actions = [
                new SubmitAction
                {
                    Title = "Submit",
                    // Carry name forward in the hidden submit data alongside the verb
                    Data = new Union<string, SubmitActionData>(new SubmitActionData
                    {
                        NonSchemaProperties =
                        {
                            ["verb"] = "multi_step_form_2",
                            ["name"] = name
                        }
                    })
                }
            ]
        };

        return Task.FromResult(Response.WithCard(
            new Microsoft.Teams.Api.Attachment(card),
            "Multi-Step Form",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    /// <summary>
    /// Handles step-2 (final) submission of the multi-step form.
    /// Reads "name" (carried from step 1) and "email", sends confirmation, closes dialog.
    /// </summary>
    [SubmitRoute("multi_step_form_2")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFormStep2SubmitAsync(
        ITurnContext turnContext, ITurnState turnState,
        Microsoft.Teams.Api.TaskModules.Request data,
        CancellationToken cancellationToken)
    {
        var name = ExtractString(data.Data, "name") ?? "Unknown";
        var email = ExtractString(data.Data, "email") ?? "No email provided";
        await turnContext.SendActivityAsync($"Multi-step form complete! Name: {name}, Email: {email}", cancellationToken: cancellationToken);
        return Response.WithMessage("Form submitted successfully!");
    }

    // ─── HTML Content ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the HTML content for the webpage dialog, served at GET /tabs/dialog-form.
    /// The form collects Name and Email, then uses the Teams JS SDK to submit back
    /// with verb "webpage_dialog" → handled by OnWebpageDialogSubmitAsync.
    /// </summary>
    public static string GetDialogFormHtml()
    {
        return """
<!DOCTYPE html>
<html>
<head>
    <title>Teams Dialog Form</title>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.5.0/css/bootstrap.min.css">
    <script src="https://res.cdn.office.net/teams-js/2.22.0/js/MicrosoftTeams.min.js"></script>
    <style>
        body { margin: 0; padding: 10px; }
        .form-group { margin-bottom: 10px; }
    </style>
</head>
<body>
    <div class="container">
        <h3>Webpage Dialog Form</h3>
        <form id="customForm">
            <div class="form-group">
                <label for="name">Name:</label>
                <input type="text" class="form-control" id="name" name="name" required>
            </div>
            <div class="form-group">
                <label for="email">Email:</label>
                <input type="email" class="form-control" id="email" name="email" required>
            </div>
            <button type="submit" class="btn btn-primary">Submit</button>
        </form>
    </div>
    <script>
        microsoftTeams.app.initialize().then(() => {
            console.log("Teams SDK initialized");
        }).catch(err => {
            console.error("Teams SDK initialization failed:", err);
        });
        document.getElementById('customForm').addEventListener('submit', function(event) {
            event.preventDefault();
            microsoftTeams.dialog.url.submit({
                verb: 'webpage_dialog',
                name: document.getElementById('name').value,
                email: document.getElementById('email').value
            });
        });
    </script>
</body>
</html>
""";
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a string value from task module request data by property name.
    /// data.Data is a JsonElement at runtime — the result of deserializing activity.Value.
    /// </summary>
    private static string? ExtractString(object? rawData, string propertyName)
    {
        if (rawData is JsonElement element &&
            element.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/samples/Teams/TaskModules/TaskModulesAgent.cs
git commit -m "feat(samples): add TaskModulesAgent with all route handlers"
```

---

## Task 5: Build and verify

**Goal:** Verify the project compiles cleanly before creating the README.

- [ ] **Step 1: Build the TaskModules project**

```bash
dotnet build src/samples/Teams/TaskModules/TaskModules.csproj -c Debug
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 2: Fix any compilation errors**

Common issues to watch for:
- `Union<string, SubmitActionData>` requires `using Microsoft.Teams.Common;` — already included
- `TextInput` (not `InputText`) — correct class name in `Microsoft.Teams.Cards`
- `Attachment` (for launcher card) = `Microsoft.Agents.Core.Models.Attachment` — imported via `using Microsoft.Agents.Core.Models;`
- `Response` (factory class) = `Microsoft.Agents.Extensions.Teams.App.TaskModules.Response` — imported via `using Microsoft.Agents.Extensions.Teams.App.TaskModules;`
- If `TaskFetchAction` is not found: check it's in `Microsoft.Teams.Cards` namespace (same as `AdaptiveCard`)
- If `SubmitActionData` is not found: it's in `Microsoft.Teams.Cards` namespace

- [ ] **Step 3: Build the full SDK solution to check for regressions**

```bash
dotnet build src/Microsoft.Agents.SDK.sln -c Debug
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit any fixes**

```bash
git add -p
git commit -m "fix(samples): resolve compilation errors in TaskModules"
```

---

## Task 6: Create README.md

**Files:**
- Create: `src/samples/Teams/TaskModules/README.md`

- [ ] **Step 1: Create `README.md`**

```markdown
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

- [ ] **Step 2: Commit**

```bash
git add src/samples/Teams/TaskModules/README.md
git commit -m "feat(samples): add TaskModules README"
```

---

## Task 7: Final verification

- [ ] **Step 1: Build entire solution one more time**

```bash
dotnet build src/Microsoft.Agents.SDK.sln -c Debug
```

Expected: `Build succeeded`, 0 errors.

- [ ] **Step 2: Verify all required files exist**

```bash
ls src/samples/Teams/TaskModules/
```

Expected output includes:
```
AspNetExtensions.cs
appsettings.json
Program.cs
Properties/
README.md
TaskModules.csproj
TaskModulesAgent.cs
```

- [ ] **Step 3: Commit final state (if any outstanding changes)**

```bash
git status
# Only commit if there are uncommitted changes
git add src/samples/Teams/TaskModules/
git commit -m "feat(samples): complete TaskModules Teams sample"
```

---

## Manual Testing Checklist (post-deploy)

After deploying to Teams with a dev tunnel:

- [ ] Sending any message returns the launcher Adaptive Card with 4 buttons
- [ ] "Simple Form" opens a dialog with a Name input → submit sends "Hi {name}..." message and closes
- [ ] "Webpage Dialog" opens the HTML form at `/tabs/dialog-form` → submit sends name+email confirmation and closes
- [ ] "Multi-Step Form" opens step 1 (Name) → Next opens step 2 (Email, with name shown in header) → Submit sends both values and closes
- [ ] "Mixed Example" opens a card dialog with placeholder content
```
