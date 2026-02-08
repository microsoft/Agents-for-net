// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing activities of a specific type in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TypeRouteBuilder"/> to create and configure routes that respond to event
    /// activities. This builder allows matching event activities by name or regular expression, and supports 
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Create"/> method 
    /// and further configured using one of <see cref="WithType(string)"/> or <see cref="WithType(Regex)"/> 
    /// or <see cref="WithSelector(RouteSelector)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = TypeRouteBuilder.Create()
    ///    .WithName("myInvoke")
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Invoke received!", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// Since this builder can't determine if this is for an Invoke Activity, the method <see cref="TypeRouteBuilder.AsInvoke(bool)"/> should be called if appropriate.
    /// </remarks>
    public class TypeRouteBuilder : RouteBuilderBase<TypeRouteBuilder>
    {
        /// <summary>
        /// Configures the route to match activities of the specified type.
        /// </summary>
        /// <remarks>This method updates the route selector to filter activities based on the provided
        /// type. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="type">The activity type to match. Cannot be null or empty.</param>
        /// <returns>A TypeRouteBuilder instance with the added selector for matching Activity.Type.</returns>
        public TypeRouteBuilder WithType(string type)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(type, nameof(type));

            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && context.Activity.IsType(type)
                );

            return this;
        }

        /// <summary>
        /// Configures the route to match activities whose type satisfies the specified regular expression pattern.
        /// </summary>
        /// <remarks>This method updates the route's selector to only match activities whose type matches
        /// the provided pattern. If the route is marked as agentic, it will also require the request to be agentic for
        /// the selector to return <see langword="true"/>.</remarks>
        /// <param name="typePattern">A regular expression used to determine whether the activity type should be matched by the route. Cannot be
        /// null.</param>
        /// <returns>A TypeRouteBuilder instance configured with the specified Activity.Type pattern selector.</returns>
        public TypeRouteBuilder WithType(Regex typePattern)
        {
            AssertionHelpers.ThrowIfNull(typePattern, nameof(typePattern));

            _route.Selector = (context, ct) => Task.FromResult
                (
                    IsContextMatch(context, _route)
                    && typePattern.IsMatch(context.Activity.Type)
                );

            return this;
        }

        /// <summary>
        /// Sets the route selector used to determine how incoming requests are matched to this route builder.
        /// </summary>
        /// <remarks>Use this method to customize the matching logic for routes. This allows for advanced
        /// routing scenarios where requests are selected based on custom rules or patterns.</remarks>
        /// <param name="selector">The route selector that defines the criteria for matching requests to the route. The supplied selector does
        /// not need to validate base route properties like ChannelId, Agentic, etc...</param>
        /// <returns>A TypeRouteBuilder instance configured with the specified custom selector.</returns>
        public override TypeRouteBuilder WithSelector(RouteSelector selector)
        {
            async Task<bool> ensureRouteMatch(ITurnContext context, CancellationToken cancellationToken)
            {
                return IsContextMatch(context, _route) && await selector(context, cancellationToken).ConfigureAwait(false);
            }

            _route.Selector = ensureRouteMatch;
            return this;
        }

        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current RouteBuilder instance with the handler set, enabling method chaining.</returns>
        public TypeRouteBuilder WithHandler(RouteHandler handler)
        {
            _route.Handler = handler;
            return (TypeRouteBuilder)this;
        }
    }
}
