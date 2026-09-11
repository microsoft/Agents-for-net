// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

/// <summary>
/// Exercises token capture through the real ASP.NET Core <see cref="AuthenticationMiddleware"/> so the
/// authentication ticket (and therefore <c>IAuthenticateResultFeature</c>) is populated exactly as it is
/// at runtime, rather than by hand-constructing features.
/// </summary>
public class A2ARequestAuthenticationTests
{
    private const string BearerScheme = "TestBearer";
    private const string TicketOnlyScheme = "TestTicketOnly";
    private const string ValidToken = "valid-request-token";

    [Fact]
    public async Task Create_WithValidatedBearer_ExposesSavedToken()
    {
        var context = await AuthenticateAsync(BearerScheme, $"Bearer {ValidToken}");

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.True(authentication.Identity.IsAuthenticated);
        Assert.Equal(ValidToken, authentication.AccessToken);
    }

    [Fact]
    public async Task Create_WithValidatedOpaqueToken_ExposesSavedToken()
    {
        var context = await AuthenticateAsync(BearerScheme, "Bearer valid-opaque");

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.Equal("valid-opaque", authentication.AccessToken);
    }

    [Fact]
    public async Task Create_WithNonBearerPrincipalAndMaliciousBearerHeader_DoesNotExposeHeaderToken()
    {
        // A scheme that authenticates without inspecting the Authorization header (for example a cookie)
        // must not cause an attacker-supplied bearer header to be treated as a validated credential.
        var context = await AuthenticateAsync(TicketOnlyScheme, "Bearer attacker-supplied-token");

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.True(authentication.Identity.IsAuthenticated);
        Assert.Null(authentication.AccessToken);
    }

    [Fact]
    public async Task Create_WithRejectedBearer_DoesNotExposeToken()
    {
        // A syntactically valid JWT that the scheme rejects: claims are still read for turn identity,
        // but the rejected token is never exposed as a credential.
        var jwt = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "rejected-caller")],
            expires: DateTime.UtcNow.AddMinutes(5)));
        var context = await AuthenticateAsync(BearerScheme, $"Bearer {jwt}");

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.False(authentication.Identity.IsAuthenticated);
        Assert.Null(authentication.AccessToken);
    }

    [Fact]
    public async Task Create_WithAnonymousRequest_HasNoToken()
    {
        var context = await AuthenticateAsync(BearerScheme, authorizationHeader: null);

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.False(authentication.Identity.IsAuthenticated);
        Assert.Null(authentication.AccessToken);
    }

    [Fact]
    public void Create_WithUnauthenticatedBearerRequest_DoesNotExposeToken()
    {
        // No authentication configured: claims still come from the unvalidated JWT for turn identity,
        // but the unvalidated token itself is never exposed as a credential.
        var context = new DefaultHttpContext();
        var jwt = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "unvalidated-caller")],
            expires: DateTime.UtcNow.AddMinutes(5));
        context.Request.Headers.Authorization = $"Bearer {new JwtSecurityTokenHandler().WriteToken(jwt)}";

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.Null(authentication.AccessToken);
        Assert.Equal("unvalidated-caller", authentication.Identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    private static async Task<HttpContext> AuthenticateAsync(string defaultScheme, string authorizationHeader)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(defaultScheme)
            .AddScheme<AuthenticationSchemeOptions, BearerLikeHandler>(BearerScheme, _ => { })
            .AddScheme<AuthenticationSchemeOptions, TicketOnlyHandler>(TicketOnlyScheme, _ => { });

        var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        if (authorizationHeader != null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        var middleware = new AuthenticationMiddleware(
            _ => Task.CompletedTask,
            provider.GetRequiredService<IAuthenticationSchemeProvider>());
        await middleware.Invoke(context);

        return context;
    }

    /// <summary>
    /// Mimics <c>JwtBearerHandler</c> with <c>SaveToken = true</c>: validates the bearer credential and
    /// stores the validated value on the ticket.
    /// </summary>
    private sealed class BearerLikeHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string header = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string token = header["Bearer ".Length..];
            if (!token.StartsWith("valid", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("invalid token"));
            }

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "caller")], Scheme.Name));
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken
            {
                Name = A2ARequestAuthentication.AccessTokenName,
                Value = token,
            }]);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, properties, Scheme.Name)));
        }
    }

    /// <summary>
    /// Mimics a non-bearer scheme (for example cookies): authenticates without reading the Authorization
    /// header and stores no token on the ticket.
    /// </summary>
    private sealed class TicketOnlyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "cookie-caller")], Scheme.Name));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, new AuthenticationProperties(), Scheme.Name)));
        }
    }
}
