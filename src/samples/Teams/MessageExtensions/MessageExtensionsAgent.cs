// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.MessageExtensions;
using Microsoft.Teams.Cards;
using System.Text.Json;

namespace MessageExtensions;

[TeamsExtension]
public partial class MessageExtensionsAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [MessageRoute]
    public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        => turnContext.SendActivityAsync($"Echo: {turnContext.Activity.Text}\n\nThis is a message extension bot. Use the message extension commands in Teams to test functionality.", cancellationToken: cancellationToken);

    [QueryRoute("searchQuery")]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSearchQueryAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
    {
        bool initialRun = query.Parameters?.FirstOrDefault(p => p.Name == "initialRun")?.Value?.ToString() == "true";
        if (initialRun)
        {
            return Task.FromResult(Response.WithResultMessage("Enter search query"));
        }

        string? searchQuery = query.Parameters?.FirstOrDefault(p => p.Name == "searchQuery")?.Value?.ToString() ?? "";

        Logger.LogInformation("Search query received: {Query}", searchQuery);

        var attachments = new List<Microsoft.Teams.Api.MessageExtensions.Attachment>();

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

                // This Value is sent to the SelectItemRoute below
                Tap = new CardAction { Type = "invoke", Value = $"{{\"index\": \"{i}\", \"query\":\"{searchQuery}\" }}" }
            };

            var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                ContentType = Microsoft.Teams.Api.ContentType.AdaptiveCard,
                Content = card,
                Preview = new Microsoft.Teams.Api.MessageExtensions.Attachment
                {
                    ContentType = Microsoft.Teams.Api.ContentType.ThumbnailCard,
                    Content = previewCard
                }
            };

            attachments.Add(attachment);
        }

        return Task.FromResult(Response.WithResultAttachments(attachments));
    }

    [SelectItemRoute]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSelectItemAsync(ITurnContext turnContext, ITurnState turnState, JsonElement item, CancellationToken cancellationToken)
    {
        var index = item.GetProperty("index").GetString() ?? "No Index";
        var query = item.GetProperty("query").GetString() ?? "No Query";

        Logger.LogInformation("Item selected: {Item}", item);

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

        var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
        {
            ContentType = new Microsoft.Teams.Api.ContentType("application/vnd.microsoft.card.adaptive"),
            Content = card
        };

        return Task.FromResult(Response.WithResultAttachments([attachment]));
    }

    [SubmitActionRoute("createCard")]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnCreateCardAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
    {
        var title = action.GetDataString("title", "Default Title");
        var description = action.GetDataString("description", "Default Description");

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

        var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
        {
            ContentType = Microsoft.Teams.Api.ContentType.AdaptiveCard,
            Content = card
        };

        return Task.FromResult(Response.WithResultAttachment(attachment));
    }

    [SubmitActionRoute("getMessageDetails")]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnGetMessageDetailsAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
    {
        var messageText = action.GetDataString("messageText", "No message content");
        var messageId = action.GetDataString("messageId", "Unknown");

        Logger.LogInformation("Getting details for Message ID: {MessageId} with Text: {MessageText}", messageId, messageText);

        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new TextBlock("Message Details")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Large,
                Color = TextColor.Accent
            },
            new TextBlock($"Message ID: {messageId}")
            {
                Wrap = true
            },
            new TextBlock($"Content: {messageText}")
            {
                Wrap = true
            }
        ])
        {
            Schema = "http://adaptivecards.io/schemas/adaptive-card.json"
        };

        var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
        {
            ContentType = new Microsoft.Teams.Api.ContentType("application/vnd.microsoft.card.adaptive"),
            Content = card
        };

        return Task.FromResult(Response.WithResultAttachment(attachment));
    }

    [QueryLinkRoute]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQueryLinkAsync(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Link query received: {Url}", url);
        if (string.IsNullOrEmpty(url))
        {
            return Task.FromResult(Response.WithResultMessage("No URL provided"));
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

        var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
        {
            ContentType = new Microsoft.Teams.Api.ContentType("application/vnd.microsoft.card.adaptive"),
            Content = card,
            Preview = new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                ContentType = Microsoft.Teams.Api.ContentType.ThumbnailCard,
                Content = new ThumbnailCard
                {
                    Title = "Link Preview",
                    Text = url
                }
            }
        };

        return Task.FromResult(Response.WithResultAttachments([attachment]));
    }

    [QueryUrlSettingRoute]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuerySettingsUrlAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Query settings URL requested");
        return Task.FromResult(Response.WithResultConfig("https://bot-devtunnel-url/settings"));
    }

    public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetchAction(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
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

        return Task.FromResult(Response.WithTaskCard(
            new Microsoft.Teams.Api.Attachment(card),
            "Fetch Task Dialog",
            Microsoft.Teams.Api.TaskModules.Size.Small,
            Microsoft.Teams.Api.TaskModules.Size.Small));
    }

    [ConfigureSettingsRoute]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnMessageExtensionSettingAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query settings, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Message extension settings submitted with state: {State}", settings.State);

        if (settings.State == "CancelledByUser")
        {
            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
        }

        // Process settings data

        return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response());
    }
}