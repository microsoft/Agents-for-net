// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides the generic base builder for routing event activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Derive from <see cref="EventRouteBuilderBase{TBuilder}"/> to create specialized event route builders while
    /// preserving fluent chaining on the concrete builder type. This base class supplies the shared event matching
    /// behavior, including event-name filters, custom selectors, channel constraints, and agentic routing support.
    /// If neither <see cref="WithName(string)"/> nor <see cref="WithName(Regex)"/> is called, the route matches any
    /// event activity regardless of name.
    /// </remarks>
    /// <typeparam name="TBuilder">The concrete builder type returned from fluent members.</typeparam>
    public class EventRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : EventRouteBuilderBase<TBuilder>
    {
        private string _eventName;
        private Regex _eventRegex;

        protected EventRouteBuilderBase() { }

        /// <summary>
        /// Configures the route to match event activities with the specified name, using a case-insensitive comparison.
        /// </summary>
        /// <remarks>This method restricts the route to only handle event activities whose name matches
        /// the specified value. If the route is marked as agentic, only agentic requests will be considered for
        /// matching.</remarks>
        /// <param name="name">The name of the event activity to match. Comparison is case-insensitive. Cannot be null.</param>
        /// <returns>The current builder instance with the added selector for matching <see cref="IActivity.Name"/>.</returns>
        public TBuilder WithName(string name)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            if (_eventName != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName({name}) with Name already set");
            }

            if (_eventRegex != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName({name}) with Name Regex already set");
            }

            _eventName = name;
            return (TBuilder)this;
        }

        /// <summary>
        /// Configures the event route to match event activities whose name satisfies the specified regular expression
        /// pattern.
        /// </summary>
        /// <remarks>This method restricts the route to event activities whose name matches the provided
        /// pattern. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="namePattern">The regular expression used to match the name of incoming event activities. Cannot be null.</param>
        /// <returns>The current builder instance configured with the specified <see cref="IActivity.Name"/> pattern selector.</returns>
        public TBuilder WithName(Regex namePattern)
        {
            AssertionHelpers.ThrowIfNull(namePattern, nameof(namePattern));

            if (_eventRegex != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName(Regex({namePattern})) with Name Regex already set");
            }

            if (_eventName != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithName(Regex({namePattern})) with Name already set");
            }

            _eventRegex = namePattern;
            return (TBuilder)this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns. If WithName was
        /// also called, this selector is in addition to the Name selector.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "event" is enforced.</param>
        /// <returns>The current builder instance configured with the custom selector.</returns>
        public override TBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"EventRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureEvent(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.Event) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureEvent;
            return (TBuilder)this;
        }

        /// <summary>
        /// Returns the current builder instance.
        /// </summary>
        /// <remarks>Event routes cannot be configured as invoke routes, so the value of <paramref name="isInvoke"/> is ignored.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override TBuilder AsInvoke(bool isInvoke = true)
        {
            return (TBuilder)this;
        }

        protected override void PreBuild()
        {
            // When no name filter is specified the route matches any event — default to Last so
            // specific-name routes take priority without callers having to set the rank explicitly.
            if (_eventName == null && _eventRegex == null && _route.Rank == RouteRank.Unspecified)
            {
                _route.Rank = RouteRank.Last;
            }

            if (_route.Selector != null)
            {
                if (_eventName != null || _eventRegex != null)
                {
                    // Match on both the existing selector and the Activity.Name
                    var existingSelector = _route.Selector;
                    _route.Selector = async (context, ct) =>
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.Event)
                        && (_eventName != null ? _eventName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase) : context.Activity.Name != null && _eventRegex.IsMatch(context.Activity.Name))
                        && await existingSelector(context, ct);
                }
                return;
            }

            if (_eventName == null && _eventRegex == null)
            {
                // If no name or regex specified, just match on any event
                _route.Selector = (context, ct) => Task.FromResult
                    (
                        IsContextMatch(context, _route)
                        && context.Activity.IsType(ActivityTypes.Event)
                    );
                return;
            }

            // Just match on Activity.Name value
            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Event)
                    && context.Activity.Name != null
                    && (_eventName != null ? _eventName.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase) : context.Activity.Name != null && _eventRegex.IsMatch(context.Activity.Name))
                );
        }
    }
}
