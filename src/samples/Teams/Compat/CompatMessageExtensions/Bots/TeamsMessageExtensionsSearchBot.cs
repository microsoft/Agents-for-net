// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Builder;

#nullable disable

namespace CompatMessageExtensions.Bots
{
    public class TeamsMessageExtensionsSearchBot(IConfiguration config) : TeamsActivityHandler
    {
        public readonly string _baseUrl = config["BaseUrl"];

        protected override async Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
        {
            var text = string.Empty;

            if (turnContext.Activity.Value != null)
            {
                text = query?.Parameters?[0]?.Value?.ToString() ?? string.Empty;

                switch (text)
                {
                    case "adaptive card":
                        Microsoft.Teams.Api.MessageExtensions.Response response = GetAdaptiveCard();
                        return response;

                    case "connector card":
                        Microsoft.Teams.Api.MessageExtensions.Response connectorCard = GetConnectorCard();
                        return connectorCard;

                    case "result grid":
                        Microsoft.Teams.Api.MessageExtensions.Response resultGrid = GetResultGrid();
                        return resultGrid;
                }
            }

            var packages = await FindPackages(text);

            // We take every row of the results and wrap them in cards wrapped in MessagingExtensionAttachment objects.
            // The Preview is optional, if it includes a Tap, that will trigger the OnTeamsMessagingExtensionSelectItemAsync event back on this bot.
            var attachments = packages.Select(package =>
            {
                var cardValue = $"{{\"packageId\": \"{package.Id}\", \"version\": \"{package.Version}\", \"description\": \"{PackageItem.NormalizeString(package.Description)}\", \"projectUrl\": \"{package.ProjectUrl}\", \"iconUrl\": \"{package.IconUrl}\"}}";
                
                var previewCard = new ThumbnailCard { Title = package.Id, Tap = new CardAction { Type = "invoke", Value = cardValue } };
                if (!string.IsNullOrEmpty(package.IconUrl))
                {
                    previewCard.Images = [new CardImage(package.IconUrl, "Icon")];
                }

                var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
                {
                    ContentType = Microsoft.Teams.Api.ContentType.HeroCard,
                    Content = new HeroCard { Title = package.Id },
                    Preview = previewCard.ToTeamsAttachment()
                };

                return attachment;
            }).ToList();

            // The list of MessagingExtensionAttachments must we wrapped in a MessagingExtensionResult wrapped in a MessagingExtensionResponse.
            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = new Microsoft.Teams.Api.MessageExtensions.Result
                {
                    Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
                    AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.List,
                    Attachments = attachments
                }
            };
        }

        protected override Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement query, CancellationToken cancellationToken)
        {
            // The Preview card's Tap should have a Value property assigned, this will be returned to the bot in this event. 
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
                Buttons =
                    [
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "NuGet Package", Value = $"https://www.nuget.org/packages/{packageId}" },
                        new CardAction { Type = ActionTypes.OpenUrl, Title = "Project", Value = projectUrl },
                    ],
            };

            if (!string.IsNullOrEmpty(iconUrl))
            {
                card.Images = [new CardImage(iconUrl, "Icon")];
            }

            var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                ContentType = Microsoft.Teams.Api.ContentType.ThumbnailCard,
                Content = card,
            };

            return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = new Microsoft.Teams.Api.MessageExtensions.Result
                {
                    Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
                    AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.List,
                    Attachments = [attachment]
                }
            });
        }

        // Generate a set of substrings to illustrate the idea of a set of results coming back from a query. 
        private async Task<IEnumerable<PackageItem>> FindPackages(string text)
        {
            var obj = JsonObject.Parse(await (new HttpClient()).GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{text}&prerelease=true"));
            var items = ProtocolJsonSerializer.ToObject<List<JsonObject>>(obj["data"]);
            return items.Select(item => new PackageItem() 
            { 
                Id = item["id"].ToString(), 
                Version = item["version"].ToString(), 
                Description = item["description"].ToString(), 
                ProjectUrl = item["projectUrl"]?.ToString(), 
                IconUrl = item["iconUrl"]?.ToString() });
        }

        public Microsoft.Teams.Api.MessageExtensions.Response GetAdaptiveCard()
        {
            var paths = new[] { ".", "Resources", "RestaurantCard.json" };
            string filepath = Path.Combine(paths);
            var previewcard = new ThumbnailCard
            {
                Title = "Adaptive Card",
                Text = "Please select to get Adaptive card"
            };
            var adaptiveList = FetchAdaptive(filepath);
            var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                ContentType = Microsoft.Teams.Api.ContentType.AdaptiveCard,
                Content = adaptiveList.Content,
                Preview = previewcard.ToTeamsAttachment()
            };

            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = new Microsoft.Teams.Api.MessageExtensions.Result
                {
                    Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
                    AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.List,
                    Attachments = new List<Microsoft.Teams.Api.MessageExtensions.Attachment> { attachment }
                }
            };
        }
        public Microsoft.Teams.Api.MessageExtensions.Response GetConnectorCard()
        {
            var path = new[] { ".", "Resources", "connectorCard.json" };
            var filepath = Path.Combine(path);
            var previewcard = new ThumbnailCard
            {
                Title = "O365 Connector Card",
                Text = "Please select to get Connector card"
            };

            var connector = FetchConnector(filepath);
            var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                ContentType = new("application/vnd.microsoft.teams.card.o365connector"),
                Content = connector.Content,
                Preview = previewcard.ToTeamsAttachment()
            };

            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = new Microsoft.Teams.Api.MessageExtensions.Result
                {
                    Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
                    AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.List,
                    Attachments = new List<Microsoft.Teams.Api.MessageExtensions.Attachment> { attachment }
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

        public Microsoft.Teams.Api.MessageExtensions.Attachment FetchConnector(string filepath)
        {
            var connectorCardJson = File.ReadAllText(filepath);
            var connectorCardAttachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
            {
                ContentType = new("application/vnd.microsoft.teams.card.o365connector"),
                Content = connectorCardJson,

            };
            return connectorCardAttachment;
        }

        public Microsoft.Teams.Api.MessageExtensions.Response GetResultGrid()
        {
            var imageFiles = Directory.EnumerateFiles("wwwroot", "*.*", SearchOption.AllDirectories)
            .Where(s => s.EndsWith(".jpg"));

            List< Microsoft.Teams.Api.MessageExtensions.Attachment> attachments = new List<Microsoft.Teams.Api.MessageExtensions.Attachment>();

            foreach (string img in imageFiles)
            {
                var image = img.Split("\\");
                var thumbnailCard = new ThumbnailCard();
                thumbnailCard.Images = new List<CardImage>() { new CardImage(_baseUrl + "/" + image[1]) };
                var attachment = new Microsoft.Teams.Api.MessageExtensions.Attachment
                {
                    ContentType = Microsoft.Teams.Api.ContentType.ThumbnailCard,
                    Content = thumbnailCard,
                };
                attachments.Add(attachment);
            }
            return new Microsoft.Teams.Api.MessageExtensions.Response
            {
                ComposeExtension = new Microsoft.Teams.Api.MessageExtensions.Result
                {
                    Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
                    AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.Carousel,
                    Attachments = attachments
                }
            };
        }
    }

    class PackageItem
    {
        public string Id;
        public string Version;
        public string Description;
        public string ProjectUrl;
        public string IconUrl;

        public static string NormalizeString(string value)
        {
            return value
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\"", "\\\"");
        }
    }
}
