// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App.AdaptiveCards;
using Microsoft.Agents.BotBuilder.App.UserAuth;
using Microsoft.Agents.BotBuilder.Errors;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.App
{
    public delegate Task AgentApplicationTurnError(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken);

    /// <summary>
    /// Application class for routing and processing incoming requests.
    /// </summary>
    public class AgentApplication : IBot
    {
        private readonly UserAuthorizationFeature _userAuth;
        private readonly int _typingTimerDelay = 1000;
        private TypingTimer? _typingTimer;

        private readonly RouteList _routes;
        private readonly ConcurrentQueue<TurnEventHandler> _beforeTurn;
        private readonly ConcurrentQueue<TurnEventHandler> _afterTurn;
        private readonly ConcurrentQueue<AgentApplicationTurnError> _turnErrorHandlers;

        /// <summary>
        /// Creates a new AgentApplication instance.
        /// </summary>
        /// <param name="options">Optional. Options used to configure the application.</param>
        /// <param name="userAuthFeature"></param>
        /// <param name="state"></param>
        public AgentApplication(AgentApplicationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            Options = options;

            if (Options.TurnStateFactory == null)
            {
                // This defaults to a TurnState with TempState only
                Options.TurnStateFactory = () => new TurnState();
            }

            _routes = new RouteList();
            _beforeTurn = new ConcurrentQueue<TurnEventHandler>();
            _afterTurn = new ConcurrentQueue<TurnEventHandler>();
            _turnErrorHandlers = new ConcurrentQueue<AgentApplicationTurnError>();

            // Application Features

            AdaptiveCards = new AdaptiveCardsFeature(this);

            if (options.UserAuthorization != null)
            {
                _userAuth = new UserAuthorizationFeature(this, options.UserAuthorization);
            }

            ApplyRouteAttributes();
        }

        #region Application Features

        /// <summary>
        /// Fluent interface for accessing Adaptive Card specific features.
        /// </summary>
        public AdaptiveCardsFeature AdaptiveCards { get; }

        /// <summary>
        /// The application's configured options.
        /// </summary>
        public AgentApplicationOptions Options { get; }

        /// <summary>
        /// Accessing user authorization features.
        /// </summary>
        public UserAuthorizationFeature Authorization
        {
            get
            {
                if (_userAuth == null)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationNotConfigured, null);
                }

                return _userAuth;
            }
        }

        #endregion

        #region Route Handling

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
        /// <param name="isInvokeRoute">Boolean indicating if the RouteSelector is for an activity that uses "invoke" which require special handling. Defaults to `false`.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication AddRoute(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(selector);
            ArgumentNullException.ThrowIfNull(handler);

            _routes.AddRoute(selector, handler, isInvokeRoute, rank);
 
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="type">Name of the activity type to match.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(string type, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(type);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _) => Task.FromResult(string.Equals(type, context.Activity?.Type, StringComparison.OrdinalIgnoreCase));
            OnActivity(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="typePattern">Regular expression to match against the incoming activity type.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(Regex typePattern, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(typePattern);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _) => Task.FromResult(context.Activity?.Type != null && typePattern.IsMatch(context.Activity?.Type));
            OnActivity(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(RouteSelector routeSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            AddRoute(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelector selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(MultipleRouteSelector routeSelectors, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string type in routeSelectors.Strings)
                {
                    OnActivity(type, handler, rank: rank);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex typePattern in routeSelectors.Regexes)
                {
                    OnActivity(typePattern, handler, rank: rank);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActivity(routeSelector, handler, rank: rank);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="ConversationUpdateEvents"/>.  If </param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public virtual AgentApplication OnConversationUpdate(string conversationUpdateEvent, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationUpdateEvent);
            ArgumentNullException.ThrowIfNull(handler);

            RouteSelector routeSelector;
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
            AddRoute(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles conversation update events using a custom selector.
        /// </summary>
        /// <param name="conversationUpdateSelector">This will be used in addition the checking for Activity.Type == ActivityTypes.ConversationUpdate.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public virtual AgentApplication OnConversationUpdate(RouteSelector conversationUpdateSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(conversationUpdateSelector);
            ArgumentNullException.ThrowIfNull(handler);

            async Task<bool> wrapper(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return string.Equals(turnContext.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                    && await conversationUpdateSelector(turnContext, cancellationToken);
            }

            AddRoute(wrapper, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvents">Name of the conversation update events to handle, can use <see cref="ConversationUpdateEvents"/> as array item.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnConversationUpdate(string[] conversationUpdateEvents, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(conversationUpdateEvents);
            ArgumentNullException.ThrowIfNull(handler);
            foreach (string conversationUpdateEvent in conversationUpdateEvents)
            {
                OnConversationUpdate(conversationUpdateEvent, handler, rank);
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
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(string text, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && context.Activity.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                );
            OnMessage(routeSelector, handler, rank: rank);
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
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(Regex textPattern, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(textPattern);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && textPattern.IsMatch(context.Activity.Text)
                );
            OnMessage(routeSelector, handler, rank: rank);
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
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(RouteSelector routeSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            AddRoute(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelector selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(MultipleRouteSelector routeSelectors, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
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
                    OnMessage(textPattern, handler, rank: rank);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnMessage(routeSelector, handler, rank: rank);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles incoming Event with a specific Name.
        /// </summary>
        /// <param name="eventName">Substring of the incoming message text.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(string eventName, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
            ArgumentNullException.ThrowIfNull(handler);
            Task<bool> routeSelector(ITurnContext context, CancellationToken _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Event, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Name != null
                    && context.Activity.Text.IndexOf(eventName, StringComparison.OrdinalIgnoreCase) >= 0
                );
            OnEvent(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles incoming Events matching a Name pattern.
        /// </summary>
        /// <param name="namePattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(Regex namePattern, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(namePattern);
            ArgumentNullException.ThrowIfNull(handler);
            Task<bool> routeSelector(ITurnContext context, CancellationToken _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && namePattern.IsMatch(context.Activity.Text)
                );
            OnEvent(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles incoming Events.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(RouteSelector routeSelector, RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            AddRoute(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles message reactions added events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessageReactionsAdded(RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.MessageReaction, StringComparison.OrdinalIgnoreCase)
                && context.Activity?.ReactionsAdded != null
                && context.Activity.ReactionsAdded.Count > 0
            );
            AddRoute(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles message reactions removed events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessageReactionsRemoved(RouteHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.MessageReaction, StringComparison.OrdinalIgnoreCase)
                && context.Activity?.ReactionsRemoved != null
                && context.Activity.ReactionsRemoved.Count > 0
            );
            AddRoute(routeSelector, handler, rank: rank);
            return this;
        }

        /// <summary>
        /// Handles handoff activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnHandoff(HandoffHandler handler, ushort rank = RouteRank.Unspecified)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelector routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "handoff/action")
            );
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                string token = turnContext.Activity.Value.GetType().GetProperty("Continuation").GetValue(turnContext.Activity.Value) as string ?? "";
                await handler(turnContext, turnState, token, cancellationToken);

                Activity activity = ActivityUtilities.CreateInvokeResponseActivity();
                await turnContext.SendActivityAsync(activity, cancellationToken);
            };
            AddRoute(routeSelector, routeHandler, isInvokeRoute: true, rank: rank);
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
        public AgentApplication OnBeforeTurn(TurnEventHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
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
        public AgentApplication OnAfterTurn(TurnEventHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _afterTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Allows the AgentApplication to provide error handling without having to change the Adapter.OnTurnError.  This
        /// is beneficial since the application has more context.
        /// </summary>
        /// <remarks>
        /// Exceptions here will bubble-up to Adapter.OnTurnError.  Since it isn't know where in the turn the exception
        /// was thrown, it is possible that OnAfterTurn handlers, and ITurnState saving has NOT happened.
        /// </remarks>
        public AgentApplication OnTurnError(AgentApplicationTurnError handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _turnErrorHandlers.Enqueue(handler);
            return this;
        }

        #endregion

        #region ShowTyping
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

        #endregion

        #region Turn Handling

        /// <summary>
        /// Called by the adapter (for example, a <see cref="CloudAdapter"/>)
        /// at runtime in order to process an inbound <see cref="Activity"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(turnContext.Activity);

            try
            {
                // Start typing timer if configured
                if (Options.StartTypingTimer)
                {
                    StartTypingTimer(turnContext);
                };

                // Handle @mentions
                if (ActivityTypes.Message.Equals(turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    if (Options.NormalizeMentions)
                    {
                        turnContext.Activity.NormalizeMentions(Options.RemoveRecipientMention);
                    }
                    else if (Options.RemoveRecipientMention)
                    {
                        turnContext.Activity.Text = turnContext.Activity.RemoveRecipientMention();
                    }
                }

                // Load turn state
                ITurnState turnState = Options.TurnStateFactory!();
                await turnState!.LoadStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                try
                {
                    // Handle user auth
                    if (_userAuth != null)
                    {
                        var signInComplete = await _userAuth.StartOrContinueSignInUserAsync(turnContext, turnState, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (!signInComplete)
                        {
                            return;
                        }
                    }

                    // Call before turn handler
                    foreach (TurnEventHandler beforeTurnHandler in _beforeTurn)
                    {
                        if (!await beforeTurnHandler(turnContext, turnState, cancellationToken))
                        {
                            // Save turn state
                            // - This lets the bot keep track of why it ended the previous turn. It also
                            //   allows the dialog system to be used before the AI system is called.
                            await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                            return;
                        }
                    }

                    // Download any input files
                    IList<IInputFileDownloader>? fileDownloaders = Options.FileDownloaders;
                    if (fileDownloaders != null && fileDownloaders.Count > 0)
                    {
                        foreach (IInputFileDownloader downloader in fileDownloaders)
                        {
                            var files = await downloader.DownloadFilesAsync(turnContext, turnState, cancellationToken).ConfigureAwait(false);
                            turnState.Temp.InputFiles = [.. turnState.Temp.InputFiles, .. files];
                        }
                    }

                    // Execute first matching handler.  The RouteList enumerator is ordered by Invoke & Rank, then by Rank & add order.
                    foreach (Route route in _routes.Enumerate())
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            await route.Handler(turnContext, turnState, cancellationToken);
                            break;
                        }
                    }

                    // Call after turn handler
                    foreach (TurnEventHandler afterTurnHandler in _afterTurn)
                    {
                        if (!await afterTurnHandler(turnContext, turnState, cancellationToken))
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    foreach (AgentApplicationTurnError errorHandler in _turnErrorHandlers)
                    {
                        await errorHandler(turnContext, turnState, ex, cancellationToken).ConfigureAwait(false);
                    }

                    throw;
                }

                await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Stop the timer if configured
                StopTypingTimer();


                if (turnContext.StreamingResponse != null && turnContext.StreamingResponse.IsStreamStarted())
                {
                    await turnContext.StreamingResponse.EndStreamAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private void ApplyRouteAttributes()
        {
            // This will evaluate all methods that have an attribute, in declaration order (grouped by inheritance chain)
            foreach (var method in GetType().GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var activityRoutes = method.GetCustomAttributes<Attribute>(true);
                foreach (var attribute in activityRoutes)
                {
                    // Add route for all IRouteAttribute instances
                    if (attribute is IRouteAttribute routeAttribute)
                    {
                        routeAttribute.AddRoute(this, method);
                    }
                }
            }
        }

        #endregion

        #region Extension

        /// <summary>
        /// Registers extension with application, providing callback to specify extension features.
        /// </summary>
        /// <typeparam name="TExtension"></typeparam>
        /// <param name="extension"></param>
        /// <param name="extensionRegistration"></param>
        public void RegisterExtension<TExtension>(TExtension extension, Action<TExtension> extensionRegistration)
            where TExtension : IAgentExtension
        {
            ArgumentNullException.ThrowIfNull(extensionRegistration);
            extensionRegistration(extension);
        }
        #endregion
    }
}
