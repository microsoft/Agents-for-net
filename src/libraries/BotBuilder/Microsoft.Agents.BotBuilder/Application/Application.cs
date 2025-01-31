﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Application.AdaptiveCards;
using Microsoft.Agents.BotBuilder.Application.Route;
using Microsoft.Agents.BotBuilder.Application.State;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Application
{
    /// <summary>
    /// Application class for routing and processing incoming requests.
    /// </summary>
    /// <typeparam name="TState">Type of the turnState. This allows for strongly typed access to the turn turnState.</typeparam>
    public class Application<TState> : IBot
        where TState : TurnState, new()
    {
        //TODO
        //private readonly AuthenticationManager<TState>? _authentication;

        private readonly int _typingTimerDelay = 1000;
        private TypingTimer? _typingTimer;

        private readonly ConcurrentQueue<Route<TState>> _invokeRoutes;
        private readonly ConcurrentQueue<Route<TState>> _routes;

        private readonly ConcurrentQueue<TurnEventHandlerAsync<TState>> _beforeTurn;
        private readonly ConcurrentQueue<TurnEventHandlerAsync<TState>> _afterTurn;

        // TODO
        //private readonly SelectorAsync? _startSignIn;

        /// <summary>
        /// Creates a new Application instance.
        /// </summary>
        /// <param name="options">Optional. Options used to configure the application.</param>
        /// <param name="state"></param>
        public Application(ApplicationOptions<TState> options)
        {
            Verify.ParamNotNull(options);

            Options = options;

            if (Options.TurnStateFactory == null)
            {
                this.Options.TurnStateFactory = () => new TState();
            }

            AdaptiveCards = new AdaptiveCards<TState>(this);

            _routes = new ConcurrentQueue<Route<TState>>();
            _invokeRoutes = new ConcurrentQueue<Route<TState>>();
            _beforeTurn = new ConcurrentQueue<TurnEventHandlerAsync<TState>>();
            _afterTurn = new ConcurrentQueue<TurnEventHandlerAsync<TState>>();

            //TODO
            /*
            if (options.Authentication != null)
            {
                _authentication = new AuthenticationManager<TState>(this, options.Authentication, options.Storage);

                if (options.Authentication.AutoSignIn != null)
                {
                    _startSignIn = options.Authentication.AutoSignIn;
                }
                else
                {
                    _startSignIn = (context, cancellationToken) => Task.FromResult(true);
                }
            }
            */
        }

        /// <summary>
        /// Fluent interface for accessing Adaptive Card specific features.
        /// </summary>
        public AdaptiveCards<TState> AdaptiveCards { get; }

        //TODO
        /*
        /// <summary>
        /// Accessing authentication specific features.
        /// </summary>
        public AuthenticationManager<TState> Authentication
        {

            get
            {
                if (_authentication == null)
                {
                    throw new ArgumentException("The Application.Authentication property is unavailable because no authentication options were configured.");
                }

                return _authentication;
            }
        }
        */

        /// <summary>
        /// The application's configured options.
        /// </summary>
        public ApplicationOptions<TState> Options { get; }

        /// <summary>
        /// Adds a new route to the application.
        /// 
        /// Developers won't typically need to call this method directly as it's used internally by all
        /// of the fluent interfaces to register routes for their specific activity types.
        /// 
        /// Routes will be matched in the order they're added to the application. The first selector to
        /// return `true` when an activity is received will have its handler called.
        ///
        /// Invoke-based activities receive special treatment and are matched separately as they typically
        /// have shorter execution timeouts.
        /// </summary>
        /// <param name="selector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="isInvokeRoute">Boolean indicating if the RouteSelectorAsync is for an activity that uses "invoke" which require special handling. Defaults to `false`.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> AddRoute(RouteSelectorAsync selector, RouteHandler<TState> handler, bool isInvokeRoute = false)
        {
            Verify.ParamNotNull(selector);
            Verify.ParamNotNull(handler);
            Route<TState> route = new(selector, handler, isInvokeRoute);
            if (isInvokeRoute)
            {
                _invokeRoutes.Enqueue(route);
            }
            else
            {
                _routes.Enqueue(route);
            }
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="type">Name of the activity type to match.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActivity(string type, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(type);
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult(string.Equals(type, context.Activity?.Type, StringComparison.OrdinalIgnoreCase));
            OnActivity(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="typePattern">Regular expression to match against the incoming activity type.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActivity(Regex typePattern, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(typePattern);
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult(context.Activity?.Type != null && typePattern.IsMatch(context.Activity?.Type));
            OnActivity(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActivity(RouteSelectorAsync routeSelector, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(routeSelector);
            Verify.ParamNotNull(handler);
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActivity(MultipleRouteSelector routeSelectors, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(routeSelectors);
            Verify.ParamNotNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string type in routeSelectors.Strings)
                {
                    OnActivity(type, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex typePattern in routeSelectors.Regexes)
                {
                    OnActivity(typePattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActivity(routeSelector, handler);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="ConversationUpdateEvents"/>.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public virtual Application<TState> OnConversationUpdate(string conversationUpdateEvent, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(conversationUpdateEvent);
            Verify.ParamNotNull(handler);

            RouteSelectorAsync routeSelector;
            switch (conversationUpdateEvent)
            {
                case ConversationUpdateEvents.MembersAdded:
                {
                    routeSelector = (context, _) => Task.FromResult
                    (
                        string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                        && context.Activity?.MembersAdded != null
                        && context.Activity.MembersAdded.Count > 0
                    );
                    break;
                }
                case ConversationUpdateEvents.MembersRemoved:
                {
                    routeSelector = (context, _) => Task.FromResult
                    (
                        string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                        && context.Activity?.MembersRemoved != null
                        && context.Activity.MembersRemoved.Count > 0
                    );
                    break;
                }
                default:
                {
                    routeSelector = (context, _) => Task.FromResult
                    (
                        string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    );
                    break;
                }
            }
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvents">Name of the conversation update events to handle, can use <see cref="ConversationUpdateEvents"/> as array item.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnConversationUpdate(string[] conversationUpdateEvents, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(conversationUpdateEvents);
            Verify.ParamNotNull(handler);
            foreach (string conversationUpdateEvent in conversationUpdateEvents)
            {
                OnConversationUpdate(conversationUpdateEvent, handler);
            }
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage("/reset", (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="text">Substring of the incoming message text.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnMessage(string text, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(text);
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && context.Activity.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                );
            OnMessage(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage(new Regex("reset"), (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="textPattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnMessage(Regex textPattern, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(textPattern);
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && textPattern.IsMatch(context.Activity.Text)
                );
            OnMessage(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnMessage(RouteSelectorAsync routeSelector, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(routeSelector);
            Verify.ParamNotNull(handler);
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnMessage(MultipleRouteSelector routeSelectors, RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(routeSelectors);
            Verify.ParamNotNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string text in routeSelectors.Strings)
                {
                    OnMessage(text, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex textPattern in routeSelectors.Regexes)
                {
                    OnMessage(textPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnMessage(routeSelector, handler);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles message reactions added events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnMessageReactionsAdded(RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.MessageReaction, StringComparison.OrdinalIgnoreCase)
                && context.Activity?.ReactionsAdded != null
                && context.Activity.ReactionsAdded.Count > 0
            );
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles message reactions removed events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnMessageReactionsRemoved(RouteHandler<TState> handler)
        {
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.MessageReaction, StringComparison.OrdinalIgnoreCase)
                && context.Activity?.ReactionsRemoved != null
                && context.Activity.ReactionsRemoved.Count > 0
            );
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles handoff activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnHandoff(HandoffHandler<TState> handler)
        {
            Verify.ParamNotNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "handoff/action")
            );
            RouteHandler<TState> routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                string token = turnContext.Activity.Value.GetType().GetProperty("Continuation").GetValue(turnContext.Activity.Value) as string ?? "";
                await handler(turnContext, turnState, token, cancellationToken);

                // Check to see if an invoke response has already been added
                if (turnContext.TurnState.Get<object>(ChannelAdapter.InvokeResponseKey) == null)
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity();
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute before the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to continue execution of the current turn. Handler returning false
        /// prevents the turn from running, but the bots state is still saved, which lets you
        /// track the reason why the turn was not processed. It also means you can use this as
        /// a way to call into the dialog system. For example, you could use the OAuthPrompt to sign the
        /// user in before allowing the AI system to run.
        /// </summary>
        /// <param name="handler">Function to call before turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnBeforeTurn(TurnEventHandlerAsync<TState> handler)
        {
            Verify.ParamNotNull(handler);
            _beforeTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute after the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to finish execution of the current turn. Handler returning false
        /// prevents the bots state from being saved.
        /// </summary>
        /// <param name="handler">Function to call after turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnAfterTurn(TurnEventHandlerAsync<TState> handler)
        {
            Verify.ParamNotNull(handler);
            _afterTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Called by the adapter (for example, a <see cref="CloudAdapter"/>)
        /// at runtime in order to process an inbound <see cref="Activity"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnContext.Activity == null)
            {
                throw new ArgumentException($"{nameof(turnContext)} must have non-null Activity.");
            }

            if (turnContext.Activity.Type == null)
            {
                throw new ArgumentException($"{nameof(turnContext)}.Activity must have non-null Type.");
            }

            await _OnTurnAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Manually start a timer to periodically send "typing" activities.
        /// </summary>
        /// <remarks>
        /// The timer waits 1000ms to send its initial "typing" activity and then send an additional
        /// "typing" activity every 1000ms.The timer will automatically end once an outgoing activity
        /// has been sent. If the timer is already running or the current activity is not a "message"
        /// the call is ignored.
        /// </remarks>
        /// <param name="turnContext">The turn context.</param>
        public void StartTypingTimer(ITurnContext turnContext)
        {
            if (turnContext.Activity.Type != ActivityTypes.Message)
            {
                return;
            }

            if (_typingTimer == null)
            {
                _typingTimer = new TypingTimer(_typingTimerDelay);
            }

            if (!_typingTimer.IsRunning())
            {
                _typingTimer.Start(turnContext);
            }

        }

        /// <summary>
        /// Manually stop the typing timer.
        /// </summary>
        /// <remarks>
        /// If the timer isn't running nothing happens.
        /// </remarks>
        public void StopTypingTimer()
        {
            _typingTimer?.Dispose();
            _typingTimer = null;
        }

        /// <summary>
        /// Internal method to wrap the logic of handling a bot turn.
        /// </summary>
        private async Task _OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            try
            {
                // Start typing timer if configured
                if (Options.StartTypingTimer)
                {
                    StartTypingTimer(turnContext);
                };

                // Remove @mentions
                if (Options.RemoveRecipientMention && ActivityTypes.Message.Equals(turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    turnContext.Activity.Text = turnContext.Activity.RemoveRecipientMention();
                }

                // Load turn state
                TState turnState = Options.TurnStateFactory!();
                IStorage? storage = Options.Storage;

                await turnState!.LoadStateAsync(storage, turnContext);

                //TODO
                /*
                // If user is in sign in flow, return the authentication setting name
                string? settingName = AuthUtilities.UserInSignInFlow(turnState);
                bool shouldStartSignIn = _startSignIn != null && await _startSignIn(turnContext, cancellationToken);

                // Sign the user in
                if (this._authentication != null && (shouldStartSignIn || settingName != null))
                {
                    if (settingName == null)
                    {
                        settingName = this._authentication.Default;
                    }

                    // Sets the setting name in the context object. It is used in `signIn/verifyState` & `signIn/tokenExchange` route selectors.
                    BotAuthenticationBase<TState>.SetSettingNameInContextActivityValue(turnContext, settingName);

                    SignInResponse response = await this._authentication.SignUserInAsync(turnContext, turnState, settingName);

                    if (response.Status == SignInStatus.Complete)
                    {
                        AuthUtilities.DeleteUserInSignInFlow(turnState);
                    }

                    if (response.Status == SignInStatus.Pending)
                    {
                        // Requires user action, save state and stop processing current activity
                        await turnState.SaveStateAsync(turnContext, storage);
                        return;
                    }

                    if (response.Status == SignInStatus.Error && response.Cause != AuthExceptionReason.InvalidActivity)
                    {
                        AuthUtilities.DeleteUserInSignInFlow(turnState);
                        throw new TeamsAIException("An error occurred when trying to sign in.", response.Error!);
                    }
                }
                */

                // Call before turn handler
                foreach (TurnEventHandlerAsync<TState> beforeTurnHandler in _beforeTurn)
                {
                    if (!await beforeTurnHandler(turnContext, turnState, cancellationToken))
                    {
                        // Save turn state
                        // - This lets the bot keep track of why it ended the previous turn. It also
                        //   allows the dialog system to be used before the AI system is called.
                        await turnState!.SaveStateAsync(turnContext, storage);

                        return;
                    }
                }

                // Populate {{$temp.input}}
                if ((turnState.Temp.Input == null || turnState.Temp.Input.Length == 0) && turnContext.Activity.Text != null)
                {
                    // Use the received activity text
                    turnState.Temp.Input = turnContext.Activity.Text;
                }

                bool eventHandlerCalled = false;

                // Run any RouteSelectors in this._invokeRoutes first if the incoming Teams activity.type is "Invoke".
                // Invoke Activities from Teams need to be responded to in less than 5 seconds.
                if (ActivityTypes.Invoke.Equals(turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Route<TState> route in _invokeRoutes)
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            await route.Handler(turnContext, turnState, cancellationToken);
                            eventHandlerCalled = true;
                            break;
                        }
                    }
                }

                // All other ActivityTypes and any unhandled Invokes are run through the remaining routes.
                if (!eventHandlerCalled)
                {
                    foreach (Route<TState> route in _routes)
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            await route.Handler(turnContext, turnState, cancellationToken);
                            eventHandlerCalled = true;
                            break;
                        }
                    }
                }

                // Call after turn handler
                foreach (TurnEventHandlerAsync<TState> afterTurnHandler in _afterTurn)
                {
                    if (!await afterTurnHandler(turnContext, turnState, cancellationToken))
                    {
                        return;
                    }
                }
                await turnState!.SaveStateAsync(turnContext, storage);
            }
            finally
            {
                // Stop the timer if configured
                StopTypingTimer();
            }
        }

        //TODO
        /*
        /// <summary>
        /// If the user is signed in, get the access token. If not, triggers the sign in flow for the provided authentication setting name
        /// and returns.In this case, the bot should end the turn until the sign in flow is completed.
        /// </summary>
        /// <remarks>
        /// Use this method to get the access token for a user that is signed in to the bot.
        /// If the user isn't signed in, this method starts the sign-in flow.
        /// The bot should end the turn in this case until the sign-in flow completes and the user is signed in.
        /// </remarks>
        /// <param name="turnContext"> The turn context.</param>
        /// <param name="turnState">The turn state.</param>
        /// <param name="settingName">The name of the authentication setting.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The access token for the user if they are signed, otherwise null.</returns>
        /// <exception cref="TeamsAIException"></exception>
        public async Task<string?> GetTokenOrStartSignInAsync(ITurnContext turnContext, TState turnState, string settingName, CancellationToken cancellationToken = default)
        {
            string? token = await Authentication.Get(settingName).IsUserSignedInAsync(turnContext, cancellationToken);

            if (token != null)
            {
                AuthUtilities.SetTokenInState(turnState, settingName, token);
                AuthUtilities.DeleteUserInSignInFlow(turnState);
                return token;
            }

            // User is currently not in sign in flow
            if (AuthUtilities.UserInSignInFlow(turnState) == null)
            {
                AuthUtilities.SetUserInSignInFlow(turnState, settingName);
            }
            else
            {
                AuthUtilities.DeleteUserInSignInFlow(turnState);
                throw new TeamsAIException("Invalid sign in flow state. Cannot start sign in when already started");
            }

            SignInResponse response = await Authentication.SignUserInAsync(turnContext, turnState, settingName);

            if (response.Status == SignInStatus.Error)
            {
                string message = response.Error!.ToString();
                if (response.Cause == AuthExceptionReason.InvalidActivity)
                {
                    message = $"User is not signed in and cannot start sign in flow for this activity: {response.Error}";
                }

                throw new TeamsAIException($"Error occured while trying to authenticate user: {message}");
            }

            if (response.Status == SignInStatus.Complete)
            {
                AuthUtilities.DeleteUserInSignInFlow(turnState);
                return turnState.Temp.AuthTokens[settingName];
            }

            // response.Status == SignInStatus.Pending
            return null;
        }
        */
    }
}
