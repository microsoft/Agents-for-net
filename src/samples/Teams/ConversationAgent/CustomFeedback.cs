// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Extensions.Logging;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Apps.TaskModules;
using Microsoft.Teams.Cards;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ConversationAgent;

public partial class TeamsConversationAgent
{
    [TeamsMessageRoute("customfeedback")]
    public static async Task SendCustomFeedbackMessageAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var activity = Activity.CreateMessageActivity();
        activity.Text = "Select thumbs up or thumbs down to open the custom feedback dialog.";
        activity.TeamsEnableFeedbackLoop("custom");

        await turnContext.SendActivityAsync(activity, cancellationToken);
    }

    [InvokeRoute("message/fetchTask")]
    public static async Task OnCustomFeedbackFetchAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var request = ProtocolJsonSerializer.ToObject<FeedbackData>(turnContext.Activity.Value);
        var wrappedRequest = ProtocolJsonSerializer.ToObject<MessageFetchTaskInvokeValue>(turnContext.Activity.Value);
        var reaction = request?.ActionValue?.Reaction ?? wrappedRequest?.Data?.ActionValue?.Reaction;

        var response = TaskModuleResponse.CreateBuilder()
            .WithType(TaskModuleResponseTypes.Continue)
            .WithTitle("Feedback")
            .WithHeight(TaskModuleSizes.Small)
            .WithWidth(TaskModuleSizes.Small)
            .WithCard(CreateFeedbackCard(reaction))
            .Build()
            .Body
            ?? throw new InvalidOperationException("The feedback dialog builder returned no body.");

        await turnContext.SendActivityAsync(Activity.CreateInvokeResponseActivity(response), cancellationToken);
    }

    [TeamsFeedbackLoopRoute]
    public Task OnCustomFeedbackSubmitAsync(ITeamsTurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken)
    {
        var feedbackText = ReadFeedbackText(feedbackData.ActionValue?.Feedback);
        _logger.LogInformation(
            "Feedback received for activity {ReplyToId}: reaction={Reaction}, feedbackLength={FeedbackLength}",
            feedbackData.ReplyToId,
            feedbackData.ActionValue?.Reaction,
            feedbackText?.Length ?? 0);

        return Task.CompletedTask;
    }

    private static TeamsAttachment CreateFeedbackCard(string? reaction)
    {
        var prompt = reaction is null
            ? "Tell us more about your experience."
            : $"You selected {reaction}. Tell us more about your experience.";

        var card = new AdaptiveCard([
            new TextBlock(prompt).WithWrap(true),
            new TextInput()
                .WithId("feedbackText")
                .WithPlaceholder("Enter your feedback here...")
                .WithIsMultiline(true)
        ]).WithActions(new SubmitAction().WithTitle("Submit"));

        return TeamsAttachment.CreateBuilder()
            .WithAdaptiveCard(card)
            .Build();
    }

    private static string? ReadFeedbackText(string? feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(feedback);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("feedbackText", out var value)
                ? value.ToString()
                : feedback;
        }
        catch (JsonException)
        {
            return feedback;
        }
    }
}
