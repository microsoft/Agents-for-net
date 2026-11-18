// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Apps;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

[Experimental("ExperimentalTeamsPlan")]
internal static class TeamsPlanTransport
{
    internal static async Task<ResourceResponse> SendReplyChainAsync(
        IConnectorClient connector,
        PlanReplyChain replyChain,
        IActivity activity,
        CancellationToken cancellationToken)
    {
        if (connector is not IRestTransport transport || transport.Endpoint == null)
        {
            throw new InvalidOperationException("Plan reply-chain delivery requires the turn's service-scoped IRestTransport.");
        }

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

        var error = new ErrorResponseException(response.ReasonPhrase) { StatusCode = (int)response.StatusCode };
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
