﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.UserAuth;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.BotBuilder.Errors;
using System.Collections.Generic;

namespace Microsoft.Agents.BotBuilder.App.UserAuth
{
    public delegate Task AuthorizationSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, CancellationToken cancellationToken);
    public delegate Task AuthorizationFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, CancellationToken cancellationToken);

    /// <summary>
    /// UserAuthorization supports and extensible number of OAuth flows.
    /// 
    /// Auto Sign In:
    /// If enabled in <see cref="UserAuthorizationOptions"/>, sign in starts automatically after the first Message the user sends.  When
    /// the sign in is complete, the turn continues with the original message. On failure, an optional message is sent, otherwise
    /// and exception thrown.
    /// 
    /// Manual Sign In:
    /// <see cref="SignInUserAsync"/> is used to get a cached token or start the sign in.  In either case, the
    /// <see cref="OnUserSignInSuccess(Func{ITurnContext, ITurnState, string, string, CancellationToken, Task})"/> and
    /// <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/> should
    /// be set to handle continuation.  That is, after calling GetTokenOrStartSignInAsync, the turn should be considered complete,
    /// and performing actions after that could be confusing.
    /// </summary>
    /// <remarks>
    /// This is always executed in the context of a turn for the user in <see cref="ITurnContext.Activity.From"/>.
    /// </remarks>
    public class UserAuthorizationFeature
    {
        private readonly AutoSignInSelectorAsync? _startSignIn;
        private const string IS_SIGNED_IN_KEY = "__InSignInFlow__";
        private const string SIGNIN_ACTIVITY_KEY = "__SignInFlowActivity__";
        private const string SignInCompletionEventName = "application/vnd.microsoft.SignInCompletion";
        private readonly IUserAuthorizationDispatcher _dispatcher;
        private readonly UserAuthorizationOptions _options;
        private readonly AgentApplication _app;
        private readonly Dictionary<string, TokenResponse> _authTokens = [];

        /// <summary>
        /// Callback when user sign in success
        /// </summary>
        private AuthorizationSuccess _userSignInSuccessHandler;

        /// <summary>
        /// Callback when user sign in fail
        /// </summary>
        private AuthorizationFailure _userSignInFailureHandler;

        public string Default { get; private set; }

        public UserAuthorizationFeature(AgentApplication app, UserAuthorizationOptions options)
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

            Default = _options.Default ?? _dispatcher.Default.Name;
            AddManualSignInCompletionHandler();
        }

        /// <summary>
        /// Return a previously acquired token.
        /// </summary>
        /// <param name="handlerName"></param>
        /// <returns></returns>
        public string GetToken(string handlerName)
        {
            return _authTokens.TryGetValue(handlerName, out var token) ? token.Token : default(string);
        }

        /// <summary>
        /// Acquire a token with OAuth.  <see cref="OnUserSignInSuccess(Func{ITurnContext, ITurnState, string, string, CancellationToken, Task})"/> and
        /// <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/> should
        /// be set to handle continuation.  Those handlers will be called with a token is acquired.
        /// </summary>
        /// <param name="turnContext"> The turn context.</param>
        /// <param name="turnState"></param>
        /// <param name="handlerName">The name of the authorization setting.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="InvalidOperationException">If a flow is already active.</exception>
        public async Task SignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(turnState);
            ArgumentException.ThrowIfNullOrWhiteSpace(handlerName);

            // Only one active flow allowed
            if (!string.IsNullOrEmpty(UserInSignInFlow(turnState)))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationAlreadyActive, null);
            }

            // Handle the case where we already have a token for this handler and the bot is calling this again.
            var existingCachedToken = GetToken(handlerName);
            if (existingCachedToken != null)
            {
                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, handlerName, existingCachedToken, cancellationToken).ConfigureAwait(false);
                }
            }

            SignInResponse response = await _dispatcher.SignUserInAsync(turnContext, handlerName, cancellationToken).ConfigureAwait(false);

            if (response.Status == SignInStatus.Pending)
            {
                SetActiveFlow(turnState, handlerName);

                // This Activity will be used to trigger the handler added by `OnSignInComplete`.
                // The Activity.Value will be updated in SignUserInAsync when flow is complete/error.
                var continuationActivity = new Activity()
                {
                    Type = ActivityTypes.Event,
                    Name = SignInCompletionEventName,
                    ServiceUrl = turnContext.Activity.ServiceUrl,
                    ChannelId = turnContext.Activity.ChannelId,
                    ChannelData = turnContext.Activity.ChannelData,
                };
                continuationActivity.ApplyConversationReference(turnContext.Activity.GetConversationReference(), isIncoming: true);

                SetSingInContinuationActivity(turnState, continuationActivity);

                return;
            }

            if (response.Status == SignInStatus.Error)
            {
                if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(turnContext, turnState, handlerName, response, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationFailed, response.Error, handlerName);
                }
            }

            // Call the handler immediately if the user was already signed in.
            if (response.Status == SignInStatus.Complete)
            {
                DeleteActiveFlow(turnState);
                CacheToken(handlerName, response.TokenResponse);

                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, handlerName, response.TokenResponse.Token, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task SignOutUserAsync(ITurnContext turnContext, ITurnState turnState, string? flowName = null, CancellationToken cancellationToken = default)
        {
            var flow = flowName ?? Default;
            await _dispatcher.SignOutUserAsync(turnContext, flow, cancellationToken).ConfigureAwait(false);
            DeleteCachedToken(flow);
        }

        public async Task ResetStateAsync(ITurnContext turnContext, ITurnState turnState, string handlerName = null, CancellationToken cancellationToken = default)
        {
            handlerName ??= Default;
            await _dispatcher.ResetStateAsync(turnContext, handlerName, cancellationToken).ConfigureAwait(false);
            DeleteActiveFlow(turnState);
            DeleteCachedToken(handlerName);
        }


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

        /// <summary>
        /// The handler function is called when the user sign in flow fails
        /// </summary>
        /// <remarks>
        /// This is only used for manual user authorization.  The Auto Sign In will end the turn with and optional error message
        /// or exception.
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
        /// When complete, the token is cached and can be access via <see cref="GetToken"/>.  For manual sign in, the <see cref="OnUserSignInSuccess"/> or 
        /// <see cref="OnUserSignInFailure"/> are called at completion.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="handlerName">The name of the handler defined in <see cref="UserAuthorizationOptions"/></param>
        /// <param name="cancellationToken"></param>
        /// <returns>false indicates the sign in is not complete.</returns>
        internal async Task<bool> AutoSignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName = null, CancellationToken cancellationToken = default)
        {
            // If a flow is active, continue that.
            string? activeFlowName = UserInSignInFlow(turnState);
            bool flowContinuation = activeFlowName != null;
            bool shouldStartSignIn = _startSignIn != null && await _startSignIn(turnContext, cancellationToken);

            if (shouldStartSignIn || flowContinuation)
            {
                // Auth flow hasn't start yet.
                activeFlowName ??= handlerName ?? Default;

                // Get token or start flow for specified flow.
                SignInResponse response = await _dispatcher.SignUserInAsync(turnContext, activeFlowName, cancellationToken).ConfigureAwait(false);

                if (response.Status == SignInStatus.Pending)
                {
                    if (!flowContinuation)
                    {
                        // Bank the incoming Activity so it can be executed after sign in is complete.
                        SetSingInContinuationActivity(turnState, turnContext.Activity);

                        // Requires user action, save state and stop processing current activity.  Done with this turn.
                        SetActiveFlow(turnState, activeFlowName);
                        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    return false;
                }

                // An InvalidActivity is expected, but anything else is a hard error and the flow is cancelled.
                if (response.Status == SignInStatus.Error)
                {
                    // Clear user auth state
                    await _dispatcher.ResetStateAsync(turnContext, activeFlowName, cancellationToken).ConfigureAwait(false);
                    DeleteActiveFlow(turnState);

                    var signInContinuation = DeleteSingInContinuationActivity(turnState);
                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (IsSignInCompletionEvent(signInContinuation))
                    {
                        signInContinuation.Value = new SignInEventValue() { HandlerName = activeFlowName, Response = response };
                        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                        await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    if (_options.SignInFailedMessage == null)
                    {
                        throw response.Error;
                    }

                    await turnContext.SendActivitiesAsync(_options.SignInFailedMessage(activeFlowName, response), cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (response.Status == SignInStatus.Complete)
                {
                    DeleteActiveFlow(turnState);
                    CacheToken(activeFlowName, response.TokenResponse);

                    var signInContinuation = DeleteSingInContinuationActivity(turnState);
                    if (signInContinuation != null)
                    {
                        if (IsSignInCompletionEvent(signInContinuation))
                        {
                            // Continue a manual sign in completion.
                            // Since we could be handling an Invoke in this turn, we need to continue the conversation in a different
                            // turn with the SignInCompletion Event.  This is because Teams has expectation for Invoke response times
                            // an a the OnSignInSuccess/Fail handling by the bot could exceed that.  Also, this is all executing prior
                            // to other Application routes having been run (ex. before/after turn).
                            // This is handled by the route added in AddManualSignInCompletionHandler().
                            signInContinuation.Value = new SignInEventValue() { HandlerName = activeFlowName, Response = response };
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                            return false;
                        }

                        // If the current activity matches the one used to trigger sign in, then
                        // this is because the user received a token that didn't involve a multi-turn
                        // flow.  No further action needed.
                        if (!ProtocolJsonSerializer.Equals(signInContinuation, turnContext.Activity))
                        {
                            // Since we could be handling an Invoke in this turn, and Teams has expectation for Invoke response times,
                            // we need to continue the conversation in a different turn with the original Activity that triggered sign in.
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                            return false;
                        }
                    }
                }

                // If we got this far, fall through to normal Activity route handling.
                if (_options.CompletedMessage != null)
                {
                    await turnContext.SendActivitiesAsync(_options.CompletedMessage(activeFlowName, response), cancellationToken).ConfigureAwait(false);
                }
            }

            // Sign in is complete.  Either a sign in completed, or auto sign in isn't enabled.
            return true;
        }

        /// <summary>
        /// For manual sign in (GetTokenOrStartSignInAsync), an Event is sent proactively to get the
        /// OnSignInSuccess and OnSignInFailure into a non-Invoke TurnContext.
        /// </summary>
        private void AddManualSignInCompletionHandler()
        {
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, SignInCompletionEventName)
            );

            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                var signInCompletion = ProtocolJsonSerializer.ToObject<SignInEventValue>(turnContext.Activity.Value);
                if (signInCompletion.Response.Status == SignInStatus.Complete && _userSignInSuccessHandler != null)
                {
                    CacheToken(signInCompletion.HandlerName, signInCompletion.Response.TokenResponse);
                    await _userSignInSuccessHandler(turnContext, turnState, signInCompletion.HandlerName, signInCompletion.Response.TokenResponse.Token, cancellationToken).ConfigureAwait(false);
                }
                else if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(turnContext, turnState, signInCompletion.HandlerName, signInCompletion.Response, cancellationToken).ConfigureAwait(false);
                }
            };

            _app.AddRoute(routeSelector, routeHandler);
        }

        public static bool IsSignInCompletionEvent(IActivity activity)
        {
            return string.Equals(activity?.Type, ActivityTypes.Event, StringComparison.OrdinalIgnoreCase)
                && string.Equals(activity?.Name, SignInCompletionEventName);

        }

        /// <summary>
        /// Set token in state
        /// </summary>
        /// <param name="name">The name of token</param>
        /// <param name="token">The value of token</param>
        private void CacheToken(string name, TokenResponse token)
        {
            _authTokens[name] = token;
        }

        /// <summary>
        /// Delete token from turn state
        /// </summary>
        /// <param name="name">The name of token</param>
        private void DeleteCachedToken(string name)
        {
            _authTokens.Remove(name);
        }

        /// <summary>
        /// Determines if the user is in the sign in flow.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        /// <returns>The handler name if the user is in sign in flow. Otherwise null.</returns>
        private static string? UserInSignInFlow(ITurnState turnState)
        {
            string? value = turnState.User.GetValue<string>(IS_SIGNED_IN_KEY);

            if (value == string.Empty || value == null)
            {
                return null;
            }

            return value;
        }

        /// <summary>
        /// Update the turn state to indicate the user is in the sign in flow by providing the authorization setting name used.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        /// <param name="handlerName">The connection setting name defined when configuring the authorization options within the application class.</param>
        private static void SetActiveFlow(ITurnState turnState, string handlerName)
        {
            turnState.User.SetValue(IS_SIGNED_IN_KEY, handlerName);
        }

        /// <summary>
        /// Delete the user in sign in flow state from the turn state.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        private static void DeleteActiveFlow(ITurnState turnState)
        {
            if (turnState.User.HasValue(IS_SIGNED_IN_KEY))
            {
                turnState.User.DeleteValue(IS_SIGNED_IN_KEY);
            }
        }

        private static void SetSingInContinuationActivity(ITurnState turnState, IActivity activity)
        {
            turnState.User.SetValue(SIGNIN_ACTIVITY_KEY, activity);
        }

        private static IActivity DeleteSingInContinuationActivity(ITurnState turnState)
        {
            var activity = turnState.User.GetValue<IActivity>(SIGNIN_ACTIVITY_KEY);
            if (activity != null)
            {
                turnState.User.DeleteValue(SIGNIN_ACTIVITY_KEY);
            }
            return activity;
        }
    }

    class SignInEventValue
    {
        public string HandlerName { get; set; }
        public SignInResponse Response { get; set; }
    }
}
