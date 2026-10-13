// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Extensions.A2A.Authorization;

internal static class A2AOAuthFlowConfiguration
{
    internal static void BindScopes(IConfigurationSection configuration, OAuthFlows flows)
    {
        if (configuration == null || flows == null)
        {
            return;
        }

        if (flows.AuthorizationCode != null)
        {
            flows.AuthorizationCode.Scopes = ReadScopes(configuration.GetSection(nameof(OAuthFlows.AuthorizationCode)));
        }
        if (flows.ClientCredentials != null)
        {
            flows.ClientCredentials.Scopes = ReadScopes(configuration.GetSection(nameof(OAuthFlows.ClientCredentials)));
        }
        if (flows.DeviceCode != null)
        {
            flows.DeviceCode.Scopes = ReadScopes(configuration.GetSection(nameof(OAuthFlows.DeviceCode)));
        }
#pragma warning disable CS0618 // Preserve configured scope catalogs without changing validation of deprecated flows.
        if (flows.Implicit != null)
        {
            flows.Implicit.Scopes = ReadScopes(configuration.GetSection(nameof(OAuthFlows.Implicit)));
        }
        if (flows.Password != null)
        {
            flows.Password.Scopes = ReadScopes(configuration.GetSection(nameof(OAuthFlows.Password)));
        }
#pragma warning restore CS0618
    }

    internal static IList<string> GetScopeNames(OAuthFlows flows)
    {
        var scopes = new List<string>();
        AddScopeNames(scopes, flows?.AuthorizationCode?.Scopes);
        AddScopeNames(scopes, flows?.ClientCredentials?.Scopes);
        AddScopeNames(scopes, flows?.DeviceCode?.Scopes);
#pragma warning disable CS0618 // Deprecated flows must still be normalized when present.
        AddScopeNames(scopes, flows?.Implicit?.Scopes);
        AddScopeNames(scopes, flows?.Password?.Scopes);
#pragma warning restore CS0618
        scopes.Sort(StringComparer.Ordinal);
        return scopes;
    }

    private static void AddScopeNames(List<string> destination, IDictionary<string, string> scopes)
    {
        if (scopes == null)
        {
            return;
        }

        foreach (var scope in scopes.Keys)
        {
            destination.Add(scope);
        }
    }

    private static Dictionary<string, string> ReadScopes(IConfigurationSection flow)
    {
        // IConfiguration treats ':' in URI scopes as a path separator; retain each complete relative key.
        var scopes = flow.GetSection("Scopes")
            .AsEnumerable(makePathsRelative: true)
            .Where(entry => entry.Value != null)
            .ToArray();
        if (scopes.Any(entry => !IsUnresolvedTemplate(entry.Key)))
        {
            scopes = scopes
                .Where(entry => !IsUnresolvedTemplate(entry.Key))
                .ToArray();
        }

        return scopes
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static bool IsUnresolvedTemplate(string scope)
        => scope?.Contains("{{") == true
            && scope.Contains("}}");
}
