// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace A2AAgent;

internal sealed class GitHubAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IHttpClientFactory httpClientFactory)
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

        HttpClient client = httpClientFactory.CreateClient(A2AAgentAuthenticationDefaults.GitHubHttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await client.SendAsync(request, Context.RequestAborted).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized || !response.IsSuccessStatusCode)
        {
            return AuthenticateResult.Fail("GitHub token validation failed.");
        }

        if (!HasRequiredScope(response.Headers))
        {
            return AuthenticateResult.Fail("GitHub token is missing the required repo scope.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(Context.RequestAborted).ConfigureAwait(false);
        GitHubUserDto user = await JsonSerializer.DeserializeAsync<GitHubUserDto>(
            responseStream,
            cancellationToken: Context.RequestAborted).ConfigureAwait(false)
            ?? throw new InvalidOperationException("GitHub user response was empty.");

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

    private static bool HasRequiredScope(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("X-OAuth-Scopes", out var scopeValues))
        {
            return false;
        }

        foreach (string? headerValue in scopeValues)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            string[] scopes = headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (string scope in scopes)
            {
                if (string.Equals(scope, "repo", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
