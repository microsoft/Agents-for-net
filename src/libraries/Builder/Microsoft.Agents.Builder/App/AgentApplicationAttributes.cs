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
    /// </code>
    /// </remarks>
    /// <param name="type">The exact activity <see cref="IActivity.Type"/> to match, e.g. <see cref="ActivityTypes"/>. Mutually exclusive with <paramref name="typeRegex"/>.</param>
    /// <param name="typeRegex">A regular expression pattern matched against <see cref="IActivity.Type"/>. Mutually exclusive with <paramref name="type"/>.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ActivityRouteAttribute(string type = null, string typeRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = TypeRouteBuilder.Create();
            if (!string.IsNullOrWhiteSpace(type))
            {
                builder.WithType(type);
            }
            else
            {
                builder.WithType(new Regex(typeRegex));
            }
            builder.WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles activities matching installation updates.
    /// <code>
    /// // Match by exact type
    /// [InstallationUpdateRoute]
    /// public async Task OnInstallationUpdateAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    /// {
    ///     // Handle any installation update activity
    /// }
    /// </code>
    /// </summary>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class InstallationUpdateRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = TypeRouteBuilder.Create().WithType(ActivityTypes.InstallationUpdate).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageRouteAttribute(string text = null, string textRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var b = MessageRouteBuilder.Create().WithText(text).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else if (!string.IsNullOrWhiteSpace(textRegex))
            {
                var b = MessageRouteBuilder.Create().WithText(new Regex(textRegex)).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else
            {
                // match any message activity
                var b = TypeRouteBuilder.Create().WithType(ActivityTypes.Message).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
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
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class EventRouteAttribute(string name = null, string nameRegex = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            if (!string.IsNullOrWhiteSpace(name))
            {
                var b = EventRouteBuilder.Create().WithName(name).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else if (!string.IsNullOrWhiteSpace(nameRegex))
            {
                var b = EventRouteBuilder.Create().WithName(new Regex(nameRegex)).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else
            {
                // match any event activity
                var b = TypeRouteBuilder.Create().WithType(ActivityTypes.Event).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
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
    /// <param name="eventName">A <see cref="ConversationUpdateEvents"/> value to match. When omitted, all conversation update activities are matched.</param>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ConversationUpdateRouteAttribute(string eventName = null, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            if (!string.IsNullOrWhiteSpace(eventName))
            {
                var b = ConversationUpdateRouteBuilder.Create().WithUpdateEvent(eventName).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
                app.AddRoute(b.Build());
            }
            else
            {
                var b = TypeRouteBuilder.Create().WithType(ActivityTypes.ConversationUpdate).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank);
                RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => b.WithOAuthHandlers(s), f => b.WithOAuthHandlers(f));
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
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MembersAddedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = ConversationUpdateRouteBuilder.Create().WithUpdateEvent(ConversationUpdateEvents.MembersAdded).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MembersRemovedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = ConversationUpdateRouteBuilder.Create().WithUpdateEvent(ConversationUpdateEvents.MembersRemoved).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageReactionsAddedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = MessageReactionsAddedRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class MessageReactionsRemovedRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = MessageReactionsRemovedRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class HandoffRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<HandoffHandler>(app);
#else
            var handler = (HandoffHandler)method.CreateDelegate(typeof(HandoffHandler), app);
#endif
            var builder = HandoffRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class FeedbackLoopRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
#if !NETSTANDARD
            var handler = method.CreateDelegate<FeedbackLoopHandler>(app);
#else
            var handler = (FeedbackLoopHandler)method.CreateDelegate(typeof(FeedbackLoopHandler), app);
#endif
            var builder = FeedbackRouteBuilder.Create().WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
            app.AddRoute(builder.Build());
        }
    }

    /// <summary>
    /// Attribute to define a route that handles end of conversation activities.
    /// </summary>
    /// <remarks>
    /// Decorate a method with this attribute to register it as a handler for <c>endOfConversation</c> activities.
    /// The method must match the <see cref="EndOfConversationHandler"/> delegate signature.
    /// <code>
    /// [EndOfConversationRoute]
    /// public async Task OnEndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, EndOfConversationData endOfConversationData, CancellationToken cancellationToken)
    /// {
    ///     // Handle end of conversation activity
    /// }
    /// </code>
    /// </remarks>
    /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
    /// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
    /// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class EndOfConversationRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
    {
        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            var handler = RouteAttributeHelper.CreateHandlerDelegate<RouteHandler>(app, method);
            var builder = TypeRouteBuilder.Create().WithType(ActivityTypes.EndOfConversation).WithHandler(handler).AsAgentic(isAgenticOnly).WithOrderRank(rank);
            RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
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
        /// <param name="builder">The route builder on which to invoke <c>WithHandler&lt;T&gt;</c>.</param>
        public static void InvokeGenericWithHandler(AgentApplication app, MethodInfo method, Type openHandlerType, object builder)
        {
            var genericParam = method.GetParameters()[2].ParameterType;
            var handlerType = openHandlerType.MakeGenericType(genericParam);
            var handler = CreateHandlerDelegate(app, method, handlerType);
            var withHandler = builder.GetType().GetMethods()
                .First(m => m.Name == "WithHandler" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(genericParam);
            withHandler.Invoke(builder, new object[] { handler });
        }

        /// <summary>
        /// Applies sign-in handlers to a route builder by invoking <paramref name="withDelegate"/> if
        /// <paramref name="signInHandlers"/> names a method on <paramref name="app"/> matching
        /// <c>Func&lt;ITurnContext, string[]&gt;</c>, otherwise invoking <paramref name="withDelimited"/>
        /// to treat it as a comma/space/semicolon-delimited list of handler names.
        /// </summary>
        public static void ApplySignInHandlers(AgentApplication app, string signInHandlers,
            Action<string> withDelimited, Action<Func<ITurnContext, string[]>> withDelegate)
        {
            if (!string.IsNullOrWhiteSpace(signInHandlers))
            {
                var delegateMethod = app.GetType().GetMethod(signInHandlers,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (delegateMethod != null)
                {
                    try
                    {
#if !NETSTANDARD
                        var d = delegateMethod.CreateDelegate<Func<ITurnContext, string[]>>(app);
#else
                        var d = (Func<ITurnContext, string[]>)delegateMethod.CreateDelegate(typeof(Func<ITurnContext, string[]>), app);
#endif
                        withDelegate(d);
                        return;
                    }
                    catch (ArgumentException) { }
                }
            }

            withDelimited(signInHandlers);
        }
    }
}
