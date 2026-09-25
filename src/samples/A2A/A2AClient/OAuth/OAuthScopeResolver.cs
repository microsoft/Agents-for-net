// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Samples.A2AClient.A2A;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

namespace Microsoft.Agents.Samples.A2AClient.OAuth;

internal static class OAuthScopeResolver
{
    public static IReadOnlyList<string> GetScopes(
        A2AAgentCardAuthentication authentication,
        OAuthCredentialProviderOptions provider)
    {
        var scopes = new List<string>(authentication.Scopes);
        foreach (string scope in provider.AdditionalScopes)
        {
            if (!string.IsNullOrWhiteSpace(scope)
                && !scopes.Exists(candidate => string.Equals(candidate, scope, StringComparison.Ordinal)))
            {
                scopes.Add(scope);
            }
        }

        return scopes;
    }

    public static IReadOnlyList<string> GetCacheIdentityScopes(IReadOnlyList<string> scopes)
        => scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
}
