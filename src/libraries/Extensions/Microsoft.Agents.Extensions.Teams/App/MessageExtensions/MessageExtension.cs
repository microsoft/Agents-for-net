// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Activities.Invokes;
using Microsoft.Teams.Api.MessageExtensions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions
{
    /// <summary>
    /// MessageExtensions class to enable fluent style registration of handlers related to Message Extensions.
    /// </summary>
    /// <remarks>
    /// Creates a new instance of the MessageExtensions class.
    /// </remarks>
    /// <param name="app"></param> The top level application class to register handlers with.
    public class MessageExtension(AgentApplication app)
    {
        private readonly AgentApplication _app = app;

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(string commandId, SubmitActionHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.SubmitAction);
            return OnSubmitAction(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(Regex commandIdPattern, SubmitActionHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.SubmitAction);
            return OnSubmitAction(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(RouteSelector routeSelector, SubmitActionHandlerAsync handler)
        {
            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.MessageExtensions.Action? messagingExtensionAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, Name.MessageExtensions.SubmitAction)
                    || (messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(turnContext.Activity.Value)) == null)
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnSubmitAction() triggered for activity type: {turnContext.Activity.Type}");
                }

                Microsoft.Teams.Api.MessageExtensions.Response result = await handler(turnContext, turnState, messagingExtensionAction.Data, cancellationToken);

                await TeamsAgentExtension.SetResponse(turnContext, result);
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that implements the submit action for an Action based Message Extension.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSubmitAction(MultipleRouteSelector routeSelectors, SubmitActionHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnSubmitAction(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnSubmitAction(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnSubmitAction(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(string commandId, BotMessagePreviewEditHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.SubmitAction, "edit");
            return OnAgentMessagePreviewEdit(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(Regex commandIdPattern, BotMessagePreviewEditHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.SubmitAction, "edit");
            return OnAgentMessagePreviewEdit(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(RouteSelector routeSelector, BotMessagePreviewEditHandlerAsync handler)
        {
            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.MessageExtensions.Action? messagingExtensionAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, Name.MessageExtensions.SubmitAction)
                    || (messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(turnContext.Activity.Value)) == null
                    || !string.Equals(messagingExtensionAction.BotMessagePreviewAction, "edit"))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnAgentMessagePreviewEdit() triggered for activity type: {turnContext.Activity.Type}");
                }

                Microsoft.Teams.Api.MessageExtensions.Response result = await handler(turnContext, turnState, ProtocolJsonSerializer.CloneTo<IActivity>(messagingExtensionAction.BotActivityPreview[0]), cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, result);
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler to process the 'edit' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewEdit(MultipleRouteSelector routeSelectors, BotMessagePreviewEditHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnAgentMessagePreviewEdit(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnAgentMessagePreviewEdit(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnAgentMessagePreviewEdit(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(string commandId, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.SubmitAction, "send");
            return OnAgentMessagePreviewSend(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(Regex commandIdPattern, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.SubmitAction, "send");
            return OnAgentMessagePreviewSend(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(RouteSelector routeSelector, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.MessageExtensions.Action? messagingExtensionAction;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, Name.MessageExtensions.SubmitAction)
                    || (messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(turnContext.Activity.Value)) == null
                    || !string.Equals(messagingExtensionAction.BotMessagePreviewAction, "send"))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnAgentMessagePreviewSend() triggered for activity type: {turnContext.Activity.Type}");
                }

                IActivity activityPreview = messagingExtensionAction.BotActivityPreview.Count > 0 ? ProtocolJsonSerializer.CloneTo<IActivity>(messagingExtensionAction.BotActivityPreview[0]) : new Activity();
                await handler(turnContext, turnState, activityPreview, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, new Response());
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler to process the 'send' action of a message that's being previewed by the
        /// user prior to sending.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAgentMessagePreviewSend(MultipleRouteSelector routeSelectors, BotMessagePreviewSendHandler handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnAgentMessagePreviewSend(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnAgentMessagePreviewSend(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnAgentMessagePreviewSend(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="commandId">ID of the commands to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(string commandId, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.FetchTask);
            return OnFetchTask(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the commands to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(Regex commandIdPattern, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.FetchTask);
            return OnFetchTask(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(RouteSelector routeSelector, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, Name.MessageExtensions.FetchTask))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnFetchTask() triggered for activity type: {turnContext.Activity.Type}");
                }

                Microsoft.Teams.Api.TaskModules.Response result = await handler(turnContext, turnState, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, result);
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler to process the initial fetch task for an Action based message extension.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFetchTask(MultipleRouteSelector routeSelectors, FetchTaskHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnFetchTask(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnFetchTask(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnFetchTask(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="commandId">ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(string commandId, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.Query);
            return OnQuery(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
        /// <param name="handler">Function to call when the command is received.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(Regex commandIdPattern, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            RouteSelector routeSelector = CreateTaskSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.Query);
            return OnQuery(routeSelector, handler);
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(RouteSelector routeSelector, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelector, nameof(routeSelector));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.MessageExtensions.Query? messagingExtensionQuery = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Query>(turnContext.Activity.Value);
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, Name.MessageExtensions.Query)
                    || (messagingExtensionQuery == null))
                {
                    throw new InvalidOperationException($"Unexpected MessageExtensions.OnQuery() triggered for activity type: {turnContext.Activity.Type}");
                }

                Dictionary<string, object> parameters = [];
                foreach (Microsoft.Teams.Api.MessageExtensions.Parameter parameter in messagingExtensionQuery.Parameters)
                {
                    parameters.Add(parameter.Name, parameter.Value);
                }
                Query<IDictionary<string, object>> query = new(messagingExtensionQuery.QueryOptions.Count ?? 25, messagingExtensionQuery.QueryOptions.Skip ?? 0, parameters);

                Microsoft.Teams.Api.MessageExtensions.Result result = await handler(turnContext, turnState, query, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, new Response()
                {
                    ComposeExtension = result
                });
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that implements a Search based Message Extension.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQuery(MultipleRouteSelector routeSelectors, QueryHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(routeSelectors, nameof(routeSelectors));
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            if (routeSelectors.Strings != null)
            {
                foreach (string commandId in routeSelectors.Strings)
                {
                    OnQuery(commandId, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex commandIdPattern in routeSelectors.Regexes)
                {
                    OnQuery(commandIdPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelector routeSelector in routeSelectors.RouteSelectors)
                {
                    OnQuery(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Registers a handler that implements the logic to handle the tap actions for items returned
        /// by a Search based message extension.
        /// <remarks>
        /// The `composeExtension/selectItem` INVOKE activity does not contain any sort of command ID,
        /// so only a single select item handler can be registered. Developers will need to include a
        /// type name of some sort in the preview item they return if they need to support multiple
        /// select item handlers.
        /// </remarks>>
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnSelectItem(SelectItemHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.SelectItem));
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.MessageExtensions.Result result = await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, new Response()
                {
                    ComposeExtension = result
                });
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that implements a Link Unfurling based Message Extension.
        /// </summary>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQueryLink(QueryLinkHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.QueryLink));
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                AppBasedQueryLink? appBasedLinkQuery = ProtocolJsonSerializer.ToObject<AppBasedQueryLink>(turnContext.Activity.Value);
                Microsoft.Teams.Api.MessageExtensions.Result result = await handler(turnContext, turnState, appBasedLinkQuery!.Url, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, new Response()
                {
                    ComposeExtension = result
                }); 
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that implements the logic to handle anonymous link unfurling.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/anonymousQueryLink` INVOKE activity does not contain any sort of command ID,
        /// so only a single select item handler can be registered.
        /// For more information visit https://learn.microsoft.com/microsoftteams/platform/messaging-extensions/how-to/link-unfurling?#enable-zero-install-link-unfurling
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnAnonymousQueryLink(QueryLinkHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.AnonQueryLink));
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                AppBasedQueryLink? appBasedLinkQuery = ProtocolJsonSerializer.ToObject<AppBasedQueryLink>(turnContext.Activity.Value);
                Microsoft.Teams.Api.MessageExtensions.Result result = await handler(turnContext, turnState, appBasedLinkQuery!.Url, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, new Response()
                {
                    ComposeExtension = result
                }); 
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that invokes the fetch of the configuration settings for a Message Extension.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/querySettingUrl` INVOKE activity does not contain a command ID, so only a single select item handler can be registered.
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnQueryUrlSetting(QueryUrlSettingHandlerAsync handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.QuerySettingUrl));
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                Microsoft.Teams.Api.MessageExtensions.Result result = await handler(turnContext, turnState, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, new Response()
                {
                    ComposeExtension = result
                });
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that implements the logic to invoke configuring Message Extension settings.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/setting` INVOKE activity does not contain a command ID, so only a single select item handler can be registered.
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnConfigureSettings(ConfigureSettingsHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.Setting));
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, null);
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        /// <summary>
        /// Registers a handler that implements the logic when a user has clicked on a button in a Message Extension card.
        /// </summary>
        /// <remarks>
        /// The `composeExtension/onCardButtonClicked` INVOKE activity does not contain any sort of command ID,
        /// so only a single select item handler can be registered. Developers will need to include a
        /// type name of some sort in the preview item they return if they need to support multiple select item handlers.
        /// </remarks>
        /// <param name="handler">Function to call when the event is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnCardButtonClicked(CardButtonClickedHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));

            Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.CardButtonClicked));
            }

            async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
            {
                await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
                await TeamsAgentExtension.SetResponse(turnContext, null);
            }

            return _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        }

        private static RouteSelector CreateTaskSelector(Func<string, bool> isMatch, string invokeName, string? botMessagePreviewAction = default)
        {
            RouteSelector routeSelector = (turnContext, cancellationToken) =>
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

                bool isCommandMatch = obj.TryGetValue("commandId", out JsonElement commandId) && commandId.ValueKind == JsonValueKind.String && isMatch(commandId.ToString());

                bool isPreviewActionMatch = !obj.TryGetValue("botMessagePreviewAction", out JsonElement previewActionToken) 
                    || string.IsNullOrEmpty(previewActionToken.ToString())
                    || string.Equals(botMessagePreviewAction, previewActionToken.ToString());

                return Task.FromResult(isCommandMatch && isPreviewActionMatch);
            };
            return routeSelector;
        }
    }
}
