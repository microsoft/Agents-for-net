// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Builder.Errors;
using System.Collections.Generic;
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Builder.App.UserAuth
{
    public delegate Task AuthorizationFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken);

    /// <summary>
    /// UserAuthorization supports and extensible number of OAuth flows.
    /// 
    /// Auto Sign In:
    /// If enabled in <see cref="UserAuthorizationOptions"/>, sign in starts automatically after the first Message the user sends.  When
    /// the sign in is complete, the turn continues with the original message. On failure, <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/>
    /// is called.
    /// 
    /// </summary>
    /// <remarks>
    /// This is always executed in the context of a turn for the user in <see cref="ITurnContext.Activity.From"/>.
    /// </remarks>
    public class UserAuthorization
    {
        private readonly AutoSignInSelector? _startSignIn;
        private const string SIGN_IN_STATE_KEY = "__SignInState__";
        private readonly IUserAuthorizationDispatcher _dispatcher;
        private readonly UserAuthorizationOptions _options;
        private readonly AgentApplication _app;
        private readonly Dictionary<string, TokenResponse> _authTokens = [];

        /// <summary>
        /// Callback when user sign in fail
        /// </summary>
        private AuthorizationFailure _userSignInFailureHandler;

        public string DefaultHandlerName { get; private set; }

        public UserAuthorization(AgentApplication app, UserAuthorizationOptions options)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _dispatcher = options.Dispatcher;

            if (_app.Options.Adapter == null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentNullException>(ErrorHelper.UserAuthorizationRequiresAdapter, null);
            }

            if (_options.AutoSignIn != null)
            {
                _startSignIn = _options.AutoSignIn;
            }
            else
            {
                // If AutoSignIn wasn't specified, default to true. 
                _startSignIn = (context, cancellationToken) => Task.FromResult(true);
            }

            DefaultHandlerName = _options.DefaultHandlerName ?? _dispatcher.Default.Name;

            if (!_dispatcher.TryGet(DefaultHandlerName, out _))
            {
                throw ExceptionHelper.GenerateException<IndexOutOfRangeException>(ErrorHelper.UserAuthorizationDefaultHandlerNotFound, null, DefaultHandlerName);
            }
        }

        [Obsolete("Use Task<string> GetTurnTokenAsync(ITurnContext, string) instead")]
        public string GetTurnToken(string handlerName)
        {
            return _authTokens.TryGetValue(handlerName, out var token) ? token.Token : default;
        }

        /// <summary>
        /// Return a previously acquired token.
        /// </summary>
        /// <remarks>
        /// This is a mechanism to access a previously acquired user token during the turn. It should be
        /// considered a method to get a token for each handler, not as a way to initiate the signin process.
        /// This will also handle refreshing the token if expired between initial acquisition and use.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="handlerName"></param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetTurnTokenAsync(ITurnContext turnContext, string handlerName = default, CancellationToken cancellationToken = default)
        {
            return await ExchangeTurnTokenAsync(turnContext, handlerName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> ExchangeTurnTokenAsync(ITurnContext turnContext, string handlerName = default, string exchangeConnection = default, IList<string> exchangeScopes = default, CancellationToken cancellationToken = default)
        {
            handlerName ??= DefaultHandlerName;

            if (_authTokens.TryGetValue(handlerName, out var token))
            {
                // An exchangeable token needs to be exchanged.
                if (!turnContext.IsAgenticRequest())
                {
                    if (!token.IsExchangeable)
                    {
                        var diff = token.Expiration - DateTimeOffset.UtcNow;
                        if (diff.HasValue && diff?.TotalMinutes >= 5)
                        {
                            return token.Token;
                        }
                    }
                }


                // Get a new token if near expiration, or it's an exchangeable token.
                var handler = _dispatcher.Get(handlerName);
                var response = await handler.GetRefreshedUserTokenAsync(turnContext, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);
                if (response?.Token != null)
                {
                    _authTokens[handlerName] = response;
                    return response.Token;
                }

                // This is a critical error since the only way we are here is we had a token (user signed in) yet
                // didn't get a token back.  We are not it a place to handle a multi-turn sign in.
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UnexpectedAuthorizationState, null, handlerName);
            }

            return null;
        }

        public async Task SignOutUserAsync(ITurnContext turnContext, ITurnState turnState, string? flowName = null, CancellationToken cancellationToken = default)
        {
            var flow = flowName ?? DefaultHandlerName;
            DeleteCachedToken(flow);
            DeleteSignInState(turnState);
            await _dispatcher.SignOutUserAsync(turnContext, flow, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The handler function is called when the user sign in flow fails
        /// </summary>
        /// <remarks>
        /// This is called for either Manual or Auto SignIn flows.  However, normally expected AgentApplication
        /// Turn process has not been performed during an Auto Sign In.  This handler should be used to send failure message to the user
        /// and the turn ended.
        /// </remarks>
        /// <param name="handler">The handler function to call when the user failed to signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public void OnUserSignInFailure(AuthorizationFailure handler)
        {
            _userSignInFailureHandler = handler;
        }

        /// <summary>
        /// This starts/continues the sign in flow.
        /// </summary>
        /// <remarks>
        /// This should be called to start or continue the user auth until true is returned, which indicates sign in is complete.
        /// When complete, the token is cached and can be access via <see cref="GetTurnTokenAsync"/>.  
        /// <see cref="OnUserSignInFailure"/> is called on an error completion.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="handlerName">The name of the handler defined in <see cref="UserAuthorizationOptions"/></param>
        /// <param name="forceAuto"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>false indicates the sign in is not complete, or that further processing of the Activity should stop.</returns>
        internal async Task<bool> StartOrContinueSignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName = null, bool forceAuto = false, CancellationToken cancellationToken = default)
        {
            // If a flow is active, continue that.
            var signInState = GetSignInState(turnState);
            string? activeFlowName = signInState.ActiveHandler;
            bool flowContinuation = activeFlowName != null;
            bool autoSignIn = forceAuto || (_startSignIn != null && await _startSignIn(turnContext, cancellationToken));

            if (autoSignIn || flowContinuation)
            {
                // Auth flow hasn't start yet.
                activeFlowName ??= handlerName ?? DefaultHandlerName;

                // Get token or start flow for specified flow.
                SignInResponse response = await _dispatcher.SignUserInAsync(
                    turnContext,
                    activeFlowName,
                    forceSignIn: !flowContinuation,
                    exchangeConnection: signInState.RuntimeOBOConnectionName,
                    exchangeScopes: signInState.RuntimeOBOScopes,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (response.Status == SignInStatus.Duplicate)
                {
                    return false;
                }

                if (response.Status == SignInStatus.Pending)
                {
                    if (!flowContinuation)
                    {
                        // Bank the incoming Activity so it can be executed after sign in is complete.
                        signInState.ContinuationActivity = turnContext.Activity;
                        signInState.ActiveHandler = activeFlowName;

                        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }

                    // Flow started, pending user input
                    return false;
                }

                // Hard error and the flow is cancelled.  It is possible there is a scenario where a retry is desired, but
                // unhandled at the moment.
                if (response.Status == SignInStatus.Error)
                {
                    // Clear user auth state
                    await _dispatcher.ResetStateAsync(turnContext, activeFlowName, cancellationToken).ConfigureAwait(false);
                    DeleteSignInState(turnState);
                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (_userSignInFailureHandler != null)
                    {
                        await _userSignInFailureHandler(turnContext, turnState, activeFlowName, response, signInState.ContinuationActivity, cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    await turnContext.SendActivitiesAsync(
                        _options.SignInFailedMessage == null ? [MessageFactory.Text("SignIn Failed")] : _options.SignInFailedMessage(activeFlowName, response),
                        cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (response.Status == SignInStatus.Complete)
                {
                    DeleteSignInState(turnState);
                    CacheToken(activeFlowName, response);

                    if (signInState.ContinuationActivity != null)
                    {
                        // If the current activity matches the one used to trigger sign in, then
                        // this is because the user received a token that didn't involve a multi-turn
                        // flow.  No further action needed.
                        if (!ProtocolJsonSerializer.Equals(signInState.ContinuationActivity, turnContext.Activity))
                        {
                            // Since we could be handling an Invoke in this turn, and Teams has expectation for Invoke response times,
                            // we need to continue the conversation in a different turn with the original Activity that triggered sign in.
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(
                                turnContext.Identity, 
                                signInState.ContinuationActivity.ApplyConversationReference(turnContext.Activity.GetConversationReference(), isIncoming: true), 
                                _app, 
                                cancellationToken).ConfigureAwait(false);
                            return false;
                        }
                    }
                }
            }

            // Sign in is complete (or never started if Auto Sign in is false)
            // AgentApplication will perform normal ITurnContext.Activity routing to Agent.
            return true;
        }

        /// <summary>
        /// Set token in state
        /// </summary>
        /// <param name="name">The name of token</param>
        /// <param name="response">The value of token</param>
        private void CacheToken(string name, SignInResponse response)
        {
            _authTokens[name] = response.TokenResponse;
        }

        /// <summary>
        /// Delete token from turn state
        /// </summary>
        /// <param name="name">The name of token</param>
        private void DeleteCachedToken(string name)
        {
            _authTokens.Remove(name);
        }

        private static SignInState GetSignInState(ITurnState turnState)
        {
            return turnState.User.GetValue<SignInState>(SIGN_IN_STATE_KEY, () => new());
        }

        private static void DeleteSignInState(ITurnState turnState)
        {
            turnState.User.DeleteValue(SIGN_IN_STATE_KEY);
        }
    }

    class SignInState
    {
        public bool IsActive() => !string.IsNullOrEmpty(ActiveHandler);
        public string ActiveHandler { get; set; }
        public IActivity ContinuationActivity { get; set; }
        public string RuntimeOBOConnectionName { get; set; }
        public IList<string> RuntimeOBOScopes { get; set; }
    }
}
