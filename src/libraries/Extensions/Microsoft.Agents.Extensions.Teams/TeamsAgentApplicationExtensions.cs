// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Teams.Api.Clients;
using Microsoft.Teams.Common.Http;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    public static class TeamsAgentApplicationExtensions
    {
        public static async Task<ApiClient> GetTeamsApiClient(this AgentApplication application, ITurnContext context, CancellationToken ct = default)
        {
            if (application?.Options?.Connections == null)
            {
                throw new InvalidOperationException("AgentApplication does not have a Connections configured.");
            }

            if (application?.Options?.HttpClientFactory == null)
            {
                throw new InvalidOperationException("AgentApplication does not have a HttpClientFactory configured.");
            }

            bool useAnonymous = AgentClaims.AllowAnonymous(context.Identity);
            IHttpClientOptions.HttpTokenFactory tokenFactory = useAnonymous ? null : () =>
            {
                var tokenAccess = application.Options.Connections.GetTokenProvider(context.Identity, context.Activity.ServiceUrl);
                return tokenAccess.GetAccessTokenAsync(AuthenticationConstants.BotFrameworkScope, [$"{AuthenticationConstants.BotFrameworkScope}/.default"]).ConfigureAwait(false).GetAwaiter().GetResult();
            };

            return new ApiClient(
                context.Activity.ServiceUrl,
                new WrapperHttpClientFactory(application.Options.HttpClientFactory, tokenFactory),
                ct);
        }

        public static Task<ApiClient> GetTeamsApiClient(this TeamsAgentExtension teamsExtension, ITurnContext context, CancellationToken ct = default)
        {
            return teamsExtension.AgentApplication.GetTeamsApiClient(context, ct);
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
