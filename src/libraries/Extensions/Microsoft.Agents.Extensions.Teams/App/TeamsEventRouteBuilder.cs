// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;

namespace Microsoft.Agents.Extensions.Teams
{

    public class TeamsEventRouteBuilder : EventRouteBuilderBase<TeamsEventRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TeamsEventRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="TeamsEventRouteBuilder"/>.</returns>
        public static TeamsEventRouteBuilder Create()
        {
            return new TeamsEventRouteBuilder();
        }

        public TeamsEventRouteBuilder WithHandler(TeamsRouteHandler handler)
        {
            AssertionHelpers.ThrowIfNull(handler, nameof(handler));
            _route.Handler = HandlerUtils.WrapHandler(handler);
            return this;
        }

        protected override void PreBuild()
        {
            _route.ChannelId = Channels.Msteams;
            base.PreBuild();
        }
    }
}