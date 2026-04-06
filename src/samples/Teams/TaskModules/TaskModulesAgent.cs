// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.TaskModules;

namespace TaskModules;

[TeamsExtension]
public partial class TaskModulesAgent(AgentApplicationOptions options, IConfiguration configuration) : AgentApplication(options)
{
    private readonly string _appBaseUrl = configuration.GetValue<string>("AppBaseUrl") ?? "http://localhost:3978";

    [MessageRoute]
    public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        return turnContext.SendActivityAsync(MessageFactory.Attachment(new Attachment(contentType: ContentTypes.AdaptiveCard, content: CardLoader.LoadCardJson("launcher-card.json"))), cancellationToken);
    }

    #region Simple Form
    [TaskFetchRoute("simple_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(
                new Microsoft.Teams.Api.TaskModules.TaskInfo
                {
                    Card = new Microsoft.Teams.Api.Attachment(ContentTypes.AdaptiveCard, CardLoader.LoadCardJson("simple-form-card.json")),
                    Title = "Simple Form",
                    Height = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(Microsoft.Teams.Api.TaskModules.Size.Small),
                    Width = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(Microsoft.Teams.Api.TaskModules.Size.Small)
                })));
    }

    [TaskSubmitRoute("simple_form")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnSimpleFormSubmitAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form!", cancellationToken: cancellationToken);
        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.MessageTask("Form was submitted"));
    }
    #endregion

    #region Dialog with Webpage Content
    [TaskFetchRoute("webpage_dialog")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(
                new Microsoft.Teams.Api.TaskModules.TaskInfo
                {
                    Url = $"{_appBaseUrl}/dialog-form",
                    Title = "Webpage Dialog",
                    Height = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(500),
                    Width = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(800)
                })));
    }

    [TaskSubmitRoute("webpage_dialog")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnWebpageDialogSubmitAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");
        var email = request.GetDataString("email", "No email provided");
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form! We got that your email is {email}", cancellationToken: cancellationToken);
        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.MessageTask("Form submitted successfully"));
    }
    #endregion

    #region Multi-Step Form
    [TaskFetchRoute("multi_step_form")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(
                new Microsoft.Teams.Api.TaskModules.TaskInfo
                {
                    Card = new Microsoft.Teams.Api.Attachment(ContentTypes.AdaptiveCard, CardLoader.LoadCardJson("multi-step-name-card.json")),
                    Title = "Multi-step Form Dialog",
                    Height = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(Microsoft.Teams.Api.TaskModules.Size.Small),
                    Width = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(Microsoft.Teams.Api.TaskModules.Size.Small)
                })));
    }

    [TaskSubmitRoute("multi_step_form_submit_name")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepSubmitNameAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");

        return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(
                new Microsoft.Teams.Api.TaskModules.TaskInfo
                {
                    Card = new Microsoft.Teams.Api.Attachment(ContentTypes.AdaptiveCard, CardLoader.LoadCardJson("multi-step-email-card.json", new Dictionary<string, string> { ["name"] = name })),
                    Title = $"Thanks {name} - Get Email",
                    Height = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(Microsoft.Teams.Api.TaskModules.Size.Small),
                    Width = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(Microsoft.Teams.Api.TaskModules.Size.Small)
                })));
    }

    [TaskSubmitRoute("multi_step_form_submit_email")]
    public async Task<Microsoft.Teams.Api.TaskModules.Response> OnMultiStepSubmitEmailAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    {
        var name = request.GetDataString("name", "Unknown");
        var email = request.GetDataString("email", "No email provided");
        await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form! We got that your email is {email}", cancellationToken: cancellationToken);
        return new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.MessageTask("Multi-step form completed successfully"));
    }
    #endregion

    #region Mixed Example with Card and Webpage
    [TaskFetchRoute("mixed_example")]
    public Task<Microsoft.Teams.Api.TaskModules.Response> OnMixedExampleFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response(
            new Microsoft.Teams.Api.TaskModules.ContinueTask(
                new Microsoft.Teams.Api.TaskModules.TaskInfo
                {
                    Url = "https://teams.microsoft.com/l/task/example-mixed",
                    Title = "Mixed Example",
                    Height = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(600),
                    Width = new Microsoft.Teams.Common.Union<int, Microsoft.Teams.Api.TaskModules.Size>(800)
                })));
    }
    #endregion
}
