// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Teams.Api.Clients;
using Microsoft.Teams.Common.Http;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    /// <summary>
    /// Provides extension methods for configuring and retrieving the Teams API client within a bot's turn context.
    /// </summary>
    /// <remarks>These extension methods enable integration with the Teams API by associating an ApiClient
    /// instance with the ITurnContext. This allows bot developers to access Teams-specific functionality during a
    /// conversation turn. The methods support both direct configuration and configuration via an AgentApplication
    /// instance.</remarks>
    public static class TeamsApiClientExtensions
    {
        public static void SetTeamsApiClient(this ITurnContext context, AgentApplication application, CancellationToken ct = default)
        {
            SetTeamsApiClient(context, application?.Options?.Connections, application?.Options?.HttpClientFactory, ct);
        }

        public static void SetTeamsApiClient(this ITurnContext context, IConnections connections, System.Net.Http.IHttpClientFactory httpClientFactory, CancellationToken ct = default)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            bool useAnonymous = AgentClaims.AllowAnonymous(context.Identity);
            IHttpClientOptions.HttpTokenFactory tokenFactory = useAnonymous ? null : () =>
            {
                var tokenAccess = connections.GetTokenProvider(context.Identity, context.Activity.ServiceUrl);
                return tokenAccess.GetAccessTokenAsync(AuthenticationConstants.BotFrameworkScope, [$"{AuthenticationConstants.BotFrameworkScope}/.default"]).ConfigureAwait(false).GetAwaiter().GetResult();
            };

            var client = new ApiClient(
                context.Activity.ServiceUrl,
                new WrapperHttpClientFactory(httpClientFactory, tokenFactory),
                ct);

            context.Services.Set<ApiClient>(client);
        }

        public static ApiClient GetTeamsApiClient(this ITurnContext context)
        {
            return context.Services.Get<ApiClient>();
        }
    }

    class WrapperHttpClientFactory(System.Net.Http.IHttpClientFactory inner, IHttpClientOptions.HttpTokenFactory tokenFactory) : Microsoft.Teams.Common.Http.IHttpClientFactory
    {
        public IHttpClient CreateClient()
        {
            return new Microsoft.Teams.Common.Http.HttpClient(inner.CreateClient()) { Options = { TokenFactory = tokenFactory } };
        }

        IHttpClient Microsoft.Teams.Common.Http.IHttpClientFactory.CreateClient(string name)
        {
            return new Microsoft.Teams.Common.Http.HttpClient(inner.CreateClient(name)) { Options = { TokenFactory = tokenFactory } };
        }
    }
}
