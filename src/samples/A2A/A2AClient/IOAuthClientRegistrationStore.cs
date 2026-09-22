// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IOAuthClientRegistrationStore
{
    Task<OAuthClientRegistration?> GetAsync(
        string providerIdentity,
        Uri redirectUri,
        CancellationToken cancellationToken);

    Task SaveAsync(
        string providerIdentity,
        Uri redirectUri,
        OAuthClientRegistration registration,
        CancellationToken cancellationToken);
}
