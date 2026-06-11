// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Compat;

/// <summary>
/// Middleware that configures the Teams API client for each turn in the Adapter pipeline.
/// </summary>
/// <remarks>
/// For <see cref="TeamsActivityHandler"/> based agents <b>ONLY</b>: This middleware MUST be added to the Adapter 
/// middleware pipeline to ensure that the Teams API client is properly set up for each incoming activity. 
/// It relies on the provided IConnections and IHttpClientFactory instances to manage connections and HTTP 
/// client creation. This middleware does not modify the activity or context, but prepares the necessary 
/// services for downstream components.<br/><br/>
/// In Program.cs, add the middleware to the Adapter configuration:
/// <code>
/// builder.Services.AddSingleton&lt;Microsoft.Agents.Builder.IMiddleware[]>(sp => 
///     [new Microsoft.Agents.Extensions.Teams.Compat.SetTeamsApiClientMiddleware(sp.GetService&lt;IConnections>(), sp.GetService&lt;IHttpClientFactory>())]);
/// </code>
/// </remarks>
public class SetTeamsApiClientMiddleware : IMiddleware
{
    private readonly IConnections _connections;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the SetTeamsApiClientMiddleware class with the specified connections and HTTP
    /// client factory.
    /// </summary>
    /// <param name="connections">An object that manages active connections used by the middleware. Cannot be null.</param>
    /// <param name="httpClientFactory">A factory used to create HTTP client instances for making outbound requests. Cannot be null.</param>
    public SetTeamsApiClientMiddleware(IConnections connections, IHttpClientFactory httpClientFactory)
    {
        AssertionHelpers.ThrowIfNull(connections, nameof(connections));
        AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

        _connections = connections;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Handles an incoming turn by configuring the Teams API client and invoking the next middleware or handler in
    /// the pipeline.
    /// </summary>
    /// <param name="turnContext">The context for the current conversation turn.</param>
    /// <param name="next">A delegate representing the next middleware or handler to be executed in the pipeline. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
    {
        if (turnContext.Activity.ChannelId == Channels.Msteams)
        {
            // Set the TeamsApiClient in the turn context for use in ActivityHandler methods.
            turnContext.SetTeamsApiClient(_connections, _httpClientFactory, cancellationToken);

            // Explicit conversation of Activity.ChannelData to Teams' ChannelData for improved performance
            turnContext.Activity.ChannelData = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.ChannelData>(turnContext.Activity.ChannelData);
        }
        await next(cancellationToken).ConfigureAwait(false);
    }
}
