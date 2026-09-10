// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Authentication information validated by the ASP.NET Core authentication pipeline.
/// </summary>
internal sealed class A2ARequestAuthentication
{
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

        var identity = HttpHelper.GetClaimsIdentity(request) ?? new ClaimsIdentity();
        if (request.HttpContext.User?.Identity?.IsAuthenticated != true)
        {
            return new A2ARequestAuthentication(identity, null);
        }

        var authorization = request.Headers.Authorization.FirstOrDefault();
        var parts = authorization?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var accessToken = parts?.Length == 2 && parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : null;

        return new A2ARequestAuthentication(identity, accessToken);
    }
}
