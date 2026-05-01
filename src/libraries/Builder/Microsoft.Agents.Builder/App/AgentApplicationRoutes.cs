// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Builder.App
{
    public partial class AgentApplication
    {
        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity<T>(RouteHandler<T> handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false) where T : IActivity
        {
            return AddRoute(TypeRouteBuilder.Create()
                .WithType<T>()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="type">Name of the activity type to match.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(string type, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(TypeRouteBuilder.Create()
                .WithType(type)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="typePattern">Regular expression to match against the incoming activity type.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnActivity(Regex typePattern, RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(TypeRouteBuilder.Create()
                .WithType(typePattern)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="Microsoft.Agents.Builder.App.ConversationUpdateEvents"/>.  If </param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public virtual AgentApplication OnConversationUpdate(string conversationUpdateEvent, RouteHandler<IConversationUpdateActivity> handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(ConversationUpdateRouteBuilder.Create()
                .WithUpdateEvent(conversationUpdateEvent)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a Agent respond anytime a user sends a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "-reset":
        /// <br/>
        /// <code>application.OnMessage("-reset", (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="text">Text to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(string text, RouteHandler<IMessageActivity> handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageRouteBuilder.Create()
                .WithText(text)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a Agent respond anytime a user sends a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage(new Regex("reset"), (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="textPattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessage(Regex textPattern, RouteHandler<IMessageActivity> handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageRouteBuilder.Create()
                .WithText(textPattern)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming Event with a specific Name.
        /// </summary>
        /// <param name="eventName">Substring of the incoming message text.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(string eventName, RouteHandler<IEventActivity> handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(EventRouteBuilder.Create()
                .WithName(eventName)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles incoming Events matching a Name pattern.
        /// </summary>
        /// <param name="namePattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnEvent(Regex namePattern, RouteHandler<IEventActivity> handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(EventRouteBuilder.Create()
                .WithName(namePattern)
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles message reactions added events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessageReactionsAdded(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageReactionsAddedRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles message reactions removed events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnMessageReactionsRemoved(RouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(MessageReactionsRemovedRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Handles handoff activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnHandoff(HandoffHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(HandoffRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }

        /// <summary>
        /// Registers a handler for feedback loop events when a user clicks the thumbsup or thumbsdown button on a response.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered</param>
        /// <param name="rank">0 - ushort.MaxValue for order of evaluation.  Ranks of the same value are evaluated in order of addition.</param>
        /// <param name="autoSignInHandlers"></param>
        /// <param name="isAgenticOnly">True if the route is for Agentic requests only.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public AgentApplication OnFeedbackLoop(FeedbackLoopHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            return AddRoute(FeedbackRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .AsAgentic(isAgenticOnly)
                .Build()
            );
        }
    }
}
