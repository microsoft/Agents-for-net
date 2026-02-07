// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing Event activities in an AgentApplication.
    /// </summary>
    /// <remarks>Use <see cref="EventRouteBuilder"/> to create and configure routes that respond to event
    /// activities, such as custom triggers or signals in a conversation. This builder allows matching event activities
    /// by name or regular expression, and supports agentic routing scenarios. Typically, instances are created via the
    /// <see cref="Create"/> method and further configured using fluent methods such as <see cref="WithName(string)"/>
    /// or <see cref="WithName(Regex)"/> or <see cref="RouteBuilder.WithSelector(RouteSelector)"/>.</remarks>
    public class EventRouteBuilder : RouteBuilderBase<EventRouteBuilder>
    {
        /// <summary>
        /// Configures the route to match event activities with the specified name, using a case-insensitive comparison.
        /// </summary>
        /// <remarks>This method restricts the route to only handle event activities whose name matches
        /// the specified value. If the route is marked as agentic, only agentic requests will be considered for
        /// matching.</remarks>
        /// <param name="text">The name of the event activity to match. Comparison is case-insensitive. Cannot be null.</param>
        /// <returns>The current instance of <see cref="EventRouteBuilder"/> with the updated event name selector.</returns>
        public EventRouteBuilder WithName(string text)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && context.Activity.IsType(ActivityTypes.Event)
                    && context.Activity?.Name != null
                    && context.Activity.Name.Equals(text, StringComparison.OrdinalIgnoreCase)
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
        /// <returns>The current instance of <see cref="EventRouteBuilder"/> with the updated event name selector.</returns>
        public EventRouteBuilder WithName(Regex textPattern)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && context.Activity.IsType(ActivityTypes.Event)
                    && context.Activity?.Name != null
                    && textPattern.IsMatch(context.Activity.Name)
                );
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
        public new EventRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
