// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Constants for adaptive card invoke names
    /// </summary>
    public class AdaptiveCardsInvokeNames
    {
        /// <summary>
        /// Action invoke name
        /// </summary>
        public static readonly string ACTION_INVOKE_NAME = "adaptiveCard/action";
    }

    /// <summary>
    /// AdaptiveCards class to enable fluent style registration of handlers related to Adaptive Cards.
    /// </summary>
    public partial class AdaptiveCard
    {
        private static readonly string DEFAULT_ACTION_SUBMIT_FILTER = "verb";

        private readonly AgentApplication _app;

        /// <summary>
        /// Creates a new instance of the AdaptiveCards class.
        /// </summary>
        /// <param name="app"></param> The top level application class to register handlers with.
        public AdaptiveCard(AgentApplication app)
        {
            this._app = app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events that will match on the verb using an exact string comparison.
        /// </summary>
        /// <param name="verb">The named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionExecute(string verb, ActionExecuteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                ActionExecuteRouteBuilder.Create()
                    .WithVerb(verb)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events that will match on the verb using a Regex pattern.
        /// </summary>
        /// <param name="verbPattern">Regular expression to match against the named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionExecute(Regex verbPattern, ActionExecuteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                ActionExecuteRouteBuilder.Create()
                    .WithVerb(verbPattern)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionExecute(RouteSelector routeSelector, ActionExecuteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                ActionExecuteRouteBuilder.Create()
                    .WithSelector(routeSelector)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This will be removed in future versions.")]
        public AgentApplication OnActionExecute(MultipleRouteSelector routeSelectors, ActionExecuteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors,nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnActionExecute(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnActionExecute(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActionExecute(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="verb">The named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionSubmit(string verb, ActionSubmitHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            string filter = _app.Options.AdaptiveCards?.ActionSubmitFilter ?? DEFAULT_ACTION_SUBMIT_FILTER;
            return _app.AddRoute(
                ActionSubmitRouteBuilder.Create()
                    .WithVerb(verb)
                    .WithFilter(filter)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="verbPattern">Regular expression to match against the named action to be handled.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionSubmit(Regex verbPattern, ActionSubmitHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            string filter = _app.Options.AdaptiveCards?.ActionSubmitFilter ?? DEFAULT_ACTION_SUBMIT_FILTER;
            return _app.AddRoute(
                ActionSubmitRouteBuilder.Create()
                    .WithVerb(verbPattern)
                    .WithFilter(filter)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActionSubmit(RouteSelector routeSelector, ActionSubmitHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                ActionSubmitRouteBuilder.Create()
                    .WithSelector(routeSelector)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This will be removed in future versions.")]
        public AgentApplication OnActionSubmit(MultipleRouteSelector routeSelectors, ActionSubmitHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnActionSubmit(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnActionSubmit(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActionSubmit(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="dataset">The dataset to be searched.</param>
        /// <param name="handler">Function to call when the search is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSearch(string dataset, SearchHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(dataset, nameof(dataset));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                SearchRouteBuilder.Create()
                    .WithDataset(dataset)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="datasetPattern">Regular expression to match against the dataset to be searched.</param>
        /// <param name="handler">Function to call when the search is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSearch(Regex datasetPattern, SearchHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(datasetPattern, nameof(datasetPattern));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                SearchRouteBuilder.Create()
                    .WithDataset(datasetPattern)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers">Optional list of OAuth handlers to run before the route handler.</param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSearch(RouteSelector routeSelector, SearchHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            return _app.AddRoute(
                SearchRouteBuilder.Create()
                    .WithSelector(routeSelector)
                    .WithHandler(handler)
                    .WithOrderRank(rank)
                    .WithOAuthHandlers(autoSignInHandlers)
                    .AsAgentic(isAgenticOnly)
                    .Build());
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        [Obsolete("This will be removed in future versions.")]
        public AgentApplication OnSearch(MultipleRouteSelector routeSelectors, SearchHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnSearch(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnSearch(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnSearch(routeSelector, handler);
                }
            }
            return _app;
        }
    }
}
