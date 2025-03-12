using Microsoft.Agents.BotBuilder.App;

namespace Microsoft.Agents.BotBuilder;

public interface IAgentExtension
{
    string ChannelId { get; init; }
    void AddRoute(AgentApplication agentApplication, RouteSelectorAsync routeSelectorAsync, RouteHandler routeHandler, bool isInvokeRoute = false);
}