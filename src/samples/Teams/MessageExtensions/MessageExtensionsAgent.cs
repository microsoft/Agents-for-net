// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.MessageExtensions;
using Microsoft.Teams.Apps.MessageExtensions;
using Microsoft.Teams.Apps.Schema;
using Microsoft.Teams.Apps.TaskModules;
using Microsoft.Teams.Cards;
using System.Text.Json;

namespace MessageExtensions;

[TeamsExtension]
public partial class MessageExtensionsAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [TeamsMessageRoute]
    public Task OnMessageAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        => turnContext.SendActivityAsync($"Echo: {turnContext.Activity.Text}\n\nThis is a message extension bot. Use the message extension commands in Teams to test functionality.", cancellationToken: cancellationToken);

    [TeamsQueryRoute("searchQuery")]
    public Task<MessageExtensionResponse> OnSearchQueryAsync(ITeamsTurnContext turnContext, ITurnState turnState, MessageExtensionQuery query, CancellationToken cancellationToken)
    {
        bool initialRun = query.Parameters?.FirstOrDefault(p => p.Name == "initialRun")?.Value?.ToString() == "true";
        if (initialRun)
        {
            return Task.FromResult(CreateMessageResponse("Enter search query"));
        }

        string? searchQuery = query.Parameters?.FirstOrDefault(p => p.Name == "searchQuery")?.Value?.ToString() ?? "";

        Logger.LogInformation("Search query received: {Query}", searchQuery);

        var attachments = new List<TeamsAttachment>();

        // Create simple search results
        for (int i = 1; i <= 5; i++)
        {
            var card = new Microsoft.Teams.Cards.AdaptiveCard([
                new TextBlock($"Search Result {i}")
                {
                    Weight = TextWeight.Bolder,
                    Size = TextSize.Large
                },
                new TextBlock($"Query: '{searchQuery}' - Result description for item {i}")
                {
                    Wrap = true,
                    IsSubtle = true
                }
            ]);

            var previewCard = new ThumbnailCard()
            {
                Title = $"Result {i}",
                Text = $"This is a preview of result {i} for query '{searchQuery}'.",

                // This Value is sent to the TeamsSelectItemRoute below
                Tap = new CardAction { Type = "invoke", Value = $"{{\"index\": \"{i}\", \"query\":\"{searchQuery}\" }}" }
            };

            attachments.Add(CreateAdaptiveCardAttachment(card, previewCard));
        }

        return Task.FromResult(CreateResultResponse([.. attachments]));
    }

    [TeamsSelectItemRoute]
    public Task<MessageExtensionResponse> OnSelectItemAsync(ITeamsTurnContext turnContext, ITurnState turnState, Dictionary<string,string> items, CancellationToken cancellationToken)
    {
        var index = items.TryGetValue("index", out string? value) ? value : "No Index";
        var query = items.TryGetValue("query", out string? value1) ? value1 : "No Query";

        Logger.LogInformation("Item selected: {Item}:{Query}", index, query);

        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new TextBlock("Item Selected")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Large,
                Color = TextColor.Good
            },
            new TextBlock($"You selected item: {index} for query: '{query}'")
            {
                Wrap = true,
                FontType = FontType.Monospace,
                Separator = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json"
        };

        return Task.FromResult(CreateResultResponse(CreateAdaptiveCardAttachment(card)));
    }

    [TeamsSubmitActionRoute("createCard")]
    public Task<MessageExtensionResponse> OnCreateCardAsync(ITeamsTurnContext turnContext, ITurnState turnState, MessageExtensionAction action, CancellationToken cancellationToken)
    {
        var data = action.GetDataAs<JsonElement>();
        var title = data.GetDataString("title", "Default Title");
        var description = data.GetDataString("description", "Default Description");

        Logger.LogInformation("Creating card with Title: {Title} and Description: {Description}", title, description);

        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new TextBlock("Custom Card Created")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Large,
                Color = TextColor.Good
            },
            new TextBlock(title)
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Medium
            },
            new TextBlock(description)
            {
                Wrap = true,
                IsSubtle = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json"
        };

        return Task.FromResult(CreateResultResponse(CreateAdaptiveCardAttachment(card)));
    }

    [TeamsQueryLinkRoute]
    public Task<MessageExtensionResponse> OnQueryLinkAsync(ITeamsTurnContext turnContext, ITurnState turnState, MessageExtensionQueryLink? query, CancellationToken cancellationToken)
    {
        var url = query?.Url;
        Logger.LogInformation("Link query received: {Url}", url);
        if (url is null)
        {
            return Task.FromResult(CreateMessageResponse("No URL provided"));
        }

        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new TextBlock("Link Preview")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Medium
            },
            new TextBlock($"URL: {url}")
            {
                IsSubtle = true,
                Wrap = true
            },
            new TextBlock("This is a preview of the linked content generated by the message extension.")
            {
                Wrap = true,
                Size = TextSize.Small
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json"
        };

        var previewCard = new ThumbnailCard
        {
            Title = "Link Preview",
            Text = url.ToString()
        };

        return Task.FromResult(CreateResultResponse(CreateAdaptiveCardAttachment(card, previewCard)));
    }

    [TeamsQuerySettingUrlRoute]
    public Task<MessageExtensionResponse> OnQuerySettingsUrlAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Query settings URL requested");
        return Task.FromResult(
            MessageExtensionResponse.CreateBuilder()
                .WithType(MessageExtensionResponseTypes.Config)
                .WithSuggestedActions(new Microsoft.Teams.Apps.Schema.SuggestedActions
                {
                    Actions =
                    [
                        new SuggestedAction(
                            ActionType.OpenUrl,
                            "Configure",
                            "https://bot-devtunnel-url/settings")
                    ]
                })
                .Build()
                .Body
                ?? throw new InvalidOperationException("The message extension action response builder returned no body."));
    }

    [TeamsFetchActionRoute]
    public Task<MessageExtensionActionResponse> OnFetchAction(ITeamsTurnContext turnContext, ITurnState turnState, MessageExtensionAction action, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Fetch MessageExtensions.Action requested");

        // Updated to use actual conversation members

        // Create an adaptive card for the task module
        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new TextBlock("Conversation Members is not implemented in C# yet :(")
            {
                Weight = TextWeight.Bolder,
                Color = TextColor.Accent
            }
        ]);

        return Task.FromResult(
            MessageExtensionActionResponse.CreateBuilder()
                .WithTask(TaskModuleResponse.CreateBuilder()
                    .WithType(TaskModuleResponseTypes.Continue)
                    .WithTitle("Fetch Task Dialog")
                    .WithHeight(TaskModuleSizes.Small)
                    .WithWidth(TaskModuleSizes.Small)
                    .WithCard(CreateAdaptiveCardAttachment(card)))
                .Build()
                .Body
                ?? throw new InvalidOperationException("The message extension action response builder returned no body."));
    }

    [TeamsSettingRoute]
    public Task<MessageExtensionResponse> OnMessageExtensionSettingAsync(ITeamsTurnContext turnContext, ITurnState turnState, MessageExtensionQuery settings, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Message extension settings submitted with state: {State}", settings.State);

        if (settings.State == "CancelledByUser")
        {
            return Task.FromResult(new MessageExtensionResponse());
        }

        // Process settings data

        return Task.FromResult(new MessageExtensionResponse());
    }

    private static MessageExtensionResponse CreateMessageResponse(string text)
    {
        return MessageExtensionResponse.CreateBuilder()
            .WithType(MessageExtensionResponseTypes.Message)
            .WithText(text)
            .Build()
            .Body
            ?? throw new InvalidOperationException("The message extension response builder returned no body.");
    }

    private static MessageExtensionResponse CreateResultResponse(params TeamsAttachment[] attachments)
    {
        return MessageExtensionResponse.CreateBuilder()
            .WithType(MessageExtensionResponseTypes.Result)
            .WithAttachmentLayout(AttachmentLayoutType.List)
            .WithAttachments(attachments)
            .Build()
            .Body
            ?? throw new InvalidOperationException("The message extension response builder returned no body.");
    }

    private static TeamsAttachment CreateAdaptiveCardAttachment(AdaptiveCard card, ThumbnailCard? preview = null)
    {
        var attachment = new TeamsAttachment
        {
            ContentType = AttachmentContentTypes.AdaptiveCard,
            Content = JsonSerializer.SerializeToElement(card)
        };

        if (preview is not null)
        {
            attachment.Properties = new()
            {
                ["preview"] = new TeamsAttachment
                {
                    ContentType = AttachmentContentTypes.ThumbnailCard,
                    Content = JsonSerializer.SerializeToElement(preview)
                }
            };
        }

        return attachment;
    }
}
