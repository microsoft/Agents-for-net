// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

/// <summary>
/// Attribute to define a route that handles Teams task module fetch events for a specific verb.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for task module fetch events in Teams.
/// The method must match the <see cref="FetchHandler"/> or <see cref="FetchHandler{TData}"/> delegate signature.
/// When the third parameter is <see cref="Microsoft.Teams.Api.TaskModules.Request"/>, the full request is passed.
/// When the third parameter is any other type, <c>Request.Data</c> is deserialized to that type and passed instead.
/// <code>
/// // Untyped — receives the full Request
/// [FetchRoute("myVerb")]
/// public async Task&lt;TaskModules.Response&gt; OnFetchAsync(ITurnContext turnContext, ITurnState turnState, TaskModules.Request request, CancellationToken cancellationToken)
/// {
///     // Handle task module fetch event
/// }
///
/// // Typed — Request.Data is deserialized to MyFetchData
/// [FetchRoute("myVerb")]
/// public async Task&lt;TaskModules.Response&gt; OnFetchAsync(ITurnContext turnContext, ITurnState turnState, MyFetchData data, CancellationToken cancellationToken)
/// {
///     // Handle task module fetch event with typed data
/// }
/// </code>
/// </remarks>
/// <param name="verb">The task module verb to match.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FetchRouteAttribute(string verb, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = FetchRouteBuilder.Create().WithVerb(verb).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (method.GetParameters()[2].ParameterType == typeof(Microsoft.Teams.Api.TaskModules.Request))
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<FetchHandler>(app, method);
            builder.WithHandler(handler);
        }
        else
        {
            var genericParam = method.GetParameters()[2].ParameterType;
            var handlerType = typeof(FetchHandler<>).MakeGenericType(genericParam);
            var handler = RouteAttributeHelper.CreateHandlerDelegate(app, method, handlerType);

            var withHandler = typeof(FetchRouteBuilder).GetMethods().First(m => m.Name == "WithHandler" && m.IsGenericMethodDefinition).MakeGenericMethod(genericParam);
            withHandler.Invoke(builder, [handler]);
        }

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams task module submit events for a specific verb.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for task module submit events in Teams.
/// The method must match the <see cref="SubmitHandler"/> or <see cref="SubmitHandler{TData}"/> delegate signature.
/// When the third parameter is <see cref="Microsoft.Teams.Api.TaskModules.Request"/>, the full request is passed.
/// When the third parameter is any other type, <c>Request.Data</c> is deserialized to that type and passed instead.
/// <code>
/// // Untyped — receives the full Request
/// [SubmitRoute("myVerb")]
/// public async Task&lt;TaskModules.Response&gt; OnSubmitAsync(ITurnContext turnContext, ITurnState turnState, TaskModules.Request request, CancellationToken cancellationToken)
/// {
///     // Handle task module submit event
/// }
///
/// // Typed — Request.Data is deserialized to MySubmitData
/// [SubmitRoute("myVerb")]
/// public async Task&lt;TaskModules.Response&gt; OnSubmitAsync(ITurnContext turnContext, ITurnState turnState, MySubmitData data, CancellationToken cancellationToken)
/// {
///     // Handle task module submit event with typed data
/// }
/// </code>
/// </remarks>
/// <param name="verb">The task module verb to match.</param>
/// <param name="verbProperty">The name of the verb property to filter on.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SubmitRouteAttribute(string verb, string verbProperty = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = SubmitRouteBuilder.Create().WithVerb(verb).WithTaskDataFilter(verbProperty).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (method.GetParameters()[2].ParameterType == typeof(Microsoft.Teams.Api.TaskModules.Request))
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<SubmitHandler>(app, method);
            builder.WithHandler(handler);
        }
        else
        {
            var genericParam = method.GetParameters()[2].ParameterType;
            var handlerType = typeof(SubmitHandler<>).MakeGenericType(genericParam);
            var handler = RouteAttributeHelper.CreateHandlerDelegate(app, method, handlerType);

            var withHandler = typeof(SubmitRouteBuilder).GetMethods().First(m => m.Name == "WithHandler" && m.IsGenericMethodDefinition).MakeGenericMethod(genericParam);
            withHandler.Invoke(builder, [handler]);
        }

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
