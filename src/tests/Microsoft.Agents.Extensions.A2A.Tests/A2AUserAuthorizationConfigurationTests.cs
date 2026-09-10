// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Generic;

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
}
