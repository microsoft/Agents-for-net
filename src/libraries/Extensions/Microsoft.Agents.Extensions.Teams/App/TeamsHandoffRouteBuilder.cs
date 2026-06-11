// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.App;

public class TeamsHandoffRouteBuilder : HandoffRouteBuilderBase<TeamsHandoffRouteBuilder>
{

    public TeamsHandoffRouteBuilder WithHandler(TeamsHandoffHandler handler, Proactive proactive)
    {
        HandoffHandler routeHandler = async (tc, turnState, continuation, cancellationToken) =>
        {
            var teamsTC = new TeamsTurnContext(tc, proactive);
            await handler(teamsTC, turnState, continuation, cancellationToken);
        };
        return WithHandlerCore(routeHandler);
    }

    protected override void PreBuild()
    {
        _route.ChannelId = Channels.Msteams;
        base.PreBuild();
    }
}
