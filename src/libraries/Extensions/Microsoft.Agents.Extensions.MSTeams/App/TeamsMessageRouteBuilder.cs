// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.MSTeams.App
{

    public class TeamsMessageRouteBuilder : MessageRouteBuilderBase<TeamsMessageRouteBuilder>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TeamsMessageRouteBuilder"/> class.
        /// </summary>
        /// <returns>A new <see cref="TeamsMessageRouteBuilder"/>.</returns>
        public static TeamsMessageRouteBuilder Create()
        {
            return new TeamsMessageRouteBuilder();
        }

        public TeamsMessageRouteBuilder WithHandler(TeamsRouteHandler handler)
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
}