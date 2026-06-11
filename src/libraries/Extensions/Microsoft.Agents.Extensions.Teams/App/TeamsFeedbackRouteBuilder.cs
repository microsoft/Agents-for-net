// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.App;

public class TeamsFeedbackRouteBuilder : FeedbackRouteBuilderBase<TeamsFeedbackRouteBuilder>
{

    public TeamsFeedbackRouteBuilder WithHandler(TeamsFeedbackLoopHandler handler, Proactive proactive)
    {
        FeedbackLoopHandler routeHandler = async (tc, turnState, feedbackData, cancellationToken) =>
        {
            var teamsTC = new TeamsTurnContext(tc, proactive);
            await handler(teamsTC, turnState, feedbackData, cancellationToken);
        };
        return WithHandlerCore(routeHandler);
    }

    protected override void PreBuild()
    {
        _route.ChannelId = Channels.Msteams;
        base.PreBuild();
    }
}
