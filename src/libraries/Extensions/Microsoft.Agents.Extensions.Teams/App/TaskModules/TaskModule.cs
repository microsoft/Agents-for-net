// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules;

/// <summary>
/// TaskModules class to enable fluent style registration of handlers related to Task Modules.
/// </summary>
public class TaskModule
{
    private readonly AgentApplication _app;
    private readonly ChannelId _channelId;

    internal TaskModule(AgentApplication app, ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler"/> method for the same purpose.</remarks>
    /// <param name="verb">Name of the verb to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(string verb, FetchHandler handler, string taskDataFilter = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verb).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="verb">Name of the verb to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch<TData>(string verb, FetchHandler<TData> handler, string taskDataFilter = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verb).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler"/> method for the same purpose.</remarks>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(Regex verbPattern, FetchHandler handler, string taskDataFilter = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verbPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch<TData>(Regex verbPattern, FetchHandler<TData> handler, string taskDataFilter = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verbPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="verb">Name of the verb to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(string verb, SubmitHandler handler, string taskDataFilter = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verb).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="verb">Name of the verb to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit<TData>(string verb, SubmitHandler<TData> handler, string taskDataFilter = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verb).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(Regex verbPattern, SubmitHandler handler, string taskDataFilter = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verbPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="taskDataFilter">The JSON field name used to identify the verb in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit<TData>(Regex verbPattern, SubmitHandler<TData> handler, string taskDataFilter = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithTaskDataFilter(taskDataFilter).WithVerb(verbPattern).WithHandler(handler).Build());
        return this;
    }
}
