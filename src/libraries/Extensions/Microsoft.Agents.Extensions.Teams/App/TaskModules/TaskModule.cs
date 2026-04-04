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
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler"/> method for the same purpose.</remarks>
    /// <param name="keyValue">The value of the key to match on.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(string keyValue, FetchHandler handler, string keyName = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValue).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>. This 
    ///  will match the value of the indicated key in the task data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="keyValue">The value of the key to match on. If null, this will match for any fetch request.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch<TData>(string keyValue, FetchHandler<TData> handler, string keyName = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValue).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module. This will match the value of the indicated key in the task 
    ///  data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler"/> method for the same purpose.</remarks>
    /// <param name="keyValuePattern">Regular expression to match against the key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(Regex keyValuePattern, FetchHandler handler, string keyName = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValuePattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    ///  Registers a handler to process the fetch of the task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>. This 
    ///  will match the value of the indicated key in the task data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchRouteAttribute"/> can be used to decorate a <see cref="FetchHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="keyValuePattern">Regular expression to match against the key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch<TData>(Regex keyValuePattern, FetchHandler<TData> handler, string keyName = null)
    {
        _app.AddRoute(FetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValuePattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module. This will match the value of the indicated key in the task 
    /// data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="keyValue">The value of the key to match on. If null, this will match for any submit request.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(string keyValue, SubmitHandler handler, string keyName = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValue).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>. This 
    /// will match the value of the indicated key in the task data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="keyValue">The value of the key to match on. If null, this will match for any submit request.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit<TData>(string keyValue, SubmitHandler<TData> handler, string keyName = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValue).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module. This will match the value of the indicated key in the task data dictionary to 
    /// determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="keyValuePattern">Regular expression to match against the task data key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(Regex keyValuePattern, SubmitHandler handler, string keyName = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValuePattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module, with <c>Request.Data</c> deserialized to <typeparamref name="TData"/>. This 
    /// will match the value of the indicated key in the task data dictionary to determine if the handler should be triggered for a given fetch request.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitRouteAttribute"/> can be used to decorate a <see cref="SubmitHandler{TData}"/> method for the same purpose.</remarks>
    /// <typeparam name="TData">The type to deserialize <c>Request.Data</c> into and pass to the handler.</typeparam>
    /// <param name="keyValuePattern">Regular expression to match against the task data key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="keyName">The JSON field name used to identify the key in the task data. Defaults to <c>"verb"</c> if not specified.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit<TData>(Regex keyValuePattern, SubmitHandler<TData> handler, string keyName = null)
    {
        _app.AddRoute(SubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(keyName).WithKeyValue(keyValuePattern).WithHandler(handler).Build());
        return this;
    }
}
