// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Samples.A2AClient.OAuth.Discovery;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Registration;

internal sealed record OAuthProviderApprovalRequest(
    Uri AgentOrigin,
    string? SecuritySchemeName,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<Uri> AdvertisedEndpoints,
    OAuthAuthorizationServerMetadata Metadata,
    Uri RedirectUri);

internal interface IOAuthProviderApproval
{
    Task<Uri?> RequestServerUriAsync(
        Uri agentOrigin,
        IReadOnlyList<Uri> advertisedEndpoints,
        CancellationToken cancellationToken);

    Task<bool> ApproveAsync(
        OAuthProviderApprovalRequest request,
        CancellationToken cancellationToken);
}
