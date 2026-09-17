// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace A2AAgent;

internal sealed class GitHubAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IGitHubClientFactory gitHubClients)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string AccessTokenName = "access_token";
    private const string GitHubLoginClaimType = "urn:github:login";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string authorizationHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        IGitHubClient client = gitHubClients.Create(token);

        User user;
        try
        {
            user = await client.User.Current().ConfigureAwait(false);
        }
        catch (AuthorizationException)
        {
            return AuthenticateResult.Fail("GitHub token validation failed.");
        }
        catch (ApiException)
        {
            return AuthenticateResult.Fail("GitHub token validation failed.");
        }
        catch (HttpRequestException)
        {
            return AuthenticateResult.Fail("GitHub token validation failed.");
        }

        IReadOnlyList<string> oauthScopes = client.GetLastApiInfo()?.OauthScopes ?? [];
        if (!oauthScopes.Contains("repo", StringComparer.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("GitHub token is missing the required repo scope.");
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name));
        identity.AddClaim(new Claim(GitHubLoginClaimType, user.Login));

        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken
            {
                Name = AccessTokenName,
                Value = token,
            },
        ]);

        return AuthenticateResult.Success(
            new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                properties,
                Scheme.Name));
    }
}
