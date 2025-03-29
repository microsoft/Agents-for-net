// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using System.Threading;
using System.Security.Claims;
using System.Collections.Generic;
using Microsoft.Agents.Connector;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Handles creation of IConnectorClient and IUserTokenClient objects for use when handling incoming Activities.
    /// </summary>
    /// <remarks>This is not something normally used or implemented by an Agent developer.  Rather it is used
    /// internally by the IChannelAdapter to create the clients need to communicate back the sender.</remarks>
    public interface IChannelServiceClientFactory
    {
        /// <summary>
        /// Creates a <see cref="IConnectorClient"/> that can be used to create <see cref="IConnectorClient"/>.
        /// </summary>
        /// <param name="claimsIdentity">The inbound <see cref="Activity"/>'s <see cref="ClaimsIdentity"/>.</param>
        /// <param name="audience"></param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="scopes">The scopes to request.</param>
        /// <param name="serviceUrl">The service URL.</param>
        /// <param name="useAnonymous">Whether to use anonymous credentials.</param>
        /// <remarks>
        /// This is called at the beginning of each turn.
        /// </remarks>
        /// <returns>A <see cref="IConnectorClient"/>.</returns>
        Task<IConnectorClient> CreateConnectorClientAsync(ClaimsIdentity claimsIdentity, string serviceUrl, string audience, CancellationToken cancellationToken, IList<string> scopes = null, bool useAnonymous = false);

        /// <summary>
        /// Creates the appropriate <see cref="IUserTokenClient" /> instance.
        /// </summary>
        /// <param name="claimsIdentity">The inbound <see cref="Activity"/>'s <see cref="ClaimsIdentity"/>.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="useAnonymous">Whether to use anonymous credentials.</param>
        /// <returns>Asynchronous Task with <see cref="IUserTokenClient" /> instance.</returns>
        Task<IUserTokenClient> CreateUserTokenClientAsync(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken, bool useAnonymous = false);
    }
}
