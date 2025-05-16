// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    internal interface IUserAuthorizationFlow
    {
        /// <summary>
        /// Whether the current activity is a valid activity this flow handles.
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if valid. Otherwise, false.</returns>
        Task<bool> IsValidActivity(ITurnContext turnContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Drives a turn for this flow.  
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token response if available.</returns>
        /// <exception cref="AuthException"/>
        /// <exception cref="DuplicateExchangeException"/>
        Task<TokenResponse> OnFlowTurn(ITurnContext turnContext, CancellationToken cancellationToken);

        /// <summary>
        /// Resets this flow state.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
    }
}