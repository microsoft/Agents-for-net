// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.BotBuilder.UserAuth;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.BotBuilder.App.UserAuth
{
    /// <summary>
    /// User Authentication supports and extensible number of OAuth flows.
    /// 
    /// Auto Sign In:
    /// If enabled in UserAuthenticationOptions, sign in starts automatically after the first Message the user sends.  When
    /// the sign in is complete, the turn continues with the original message. On failure, an optional message is sent, otherwise
    /// and exception thrown.
    /// 
    /// Manual Sign In:
    /// <see cref="GetTokenOrStartSignInAsync"/> is used to get a cached token or start the sign in.  In either case, the
    /// <see cref="OnUserSignInSuccess(Func{ITurnContext, ITurnState, string, TokenResponse, CancellationToken, Task})"/> and
    /// <see cref="OnUserSignInFailure(Func{ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task})"/> should
    /// be set to handle continuation.  That is, after calling GetTokenOrStartSignInAsync, the turn should be considered complete,
    /// and performing actions after that could be confusing.
    /// </summary>
    public class UserAuthenticationFeature
    {
        private readonly SelectorAsync? _startSignIn;
        private const string IS_SIGNED_IN_KEY = "__InSignInFlow__";
        private const string SIGNIN_ACTIVITY_KEY = "__SignInFlowActivity__";
        private const string SignInCompletionEventName = "application/vnd.microsoft.SignInCompletion";
        private readonly IUserAuthenticationDispatcher _dispatcher;
        private readonly UserAuthenticationOptions _options;
        private readonly AgentApplication _app;

        /// <summary>
        /// Callback when user sign in success
        /// </summary>
        private Func<ITurnContext, ITurnState, string, TokenResponse, CancellationToken, Task>? _userSignInSuccessHandler;

        /// <summary>
        /// Callback when user sign in fail
        /// </summary>
        private Func<ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task>? _userSignInFailureHandler;

        public string Default { get; private set; }

        public UserAuthenticationFeature(AgentApplication app, UserAuthenticationOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _dispatcher = new UserAuthenticationDispatcher([.. _options.Handlers]);
            _app = app ?? throw new ArgumentNullException(nameof(app));

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
        /// This starts the sign in flow.
        /// </summary>
        /// <param name="turnContext"></param>
        /// <param name="turnState"></param>
        /// <param name="flowName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<bool> SignUserInAsync(ITurnContext turnContext, ITurnState turnState, string flowName = null, CancellationToken cancellationToken = default)
        {
            // NOTE:  This is marked internal for now.  It could be this can be consolidated with GetTokenOrStartSignInAsync.

            // If a flow is active, continue that.
            string? activeFlowName = UserInSignInFlow(turnState);
            bool shouldStartSignIn = _startSignIn != null && await _startSignIn(turnContext, cancellationToken);

            if (shouldStartSignIn || activeFlowName != null)
            {
                if (activeFlowName == null)
                {
                    // Auth flow hasn't start yet.
                    activeFlowName = flowName ?? Default;
                }

                // Get token or start flow for specified flow.
                SignInResponse response = await _dispatcher.SignUserInAsync(turnContext, activeFlowName, cancellationToken).ConfigureAwait(false);

                if (response.Status == SignInStatus.Pending)
                {
                    // Bank the Activity so it can be executed after sign in is complete.
                    SetSingInContinuationActivity(turnContext, turnState);

                    // Requires user action, save state and stop processing current activity.  Done with this turn.
                    SetActiveFlow(turnState, activeFlowName);
                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                    return true;
                }

                // An InvalidActivity is expected, but anything else is a hard error and the flow is cancelled.
                if (response.Status == SignInStatus.Error && response.Cause != AuthExceptionReason.InvalidActivity)
                {
                    // Clear user auth state
                    await _dispatcher.ResetStateAsync(turnContext, activeFlowName, cancellationToken).ConfigureAwait(false);
                    DeleteActiveFlow(turnState);

                    var signInContinuation = DeleteSingInContinuationActivity(turnState);
                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (IsSignInCompletionEvent(signInContinuation))
                    {
                        signInContinuation.Value = new SignInEventValue() { FlowName = flowName, Response = response };
                        await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                        await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                        return true;
                    }

                    if (_options.SignInFailedMessage == null)
                    {
                        throw response.Error;
                    }

                    await turnContext.SendActivitiesAsync(_options.SignInFailedMessage(activeFlowName, response), cancellationToken).ConfigureAwait(false);
                    return true;
                }

                if (response.Status == SignInStatus.Complete)
                {
                    DeleteActiveFlow(turnState);
                    SetTokenInState(turnState, activeFlowName, response.TokenResponse.Token);
                    // TODO: should probably call "_authentication.Get(flowName).ResetState?", but is this safe?

                    var signInContinuation = DeleteSingInContinuationActivity(turnState);
                    if (signInContinuation != null)
                    {
                        if (IsSignInCompletionEvent(signInContinuation))
                        {
                            // This is to continue a manual signin completion.
                            // Since we could be handling an Invoke in this turn, and ITurnContext.Activity is readonly,
                            // we need to continue the conversation in a different turn with the SignInCompletion Event.
                            // This is handled by the route added in AddManualSignInCompletionHandler().
                            signInContinuation.Value = new SignInEventValue() { FlowName = activeFlowName, Response = response };
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                            return true;
                        }

                        // If the current activity matches the one used to trigger sign in, then
                        // this is because the user received a token that didn't involve a multi-turn
                        // flow.  No further action needed.
                        if (!ProtocolJsonSerializer.Equals(signInContinuation, turnContext.Activity))
                        {
                            // Since we could be handling an Invoke in this turn, and ITurnContext.Activity is readonly,
                            // we need to continue the conversation in a different turn with the original Activity that triggered sign in.
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            await _app.Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInContinuation, _app, cancellationToken).ConfigureAwait(false);
                            return true;
                        }
                    }
                }

                // If we got this far, fall through to normal Activity route handling.
                if (_options.CompletedMessage != null)
                {
                    await turnContext.SendActivitiesAsync(_options.CompletedMessage(activeFlowName, response), cancellationToken).ConfigureAwait(false);
                }
            }

            return false;
        }

        public async Task SignOutUserAsync(ITurnContext turnContext, ITurnState turnState, string? flowName = null, CancellationToken cancellationToken = default)
        {
            var flow = flowName ?? Default;
            await _dispatcher.SignOutUserAsync(turnContext, flow, cancellationToken).ConfigureAwait(false);
            DeleteTokenFromState(turnState, flow);
        }

        public async Task ResetStateAsync(ITurnContext turnContext, ITurnState turnState, string flowName = null, CancellationToken cancellationToken = default)
        {
            await _dispatcher.ResetStateAsync(turnContext, flowName ?? Default, cancellationToken).ConfigureAwait(false);
            DeleteActiveFlow(turnState);
            DeleteTokenFromState(turnState, flowName ?? _options.Default);
        }


        /// <summary>
        /// The handler function is called when the user has successfully signed in
        /// </summary>
        /// <remarks>
        /// This is only used for manual user authentication.  The Auto Sign In will continue the turn with the original user message.
        /// </remarks>
        /// <param name="handler">The handler function to call when the user has successfully signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public void OnUserSignInSuccess(Func<ITurnContext, ITurnState, string, TokenResponse, CancellationToken, Task> handler)
        {
            _userSignInSuccessHandler = handler;
        }

        /// <summary>
        /// The handler function is called when the user sign in flow fails
        /// </summary>
        /// <remarks>
        /// This is only used for manual user authentication.  The Auto Sign In will end the turn with and optional error message
        /// or exception.
        /// </remarks>
        /// <param name="handler">The handler function to call when the user failed to signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public void OnUserSignInFailure(Func<ITurnContext, ITurnState, string, SignInResponse, CancellationToken, Task> handler)
        {
            _userSignInFailureHandler = handler;
        }

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
                    SetTokenInState(turnState, signInCompletion.FlowName, signInCompletion.Response.TokenResponse.Token);
                    await _userSignInSuccessHandler(turnContext, turnState, signInCompletion.FlowName, signInCompletion.Response.TokenResponse, cancellationToken).ConfigureAwait(false);
                }
                else if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(turnContext, turnState, signInCompletion.FlowName, signInCompletion.Response, cancellationToken).ConfigureAwait(false);
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
        /// If the user is signed in, get the access token. If not, triggers the sign in flow for the provided user authentication flow name
        /// and returns. In this case, the bot should end the turn until the sign in flow is completed.  In all cases, the completionHandler is
        /// invoked with the SignInResponse.
        /// </summary>
        /// <remarks>
        /// The OnUserSignInSuccess and OnUserSignInFailure should be set prior to using manual sign in.
        /// </remarks>
        /// <param name="turnContext"> The turn context.</param>
        /// <param name="turnState"></param>
        /// <param name="flowName">The name of the authentication setting.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="InvalidOperationException">If a flow is already active.</exception>
        public async Task GetTokenOrStartSignInAsync(ITurnContext turnContext, ITurnState turnState, string flowName, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(turnState);
            ArgumentException.ThrowIfNullOrWhiteSpace(flowName);

            // Only one active flow allowed
            if (!string.IsNullOrEmpty(UserInSignInFlow(turnState)))
            {
                DeleteActiveFlow(turnState);
                throw new InvalidOperationException("Invalid sign in flow state. Cannot start sign in when already started");
            }

            SignInResponse response = await _dispatcher.SignUserInAsync(turnContext, flowName, cancellationToken).ConfigureAwait(false);

            if (response.Status == SignInStatus.Pending)
            {
                SetActiveFlow(turnState, flowName);

                var continuationActivity = new Activity()
                {
                    Type = ActivityTypes.Event,
                    Name = SignInCompletionEventName,
                    ServiceUrl = turnContext.Activity.ServiceUrl,
                    ChannelId = turnContext.Activity.ChannelId,
                    ChannelData = turnContext.Activity.ChannelData,
                };
                continuationActivity.ApplyConversationReference(turnContext.Activity.GetConversationReference(), isIncoming: true);

                // This Activity will be used to trigger the handler added by `OnSignInComplete`.
                // The Activity.Value will be updated in SignUserInAsync when flow is complete/error.
                SetSingInContinuationActivity(turnState, continuationActivity);


                return;
            }

            if (response.Status == SignInStatus.Error)
            {
                if (_userSignInFailureHandler != null)
                {
                    await _userSignInFailureHandler(turnContext, turnState, flowName, response, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    string message = response.Error!.ToString();

                    // TODO: the current activity shouldn't trigger an error in the case of manual signin.  Problem is, the flow impls
                    // currently fail all but specific activities.
                    if (response.Cause == AuthExceptionReason.InvalidActivity)
                    {
                        message = $"User is not signed in and cannot start sign in flow for this activity: {response.Error}";
                    }

                    throw new InvalidOperationException($"Error occurred while trying to authenticate user: {message}, flow: {flowName}");
                }
            }

            // Call the handler immediately if the user was already signed in.
            if (response.Status == SignInStatus.Complete)
            {
                DeleteActiveFlow(turnState);
                SetTokenInState(turnState, flowName, response.TokenResponse.Token);

                // call the handler directly
                if (_userSignInSuccessHandler != null)
                {
                    await _userSignInSuccessHandler(turnContext, turnState, flowName, response.TokenResponse, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Set token in state
        /// </summary>
        /// <param name="state">The turn state</param>
        /// <param name="name">The name of token</param>
        /// <param name="token">The value of token</param>
        private static void SetTokenInState(ITurnState state, string name, string token)
        {
            state.Temp.AuthTokens[name] = token;
        }

        /// <summary>
        /// Delete token from turn state
        /// </summary>
        /// <param name="turnState">The turn state</param>
        /// <param name="name">The name of token</param>
        private static void DeleteTokenFromState(ITurnState turnState, string name)
        {
            if (turnState.Temp.AuthTokens != null && turnState.Temp.AuthTokens.ContainsKey(name))
            {
                turnState.Temp.AuthTokens.Remove(name);
            }
        }

        /// <summary>
        /// Determines if the user is in the sign in flow.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        /// <returns>The connection setting name if the user is in sign in flow. Otherwise null.</returns>
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
        /// Update the turn state to indicate the user is in the sign in flow by providing the authentication setting name used.
        /// </summary>
        /// <param name="turnState">The turn state.</param>
        /// <param name="flowName">The connection setting name defined when configuring the authentication options within the application class.</param>
        private static void SetActiveFlow(ITurnState turnState, string flowName)
        {
            turnState.User.SetValue(IS_SIGNED_IN_KEY, flowName);
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

        private static void SetSingInContinuationActivity(ITurnContext turnContext, ITurnState turnState)
        {
            SetSingInContinuationActivity(turnState, turnContext.Activity);
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
        public string FlowName { get; set; }
        public SignInResponse Response { get; set; }
    }
}
