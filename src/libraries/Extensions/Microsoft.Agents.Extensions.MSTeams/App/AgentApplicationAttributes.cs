// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.MSTeams.App
{
    /// <summary>
    /// Attribute to define a route that handles activities matching a specific type or type pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for activities of the specified type.
    /// Provide either <paramref name="type"/> for an exact match or <paramref name="typeRegex"/> for a pattern match; they are mutually exclusive.
    /// When neither is provided the route matches any activity type and defaults to <see cref="RouteRank.Last"/>.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// // Match by exact type
    /// [TeamsActivityRoute(ActivityTypes.Event)]
    /// public async Task OnEventAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any event activity
    /// }
    ///
    /// // Match by type pattern
    /// [TeamsActivityRoute(typeRegex: "event|invoke")]
    /// public async Task OnEventOrInvokeAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle event or invoke activities
    /// }
    ///
    /// // Match any activity type (fires last, after all specific routes)
    /// [TeamsActivityRoute]
    /// public async Task OnAnyAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any unmatched activity
    /// }
    /// </code>
    /// </remarks>
    /// <param name="type">The exact activity <see cref="IActivity.Type"/> to match, e.g. <see cref="ActivityTypes"/>. Mutually exclusive with <paramref name="typeRegex"/>.</param>
    /// <param name="typeRegex">A regular expression pattern matched against <see cref="IActivity.Type"/>. Mutually exclusive with <paramref name="type"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. When no type filter is specified, defaults to <see cref="RouteRank.Last"/> so specific-type routes take priority.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsActivityRouteAttribute(string type = null, string typeRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var builder = TeamsTypeRouteBuilder.Create();
            if (!string.IsNullOrWhiteSpace(type))
            {
                builder.WithType(type);
            }
            else if (!string.IsNullOrWhiteSpace(typeRegex))
            {
                builder.WithType(new Regex(typeRegex));
            }
            builder.WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles <see cref="ActivityTypes.InstallationUpdate"/> activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for installation update activities.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// [TeamsInstallationUpdateRoute]
    /// public async Task OnInstallationUpdateAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any installation update activity
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsInstallationUpdateRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var builder = TeamsTypeRouteBuilder.Create().WithType(ActivityTypes.InstallationUpdate).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles message activities, optionally matching specific text or a text pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for message activities.
    /// Provide <paramref name="text"/> for an exact match, <paramref name="textRegex"/> for a pattern match, or neither to match any message.
    /// <paramref name="text"/> and <paramref name="textRegex"/> are mutually exclusive.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// // Match any message
    /// [TeamsMessageRoute]
    /// public async Task OnMessageAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any message
    /// }
    ///
    /// // Match a specific message
    /// [TeamsMessageRoute("hello")]
    /// public async Task OnHelloAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle "hello" message
    /// }
    ///
    /// // Match a text pattern
    /// [TeamsMessageRoute(textRegex: "he.*o")]
    /// public async Task OnHelloPatternAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle messages matching pattern
    /// }
    /// </code>
    /// </remarks>
    /// <param name="text">The exact message text to match (case-insensitive). Mutually exclusive with <paramref name="textRegex"/>. When both are omitted, all messages are matched.</param>
    /// <param name="textRegex">A regular expression pattern matched against <see cref="IActivity.Text"/>. Mutually exclusive with <paramref name="text"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. When no text filter is specified, defaults to <see cref="RouteRank.Last"/> so specific-text routes take priority.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsMessageRouteAttribute(string text = null, string textRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var b = TeamsMessageRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));

            if (!string.IsNullOrWhiteSpace(text))
            {
                b = b.WithText(text);
            }
            else if (!string.IsNullOrWhiteSpace(textRegex))
            {
                b = b.WithText(new Regex(textRegex));
            }

            app.AddRoute(b.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles event activities, optionally matching a specific event name or name pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for event activities.
    /// Provide <paramref name="name"/> for an exact match, <paramref name="nameRegex"/> for a pattern match, or neither to match any event.
    /// <paramref name="name"/> and <paramref name="nameRegex"/> are mutually exclusive.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// // Match any event
    /// [TeamsEventRoute]
    /// public async Task OnAnyEventAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any event activity
    /// }
    ///
    /// // Match a specific event
    /// [TeamsEventRoute("myEvent")]
    /// public async Task OnMyEventAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle "myEvent" event
    /// }
    ///
    /// // Match an event name pattern
    /// [TeamsEventRoute(nameRegex: "my.*Event")]
    /// public async Task OnMyEventPatternAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle events matching pattern
    /// }
    /// </code>
    /// </remarks>
    /// <param name="name">The exact event name to match (case-insensitive), e.g. <see cref="IActivity.Name"/>. Mutually exclusive with <paramref name="nameRegex"/>. When both are omitted, all events are matched.</param>
    /// <param name="nameRegex">A regular expression pattern matched against <see cref="IActivity.Name"/>. Mutually exclusive with <paramref name="name"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. When no name filter is specified, defaults to <see cref="RouteRank.Last"/> so specific-name routes take priority.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsEventRouteAttribute(string name = null, string nameRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var b = TeamsEventRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));

            if (!string.IsNullOrWhiteSpace(name))
            {
                b = b.WithName(name);
            }
            else if (!string.IsNullOrWhiteSpace(nameRegex))
            {
                b = b.WithName(new Regex(nameRegex));
            }

            app.AddRoute(b.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles conversation update activities, optionally matching a specific event.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for conversation update activities.
    /// When <paramref name="eventName"/> is provided, it is matched against <see cref="ConversationUpdateEvents"/> values.
    /// When omitted, all conversation update activities are matched.
    /// Use <see cref="MembersAddedRouteAttribute"/> or <see cref="MembersRemovedRouteAttribute"/> for the common member events.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// // Match any conversation update
    /// [TeamsConversationUpdateRoute]
    /// public async Task OnConversationUpdateAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any conversation update
    /// }
    /// </code>
    /// </remarks>
    /// <param name="eventName">
    /// A <see cref="ConversationUpdateEvents"/> value to match. Only <see cref="ConversationUpdateEvents.MembersAdded"/>
    /// and <see cref="ConversationUpdateEvents.MembersRemoved"/> receive specific matching logic; any other value
    /// matches all <c>conversationUpdate</c> activities. When omitted, all conversation update activities are matched
    /// and the route defaults to <see cref="RouteRank.Last"/>.
    /// Prefer <see cref="MembersAddedRouteAttribute"/> or <see cref="MembersRemovedRouteAttribute"/> for member events.
    /// </param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsConversationUpdateRouteAttribute(string eventName = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            if (!string.IsNullOrWhiteSpace(eventName))
            {
                var b = TeamsConversationUpdateRouteBuilder.Create().WithUpdateEvent(eventName).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else
            {
                var b = TeamsTypeRouteBuilder.Create().WithType(ActivityTypes.ConversationUpdate).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank);
                RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
        }
    }

    /// <summary>
    /// Attribute to define a route that handles conversation update activities when members are added.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for the <see cref="ConversationUpdateEvents.MembersAdded"/> event.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// [TeamsMembersAddedRoute]
    /// public async Task OnMembersAddedAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///    foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
    ///    {
    ///        if (member.Id != turnContext.Activity.Recipient.Id)
    ///        {
    ///            await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
    ///        }
    ///    }
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsMembersAddedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var builder = TeamsConversationUpdateRouteBuilder.Create().WithUpdateEvent(ConversationUpdateEvents.MembersAdded).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles conversation update activities when members are removed.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for the <see cref="ConversationUpdateEvents.MembersRemoved"/> event.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// [TeamsMembersRemovedRoute]
    /// public async Task OnMembersRemovedAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle members removed event
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsMembersRemovedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var builder = TeamsConversationUpdateRouteBuilder.Create().WithUpdateEvent(ConversationUpdateEvents.MembersRemoved).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles handoff action invoke activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for <c>handoff/action</c> invoke activities.
    /// The method must match the <see cref="TeamsHandoffHandler"/> delegate signature.
    /// <code>
    /// [TeamsHandoffRoute]
    /// public async Task OnHandoffAsync(ITeamsTurnContext turnContext, ITurnState turnState, string continuation, CancellationToken cancellationToken)
    /// {
    ///     // Handle handoff action
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsHandoffRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsHandoffHandler>(app, method);
            var builder = TeamsHandoffRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles feedback loop invoke activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for <c>message/submitAction</c> invoke activities
    /// where <c>actionName</c> is <c>feedback</c>.
    /// The method must match the <see cref="TeamsFeedbackLoopHandler"/> delegate signature.
    /// <code>
    /// [TeamsFeedbackLoopRoute]
    /// public async Task OnFeedbackAsync(ITeamsTurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken)
    /// {
    ///     // Handle feedback loop action
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsFeedbackLoopRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsFeedbackLoopHandler>(app, method);
            var builder = TeamsFeedbackRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles end of conversation activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for <c>endOfConversation</c> activities.
    /// The method must match the <see cref="TeamsRouteHandler"/> delegate signature.
    /// <code>
    /// [TeamsEndOfConversationRoute]
    /// public async Task OnEndOfConversationAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle end of conversation activity
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class TeamsEndOfConversationRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<TeamsRouteHandler>(app, method);
            var builder = TeamsTypeRouteBuilder.Create().WithType(ActivityTypes.EndOfConversation).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }
}