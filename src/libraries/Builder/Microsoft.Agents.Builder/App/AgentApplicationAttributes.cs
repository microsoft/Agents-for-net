// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Attribute to define a route that handles activities matching a specific type or type pattern.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for activities of the specified type.
    /// Provide either <paramref name="type"/> for an exact match or <paramref name="typeRegex"/> for a pattern match; they are mutually exclusive.
    /// When neither is provided the route matches any activity type and defaults to <see cref="RouteRank.Last"/>.
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// // Match by exact type
    /// [ActivityRoute(ActivityTypes.Event)]
    /// public async Task OnEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any event activity
    /// }
    ///
    /// // Match by type pattern
    /// [ActivityRoute(typeRegex: "event|invoke")]
    /// public async Task OnEventOrInvokeAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle event or invoke activities
    /// }
    ///
    /// // Match any activity type (fires last, after all specific routes)
    /// [ActivityRoute]
    /// public async Task OnAnyAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
    public class ActivityRouteAttribute(string type = null, string typeRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = TypeRouteBuilder.Create();
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
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// [InstallationUpdateRoute]
    /// public async Task OnInstallationUpdateAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any installation update activity
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class InstallationUpdateRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = TypeRouteBuilder.Create().WithType(ActivityTypes.InstallationUpdate).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
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
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// // Match any message
    /// [MessageRoute]
    /// public async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any message
    /// }
    ///
    /// // Match a specific message
    /// [MessageRoute("hello")]
    /// public async Task OnHelloAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle "hello" message
    /// }
    ///
    /// // Match a text pattern
    /// [MessageRoute(textRegex: "he.*o")]
    /// public async Task OnHelloPatternAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
    public class MessageRouteAttribute(string text = null, string textRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var b = MessageRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
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
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// // Match any event
    /// [EventRoute]
    /// public async Task OnAnyEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any event activity
    /// }
    ///
    /// // Match a specific event
    /// [EventRoute("myEvent")]
    /// public async Task OnMyEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle "myEvent" event
    /// }
    ///
    /// // Match an event name pattern
    /// [EventRoute(nameRegex: "my.*Event")]
    /// public async Task OnMyEventPatternAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
    public class EventRouteAttribute(string name = null, string nameRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var b = EventRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
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
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// // Match any conversation update
    /// [ConversationUpdateRoute]
    /// public async Task OnConversationUpdateAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
    public class ConversationUpdateRouteAttribute(string eventName = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            if (!string.IsNullOrWhiteSpace(eventName))
            {
                var b = ConversationUpdateRouteBuilder.Create().WithUpdateEvent(eventName).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else
            {
                var b = TypeRouteBuilder.Create().WithType(ActivityTypes.ConversationUpdate).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank);
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
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// [MembersAddedRoute]
    /// public async Task OnMembersAddedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
    public class MembersAddedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = ConversationUpdateRouteBuilder.Create().WithUpdateEvent(ConversationUpdateEvents.MembersAdded).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles conversation update activities when members are removed.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for the <see cref="ConversationUpdateEvents.MembersRemoved"/> event.
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// [MembersRemovedRoute]
    /// public async Task OnMembersRemovedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle members removed event
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MembersRemovedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = ConversationUpdateRouteBuilder.Create().WithUpdateEvent(ConversationUpdateEvents.MembersRemoved).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles message reaction added activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for activities where reactions have been added to a message.
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// [MessageReactionsAddedRoute]
    /// public async Task OnReactionsAddedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle reactions added event
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageReactionsAddedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = MessageReactionsAddedRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles message reaction removed activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for activities where reactions have been removed from a message.
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// [MessageReactionsRemovedRoute]
    /// public async Task OnReactionsRemovedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle reactions removed event
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageReactionsRemovedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = MessageReactionsRemovedRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles handoff action invoke activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for <c>handoff/action</c> invoke activities.
    /// The method must match the <see cref="HandoffHandler"/> delegate signature.
    /// <code>
    /// [HandoffRoute]
    /// public async Task OnHandoffAsync(ITurnContext turnContext, ITurnState turnState, string continuation, CancellationToken cancellationToken)
    /// {
    ///     // Handle handoff action
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class HandoffRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<HandoffHandler>(app, method);
            var builder = HandoffRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
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
    /// The method must match the <see cref="FeedbackLoopHandler"/> delegate signature.
    /// <code>
    /// [FeedbackLoopRoute]
    /// public async Task OnFeedbackAsync(ITurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken)
    /// {
    ///     // Handle feedback loop action
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class FeedbackLoopRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<FeedbackLoopHandler>(app, method);
            var builder = FeedbackRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles end of conversation activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for <c>endOfConversation</c> activities.
    /// The method must match the <see cref="RouteHandler"/> delegate signature.
    /// <code>
    /// [EndOfConversationRoute]
    /// public async Task OnEndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle end of conversation activity
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="autoSignInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance or static method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class EndOfConversationRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string autoSignInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = TypeRouteBuilder.Create().WithType(ActivityTypes.EndOfConversation).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, autoSignInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Shared helper for applying sign-in handlers to a route builder.
    /// </summary>
    public static class RouteAttributeHelper
    {
        /// <summary>
        /// Creates a bound delegate from <paramref name="method"/>, handling both instance and static methods.
        /// For instance methods the delegate is bound to <paramref name="app"/>; for static methods no target is bound.
        /// </summary>
        public static T CreateHandlerDelegate<T>(AgentApplication app, MethodInfo method) where T : class, Delegate
        {
#if !NETSTANDARD
            return method.IsStatic ? method.CreateDelegate<T>() : method.CreateDelegate<T>(app);
#else
            return method.IsStatic
                ? (T)method.CreateDelegate(typeof(T))
                : (T)method.CreateDelegate(typeof(T), app);
#endif
        }

        /// <summary>
        /// Creates a bound delegate of the given <paramref name="delegateType"/> from <paramref name="method"/>,
        /// handling both instance and static methods.
        /// For instance methods the delegate is bound to <paramref name="app"/>; for static methods no target is bound.
        /// </summary>
        public static Delegate CreateHandlerDelegate(AgentApplication app, MethodInfo method, Type delegateType)
        {
            return method.IsStatic
                ? method.CreateDelegate(delegateType)
                : method.CreateDelegate(delegateType, app);
        }

        /// <summary>
        /// Infers the generic type parameter from the method's third parameter, creates a delegate of
        /// <c>openHandlerType&lt;T&gt;</c>, then finds and invokes the generic <c>WithHandler&lt;T&gt;</c>
        /// method on <paramref name="builder"/>.
        /// </summary>
        /// <param name="app">The agent application to bind the delegate to for instance methods.</param>
        /// <param name="method">The method to wrap as a delegate.</param>
        /// <param name="openHandlerType">The open generic delegate type, e.g. <c>typeof(FetchHandler&lt;&gt;)</c>.</param>
        /// <param name="paramIndex">The index of the parameter to use for inferring the generic type.</param>
        /// <param name="builder">The route builder on which to invoke <c>WithHandler&lt;T&gt;</c>.</param>
        public static void InvokeGenericWithHandler(AgentApplication app, MethodInfo method, Type openHandlerType, int paramIndex, object builder)
        {
            var genericParam = method.GetParameters()[paramIndex].ParameterType;
            var handlerType = openHandlerType.MakeGenericType(genericParam);
            var handler = CreateHandlerDelegate(app, method, handlerType);
            var withHandler = builder.GetType().GetMethods()
                .First(m => m.Name == "WithHandler" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(genericParam);
            withHandler.Invoke(builder, new object[] { handler });
        }

        /// <summary>
        /// Applies sign-in handlers to a route builder by invoking <paramref name="withDelegate"/> if
        /// <paramref name="autoSignInHandlers"/> names a method on <paramref name="app"/> matching
        /// <c>Func&lt;ITurnContext, string[]&gt;</c>, otherwise invoking <paramref name="withDelimited"/>
        /// to treat it as a comma/space/semicolon-delimited list of handler names.
        /// </summary>
        public static void ApplySignInHandlers(AgentApplication app, string autoSignInHandlers,
            Action<string> withDelimited, Action<Func<ITurnContext, string[]>> withDelegate)
        {
            if (!string.IsNullOrWhiteSpace(autoSignInHandlers))
            {
                try
                {
                    var delegateMethod = app.GetType().GetMethod(autoSignInHandlers,
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (delegateMethod != null)
                    {
                        var d = CreateHandlerDelegate<Func<ITurnContext, string[]>>(app, delegateMethod);
                        withDelegate(d);
                        return;
                    }
                }
                catch (AmbiguousMatchException) { }
                catch (ArgumentException) { }
            }

            withDelimited(autoSignInHandlers);
        }

        public static string[] DelimitedToList(string delimitedTokenHandlers)
        {
#if !NETSTANDARD
            return !string.IsNullOrEmpty(delimitedTokenHandlers) ? delimitedTokenHandlers.Split([',', ' ', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : null;
#else
            return !string.IsNullOrEmpty(delimitedTokenHandlers)
                ? delimitedTokenHandlers
                    .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToArray()
                : null;
#endif
        }
    }
}
