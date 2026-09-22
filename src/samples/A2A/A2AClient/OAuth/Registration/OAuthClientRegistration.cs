// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Samples.A2AClient.OAuth;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Registration;

internal enum OAuthTokenEndpointAuthenticationMethod
{
    None,
    ClientSecretBasic,
    ClientSecretPost,
}

internal sealed record OAuthClientRegistration(
    string Id,
    IReadOnlyList<A2AOAuthFlowType> GrantTypes,
    string ClientId,
    string? ClientSecret,
    Uri? RedirectUri,
    OAuthTokenEndpointAuthenticationMethod TokenEndpointAuthenticationMethod,
    bool UsePkce);
