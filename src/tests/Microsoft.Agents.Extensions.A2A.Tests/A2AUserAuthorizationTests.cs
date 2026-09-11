// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AUserAuthorizationTests
{
    [Fact]
    public async Task SignInUserAsync_WithValidatedOpaqueToken_ReturnsToken()
    {
        var turnContext = CreateTurnContext(CreateAuthentication("opaque-token"));
        var authorization = CreateAuthorization();

        var response = await authorization.SignInUserAsync(turnContext);

        Assert.Equal("opaque-token", response.Token);
        Assert.Null(response.Expiration);
        Assert.False(response.IsExchangeable);
    }

    [Fact]
    public async Task SignInUserAsync_WithoutValidatedToken_Throws()
    {
        var turnContext = CreateTurnContext(CreateAuthentication(null));
        var authorization = CreateAuthorization();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => authorization.SignInUserAsync(turnContext));
    }

    [Fact]
    public async Task SignInUserAsync_WithExpiredValidatedJwt_Throws()
    {
        var jwt = new JwtSecurityToken(
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));
        var turnContext = CreateTurnContext(CreateAuthentication(new JwtSecurityTokenHandler().WriteToken(jwt)));
        var authorization = CreateAuthorization();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => authorization.SignInUserAsync(turnContext));
    }

    /// <summary>
    /// <c>AgentClaims.IsExchangeableToken</c> requires the audience to contain the app ID that requested
    /// the token ('azp' for v2, 'appid' for v1). A delegated token acquired by a separate public-client
    /// registration has <c>aud</c> = Agent API and <c>azp</c> = console client, so it is not exchangeable
    /// and the OBO route cannot run.
    /// </summary>
    [Fact]
    public async Task SignInUserAsync_DelegatedTokenFromSeparateClientRegistration_IsNotExchangeable()
    {
        var turnContext = CreateTurnContext(CreateAuthentication(CreateDelegatedToken(
            audience: AgentApiClientId,
            authorizedParty: ConsoleClientId)));

        var response = await CreateAuthorization().SignInUserAsync(turnContext);

        Assert.False(response.IsExchangeable);
    }

    [Fact]
    public async Task SignInUserAsync_DelegatedTokenFromAgentApiRegistration_IsExchangeable()
    {
        var turnContext = CreateTurnContext(CreateAuthentication(CreateDelegatedToken(
            audience: AgentApiClientId,
            authorizedParty: AgentApiClientId)));

        var response = await CreateAuthorization().SignInUserAsync(turnContext);

        Assert.True(response.IsExchangeable);
    }

    [Fact]
    public async Task SignInUserAsync_WithOBOScopesAndSeparateClientRegistration_Throws()
    {
        var turnContext = CreateTurnContext(CreateAuthentication(CreateDelegatedToken(
            audience: AgentApiClientId,
            authorizedParty: ConsoleClientId)));
        var authorization = new A2AUserAuthorization(
            "graph",
            Mock.Of<IConnections>(),
            new OBOSettings { OBOConnectionName = "ServiceConnection", OBOScopes = ["User.Read"] });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => authorization.SignInUserAsync(turnContext));
    }

    private const string AgentApiClientId = "22222222-2222-2222-2222-222222222222";
    private const string ConsoleClientId = "11111111-1111-1111-1111-111111111111";

    private static string CreateDelegatedToken(string audience, string authorizedParty)
    {
        var jwt = new JwtSecurityToken(
            claims:
            [
                new Claim("aud", audience),
                new Claim("ver", "2.0"),
                new Claim("azp", authorizedParty),
                new Claim("scp", "access_as_user"),
            ],
            expires: DateTime.UtcNow.AddMinutes(30));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private static A2AUserAuthorization CreateAuthorization()
        => new("a2a", Mock.Of<IConnections>(), new OBOSettings());

    private static ITurnContext CreateTurnContext(A2ARequestAuthentication authentication)
    {
        var context = new Mock<ITurnContext>();
        var services = new TurnContextStateCollection();
        services.Set(authentication);
        context.SetupGet(c => c.Services).Returns(services);
        return context.Object;
    }

    /// <summary>
    /// Builds request authentication the way the runtime does: an authentication ticket that carries the
    /// validated token, as produced by a handler with <c>SaveToken = true</c>.
    /// </summary>
    private static A2ARequestAuthentication CreateAuthentication(string token)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "caller")], "Test");
        var properties = new AuthenticationProperties();
        if (token != null)
        {
            properties.StoreTokens([new AuthenticationToken
            {
                Name = A2ARequestAuthentication.AccessTokenName,
                Value = token,
            }]);
        }

        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Features.Set<IAuthenticateResultFeature>(new AuthenticateResultFeature
        {
            AuthenticateResult = AuthenticateResult.Success(
                new AuthenticationTicket(principal, properties, "Test")),
        });

        return A2ARequestAuthentication.Create(httpContext.Request);
    }

    private sealed class AuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult AuthenticateResult { get; set; }
    }
}
