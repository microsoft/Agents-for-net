// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Extensions.MSTeams.App;

public class TeamsConversationUpdateRouteBuilder : ConversationUpdateRouteBuilderBase<TeamsConversationUpdateRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the <see cref="TeamsConversationUpdateRouteBuilder"/> class.
    /// </summary>
    /// <returns>A new <see cref="TeamsConversationUpdateRouteBuilder"/>.</returns>
    public static TeamsConversationUpdateRouteBuilder Create()
    {
        return new TeamsConversationUpdateRouteBuilder();
    }

    public TeamsConversationUpdateRouteBuilder WithHandler(TeamsRouteHandler handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        _route.Handler = HandlerUtils.WrapHandler(handler);
        return this;
    }

    protected override void PreBuild()
    {
        _route.ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams;
        base.PreBuild();
    }
}