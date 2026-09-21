// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.TaskModules;
using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Apps.TaskModules;
using System.Text.Json;

namespace TaskModules;

[TeamsExtension]
public partial class TaskModulesAgent(AgentApplicationOptions options, IConfiguration configuration) : AgentApplication(options)
{
    private readonly string _appBaseUrl = configuration.GetValue<string>("AppBaseUrl") ?? "http://localhost:3978";

    [TeamsMessageRoute]
    public Task OnMessageAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        return turnContext.SendActivityAsync(MessageFactory.Attachment(new Attachment(contentType: ContentTypes.AdaptiveCard, content: CardLoader.LoadCardJson("launcher-card.json"))), cancellationToken);
    }

    #region Simple Form
    [TeamsTaskFetchRoute("simple_form")]
    public Task<TaskModuleResponse> OnSimpleFormFetchAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest data, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateCardResponse(
            CardLoader.LoadCardJson("simple-form-card.json"),
            "Simple Form",
            TaskModuleSizes.Small,
            TaskModuleSizes.Small));
    }

    [TeamsTaskSubmitRoute("simple_form")]
    public async Task<TaskModuleResponse> OnSimpleFormSubmitAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form!", cancellationToken: cancellationToken);
        return CreateMessageResponse("Form was submitted");
    }
    #endregion

    #region Dialog with Webpage Content
    [TeamsTaskFetchRoute("webpage_dialog")]
    public Task<TaskModuleResponse> OnWebpageDialogFetchAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateUrlResponse($"{_appBaseUrl}/dialog-form", "Webpage Dialog", 500, 800));
    }

    [TeamsTaskSubmitRoute("webpage_dialog")]
    public async Task<TaskModuleResponse> OnWebpageDialogSubmitAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");
        var email = request.GetDataString("email", "No email provided");
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form! We got that your email is {email}", cancellationToken: cancellationToken);
        return CreateMessageResponse("Form submitted successfully");
    }
    #endregion

    #region Multi-Step Form
    [TeamsTaskFetchRoute("multi_step_form")]
    public Task<TaskModuleResponse> OnMultiStepFetchAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateCardResponse(
            CardLoader.LoadCardJson("multi-step-name-card.json"),
            "Multi-step Form Dialog",
            TaskModuleSizes.Small,
            TaskModuleSizes.Small));
    }

    [TeamsTaskSubmitRoute("multi_step_form_submit_name")]
    public Task<TaskModuleResponse> OnMultiStepSubmitNameAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");

        return Task.FromResult(CreateCardResponse(
            CardLoader.LoadCardJson("multi-step-email-card.json", new Dictionary<string, string> { ["name"] = name }),
            $"Thanks {name} - Get Email",
            TaskModuleSizes.Small,
            TaskModuleSizes.Small));
    }

    [TeamsTaskSubmitRoute("multi_step_form_submit_email")]
    public async Task<TaskModuleResponse> OnMultiStepSubmitEmailAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");
        var email = request.GetDataString("email", "No email provided");
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form! We got that your email is {email}", cancellationToken: cancellationToken);
        return CreateMessageResponse("Multi-step form completed successfully");
    }
    #endregion

    #region Mixed Example with Card and Webpage
    [TeamsTaskFetchRoute("mixed_example")]
    public Task<TaskModuleResponse> OnMixedExampleFetchAsync(ITeamsTurnContext turnContext, ITurnState turnState, TaskModuleRequest data, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateUrlResponse(
            "https://teams.microsoft.com/l/task/example-mixed",
            "Mixed Example",
            600,
            800));
    }
    #endregion

    private static TaskModuleResponse CreateCardResponse(JsonElement card, string title, object height, object width)
    {
        return TaskModuleResponse.CreateBuilder()
            .WithType(TaskModuleResponseTypes.Continue)
            .WithCard(new TeamsAttachment
            {
                ContentType = AttachmentContentTypes.AdaptiveCard,
                Content = card
            })
            .WithTitle(title)
            .WithHeight(height)
            .WithWidth(width)
            .Build()
            .Body
            ?? throw new InvalidOperationException("The task module response builder returned no body.");
    }

    private static TaskModuleResponse CreateMessageResponse(string message)
    {
        return TaskModuleResponse.CreateBuilder()
            .WithType(TaskModuleResponseTypes.Message)
            .WithMessage(message)
            .Build()
            .Body
            ?? throw new InvalidOperationException("The task module response builder returned no body.");
    }

    private static TaskModuleResponse CreateUrlResponse(string url, string title, object height, object width)
    {
        // The 2.1 builder only supports card-based Continue responses, so URL dialogs use the protocol payload directly.
        return new TaskModuleResponse
        {
            Task = new Response
            {
                Type = TaskModuleResponseTypes.Continue,
                Value = new { url, title, height, width }
            }
        };
    }
}
