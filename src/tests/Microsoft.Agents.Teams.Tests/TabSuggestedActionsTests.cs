// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Teams.Models;
using Xunit;
using static Microsoft.Agents.Teams.Tests.TabsTestData;

namespace Microsoft.Agents.Teams.Tests
{
    public class TabSuggestedActionsTests
    {
        [Theory]
        [ClassData(typeof(TabSuggestedActionsTestData))]
        public void TabSuggestedActionsInits(IList<CardAction> actions)
        {
            var suggestedActions = new TabSuggestedActions()
            {
                Actions = actions
            };

            Assert.NotNull(suggestedActions);
            Assert.IsType<TabSuggestedActions>(suggestedActions);
            Assert.Equal(actions, suggestedActions.Actions);
        }
    }
}
