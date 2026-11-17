// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Claims;

namespace WorkIQBriefingAgent;

internal static class SignedInUser
{
    public static string GetId(ClaimsPrincipal principal)
    {
        // JWT validation may preserve short claim names or map them to the standard claim URIs.
        var tenant = principal.FindFirst("tid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
        var user = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var scopes = principal.FindFirst("scp")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/scope")?.Value
            ?? string.Empty;
        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(tenant, out var tenantId)
            || !Guid.TryParse(user, out var userId)
            || !scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("access_as_user", StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException("A signed-in user with the agent's access_as_user permission is required.");
        }
        return $"{tenantId:D}:{userId:D}";
    }
}
