// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Api.Activities.Invokes;
using System;
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
        _app.AddRoute(SubmitActionRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithHandler(handler).Build());
        return this;
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
        _app.AddRoute(SubmitActionRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithHandler(handler).Build());
        return this;
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
        _app.AddRoute(SubmitActionRouteBuilder.Create().WithChannelId(_channelId).WithSelector(routeSelector).WithHandler(handler).Build());
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
        _app.AddRoute(MessagePreviewEditRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithHandler(handler).Build());
        return this;
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
        _app.AddRoute(MessagePreviewEditRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithHandler(handler).Build());
        return this;
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
        _app.AddRoute(MessagePreviewEditRouteBuilder.Create().WithChannelId(_channelId).WithSelector(routeSelector).WithHandler(handler).Build());
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
        _app.AddRoute(MessagePreviewSendRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithHandler(handler).Build());
        return this;
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
        _app.AddRoute(MessagePreviewSendRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithHandler(handler).Build());
        return this;
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
        _app.AddRoute(MessagePreviewSendRouteBuilder.Create().WithChannelId(_channelId).WithSelector(routeSelector).WithHandler(handler).Build());
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
        _app.AddRoute(FetchTaskRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the commands to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchTask(Regex commandIdPattern, FetchTaskHandlerAsync handler)
    {
        _app.AddRoute(FetchTaskRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchTask(RouteSelector routeSelector, FetchTaskHandlerAsync handler)
    {
        _app.AddRoute(FetchTaskRouteBuilder.Create().WithChannelId(_channelId).WithSelector(routeSelector).WithHandler(handler).Build());
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
        _app.AddRoute(QueryRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(Regex commandIdPattern, QueryHandlerAsync handler)
    {
        _app.AddRoute(QueryRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
    /// <param name="handler">Function to call when the route is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(RouteSelector routeSelector, QueryHandlerAsync handler)
    {
        _app.AddRoute(QueryRouteBuilder.Create().WithChannelId(_channelId).WithSelector(routeSelector).WithHandler(handler).Build());
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
        _app.AddRoute(SelectItemRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Link Unfurling based Message Extension.
    /// </summary>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQueryLink(QueryLinkHandlerAsync handler)
    {
        _app.AddRoute(QueryLinkRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
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
        _app.AddRoute(AnonQueryLinkRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
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
        _app.AddRoute(QueryUrlSettingRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements the logic to invoke configuring Message Extension settings.
    /// </summary>
    /// <param name="handler">A delegate that processes the settings event. The handler receives the turn context, turn state, deserialized
    /// settings data of type <see cref="Microsoft.Teams.Api.MessageExtensions.Query"/>, and a cancellation token. Cannot be null.</param>
    /// <returns>The current MessageExtension instance for method chaining.</returns>
    public MessageExtension OnConfigureSettings(ConfigureSettingsHandler handler)
    {
        _app.AddRoute(ConfigureSettingsRouteBuilder.Create().WithChannelId(_channelId).WithHandler(handler).Build());
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
        _app.AddRoute(CardButtonClickedRouteBuilder.Create().WithChannelId(_channelId).WithHandler<TData>(handler).Build());
        return this;
    }
}
