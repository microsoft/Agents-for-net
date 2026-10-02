// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

internal sealed class OAuthCredentialProviderOptions
{
    public required string Id { get; init; }

    public OAuthCredentialProviderType Type { get; init; }

    public IReadOnlyList<Uri> AllowedAuthorities { get; init; } = [];

    public IReadOnlyList<Uri> AllowedOrigins { get; init; } = [];

    public IReadOnlyList<string> AdditionalScopes { get; init; } = [];

    public IReadOnlyDictionary<string, OAuthClientRegistration> Registrations { get; init; }
        = new Dictionary<string, OAuthClientRegistration>(StringComparer.Ordinal);

    public bool AllowInteractiveApproval { get; init; }

    public Uri? RedirectUri { get; init; }

    public Uri? MetadataUrl { get; init; }

    public Uri? ServerUrl { get; init; }
}
