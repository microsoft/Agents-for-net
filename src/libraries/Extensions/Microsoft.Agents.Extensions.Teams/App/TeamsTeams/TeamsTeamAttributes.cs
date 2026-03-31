// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.App.TeamsTeams;

public static class TeamsTeamAttributes
{
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamArchivedRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().ForTeamArchived().WithHandler(handler).Build());
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamUnarchivedRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().ForTeamUnarchived().WithHandler(handler).Build());
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamDeletedRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().ForTeamDeleted().WithHandler(handler).Build());
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamHardDeletedRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().ForTeamHardDeleted().WithHandler(handler).Build());
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamRenamedRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().ForTeamRenamed().WithHandler(handler).Build());
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamRestoredRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().ForTeamRestored().WithHandler(handler).Build());
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
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamUpdateRouteAttribute() : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<TeamUpdateHandler>(app);
#else
            var handler = (TeamUpdateHandler)method.CreateDelegate(typeof(TeamUpdateHandler), app);
#endif

            app.AddRoute(TeamUpdateRouteBuilder.Create().WithHandler(handler).Build());
        }
    }
}
