// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing Event activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="EventRouteBuilder"/> to create and configure routes that respond to event
    /// activities. This builder allows matching event activities by name or regular expression, and supports 
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Create"/> method 
    /// and further configured using one of <see cref="WithName(string)"/> or <see cref="WithName(Regex)"/> 
    /// or <see cref="WithSelector(RouteSelector)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = EventRouteBuilder.Create()
    ///    .WithName("myEvent")
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Event received!", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class EventRouteBuilder : RouteBuilderBase<EventRouteBuilder>
    {
        /// <summary>
        /// Configures the route to match event activities with the specified name, using a case-insensitive comparison.
        /// </summary>
        /// <remarks>This method restricts the route to only handle event activities whose name matches
        /// the specified value. If the route is marked as agentic, only agentic requests will be considered for
        /// matching.</remarks>
        /// <param name="text">The name of the event activity to match. Comparison is case-insensitive. Cannot be null.</param>
        /// <returns>A EventRouteBuilder instance with the added selector for matching Activity.Name.</returns>
        public EventRouteBuilder WithName(string text)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(text, nameof(text));

            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Event)
                    && text.Equals(context.Activity.Name, StringComparison.OrdinalIgnoreCase)
                );

            return this;
        }

        /// <summary>
        /// Configures the event route to match event activities whose name satisfies the specified regular expression
        /// pattern.
        /// </summary>
        /// <remarks>This method restricts the route to event activities whose name matches the provided
        /// pattern. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="textPattern">The regular expression used to match the name of incoming event activities. Cannot be null.</param>
        /// <returns>A EventRouteBuilder instance configured with the specified Activity.Name pattern selector.</returns>
        public EventRouteBuilder WithName(Regex textPattern)
        {
            AssertionHelpers.ThrowIfNull(textPattern, nameof(textPattern));

            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(ActivityTypes.Event)
                    && textPattern.IsMatch(context.Activity.Name)
                );

            return this;
        }

        /// <summary>
        /// Sets a custom route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc. An Activity type of "event" is enforced.</param>
        /// <returns>A EventRouteBuilder instance configured with the custom selector.</returns>
        public override EventRouteBuilder WithSelector(RouteSelector selector)
        {
            async Task<bool> ensureEvent(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && context.Activity.IsType(ActivityTypes.Event) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureEvent;
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public EventRouteBuilder WithHandler(RouteHandler handler)
        {
            _route.Handler = handler;
            return this;
        }

        /// <summary>
        /// Returns the current event route builder instance. For event routes, the invoke flag is ignored to
        /// prevent misconfiguration.
        /// </summary>
        /// <remarks>Events cannot be configured as invoke routes. This method always returns the
        /// current instance, regardless of the value of <paramref name="isInvoke"/>.</remarks>
        /// <param name="isInvoke">Ignored</param>
        /// <returns>The current instance of <see cref="EventRouteBuilder"/>.</returns>
        public override EventRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
