// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.App;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageExtensions;

public class TeamsMessageExtensionAgent : AgentApplication
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamsMessageExtensionAgent> _logger;
    public TeamsMessageExtensionAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory, ILogger<TeamsMessageExtensionAgent> logger) : base(options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        RegisterExtension(new TeamsAgentExtension(this), tae =>
        {
            tae.MessageExtensions.OnQuery("findNuGetPackage", OnQueryAsync);
            tae.MessageExtensions.OnSelectItem(OnSelectItemAsync);
            tae.MessageExtensions.OnQueryLink(OnQueryLinkAsync);
        });

        AddRoute(MessageRouteBuilder.Create().WithChannelId(Channels.Msteams).WithText("-name").WithHandler(MyNameAsync).Build());

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private Task<Microsoft.Teams.Api.MessageExtensions.Result> OnQueryLinkAsync(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
    {
        return Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Result() { Text = "On Query Link" });
    }

    private async Task<Microsoft.Teams.Api.MessageExtensions.Result> OnSelectItemAsync(ITurnContext turnContext, ITurnState turnState, object item, CancellationToken cancellationToken)
    {
        PackageItem? package = JsonSerializer.Deserialize<PackageItem>((JsonElement)item);
        if (package is null)
        {
            await turnContext.SendActivityAsync("selected item is not a packageItem", cancellationToken: cancellationToken);
            _logger.LogWarning("Selected Item cannot be deserialized as a PackageItem");
            return null!;
        }

        ThumbnailCard card = new()
        {
            Title = $"{package.PackageId}, {package.Version}",
            Subtitle = package.Description,
            Buttons =
                [
                    new() { Type = ActionTypes.OpenUrl, Title = "Nuget Package", Value = $"https://www.nuget.org/packages/{package.PackageId}" },
                    new() { Type = ActionTypes.OpenUrl, Title = "Project", Value = package.ProjectUrl},
                ],
        };

        if (!string.IsNullOrEmpty(package.IconUrl))
        {
            card.Images = [new(package.IconUrl, "Icon")];
        }

        Microsoft.Teams.Api.MessageExtensions.Attachment attachment = new()
        {
            ContentType = Microsoft.Teams.Api.ContentType.ThumbnailCard,
            Content = card,
        };

        return await Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Result
        {
            Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Message,
            AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.List,
            Attachments = [attachment]
        });
    }

    private async Task<Microsoft.Teams.Api.MessageExtensions.Result> OnQueryAsync(ITurnContext turnContext, ITurnState turnState, Query<IDictionary<string, object>> query, CancellationToken cancellationToken)
    {
        string? text = string.Empty;
        if (query.Parameters.TryGetValue("NuGetPackageName", out var elObj))
        {
            text = elObj.ToString();
        }

        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Query Parameters does not include NuGetPackageName");
            return await Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Result());
        }

        var packages = await FindPackages(text);
        List<Microsoft.Teams.Api.MessageExtensions.Attachment> attachments = [.. packages.Select(package =>
        {
            ThumbnailCard previewCard = new() { Title = package.PackageId, Tap = new CardAction("invoke") { Value = package } };
            if (!string.IsNullOrEmpty(package.IconUrl))
            {
                previewCard.Images = [new CardImage { Url = package.IconUrl, Alt = "Icon" }];
            }

            return new Microsoft.Teams.Api.MessageExtensions.Attachment()
            {
                ContentType = Microsoft.Teams.Api.ContentType.HeroCard,
                Content = new HeroCard { Title = package.Id },
                Preview = previewCard.ToTeamsAttachment()
            };
        })];

        return new Microsoft.Teams.Api.MessageExtensions.Result
        {
            Type = Microsoft.Teams.Api.MessageExtensions.ResultType.Result,
            AttachmentLayout = Microsoft.Teams.Api.Attachment.Layout.List,
            Attachments = attachments
        };
    }

    private async Task<IEnumerable<PackageItem>> FindPackages(string text)
    {
        HttpClient httpClient = _httpClientFactory.CreateClient();
        string jsonResult = await httpClient.GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{text}&prerelease=true");
        JsonElement data = JsonDocument.Parse(jsonResult).RootElement.GetProperty("data");
        PackageItem[]? packages = data.Deserialize<PackageItem[]>();
        return packages!;
    }

    private async Task MyNameAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var teamsApiClient = turnContext.GetTeamsApiClient();
        var member = await teamsApiClient.Conversations.Members.GetByIdAsync(turnContext.Activity.Conversation.Id, turnContext.Activity.From.Id);
        string name = member.Name ?? "No idea";
        await turnContext.SendActivityAsync($"Your name is: {name}", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
    }
}

class PackageItem
{
    [JsonPropertyName("@id")]
    public string? Id { get; set; }

    [JsonPropertyName("id")]
    public string? PackageId { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("projectUrl")]
    public string? ProjectUrl { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }
    public static string NormalizeString(string value)
    {
        return value
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", "\\\"");
    }
}
