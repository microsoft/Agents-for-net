﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules
{
    /// <summary>
    /// TaskModules class to enable fluent style registration of handlers related to Task Modules.
    /// </summary>
    public class TaskModulesFeature
    {
        private static readonly string FETCH_INVOKE_NAME = "task/fetch";
        private static readonly string SUBMIT_INVOKE_NAME = "task/submit";

        private static readonly string DEFAULT_TASK_DATA_FILTER = "verb";

        private readonly AgentApplication _app;
        private readonly TaskModulesOptions _taskModulesOptions;

        /// <summary>
        /// Creates a new instance of the TaskModules class.
        /// </summary>
        /// <param name="app"> The top level application class to register handlers with.</param>
        /// <param name="taskModulesOptions"></param>
        public TaskModulesFeature(AgentApplication app, TaskModulesOptions? taskModulesOptions = null)
        {
            this._app = app;
            this._taskModulesOptions = taskModulesOptions;
        }

        /// <summary>
        ///  Registers a handler to process the initial fetch of the task module.
        /// </summary>
        /// <param name="verb">Name of the verb to register the handler for.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetch(string verb, FetchHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(verb);
            ArgumentNullException.ThrowIfNull(handler);

            string filter = _taskModulesOptions?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelectorAsync routeSelector = CreateTaskSelector((string input) => string.Equals(verb, input), filter, FETCH_INVOKE_NAME);
            return OnFetch(routeSelector, handler);
        }

        /// <summary>
        ///  Registers a handler to process the initial fetch of the task module.
        /// </summary>
        /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetch(Regex verbPattern, FetchHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(verbPattern);
            ArgumentNullException.ThrowIfNull(handler);

            string filter = _taskModulesOptions?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelectorAsync routeSelector = CreateTaskSelector((string input) => verbPattern.IsMatch(input), filter, FETCH_INVOKE_NAME);
            return OnFetch(routeSelector, handler);
        }

        /// <summary>
        ///  Registers a handler to process the initial fetch of the task module.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetch(RouteSelectorAsync routeSelector, FetchHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                TaskModuleAction? taskModuleAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, FETCH_INVOKE_NAME)
                    || (taskModuleAction = ProtocolJsonSerializer.ToObject<TaskModuleAction>(turnContext.Activity.Value)) == null)
                {
                    throw new InvalidOperationException($"Unexpected TaskModules.OnFetch() triggered for activity type: {turnContext.Activity.Type}");
                }

                TaskModuleResponse result = await handler(turnContext, turnState, taskModuleAction.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };

            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        ///  Registers a handler to process the initial fetch of the task module.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetch(MultipleRouteSelector routeSelectors, FetchHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);

            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnFetch(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnFetch(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnFetch(routeSelector, handler);
                }
            }

            return _app;
        }

        /// <summary>
        /// Registers a handler to process the submission of a task module.
        /// </summary>
        /// <param name="verb">Name of the verb to register the handler for.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmit(string verb, SubmitHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(verb);
            ArgumentNullException.ThrowIfNull(handler);

            string filter = _taskModulesOptions?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelectorAsync routeSelector = CreateTaskSelector((string input) => string.Equals(verb, input), filter, SUBMIT_INVOKE_NAME);
            return OnSubmit(routeSelector, handler);
        }


        /// <summary>
        /// Registers a handler to process the submission of a task module.
        /// </summary>
        /// <param name="verbPattern">Regular expression to match against the verbs to register the handler for</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmit(Regex verbPattern, SubmitHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(verbPattern);
            ArgumentNullException.ThrowIfNull(handler);

            string filter = _taskModulesOptions?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelectorAsync routeSelector = CreateTaskSelector((string input) => verbPattern.IsMatch(input), filter, SUBMIT_INVOKE_NAME);
            return OnSubmit(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the submission of a task module.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmit(RouteSelectorAsync routeSelector, SubmitHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            RouteHandler routeHandler = async (ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) =>
            {
                TaskModuleAction? taskModuleAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, SUBMIT_INVOKE_NAME)
                    || (taskModuleAction = ProtocolJsonSerializer.ToObject<TaskModuleAction>(turnContext.Activity.Value)) == null)
                {
                    throw new InvalidOperationException($"Unexpected TaskModules.OnSubmit() triggered for activity type: {turnContext.Activity.Type}");
                }

                TaskModuleResponse result = await handler(turnContext, turnState, taskModuleAction.Value, cancellationToken);

                // Check to see if an invoke response has already been added
                if (!turnContext.StackState.Has(ChannelAdapter.InvokeResponseKey))
                {
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity(result);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };

            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the submission of a task module.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync verb(s) to register the handler for.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmit(MultipleRouteSelector routeSelectors, SubmitHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);

            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnSubmit(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnSubmit(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnSubmit(routeSelector, handler);
                }
            }

            return _app;
        }

        private static RouteSelectorAsync CreateTaskSelector(Func<string, bool> isMatch, string filter, string invokeName)
        {
            RouteSelectorAsync routeSelector = (ITurnContext turnContext, CancellationToken cancellationToken) =>
            {
                bool isInvoke = string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, invokeName);
                if (!isInvoke)
                {
                    return Task.FromResult(false);
                }

                if (turnContext.Activity.Value == null)
                {
                    return Task.FromResult(false);
                }

                var obj = ProtocolJsonSerializer.ToJsonElements(turnContext.Activity.Value);

                if (!obj.ContainsKey("data"))
                {
                    return Task.FromResult(false);
                }

                var data = JsonObject.Create(obj["data"]);

                bool isVerbMatch = data.TryGetPropertyValue(filter, out JsonNode filterField) && filterField.GetValueKind() == JsonValueKind.String
                    && isMatch(filterField.ToString());

                return Task.FromResult(isVerbMatch);
            };
            return routeSelector;
        }
    }
}
