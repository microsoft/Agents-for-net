// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.RestClients
{
    /// <summary>
    /// UserToken operations.
    /// </summary>
    internal interface IUserToken
    {
        /// <summary> Get token with HTTP message.</summary>
        /// <param name='userId'> User ID.</param>
        /// <param name='connectionName'> Connection name.</param>
        /// <param name='channelId'> Channel ID.</param>
        /// <param name='code'> Code.</param>
        /// <param name='cancellationToken'> The cancellation token.</param>
        /// <returns>A Task representing the <see cref="TokenResponse"/> of the HTTP operation.</returns>
        Task<TokenResponse> GetTokenAsync(string userId, string connectionName, string channelId = default(string), string code = default(string), CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Get AAD token with HTTP message.</summary>
        /// <param name='userId'> User ID.</param>
        /// <param name='connectionName'> Connection name.</param>
        /// <param name='aadResourceUrls'>AAD resource URLs. </param>
        /// <param name='channelId'>The channel ID. </param>
        /// <param name='cancellationToken'> The cancellation token.</param>
        /// <returns>A Task representing the <see cref="TokenResponse"/> of the HTTP operation.</returns>
        Task<IReadOnlyDictionary<string, TokenResponse>> GetAadTokensAsync(string userId, string connectionName, AadResourceUrls aadResourceUrls, string channelId = default(string), CancellationToken cancellationToken = default(CancellationToken));
        
        /// <summary>Sign out with HTTP message.</summary>
        /// <param name='userId'> User ID.</param>
        /// <param name='connectionName'>Connection name. </param>
        /// <param name='channelId'> Channel ID. </param>
        /// <param name='cancellationToken'> The cancellation token. </param>
        Task<object> SignOutAsync(string userId, string connectionName = default(string), string channelId = default(string), CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Get the token status with HTTP message. </summary>
        /// <param name='userId'> User ID.</param>
        /// <param name='channelId'> Channel ID.</param>
        /// <param name='include'> Include.</param>
        /// <param name='cancellationToken'> The cancellation token. </param>
        /// <returns>A task representing an IList of <see cref="TokenStatus"/> from the HTTP operation.</returns>
        Task<IReadOnlyList<TokenStatus>> GetTokenStatusAsync(string userId, string channelId = default(string), string include = default(string), CancellationToken cancellationToken = default(CancellationToken));

        /// <summary> Exchange. </summary>
        /// <param name='operations'>
        /// The operations group for this extension method.
        /// </param>
        /// <param name='userId'> User ID. </param>
        /// <param name='connectionName'> Connection name. </param>
        /// <param name='channelId'> Channel ID. </param>
        /// <param name='exchangeRequest'> Exchange request. </param>
        /// <param name='cancellationToken'> The cancellation token. </param>
        /// <returns> A task that represents the work queued to execute. </returns>
        Task<object> ExchangeAsyncAsync(string userId, string connectionName, string channelId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default(CancellationToken));
    }
}
