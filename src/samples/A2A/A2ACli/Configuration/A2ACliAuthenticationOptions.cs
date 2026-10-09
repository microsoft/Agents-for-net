// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Samples.A2ACli.OAuth.Configuration;

namespace Microsoft.Agents.Samples.A2ACli.Configuration;

internal sealed class A2ACliAuthenticationOptions
{
    public IReadOnlyDictionary<string, OAuthCredentialProviderOptions> Providers { get; init; }
        = new Dictionary<string, OAuthCredentialProviderOptions>(StringComparer.Ordinal);
}
