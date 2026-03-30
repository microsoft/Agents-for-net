// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

/// <summary>
/// TaskModules class to enable fluent style registration of handlers related to Task Modules.
/// </summary>
public class TaskModule
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    public TaskModulesOptions Options { get; }

    internal TaskModule(AgentApplication app, ChannelId channelId, TaskModulesOptions? taskModulesOptions = null)
    {
        _app = app;
        _channelId = channelId;
        Options = taskModulesOptions;
    }

    /// <summary>
    ///  Registers a handler to process the initial fetch of the task module.
    /// </summary>
    /// <param name="verb">Name of the verb to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(string verb, FetchHandlerAsync handler)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithFilter(Options?.TaskDataFilter).WithVerb(verb).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the initial fetch of the task module.
    /// </summary>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(Regex verbPattern, FetchHandlerAsync handler)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithFilter(Options?.TaskDataFilter).WithVerb(verbPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the initial fetch of the task module.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(RouteSelector routeSelector, FetchHandlerAsync handler)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithFilter(Options?.TaskDataFilter).WithSelector(routeSelector).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <param name="verb">Name of the verb to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(string verb, SubmitHandlerAsync handler)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithFilter(Options?.TaskDataFilter).WithVerb(verb).WithHandler(handler).Build());
        return this;
    }


    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(Regex verbPattern, SubmitHandlerAsync handler)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithFilter(Options?.TaskDataFilter).WithVerb(verbPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(RouteSelector routeSelector, SubmitHandlerAsync handler)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithFilter(Options?.TaskDataFilter).WithSelector(routeSelector).WithHandler(handler).Build());
        return this;
    }
}
