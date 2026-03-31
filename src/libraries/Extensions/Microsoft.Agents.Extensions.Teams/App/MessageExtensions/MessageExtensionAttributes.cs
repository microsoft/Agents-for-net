// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Attribute to define a route that handles Teams message extension query events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension query events in Teams.
/// The method must match the <see cref="QueryHandler"/> delegate signature.
/// <code>
/// [QueryRoute("myCommand")]
/// public async Task&lt;Response&gt; OnQueryAsync(ITurnContext turnContext, ITurnState turnState, Query query, CancellationToken cancellationToken)
/// {
///     // Handle query event
/// }
/// </code>
/// </remarks>
/// <param name="commandId">The message extension command ID to match.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryRouteAttribute(string commandId, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<QueryHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(QueryHandler), app);
#endif
        var builder = QueryRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension query link (link unfurling) events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension link unfurling events in Teams.
/// The method must match the <see cref="QueryLinkHandler"/> delegate signature.
/// <code>
/// [QueryLinkRoute]
/// public async Task&lt;Response&gt; OnQueryLinkAsync(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
/// {
///     // Handle query link event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryLinkRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<QueryLinkHandler>(app);
#else
        var handler = (QueryLinkHandler)method.CreateDelegate(typeof(QueryLinkHandler), app);
#endif
        var builder = QueryLinkRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension query URL setting events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension query URL setting events in Teams.
/// The method must match the <see cref="QueryUrlSettingHandler"/> delegate signature.
/// <code>
/// [QueryUrlSettingRoute]
/// public async Task&lt;Response&gt; OnQueryUrlSettingAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle query URL setting event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryUrlSettingRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<QueryUrlSettingHandler>(app);
#else
        var handler = (QueryUrlSettingHandler)method.CreateDelegate(typeof(QueryUrlSettingHandler), app);
#endif
        var builder = QueryUrlSettingRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension fetch task events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension fetch task events in Teams.
/// The method must match the <see cref="FetchTaskHandler"/> delegate signature.
/// <code>
/// [FetchTaskRoute("myCommand")]
/// public async Task&lt;MessageExtensions.ActionResponse&gt; OnFetchTaskAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle fetch task event
/// }
/// </code>
/// </remarks>
/// <param name="commandId">The message extension command ID to match.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FetchTaskRouteAttribute(string commandId, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<FetchTaskHandler>(app);
#else
        var handler = (FetchTaskHandler)method.CreateDelegate(typeof(FetchTaskHandler), app);
#endif
        var builder = FetchTaskRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension agent message preview edit events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension agent message preview edit events in Teams.
/// The method must match the <see cref="AgentMessagePreviewEditHandler"/> delegate signature.
/// <code>
/// [MessagePreviewEditRoute("myCommand")]
/// public async Task&lt;MessageExtensions.Response&gt; OnMessagePreviewEditAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Handle message preview edit event
/// }
/// </code>
/// </remarks>
/// <param name="commandId">The message extension command ID to match.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessagePreviewEditRouteAttribute(string commandId, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<AgentMessagePreviewEditHandler>(app);
#else
        var handler = (AgentMessagePreviewEditHandler)method.CreateDelegate(typeof(AgentMessagePreviewEditHandler), app);
#endif
        var builder = MessagePreviewEditRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension agent message preview send events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension agent message preview send events in Teams.
/// The method must match the <see cref="AgentMessagePreviewSendHandler"/> delegate signature.
/// <code>
/// [MessagePreviewSendRoute("myCommand")]
/// public async Task OnMessagePreviewSendAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Handle message preview send event
/// }
/// </code>
/// </remarks>
/// <param name="commandId">The message extension command ID to match.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessagePreviewSendRouteAttribute(string commandId, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<AgentMessagePreviewSendHandler>(app);
#else
        var handler = (AgentMessagePreviewSendHandler)method.CreateDelegate(typeof(AgentMessagePreviewSendHandler), app);
#endif
        var builder = MessagePreviewSendRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension configure settings events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension configure settings events in Teams.
/// The method must match the <see cref="ConfigureSettingsHandler"/> delegate signature.
/// <code>
/// [ConfigureSettingsRoute]
/// public async Task OnConfigureSettingsAsync(ITurnContext turnContext, ITurnState turnState, Query query, CancellationToken cancellationToken)
/// {
///     // Handle configure settings event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ConfigureSettingsRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<ConfigureSettingsHandler>(app);
#else
        var handler = (ConfigureSettingsHandler)method.CreateDelegate(typeof(ConfigureSettingsHandler), app);
#endif
        var builder = ConfigureSettingsRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension submit action events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension submit action events in Teams.
/// The method must match the <see cref="SubmitActionHandler{TData}"/> delegate signature, where <c>TData</c> is inferred
/// from the method's third parameter type.
/// <code>
/// [SubmitActionRoute("myCommand")]
/// public async Task&lt;MessageExtensions.Response&gt; OnSubmitActionAsync(ITurnContext turnContext, ITurnState turnState, MyData data, CancellationToken cancellationToken)
/// {
///     // Handle submit action event
/// }
/// </code>
/// </remarks>
/// <param name="commandId">The message extension command ID to match.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SubmitActionRouteAttribute(string commandId, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var genericParam = method.GetParameters()[2].ParameterType;
        var handlerType = typeof(SubmitActionHandler<>).MakeGenericType(genericParam);
        var handler = method.CreateDelegate(handlerType, app);

        var builder = SubmitActionRouteBuilder.Create().WithCommand(commandId).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        var withHandler = typeof(SubmitActionRouteBuilder).GetMethod("WithHandler").MakeGenericMethod(genericParam);
        withHandler.Invoke(builder, [handler]);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension select item events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension select item events in Teams.
/// The method must match the <see cref="SelectItemHandler{TData}"/> delegate signature, where <c>TData</c> is inferred
/// from the method's third parameter type.
/// <code>
/// [SelectItemRoute]
/// public async Task&lt;MessageExtensions.Response&gt; OnSelectItemAsync(ITurnContext turnContext, ITurnState turnState, MyItem item, CancellationToken cancellationToken)
/// {
///     // Handle select item event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SelectItemRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var genericParam = method.GetParameters()[2].ParameterType;
        var handlerType = typeof(SelectItemHandler<>).MakeGenericType(genericParam);
        var handler = method.CreateDelegate(handlerType, app);

        var builder = SelectItemRouteBuilder.Create().AsAgentic(isAgenticOnly).WithOrderRank(rank);

        var withHandler = typeof(SelectItemRouteBuilder).GetMethod("WithHandler").MakeGenericMethod(genericParam);
        withHandler.Invoke(builder, [handler]);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension card button clicked events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension card button click events in Teams.
/// The method must match the <see cref="CardButtonClickedHandler{TData}"/> delegate signature, where <c>TData</c> is inferred
/// from the method's third parameter type.
/// <code>
/// [CardButtonClickedRoute]
/// public async Task OnCardButtonClickedAsync(ITurnContext turnContext, ITurnState turnState, MyCardData cardData, CancellationToken cancellationToken)
/// {
///     // Handle card button clicked event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class CardButtonClickedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var genericParam = method.GetParameters()[2].ParameterType;
        var handlerType = typeof(CardButtonClickedHandler<>).MakeGenericType(genericParam);
        var handler = method.CreateDelegate(handlerType, app);

        var builder = CardButtonClickedRouteBuilder.Create().AsAgentic(isAgenticOnly).WithOrderRank(rank);

        var withHandler = typeof(CardButtonClickedRouteBuilder).GetMethod("WithHandler").MakeGenericMethod(genericParam);
        withHandler.Invoke(builder, [handler]);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
