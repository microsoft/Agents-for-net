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
    /// RouteBuilder for routing Message activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.MessageRouteBuilder"/> to create and configure routes that respond to message
    /// activities. This builder allows matching event activities by text value or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.MessageRouteBuilder.Create"/> method
    /// and further configured using one of <see cref="Microsoft.Agents.Builder.App.MessageRouteBuilder.WithText(string)"/> or <see cref="Microsoft.Agents.Builder.App.MessageRouteBuilder.WithText(System.Text.RegularExpressions.Regex)"/>
    /// or <see cref="Microsoft.Agents.Builder.App.MessageRouteBuilder.WithSelector(Microsoft.Agents.Builder.App.RouteSelector)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = MessageRouteBuilder.Create()
    ///    .WithText("hello")
    ///    .WithHandler(async (context, state, ct) => Task.FromResult(context.SendActivityAsync("Hi!", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class MessageRouteBuilder : MessageRouteBuilderBase<MessageRouteBuilder>
    {
        /// <summary>
        /// Assigns the specified route handler to the current route and returns the updated builder instance.
        /// </summary>
        /// <param name="handler">The route handler to associate with the route. Cannot be null.</param>
        /// <returns>The current MessageRouteBuilder instance with the handler set, enabling method chaining.</returns>
        public MessageRouteBuilder WithHandler(RouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = handler;
            return this;
        }
    }
}
