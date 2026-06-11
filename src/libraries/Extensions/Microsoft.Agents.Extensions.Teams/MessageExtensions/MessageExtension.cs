// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// MessageExtensions class to enable fluent style registration of handlers related to Message Extensions.
/// </summary>
public class MessageExtension
{
    private readonly AgentApplication _app;
    private readonly Core.Models.ChannelId _channelId;

    internal MessageExtension(AgentApplication app, Core.Models.ChannelId channelId)
    {
        _app = app;
        _channelId = channelId;
    }

    /// <summary>
    /// Registers a handler to process the 'edit' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnMessagePreviewEdit("composeCmd", (ctx, state, preview, ct) =>
    /// {
    ///     var draft = preview.Attachments?.FirstOrDefault()?.Content;
    ///     return Task.FromResult(new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [BuildEditCard(draft)] } });
    /// });
    /// </code>
    /// Alternatively, the <see cref="MessagePreviewEditRouteAttribute"/> can be used to decorate a <see cref="MessagePreviewEditHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnMessagePreviewEdit(string commandId, MessagePreviewEditHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessagePreviewEditRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the 'edit' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="MessagePreviewEditRouteAttribute"/> can be used to decorate a <see cref="MessagePreviewEditHandler"/> method for the same purpose.</remarks>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnMessagePreviewEdit(Regex commandIdPattern, MessagePreviewEditHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessagePreviewEditRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the 'send' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnMessagePreviewSend("composeCmd", async (ctx, state, preview, ct) =>
    /// {
    ///     var content = preview.Attachments?.FirstOrDefault()?.Content;
    ///     await _channel.PostAsync(content, ct);
    /// });
    /// </code>
    /// Alternatively, the <see cref="MessagePreviewSendRouteAttribute"/> can be used to decorate a <see cref="MessagePreviewSendHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnMessagePreviewSend(string commandId, MessagePreviewSendHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessagePreviewSendRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the 'send' action of a message that's being previewed by the
    /// user prior to sending.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="MessagePreviewSendRouteAttribute"/> can be used to decorate a <see cref="MessagePreviewSendHandler"/> method for the same purpose.</remarks>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnMessagePreviewSend(Regex commandIdPattern, MessagePreviewSendHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(MessagePreviewSendRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnFetchAction("myCommand", (ctx, state, ct) =>
    ///     Task.FromResult(new ActionResponse
    ///     {
    ///         Task = new TaskInfo
    ///         {
    ///             Type = TaskInfoType.Continue,
    ///             Value = new TaskModuleTaskInfo { Title = "My Form", Height = 300, Width = 400, Url = "https://example.com/form" }
    ///         }
    ///     }));
    /// </code>
    /// Alternatively, the <see cref="FetchActionRouteAttribute"/> can be used to decorate a <see cref="FetchActionHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="commandId">ID of the commands to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchAction(string commandId, FetchActionHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(FetchActionRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler to process the initial fetch task for an Action based message extension.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="FetchActionRouteAttribute"/> can be used to decorate a <see cref="FetchActionHandler"/> method for the same purpose.</remarks>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the commands to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnFetchAction(Regex commandIdPattern, FetchActionHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(FetchActionRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements the submit action for an Action based Message Extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// public record CreateTaskData(string Title, string AssignedTo);
    ///
    /// TeamsExtension.MessageExtensions.OnSubmitAction&lt;CreateTaskData&gt;("createTask", async (ctx, state, data, ct) =>
    /// {
    ///     var task = await _service.CreateAsync(data.Title, data.AssignedTo, ct);
    ///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [task.ToCard()] } };
    /// });
    /// </code>
    /// Alternatively, the <see cref="SubmitActionRouteAttribute"/> can be used to decorate a <see cref="SubmitActionHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnSubmitAction(string commandId, SubmitActionHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(SubmitActionRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements the submit action for an Action based Message Extension.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="SubmitActionRouteAttribute"/> can be used to decorate a <see cref="SubmitActionHandler"/> method for the same purpose.</remarks>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the commands to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnSubmitAction(Regex commandIdPattern, SubmitActionHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(SubmitActionRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnQuery("searchProducts", async (ctx, state, query, ct) =>
    /// {
    ///     var keyword = query.Parameters.FirstOrDefault()?.Value ?? string.Empty;
    ///     var items = await _catalog.SearchAsync(keyword, ct);
    ///     var attachments = items.Select(i => i.ToHeroCard().ToMessagingExtensionAttachment()).ToList();
    ///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = attachments } };
    /// });
    /// </code>
    /// Alternatively, the <see cref="QueryRouteAttribute"/> can be used to decorate a <see cref="QueryHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="commandId">ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(string commandId, QueryHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(QueryRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Search based Message Extension.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="QueryRouteAttribute"/> can be used to decorate a <see cref="QueryHandler"/> method for the same purpose.</remarks>
    /// <param name="commandIdPattern">Regular expression to match against the ID of the command to register the handler for.</param>
    /// <param name="handler">Function to call when the command is received.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQuery(Regex commandIdPattern, QueryHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(QueryRouteBuilder.Create().WithChannelId(_channelId).WithCommand(commandIdPattern).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements the logic to handle the tap actions for items returned
    /// by a Search based message extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnSelectItem&lt;ProductSummary&gt;(async (ctx, state, item, ct) =>
    /// {
    ///     var details = await _catalog.GetDetailsAsync(item.Id, ct);
    ///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [details.ToHeroCard().ToMessagingExtensionAttachment()] } };
    /// });
    /// </code>
    /// Alternatively, the <see cref="SelectItemRouteAttribute"/> can be used to decorate a <see cref="SelectItemHandler"/> method for the same purpose.
    /// </remarks>
    /// <typeparam name="TData">The type of the <c>data</c> argument on the handler.</typeparam>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnSelectItem<TData>(SelectItemHandler<TData> handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(SelectItemRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements a Link Unfurling based Message Extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnQueryLink(async (ctx, state, url, ct) =>
    /// {
    ///     var preview = await _service.FetchPreviewAsync(url, ct);
    ///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [preview.ToCard().ToMessagingExtensionAttachment()] } };
    /// });
    /// </code>
    /// Alternatively, the <see cref="QueryLinkRouteAttribute"/> can be used to decorate a <see cref="QueryLinkHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQueryLink(QueryLinkHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(QueryLinkRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements the logic to handle anonymous link unfurling.
    /// </summary>
    /// <remarks>Alternatively, the <see cref="AnonQueryLinkRouteAttribute"/> can be used to decorate a <see cref="QueryLinkHandler"/> method for the same purpose.</remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnAnonymousQueryLink(QueryLinkHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(AnonQueryLinkRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that invokes the fetch of the configuration settings for a Message Extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnQueryUrlSetting((ctx, state, ct) =>
    ///     Task.FromResult(new Response
    ///     {
    ///         ComposeExtension = new Result
    ///         {
    ///             Type = ResultType.Config,
    ///             SuggestedActions = new SuggestedActions
    ///             {
    ///                 Actions = [new CardAction { Type = "openUrl", Value = "https://example.com/config" }]
    ///             }
    ///         }
    ///     }));
    /// </code>
    /// Alternatively, the <see cref="QueryUrlSettingRouteAttribute"/> can be used to decorate a <see cref="QueryUrlSettingHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">Function to call when the event is triggered.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The application instance for chaining purposes.</returns>
    public MessageExtension OnQueryUrlSetting(QueryUrlSettingHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(QueryUrlSettingRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that processes the configure settings event for a Message Extension.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnConfigureSettings((ctx, state, query, ct) =>
    /// {
    ///     var setting = query.Parameters.FirstOrDefault()?.Value ?? string.Empty;
    ///     _settingsStore.Save(ctx.Activity.From.Id, setting);
    ///     return Task.FromResult(new Response { ComposeExtension = new Result { Type = ResultType.Config } });
    /// });
    /// </code>
    /// Alternatively, the <see cref="ConfigureSettingsRouteAttribute"/> can be used to decorate a <see cref="ConfigureSettingsHandler"/> method for the same purpose.
    /// </remarks>
    /// <param name="handler">A delegate that processes the settings event. The handler receives the turn context, turn state, deserialized
    /// settings data of type <see cref="Microsoft.Teams.Api.MessageExtensions.Query"/>, and a cancellation token. Cannot be null.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The current MessageExtension instance for method chaining.</returns>
    public MessageExtension OnConfigureSettings(ConfigureSettingsHandler handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(ConfigureSettingsRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }

    /// <summary>
    /// Registers a handler that implements the logic when a user has clicked on a button in a Message Extension card.
    /// </summary>
    /// <remarks>
    /// <code>
    /// TeamsExtension.MessageExtensions.OnCardButtonClicked&lt;ApprovalAction&gt;(async (ctx, state, cardData, ct) =>
    /// {
    ///     await _approvalService.RecordAsync(cardData.ItemId, cardData.Decision, ct);
    ///     await ctx.SendActivityAsync($"Decision '{cardData.Decision}' recorded.", cancellationToken: ct);
    /// });
    /// </code>
    /// Alternatively, the <see cref="CardButtonClickedRouteAttribute"/> can be used to decorate a <see cref="CardButtonClickedHandler"/> method for the same purpose.
    /// </remarks>
    /// <typeparam name="TData">The type of the <c>cardData</c> argument on the handler.</typeparam>
    /// <param name="handler">A delegate that handles the card button click event. The delegate receives the turn context, turn state,
    /// deserialized value payload of type TData, and a cancellation token. Cannot be null.</param>
    /// <param name="autoSignInHandlers">OAuth sign-in handler names for automatic sign-in before the route handler is invoked. Specify <see langword="null"/> to skip automatic sign-in.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <returns>The current <see cref="MessageExtension"/> instance for method chaining.</returns>
    public MessageExtension OnCardButtonClicked<TData>(CardButtonClickedHandler<TData> handler, string[] autoSignInHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _app.AddRoute(CardButtonClickedRouteBuilder.Create().WithChannelId(_channelId).WithOrderRank(rank).WithHandler<TData>(handler).WithOAuthHandlers(autoSignInHandlers).Build());
        return this;
    }
}
