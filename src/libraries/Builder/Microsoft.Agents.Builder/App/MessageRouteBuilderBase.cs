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
    /// Provides the generic base builder for routing message activities in an <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Derive from <see cref="MessageRouteBuilderBase{TBuilder}"/> to create specialized message route builders
    /// while preserving fluent chaining on the concrete builder type. This base class supplies the shared message
    /// matching behavior, including message-only routing, optional text filters, custom selectors, channel
    /// constraints, and agentic routing support.
    /// </remarks>
    /// <typeparam name="TBuilder">The concrete builder type returned from fluent members.</typeparam>
    public abstract class MessageRouteBuilderBase<TBuilder> : RouteBuilderBase<TBuilder>
        where TBuilder : MessageRouteBuilderBase<TBuilder>
    {
        protected string _text;
        protected Regex _textPattern;

        protected MessageRouteBuilderBase() : base() { }

        /// <summary>
        /// Adds a selector to the route that matches incoming message activities with text equal to the specified
        /// value, ignoring case.
        /// </summary>
        /// <remarks>This method only matches activities of type 'Message' and will not match other
        /// activity types. If the route is marked as agentic, the selector will only match agentic requests as
        /// determined by AgenticAuthorization. Use this method to restrict route handling to specific message text
        /// values.</remarks>
        /// <param name="text">The text to match against the incoming activity's message content. Comparison is case-insensitive. Cannot be
        /// null.</param>
        /// <returns>The current builder instance with the added selector for matching <see cref="IActivity.Text"/>.</returns>
        public TBuilder WithText(string text)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(text, nameof(text));

            if (_text != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"MessageRouteBuilder.WithText({text}) with Text already set");
            }

            if (_textPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"MessageRouteBuilder.WithText({text}) with Text Regex already set");
            }

            _text = text;
            return (TBuilder)this;
        }

        /// <summary>
        /// Adds a text pattern selector to the route, matching incoming message activities whose text satisfies the
        /// specified regular expression.
        /// </summary>
        /// <remarks>This method only applies the selector to message activities. If the route is marked
        /// as agentic, the selector will only match agentic requests. Use this method to restrict route handling to
        /// messages whose text matches a specific pattern.</remarks>
        /// <param name="textPattern">The regular expression used to match the text of incoming message activities. Cannot be null. The selector
        /// will only match activities whose text property is not null and matches this pattern.</param>
        /// <returns>The current builder instance configured with the specified <see cref="IActivity.Text"/> pattern selector.</returns>
        public TBuilder WithText(Regex textPattern)
        {
            AssertionHelpers.ThrowIfNull(textPattern, nameof(textPattern));

            if (_textPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"MessageRouteBuilder.WithText(Regex({textPattern})) with Text Regex already set");
            }

            if (_text != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"MessageRouteBuilder.WithText(Regex({textPattern})) with Text already set");
            }

            _textPattern = textPattern;

            return (TBuilder)this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns. If WithText was
        /// also called, this selector is in addition to the Text selector.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "message" is enforced.</param>
        /// <returns>The current builder instance with the specified selector applied.</returns>
        public override TBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"MessageRouteBuilder.WithSelector()");
            }

            async Task<bool> ensureMessage(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.Message) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureMessage;
            return (TBuilder)this;
        }

        /// <summary>
        /// For message routes, the invoke flag is ignored to prevent misconfiguration.
        /// </summary>
        /// <remarks>Messages cannot be configured as invoke routes. This method always returns the
        /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current builder instance.</returns>
        public override TBuilder AsInvoke(bool isInvoke = true)
        {
            return (TBuilder)this;
        }

        protected override void PreBuild()
        {
            // When no text filter is specified the route matches any message — default to Last so
            // specific-text routes take priority without callers having to set the rank explicitly.
            if (_text == null && _textPattern == null && _route.Rank == RouteRank.Unspecified)
            {
                _route.Rank = RouteRank.Last;
            }

            if (_route.Selector != null)
            {
                if (_text != null || _textPattern != null)
                {
                    // Match on the existing selector, require a message activity, and apply the configured text/text-pattern filter
                    var existingSelector = _route.Selector;
                    _route.Selector = async (context, ct) =>
                        IsContextMatch(context, _route)
                        && context.Activity is IMessageActivity messageActivity
                        && (_text != null ? _text.Equals(messageActivity.Text, StringComparison.OrdinalIgnoreCase) : messageActivity.Text != null && _textPattern.IsMatch(messageActivity.Text))
                        && await existingSelector(context, ct).ConfigureAwait(false);
                }
                return;
            }

            if (_text == null && _textPattern == null)
            {
                // If no text or pattern specified, match any message activity
                _route.Selector = (context, ct) => Task.FromResult(IsContextMatch(context, _route) && context.Activity is IMessageActivity);
                return;
            }

            // Just match on Activity.Text value
            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity is IMessageActivity messageActivity
                    && (_text != null ? _text.Equals(messageActivity.Text, StringComparison.OrdinalIgnoreCase) : messageActivity.Text != null && _textPattern.IsMatch(messageActivity.Text))
                );
        }
    }
}