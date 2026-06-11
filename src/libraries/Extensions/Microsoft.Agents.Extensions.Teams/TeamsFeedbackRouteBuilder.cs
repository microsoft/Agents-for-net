// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams;

public class TeamsFeedbackRouteBuilder : FeedbackRouteBuilderBase<TeamsFeedbackRouteBuilder>
{

    public TeamsFeedbackRouteBuilder WithHandler(TeamsFeedbackLoopHandler handler, Proactive proactive)
    {
        var routeHandler = HandlerUtils.WrapHandler(handler, proactive);
        return WithHandlerCore(routeHandler);
    }

    protected override void PreBuild()
    {
        _route.ChannelId = Channels.Msteams;
        base.PreBuild();
    }
}
