// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Messages;
using System;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.App
{

    public class TeamsMessageRouteBuilder : MessageRouteBuilderBase<TeamsMessageRouteBuilder>
    {

        public TeamsMessageRouteBuilder WithHandler(TeamsRouteHandler handler, Proactive proactive)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = HandlerUtils.WrapHandler(handler, proactive);
            return this;
        }

        protected override void PreBuild()
        {
            _route.ChannelId = Channels.Msteams;
            base.PreBuild();
        }
    }
}
