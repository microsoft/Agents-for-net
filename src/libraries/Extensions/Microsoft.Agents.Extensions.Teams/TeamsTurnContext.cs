// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;

namespace Microsoft.Agents.Extensions.Teams
{
    public class TeamsTurnContext : TurnContextWrapper, ITeamsTurnContext
    {
        public TeamsTurnContext(ITurnContext turnContext) : base(turnContext) { }
    }
}
