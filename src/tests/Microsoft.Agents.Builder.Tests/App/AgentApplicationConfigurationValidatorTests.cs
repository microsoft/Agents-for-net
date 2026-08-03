// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AgentApplicationConfigurationValidatorTests
    {
        private const int NoUserAuthorizationHandlersCode = -50012;
        private const int DefaultHandlerNotFoundCode = -50032;

        [Fact]
        public void Validate_NullConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AgentApplicationConfigurationValidator.Validate(null));
        }

        [Fact]
        public void Validate_NoAgentApplicationSection_DoesNotThrow()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "Connections:ServiceConnection:Settings:ClientId", "client-id" }
            });

            AgentApplicationConfigurationValidator.Validate(configuration);
        }

        [Fact]
        public void Validate_NoUserAuthorization_DoesNotThrow()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplication:StartTypingTimer", "true" }
            });

            AgentApplicationConfigurationValidator.Validate(configuration);
        }

        [Fact]
        public void Validate_DefaultHandlerNameDefined_DoesNotThrow()
        {
            AgentApplicationConfigurationValidator.Validate(CreateUserAuthorizationConfiguration("me"));
        }

        [Theory]
        [InlineData("AUTO")]
        [InlineData(" auto ")]
        public void Validate_DefaultHandlerNameNotAnExactMatch_DoesNotThrow(string defaultHandlerName)
        {
            // Handler lookup is case-insensitive and trims the name.
            AgentApplicationConfigurationValidator.Validate(CreateUserAuthorizationConfiguration(defaultHandlerName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_NoDefaultHandlerName_DoesNotThrow(string defaultHandlerName)
        {
            // The first handler defined is used when a default isn't specified.
            AgentApplicationConfigurationValidator.Validate(CreateUserAuthorizationConfiguration(defaultHandlerName));
        }

        [Fact]
        public void Validate_UnknownDefaultHandlerName_Throws()
        {
            var configuration = CreateUserAuthorizationConfiguration("NotFound");

            var exception = Assert.Throws<IndexOutOfRangeException>(() => AgentApplicationConfigurationValidator.Validate(configuration));

            Assert.Contains("NotFound", exception.Message);
            Assert.Equal(DefaultHandlerNotFoundCode, exception.HResult);
        }

        [Fact]
        public void Validate_UserAuthorizationWithoutHandlers_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplication:UserAuthorization:AutoSignIn", "true" },
                { "AgentApplication:UserAuthorization:DefaultHandlerName", "auto" }
            });

            var exception = Assert.Throws<InvalidOperationException>(() => AgentApplicationConfigurationValidator.Validate(configuration));

            Assert.Equal(NoUserAuthorizationHandlersCode, exception.HResult);
        }

        [Fact]
        public void Validate_ClassNameSection_Throws()
        {
            // AgentApplicationOptions falls back to the class name when the section name isn't found.
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplicationOptions:UserAuthorization:DefaultHandlerName", "NotFound" },
                { "AgentApplicationOptions:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "connection" }
            });

            Assert.Throws<IndexOutOfRangeException>(() => AgentApplicationConfigurationValidator.Validate(configuration));
        }

        [Fact]
        public void Validate_CustomConfigKey_Throws()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "MyAgent:UserAuthorization:DefaultHandlerName", "NotFound" },
                { "MyAgent:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "connection" }
            });

            Assert.Throws<IndexOutOfRangeException>(() => AgentApplicationConfigurationValidator.Validate(configuration, "MyAgent"));
        }

        [Fact]
        public void Validate_CustomConfigKeyNotUsedByConfiguration_DoesNotThrow()
        {
            // An invalid section that AgentApplicationOptions won't read isn't validated.
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "MyAgent:UserAuthorization:DefaultHandlerName", "NotFound" },
                { "MyAgent:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "connection" }
            });

            AgentApplicationConfigurationValidator.Validate(configuration);
        }

        private static IConfiguration CreateUserAuthorizationConfiguration(string defaultHandlerName)
        {
            var settings = new Dictionary<string, string>
            {
                { "AgentApplication:UserAuthorization:AutoSignIn", "true" },
                { "AgentApplication:UserAuthorization:DefaultHandlerName", defaultHandlerName },
                { "AgentApplication:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "auto-connection" },
                { "AgentApplication:UserAuthorization:Handlers:me:Settings:AzureBotOAuthConnectionName", "me-connection" }
            };

            return CreateConfiguration(settings);
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
