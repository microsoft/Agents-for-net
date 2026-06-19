// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Provides the generic base builder for routing activities by activity type in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Derive from <see cref="TypeRouteBuilderBase{TBuilder}"/> to create specialized type-based route builders
    /// while preserving fluent chaining on the concrete builder type. This base class supplies the shared activity
    /// type matching behavior, including string and regular expression filters, custom selectors, channel
    /// constraints, and agentic routing support. If neither <see cref="WithType(string)"/> nor
    /// <see cref="WithType(Regex)"/> is called, the route matches any activity type. Because this builder only
    /// filters on activity type, call <see cref="RouteBuilderBase{TBuilder}.AsInvoke(bool)"/> when the route should
    /// be treated as an invoke route.
    /// </remarks>
    /// <typeparam name="TBuilder">The concrete builder type returned from fluent members.</typeparam>
    public abstract class TypeRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : TypeRouteBuilderBase<TBuilder>
    {
        private string _type;
        private Regex _typePattern;

        protected TypeRouteBuilderBase() : base() { }

        /// <summary>
        /// Configures the route to match activities of the specified type.
        /// </summary>
        /// <remarks>This method updates the route selector to filter activities based on the provided
        /// type. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="type">The activity type to match. Cannot be null or empty.</param>
        /// <returns>The current builder instance with the added selector for matching <see cref="IActivity.Type"/>.</returns>
        public TBuilder WithType(string type)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(type, nameof(type));

            if (_type != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType({type}) with Type already set");
            }

            if (_typePattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType({type}) with Type Regex already set");
            }

            _type = type;
            return (TBuilder)this;
        }

        /// <summary>
        /// Configures the route to match activities whose type satisfies the specified regular expression pattern.
        /// </summary>
        /// <remarks>This method updates the route's selector to only match activities whose type matches
        /// the provided pattern. If the route is marked as agentic, it will also require the request to be agentic for
        /// the selector to return <see langword="true"/>.</remarks>
        /// <param name="typePattern">A regular expression used to determine whether the activity type should be matched by the route. Cannot be
        /// null.</param>
        /// <returns>The current builder instance configured with the specified <see cref="IActivity.Type"/> pattern selector.</returns>
        public TBuilder WithType(Regex typePattern)
        {
            AssertionHelpers.ThrowIfNull(typePattern, nameof(typePattern));

            if (_typePattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType(Regex({typePattern})) with Type Regex already set");
            }

            if (_type != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithType(Regex({typePattern})) with Type already set");
            }

            _typePattern = typePattern;
            return (TBuilder)this;
        }

        /// <summary>
        /// Sets the route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns. If WithType was
        /// also called, this selector is in addition to the Type selector.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc...</param>
        /// <returns>The current builder instance configured with the specified custom selector.</returns>
        public override TBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"TypeRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureRouteMatch(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureRouteMatch;
            return (TBuilder)this;
        }

        protected override void PreBuild()
        {
            // When no type filter is specified the route matches any activity — default to Last so
            // specific-type routes take priority without callers having to set the rank explicitly.
            if (_type == null && _typePattern == null && _route.Rank == RouteRank.Unspecified)
            {
                _route.Rank = RouteRank.Last;
            }

            if (_route.Selector != null)
            {
                if (_type != null || _typePattern != null)
                {
                    // Match on both the existing selector and the Activity.Type
                    var existingSelector = _route.Selector;
                    _route.Selector = async (context, ct) =>
                        IsContextMatch(context, _route)
                        && (_type != null ? context.Activity.IsType(_type) : context.Activity.Type != null && _typePattern.IsMatch(context.Activity.Type))
                        && await existingSelector(context, ct);
                }
                return;
            }

            if (_type == null && _typePattern == null)
            {
                // If no type or pattern specified, match any activity
                _route.Selector = (context, ct) => Task.FromResult(IsContextMatch(context, _route));
                return;
            }

            // Just match on Activity.Type value
            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && (_type != null ? context.Activity.IsType(_type) : context.Activity.Type != null && _typePattern.IsMatch(context.Activity.Type))
                );
        }
    }
}