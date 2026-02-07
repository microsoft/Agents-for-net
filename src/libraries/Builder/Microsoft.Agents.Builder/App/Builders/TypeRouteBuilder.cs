// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App.Builders
{
    /// <summary>
    /// RouteBuilder for routing activities of a specific type in an AgentApplication.
    /// </summary>
    public class TypeRouteBuilder : RouteBuilderBase<TypeRouteBuilder>
    {
        /// <summary>
        /// Configures the route to match activities of the specified type.
        /// </summary>
        /// <remarks>This method updates the route selector to filter activities based on the provided
        /// type. If the route is marked as agentic, only agentic requests will be considered for matching.</remarks>
        /// <param name="type">The activity type to match. Cannot be null or empty.</param>
        /// <returns>A <see cref="TypeRouteBuilder"/> instance configured to match the specified activity type.</returns>
        public TypeRouteBuilder WithType(string type)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
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
        /// <returns>The current instance of <see cref="TypeRouteBuilder"/> with the updated type matching configuration.</returns>
        public TypeRouteBuilder WithType(Regex typePattern)
        {
            _route.Selector = (context, ct) => Task.FromResult
                (
                    (!_route.Flags.HasFlag(RouteFlags.Agentic) || AgenticAuthorization.IsAgenticRequest(context))
                    && typePattern.IsMatch(context.Activity.Type)
                );
            return this;
        }
    }
}
