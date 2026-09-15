// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Security.Claims;

namespace A2AAgent;

/// <summary>
/// Reads the caller identity claims produced by ASP.NET Core JWT bearer validation.
/// </summary>
/// <remarks>
/// <para>
/// <c>JwtBearerOptions.MapInboundClaims</c> defaults to <c>JwtSecurityTokenHandler.DefaultMapInboundClaims</c>,
/// which is <c>true</c>, so the shared sample authentication helper produces identities whose Entra claims
/// have been renamed to WS-* claim types. The delegated <c>scp</c> claim becomes the mapped scope
/// claim below, while <c>idtyp</c> keeps its short name.
/// </para>
/// <para>
/// Both spellings are accepted so the sample behaves the same whether a host maps inbound claims (the
/// default) or turns mapping off.
/// </para>
/// </remarks>
internal static class A2ATokenIdentity
{
    internal const string RequiredDelegatedScope = "access_as_user";

    internal const string ScopeClaim = "scp";
    internal const string IdentityTypeClaim = "idtyp";

    internal const string MappedTenantIdClaim = "http://schemas.microsoft.com/identity/claims/tenantid";
    internal const string MappedScopeClaim = "http://schemas.microsoft.com/identity/claims/scope";

    internal static void RequireDelegated(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!HasScope(identity) || IsApplicationIdentityType(identity))
        {
            throw new InvalidOperationException("This route requires a delegated user token.");
        }

        if (!HasScope(identity, RequiredDelegatedScope))
        {
            throw new InvalidOperationException($"This route requires the {RequiredDelegatedScope} delegated scope.");
        }
    }

    internal static bool HasScope(ClaimsIdentity identity)
        => FindFirst(identity, ScopeClaim, MappedScopeClaim) != null;

    internal static bool HasScope(ClaimsIdentity identity, string scope)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrEmpty(scope);

        return identity.Claims
            .Where(claim => claim.Type == ScopeClaim || claim.Type == MappedScopeClaim)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
    }

    private static bool IsApplicationIdentityType(ClaimsIdentity identity)
        => identity.HasClaim(IdentityTypeClaim, "app");

    private static string? FindFirst(ClaimsIdentity identity, string claimType, string alternateClaimType)
        => identity.FindFirst(claimType)?.Value ?? identity.FindFirst(alternateClaimType)?.Value;
}
