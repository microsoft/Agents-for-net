// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using System;
using System.Security.Claims;

namespace A2AAgent;

/// <summary>
/// Reads the caller identity claims produced by ASP.NET Core JWT bearer validation.
/// </summary>
/// <remarks>
/// <para>
/// <c>JwtBearerOptions.MapInboundClaims</c> defaults to <c>JwtSecurityTokenHandler.DefaultMapInboundClaims</c>,
/// which is <c>true</c>, so the shared sample authentication helper produces identities whose Entra claims
/// have been renamed to the WS-* claim types: <c>tid</c>, <c>oid</c>, <c>sub</c>, <c>scp</c>, and <c>roles</c>
/// become the mapped names below. Claims that have no inbound mapping (<c>azp</c>, <c>appid</c>, <c>idtyp</c>)
/// keep their short names.
/// </para>
/// <para>
/// Both spellings are accepted so the sample behaves the same whether a host maps inbound claims (the
/// default) or turns mapping off. This mirrors <c>AgentClaims.IsTenantIdIssuerValid</c>, which accepts the
/// short and mapped tenant-ID claims.
/// </para>
/// </remarks>
internal static class A2ATokenIdentity
{
    internal const string ScopeClaim = "scp";
    internal const string RolesClaim = "roles";
    internal const string IdentityTypeClaim = "idtyp";
    internal const string ObjectIdClaim = "oid";
    internal const string SubjectClaim = "sub";

    internal const string MappedTenantIdClaim = "http://schemas.microsoft.com/identity/claims/tenantid";
    internal const string MappedObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    internal const string MappedScopeClaim = "http://schemas.microsoft.com/identity/claims/scope";

    internal static void RequireDelegated(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!HasScope(identity) || IsApplicationIdentityType(identity))
        {
            throw new InvalidOperationException("This route requires a delegated user token.");
        }
    }

    internal static void RequireApplication(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var isApplication = IsApplicationIdentityType(identity) || HasRoles(identity);
        if (!isApplication || HasScope(identity))
        {
            throw new InvalidOperationException("This route requires an application token.");
        }
    }

    internal static string? FindTenantId(ClaimsIdentity identity)
        => FindFirst(identity, AuthenticationConstants.TenantIdClaim, MappedTenantIdClaim);

    internal static string? FindObjectId(ClaimsIdentity identity)
        => FindFirst(identity, ObjectIdClaim, MappedObjectIdClaim);

    internal static string? FindSubject(ClaimsIdentity identity)
        => FindFirst(identity, SubjectClaim, ClaimTypes.NameIdentifier);

    // 'azp' (v2) and 'appid' (v1) have no inbound claim mapping, so only the short names exist.
    internal static string? FindApplicationId(ClaimsIdentity identity)
        => FindFirst(identity, AuthenticationConstants.AzpClaim, AuthenticationConstants.AppIdClaim);

    internal static bool HasScope(ClaimsIdentity identity)
        => FindFirst(identity, ScopeClaim, MappedScopeClaim) != null;

    internal static bool HasRoles(ClaimsIdentity identity)
        => FindFirst(identity, RolesClaim, ClaimTypes.Role) != null;

    private static bool IsApplicationIdentityType(ClaimsIdentity identity)
        => identity.HasClaim(IdentityTypeClaim, "app");

    private static string? FindFirst(ClaimsIdentity identity, string claimType, string alternateClaimType)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return identity.FindFirst(claimType)?.Value ?? identity.FindFirst(alternateClaimType)?.Value;
    }
}
