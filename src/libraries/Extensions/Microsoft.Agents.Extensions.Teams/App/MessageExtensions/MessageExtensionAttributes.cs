// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Attribute to define a route that handles Teams message extension query events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension query events in Teams.
/// The method must match the <see cref="QueryHandler"/> delegate signature.
/// <code>
/// [QueryRoute("searchProducts")]
/// public async Task&lt;Response&gt; OnSearchProductsAsync(ITurnContext turnContext, ITurnState turnState, Query query, CancellationToken cancellationToken)
/// {
///     var keyword = query.Parameters.FirstOrDefault()?.Value ?? string.Empty;
///     var items = await _catalog.SearchAsync(keyword, cancellationToken);
///     var attachments = items.Select(i => i.ToHeroCard().ToMessagingExtensionAttachment()).ToList();
///     return Response.WithResult(new Result { Type = ResultType.List, Attachments = attachments });
/// }
/// </code>
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
/// public async Task&lt;Response&gt; OnQueryLinkAsync(ITurnContext turnContext, ITurnState turnState, string url, CancellationToken cancellationToken)
/// {
///     var preview = await _service.FetchPreviewAsync(url, cancellationToken);
///     var attachment = preview.ToCard().ToMessagingExtensionAttachment();
///     return Response.WithResult(new Result { Type = ResultType.List, Attachments = [attachment] });
/// }
/// </code>
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
/// Attribute to define a route that handles Teams message extension query URL setting events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension query URL setting events in Teams.
/// The method must match the <see cref="QueryUrlSettingHandler"/> delegate signature.
/// <code>
/// [QueryUrlSettingRoute]
/// public Task&lt;Response&gt; OnQueryUrlSettingAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
/// {
///     return ResponseTask.WithResult(new Result
///     {
///         Type = ResultType.Config,
///         SuggestedActions = new SuggestedActions
///         {
///             Actions = [new CardAction { Type = "openUrl", Value = "https://example.com/config" }]
///         }
///     });
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class QueryUrlSettingRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<QueryUrlSettingHandler>(app, method);
        var builder = QueryUrlSettingRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension fetch task events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension fetch task events in Teams.
/// The method must match the <see cref="FetchTaskHandler"/> delegate signature.
/// <code>
/// [FetchTaskRoute("myCommand")]
/// public Task&lt;ActionResponse&gt; OnFetchTaskAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
/// </remarks>
/// <param name="commandId">The message extension command ID to match. Mutually exclusive with commandIdPattern.</param>
/// <param name="commandIdPattern">The regular expression pattern to match the message extension command ID. Mutually exclusive with commandId.</param>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FetchTaskRouteAttribute(string commandId = null, string commandIdPattern = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<FetchTaskHandler>(app, method);
        var builder = FetchTaskRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);

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
/// The method must match the <see cref="AgentMessagePreviewEditHandler"/> delegate signature.
/// <code>
/// [MessagePreviewEditRoute("composeCmd")]
/// public Task&lt;Response&gt; OnMessagePreviewEditAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Re-open the compose form populated with the draft content
///     var draft = activityPreview.Attachments?.FirstOrDefault()?.Content;
///     return ResponseTask.WithResult(new Result { Type = ResultType.List, Attachments = [BuildEditCard(draft)] });
/// }
/// </code>
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
        var handler = RouteAttributeHelper.CreateHandlerDelegate<AgentMessagePreviewEditHandler>(app, method);
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
/// The method must match the <see cref="AgentMessagePreviewSendHandler"/> delegate signature.
/// <code>
/// [MessagePreviewSendRoute("composeCmd")]
/// public async Task OnMessagePreviewSendAsync(ITurnContext turnContext, ITurnState turnState, IActivity activityPreview, CancellationToken cancellationToken)
/// {
///     // Post the confirmed message to a channel or external system
///     var content = activityPreview.Attachments?.FirstOrDefault()?.Content;
///     await _channel.PostAsync(content, cancellationToken);
/// }
/// </code>
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
        var handler = RouteAttributeHelper.CreateHandlerDelegate<AgentMessagePreviewSendHandler>(app, method);
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
/// The method must match the <see cref="ConfigureSettingsHandler"/> delegate signature.
/// <code>
/// [ConfigureSettingsRoute]
/// public Task&lt;Response&gt; OnConfigureSettingsAsync(ITurnContext turnContext, ITurnState turnState, Query query, CancellationToken cancellationToken)
/// {
///     // Persist user settings and return an updated result
///     var setting = query.Parameters.FirstOrDefault()?.Value ?? string.Empty;
///     _settingsStore.Save(turnContext.Activity.From.Id, setting);
///     return ResponseTask.WithResult(new Result { Type = ResultType.Config });
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class ConfigureSettingsRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<ConfigureSettingsHandler>(app, method);
        var builder = ConfigureSettingsRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams message extension submit action events for a specific command.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for message extension submit action events in Teams.
/// The method must match the <see cref="SubmitActionHandler{TData}"/> delegate signature, where <c>TData</c> is inferred
/// from the method's third parameter type.
/// <code>
/// public record CreateTaskData(string Title, string AssignedTo);
///
/// [SubmitActionRoute("createTask")]
/// public async Task&lt;Response&gt; OnCreateTaskAsync(ITurnContext turnContext, ITurnState turnState, CreateTaskData data, CancellationToken cancellationToken)
/// {
///     var task = await _taskService.CreateAsync(data.Title, data.AssignedTo, cancellationToken);
///     var card = task.ToAdaptiveCard().ToMessagingExtensionAttachment();
///     return Response.WithResult(new Result { Type = ResultType.List, Attachments = [card] });
/// }
/// </code>
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

        if (method.GetParameters()[2].ParameterType == typeof(Microsoft.Teams.Api.MessageExtensions.Action))
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<SubmitActionHandler>(app, method);
            builder.WithHandler(handler);
        }
        else
        {
            RouteAttributeHelper.InvokeGenericWithHandler(app, method, typeof(SubmitActionHandler<>), 2, builder);
        }

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
/// public async Task&lt;Response&gt; OnSelectProductAsync(ITurnContext turnContext, ITurnState turnState, ProductSummary item, CancellationToken cancellationToken)
/// {
///     var details = await _catalog.GetDetailsAsync(item.Id, cancellationToken);
///     var card = details.ToHeroCard().ToMessagingExtensionAttachment();
///     return Response.WithResult(new Result { Type = ResultType.List, Attachments = [card] });
/// }
/// </code>
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
/// public async Task OnApprovalClickedAsync(ITurnContext turnContext, ITurnState turnState, ApprovalAction cardData, CancellationToken cancellationToken)
/// {
///     await _approvalService.RecordAsync(cardData.ItemId, cardData.Decision, cancellationToken);
///     await turnContext.SendActivityAsync($"Decision '{cardData.Decision}' recorded.", cancellationToken: cancellationToken);
/// }
/// </code>
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
