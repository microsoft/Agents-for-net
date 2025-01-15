﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.BotBuilder.Teams;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Teams.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Interfaces;

namespace MessagingExtensionsSearch.Bots
{
    public class TeamsMessagingExtensionsSearchBot : TeamsActivityHandler
    {
        public readonly string _baseUrl;
        public TeamsMessagingExtensionsSearchBot(IConfiguration config)
        {
            _baseUrl = config["BaseUrl"];

        }

        protected override async Task<MessagingExtensionResponse> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, MessagingExtensionQuery query, CancellationToken cancellationToken)
        {
            var text = string.Empty;

            if (turnContext.Activity.Value != null)
            {
                text = query?.Parameters?[0]?.Value as string ?? string.Empty;

                switch (text)
                {
                    case "adaptive card":
                        MessagingExtensionResponse response = GetAdaptiveCard();
                        return response;

                    case "connector card":
                        MessagingExtensionResponse connectorCard = GetConnectorCard();
                        return connectorCard;

                    case "result grid":
                        MessagingExtensionResponse resultGrid = GetResultGrid();
                        return resultGrid;
                }
            }

            var packages = await FindPackages(text);

            // We take every row of the results and wrap them in cards wrapped in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.
            var attachments = packages.Select(package =>
            {
                var cardValue = $"{{\"packageId\": \"{package.Item1}\", \"version\": \"{package.Item2}\", \"description\": \"{package.Item3}\", \"projectUrl\": \"{package.Item4}\", \"iconUrl\": \"{package.Item5}\"}}";
                var previewCard = new ThumbnailCard { Title = package.Item1, Tap = new CardAction { Type = "invoke", Value = cardValue } };
                if (!string.IsNullOrEmpty(package.Item5))
                {
                    previewCard.Images = new List<CardImage>() { new CardImage(package.Item5, "Icon") };
                }

                var attachment = new MessagingExtensionAttachment
                {
                    ContentType = HeroCard.ContentType,
                    Content = new HeroCard { Title = package.Item1 },
                    Preview = previewCard.ToAttachment()
                };

                return attachment;
            }).ToList();

            // The list of MessagingExtensionAttachments must we wrapped in a MessagingExtensionResult wrapped in a MessagingExtensionResponse.
            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = attachments
                }
            };
        }

        protected override Task<MessagingExtensionResponse> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement query, CancellationToken cancellationToken)
        {
            // The Preview card's Tap should have a Value property assigned, this will be returned to the bot in this event. 
            // var (packageId, version, description, projectUrl, iconUrl) =  query.ToObject<(string, string, string, string, string)>();
            string packageId = query.GetProperty("packageId").GetString();
            string version = query.GetProperty("version").GetString();
            string description = query.GetProperty("description").GetString();
            string projectUrl = query.GetProperty("projectUrl").GetString();
            string iconUrl = query.GetProperty("iconUrl").GetString();

            // We take every row of the results and wrap them in cards wrapped in in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.

            var card = new ThumbnailCard
            {
                Title = $"{packageId}, {version}",
                Subtitle = description,
                Buttons = new List<CardAction>
                    {
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "Nuget Package", Value = $"https://www.nuget.org/packages/{packageId}" },
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "Project", Value = projectUrl },
                    },
            };

            if (!string.IsNullOrEmpty(iconUrl))
            {
                card.Images = new List<CardImage>() { new CardImage(iconUrl, "Icon") };
            }

            var attachment = new MessagingExtensionAttachment
            {
                ContentType = ThumbnailCard.ContentType,
                Content = card,
            };

            return Task.FromResult(new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            });
        }

        // Generate a set of substrings to illustrate the idea of a set of results coming back from a query. 
        private async Task<IEnumerable<(string, string, string, string, string)>> FindPackages(string text)
        {
            var obj = JsonObject.Parse(await (new HttpClient()).GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{text}&prerelease=true"));
            var items = ProtocolJsonSerializer.ToObject<List<JsonObject>>(obj["data"]);
            return items.Select(item => (item["id"].ToString(), item["version"].ToString(), item["description"].ToString(), item["projectUrl"]?.ToString(), item["iconUrl"]?.ToString()));
        }

        public MessagingExtensionResponse GetAdaptiveCard()
        {
            var paths = new[] { ".", "Resources", "RestaurantCard.json" };
            string filepath = Path.Combine(paths);
            var previewcard = new ThumbnailCard
            {
                Title = "Adaptive Card",
                Text = "Please select to get Adaptive card"
            };
            var adaptiveList = FetchAdaptive(filepath);
            var attachment = new MessagingExtensionAttachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = adaptiveList.Content,
                Preview = previewcard.ToAttachment()
            };

            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            };
        }
        public MessagingExtensionResponse GetConnectorCard()
        {
            var path = new[] { ".", "Resources", "connectorCard.json" };
            var filepath = Path.Combine(path);
            var previewcard = new ThumbnailCard
            {
                Title = "O365 Connector Card",
                Text = "Please select to get Connector card"
            };

            var connector = FetchConnector(filepath);
            var attachment = new MessagingExtensionAttachment
            {
                ContentType = O365ConnectorCard.ContentType,
                Content = connector.Content,
                Preview = previewcard.ToAttachment()
            };

            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "list",
                    Attachments = new List<MessagingExtensionAttachment> { attachment }
                }
            };
        }

        public Attachment FetchAdaptive(string filepath)
        {
            var adaptiveCardJson = File.ReadAllText(filepath);
            var adaptiveCardAttachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = adaptiveCardJson
            };
            return adaptiveCardAttachment;
        }

        public Attachment FetchConnector(string filepath)
        {
            var connectorCardJson = File.ReadAllText(filepath);
            var connectorCardAttachment = new MessagingExtensionAttachment
            {
                ContentType = O365ConnectorCard.ContentType,
                Content = connectorCardJson,

            };
            return connectorCardAttachment;
        }

        public MessagingExtensionResponse GetResultGrid()
        {
            var imageFiles = Directory.EnumerateFiles("wwwroot", "*.*", SearchOption.AllDirectories)
            .Where(s => s.EndsWith(".jpg"));

            List<MessagingExtensionAttachment> attachments = new List<MessagingExtensionAttachment>();

            foreach (string img in imageFiles)
            {
                var image = img.Split("\\");
                var thumbnailCard = new ThumbnailCard();
                thumbnailCard.Images = new List<CardImage>() { new CardImage(_baseUrl + "/" + image[1]) };
                var attachment = new MessagingExtensionAttachment
                {
                    ContentType = ThumbnailCard.ContentType,
                    Content = thumbnailCard,
                };
                attachments.Add(attachment);
            }
            return new MessagingExtensionResponse
            {
                ComposeExtension = new MessagingExtensionResult
                {
                    Type = "result",
                    AttachmentLayout = "grid",
                    Attachments = attachments
                }
            };
        }
    }
}
