// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Authorization;
/// <summary>
/// Supplies the A2A request token to the agent authorization pipeline.
/// </summary>
/// <remarks>
/// This authorization handler consumes the token supplied with the A2A request for the current
/// turn. It does not perform interactive sign-in; OBO exchange, when configured, is a separate
/// operation performed by the base <see cref="OBOExchange"/> implementation.
/// </remarks>
public class A2AUserAuthorization : OBOExchange, IUserAuthorization
{
    private readonly A2AUserAuthorizationSettings _settings;

    /// <summary>
    /// Initializes a handler loaded from configuration.
    /// </summary>
    /// <param name="name">The authorization handler name.</param>
    /// <param name="storage">The storage provider required by the module loader.</param>
    /// <param name="connections">The configured authorization connections.</param>
    /// <param name="configurationSection">The configuration section containing A2A authorization settings.</param>
    /// <param name="logger">The optional logger for authorization operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public A2AUserAuthorization(string name, IStorage storage, IConnections connections, IConfigurationSection configurationSection, ILogger logger = null)
        : this(name, connections, A2AUserAuthorizationSettings.FromConfiguration(configurationSection), logger)
    {
    }

    /// <summary>
    /// Code-first constructor.
    /// </summary>
    /// <param name="name">The authorization handler name.</param>
    /// <param name="connections">The configured authorization connections.</param>
    /// <param name="settings">The OBO exchange settings to adapt for A2A.</param>
    /// <param name="logger">The optional logger for authorization operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public A2AUserAuthorization(string name, IConnections connections, OBOSettings settings, ILogger logger = null)
        : this(name, connections, A2AUserAuthorizationSettings.FromOBOSettings(settings), logger)
    {
    }

    /// <summary>
    /// Code-first constructor that accepts Agent Card authorization metadata.
    /// </summary>
    /// <param name="name">The authorization handler name.</param>
    /// <param name="connections">The configured authorization connections.</param>
    /// <param name="settings">The A2A authorization and Agent Card settings.</param>
    /// <param name="logger">The optional logger for authorization operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public A2AUserAuthorization(string name, IConnections connections, A2AUserAuthorizationSettings settings, ILogger logger = null) : base(connections)
    {
        _settings = settings ?? new A2AUserAuthorizationSettings();
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the OBO settings used only when an exchange is requested.
    /// </summary>
    /// <returns>The configured A2A OBO settings.</returns>
    protected override OBOSettings GetOBOSettings()
    {
        return _settings;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The A2A request token is used as the source token. OBO exchange is performed only when
    /// the caller supplies an exchange connection or scopes.
    /// </remarks>
    public string Name { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// A2A requests already carry their request token, so this method obtains that token rather
    /// than starting an interactive sign-in flow. Any configured OBO exchange remains separate.
    /// </remarks>
    public async Task<TokenResponse> GetRefreshedUserTokenAsync(ITurnContext turnContext, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
    {
        var tokenResponse = CreateTokenResponse(turnContext);

        if (tokenResponse.Expiration != null)
        {
            var diff = tokenResponse.Expiration - DateTimeOffset.UtcNow;
            if (diff.HasValue && diff?.TotalMinutes <= 0)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UnexpectedTokenExpiration, null, [Name]);
            }
        }

        try
        {
            return await HandleOBO(turnContext, tokenResponse, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<TokenResponse> SignInUserAsync(ITurnContext turnContext, bool forceSignIn = false, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
    {
        // There is no "sign in" or external token retrieval in this handler.  A single impl is sufficient.
        return await GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets no state because A2A request-token authorization is stateless.
    /// </summary>
    /// <param name="turnContext">The current turn context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        // No concept of reset with ConnectorAuth
        return Task.CompletedTask;
    }

    /// <summary>
    /// Signs out no user because A2A request-token authorization does not retain a user session.
    /// </summary>
    /// <param name="turnContext">The current turn context.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    public Task SignOutUserAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        // No concept of sign-out with ConnectorAuth
        return Task.CompletedTask;
    }

    private TokenResponse CreateTokenResponse(ITurnContext turnContext)
    {
        var requestToken = turnContext.Services.Get<A2ARequestAuthentication>()?.AccessToken;
        if (!string.IsNullOrEmpty(requestToken))
        {
            return CreateTokenResponse(requestToken);
        }

        if (turnContext.Identity is CaseSensitiveClaimsIdentity identity)
        {
            return CreateTokenResponse(identity.SecurityToken.UnsafeToString());
        }

        throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UnexpectedRequestToken, null, [Name]);
    }

    private static TokenResponse CreateTokenResponse(string token)
    {
        var tokenResponse = new TokenResponse()
        {
            Token = token,
        };

        try
        {
            var jwtToken = new JwtSecurityToken(token);
            tokenResponse.Expiration = jwtToken.ValidTo;
            tokenResponse.IsExchangeable = AgentClaims.IsExchangeableToken(jwtToken);
        }
        catch (Exception)
        {
            tokenResponse.IsExchangeable = false;
        }

        return tokenResponse;
    }

}
