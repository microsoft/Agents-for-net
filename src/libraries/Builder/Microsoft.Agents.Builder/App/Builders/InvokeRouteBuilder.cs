// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing Invoke activities in an AgentApplication.
    /// </summary>
    /// <remarks>Use this builder to define routing logic for activities of type 'invoke', such as those
    /// triggered by adaptive cards or other client-initiated operations. The builder allows specifying matching
    /// criteria based on the activity's name, enabling precise control over which invoke activities are handled by the
    /// route.</remarks>
    public class InvokeRouteBuilder : RouteBuilderBase<InvokeRouteBuilder>
    {
        public InvokeRouteBuilder() : base()
        {
            _route.Flags |= RouteFlags.Invoke;
        }

        /// <summary>
        /// Configures the route to match only invoke activities with the specified name.
        /// </summary>
        /// <remarks>This method restricts the route to handle only invoke activities whose <c>Name</c>
        /// property matches the specified value. If the route is marked as agentic, the request must also be authorized
        /// as agentic to match.</remarks>
        /// <param name="text">The name of the invoke activity to match. Comparison is case-insensitive.</param>
        /// <returns>The current <see cref="InvokeRouteBuilder"/> instance for method chaining.</returns>
        public InvokeRouteBuilder WithName(string text)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && context.Activity.IsType(ActivityTypes.Invoke)
                    && context.Activity?.Name != null
                    && context.Activity.Name.Equals(text, StringComparison.OrdinalIgnoreCase)
                );
            return this;
        }

        /// <summary>
        /// Configures the route to match invoke activities whose name matches the specified regular expression pattern.
        /// </summary>
        /// <remarks>This method restricts the route to handle only invoke activities with names matching
        /// the provided pattern. If the route is marked as agentic, it will only match agentic requests. Use this
        /// method to filter invoke activities based on their name when defining route handlers.</remarks>
        /// <param name="textPattern">A regular expression used to match the name of incoming invoke activities. Only activities with a name that
        /// matches this pattern will be handled by the route.</param>
        /// <returns>The current <see cref="InvokeRouteBuilder"/> instance for method chaining.</returns>
        public InvokeRouteBuilder WithName(Regex textPattern)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && context.Activity.IsType(ActivityTypes.Invoke)
                    && context.Activity?.Name != null
                    && textPattern.IsMatch(context.Activity.Name)
                );
            return this;
        }

        /// <summary>
        /// Returns the current route builder instance configured for Invoke routing. This method ensures that the route
        /// remains set as an Invoke route.
        /// </summary>
        /// <remarks>This override prevents changing the route configuration from Invoke routing,
        /// maintaining consistency with the route's initial setup.</remarks>
        /// <param name="isInvoke">A value indicating whether the route should be treated as an Invoke route. The parameter is ignored, as the
        /// route is always configured for Invoke routing.</param>
        /// <returns>The current instance of <see cref="InvokeRouteBuilder"/> with Invoke routing enabled.</returns>
        public new InvokeRouteBuilder AsInvoke(bool isInvoke = true)
        {
            return this;
        }
    }
}
