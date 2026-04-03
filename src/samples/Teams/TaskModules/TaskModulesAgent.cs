// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Teams.Cards;

namespace TaskModules;

[TeamsExtension]
public partial class TaskModulesAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    #region Message Route
    [MessageRoute]
    public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new Microsoft.Teams.Cards.TextBlock("Select the examples you want to see!")
            {
                Size = Microsoft.Teams.Cards.TextSize.Large,
                Weight = Microsoft.Teams.Cards.TextWeight.Bolder
            }
        ])
        {
            Actions = new List<Microsoft.Teams.Cards.Action>
            {
                new Microsoft.Teams.Cards.TaskFetchAction(
                    Microsoft.Teams.Cards.TaskFetchAction.FromObject(new { opendialogtype = "simple_form" }))
                {
                    Title = "Simple form test"
                },
                new Microsoft.Teams.Cards.TaskFetchAction(
                    Microsoft.Teams.Cards.TaskFetchAction.FromObject(new { opendialogtype = "webpage_dialog" }))
                {
                    Title = "Webpage Dialog"
                },
                new Microsoft.Teams.Cards.TaskFetchAction(
                    Microsoft.Teams.Cards.TaskFetchAction.FromObject(new { opendialogtype = "multi_step_form" }))
                {
                    Title = "Multi-step Form"
                },
                new Microsoft.Teams.Cards.TaskFetchAction(
                    Microsoft.Teams.Cards.TaskFetchAction.FromObject(new { opendialogtype = "mixed_example" }))
                {
                    Title = "Mixed Example"
                }
            }
        };

        // Launcher card is sent as a regular message — use Core Attachment type.
        var attachment = new Attachment
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = card
        };

        return turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
    }
    #endregion

    #region Fetch Route Handlers
    [FetchRoute("simple_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormFetchAsync(
        ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
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
                    Data = new Microsoft.Teams.Common.Union<string, SubmitActionData>(new SubmitActionData
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

    [FetchRoute("webpage_dialog")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogFetchAsync(
        ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    {
        // For dev: hardcoded to localhost:3978. In production, read from configuration.
        var url = "http://localhost:3978/tabs/dialog-form";

        return Task.FromResult(Response.WithUrl(
            url,
            "Webpage Dialog",
            height: 500,
            width: 800));
    }

    [FetchRoute("multi_step_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFormFetchAsync(
        ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
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
                    Data = new Microsoft.Teams.Common.Union<string, SubmitActionData>(new SubmitActionData
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

    [FetchRoute("mixed_example")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMixedExampleFetchAsync(
        ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
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
    #endregion

    #region Submit Route Handlers
    [SubmitRoute("simple_form")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormSubmitAsync(
        ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form!", cancellationToken: cancellationToken);
        return Response.WithMessage($"Form submitted by {name}.");
    }

    [SubmitRoute("webpage_dialog")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogSubmitAsync(
        ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";
        var email = data.GetValueOrDefault("email") ?? "No email provided";
        await turnContext.SendActivityAsync($"Hi {name}! We received your email: {email}", cancellationToken: cancellationToken);
        return Response.WithMessage($"Thank you, {name}!");
    }

    [SubmitRoute("multi_step_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFormStep1SubmitAsync(
        ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";

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
                    Data = new Microsoft.Teams.Common.Union<string, SubmitActionData>(new SubmitActionData
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

    [SubmitRoute("multi_step_form_2")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFormStep2SubmitAsync(
        ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";
        var email = data.GetValueOrDefault("email") ?? "No email provided";
        await turnContext.SendActivityAsync($"Multi-step form complete! Name: {name}, Email: {email}", cancellationToken: cancellationToken);
        return Response.WithMessage("Form submitted successfully!");
    }
    #endregion
}
