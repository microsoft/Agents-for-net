// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Telemetry.App.Scopes;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public delegate Task AgentApplicationTurnError(ITurnContext turnContext, ITurnState turnState, Exception exception, CancellationToken cancellationToken);

    /// <summary>
    /// Application class for routing and processing incoming requests.
    /// </summary>
    public partial class AgentApplication : IAgent
    {
        private readonly UserAuthorization _userAuth;

        private readonly RouteList _routes;
        private readonly ConcurrentQueue<TurnEventHandler> _beforeTurn;
        private readonly ConcurrentQueue<TurnEventHandler> _afterTurn;
        private readonly ConcurrentQueue<AgentApplicationTurnError> _turnErrorHandlers;
        
        public List<IAgentExtension> RegisteredExtensions { get; private set; } = new List<IAgentExtension>();

        /// <summary>
        /// Creates a new AgentApplication instance.
        /// </summary>
        /// <param name="options">Optional. Options used to configure the application.</param>
        public AgentApplication(AgentApplicationOptions options)
        {
            AssertionHelpers.ThrowIfNull(options, nameof(options));

            Options = options;

            Logger = options.LoggerFactory?.CreateLogger<AgentApplication>() ?? AgentApplicationOptions.DefaultLoggerFactory.CreateLogger<AgentApplication>();

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

            AdaptiveCards = new AdaptiveCard(this);
            Proactive = new Proactive.Proactive(this);

            if (options.UserAuthorization != null)
            {
                _userAuth = new UserAuthorization(this, options.UserAuthorization);
            }

            ApplyRouteAttributes();
        }

        #region Application Features

        /// <summary>
        /// Fluent interface for accessing Adaptive Card specific features.
        /// </summary>
        public AdaptiveCard AdaptiveCards { get; }

        public Proactive.Proactive Proactive { get; }

        /// <summary>
        /// The application's configured options.
        /// </summary>
        public AgentApplicationOptions Options { get; }

        public ILogger Logger { get; private set; }

        /// <summary>
        /// Accessing user authorization features.
        /// </summary>
        public UserAuthorization UserAuthorization
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
        public AgentApplication AddRoute(Route route)
        {
            AssertionHelpers.ThrowIfNull(route, nameof(route));
            _routes.AddRoute(route);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute before the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to continue execution of the current turn. Handler returning false
        /// prevents the turn from running, but the Agents state is still saved, which lets you
        /// track the reason why the turn was not processed. It also means you can use this as
        /// a way to call into the dialog system. For example, you could use the OAuthPrompt to sign the
        /// user in before allowing the AI system to run.
        /// </summary>
        /// <param name="handler">Function to call before turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnBeforeTurn(TurnEventHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _beforeTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute after the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to finish execution of the current turn. Handler returning false
        /// prevents the Agents state from being saved.
        /// </summary>
        /// <param name="handler">Function to call after turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAfterTurn(TurnEventHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
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
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
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
            // Idempotent — if already started for this turn, do nothing.
            if (turnContext.Services.Get<TypingWorker>() != null)
            {
                return;
            }

            var worker = TypingWorker.Create(turnContext, Options.TypingOptions);
            if (worker == null)
            {
                return;
            }

            turnContext.Services.Set<TypingWorker>(worker);
            worker.Start();
        }

        /// <summary>
        /// Manually stop the typing timer for the current turn.
        /// </summary>
        /// <remarks>
        /// Stops the typing worker immediately and waits for it to finish. Subsequent calls for
        /// the same turn are no-ops. The worker is also stopped automatically at end of turn.
        /// </remarks>
        /// <param name="turnContext">The turn context.</param>
#pragma warning disable CA1822 // Method is intentionally an instance member for API symmetry with StartTypingTimer.
        public async Task StopTypingTimer(ITurnContext turnContext)
        {
            var worker = turnContext.Services.Get<TypingWorker>();
            if (worker != null)
            {
                await worker.DisposeAsync().ConfigureAwait(false);
                // Remove the entry so StartTypingTimer can create a new worker if called again.
                turnContext.Services.TryRemove(typeof(TypingWorker).FullName, out _);
            }
        }
#pragma warning restore CA1822
        #endregion

        #region Turn Handling

        /// <summary>
        /// Called by the adapter (for example, a <see cref="Microsoft.Agents.Hosting.AspNetCore.CloudAdapter"/>)
        /// at runtime in order to process an inbound <see cref="Microsoft.Agents.Core.Models.Activity"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            using var onTurnTelemetryScope = new ScopeOnTurn(turnContext);

            if (_userAuth != null)
            {
                turnContext.Services.Set<UserAuthorization>(_userAuth);
            }

            bool routeMatched = false;
            bool routeAuthorized = false;

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
                        turnContext.Activity.RemoveRecipientMention();
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
                        // For AutoSignIn, this will initiate the OAuth flow.  Otherwise, this will continue OAuth flows
                        // start by a Route (when `autoSignInHandlers` are specified on the Route).
                        var signInComplete = await _userAuth.StartOrContinueSignInUserAsync(turnContext, turnState, cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (!signInComplete)
                        {
                            return;
                        }
                    }

                    // Download any input files
                    IList<IInputFileDownloader>? fileDownloaders = Options.FileDownloaders;
                    if (fileDownloaders != null && fileDownloaders.Count > 0)
                    {
                        using var telemetryScope = new ScopeDownloadFiles(turnContext);
                        foreach (IInputFileDownloader downloader in fileDownloaders)
                        {
                            var files = await downloader.DownloadFilesAsync(turnContext, turnState, cancellationToken).ConfigureAwait(false);
                            turnState.Temp.InputFiles = [.. turnState.Temp.InputFiles, .. files];
                        }
                    }

                    // Call before turn handler
                    using (var telemetryScope = new ScopeBeforeTurn())
                    {
                        foreach (TurnEventHandler beforeTurnHandler in _beforeTurn)
                        {
                            if (!await beforeTurnHandler(turnContext, turnState, cancellationToken))
                            {
                                // Save turn state
                                // - This lets the Agent keep track of why it ended the previous turn. It also
                                //   allows the dialog system to be used before the AI system is called.
                                await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                                return;
                            }
                        }
                    }

                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        var (routeCount, routeListFormatted) = _routes.FormatRouteList();
                        LogRouteList(Logger, routeCount, routeListFormatted);
                    }

                    // Execute first matching handler.  The RouteList enumerator is ordered by Invoke & Rank, then by Rank & add order.
                    foreach (Route route in _routes.Enumerate())
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            routeMatched = true;
                            var handlers = route.OAuthHandlers(turnContext);
                            if (_userAuth == null || handlers?.Length == 0)
                            {
                                routeAuthorized = true;
                                using var routeTelemetryScope = new ScopeRouteHandler(
                                        isAgentic: route.Flags.HasFlag(RouteFlags.Agentic),
                                        isInvoke: route.Flags.HasFlag(RouteFlags.Invoke)
                                    );
                                await route.Handler(turnContext, turnState, cancellationToken);
                            }
                            else
                            {
                                bool signInComplete = false;

                                foreach (var handler in handlers)
                                {
                                    signInComplete = await _userAuth.StartOrContinueSignInUserAsync(turnContext, turnState, handler, forceAuto: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                                    if (!signInComplete)
                                    {
                                        break;
                                    }
                                }

                                if (signInComplete)
                                {
                                    routeAuthorized = true;
                                    using var routeTelemetryScope = new ScopeRouteHandler(
                                        isAgentic: route.Flags.HasFlag(RouteFlags.Agentic),
                                        isInvoke: route.Flags.HasFlag(RouteFlags.Invoke)
                                    );
                                    await route.Handler(turnContext, turnState, cancellationToken);
                                }
                            }

                            if (!route.Flags.HasFlag(RouteFlags.NonTerminal))
                            {
                                break;
                            }
                        }
                    }
                    onTurnTelemetryScope.Share(routeAuthorized, routeMatched);

                    // Call after turn handler
                    using (var telemetryScope = new ScopeAfterTurn())
                    {
                        foreach (TurnEventHandler afterTurnHandler in _afterTurn)
                        {
                            if (!await afterTurnHandler(turnContext, turnState, cancellationToken))
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    onTurnTelemetryScope.Share(routeAuthorized, routeMatched);
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
                // Stop the typing worker for this turn if one was started.
                var typingWorker = turnContext.Services?.Get<TypingWorker>();
                if (typingWorker != null)
                {
                    await typingWorker.DisposeAsync().ConfigureAwait(false);
                }

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
            AssertionHelpers.ThrowIfNull(extensionRegistration, nameof(extensionRegistration));
            if (RegisteredExtensions.Contains(extension))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.ExtensionAlreadyRegistered, null, nameof(TExtension));
            }
            // TODO: add Logging event for extension registration
            RegisteredExtensions.Add(extension);
            extensionRegistration(extension);
        }
        #endregion
    }
}
