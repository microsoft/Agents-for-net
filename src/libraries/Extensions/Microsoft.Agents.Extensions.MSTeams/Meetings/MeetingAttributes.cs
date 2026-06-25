// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.MSTeams.Meetings;

/// <summary>
/// Attribute to define a route that handles Teams meeting start events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for meeting start events in Teams.
/// The method must match the <see cref="MeetingStartHandler"/> delegate signature.
/// <code>
/// [TeamsMeetingStartRoute]
/// public async Task OnMeetingStartAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Meetings.MeetingDetails meeting, CancellationToken cancellationToken)
/// {
///     // Handle meeting start event
/// }
/// </code>
/// Alternatively, <see cref="Meeting.OnStart"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
[RouteHandlerType(typeof(MeetingStartHandler))]
public class TeamsMeetingStartRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<MeetingStartHandler>(app, method);
        var builder = MeetingStartRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams meeting end events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for meeting end events in Teams.
/// The method must match the <see cref="MeetingEndHandler"/> delegate signature.
/// <code>
/// [TeamsMeetingEndRoute]
/// public async Task OnMeetingEndAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Meetings.MeetingDetails meeting, CancellationToken cancellationToken)
/// {
///     // Handle meeting end event
/// }
/// </code>
/// Alternatively, <see cref="Meeting.OnEnd"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
[RouteHandlerType(typeof(MeetingEndHandler))]
public class TeamsMeetingEndRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<MeetingEndHandler>(app, method);
        var builder = MeetingEndRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams meeting participants join events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for meeting participants join events in Teams.
/// The method must match the <see cref="MeetingParticipantsEventHandler"/> delegate signature.
/// <code>
/// [TeamsMeetingParticipantsJoinRoute]
/// public async Task OnParticipantsJoinAsync(ITeamsTurnContext turnContext, ITurnState turnState, MeetingParticipantsEventDetails participants, CancellationToken cancellationToken)
/// {
///     // Handle participants join event
/// }
/// </code>
/// Alternatively, <see cref="Meeting.OnParticipantsJoin"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
[RouteHandlerType(typeof(MeetingParticipantsEventHandler))]
public class TeamsMeetingParticipantsJoinRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<MeetingParticipantsEventHandler>(app, method);
        var builder = MeetingParticipantsJoinRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams meeting participants leave events.
/// </summary>
/// <remarks>
/// Decorate a method with this attribute to register it as a handler for meeting participants leave events in Teams.
/// The method must match the <see cref="MeetingParticipantsEventHandler"/> delegate signature.
/// <code>
/// [TeamsMeetingParticipantsLeaveRoute]
/// public async Task OnParticipantsLeaveAsync(ITeamsTurnContext turnContext, ITurnState turnState, MeetingParticipantsEventDetails participants, CancellationToken cancellationToken)
/// {
///     // Handle participants leave event
/// }
/// </code>
/// Alternatively, <see cref="Meeting.OnParticipantsLeave"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
[RouteHandlerType(typeof(MeetingParticipantsEventHandler))]
public class TeamsMeetingParticipantsLeaveRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<MeetingParticipantsEventHandler>(app, method);
        var builder = MeetingParticipantsLeaveRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
