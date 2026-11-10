// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

internal static class A2AUserIdentity
{
    private const string MappedObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string MappedScopeClaim = "http://schemas.microsoft.com/identity/claims/scope";
    private const string MappedTenantIdClaim = "http://schemas.microsoft.com/identity/claims/tenantid";

    internal static string GetUserId(ClaimsIdentity identity)
    {
        if (identity?.IsAuthenticated != true || !IsDelegated(identity))
        {
            return null;
        }

        var issuer = identity.FindFirst("iss")?.Value;
        var tenantId = identity.FindFirst("tid")?.Value
            ?? identity.FindFirst(MappedTenantIdClaim)?.Value;
        var objectId = identity.FindFirst("oid")?.Value
            ?? identity.FindFirst(MappedObjectIdClaim)?.Value;
        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(objectId))
        {
            return $"oauth:{Encode(issuer ?? "entra")}:{Encode(tenantId)}:{Encode(objectId)}";
        }

        var subject = identity.FindFirst("sub")?.Value
            ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrEmpty(issuer) && !string.IsNullOrEmpty(subject)
            ? $"oauth:{Encode(issuer)}:{Encode(subject)}"
            : null;
    }

    internal static string GetUserIdFromTrustedToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityToken(token);
            return GetUserId(new ClaimsIdentity(jwt.Claims, "OAuth"));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string Encode(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static bool IsDelegated(ClaimsIdentity identity)
    {
        return identity.HasClaim(claim =>
            string.Equals(claim.Type, "scp", StringComparison.Ordinal)
            || string.Equals(claim.Type, "scope", StringComparison.Ordinal)
            || string.Equals(claim.Type, MappedScopeClaim, StringComparison.Ordinal)
            || (string.Equals(claim.Type, "idtyp", StringComparison.Ordinal)
                && string.Equals(claim.Value, "user", StringComparison.OrdinalIgnoreCase)));
    }
}
