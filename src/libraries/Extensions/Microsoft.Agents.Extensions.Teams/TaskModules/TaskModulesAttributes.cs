// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.TaskModules;

/// <summary>
/// Attribute to define a route that handles Teams task module fetch events.
/// The decorated method must match the <see cref="TaskFetchHandler"/> delegate signature —
/// the third parameter must be <see cref="Microsoft.Teams.Api.TaskModules.Request"/>.
/// </summary>
/// <remarks>
/// <code>
/// [TaskFetchRoute("myKey")]
/// public async Task&lt;TaskModules.Response&gt; OnFetchAsync(ITurnContext turnContext, ITurnState turnState, TaskModules.Request request, CancellationToken cancellationToken)
/// {
///     // Handle task module fetch event
/// }
/// </code>
/// </remarks>
/// <param name="value">The task module key value to match.  If null this will match for any fetch request.</param>
/// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TaskFetchRouteAttribute(string value = null, string key = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = TaskFetchRouteBuilder.Create()
            .WithKey(key)
            .WithValue(value)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);

        var handler = RouteAttributeHelper.CreateHandlerDelegate<TaskFetchHandler>(app, method);
        builder.WithHandler(handler);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams task module submit events.
/// The decorated method must match the <see cref="TaskSubmitHandler"/> delegate signature —
/// the third parameter must be <see cref="Microsoft.Teams.Api.TaskModules.Request"/>.
/// </summary>
/// <remarks>
/// <code>
/// [TaskSubmitRoute("myKey")]
/// public async Task&lt;TaskModules.Response&gt; OnSubmitAsync(ITurnContext turnContext, ITurnState turnState, TaskModules.Request request, CancellationToken cancellationToken)
/// {
///     // Handle task module submit event
/// }
/// </code>
/// </remarks>
/// <param name="value">The task module key value to match.  If null, this will match for any submit request.</param>
/// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TaskSubmitRouteAttribute(string value = null, string key = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = TaskSubmitRouteBuilder.Create()
            .WithKey(key)
            .WithValue(value)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);

        var handler = RouteAttributeHelper.CreateHandlerDelegate<TaskSubmitHandler>(app, method);
        builder.WithHandler(handler);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
