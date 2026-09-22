// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AClientAuthenticationOptions
{
    public IReadOnlyDictionary<string, OAuthCredentialProviderOptions> Providers { get; init; }
        = new Dictionary<string, OAuthCredentialProviderOptions>(StringComparer.Ordinal);
}
