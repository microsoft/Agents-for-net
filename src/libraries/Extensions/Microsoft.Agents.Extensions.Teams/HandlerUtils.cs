using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Extensions.Teams.Messages;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
using Microsoft.Agents.Extensions.Teams.TeamsTeams;

namespace Microsoft.Agents.Extensions.Teams
{
    public static class HandlerUtils
    {
        public static RouteHandler WrapHandler<T>(RouteHandler handler, Proactive proactive)
        {
            return async (tc, turnState, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, cancellationToken);
            };
        }

        public static TeamUpdateHandler WrapHandler(TeamUpdateHandler handler, Proactive proactive)
        {
            return async (tc, turnState, data, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, data, cancellationToken);
            };
        }

        public static ChannelUpdateHandler WrapHandler(ChannelUpdateHandler handler, Proactive proactive)
        {
            return async (tc, turnState, data, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, data, cancellationToken);
            };
        }

        public static ReadReceiptHandler WrapHandler(ReadReceiptHandler handler, Proactive proactive)
        {
            return async (tc, turnState, data, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, data, cancellationToken);
            };
        }

        public static O365ConnectorCardActionHandler WrapHandler(O365ConnectorCardActionHandler handler, Proactive proactive)
        {
            return async (tc, turnState, data, cancellationToken) =>
            {
                var teamsTC = new TeamsTurnContext(tc, proactive);
                await handler(teamsTC, turnState, data, cancellationToken);
            };
        }
    }
}
