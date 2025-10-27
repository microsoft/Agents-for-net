// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// A factory to create REST clients to interact with a Channel Service.
    /// </summary>
    /// <remarks>
    /// Connector and UserToken client factory.
    /// </remarks>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <exception cref="System.ArgumentException"></exception>
    /// <exception cref="System.InvalidOperationException">Thrown when an instance of <see cref="IAccessTokenProvider"/> is not found via <see cref="IConnections"/>.</exception>
    public class RestChannelServiceClientFactory : IChannelServiceClientFactory
    {
        private readonly string _tokenServiceEndpoint;
        private readonly string _tokenServiceAudience;
        private readonly int? _iMaxApxConversationIdLength;
        private readonly ILogger _logger;
        private readonly IConnections _connections;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <param name="configuration"></param>
        /// <param name="httpClientFactory">Used to create an HttpClient with the fullname of this class</param>
        /// <param name="connections"></param>
        /// <param name="tokenServiceEndpoint"></param>
        /// <param name="tokenServiceAudience"></param>
        /// <param name="logger"></param>
        /// <param name="customClient">For testing purposes only.</param>
        public RestChannelServiceClientFactory(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IConnections connections,
            string tokenServiceEndpoint = AuthenticationConstants.BotFrameworkOAuthUrl,
            string tokenServiceAudience = AuthenticationConstants.BotFrameworkScope,
            ILogger logger = null)
        {
            AssertionHelpers.ThrowIfNull(configuration, nameof(configuration));

            _logger = logger ?? NullLogger.Instance;
            _connections = connections ?? throw new ArgumentNullException(nameof(connections));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            var tokenEndpoint = configuration?.GetValue<string>($"{nameof(RestChannelServiceClientFactory)}:TokenServiceEndpoint");
            _tokenServiceEndpoint = string.IsNullOrWhiteSpace(tokenEndpoint)
                ? tokenServiceEndpoint ?? throw new ArgumentNullException(nameof(tokenServiceEndpoint))
                : tokenEndpoint;

            var tokenAudience = configuration?.GetValue<string>($"{nameof(RestChannelServiceClientFactory)}:TokenServiceAudience");
            _tokenServiceAudience = string.IsNullOrWhiteSpace(tokenAudience)
                ? tokenServiceAudience ?? throw new ArgumentNullException(nameof(tokenServiceAudience))
                : tokenAudience;

            _iMaxApxConversationIdLength = configuration?.GetValue<int?>($"{nameof(RestChannelServiceClientFactory)}:MaxApxConversationIdLength");
        }

        /// <inheritdoc />
        public Task<IConnectorClient> CreateConnectorClientAsync(ClaimsIdentity claimsIdentity, string serviceUrl, string audience, CancellationToken cancellationToken = default, IList<string> scopes = null, bool useAnonymous = false)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(serviceUrl, nameof(serviceUrl));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(audience, nameof(audience));

            return Task.FromResult<IConnectorClient>(new RestConnectorClient(
                new Uri(serviceUrl),
                _httpClientFactory,
                useAnonymous ? null : () =>
                {
                    try
                    {
                        var tokenAccess = _connections.GetTokenProvider(claimsIdentity, serviceUrl);
                        return tokenAccess.GetAccessTokenAsync(audience, scopes);
                    }
                    catch (Exception ex)
                    {
                        // have to do it this way b/c of the lambda expression. 
                        throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                ErrorHelper.NullIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
                    }
                },
                typeof(RestChannelServiceClientFactory).FullName,
                maxApxConversationIdLength: _iMaxApxConversationIdLength));
        }

        public Task<IConnectorClient> CreateConnectorClientAsync(ITurnContext turnContext, string audience = null, IList<string> scopes = null, bool useAnonymous = false, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.Recipient.Role == RoleTypes.ConnectorUser)
            {
                return Task.FromResult((IConnectorClient)new MCSConnectorClient(new Uri(turnContext.Activity.ServiceUrl), _httpClientFactory));
            }

            if (!AgenticAuthorization.IsAgenticRequest(turnContext))
            {
                return CreateConnectorClientAsync(turnContext.Identity, turnContext.Activity.ServiceUrl, audience ?? AgentClaims.GetTokenAudience(turnContext.Identity), cancellationToken, scopes, useAnonymous);
            }

            return Task.FromResult<IConnectorClient>(new RestConnectorClient(
                new Uri(turnContext.Activity.ServiceUrl),
                _httpClientFactory,
                () =>
                {
                    var connection = _connections.GetTokenProvider(turnContext.Identity, turnContext.Activity);
                    if (connection is IAgenticTokenProvider agenticTokenProvider)
                    {
                        try
                        {
                            if (turnContext.Activity.Recipient.Role.Equals(RoleTypes.AgenticIdentity))
                            {
                                return agenticTokenProvider.GetAgenticInstanceTokenAsync(
                                    AgenticAuthorization.GetAgentInstanceId(turnContext),
                                    cancellationToken);
                            }

                            return agenticTokenProvider.GetAgenticUserTokenAsync(
                                AgenticAuthorization.GetAgentInstanceId(turnContext), 
                                AgenticAuthorization.GetAgenticUser(turnContext),
                                connection.ConnectionSettings.Scopes ?? [AuthenticationConstants.ApxProductionScope], 
                                cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            // have to do it this way b/c of the lambda expression. 
                            throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                    ErrorHelper.AgenticTokenProviderFailed, ex, AgenticAuthorization.GetAgentInstanceId(turnContext), AgenticAuthorization.GetAgenticUser(turnContext), turnContext.Activity.Recipient.Role);
                        }
                    }
                    else
                    {
                        // have to do it this way b/c of the lambda expression. 
                        throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                ErrorHelper.AgenticTokenProviderNotFound, null, $"{AgentClaims.GetAppId(turnContext.Identity)}:{turnContext.Activity.ServiceUrl}");
                    }
                },
                typeof(RestChannelServiceClientFactory).FullName, 
                maxApxConversationIdLength: _iMaxApxConversationIdLength));
        }

        /// <inheritdoc />
        public Task<IUserTokenClient> CreateUserTokenClientAsync(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken, bool useAnonymous = false)
        {
            AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));

            var appId = AgentClaims.GetAppId(claimsIdentity) ?? Guid.Empty.ToString();

            return Task.FromResult<IUserTokenClient>(new RestUserTokenClient(
                appId,
                new Uri(_tokenServiceEndpoint),
                _httpClientFactory,
                useAnonymous ? null : () =>
                {
                    try
                    {
                        var tokenAccess = _connections.GetTokenProvider(claimsIdentity, _tokenServiceEndpoint);
                        return tokenAccess.GetAccessTokenAsync(_tokenServiceAudience, [$"{_tokenServiceAudience}/.default"]);
                    }
                    catch (Exception ex)
                    {
                        // have to do it this way b/c of the lambda expression. 
                        throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<OperationCanceledException>(
                                ErrorHelper.NullUserTokenProviderIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{_tokenServiceEndpoint}");
                    }
                },
                typeof(RestChannelServiceClientFactory).FullName,
                _logger));
        }
    }
}
