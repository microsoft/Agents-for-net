﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Teams.Models;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using static Microsoft.Agents.Teams.Tests.TabsTestData;

namespace Microsoft.Agents.Teams.Tests
{
    public class TabSubmitDataTests
    {
        [Theory]
        [ClassData(typeof(TabSubmitDataTestData))]
        public void TabSubmitDataInits(string tabType, IDictionary<string, JsonElement> properties)
        {
            var submitData = new TabSubmitData()
            {
                Type = tabType,
                Properties = properties
            };

            Assert.NotNull(submitData);
            Assert.IsType<TabSubmitData>(submitData);
            Assert.Equal(tabType, submitData.Type);

            var dataProps = submitData.Properties;
            Assert.Equal(properties, dataProps);
            if (dataProps != null)
            {
                Assert.Equal(properties.Count, submitData.Properties.Count);
            }
        }
    }
}
