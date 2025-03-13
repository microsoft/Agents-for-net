using Microsoft.Agents.BotBuilder.App;

namespace Microsoft.Agents.BotBuilder;

public abstract class AgentExtension : IAgentExtension
{
    public virtual string ChannelId {get;init;}
    public void AddRoute(AgentApplication agentApplication, RouteSelectorAsync routeSelectorAsync, RouteHandler routeHandler, bool isInvokeRoute = false) {
        var ensureChannelMatches = new RouteSelectorAsync(async (turnContext, cancellationToken) => {
            return turnContext.Activity.ChannelId == ChannelId && await routeSelectorAsync(turnContext, cancellationToken);
        });
        agentApplication.AddRoute(ensureChannelMatches, routeHandler);
    }
}
