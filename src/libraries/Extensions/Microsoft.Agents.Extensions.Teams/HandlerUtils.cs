using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Extensions.Teams.Messages;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
using Microsoft.Agents.Extensions.Teams.TeamsTeams;
using System;
using System.Linq;
using System.Reflection;


namespace Microsoft.Agents.Extensions.Teams
{
    internal static class HandlerUtils
    {
        public static RouteHandler WrapHandler(TeamsRouteHandler handler, Proactive proactive)
        {
            return async (tc, turnState, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, cancellationToken);
            };
        }

        public static HandoffHandler WrapHandler(TeamsHandoffHandler handler, Proactive proactive)
        {
            return async (tc, turnState, continuation, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, continuation, cancellationToken);
            };
        }

        public static FeedbackLoopHandler WrapHandler(TeamsFeedbackLoopHandler handler, Proactive proactive)
        {
            return async (tc, turnState, feedbackData, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, feedbackData, cancellationToken);
            };
        }

        public static void TeamsInvokeGenericWithHandler(AgentApplication app, MethodInfo method, Type openHandlerType, int paramIndex, object builder)
        {
            var genericParam = method.GetParameters()[paramIndex].ParameterType;
            var handlerType = openHandlerType.MakeGenericType(genericParam);
            var handler = RouteAttributeHelper.CreateHandlerDelegate(app, method, handlerType);
            var withHandler = builder.GetType().GetMethods()
                .First(m => m.Name == "WithHandler" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(genericParam);
            withHandler.Invoke(builder, new object[] { handler, app.Proactive });
        }
    }
}
