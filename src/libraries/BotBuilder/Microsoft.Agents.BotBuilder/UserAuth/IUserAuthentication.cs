// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth
{
    /// <summary>
    /// Handles user sign-in and sign-out.
    /// </summary>
    public interface IUserAuthentication
    {
        string Name { get; }

        /// <summary>
        /// Signs in a user.
        /// This method will be called automatically by the Application class.
        /// </summary>
        /// <param name="context">Current turn context.</param>
        /// <param name="state">Application state.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The authentication token if user is signed in. Otherwise returns null. In that case the bot will attempt to sign the user in.</returns>
        Task<TokenResponse> SignInUserAsync(ITurnContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out a user.
        /// </summary>
        /// <param name="context">Current turn context.</param>
        /// <param name="state">Application state.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SignOutUserAsync(ITurnContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the user is signed, if they are then return the token.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token if the user is signed. Otherwise null.</returns>
        Task<string?> IsUserSignedInAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);

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
