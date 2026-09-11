// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AUserAuthorizationConfigurationTests
{
    [Fact]
    public void Constructor_WithExplicitA2ATypeAndNoAssembly_LoadsHandler()
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
}
