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

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// MessageExtensions class to enable fluent style registration of handlers related to Message Extensions.
/// </summary>
/// <remarks>
/// Creates a new instance of the MessageExtensions class.
/// </remarks> The top level application class to register handlers with.
public class MessageExtension
{
    private readonly AgentApplication _app;
    private readonly Core.Models.ChannelId _channelId;

    internal MessageExtension(AgentApplication app, Core.Models.ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    [Obsolete("OnSubmitAction(string, SubmitActionHandlerAsync) will be deprecated in future versions. Please use OnSubmitAction<TData>(string, SubmitActionHandlerAsync<TData>) instead for strongly-typed data handling.")]
    public MessageExtension OnSubmitAction(string commandId, SubmitActionHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        return OnSubmitAction<object>(commandId, (turnContext, turnState, data, cancellationToken) =>
        {
            return handler(turnContext, turnState, data, cancellationToken);
        });
    }

    /// <summary>
    /// Registers a handler that implements the submit action for an Action based Message Extension.
    /// </summary>
    /// <typeparam name="TData">The type of the data object that will be deserialized from the submit action payload.</typeparam>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnSubmitAction<TData>(string commandId, SubmitActionHandlerAsync<TData> handler)
    {
        AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.SubmitAction);
        return OnSubmitAction(routeSelector, handler);
    }

    [Obsolete("OnSubmitAction(Regex, SubmitActionHandlerAsync) will be deprecated in future versions. Please use OnSubmitAction<TData>(Regex, SubmitActionHandlerAsync<TData>) instead for strongly-typed data handling.")]
    public MessageExtension OnSubmitAction(Regex commandIdPattern, SubmitActionHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        return OnSubmitAction<object>(commandIdPattern, (turnContext, turnState, data, cancellationToken) =>
        {
            return handler(turnContext, turnState, data, cancellationToken);
        });
    }

    /// <summary>
    /// Registers a handler that implements the submit action for an Action based Message Extension.
    /// </summary>
    /// <typeparam name="TData">The type of the data object that will be deserialized from the submit action payload.</typeparam>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnSubmitAction<TData>(Regex commandIdPattern, SubmitActionHandlerAsync<TData> handler)
    {
        AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.SubmitAction);
        return OnSubmitAction(routeSelector, handler);
    }

    [Obsolete("OnSubmitAction(RouteSelector, SubmitActionHandlerAsync) will be deprecated in future versions. Please use OnSubmitAction<TData>(RouteSelector, SubmitActionHandlerAsync<TData>) instead for strongly-typed data handling.")]
    public MessageExtension OnSubmitAction(RouteSelector routeSelector, SubmitActionHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        return OnSubmitAction<object>(routeSelector, (turnContext, turnState, data, cancellationToken) =>
        {
            return handler(turnContext, turnState, data, cancellationToken);
        });
    }

    /// <summary>
    /// Registers a handler that implements the submit action for an Action based Message Extension.
    /// </summary>
    /// <typeparam name="TData">The type of the strongly-typed data object expected from the submit action payload.</typeparam>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming activity is not a valid Message Extension SubmitAction invoke or if the payload cannot be
    /// deserialized.</exception>
    public MessageExtension OnSubmitAction<TData>(RouteSelector routeSelector, SubmitActionHandlerAsync<TData> handler)
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

            Microsoft.Teams.Api.MessageExtensions.Response result = await handler(turnContext, turnState, ProtocolJsonSerializer.ToObject<TData>(messagingExtensionAction.Data), cancellationToken);

            await TeamsAgentExtension.SetResponse(turnContext, result);
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    /// <summary>
    /// Registers a handler to process the 'edit' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAgentMessagePreviewEdit(string commandId, BotMessagePreviewEditHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.SubmitAction, "edit");
        return OnAgentMessagePreviewEdit(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the 'edit' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAgentMessagePreviewEdit(Regex commandIdPattern, BotMessagePreviewEditHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.SubmitAction, "edit");
        return OnAgentMessagePreviewEdit(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the 'edit' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAgentMessagePreviewEdit(RouteSelector routeSelector, BotMessagePreviewEditHandlerAsync handler)
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

            Microsoft.Teams.Api.MessageExtensions.Response result = await handler(turnContext, turnState, messagingExtensionAction.BotActivityPreview[0].ToCoreActivity(), cancellationToken);
            await TeamsAgentExtension.SetResponse(turnContext, result);
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    /// <summary>
    /// Registers a handler to process the 'send' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAgentMessagePreviewSend(string commandId, BotMessagePreviewSendHandler handler)
    {
        AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.SubmitAction, "send");
        return OnAgentMessagePreviewSend(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the 'send' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAgentMessagePreviewSend(Regex commandIdPattern, BotMessagePreviewSendHandler handler)
    {
        AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.SubmitAction, "send");
        return OnAgentMessagePreviewSend(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the 'send' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAgentMessagePreviewSend(RouteSelector routeSelector, BotMessagePreviewSendHandler handler)
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

            IActivity activityPreview = messagingExtensionAction.BotActivityPreview.Count > 0 ? messagingExtensionAction.BotActivityPreview[0].ToCoreActivity() : new Activity();
            await handler(turnContext, turnState, activityPreview, cancellationToken);
            await TeamsAgentExtension.SetResponse(turnContext, new Response());
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <param name="commandId">ID of the commands to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchTask(string commandId, FetchTaskHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.FetchTask);
        return OnFetchTask(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the commands to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchTask(Regex commandIdPattern, FetchTaskHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.FetchTask);
        return OnFetchTask(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchTask(RouteSelector routeSelector, FetchTaskHandlerAsync handler)
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

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(string commandId, QueryHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(commandId, nameof(commandId));
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => string.Equals(commandId, input), Name.MessageExtensions.Query);
        return OnQuery(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(Regex commandIdPattern, QueryHandlerAsync handler)
    {
        AssertionHelpers.ThrowIfNull(commandIdPattern, nameof(commandIdPattern)); 
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        RouteSelector routeSelector = CreateSelector((string input) => commandIdPattern.IsMatch(input), Name.MessageExtensions.Query);
        return OnQuery(routeSelector, handler);
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(RouteSelector routeSelector, QueryHandlerAsync handler)
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

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    [Obsolete("OnSelectItem(SelectItemHandlerAsync) will be deprecated in future versions. Please use OnSelectItem<TData>(SelectItemHandlerAsync<TData>) instead for strongly-typed data handling.")]
    public MessageExtension OnSelectItem(SelectItemHandlerAsync handler)
    {
        return OnSelectItem<object>((turnContext, turnState, data, cancellationToken) =>
        {
            return handler(turnContext, turnState, data, cancellationToken);
        });
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
    /// <typeparam name="TData">The type of the data object expected from the SelectItem event payload.</typeparam>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnSelectItem<TData>(SelectItemHandlerAsync<TData> handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(turnContext.Activity.IsType(ActivityTypes.Invoke)
                && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.SelectItem));
        }

        async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            Microsoft.Teams.Api.MessageExtensions.Result result = await handler(turnContext, turnState, ProtocolJsonSerializer.ToObject<TData>(turnContext.Activity.Value), cancellationToken);
            await TeamsAgentExtension.SetResponse(turnContext, new Response()
            {
                ComposeExtension = result
            });
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Link Unfurling based Message Extension.
    /// </summary>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQueryLink(QueryLinkHandlerAsync handler)
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

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
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
    public MessageExtension OnAnonymousQueryLink(QueryLinkHandlerAsync handler)
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

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    /// <summary>
    /// Registers a handler that invokes the fetch of the configuration settings for a Message Extension.
    /// </summary>
    /// <remarks>
    /// The `composeExtension/querySettingUrl` INVOKE activity does not contain a command ID, so only a single select item handler can be registered.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQueryUrlSetting(QueryUrlSettingHandlerAsync handler)
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

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    [Obsolete("OnConfigureSettings(ConfigureSettingsHandlerAsync) will be deprecated in future versions. Please use OnConfigureSettings<TData>(ConfigureSettingsHandlerAsync<TData>) instead for strongly-typed data handling.")]
    public MessageExtension OnConfigureSettings(ConfigureSettingsHandler handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        return OnConfigureSettings<object>((turnContext, turnState, data, cancellationToken) =>
        {
            return handler(turnContext, turnState, data, cancellationToken);
        });
    }

    /// <summary>
    /// Registers a handler that implements the logic to invoke configuring Message Extension settings.
    /// </summary>
    /// <remarks>
    /// The <see cref="Name.MessageExtensions.Setting"/> INVOKE activity does not contain a command ID, so only a single select item handler can be registered.
    /// </remarks>
    /// <typeparam name="TData">The type of the settings data expected from the message extension settings event.</typeparam>
    /// <param name="handler">A delegate that processes the settings event. The handler receives the turn context, turn state, deserialized
    /// settings data of type TData, and a cancellation token. Cannot be null.</param>
    /// <returns>The current MessageExtension instance for method chaining.</returns>
    public MessageExtension OnConfigureSettings<TData>(ConfigureSettingsHandler<TData> handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.Setting));
        }

        async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await handler(turnContext, turnState, ProtocolJsonSerializer.ToObject<TData>(turnContext.Activity.Value), cancellationToken);
            await TeamsAgentExtension.SetResponse(turnContext, null);
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    [Obsolete("OnCardButtonClicked(CardButtonClickedHandler) will be deprecated in future versions. Please use OnCardButtonClicked<TData>(CardButtonClickedHandler<TData>) instead for strongly-typed data handling.")]
    public MessageExtension OnCardButtonClicked(CardButtonClickedHandler handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        return OnCardButtonClicked<object>((turnContext, turnState, data, cancellationToken) =>
        {
            return handler(turnContext, turnState, data, cancellationToken);
        });
    }

    /// <summary>
    /// Registers a handler that implements the logic when a user has clicked on a button in a Message Extension card.
    /// </summary>
    /// <remarks>
    /// The <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.CardButtonClicked"/> INVOKE activity does not contain any sort of command ID,
    /// so only a single select item handler can be registered. Developers will need to include a
    /// type name of some sort in the preview item they return if they need to support multiple select item handlers.
    /// </remarks>
    /// <typeparam name="TData">The type of the value payload expected from the card button click event.</typeparam>
    /// <param name="handler">A delegate that handles the card button click event. The delegate receives the turn context, turn state,
    /// deserialized value payload of type TData, and a cancellation token. Cannot be null.</param>
    /// <returns>The current AgentApplication instance for method chaining.</returns>
    public MessageExtension OnCardButtonClicked<TData>(CardButtonClickedHandler<TData> handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));

        Task<bool> routeSelector(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(turnContext.Activity.Name, Name.MessageExtensions.CardButtonClicked));
        }

        async Task routeHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await handler(turnContext, turnState, ProtocolJsonSerializer.ToObject<TData>(turnContext.Activity.Value), cancellationToken);
            await TeamsAgentExtension.SetResponse(turnContext, null);
        }

        _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
        return this;
    }

    private RouteSelector CreateSelector(Func<string, bool> isMatch, string invokeName, string? botMessagePreviewAction = default)
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

            bool isCommandMatch = obj.TryGetValue("commandId", out JsonElement commandId) && commandId.ValueKind == JsonValueKind.String && isMatch(commandId.ToString());

            bool isPreviewActionMatch = !obj.TryGetValue("botMessagePreviewAction", out JsonElement previewActionToken)
                || string.IsNullOrEmpty(previewActionToken.ToString())
                || string.Equals(botMessagePreviewAction, previewActionToken.ToString());

            return Task.FromResult(isCommandMatch && isPreviewActionMatch);
        }
        return routeSelector;
    }
}
