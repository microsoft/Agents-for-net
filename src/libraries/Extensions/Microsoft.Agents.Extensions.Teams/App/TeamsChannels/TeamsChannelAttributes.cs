// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.App.TeamsChannels;

/// <summary>
/// Attribute to define a route that handles Teams channel created events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel created events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelCreatedRoute]
/// public async Task OnChannelCreatedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel created event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelCreatedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelCreated().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel deleted events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel deleted events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelDeletedRoute]
/// public async Task OnChannelDeletedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel deleted event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelDeletedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelDeleted().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel member added events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel member added events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelMemberAddedRoute]
/// public async Task OnChannelMemberAddedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel member added event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelMemberAddedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelMemberAdded().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel member removed events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel member removed events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelMemberRemovedRoute]
/// public async Task OnChannelMemberRemovedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel member removed event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelMemberRemovedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelMemberRemoved().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel renamed events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel renamed events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelRenamedRoute]
/// public async Task OnChannelRenamedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel renamed event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelRenamedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelRenamed().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel restored events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel restored events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelRestoredRoute]
/// public async Task OnChannelRestoredAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel restored event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelRestoredRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelRestored().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel shared events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel shared events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelSharedRoute]
/// public async Task OnChannelSharedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel shared event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelSharedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelShared().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams channel unshared events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for channel unshared events in Teams.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelUnSharedRoute]
/// public async Task OnChannelUnSharedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle channel unshared event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelUnSharedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().ForChannelUnShared().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles any Teams channel update event.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for all channel update events in Teams,
/// including created, deleted, renamed, restored, shared, unshared, member added, and member removed.
/// Use the specific event attributes (e.g., <see cref="ChannelCreatedRouteAttribute"/>) to handle individual event types.
/// The method must match the <see cref="ChannelUpdateHandler"/> delegate signature.
/// <code>
/// [ChannelUpdateRoute]
/// public async Task OnAnyChannelEventAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
/// {
///     // Handle any channel update event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ChannelUpdateRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ChannelUpdateHandler>(app, method);
        var builder = ChannelUpdateRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
