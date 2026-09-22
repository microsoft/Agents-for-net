// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Samples.A2AClient;

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
}
