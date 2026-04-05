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
    ///  Registers a handler to process the fetch of the task module.  This will match the value of the indicated key in the task 
    ///  data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="TaskFetchRouteAttribute"/> can be used to decorate a <see cref="TaskFetchHandler"/> method for the same purpose.</remarks>
    /// <param name="value">The value of the key to match on.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(string value, TaskFetchHandler handler, string key = null)
    {
        _app.AddRoute(TaskFetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(value).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module. This will match the value of the indicated key in the task 
    ///  data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="TaskFetchRouteAttribute"/> can be used to decorate a <see cref="TaskFetchHandler"/> method for the same purpose.</remarks>
    /// <param name="valuePattern">Regular expression to match against the key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(Regex valuePattern, TaskFetchHandler handler, string key = null)
    {
        _app.AddRoute(TaskFetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(valuePattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module. This will match the value of the indicated key in the task
    /// data dictionary to determine if the handler should be triggered for a given submit request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="TaskSubmitRouteAttribute"/> can be used to decorate a <see cref="TaskSubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="value">The value of the key to match on. If null, this will match for any submit request.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(string value, TaskSubmitHandler handler, string key = null)
    {
        _app.AddRoute(TaskSubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(value).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module. This will match the value of the indicated key in the task data dictionary to
    /// determine if the handler should be triggered for a given submit request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="TaskSubmitRouteAttribute"/> can be used to decorate a <see cref="TaskSubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="valuePattern">Regular expression to match against the task data key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(Regex valuePattern, TaskSubmitHandler handler, string key = null)
    {
        _app.AddRoute(TaskSubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(valuePattern).WithHandler(handler).Build());
        return this;
    }
}
