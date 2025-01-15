﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Teams.Models;
using Xunit;
using static Microsoft.Agents.Teams.Tests.TabsTestData;

namespace Microsoft.Agents.Teams.Tests
{
    public class TabResponsePayloadTests
    {
        [Theory]
        [ClassData(typeof(TabResponsePayloadTestData))]
        public void TabResponsePayloadInits(string tabType, TabResponseCards value, TabSuggestedActions suggestedActions)
        {
            var resPayload = new TabResponsePayload()
            {
                Type = tabType,
                Value = value,
                SuggestedActions = suggestedActions
            };

            Assert.NotNull(resPayload);
            Assert.IsType<TabResponsePayload>(resPayload);
        }
    }
}
