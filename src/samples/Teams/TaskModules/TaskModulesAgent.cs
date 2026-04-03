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
