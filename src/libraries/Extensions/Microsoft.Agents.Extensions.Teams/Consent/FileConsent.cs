// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Consent;

/// <summary>
/// Provides routing for Microsoft Teams file consent card interactions.
/// </summary>
/// <remarks>
/// <para>
/// Teams requires explicit user consent before a bot can upload a file into a conversation.
/// The consent flow is:
/// </para>
/// <list type="number">
///   <item>Register accept and decline handlers via <see cref="OnAccept"/> and <see cref="OnDecline"/>.</item>
///   <item>Send a <see cref="Microsoft.Teams.Api.FileConsentCard"/> attachment to prompt the user.</item>
///   <item>If the user accepts, the registered accept handler is called with <see cref="Microsoft.Teams.Api.FileConsentCardResponse"/> containing upload details. Perform an HTTP PUT to <see cref="Microsoft.Teams.Api.FileUploadInfo.UploadUrl"/> to complete the upload.</item>
///   <item>If the user declines, the registered decline handler is called.</item>
/// </list>
/// <example>
/// The following example demonstrates the full consent-and-upload flow using route attributes.
/// <code>
/// [TeamsExtension]
/// public partial class MyAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory) : AgentApplication(options)
/// {
///     // Send a file consent card when the user requests a file upload.
///     [MessageRoute]
///     public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
///     {
///         var consentCard = new Microsoft.Teams.Api.FileConsentCard
///         {
///             Description = "Here is the report you requested.",
///             SizeInBytes = 42000,
///             AcceptContext = new { filename = "report.txt" },
///             DeclineContext = new { filename = "report.txt" }
///         };
///
///         var attachment = new Microsoft.Agents.Core.Models.Attachment
///         {
///             ContentType = "application/vnd.microsoft.teams.card.file.consent",
///             Name = "report.txt",
///             Content = consentCard
///         };
///
///         return turnContext.SendActivityAsync(MessageFactory.Attachment(attachment), cancellationToken);
///     }
///
///     [FileConsentAcceptRoute]
///     public async Task OnFileConsentAcceptAsync(
///         ITurnContext turnContext,
///         ITurnState turnState,
///         Microsoft.Teams.Api.FileConsentCardResponse response,
///         CancellationToken cancellationToken)
///     {
///         var filePath = Path.Combine("wwwroot", "report.txt");
///         var fileInfo = new FileInfo(filePath);
///         var client = httpClientFactory.CreateClient();
///
///         using var fileStream = File.OpenRead(filePath);
///         var fileContent = new StreamContent(fileStream);
///         fileContent.Headers.ContentLength = fileInfo.Length;
///         fileContent.Headers.ContentRange =
///             new System.Net.Http.Headers.ContentRangeHeaderValue(0, fileInfo.Length - 1, fileInfo.Length);
///
///         await client.PutAsync(response.UploadInfo.UploadUrl, fileContent, cancellationToken);
///
///         await turnContext.SendActivityAsync(
///             $"File **{response.UploadInfo.Name}** uploaded successfully.",
///             cancellationToken: cancellationToken);
///     }
///
///     [FileConsentDeclineRoute]
///     public Task OnFileConsentDeclineAsync(
///         ITurnContext turnContext,
///         ITurnState turnState,
///         Microsoft.Teams.Api.FileConsentCardResponse response,
///         CancellationToken cancellationToken)
///     {
///         return turnContext.SendActivityAsync(
///             "File upload was declined.",
///             cancellationToken: cancellationToken);
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public class FileConsent
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal FileConsent(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Handles when a file consent card is accepted by the user.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FileConsentAcceptRouteAttribute"/> can be used to decorate a <see cref="FileConsentHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public FileConsent OnAccept(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(FileConsentAcceptRouteBuilder.Create()
            .WithHandler(handler)
            .WithChannelId(_channelId)
            .WithOrderRank(rank)
            .AsAgentic(isAgenticOnly)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Handles when a file consent card is declined by the user.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FileConsentDeclineRouteAttribute"/> can be used to decorate a <see cref="FileConsentHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
    /// <param name="autoSignInHandlers">List of UserAuthorization handlers to get token for.</param>
    /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
    /// <returns>The AgentExtension instance for chaining purposes.</returns>
    public FileConsent OnDecline(FileConsentHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
    {
        _app.AddRoute(FileConsentDeclineRouteBuilder.Create()
            .WithHandler(handler)
            .WithChannelId(_channelId)
            .WithOrderRank(rank)
            .AsAgentic(isAgenticOnly)
            .WithOAuthHandlers(autoSignInHandlers)
            .Build());
        return this;
    }
}