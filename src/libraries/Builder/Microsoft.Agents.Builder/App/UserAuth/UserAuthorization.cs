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

#if MANUAL_SIGNIN
using Microsoft.Agents.Authentication;
using System.Security.Claims;
using Microsoft.Agents.Core;
#endif

namespace Microsoft.Agents.Builder.App.UserAuth
{
#if MANUAL_SIGNIN
    public delegate Task AuthorizationSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken);
#endif
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

#if MANUAL_SIGNIN
        private const string SignInCompletionEventName = "application/vnd.microsoft.SignInCompletion";

        /// <summary>
        /// Callback when user sign in success
        /// </summary>
        private AuthorizationSuccess _userSignInSuccessHandler;
#endif

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

#if MANUAL_SIGNIN
            AddManualSignInCompletionHandler();
#endif
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
                if (!token.IsExchangeable)
                {
                    var diff = token.Expiration - DateTimeOffset.UtcNow;
                    if (diff.HasValue && diff?.TotalMinutes >= 5)
                    {
                        return token.Token;
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

#if MANUAL_SIGNIN
        /// <summary>
        /// Acquire a token with OAuth.  <see cref="OnUserSignInSuccess(Func{ITurnContext, ITurnState, string, string, CancellationToken, Task})"/> and
        /// <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/> should
        /// be set to handle continuation.  Those handlers will be called with a token is acquired.
        /// </summary>
        /// <param name="turnContext"> The turn context.</param>
        /// <param name="turnState"></param>
        /// <param name="handlerName">The name of the authorization setting.</param>
        /// <param name="exchangeConnection"></param>
        /// <param name="exchangeScopes"></param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="System.InvalidOperationException">If a flow is already active.</exception>
        public async Task SignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnState, nameof(turnState));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(handlerName, nameof(handlerName));

            // Handle the case where we already have a token for this handler and the Agent is calling this again.
            var existingCachedToken = await GetTurnTokenAsync(turnContext, turnState, handlerName, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (existingCachedToken != null)
            {
                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, handlerName, existingCachedToken, turnContext.Activity, cancellationToken).ConfigureAwait(false);
                }
                return;
            }

            // Only one active flow allowed
            var signInState = GetSignInState(turnState);
            if (signInState.IsActive())
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationAlreadyActive, null, signInState.ActiveHandler);
            }

            // Start flow or get token
            SignInResponse response = await _dispatcher.SignUserInAsync(turnContext, handlerName, true, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);

            if (response.Status == SignInStatus.Pending)
            {
                signInState.ActiveHandler = handlerName;
                signInState.PassedOBOConnectionName = exchangeConnection;
                signInState.PassedOBOScopes = exchangeScopes;

                // This indicates this is a manual flow.  The ManualContext.Response will be set at completion.
                signInState.ManualContext = new ManualContext() { HandlerName = handlerName, InitiatingActivity = turnContext.Activity };

                // This Activity will be used to trigger the handler added by `OnSignInComplete`.
                // This is routed like any other Activity.
                signInState.ContinuationActivity = new Activity()
                {
                    Type = ActivityTypes.Event,
                    Name = SignInCompletionEventName,
                    ServiceUrl = turnContext.Activity.ServiceUrl,
                    ChannelId = turnContext.Activity.ChannelId,
                    ChannelData = turnContext.Activity.ChannelData,
                    Value = signInState.ManualContext
                };
                signInState.ContinuationActivity.ApplyConversationReference(turnContext.Activity.GetConversationReference(), isIncoming: true);

                return;
            }

            if (response.Status == SignInStatus.Error)
            {
                DeleteSignInState(turnState);

                if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(turnContext, turnState, handlerName, response, turnContext.Activity, cancellationToken).ConfigureAwait(false);
                    return;
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationFailed, response.Error, handlerName);
                }
            }

            // Call the handler immediately if we get a token back (user already signed in).
            if (response.Status == SignInStatus.Complete)
            {
                DeleteSignInState(turnState);
                CacheToken(handlerName, response);

                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, handlerName, response.TokenResponse.Token, turnContext.Activity, cancellationToken).ConfigureAwait(false);
                }
            }
        }
#endif

        public async Task SignOutUserAsync(ITurnContext turnContext, ITurnState turnState, string? flowName = null, CancellationToken cancellationToken = default)
        {
            var flow = flowName ?? DefaultHandlerName;
            DeleteCachedToken(flow);
            DeleteSignInState(turnState);
            await _dispatcher.SignOutUserAsync(turnContext, flow, cancellationToken).ConfigureAwait(false);
        }

#if MANUAL_SIGNIN
        /// <summary>
        /// The handler function is called when the user has successfully signed in
        /// </summary>
        /// <remarks>
        /// This is only used for manual user authorization.  The Auto Sign In will continue the turn with the original user message.
        /// </remarks>
        /// <param name="handler">The handler function to call when the user has successfully signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public void OnUserSignInSuccess(AuthorizationSuccess handler)
        {
            _userSignInSuccessHandler = handler;
        }
#endif

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

#if MANUAL_SIGNIN
                    // Handle manual signin error callback
                    if (signInState.ManualContext != null)
                    {
                        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                        // This will execute OnUserSignInFailure in a new TurnContext.  This is for manual sign in only.
                        // This could be optimized to execute the OnUserSignInFailure directly if the we're not currently
                        // handling an Invoke.
                        signInState.ManualContext.Response = response;
                        signInState.ContinuationActivity.Value = signInState.ManualContext;

                        await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInState.ContinuationActivity, _app, cancellationToken).ConfigureAwait(false);
                        return false;
                    }
#endif

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
#if MANUAL_SIGNIN
                        if (signInState.ManualContext != null)
                        {
                            // Continue a manual sign in completion.
                            // Since we could be handling an Invoke in this turn, we need to continue the conversation in a different
                            // turn with the SignInCompletion Event.  This is because Teams has expectation for Invoke response times
                            // an a the OnSignInSuccess/Fail handling by the Agent could exceed that.  Also, this is all executing prior
                            // to other Application routes having been run (ex. before/after turn).
                            // This is handled by the route added in AddManualSignInCompletionHandler().
                            //
                            // Note:  This should be optimized to only do this if the current TurnContext.Activity is Invoke.  Otherwise,
                            // the OnUserSignInSuccess can be called directly?
                            signInState.ManualContext.Response = response;
                            signInState.ContinuationActivity.Value = signInState.ManualContext;
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInState.ContinuationActivity, _app, cancellationToken).ConfigureAwait(false);
                            return false;
                        }
#endif

                        // If the current activity matches the one used to trigger sign in, then
                        // this is because the user received a token that didn't involve a multi-turn
                        // flow.  No further action needed.
                        if (!ProtocolJsonSerializer.Equals(signInState.ContinuationActivity, turnContext.Activity))
                        {
                            // Since we could be handling an Invoke in this turn, and Teams has expectation for Invoke response times,
                            // we need to continue the conversation in a different turn with the original Activity that triggered sign in.
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInState.ContinuationActivity, _app, cancellationToken).ConfigureAwait(false);
                            return false;
                        }
                    }
                }
            }

            // Sign in is complete (or never started if Auto Sign in is false)
            // AgentApplication will perform normal ITurnContext.Activity routing to Agent.
            return true;
        }

#if MANUAL_SIGNIN
        // For manual sign in (SignInUserAsync), an Event is sent proactively to get the
        // OnSignInSuccess and OnSignInFailure into a non-Invoke TurnContext.
        private void AddManualSignInCompletionHandler()
        {
            static Task<bool> routeSelector(ITurnContext context, CancellationToken _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, SignInCompletionEventName)
            );

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                var manualContext = ProtocolJsonSerializer.ToObject<ManualContext>(turnContext.Activity.Value);
                if (manualContext.Response.Status == SignInStatus.Complete && _userSignInSuccessHandler != null)
                {
                    CacheToken(manualContext.HandlerName, manualContext.Response);
                    await _userSignInSuccessHandler(
                        turnContext,
                        turnState,
                        manualContext.HandlerName,
                        manualContext.Response.TokenResponse.Token,
                        manualContext.InitiatingActivity,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(
                        turnContext,
                        turnState,
                        manualContext.HandlerName,
                        manualContext.Response,
                        manualContext.InitiatingActivity,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            _app.AddRoute(routeSelector, routeHandler);
        }
#endif

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

#if MANUAL_SIGNIN
    public class ManualContext
    {
        public string HandlerName { get; set; }
        public SignInResponse Response { get; set; }
        public IActivity InitiatingActivity { get; set; }
    }
#endif

    class SignInState
    {
        public bool IsActive() => !string.IsNullOrEmpty(ActiveHandler);
        public string ActiveHandler { get; set; }
        public IActivity ContinuationActivity { get; set; }
        public string RuntimeOBOConnectionName { get; set; }
        public IList<string> RuntimeOBOScopes { get; set; }

#if MANUAL_SIGNIN
        public ManualContext ManualContext { get; set; }
#endif
    }
}
