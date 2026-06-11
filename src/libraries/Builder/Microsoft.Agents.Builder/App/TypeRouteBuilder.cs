// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing activities of a specific type in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TypeRouteBuilder"/> to create and configure routes that respond to activities
    /// of a particular type. This builder allows matching activities by type string or regular expression,
    /// and supports channelId and agentic routing scenarios. Instances are created via the <see cref="Create"/>
    /// method and optionally configured using <see cref="WithType(string)"/>, <see cref="WithType(Regex)"/>,
    /// or <see cref="WithSelector(RouteSelector)"/>. If neither <see cref="WithType(string)"/> nor
    /// <see cref="WithType(Regex)"/> is called, the route will match any activity type.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = TypeRouteBuilder.Create()
    ///    .WithType("myInvokeName")
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Invoke received!", cancellationToken: ct)))
    ///    .Build();
    ///
    /// app.AddRoute(route);
    /// </code>
    /// Since this builder can't determine if this is for an Invoke Activity, the method <see cref="TypeRouteBuilder.AsInvoke(bool)"/> should be called if appropriate.
    /// </remarks>
    public class TypeRouteBuilder : TypeRouteBuilderBase<TypeRouteBuilder>
    {
        public TypeRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return this;
        }
    }
}
