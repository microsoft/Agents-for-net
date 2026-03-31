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
/// public async Task&lt;Result&gt; OnQueryAsync(ITurnContext turnContext, ITurnState turnState, Query query, CancellationToken cancellationToken)
/// {
///     // Handle query event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryRouteAttribute(string commandId) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<QueryHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(QueryHandler), app);
#endif

        app.AddRoute(QueryRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).Build());
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
/// public async Task&lt;Result&gt; OnQueryLinkAsync(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
/// {
///     // Handle query link event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryLinkRouteAttribute() : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<QueryLinkHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(QueryLinkHandler), app);
#endif

        app.AddRoute(QueryLinkRouteBuilder.Create().WithHandler(handler).Build());
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
/// public async Task&lt;Result&gt; OnQueryUrlSettingAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle query URL setting event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryUrlSettingRouteAttribute() : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<QueryUrlSettingHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(QueryUrlSettingHandler), app);
#endif

        app.AddRoute(QueryUrlSettingRouteBuilder.Create().WithHandler(handler).Build());
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
/// public async Task&lt;TaskModules.Response&gt; OnFetchTaskAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     // Handle fetch task event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FetchTaskRouteAttribute(string commandId) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<FetchTaskHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(FetchTaskHandler), app);
#endif

        app.AddRoute(FetchTaskRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension bot message preview edit events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension bot message preview edit events in Teams.
/// The method must match the <see cref="BotMessagePreviewEditHandler"/> delegate signature.
/// <code>
/// [MessagePreviewEditRoute("myCommand")]
/// public async Task&lt;MessageExtensions.Response&gt; OnMessagePreviewEditAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Handle message preview edit event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessagePreviewEditRouteAttribute(string commandId) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<BotMessagePreviewEditHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(BotMessagePreviewEditHandler), app);
#endif

        app.AddRoute(MessagePreviewEditRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension bot message preview send events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension bot message preview send events in Teams.
/// The method must match the <see cref="BotMessagePreviewSendHandler"/> delegate signature.
/// <code>
/// [MessagePreviewSendRoute("myCommand")]
/// public async Task OnMessagePreviewSendAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Handle message preview send event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessagePreviewSendRouteAttribute(string commandId) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<BotMessagePreviewSendHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(BotMessagePreviewSendHandler), app);
#endif

        app.AddRoute(MessagePreviewSendRouteBuilder.Create().WithCommand(commandId).WithHandler(handler).Build());
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
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ConfigureSettingsRouteAttribute() : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
#if !NETSTANDARD
        var handler = method.CreateDelegate<ConfigureSettingsHandler>(app);
#else
        var handler = (QueryHandler)method.CreateDelegate(typeof(ConfigureSettingsHandler), app);
#endif

        app.AddRoute(ConfigureSettingsRouteBuilder.Create().WithHandler(handler).Build());
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
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SubmitActionRouteAttribute(string commandId) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        // Get handler delegate
        var genericParam = method.GetParameters()[2].ParameterType;
        var handlerType = typeof(SubmitActionHandler<>).MakeGenericType(genericParam);
        var handler = method.CreateDelegate(handlerType, app);

        var builder = SubmitActionRouteBuilder.Create().WithCommand(commandId);

        // Invoke WithHandler
        var withHandler = typeof(SubmitActionRouteBuilder).GetMethod("WithHandler").MakeGenericMethod(genericParam);
        withHandler.Invoke(builder, [handler]);

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
/// public async Task&lt;MessageExtensions.Result&gt; OnSelectItemAsync(ITurnContext turnContext, ITurnState turnState, MyItem item, CancellationToken cancellationToken)
/// {
///     // Handle select item event
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SelectItemRouteAttribute() : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        // Get handler delegate
        var genericParam = method.GetParameters()[2].ParameterType;
        var handlerType = typeof(SelectItemHandler<>).MakeGenericType(genericParam);
        var handler = method.CreateDelegate(handlerType, app);

        var builder = SelectItemRouteBuilder.Create();

        // Invoke WithHandler
        var withHandler = typeof(SelectItemRouteBuilder).GetMethod("WithHandler").MakeGenericMethod(genericParam);
        withHandler.Invoke(builder, [handler]);

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
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class CardButtonClickedRouteAttribute() : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        // Get handler delegate
        var genericParam = method.GetParameters()[2].ParameterType;
        var handlerType = typeof(CardButtonClickedHandler<>).MakeGenericType(genericParam);
        var handler = method.CreateDelegate(handlerType, app);

        var builder = CardButtonClickedRouteBuilder.Create();

        // Invoke WithHandler
        var withHandler = typeof(CardButtonClickedRouteBuilder).GetMethod("WithHandler").MakeGenericMethod(genericParam);
        withHandler.Invoke(builder, [handler]);

        app.AddRoute(builder.Build());
    }
}
