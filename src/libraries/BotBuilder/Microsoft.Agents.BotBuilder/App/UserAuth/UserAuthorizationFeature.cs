// Copyright (c) Microsoft Corporation. All rights reserved.
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
    public delegate Task AuthorizationSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken);
    public delegate Task AuthorizationFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken);

    /// <summary>
    /// UserAuthorization supports and extensible number of OAuth flows.
    /// 
    /// Auto Sign In:
    /// If enabled in <see cref="UserAuthorizationOptions"/>, sign in starts automatically after the first Message the user sends.  When
    /// the sign in is complete, the turn continues with the original message. On failure, <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/>
    /// is called.
    /// 
    /// Manual Sign In:
    /// <see cref="SignInUserAsync"/> is used to get a cached token or start the sign in.  In either case, the
    /// <see cref="OnUserSignInSuccess(Func{ITurnContext, ITurnState, string, string, CancellationToken, Task})"/> and
    /// <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/> should
    /// be set to handle continuation.  That is, after calling SignInUserAsync, the turn should be considered complete,
    /// and performing actions after that could be confusing.  i.e., Perform additional turn activity in OnUserSignInSuccess.
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
        private readonly Dictionary<string, string> _authTokens = [];

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
        public string GetTurnToken(string handlerName)
        {
            return _authTokens.TryGetValue(handlerName, out var token) ? token : default(string);
        }

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
        /// <exception cref="InvalidOperationException">If a flow is already active.</exception>
        public async Task SignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName, string exchangeConnection = null, IList<string> exchangeScopes = null, CancellationToken cancellationToken = default)
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
            var existingCachedToken = GetTurnToken(handlerName);
            if (existingCachedToken != null)
            {
                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, handlerName, existingCachedToken, turnContext.Activity, cancellationToken).ConfigureAwait(false);
                }
            }

            SignInResponse response = await _dispatcher.SignUserInAsync(turnContext, handlerName, exchangeConnection, exchangeScopes, cancellationToken).ConfigureAwait(false);

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
                    Value = new SignInEventValue() { HandlerName = handlerName, PassedOBOConnectionName = exchangeConnection, PassedOBOScopes = exchangeScopes, InitiatingActivity = turnContext.Activity }
                    
                };
                continuationActivity.ApplyConversationReference(turnContext.Activity.GetConversationReference(), isIncoming: true);

                SetSignInContinuationActivity(turnState, continuationActivity);

                return;
            }

            if (response.Status == SignInStatus.Error)
            {
                if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(turnContext, turnState, handlerName, response, turnContext.Activity, cancellationToken).ConfigureAwait(false);
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
                CacheToken(handlerName, response.Token);

                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, handlerName, response.Token, turnContext.Activity, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task SignOutUserAsync(ITurnContext turnContext, ITurnState turnState, string? flowName = null, CancellationToken cancellationToken = default)
        {
            var flow = flowName ?? Default;
            await _dispatcher.SignOutUserAsync(turnContext, flow, cancellationToken).ConfigureAwait(false);
            DeleteCachedToken(flow);
        }

        /// <summary>
        /// Clears all UserAuth state for the user.  This includes cached tokens, and flow related state.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="handlerName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ResetStateAsync(ITurnContext turnContext, ITurnState turnState, string handlerName = null, CancellationToken cancellationToken = default)
        {
            handlerName ??= Default;
            
            await SignOutUserAsync(turnContext, turnState, handlerName, cancellationToken).ConfigureAwait(false);

            await _dispatcher.ResetStateAsync(turnContext, handlerName, cancellationToken).ConfigureAwait(false);
            DeleteActiveFlow(turnState);
            DeleteSignInContinuationActivity(turnState);
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
        /// When complete, the token is cached and can be access via <see cref="GetTurnToken"/>.  For manual sign in, the <see cref="OnUserSignInSuccess"/> or 
        /// <see cref="OnUserSignInFailure"/> are called at completion.
        /// </remarks>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="handlerName">The name of the handler defined in <see cref="UserAuthorizationOptions"/></param>
        /// <param name="cancellationToken"></param>
        /// <returns>false indicates the sign in is not complete.</returns>
        internal async Task<bool> StartOrContinueSignInUserAsync(ITurnContext turnContext, ITurnState turnState, string handlerName = null, CancellationToken cancellationToken = default)
        {
            // If a flow is active, continue that.
            string? activeFlowName = UserInSignInFlow(turnState);
            bool flowContinuation = activeFlowName != null;
            bool autoSignIn = _startSignIn != null && await _startSignIn(turnContext, cancellationToken);

            if (autoSignIn || flowContinuation)
            {
                // Auth flow hasn't start yet.
                activeFlowName ??= handlerName ?? Default;

                string exchangeConnection = null;
                IList<string> exchangeScopes = null;

                var signInContinuation = GetSignInContinuationActivity(turnState);
                if (IsSignInCompletionEvent(signInContinuation))
                {
                    var signInEvent = ProtocolJsonSerializer.ToObject<SignInEventValue>(signInContinuation.Value);
                    exchangeConnection = signInEvent.PassedOBOConnectionName;
                    exchangeScopes = signInEvent.PassedOBOScopes;
                }

                // Get token or start flow for specified flow.
                SignInResponse response = await _dispatcher.SignUserInAsync(
                    turnContext, 
                    activeFlowName, 
                    exchangeConnection: exchangeConnection, 
                    exchangeScopes: exchangeScopes, 
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (response.Status == SignInStatus.Pending)
                {
                    if (!flowContinuation)
                    {
                        // Bank the incoming Activity so it can be executed after sign in is complete.
                        SetSignInContinuationActivity(turnState, turnContext.Activity);

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
                    DeleteSignInContinuationActivity(turnState);
                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Handle manual signin error callback
                    if (IsSignInCompletionEvent(signInContinuation))
                    {
                        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                        // This will execute OnUserSignInFailure in a new TurnContext.  This is for manual sign in only.
                        // This could be optimized to execute the OnUserSignInFailure directly if the we're not currently
                        // handling an Invoke.
                        var signInEvent = ProtocolJsonSerializer.ToObject<SignInEventValue>(signInContinuation.Value);
                        signInEvent.HandlerName = activeFlowName;
                        signInEvent.Response = response;
                        signInContinuation.Value = signInEvent;

                        await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    if (_userSignInFailureHandler != null)
                    {
                        await _userSignInFailureHandler(turnContext, turnState, activeFlowName, response, signInContinuation, cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    await turnContext.SendActivitiesAsync(
                        _options.SignInFailedMessage == null ? [MessageFactory.Text("SignIn Failed")] : _options.SignInFailedMessage(activeFlowName, response), 
                        cancellationToken).ConfigureAwait(false);
                    return false;
                }

                if (response.Status == SignInStatus.Complete)
                {
                    DeleteActiveFlow(turnState);
                    CacheToken(activeFlowName, response.Token);

                    if (signInContinuation != null)
                    {
                        DeleteSignInContinuationActivity(turnState);

                        if (IsSignInCompletionEvent(signInContinuation))
                        {
                            // Continue a manual sign in completion.
                            // Since we could be handling an Invoke in this turn, we need to continue the conversation in a different
                            // turn with the SignInCompletion Event.  This is because Teams has expectation for Invoke response times
                            // an a the OnSignInSuccess/Fail handling by the bot could exceed that.  Also, this is all executing prior
                            // to other Application routes having been run (ex. before/after turn).
                            // This is handled by the route added in AddManualSignInCompletionHandler().
                            //
                            // Note:  This should be optimized to only do this if the current TurnContext.Activity is Invoke.  Otherwise,
                            // the OnUserSignInSuccess can be called directly?
                            var signInEvent = ProtocolJsonSerializer.ToObject<SignInEventValue>(signInContinuation.Value);
                            signInContinuation.Value = new SignInEventValue() { HandlerName = activeFlowName, Response = response, InitiatingActivity = signInEvent.InitiatingActivity };
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
            }

            // Sign in is complete (or never started if Auto Sign in is false)
            // AgentApplication will perform normal ITurnContext.Activity routing to bot.
            return true;
        }

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
                var signInCompletion = ProtocolJsonSerializer.ToObject<SignInEventValue>(turnContext.Activity.Value);
                if (signInCompletion.Response.Status == SignInStatus.Complete && _userSignInSuccessHandler != null)
                {
                    CacheToken(signInCompletion.HandlerName, signInCompletion.Response.Token);
                    await _userSignInSuccessHandler(
                        turnContext, 
                        turnState, 
                        signInCompletion.HandlerName, 
                        signInCompletion.Response.Token,
                        signInCompletion.InitiatingActivity, 
                        cancellationToken).ConfigureAwait(false);
                }
                else if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(
                        turnContext, 
                        turnState, 
                        signInCompletion.HandlerName, 
                        signInCompletion.Response, 
                        signInCompletion.InitiatingActivity,
                        cancellationToken).ConfigureAwait(false);
                }
            }

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
        private void CacheToken(string name, string token)
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

        // Sign In continuation is the Activity that will be processed when the flow is complete.
        private static void SetSignInContinuationActivity(ITurnState turnState, IActivity activity)
        {
            turnState.User.SetValue(SIGNIN_ACTIVITY_KEY, activity);
        }

        private static IActivity GetSignInContinuationActivity(ITurnState turnState)
        {
            return turnState.User.GetValue<IActivity>(SIGNIN_ACTIVITY_KEY);
        }

        private static IActivity DeleteSignInContinuationActivity(ITurnState turnState)
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
        public IActivity InitiatingActivity { get; set; }
        public string PassedOBOConnectionName { get; set; }
        public IList<string> PassedOBOScopes { get; set; }
    }
}
