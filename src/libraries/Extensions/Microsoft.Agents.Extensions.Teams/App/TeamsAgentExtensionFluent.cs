using System;
using Microsoft.Agents.BotBuilder.App;

namespace Microsoft.Agents.Extensions.Teams.App;

public static class TeamsAgentExtensionFluent
{
    public static AgentApplication WithTeams(this AgentApplication application, Action<TeamsAgentExtension> setupFunction) {
        TeamsAgentExtension teamsAgentExtension = new TeamsAgentExtension(application);
        setupFunction(teamsAgentExtension);
        return application;
    }

    public static AgentApplication WithTeams(this AgentApplication application, TeamsAgentExtension teamsAgentExtension, Action<TeamsAgentExtension> setupFunction) {
        setupFunction(teamsAgentExtension);
        return application;
    }
}
