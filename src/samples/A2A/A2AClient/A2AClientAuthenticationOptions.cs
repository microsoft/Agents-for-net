// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AClientAuthenticationOptions
{
    public IReadOnlyDictionary<string, OAuthConnectionOptions> Connections { get; init; }
        = new Dictionary<string, OAuthConnectionOptions>(StringComparer.Ordinal);

    public OAuthConnectionOptions GetRequiredConnection(string securitySchemeName)
    {
        if (!Connections.TryGetValue(securitySchemeName, out OAuthConnectionOptions? connection))
        {
            throw new InvalidOperationException(
                $"Missing A2A client OAuth connection for security scheme '{securitySchemeName}'.");
        }

        return connection;
    }
}
