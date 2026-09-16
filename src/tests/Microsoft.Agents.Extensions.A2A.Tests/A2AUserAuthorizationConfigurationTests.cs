// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AUserAuthorizationConfigurationTests
{
    [Fact]
    public void AuthorizationTypes_ExposeOnlySupportedMetadata()
    {
        Assert.Equal(
            ["EnforceRequiredScopes", "OAuthFlows", "RequiredScopes", "SecuritySchemeName"],
            typeof(A2AUserAuthorizationSettings)
                .GetProperties()
                .Where(property => property.DeclaringType == typeof(A2AUserAuthorizationSettings))
                .Select(property => property.Name)
                .OrderBy(name => name));
        Assert.Equal(
            ["HandlerName", "OBOSettings", "ReferencedSecurityScheme", "RequiredScopes", "SecurityScheme", "SecuritySchemeName"],
            typeof(A2AAuthorizationMetadata)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name));
    }

    [Fact]
    public void Configuration_WithSecuritySchemeNameAndNoOAuthFlows_ReferencesExistingScheme()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "agentBearer",
                  "RequiredScopes": [ "api://agent/access_as_user" ]
                }
              }
            }
            """);

        var metadata = Assert.Single(A2AAuthorizationMetadata.Resolve(configuration));

        Assert.Equal("request", metadata.HandlerName);
        Assert.Equal("agentBearer", metadata.SecuritySchemeName);
        Assert.Equal("agentBearer", metadata.ReferencedSecurityScheme);
        Assert.Null(metadata.SecurityScheme);
        Assert.Equal(["api://agent/access_as_user"], metadata.RequiredScopes);
    }

    [Fact]
    public void Configuration_WithInlineDeviceCodeFlow_BindsA2AOAuthFlows()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "deviceCode",
                  "OAuthFlows": {
                    "DeviceCode": {
                      "DeviceAuthorizationUrl": "https://login.example.com/devicecode",
                      "TokenUrl": "https://login.example.com/token",
                      "Scopes": {
                        "agent.read": "Access the agent"
                      }
                    }
                  }
                }
              }
            }
            """);

        var metadata = Assert.Single(A2AAuthorizationMetadata.Resolve(configuration));

        Assert.Equal("deviceCode", metadata.SecuritySchemeName);
        Assert.Equal("https://login.example.com/devicecode", metadata.SecurityScheme.OAuth2SecurityScheme.Flows.DeviceCode.DeviceAuthorizationUrl);
        Assert.Equal("Access the agent", metadata.SecurityScheme.OAuth2SecurityScheme.Flows.DeviceCode.Scopes["agent.read"]);
    }

    [Fact]
    public void Configuration_WithoutRequiredScopes_LeavesOptionalMetadataUnset()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "deviceCode",
                  "OAuthFlows": {
                    "DeviceCode": {
                      "DeviceAuthorizationUrl": "https://login.example.com/devicecode",
                      "TokenUrl": "https://login.example.com/token",
                      "Scopes": {
                        "agent.read": "Access the agent"
                      }
                    }
                  }
                }
              }
            }
            """);
        var metadata = Assert.Single(A2AAuthorizationMetadata.Resolve(configuration));
        Assert.Null(metadata.RequiredScopes);
        Assert.Null(metadata.RequiredScopes);
    }

    [Fact]
    public void Configuration_WithSecuritySchemeNameAndInlineFlow_DefinesInlineScheme()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "deviceCode",
                  "OAuthFlows": {
                    "DeviceCode": {
                      "DeviceAuthorizationUrl": "https://login.example.com/devicecode",
                      "TokenUrl": "https://login.example.com/token"
                    }
                  }
                }
              }
            }
            """);

        var metadata = Assert.Single(A2AAuthorizationMetadata.Resolve(configuration));

        Assert.Equal("deviceCode", metadata.SecuritySchemeName);
        Assert.Null(metadata.ReferencedSecurityScheme);
        Assert.NotNull(metadata.SecurityScheme);
    }

    [Fact]
    public void Configuration_WithMixedCaseTypeAndAssembly_ResolvesAuthorizationMetadata()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Type": "mIcRoSoFt.aGeNtS.eXtEnSiOnS.A2A.aUtHoRiZaTiOn.A2AuSeRaUtHoRiZaTiOn",
                "Assembly": "mIcRoSoFt.aGeNtS.eXtEnSiOnS.A2A",
                "Settings": {
                  "SecuritySchemeName": "agentBearer"
                }
              }
            }
            """);

        var metadata = Assert.Single(A2AAuthorizationMetadata.Resolve(configuration));

        Assert.Equal("agentBearer", metadata.SecuritySchemeName);
    }

    [Fact]
    public void Configuration_WithMultipleOAuthFlows_Throws()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "agentOAuth",
                  "OAuthFlows": {
                    "ClientCredentials": {
                      "TokenUrl": "https://login.example.com/token"
                    },
                    "DeviceCode": {
                      "DeviceAuthorizationUrl": "https://login.example.com/devicecode",
                      "TokenUrl": "https://login.example.com/token"
                    }
                  }
                }
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => A2AAuthorizationMetadata.Resolve(configuration));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100008);
        Assert.Equal("OAuthFlows must specify exactly one OAuth flow.", exception.Message);
    }

    [Fact]
    public void Configuration_WithInlineOAuthFlowAndNoSchemeName_Throws()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "OAuthFlows": {
                    "DeviceCode": {
                      "DeviceAuthorizationUrl": "https://login.example.com/devicecode",
                      "TokenUrl": "https://login.example.com/token"
                    }
                  }
                }
              }
            }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => A2AAuthorizationMetadata.Resolve(configuration));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100007);
        Assert.Equal("SecuritySchemeName is required when OAuthFlows is configured.", exception.Message);
    }

    [Fact]
    public void Configuration_WithEnforcementAndNoRequiredScopes_Throws()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "delegated",
                  "EnforceRequiredScopes": true
                }
              }
            }
            """);

        IConfigurationSection settings = configuration.GetSection(
            "AgentApplication:UserAuthorization:Handlers:request:Settings");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new A2AUserAuthorization(
                "request",
                new MemoryStorage(),
                Mock.Of<IConnections>(),
                settings,
                NullLogger.Instance));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100020);
    }

    [Fact]
    public void CodeFirstConstructor_WithEnforcementAndNoRequiredScopes_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new A2AUserAuthorization(
                "request",
                Mock.Of<IConnections>(),
                new A2AUserAuthorizationSettings
                {
                    SecuritySchemeName = "delegated",
                    EnforceRequiredScopes = true,
                },
                NullLogger.Instance));

        A2AErrorMetadataAssertions.AssertErrorMetadata(exception, -100020);
    }

    [Fact]
    public void Configuration_KeepsOBOScopesSeparateFromAgentCardScopes()
    {
        var configuration = CreateAgentApplicationConfiguration(
            """
            {
              "request": {
                "Assembly": "Microsoft.Agents.Extensions.A2A",
                "Type": "A2AUserAuthorization",
                "Settings": {
                  "SecuritySchemeName": "agentBearer",
                  "RequiredScopes": [ "api://agent/access_as_user" ],
                  "OBOScopes": [ "User.Read" ]
                }
              }
            }
            """);

        var metadata = Assert.Single(A2AAuthorizationMetadata.Resolve(configuration));

        Assert.Equal(["api://agent/access_as_user"], metadata.RequiredScopes);
        Assert.Equal(["User.Read"], metadata.OBOSettings.OBOScopes);
    }

    [Fact]
    public void Constructor_WithExplicitA2ATypeAndNoAssembly_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["UserAuthorization:DefaultHandlerName"] = "request",
                ["UserAuthorization:AutoSignIn"] = "true",
                ["UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
                ["UserAuthorization:Handlers:request:Settings:OBOScopes:0"] = "scope"
            })
            .Build();
        var storage = new MemoryStorage();
        var services = new ServiceCollection()
            .AddSingleton<IStorage>(storage)
            .AddSingleton(Mock.Of<IConnections>())
            .BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new UserAuthorizationOptions(
                services,
                NullLoggerFactory.Instance,
                configuration,
                storage));

        Assert.Contains("Assembly", exception.Message, StringComparison.Ordinal);
        Assert.Contains("request", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WithExplicitA2ATypeAndAssembly_LoadsHandler()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["UserAuthorization:DefaultHandlerName"] = "request",
                ["UserAuthorization:AutoSignIn"] = "true",
                ["UserAuthorization:Handlers:request:Assembly"] = "Microsoft.Agents.Extensions.A2A",
                ["UserAuthorization:Handlers:request:Type"] = "A2AUserAuthorization",
                ["UserAuthorization:Handlers:request:Settings:OBOScopes:0"] = "scope"
            })
            .Build();
        var storage = new MemoryStorage();
        var services = new ServiceCollection()
            .AddSingleton<IStorage>(storage)
            .AddSingleton(Mock.Of<IConnections>())
            .BuildServiceProvider();

        var options = new UserAuthorizationOptions(
            services,
            NullLoggerFactory.Instance,
            configuration,
            storage);

        Assert.Equal("request", options.DefaultHandlerName);
    }

    [Fact]
    public void Constructor_WithEmptySettingsSection_CreatesHandlerWithoutOBO()
    {
        // The A2AAgent sample configures delegated passthrough and application-token handlers with
        // an empty "Settings": {} node. JSON configuration turns that into a section with no children,
        // and IConfiguration binding returns null for such a section.
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
                """
                {
                  "UserAuthorization": {
                    "DefaultHandlerName": "delegated",
                    "AutoSignIn": false,
                    "Handlers": {
                      "delegated": {
                        "Assembly": "Microsoft.Agents.Extensions.A2A",
                        "Type": "A2AUserAuthorization",
                        "Settings": {}
                      }
                    }
                  }
                }
                """)))
            .Build();

        var settings = configuration.GetSection("UserAuthorization:Handlers:delegated:Settings");
        Assert.False(settings.Exists());

        var handler = new A2AUserAuthorization(
            "delegated",
            new MemoryStorage(),
            Mock.Of<IConnections>(),
            settings,
            NullLogger.Instance);

        Assert.Equal("delegated", handler.Name);
    }

    private static IConfiguration CreateAgentApplicationConfiguration(string handlers)
        => new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
                $$"""
                {
                  "AgentApplication": {
                    "UserAuthorization": {
                      "Handlers": {{handlers}}
                    }
                  }
                }
                """)))
            .Build();
}
