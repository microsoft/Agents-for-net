// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

/// <summary>
/// Authentication information validated by the ASP.NET Core authentication pipeline.
/// </summary>
internal sealed class A2ARequestAuthentication
{
    /// <summary>
    /// Token name used by ASP.NET Core authentication handlers, including
    /// <c>JwtBearerHandler</c> with <c>SaveToken = true</c>, to store the credential that was validated.
    /// </summary>
    internal const string AccessTokenName = "access_token";

    private A2ARequestAuthentication(ClaimsIdentity identity, string accessToken)
    {
        Identity = identity;
        AccessToken = accessToken;
    }

    /// <summary>
    /// Gets the authenticated request identity.
    /// </summary>
    public ClaimsIdentity Identity { get; }

    /// <summary>
    /// Gets the validated bearer token, if one was supplied.
    /// </summary>
    public string AccessToken { get; }

    /// <summary>
    /// Creates authentication information from an HTTP request.
    /// </summary>
    public static A2ARequestAuthentication Create(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new A2ARequestAuthentication(
            GetRequestIdentity(request),
            GetValidatedAccessToken(request.HttpContext));
    }

    private static ClaimsIdentity GetRequestIdentity(HttpRequest request)
    {
        ClaimsIdentity authenticatedIdentity = request.HttpContext.User?.Identities
            .FirstOrDefault(identity => identity.IsAuthenticated);
        if (authenticatedIdentity is not null)
        {
            return authenticatedIdentity;
        }

        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out AuthenticationHeaderValue header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return new ClaimsIdentity();
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        if (!tokenHandler.CanReadToken(header.Parameter))
        {
            return new ClaimsIdentity();
        }

        try
        {
            JwtSecurityToken token = tokenHandler.ReadJwtToken(header.Parameter);
            return new ClaimsIdentity(token.Claims);
        }
        catch (SecurityTokenException)
        {
            return new ClaimsIdentity();
        }
        catch (ArgumentException)
        {
            return new ClaimsIdentity();
        }
    }

    /// <summary>
    /// Returns the access token that an ASP.NET Core authentication handler validated for this request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The token is read from the authentication ticket that <c>UseAuthentication</c> stores in
    /// <see cref="IAuthenticateResultFeature"/> — the same value
    /// <c>HttpContext.GetTokenAsync("access_token")</c> returns — so only a credential that an
    /// authentication handler actually validated and chose to save is exposed.
    /// </para>
    /// <para>
    /// The raw <c>Authorization</c> header is deliberately not used. An authenticated principal does not
    /// imply that the header carried the credential that produced it: a request authenticated by another
    /// scheme could carry an arbitrary attacker-supplied bearer header, and forwarding it would hand that
    /// value to the agent's authorization handlers and to any on-behalf-of exchange.
    /// </para>
    /// <para>
    /// Handlers must opt in with <c>SaveToken = true</c>. When they do not, no request token is exposed and
    /// <see cref="A2AUserAuthorization"/> falls back to the validated security token on the identity.
    /// </para>
    /// </remarks>
    private static string GetValidatedAccessToken(HttpContext context)
    {
        if (context?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var authenticateResult = context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult;
        if (authenticateResult?.Succeeded != true)
        {
            return null;
        }

        return authenticateResult.Properties?.GetTokenValue(AccessTokenName);
    }
}
