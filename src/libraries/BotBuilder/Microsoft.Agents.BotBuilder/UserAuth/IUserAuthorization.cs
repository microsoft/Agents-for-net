// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth
{
    /// <summary>
    /// Handles user sign-in and sign-out.
    /// </summary>
    public interface IUserAuthorization
    {
        string Name { get; }

        /// <summary>
        /// Signs in a user.
        /// This method will be called automatically by the AgentApplication class.
        /// </summary>
        /// <param name="context">Current turn context.</param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="state">AgentApplication state.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The authentication token if user is signed in. Otherwise returns null. In that case the bot will attempt to sign the user in.</returns>
        Task<string> SignInUserAsync(ITurnContext context, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out a user.
        /// </summary>
        /// <param name="context">Current turn context.</param>
        /// <param name="state">AgentApplication state.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SignOutUserAsync(ITurnContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the sign in flow state.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ResetStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
    }
}
