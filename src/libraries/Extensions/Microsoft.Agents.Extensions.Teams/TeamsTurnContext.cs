using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Extensions.Teams
{
    public class TeamsTurnContext : TurnContextWrapper, ITeamsTurnContext
    {
        public TeamsTurnContext(ITurnContext turnContext) : base(turnContext) { }
    }
}
