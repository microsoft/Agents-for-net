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
    private static readonly string DEFAULT_TASK_DATA_FILTER = "verb";

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
        AssertionHelpers.ThrowIfNull(verb, nameof(verb));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(verb, input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch);
        return OnFetch(routeSelector, handler);
    }

    /// <summary>
    ///  Registers a handler to process the initial fetch of the task module.
    /// </summary>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(Regex verbPattern, FetchHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
        RouteSelector routeSelector = CreateSelector((string input) => verbPattern.IsMatch(input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch);
        return OnFetch(routeSelector, handler);
    }

    /// <summary>
    ///  Registers a handler to process the initial fetch of the task module.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnFetch(RouteSelector routeSelector, FetchHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Microsoft.Teams.Api.TaskModules.Request? taskModuleAction;
            if (!turnContext.Activity.IsType(ActivityTypes.Invoke)
                || !string.Equals(turnContext.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch)
                || (taskModuleAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.TaskModules.Request>(turnContext.Activity.Value)) == null)
            {
                throw new InvalidOperationException($"Unexpected TaskModules.OnFetch() triggered for activity type: {turnContext.Activity.Type}");
            }

            var result = await handler(turnContext, turnState, taskModuleAction, cancellationToken);

            // Check to see if an invoke response has already been added
            if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
            {
                var activity = Activity.CreateInvokeResponseActivity(result);
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
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
        AssertionHelpers.ThrowIfNull(verb, nameof(verb));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(verb, input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Submit);
        return OnSubmit(routeSelector, handler);
    }


    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(Regex verbPattern, SubmitHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
        RouteSelector routeSelector = CreateSelector((string input) => verbPattern.IsMatch(input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Submit);
        return OnSubmit(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the submission of a task module.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public TaskModule OnSubmit(RouteSelector routeSelector, SubmitHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Microsoft.Teams.Api.TaskModules.Request? taskModuleAction;
            if (!turnContext.Activity.IsType(ActivityTypes.Invoke)
                || !string.Equals(turnContext.Activity.Name, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Submit)
                || (taskModuleAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.TaskModules.Request>(turnContext.Activity.Value)) == null)
            {
                throw new InvalidOperationException($"Unexpected TaskModules.OnSubmit() triggered for activity type: {turnContext.Activity.Type}");
            }

            var result = await handler(turnContext, turnState, taskModuleAction, cancellationToken);

            // Check to see if an invoke response has already been added
            if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
            {
                var activity = Activity.CreateInvokeResponseActivity(result);
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    private RouteSelector CreateSelector(Func<string, bool> isMatch, string filter, string invokeName)
    {
        Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId != _channelId)
            {
                return Task.FromResult(false);
            }

            bool isInvoke = turnContext.Activity.IsType(ActivityTypes.Invoke) && string.Equals(turnContext.Activity.Name, invokeName);
            if (!isInvoke)
            {
                return Task.FromResult(false);
            }

            if (turnContext.Activity.Value == null)
            {
                return Task.FromResult(false);
            }

            var obj = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);

            if (!obj.TryGetValue("data", out var dataNode))
            {
                return Task.FromResult(false);
            }

            var data = JsonObject.Create(obj["data"]);

            bool isVerbMatch = data.TryGetPropertyValue(filter, out JsonNode filterField) && filterField.GetValueKind() == JsonValueKind.String
                && isMatch(filterField.ToString());

            return Task.FromResult(isVerbMatch);
        }
        return routeSelector;
    }
}
