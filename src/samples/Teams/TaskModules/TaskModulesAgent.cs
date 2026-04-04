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
public partial class TaskModulesAgent(AgentApplicationOptions options, IConfiguration configuration) : AgentApplication(options)
{
    private readonly string _appBaseUrl = configuration.GetValue<string>("AppBaseUrl") ?? "http://localhost:3978";

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

    #region Simple Form
    [FetchRoute("simple_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
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

    [SubmitRoute("simple_form")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormSubmitAsync(ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form!", cancellationToken: cancellationToken);
        return Response.WithMessage("Form was submitted");
    }
    #endregion

    #region Dialog with Webpage Content
    [FetchRoute("webpage_dialog")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    {
        // For dev: hardcoded to localhost:3978. In production, read from configuration.
        var url = $"{_appBaseUrl}/tabs/dialog-form";

        return Task.FromResult(Response.WithUrl(
            url,
            "Webpage Dialog",
            height: 500,
            width: 800));
    }

    [SubmitRoute("webpage_dialog")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogSubmitAsync(ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";
        var email = data.GetValueOrDefault("email") ?? "No email provided";
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form! We got that your email is {email}", cancellationToken: cancellationToken);
        return Response.WithMessage($"Form submitted successfully");
    }
    #endregion

    #region Multi-Step Form
    [FetchRoute("multi_step_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    {
        var card = new AdaptiveCard([
            new TextBlock("This is a multi-step form") { Weight = TextWeight.Bolder, Size = TextSize.Large },
            new TextInput
            {
                Id = "name",
                Label = "Name",
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
                        NonSchemaProperties = { ["verb"] = "webpage_dialog_step_1" }
                    })
                }
            ]
        };

        return Task.FromResult(Response.WithCard(
            new Microsoft.Teams.Api.Attachment(card),
            "Multi-step Form Dialog",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    [SubmitRoute("webpage_dialog_step_1")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepSubmitNameAsync(ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";

        var card = new AdaptiveCard([
            new TextBlock($"Email, {name}!") { Weight = TextWeight.Bolder, Size = TextSize.Large },
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
                            ["verb"] = "webpage_dialog_step_2",
                            ["name"] = name
                        }
                    })
                }
            ]
        };

        return Task.FromResult(Response.WithCard(
            new Microsoft.Teams.Api.Attachment(card),
            $"Thanks {name} - Get Email",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    [SubmitRoute("webpage_dialog_step_2")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepSubmitEmailAsync(ITurnContext turnContext, ITurnState turnState, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var name = data.GetValueOrDefault("name") ?? "Unknown";
        var email = data.GetValueOrDefault("email") ?? "No email provided";
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form! We got that your email is {email}", cancellationToken: cancellationToken);
        return Response.WithMessage("Multi-step form completed successfully");
    }
    #endregion

    #region Mixed Example with Card and Webpage
    [FetchRoute("mixed_example")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMixedExampleFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    {
        return Task.FromResult(Response.WithUrl(
            "https://teams.microsoft.com/l/task/example-mixed",
            "Mixed Example",
            600,
            800));
    }
    #endregion
}
