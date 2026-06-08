// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.App.UserAuth;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class TeamsAgenticSettingsTests
    {
        [Fact]
        public void Constructor_SetsRedirectUri_WhenProvided()
        {
            var settings = new TeamsAgenticSettings(
                "oauthConnection",
                ["scope"],
                redirectUri: "https://bot.contoso.com/auth/callback",
                connectionName: "connection");

            Assert.Equal("https://bot.contoso.com/auth/callback", settings.RedirectUri);
            Assert.Equal("oauthConnection", settings.OAuthConnectionName);
            Assert.Equal("connection", settings.ConnectionName);
        }
    }
}
