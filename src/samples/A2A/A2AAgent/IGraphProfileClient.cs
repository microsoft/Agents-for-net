// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent;

public interface IGraphProfileClient
{
    Task<GraphProfile> GetMeAsync(string accessToken, CancellationToken cancellationToken);
}
