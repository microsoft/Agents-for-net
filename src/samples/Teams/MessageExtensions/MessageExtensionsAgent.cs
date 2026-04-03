// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Teams.Cards;

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
                Text = $"This is a preview of result {i} for query '{searchQuery}'."
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

    [SubmitActionRoute("createCard")]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnCreateCardAsync(ITurnContext turnContext, ITurnState turnState, IDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var title = data.TryGetValue("title", out string? titleValue) ? titleValue : "Default Title";
        var description = data.TryGetValue("description", out string? descriptionValue) ? descriptionValue : "Default Description";

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
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnGetMessageDetailsAsync(ITurnContext turnContext, ITurnState turnState, IDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var messageText = data.TryGetValue("messageText", out string? messageTextValue) ? messageTextValue : "No message content";
        var messageId = data.TryGetValue("messageId", out string? messageIdValue) ? messageIdValue : "Unknown";

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

    [SelectItemRoute]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSelectItemAsync(ITurnContext turnContext, ITurnState turnState, string item, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Item selected: {Item}", item);

        var card = new Microsoft.Teams.Cards.AdaptiveCard([
            new TextBlock("Item Selected")
            {
                Weight = TextWeight.Bolder,
                Size = TextSize.Large,
                Color = TextColor.Good
            },
            new TextBlock("You selected the following item:")
            {
                Wrap = true
            },
            new TextBlock(item)
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

    [QueryUrlSettingRoute]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnQuerySettingsUrlAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Query settings URL requested");
        return Task.FromResult(Response.WithResultConfig("https://bot-devtunnel-url/settings"));
    }

    public Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnFetchTask(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Fetch task module requested");

        // Updated to use actual converation members

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

    public static string GetSettingsHtml()
    {
        return """
<!DOCTYPE html>
<html>
<head>
    <title>Message Extension Settings</title>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <link
       rel="stylesheet"
       href="https://stackpath.bootstrapcdn.com/bootstrap/4.5.0/css/bootstrap.min.css"
    />
    <script src="https://res.cdn.office.net/teams-js/2.22.0/js/MicrosoftTeams.min.js"></script>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 20px;
            background-color: #f5f5f5;
        }
        .container {
            background-color: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            max-width: 500px;
        }
        .form-group {
            margin-bottom: 15px;
        }
        label {
            display: block;
            margin-bottom: 5px;
            font-weight: 600;
        }
        select, input {
            width: 100%;
            padding: 8px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }
        .buttons {
            margin-top: 20px;
            text-align: right;
        }
        button {
            padding: 8px 16px;
            margin-left: 8px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
        }
        .btn-primary {
            background-color: #0078d4;
            color: white;
        }
        .btn-secondary {
            background-color: #6c757d;
            color: white;
        }
    </style>
</head>
<body>
    <div class="container">
        <h2>Message Extension Settings</h2>
        <form id="settingsForm">
            <div class="form-group">
                <label for="defaultAction">Default Action:</label>
                <select id="defaultAction" name="defaultAction">
                    <option value="search">Search</option>
                    <option value="compose">Compose</option>
                    <option value="both">Both</option>
                </select>
            </div>
            
            <div class="form-group">
                <label for="maxResults">Max Search Results:</label>
                <input type="number" id="maxResults" name="maxResults" value="10" min="1" max="50">
            </div>
            
            <div class="buttons">
                <button type="button" class="btn-secondary" onclick="cancelSettings()">Cancel</button>
                <button type="button" class="btn-primary" onclick="saveSettings()">Save</button>
            </div>
        </form>
    </div>

    <script>
        microsoftTeams.app.initialize().then(() => {
            console.log("Teams SDK initialized");
        }).catch(err => {
            console.error("Teams SDK initialization failed:", err);
        });        

        function saveSettings() {
            const formData = new FormData(document.getElementById('settingsForm'));
            const settings = {};
            for (let [key, value] of formData.entries()) {
                settings[key] = value;
            }
            
            microsoftTeams.dialog.url.submit(settings);
        }
        
        function cancelSettings() {
            microsoftTeams.dialog.url.submit();
        }
    </script>
</body>
</html>
""";
    }
}