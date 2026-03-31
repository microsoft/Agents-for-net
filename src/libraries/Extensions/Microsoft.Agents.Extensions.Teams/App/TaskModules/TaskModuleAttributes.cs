// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

public static class TaskModuleAttributes
{
    /// <summary>
    /// Attribute to define a route that handles Teams task module fetch events for a specific verb.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for task module fetch events in Teams.
    /// The method must match the <see cref="FetchHandler"/> delegate signature.
    /// <code>
    /// [FetchRoute("myVerb")]
    /// public async Task&lt;TaskModules.Response&gt; OnFetchAsync(ITurnContext turnContext, ITurnState turnState, TaskModules.Request data, CancellationToken cancellationToken)
    /// {
    ///     // Handle task module fetch event
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class FetchRouteAttribute(string verb) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<FetchHandler>(app);
#else
            var handler = (FetchHandler)method.CreateDelegate(typeof(FetchHandler), app);
#endif

            app.AddRoute(FetchRouteBuilder.Create().WithVerb(verb).WithHandler(handler).Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles Teams task module submit events for a specific verb.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for task module submit events in Teams.
    /// The method must match the <see cref="SubmitHandler"/> delegate signature.
    /// <code>
    /// [SubmitRoute("myVerb")]
    /// public async Task&lt;TaskModules.Response&gt; OnSubmitAsync(ITurnContext turnContext, ITurnState turnState, TaskModules.Request data, CancellationToken cancellationToken)
    /// {
    ///     // Handle task module submit event
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class SubmitRouteAttribute(string verb) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<SubmitHandler>(app);
#else
            var handler = (SubmitHandler)method.CreateDelegate(typeof(SubmitHandler), app);
#endif

            app.AddRoute(SubmitRouteBuilder.Create().WithVerb(verb).WithHandler(handler).Build());
        }
    }
}
