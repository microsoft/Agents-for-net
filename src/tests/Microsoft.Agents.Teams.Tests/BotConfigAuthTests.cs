﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Teams.Primitives;
using Xunit;

namespace Microsoft.Agents.Teams.Tests
{
    public class BotConfigAuthTests
    {
        [Fact]
        public void BotConfigAuthInitsWithNoArgs()
        {
            var botConfigAuthResponse = new BotConfigAuth();

            Assert.NotNull(botConfigAuthResponse);
            Assert.IsType<BotConfigAuth>(botConfigAuthResponse);
            Assert.Equal("auth", botConfigAuthResponse.Type);
        }
    }
}
