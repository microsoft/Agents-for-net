// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient.OAuth.Discovery;

internal interface IOAuthAuthorizationServerMetadataClient
{
    Task<OAuthAuthorizationServerMetadata> DiscoverAsync(
        IReadOnlyList<Uri> metadataCandidates,
        CancellationToken cancellationToken);
}
