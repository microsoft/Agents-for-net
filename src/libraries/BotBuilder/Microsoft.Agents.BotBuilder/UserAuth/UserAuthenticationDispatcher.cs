// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.UserAuth
{
    /// <summary>
    /// The user authentication manager
    /// </summary>
    public class UserAuthenticationDispatcher : IUserAuthenticationDispatcher
    {
        private readonly Dictionary<string, IUserAuthentication> _userAuthHandlers = [];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userAuthHandlers"></param>
        public UserAuthenticationDispatcher(IUserAuthentication[] userAuthHandlers)
        {
            ArgumentNullException.ThrowIfNull(nameof(userAuthHandlers));

            foreach(var authenticator in userAuthHandlers)
            {
                _userAuthHandlers.Add(authenticator.Name, authenticator);
            }
        }

        public IUserAuthentication Default => _userAuthHandlers.Count > 0 ? _userAuthHandlers.First().Value : throw new InvalidOperationException("No IUserAuthentication have been defined.");

        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="options">The authentication options</param>
        /// <param name="storage">The storage to use.</param>
        /// <exception cref="TeamsAIException">Throws when the options does not contain authentication handlers</exception>
        public UserAuthenticationDispatcher(UserAuthenticationOptions options)
        {
            ArgumentNullException.ThrowIfNull(nameof(options));

            if (options.Handlers.Count == 0)
            {
                throw new ArgumentException("Authentications setting is empty");
            }

            foreach(var authenticator in options.Handlers)
            {
                _userAuthHandlers.Add(authenticator.Name, authenticator);
            }
        }

        /// <inheritdoc/>
        public async Task<SignInResponse> SignUserInAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nameof(turnContext));

            IUserAuthentication auth = Get(flowName);
            TokenResponse token;
            try
            {
                token = await auth.SignInUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SignInResponse newResponse = new(SignInStatus.Error)
                {
                    Error = ex,
                    Cause = AuthExceptionReason.Other
                };
                if (ex is AuthException authEx)
                {
                    newResponse.Cause = authEx.Cause;
                }

                return newResponse;
            }

            if (token != null)
            {
                return new SignInResponse(SignInStatus.Complete)
                {
                    TokenResponse = token
                };
            }

            return new SignInResponse(SignInStatus.Pending);
        }

        /// <inheritdoc/>
        public async Task SignOutUserAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(nameof(turnContext));

            IUserAuthentication auth = Get(flowName);
            await auth.SignOutUserAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task ResetStateAsync(ITurnContext turnContext, string flowName, CancellationToken cancellationToken = default)
        {
            await Get(flowName).ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public IUserAuthentication Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Default; 
            }

            if (_userAuthHandlers.TryGetValue(name, out IUserAuthentication? value))
            {
                return value;
            }

            throw new InvalidOperationException($"Could not find user authentication handler '{name}'.");
        }
    }
}
