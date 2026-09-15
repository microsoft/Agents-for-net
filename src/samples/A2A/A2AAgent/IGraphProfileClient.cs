// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace A2AAgent;

/// <summary>
/// Reads profile information for the signed-in user from Microsoft Graph.
/// </summary>
public interface IGraphProfileClient
{
    /// <summary>
    /// Gets the signed-in user's Microsoft Graph profile.
    /// </summary>
    /// <param name="accessToken">A delegated Microsoft Graph access token.</param>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>The signed-in user's profile.</returns>
    Task<GraphProfile> GetMeAsync(string accessToken, CancellationToken cancellationToken);
}
