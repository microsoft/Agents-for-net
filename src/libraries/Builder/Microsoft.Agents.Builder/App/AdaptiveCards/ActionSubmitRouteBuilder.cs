// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Provides a concrete builder for routing Adaptive Card <c>Action.Submit</c> message activities in an
    /// <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Routes built from this builder match message activities with no text and a value object whose configured
    /// filter field (defaults to <c>verb</c>) matches the configured verb or verb pattern. When neither
    /// <see cref="WithVerb(string)"/> nor <see cref="WithVerb(Regex)"/> is called the route matches any Action.Submit
    /// message (a message with no text and a non-null value). A custom selector supplied via
    /// <see cref="WithSelector(RouteSelector)"/> is additive to the basic route matching (channel, activity type,
    /// and verb).
    /// </remarks>
    public class ActionSubmitRouteBuilder : RouteBuilderBase<ActionSubmitRouteBuilder>
    {
        private const string DefaultActionSubmitFilter = "verb";

        private string _verb;
        private Regex _verbPattern;
        private string _filter = DefaultActionSubmitFilter;

        /// <summary>
        /// Creates a new instance of the <see cref="ActionSubmitRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="ActionSubmitRouteBuilder"/>.</returns>
        public static ActionSubmitRouteBuilder Create()
        {
            return new ActionSubmitRouteBuilder();
        }

        public ActionSubmitRouteBuilder() : base() { }

        /// <summary>
        /// Configures the route to match only Action.Submit activities whose filter field equals the specified value.
        /// </summary>
        /// <param name="verb">The verb to match. Comparison is exact.</param>
        /// <returns>The current <see cref="ActionSubmitRouteBuilder"/> instance for method chaining.</returns>
        public ActionSubmitRouteBuilder WithVerb(string verb)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));

            if (_verb != null || _verbPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ActionSubmitRouteBuilder.WithVerb({verb})");
            }

            _verb = verb;
            return this;
        }

        /// <summary>
        /// Configures the route to match Action.Submit activities whose filter field matches the specified pattern.
        /// </summary>
        /// <param name="verbPattern">A regular expression used to match the value of the filter field.</param>
        /// <returns>The current <see cref="ActionSubmitRouteBuilder"/> instance for method chaining.</returns>
        public ActionSubmitRouteBuilder WithVerb(Regex verbPattern)
        {
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));

            if (_verb != null || _verbPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ActionSubmitRouteBuilder.WithVerb(Regex({verbPattern}))");
            }

            _verbPattern = verbPattern;
            return this;
        }

        /// <summary>
        /// Sets the name of the value field used to filter Action.Submit activities. Defaults to <c>verb</c>.
        /// </summary>
        /// <param name="filter">The value field name to match the verb against.</param>
        /// <returns>The current <see cref="ActionSubmitRouteBuilder"/> instance for method chaining.</returns>
        public ActionSubmitRouteBuilder WithFilter(string filter)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(filter, nameof(filter));
            _filter = filter;
            return this;
        }

        /// <summary>
        /// Sets a custom route selector that is additive to the basic Action.Submit matching (channel, activity type,
        /// and verb).
        /// </summary>
        /// <param name="selector">The route selector that defines additional criteria for matching requests to the route. The supplied
        /// selector does not need to validate base route properties like ChannelId, Agentic, activity type, or
        /// verb.</param>
        /// <returns>The current <see cref="ActionSubmitRouteBuilder"/> instance with the specified selector applied.</returns>
        public override ActionSubmitRouteBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ActionSubmitRouteBuilder.WithSelector()");
            }

            _route.Selector = selector;
            return this;
        }

        /// <summary>
        /// Configures the route to handle Action.Submit events using the specified handler.
        /// </summary>
        /// <param name="handler">The handler to invoke when a matching Action.Submit activity is received. Cannot be null.</param>
        /// <returns>The current <see cref="ActionSubmitRouteBuilder"/> instance for method chaining.</returns>
        public ActionSubmitRouteBuilder WithHandler(ActionSubmitHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(turnContext.Activity.Text)
                    || turnContext.Activity.Value == null)
                {
                    throw new InvalidOperationException($"Unexpected AdaptiveCards.OnActionSubmit() triggered for activity type: {turnContext.Activity.Type}");
                }

                await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken).ConfigureAwait(false);
            }

            _route.Handler = routeHandler;
            return this;
        }

        /// <summary>
        /// For Action.Submit routes, the invoke flag is ignored to prevent misconfiguration.
        /// </summary>
        /// <remarks>Action.Submit activities are message activities and cannot be configured as invoke routes.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override ActionSubmitRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        protected override void PreBuild()
        {
            var existingSelector = _route.Selector;

            _route.Selector = async (context, ct) =>
            {
                if (!IsContextMatch(context, _route)
                    || !string.Equals(context.Activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(context.Activity.Text)
                    || context.Activity.Value == null)
                {
                    return false;
                }

                if ((_verb != null || _verbPattern != null) && !IsVerbMatch(context.Activity.Value))
                {
                    return false;
                }

                return existingSelector == null || await existingSelector(context, ct).ConfigureAwait(false);
            };
        }

        private bool IsVerbMatch(object value)
        {
            JsonObject obj = ProtocolJsonSerializer.ToObject<JsonObject>(value);
            if (obj == null
                || obj[_filter] == null
                || obj[_filter].GetValueKind() != System.Text.Json.JsonValueKind.String)
            {
                return false;
            }

            string verb = obj[_filter].ToString();

            if (_verb != null)
            {
                return string.Equals(_verb, verb);
            }

            return verb != null && _verbPattern.IsMatch(verb);
        }
    }
}
