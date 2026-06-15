using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.Teams.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    /// <summary>
    /// Provides <see cref="AgentApplication"/> extension methods for registering Teams-aware route handlers.
    /// </summary>
    public static class TeamsAppExtensions
    {
        /// <summary>
        /// Registers a Teams activity handler for the specified activity type.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="type">The exact activity type to match.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnActivity(this AgentApplication app, string type, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnActivity(type, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams activity handler for activity types that match the supplied pattern.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="typePattern">The regular expression used to match activity types.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnActivity(this AgentApplication app, Regex typePattern, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnActivity(typePattern, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams conversation update handler for the specified conversation update event.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="conversationUpdateEvent">The conversation update event name to match.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnConversationUpdate(this AgentApplication app, string conversationUpdateEvent, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnConversationUpdate(conversationUpdateEvent, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams message handler for the specified message text.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="text">The exact message text to match.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnMessage(this AgentApplication app, string text, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnMessage(text, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams message handler for message text that matches the supplied pattern.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="textPattern">The regular expression used to match message text.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnMessage(this AgentApplication app, Regex textPattern, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnMessage(textPattern, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams event handler for the specified event name.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="eventName">The exact event name to match.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnEvent(this AgentApplication app, string eventName, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnEvent(eventName, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams event handler for event names that match the supplied pattern.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="namePattern">The regular expression used to match event names.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnEvent(this AgentApplication app, Regex namePattern, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnEvent(namePattern, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams event handler using a custom route selector.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="routeSelector">The custom selector that determines whether the route matches.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnEvent(this AgentApplication app, RouteSelector routeSelector, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnEvent(routeSelector, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams handler for added message reactions.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnMessageReactionsAdded(this AgentApplication app, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnMessageReactionsAdded(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams handler for removed message reactions.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="handler">The Teams-aware handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnMessageReactionsRemoved(this AgentApplication app, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnMessageReactionsRemoved(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams handoff handler.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="handler">The Teams handoff handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnHandoff(this AgentApplication app, TeamsHandoffHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnHandoff(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        /// <summary>
        /// Registers a Teams feedback loop handler.
        /// </summary>
        /// <param name="app">The agent application to configure.</param>
        /// <param name="handler">The Teams feedback loop handler to invoke.</param>
        /// <param name="rank">The route evaluation order. Lower values run first.</param>
        /// <param name="autoSignInHandlers">The OAuth sign-in handlers to associate with the route.</param>
        /// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns.</param>
        /// <returns>The configured agent application.</returns>
        public static AgentApplication OnFeedbackLoop(this AgentApplication app, TeamsFeedbackLoopHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler);
            return app.OnFeedbackLoop(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }
    }
}