// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.HeaderPropagation;
using System.Threading;

namespace Microsoft.Agents.Extensions.Teams;

/// <summary>
/// Provides extension methods for configuring the Teams <see cref="ApiClient"/> within an agents's turn context.
/// </summary>
/// <remarks>
/// These extension methods enable integration with the Teams API by associating an ApiClient
/// instance with the <see cref="ITurnContext"/>. This allows agent developers to access Teams-specific functionality during a
/// conversation turn. The methods support both direct configuration and configuration via an AgentApplication
/// instance.<br/><br/>
/// This creates HttpClients named "TeamsHttpClientFactory".
/// </remarks>
public static class TeamsApiClientExtensions
{
    /// <summary>
    /// Configures the Teams API client for the specified turn context using the provided agent application
    /// settings.
    /// </summary>
    /// <remarks>This extension method initializes the Teams API client for the given context based on
    /// the application's connection and HTTP client factory settings. Ensure that the application parameter is not
    /// null to avoid configuration errors.</remarks>
    /// <param name="context">The turn context in which to set up the Teams API client.</param>
    /// <param name="application">The agent application containing configuration options for the Teams API client. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    public static void SetTeamsApiClient(this ITurnContext context, AgentApplication application, CancellationToken ct = default)
    {
        SetTeamsApiClient(context, application?.Options?.Connections, application?.Options?.HttpClientFactory, ct);
    }

    /// <summary>
    /// Registers an ApiClient instance for Microsoft Teams API access in the current turn context.
    /// </summary>
    /// <remarks>After calling this method, the registered ApiClient can be retrieved from the
    /// context's service collection for use in subsequent Teams API operations. If the context identity allows
    /// anonymous access, the client will be configured without authentication; otherwise, it will use a token
    /// provider for authenticated requests.</remarks>
    /// <param name="context">The turn context in which to register the Teams ApiClient. Cannot be null.</param>
    /// <param name="connections">The connections provider used to obtain authentication tokens for Teams API requests. Cannot be null.</param>
    /// <param name="httpClientFactory">The factory used to create HTTP clients for communicating with the Teams API. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the registration operation. Optional.</param>
    public static void SetTeamsApiClient(this ITurnContext context, IConnections connections, System.Net.Http.IHttpClientFactory httpClientFactory, CancellationToken ct = default)
    {
        AssertionHelpers.ThrowIfNull(connections, nameof(connections));
        AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

        bool useAnonymous = AgentClaims.AllowAnonymous(context.Identity);
        Microsoft.Teams.Common.Http.IHttpClientOptions.HttpTokenFactory tokenFactory = useAnonymous ? null : () =>
        {
            var tokenAccess = connections.GetTokenProvider(context.Identity, context.Activity.ServiceUrl);
            return tokenAccess.GetAccessTokenAsync(AuthenticationConstants.BotFrameworkAudience, [AuthenticationConstants.BotFrameworkDefaultScope]).ConfigureAwait(false).GetAwaiter().GetResult();
        };

        var client = new Microsoft.Teams.Api.Clients.ApiClient(
            context.Activity.ServiceUrl,
            new TeamsHttpClientFactory(httpClientFactory, tokenFactory),
            ct);

        context.Services.Set<Microsoft.Teams.Api.Clients.ApiClient>(client);
    }

    public static Microsoft.Teams.Api.Clients.ApiClient GetTeamsApiClient(this ITurnContext context)
    {
        return context.Services.Get<Microsoft.Teams.Api.Clients.ApiClient>();
    }
}

class TeamsHttpClientFactory(System.Net.Http.IHttpClientFactory inner, Microsoft.Teams.Common.Http.IHttpClientOptions.HttpTokenFactory tokenFactory) : Microsoft.Teams.Common.Http.IHttpClientFactory
{
    public Microsoft.Teams.Common.Http.IHttpClient CreateClient()
    {
        var httpClient = inner.CreateClient(nameof(TeamsHttpClientFactory));
        httpClient.AddDefaultUserAgent();
        httpClient.AddHeaderPropagation();
        return new Microsoft.Teams.Common.Http.HttpClient(httpClient) { Options = { TokenFactory = tokenFactory } };
    }

    Microsoft.Teams.Common.Http.IHttpClient Microsoft.Teams.Common.Http.IHttpClientFactory.CreateClient(string name)
    {
        var httpClient = inner.CreateClient(nameof(TeamsHttpClientFactory));
        httpClient.AddDefaultUserAgent();
        httpClient.AddHeaderPropagation();
        return new Microsoft.Teams.Common.Http.HttpClient(httpClient) { Options = { TokenFactory = tokenFactory } };
    }
}
