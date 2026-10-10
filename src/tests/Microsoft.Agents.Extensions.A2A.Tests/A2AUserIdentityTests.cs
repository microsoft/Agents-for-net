// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.A2A.Authorization;
using System.Security.Claims;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AUserIdentityTests
{
    [Fact]
    public void GetUserId_WithDelimiterInIssuerOrSubject_ProducesDistinctIds()
    {
        var first = CreateIdentity(
            new Claim("iss", "https://idp/x"),
            new Claim("sub", "y:z"));
        var second = CreateIdentity(
            new Claim("iss", "https://idp/x:y"),
            new Claim("sub", "z"));

        Assert.NotEqual(A2AUserIdentity.GetUserId(first), A2AUserIdentity.GetUserId(second));
    }

    [Fact]
    public void GetUserId_WithDelimiterInTenantOrObjectId_ProducesDistinctIds()
    {
        var first = CreateIdentity(
            new Claim("tid", "tenant"),
            new Claim("oid", "user:one"));
        var second = CreateIdentity(
            new Claim("tid", "tenant:user"),
            new Claim("oid", "one"));

        Assert.NotEqual(A2AUserIdentity.GetUserId(first), A2AUserIdentity.GetUserId(second));
    }

    private static ClaimsIdentity CreateIdentity(params Claim[] claims)
    {
        return new ClaimsIdentity(
        [
            .. claims,
            new Claim("scp", "agent.access"),
        ],
        authenticationType: "Bearer");
    }
}
