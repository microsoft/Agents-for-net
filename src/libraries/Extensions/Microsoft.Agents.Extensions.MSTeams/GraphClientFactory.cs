// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Shared factory used by <see cref="TeamsAgentExtension"/> and <see cref="TeamsTurnContext"/> to build
/// <see cref="GraphServiceClient"/> instances, keeping the token-provider and scope-derivation logic in a single place.
/// </summary>
internal static class GraphClientFactory
{
    /// <summary>
    /// Creates a <see cref="GraphServiceClient"/> that acquires a delegated (user) token via
    /// <see cref="UserAuthorization"/> for the current turn.
    /// </summary>
    internal static GraphServiceClient CreateUserGraphClient(UserAuthorization userAuthorization, ITurnContext turnContext, string handlerName, string graphBaseUrl)
    {
        AssertionHelpers.ThrowIfNull(userAuthorization, nameof(userAuthorization));
        AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
        AssertionHelpers.ThrowIfNullOrEmpty(graphBaseUrl, nameof(graphBaseUrl));

        return new GraphServiceClient(new UserTokenProvider(userAuthorization, turnContext, handlerName), graphBaseUrl);
    }

    /// <summary>
    /// Creates a <see cref="GraphServiceClient"/> that acquires an app-only (application) token from the supplied
    /// token connection.
    /// </summary>
    internal static GraphServiceClient CreateAppGraphClient(Microsoft.Agents.Authentication.IAccessTokenProvider tokenProvider, string graphBaseUrl)
    {
        AssertionHelpers.ThrowIfNull(tokenProvider, nameof(tokenProvider));
        AssertionHelpers.ThrowIfNullOrEmpty(graphBaseUrl, nameof(graphBaseUrl));

        // Derive the resource and the app-only ".default" scope from the Graph base URL so national
        // clouds (e.g. graph.microsoft.us) are honored.
        var graphUri = new Uri(graphBaseUrl);
        var resourceUrl = $"{graphUri.Scheme}://{graphUri.Host}";
        var scopes = new List<string> { $"{resourceUrl}/.default" };

        return new GraphServiceClient(new AppTokenProvider(tokenProvider, resourceUrl, scopes), graphBaseUrl);
    }
}

class UserTokenProvider(UserAuthorization userAuthorization, ITurnContext turnContext, string handlerName) : IAuthenticationProvider
{
    public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        var token = await userAuthorization.GetTurnTokenAsync(turnContext, handlerName, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (token != null)
        {
            request.Headers["Authorization"] = [$"Bearer {token}"];
        }
    }
}

class AppTokenProvider(Microsoft.Agents.Authentication.IAccessTokenProvider tokenProvider, string resourceUrl, IList<string> scopes) : IAuthenticationProvider
{
    public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        var token = await tokenProvider.GetAccessTokenAsync(resourceUrl, scopes).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers["Authorization"] = [$"Bearer {token}"];
        }
    }
}
