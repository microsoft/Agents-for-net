// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

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
