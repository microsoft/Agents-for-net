// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal interface IOAuthAuthorizationCodeReceiver
{
    Task<string> ReceiveCodeAsync(
        Uri authorizationUri,
        Uri redirectUri,
        string expectedState,
        CancellationToken cancellationToken);
}
