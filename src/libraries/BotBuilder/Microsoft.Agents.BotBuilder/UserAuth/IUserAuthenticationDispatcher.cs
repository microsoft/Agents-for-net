// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth
{
    public interface IUserAuthenticationDispatcher
    {
        IUserAuthentication Default {  get; }

        /// <summary>
        /// Get an authentication class via name
        /// </summary>
        /// <param name="flowName">The name of the user authentication flow</param>
        /// <returns>The user authentication handler</returns>
        /// <exception cref="InvalidOperationException">When cannot find the class with given name</exception>
        IUserAuthentication Get(string flowName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="flowName">The name of the authentication handler to use. If null, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns></returns>
        Task ResetStateAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sign in a user.
        /// </summary>
        /// <remarks>
        /// On success, this will put the token in ITurnState.Temp.AuthTokens[settingName].
        /// </remarks>
        /// <param name="turnContext">The turn context</param>
        /// <param name="flowName">The name of the authentication handler to use. If null, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in status</returns>
        Task<SignInResponse> SignUserInAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out a user.
        /// </summary>
        /// <param name="turnContext">The turn context</param>
        /// <param name="flowName">Optional. The name of the authentication handler to use. If not specified, the default handler name is used.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task SignOutUserAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default);
    }
}