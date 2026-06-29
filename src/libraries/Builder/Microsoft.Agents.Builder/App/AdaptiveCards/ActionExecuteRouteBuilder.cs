// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Provides a concrete builder for routing Adaptive Card <c>Action.Execute</c> invoke activities in an
    /// <see cref="AgentApplication"/>.
    /// </summary>
    /// <remarks>
    /// Routes built from this builder match invoke activities named <c>adaptiveCard/action</c> whose
    /// <c>Action.Verb</c> matches the configured verb or verb pattern. When neither <see cref="WithVerb(string)"/>
    /// nor <see cref="WithVerb(Regex)"/> is called the route matches any verb. A custom selector supplied via
    /// <see cref="WithSelector(RouteSelector)"/> is additive to the basic route matching (channel, activity type,
    /// invoke name, and verb).
    /// </remarks>
    public class ActionExecuteRouteBuilder : RouteBuilderBase<ActionExecuteRouteBuilder>
    {
        private const string ActionExecuteType = "Action.Execute";

        private string _verb;
        private Regex _verbPattern;

        /// <summary>
        /// Creates a new instance of the <see cref="ActionExecuteRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="ActionExecuteRouteBuilder"/>.</returns>
        public static ActionExecuteRouteBuilder Create()
        {
            return new ActionExecuteRouteBuilder();
        }

        public ActionExecuteRouteBuilder() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Configures the route to match only Action.Execute activities whose verb equals the specified value.
        /// </summary>
        /// <param name="verb">The verb to match. Comparison is exact.</param>
        /// <returns>The current <see cref="ActionExecuteRouteBuilder"/> instance for method chaining.</returns>
        public ActionExecuteRouteBuilder WithVerb(string verb)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(verb, nameof(verb));

            if (_verb != null || _verbPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ActionExecuteRouteBuilder.WithVerb({verb})");
            }

            _verb = verb;
            return this;
        }

        /// <summary>
        /// Configures the route to match Action.Execute activities whose verb matches the specified pattern.
        /// </summary>
        /// <param name="verbPattern">A regular expression used to match the verb.</param>
        /// <returns>The current <see cref="ActionExecuteRouteBuilder"/> instance for method chaining.</returns>
        public ActionExecuteRouteBuilder WithVerb(Regex verbPattern)
        {
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));

            if (_verb != null || _verbPattern != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ActionExecuteRouteBuilder.WithVerb(Regex({verbPattern}))");
            }

            _verbPattern = verbPattern;
            return this;
        }

        /// <summary>
        /// Sets a custom route selector that is additive to the basic Action.Execute matching (channel, activity type,
        /// invoke name, and verb).
        /// </summary>
        /// <param name="selector">The route selector that defines additional criteria for matching requests to the route. The supplied
        /// selector does not need to validate base route properties like ChannelId, Agentic, activity type, invoke
        /// name, or verb.</param>
        /// <returns>The current <see cref="ActionExecuteRouteBuilder"/> instance with the specified selector applied.</returns>
        public override ActionExecuteRouteBuilder WithSelector(RouteSelector selector)
        {
            AssertionHelpers.ThrowIfNull(selector, nameof(selector));

            if (_route.Selector != null)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.RouteSelectorAlreadyDefined, null, $"ActionExecuteRouteBuilder.WithSelector()");
            }

            _route.Selector = selector;
            return this;
        }

        /// <summary>
        /// Configures the route to handle Action.Execute events using the specified handler.
        /// </summary>
        /// <param name="handler">The handler to invoke when a matching Action.Execute activity is received. Cannot be null.</param>
        /// <returns>The current <see cref="ActionExecuteRouteBuilder"/> instance for method chaining.</returns>
        public ActionExecuteRouteBuilder WithHandler(ActionExecuteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                if (AdaptiveCardInvokeResponseFactory.TryValidateActionInvokeValue(turnContext.Activity, ActionExecuteType, out AdaptiveCardInvokeValue invokeValue, out var response))
                {
                    response = await handler(turnContext, turnState, invokeValue.Action.Data, cancellationToken).ConfigureAwait(false);
                }

                var activity = Activity.CreateInvokeResponseActivity(response, response.StatusCode ?? (int)HttpStatusCode.OK);
                await turnContext.SendActivityAsync(activity, cancellationToken).ConfigureAwait(false);
            }

            _route.Handler = routeHandler;
            return this;
        }

        /// <summary>
        /// Returns the current builder instance.
        /// </summary>
        /// <remarks>Action.Execute routes always handle invoke activities, so the value of <paramref name="isInvoke"/> is ignored.</remarks>
        /// <param name="isInvoke">Ignored.</param>
        /// <returns>The current builder instance.</returns>
        public override ActionExecuteRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }

        protected override void PreBuild()
        {
            var existingSelector = _route.Selector;

            _route.Selector = async (context, ct) =>
            {
                if (!IsContextMatch(context, _route)
                    || !context.Activity.IsType(ActivityTypes.Invoke)
                    || !string.Equals(context.Activity.Name, AdaptiveCardsInvokeNames.ACTION_INVOKE_NAME))
                {
                    return false;
                }

                AdaptiveCardInvokeValue invokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardInvokeValue>(context.Activity.Value);
                if (!IsVerbMatch(invokeValue?.Action?.Verb))
                {
                    return false;
                }

                return existingSelector == null || await existingSelector(context, ct).ConfigureAwait(false);
            };
        }

        private bool IsVerbMatch(string verb)
        {
            if (_verb != null)
            {
                return string.Equals(_verb, verb);
            }

            if (_verbPattern != null)
            {
                return verb != null && _verbPattern.IsMatch(verb);
            }

            return true;
        }
    }
}
