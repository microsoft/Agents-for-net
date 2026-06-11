using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    public static class TeamsAppExtensions
    {
        public static AgentApplication OnActivity(this AgentApplication app, string type, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnActivity(type, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnActivity(this AgentApplication app, Regex typePattern, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnActivity(typePattern, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnConversationUpdate(this AgentApplication app, string conversationUpdateEvent, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnConversationUpdate(conversationUpdateEvent, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnMessage(this AgentApplication app, string text, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnMessage(text, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnMessage(this AgentApplication app, Regex textPattern, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnMessage(textPattern, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnEvent(this AgentApplication app, string eventName, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnEvent(eventName, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnEvent(this AgentApplication app, Regex namePattern, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnEvent(namePattern, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnEvent(this AgentApplication app, RouteSelector routeSelector, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnEvent(routeSelector, newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnMessageReactionsAdded(this AgentApplication app, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnMessageReactionsAdded(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnMessageReactionsRemoved(this AgentApplication app, TeamsRouteHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnMessageReactionsRemoved(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnHandoff(this AgentApplication app, TeamsHandoffHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnHandoff(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }

        public static AgentApplication OnFeedbackLoop(this AgentApplication app, TeamsFeedbackLoopHandler handler, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null, bool isAgenticOnly = false)
        {
            var newHandler = HandlerUtils.WrapHandler(handler, app.Proactive);
            return app.OnFeedbackLoop(newHandler, rank, autoSignInHandlers, isAgenticOnly);
        }
    }
}
