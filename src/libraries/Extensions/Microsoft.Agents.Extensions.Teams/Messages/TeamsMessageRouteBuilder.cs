// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Extensions.Teams.Messages;

public class TeamsMessageRouteBuilder : MessageEventRouteBuilderBase<MessageUndeleteRouteBuilder>
{
    public TeamsMessageRouteBuilder() : base()
    {
        ActivityTypeName = Microsoft.Teams.Api.Activities.ActivityType.MessageUpdate;
        EventTypeName = "undeleteMessage";
    }

    public TeamsMessageRouteBuilder WithHandler(RouteHandler handler)
    {
        _route.Handler = handler;
        return this;
    }
}
