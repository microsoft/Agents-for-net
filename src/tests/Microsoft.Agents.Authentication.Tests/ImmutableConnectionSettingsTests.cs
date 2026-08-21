// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Auth.Tests
{
    public class ImmutableConnectionSettingsTests
    {
        private const string ClientId = "client-id";
        private const string AuthorityEndpoint = "https://botframework/test.com";
        private const string TenantId = "tenant-id";
        private const string AlternateBlueprintConnectionName = "alternate-connection";
        private static readonly List<string> _scopes = ["scope1", "scope2"];

        [Fact]
        public void Properties_DelegateToUnderlyingSettings()
        {
            Dictionary<string, string> configSettings = new()
            {
                { "Connections:Settings:ClientId", ClientId },
                { "Connections:Settings:AuthorityEndpoint", AuthorityEndpoint },
                { "Connections:Settings:TenantId", TenantId },
                { "Connections:Settings:Scopes:0", _scopes[0] },
                { "Connections:Settings:Scopes:1", _scopes[1] },
                { "Connections:Settings:AlternateBlueprintConnectionName", AlternateBlueprintConnectionName }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new TestConnectionSettings(configuration.GetSection("Connections:Settings"));
            var immutableSettings = new ImmutableConnectionSettings(settings);

            Assert.Equal(ClientId, immutableSettings.ClientId);
            Assert.Equal(AuthorityEndpoint, immutableSettings.Authority);
            Assert.Equal(TenantId, immutableSettings.TenantId);
            Assert.Same(settings.Scopes, immutableSettings.Scopes);
            Assert.Equal(_scopes, immutableSettings.Scopes);
            Assert.Equal(AlternateBlueprintConnectionName, immutableSettings.AlternateBlueprintConnectionName);
        }

        [Fact]
        public void Properties_ReturnNullWhenBaseSettingsHaveNoValues()
        {
            Dictionary<string, string> configSettings = new()
            {
                { "Connections:Settings:Other", "other" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new TestConnectionSettings(configuration.GetSection("Connections:Settings"));
            var immutableSettings = new ImmutableConnectionSettings(settings);

            Assert.Null(immutableSettings.ClientId);
            Assert.Null(immutableSettings.Authority);
            Assert.Null(immutableSettings.TenantId);
            Assert.Null(immutableSettings.Scopes);
            Assert.Null(immutableSettings.AlternateBlueprintConnectionName);
        }

        private class TestConnectionSettings(IConfigurationSection configurationSection) : ConnectionSettingsBase(configurationSection)
        {
        }
    }
}
