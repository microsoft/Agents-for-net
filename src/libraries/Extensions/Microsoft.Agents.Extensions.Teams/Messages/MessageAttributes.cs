// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.Messages;

/// <summary>
/// Attribute to define a route that handles Teams message edit events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message edit events in Teams.
/// The decorated method must match the <see cref="RouteHandler"/> delegate signature.
/// <code>
/// [MessageEditRoute]
/// public async Task OnMessageEditAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle message edit event
/// }
/// </code>
/// Alternatively, <see cref="Message.OnMessageEdit"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessageEditRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
        var builder = MessageEditRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message undelete (undo soft-delete) events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message undelete events in Teams.
/// The decorated method must match the <see cref="RouteHandler"/> delegate signature.
/// <code>
/// [MessageUndeleteRoute]
/// public async Task OnMessageUndeleteAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle message undelete event
/// }
/// </code>
/// Alternatively, <see cref="Message.OnMessageUndelete"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessageUndeleteRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
        var builder = MessageUndeleteRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message soft-delete events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message soft-delete events in Teams.
/// The decorated method must match the <see cref="RouteHandler"/> delegate signature.
/// <code>
/// [MessageDeleteRoute]
/// public async Task OnMessageDeleteAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle message soft-delete event
/// }
/// </code>
/// Alternatively, <see cref="Message.OnMessageDelete"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessageDeleteRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
        var builder = MessageDeleteRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams read receipt events for messages sent by the agent in personal scope.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for read receipt events in Teams.
/// The decorated method must match the <see cref="ReadReceiptHandler"/> delegate signature —
/// the third parameter must be <see cref="System.Text.Json.JsonElement"/>.
/// <code>
/// [ReadReceiptRoute]
/// public async Task OnReadReceiptAsync(ITurnContext turnContext, ITurnState turnState, System.Text.Json.JsonElement data, CancellationToken cancellationToken)
/// {
///     // Handle read receipt event
/// }
/// </code>
/// Alternatively, <see cref="Message.OnReadReceipt"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ReadReceiptRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ReadReceiptHandler>(app, method);
        var builder = ReadReceiptRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams O365 Connector Card Action invoke activities.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for O365 Connector Card Action invokes in Teams.
/// The decorated method must match the <see cref="O365ConnectorCardActionHandler"/> delegate signature —
/// the third parameter must be <see cref="Microsoft.Teams.Api.O365.ConnectorCardActionQuery"/>.
/// <code>
/// [O365ConnectorCardActionRoute]
/// public async Task OnO365ConnectorCardActionAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.O365.ConnectorCardActionQuery query, CancellationToken cancellationToken)
/// {
///     // Handle O365 connector card action
/// }
/// </code>
/// Alternatively, <see cref="Message.OnO365ConnectorCardAction"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class O365ConnectorCardActionRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<O365ConnectorCardActionHandler>(app, method);
        var builder = O365ConnectorCardActionRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
