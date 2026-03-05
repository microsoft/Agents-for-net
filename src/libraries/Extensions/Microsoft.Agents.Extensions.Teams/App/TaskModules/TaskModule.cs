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

namespace Microsoft.Agents.Extensions.Teams.App.TaskModules
{
    /// <summary>
    /// TaskModules class to enable fluent style registration of handlers related to Task Modules.
    /// </summary>
    public class TaskModule
    {
        private static readonly string DEFAULT_TASK_DATA_FILTER = "verb";

        private readonly AgentApplication _app;

        public TaskModulesOptions Options { get; }

        /// <summary>
        /// Creates a new instance of the TaskModules class.
        /// </summary>
        /// <param name="app"> The top level application class to register handlers with.</param>
        /// <param name="taskModulesOptions"></param>
        public TaskModule(AgentApplication app, TaskModulesOptions? taskModulesOptions = null)
        {
            _app = app;
            Options = taskModulesOptions;
        }

        /// <summary>
        ///  Registers a handler to process the initial fetch of the task module.
        /// </summary>
        /// <param name="verb">Name of the verb to register the handler for.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetch(string verb, FetchHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(verb, nameof(verb));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(verb, input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch);
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
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelector routeSelector = CreateTaskSelector((string input) => verbPattern.IsMatch(input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Fetch);
            return OnFetch(routeSelector, handler);
        }

        /// <summary>
        ///  Registers a handler to process the initial fetch of the task module.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetch(RouteSelector routeSelector, FetchHandlerAsync handler)
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
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

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
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
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
            AssertionHelpers.ThrowIfNull(verb, nameof(verb));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(verb, input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Submit);
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
            AssertionHelpers.ThrowIfNull(verbPattern, nameof(verbPattern));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            string filter = Options?.TaskDataFilter ?? DEFAULT_TASK_DATA_FILTER;
            RouteSelector routeSelector = CreateTaskSelector((string input) => verbPattern.IsMatch(input), filter, Microsoft.Teams.Api.Activities.Invokes.Name.Tasks.Submit);
            return OnSubmit(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the submission of a task module.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmit(RouteSelector routeSelector, SubmitHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.TaskModules.Request? taskModuleAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
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
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

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
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnSubmit(routeSelector, handler);
                }
            }

            return _app;
        }

        private static RouteSelector CreateTaskSelector(Func<string, bool> isMatch, string filter, string invokeName)
        {
            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
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
}
