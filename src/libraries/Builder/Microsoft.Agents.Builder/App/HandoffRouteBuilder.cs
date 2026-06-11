// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// RouteBuilder for routing Handoff activities in an AgentApplication.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder"/> to create and configure routes that respond to activities of type 'invoke' with
    /// name "handoff/action". This builder allows matching event activities by name or regular expression, and supports
    /// channelId and agentic routing scenarios. Instances are created via the <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder.Create"/> method
    /// and further configured using <see cref="Microsoft.Agents.Builder.App.HandoffRouteBuilder.WithHandler(Microsoft.Agents.Builder.App.HandoffHandler)"/>.<br/><br/>
    /// Example usage:<br/><br/>
    /// <code>
    /// var route = HandoffRouteBuilder.Create()
    ///    .WithHandler(async (context, state, continuation, ct) => Task.FromResult(context.SendActivityAsync("Handoff action received", cancellationToken: ct)))
    ///    .Build();
    ///    
    /// app.AddRoute(route);
    /// </code>
    /// </remarks>
    public class HandoffRouteBuilder : HandoffRouteBuilderBase<HandoffRouteBuilder>
    {
        public HandoffRouteBuilder WithHandler(HandoffHandler handler)
        {
            return WithHandlerCore(handler);
        }
    }
}
