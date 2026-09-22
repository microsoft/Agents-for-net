// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class EntraOAuthCredentialProvider : GenericOAuth2CredentialProvider
{
    private static readonly IReadOnlyList<Uri> s_defaultAllowedAuthorities = [new Uri("https://login.microsoftonline.com")];

    public EntraOAuthCredentialProvider(OAuthCredentialProviderOptions options)
        : base(options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Type != OAuthCredentialProviderType.Entra)
        {
            throw new InvalidOperationException(
                $"Provider '{options.Id}' must declare Type '{OAuthCredentialProviderType.Entra}'.");
        }
    }

    protected override int ProviderSpecificity => 1;

    protected override IReadOnlyList<Uri> AllowedAuthorities
        => Options.AllowedAuthorities.Count > 0
            ? Options.AllowedAuthorities
            : s_defaultAllowedAuthorities;
}
