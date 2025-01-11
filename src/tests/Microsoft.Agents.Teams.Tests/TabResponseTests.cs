// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Teams.Models;
using Xunit;
using static Microsoft.Agents.Teams.Tests.TabsTestData;

namespace Microsoft.Agents.Teams.Tests
{
    public class TabResponseTests
    {
        [Theory]
        [ClassData(typeof(TabResponseTestData))]
        public void TabResponseInits(TabResponsePayload tab)
        {
            var tabResponse = new TabResponse()
            {
                Tab = tab
            };

            Assert.NotNull(tabResponse);
            Assert.IsType<TabResponse>(tabResponse);
            Assert.Equal(tab, tabResponse.Tab);
        }
    }
}
