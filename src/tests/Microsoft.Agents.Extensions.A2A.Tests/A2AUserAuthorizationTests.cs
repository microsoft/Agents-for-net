// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.UserAuth;
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

    private static A2ARequestAuthentication CreateAuthentication(string token)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"))
        };
        if (token != null)
        {
            httpContext.Request.Headers.Authorization = $"Bearer {token}";
        }

        return A2ARequestAuthentication.Create(httpContext.Request);
    }
}
