// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Samples.A2AClient;

internal enum OAuthTokenEndpointAuthenticationMethod
{
    None,
    ClientSecretBasic,
    ClientSecretPost,
}

internal sealed class OAuthConnectionOptions
{
    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public Uri? RedirectUri { get; init; }

    public IReadOnlyList<Uri> AllowedOrigins { get; init; } = [];

    public IReadOnlyList<string> AdditionalScopes { get; init; } = [];

    public OAuthTokenEndpointAuthenticationMethod TokenEndpointAuthenticationMethod { get; init; }

    public bool UsePkce { get; init; } = true;
}
