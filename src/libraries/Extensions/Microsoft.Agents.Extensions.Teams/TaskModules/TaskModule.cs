// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.TaskModules;

/// <summary>
/// Provides methods for registering handlers for Teams Task Module fetch and submit events.
/// </summary>
/// <remarks>
/// <para>
/// Task Module routing uses a key/value pair embedded in the activity data to identify which handler
/// to invoke. When Teams delivers a fetch or submit request, the SDK reads a named field from the
/// activity data and compares its value against the registered handlers.
/// </para>
/// <para>
/// The default field name is <c>"task"</c>. The value of that field (for example, <c>"simple_form"</c>)
/// determines which <c>OnFetch</c> or <c>OnSubmit</c> handler is called. The field name can be overridden
/// per handler using the <c>key</c> parameter, but it is strongly recommended to keep the default
/// <c>"task"</c> for both fetch and submit — varying only the value — to keep routing consistent and simple.
/// </para>
/// <para>
/// For example, if a card's submit action data contains <c>{ "task": "simple_form" }</c>, then the handler
/// registered with <c>OnFetch("simple_form", ...)</c> is called for the fetch and
/// <c>OnSubmit("simple_form", ...)</c> is called when the user submits the form.
/// </para>
/// </remarks>
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
    /// Registers a handler to process the fetch of a task module. The value of the <c>"task"</c> key
    /// (or the specified <paramref name="key"/>) in the activity data is matched against
    /// <paramref name="value"/> to determine whether this handler should be triggered.
    /// </summary>
    /// <remarks>
    /// <para>Alternatively, the <see cref="TaskFetchRouteAttribute"/> can be used to decorate a
    /// <see cref="TaskFetchHandler"/> method for the same purpose.</para>
    /// <para>The following example opens a simple Adaptive Card form inside a task module dialog.
    /// The card's submit action includes <c>"task": "simple_form"</c> so that the matching submit
    /// handler is triggered when the user submits the form.</para>
    /// <code>
    /// [TaskFetchRoute("simple_form")]
    /// public Task&lt;Microsoft.Teams.Api.TaskModules.Response&gt; OnSimpleFormFetchAsync(
    ///     ITurnContext turnContext, ITurnState turnState,
    ///     Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
    /// {
    ///     // Simple Adaptive Card form.  The submit action data includes "task": "simple_form"
    ///     // so the OnSimpleFormSubmitAsync handler below is called when the user submits.
    ///     const string cardJson = """
    ///         {
    ///           "type": "AdaptiveCard",
    ///           "version": "1.4",
    ///           "body": [{ "type": "Input.Text", "id": "name", "label": "Name" }],
    ///           "actions": [{
    ///             "type": "Action.Submit",
    ///             "title": "Submit",
    ///             "data": { "task": "simple_form" }
    ///           }]
    ///         }
    ///         """;
    ///     return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response(
    ///         new Microsoft.Teams.Api.TaskModules.ContinueTask(
    ///             new Microsoft.Teams.Api.TaskModules.TaskInfo
    ///             {
    ///                 Card = new Microsoft.Teams.Api.Attachment(ContentTypes.AdaptiveCard, cardJson),
    ///                 Title = "Simple Form",
    ///                 Height = new Microsoft.Teams.Common.Union&lt;int, Microsoft.Teams.Api.TaskModules.Size&gt;(Microsoft.Teams.Api.TaskModules.Size.Small),
    ///                 Width = new Microsoft.Teams.Common.Union&lt;int, Microsoft.Teams.Api.TaskModules.Size&gt;(Microsoft.Teams.Api.TaskModules.Size.Small)
    ///             })));
    /// }
    /// </code>
    /// </remarks>
    /// <param name="value">The value of the key to match on.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(string value, TaskFetchHandler handler, string key = null, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(TaskFetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(value).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the fetch of a task module. The value of the <c>"task"</c> key
    /// (or the specified <paramref name="key"/>) in the activity data is matched against
    /// <paramref name="valuePattern"/> to determine whether this handler should be triggered.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="TaskFetchRouteAttribute"/> can be used to decorate a <see cref="TaskFetchHandler"/> method for the same purpose.</remarks>
    /// <param name="valuePattern">Regular expression to match against the key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(Regex valuePattern, TaskFetchHandler handler, string key = null, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(TaskFetchRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(valuePattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module. The value of the <c>"task"</c> key
    /// (or the specified <paramref name="key"/>) in the activity data is matched against
    /// <paramref name="value"/> to determine whether this handler should be triggered.
    /// </summary>
    /// <remarks>
    /// <para>Alternatively, the <see cref="TaskSubmitRouteAttribute"/> can be used to decorate a
    /// <see cref="TaskSubmitHandler"/> method for the same purpose.</para>
    /// <para>The following example reads a field from the submitted form data and returns a completion message.</para>
    /// <code>
    /// [TaskSubmitRoute("simple_form")]
    /// public async Task&lt;Microsoft.Teams.Api.TaskModules.Response&gt; OnSimpleFormSubmitAsync(
    ///     ITurnContext turnContext, ITurnState turnState,
    ///     Microsoft.Teams.Api.TaskModules.Request request, CancellationToken cancellationToken)
    /// {
    ///     var name = request.GetDataString("name", "Unknown");
    ///     await turnContext.SendActivityAsync($"Hi {name}, thanks for submitting the form!", cancellationToken: cancellationToken);
    ///     return new Microsoft.Teams.Api.TaskModules.Response(
    ///         new Microsoft.Teams.Api.TaskModules.MessageTask("Form was submitted"));
    /// }
    /// </code>
    /// </remarks>
    /// <param name="value">The value of the key to match on. If null, this will match for any submit request.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(string value, TaskSubmitHandler handler, string key = null, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(TaskSubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(value).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module. The value of the <c>"task"</c> key
    /// (or the specified <paramref name="key"/>) in the activity data is matched against
    /// <paramref name="valuePattern"/> to determine whether this handler should be triggered.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="TaskSubmitRouteAttribute"/> can be used to decorate a <see cref="TaskSubmitHandler"/> method for the same purpose.</remarks>
    /// <param name="valuePattern">Regular expression to match against the task data key value.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <param name="key">The JSON field name used to identify the key in the task data. Defaults to <c>"task"</c> if not specified.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(Regex valuePattern, TaskSubmitHandler handler, string key = null, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(TaskSubmitRouteBuilder.Create().WithChannelId(_channelId).WithKey(key).WithValue(valuePattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }
}
