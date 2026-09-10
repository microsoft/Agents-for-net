// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Claims;

namespace A2AAgent;

internal static class A2ATokenIdentity
{
    internal static void RequireDelegated(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (!identity.HasClaim(claim => claim.Type == "scp") ||
            identity.HasClaim("idtyp", "app"))
        {
            throw new InvalidOperationException("This route requires a delegated user token.");
        }
    }

    internal static void RequireApplication(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var isApplication = identity.HasClaim("idtyp", "app") ||
            identity.HasClaim(claim => claim.Type == "roles");
        if (!isApplication || identity.HasClaim(claim => claim.Type == "scp"))
        {
            throw new InvalidOperationException("This route requires an application token.");
        }
    }
}
