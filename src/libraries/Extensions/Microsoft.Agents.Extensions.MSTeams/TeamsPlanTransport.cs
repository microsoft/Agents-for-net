// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Apps;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ErrorHelper = Microsoft.Agents.Extensions.MSTeams.Errors.ErrorHelper;

namespace Microsoft.Agents.Extensions.MSTeams;

[Experimental("ExperimentalTeamsPlan")]
internal static class TeamsPlanTransport
{
    /// <summary>
    /// Posts a fresh Plan completion into the saved Teams chat reply chain.
    /// </summary>
    /// <remarks>
    /// Used by the Teams-owned outgoing send hook, not as a replacement for ordinary adapter delivery.
    /// Hosted responses and established streams remain on the adapter path.
    /// <see cref="Microsoft.Agents.Connector.IConversations"/> has no reply-chain operation, so this
    /// method uses the current turn's <see cref="Microsoft.Agents.Connector.IRestTransport"/> for its
    /// service-scoped endpoint and configured HTTP client.
    /// </remarks>
    internal static async Task<ResourceResponse> SendReplyChainAsync(
        ITurnContext turnContext,
        PlanReplyChain replyChain,
        IActivity activity,
        CancellationToken cancellationToken)
    {
        var connector = turnContext.Services.Get<IConnectorClient>()
            ?? throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PlanConnectorClientMissing, null);
        if (connector is not IRestTransport transport || transport.Endpoint == null)
        {
            throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PlanReplyChainTransportRequired, null);
        }

        // Saved chat/root IDs select the thread; the live turn supplies the service URL and credentials.
        var uri = new Uri(
            $"{transport.Endpoint.AbsoluteUri.TrimEnd('/')}/v3/conversations/{Uri.EscapeDataString(replyChain.ChatOrChannelId)}/replyChains/{Uri.EscapeDataString(replyChain.ReplyChainId)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(ProtocolJsonSerializer.ToJson(activity), Encoding.UTF8, "application/json")
        };
        using var client = await transport.GetHttpClientAsync().ConfigureAwait(false);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted)
        {
            return string.IsNullOrEmpty(content)
                ? new ResourceResponse { Id = string.Empty }
                : ProtocolJsonSerializer.ToObject<ResourceResponse>(content);
        }

        // Keep the channel status and error code for conflict/timeout handling; retry decisions belong to the caller.
        var error = ErrorResponseException.CreateErrorResponseException(
            ErrorHelper.PlanReplyChainRequestFailed, null,
            ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), response.ReasonPhrase ?? string.Empty);
        error.StatusCode = (int)response.StatusCode;
        try
        {
            if (!string.IsNullOrEmpty(content))
            {
                error.Body = ProtocolJsonSerializer.ToObject<ErrorResponse>(content);
            }
        }
        catch (JsonException)
        {
            // An unrecognized error body must not hide the HTTP failure.
        }

        throw error;
    }
}
