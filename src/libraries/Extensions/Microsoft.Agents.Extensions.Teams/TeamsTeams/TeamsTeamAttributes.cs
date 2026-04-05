// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.TeamsTeams;

/// <summary>
/// Attribute to define a route that handles Teams team archived events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for team archived events in Teams.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamArchivedRoute]
/// public async Task OnTeamArchivedAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle team archived event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamArchivedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().ForTeamArchived().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams team unarchived events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for team unarchived events in Teams.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamUnarchivedRoute]
/// public async Task OnTeamUnarchivedAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle team unarchived event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamUnarchivedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().ForTeamUnarchived().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams team deleted events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for team deleted events in Teams.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamDeletedRoute]
/// public async Task OnTeamDeletedAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle team deleted event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamDeletedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().ForTeamDeleted().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams team hard deleted events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for team hard deleted events in Teams.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamHardDeletedRoute]
/// public async Task OnTeamHardDeletedAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle team hard deleted event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamHardDeletedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().ForTeamHardDeleted().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams team renamed events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for team renamed events in Teams.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamRenamedRoute]
/// public async Task OnTeamRenamedAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle team renamed event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamRenamedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().ForTeamRenamed().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams team restored events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for team restored events in Teams.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamRestoredRoute]
/// public async Task OnTeamRestoredAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle team restored event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamRestoredRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().ForTeamRestored().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles any Teams team update event.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for all team update events in Teams,
/// including archived, unarchived, deleted, hard deleted, renamed, and restored.
/// Use the specific event attributes (e.g., <see cref="TeamArchivedRouteAttribute"/>) to handle individual event types.
/// The method must match the <see cref="TeamUpdateHandler"/> delegate signature.
/// <code>
/// [TeamUpdateRoute]
/// public async Task OnAnyTeamEventAsync(ITurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
/// {
///     // Handle any team update event
/// }
/// </code>
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TeamUpdateRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamUpdateHandler>(app, method);
        var builder = TeamUpdateRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
