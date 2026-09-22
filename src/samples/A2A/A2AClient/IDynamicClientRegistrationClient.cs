// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IDynamicClientRegistrationClient
{
    Task<OAuthClientRegistration> RegisterPublicClientAsync(
        string registrationId,
        Uri registrationEndpoint,
        Uri redirectUri,
        CancellationToken cancellationToken);
}
