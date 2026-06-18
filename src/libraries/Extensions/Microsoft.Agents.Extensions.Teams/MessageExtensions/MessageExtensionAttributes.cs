// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Attribute to define a route that handles Teams message extension query events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension query events in Teams.
/// The method must match the <see cref="QueryHandler"/> delegate signature.
/// <code>
/// [QueryRoute("searchProducts")]
/// public async Task&lt;Response&gt; OnSearchProductsAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
/// {
///     var keyword = query.Parameters.FirstOrDefault()?.Value ?? string.Empty;
///     var items = await _catalog.SearchAsync(keyword, cancellationToken);
///     var attachments = items.Select(i => i.ToHeroCard().ToMessagingExtensionAttachment()).ToList();
///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = attachments } };
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnQuery(string, QueryHandler)"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="commandId">The message extension command ID to match. Mutually exclusive with commandIdPattern.</param>
/// <param name="commandIdPattern">The regular expression pattern to match the message extension command ID.  Mutually exclusive with commandId.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<QueryHandler>(app, method);
        var builder = QueryRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            builder.WithCommand(commandId);
        }
        else if (!string.IsNullOrWhiteSpace(commandIdPattern))
        {
            builder.WithCommand(new Regex(commandIdPattern));
        }

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension query link (link unfurling) events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension link unfurling events in Teams.
/// The method must match the <see cref="QueryLinkHandler"/> delegate signature.
/// <code>
/// [QueryLinkRoute]
/// public async Task&lt;Response&gt; OnQueryLinkAsync(ITeamsTurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
/// {
///     var preview = await _service.FetchPreviewAsync(url, cancellationToken);
///     var attachment = preview.ToCard().ToMessagingExtensionAttachment();
///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [attachment] } };
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnQueryLink"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryLinkRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<QueryLinkHandler>(app, method);
        var builder = QueryLinkRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension anonymous query link (anonymous link unfurling) events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension anonymous query link events in Teams.
/// The method must match the <see cref="QueryLinkHandler"/> delegate signature.
/// <code>
/// [AnonQueryLinkRoute]
/// public async Task&lt;Response&gt; OnAnonQueryLinkAsync(ITeamsTurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
/// {
///     var preview = await _service.FetchPreviewAsync(url, cancellationToken);
///     var attachment = preview.ToCard().ToMessagingExtensionAttachment();
///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [attachment] } };
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnAnonymousQueryLink"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class AnonQueryLinkRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<QueryLinkHandler>(app, method);
        var builder = AnonQueryLinkRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension query URL setting events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension query URL setting events in Teams.
/// The method must match the <see cref="QuerySettingUrlHandler"/> delegate signature.
/// <code>
/// [QuerySettingUrlRoute]
/// public Task&lt;Response&gt; OnQuerySettingUrlAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     return Task.FromResult(new Response
///     {
///         ComposeExtension = new Result
///         {
///             Type = ResultType.Config,
///             SuggestedActions = new SuggestedActions
///             {
///                 Actions = [new CardAction { Type = "openUrl", Value = "https://example.com/config" }]
///             }
///         }
///     });
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnQuerySettingUrl"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QuerySettingUrlRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<QuerySettingUrlHandler>(app, method);
        var builder = QuerySettingUrlRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension <c>composeExtension/fetchTask</c> Invokes for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension <c>composeExtension/fetchTask</c> Invokes in Teams.
/// The method must match the <see cref="FetchActionHandler"/> delegate signature.
/// <code>
/// [FetchActionRoute("myCommand")]
/// public Task&lt;ActionResponse&gt; OnFetchTaskAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
/// {
///     return Task.FromResult(new ActionResponse
///     {
///         Task = new TaskInfo
///         {
///             Type = TaskInfoType.Continue,
///             Value = new TaskModuleTaskInfo { Title = "My Form", Height = 300, Width = 400, Url = "https://example.com/form" }
///         }
///     });
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnFetchAction(string, FetchActionHandler)"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="commandId">The message extension command ID to match. Mutually exclusive with commandIdPattern.</param>
/// <param name="commandIdPattern">The regular expression pattern to match the message extension command ID. Mutually exclusive with commandId.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FetchActionRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<FetchActionHandler>(app, method);
        var builder = FetchActionRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            builder.WithCommand(commandId);
        }
        else if (!string.IsNullOrWhiteSpace(commandIdPattern))
        {
            builder.WithCommand(new Regex(commandIdPattern));
        }

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension agent message preview edit events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension agent message preview edit events in Teams.
/// The method must match the <see cref="MessagePreviewEditHandler"/> delegate signature.
/// <code>
/// [MessagePreviewEditRoute("composeCmd")]
/// public Task&lt;Response&gt; OnMessagePreviewEditAsync(ITeamsTurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Re-open the compose form populated with the draft content
///     var draft = activityPreview.Attachments?.FirstOrDefault()?.Content;
///     return Task.FromResult(new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [BuildEditCard(draft)] } });
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnMessagePreviewEdit(string, MessagePreviewEditHandler)"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="commandId">The message extension command ID to match. Mutually exclusive with commandIdPattern.</param>
/// <param name="commandIdPattern">The regular expression pattern to match the message extension command ID. Mutually exclusive with commandId.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessagePreviewEditRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<MessagePreviewEditHandler>(app, method);
        var builder = MessagePreviewEditRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            builder.WithCommand(commandId);
        }
        else if (!string.IsNullOrWhiteSpace(commandIdPattern))
        {
            builder.WithCommand(new Regex(commandIdPattern));
        }

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension agent message preview send events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension agent message preview send events in Teams.
/// The method must match the <see cref="MessagePreviewSendHandler"/> delegate signature.
/// <code>
/// [MessagePreviewSendRoute("composeCmd")]
/// public async Task OnMessagePreviewSendAsync(ITeamsTurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Post the confirmed message to a channel or external system
///     var content = activityPreview.Attachments?.FirstOrDefault()?.Content;
///     await _channel.PostAsync(content, cancellationToken);
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnMessagePreviewSend(string, MessagePreviewSendHandler)"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="commandId">The message extension command ID to match.  Mutually exclusive with commandIdPattern.</param>
/// <param name="commandIdPattern">The message extension command ID pattern to match using regular expressions.  Mutually exclusive with commandId.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class MessagePreviewSendRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<MessagePreviewSendHandler>(app, method);
        var builder = MessagePreviewSendRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            builder.WithCommand(commandId);
        }
        else if (!string.IsNullOrWhiteSpace(commandIdPattern))
        {
            builder.WithCommand(new Regex(commandIdPattern));
        }

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension configure settings events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension configure settings events in Teams.
/// The method must match the <see cref="SettingHandler"/> delegate signature.
/// <code>
/// [SettingRoute]
/// public Task&lt;Response&gt; OnSettingAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
/// {
///     // Persist user settings and return an updated result
///     var setting = query.Parameters.FirstOrDefault()?.Value ?? string.Empty;
///     _settingsStore.Save(turnContext.Activity.From.Id, setting);
///     return Task.FromResult(new Response { ComposeExtension = new Result { Type = ResultType.Config } });
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnSetting"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SettingRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<SettingHandler>(app, method);
        var builder = SettingRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension submit action events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension submit action events in Teams.
/// The method must match the <see cref="SubmitActionHandler"/> delegate signature —
/// the third parameter must be <see cref="Microsoft.Teams.Api.MessageExtensions.Action"/>.
/// <code>
/// [SubmitActionRoute("createTask")]
/// public async Task&lt;Response&gt; OnCreateTaskAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
/// {
///     var task = await _taskService.CreateAsync(action.Data["title"]?.ToString(), action.Data["assignedTo"]?.ToString(), cancellationToken);
///     var card = task.ToAdaptiveCard().ToMessagingExtensionAttachment();
///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [card] } };
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnSubmitAction(string, SubmitActionHandler)"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="commandId">The message extension command ID to match. Mutually exclusive with commandIdPattern.</param>
/// <param name="commandIdPattern">The regular expression pattern to match the message extension command ID. Mutually exclusive with commandId.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SubmitActionRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = SubmitActionRouteBuilder.Create().AsAgentic(isAgenticOnly).WithOrderRank(rank);

        if (!string.IsNullOrWhiteSpace(commandId))
        {
            builder.WithCommand(commandId);
        }
        else if (!string.IsNullOrWhiteSpace(commandIdPattern))
        {
            builder.WithCommand(new Regex(commandIdPattern));
        }

        var handler = RouteAttributeHelper.CreateHandlerDelegate<SubmitActionHandler>(app, method);
        builder.WithHandler(handler);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension select item events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension select item events in Teams.
/// The method must match the <see cref="SelectItemHandler{TData}"/> delegate signature, where <c>TData</c> is inferred
/// from the method's third parameter type.
/// <code>
/// public record ProductSummary(string Id, string Name);
///
/// [SelectItemRoute]
/// public async Task&lt;Response&gt; OnSelectProductAsync(ITeamsTurnContext turnContext, ITurnState turnState, ProductSummary item, CancellationToken cancellationToken)
/// {
///     var details = await _catalog.GetDetailsAsync(item.Id, cancellationToken);
///     var card = details.ToHeroCard().ToMessagingExtensionAttachment();
///     return new Response { ComposeExtension = new Result { Type = ResultType.List, Attachments = [card] } };
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnSelectItem"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class SelectItemRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = SelectItemRouteBuilder.Create().AsAgentic(isAgenticOnly).WithOrderRank(rank);

        RouteAttributeHelper.InvokeGenericWithHandler(app, method, typeof(SelectItemHandler<>), 2, builder);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension card button clicked events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension card button click events in Teams.
/// The method must match the <see cref="CardButtonClickedHandler{TData}"/> delegate signature, where <c>TData</c> is inferred
/// from the method's third parameter type.
/// <code>
/// public record ApprovalAction(string ItemId, string Decision);
///
/// [CardButtonClickedRoute]
/// public async Task OnApprovalClickedAsync(ITeamsTurnContext turnContext, ITurnState turnState, ApprovalAction cardData, CancellationToken cancellationToken)
/// {
///     await _approvalService.RecordAsync(cardData.ItemId, cardData.Decision, cancellationToken);
///     await turnContext.SendActivityAsync($"Decision '{cardData.Decision}' recorded.", cancellationToken: cancellationToken);
/// }
/// </code>
/// Alternatively, <see cref="MessageExtension.OnCardButtonClicked"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class CardButtonClickedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var builder = CardButtonClickedRouteBuilder.Create().AsAgentic(isAgenticOnly).WithOrderRank(rank);

        RouteAttributeHelper.InvokeGenericWithHandler(app, method, typeof(CardButtonClickedHandler<>), 2, builder);

        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
