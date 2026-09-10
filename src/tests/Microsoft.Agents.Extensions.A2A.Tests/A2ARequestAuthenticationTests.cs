// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2ARequestAuthenticationTests
{
    [Fact]
    public void Create_WithAuthenticatedBearerRequest_ExposesToken()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "caller")],
                    authenticationType: "Test"))
        };
        context.Request.Headers.Authorization = "Bearer opaque-token";

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.Equal("opaque-token", authentication.AccessToken);
        Assert.True(authentication.Identity.IsAuthenticated);
    }

    [Fact]
    public void Create_WithUnauthenticatedBearerRequest_DoesNotExposeToken()
    {
        var context = new DefaultHttpContext();
        var jwt = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "unvalidated-caller")],
            expires: DateTime.UtcNow.AddMinutes(5));
        context.Request.Headers.Authorization = $"Bearer {new JwtSecurityTokenHandler().WriteToken(jwt)}";

        var authentication = A2ARequestAuthentication.Create(context.Request);

        Assert.Null(authentication.AccessToken);
        Assert.Equal("unvalidated-caller", authentication.Identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }
}
