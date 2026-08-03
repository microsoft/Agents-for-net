// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class UserAuthorizationDispatcherTests
    {
        private const int NoUserAuthorizationHandlersCode = -50012;

        [Fact]
        public void Constructor_MissingHandlersSection_ThrowsNoHandlersDefined()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplication:UserAuthorization:DefaultHandlerName", "auto" }
            });

            var exception = Assert.Throws<InvalidOperationException>(() => CreateDispatcher(configuration));

            Assert.Equal(NoUserAuthorizationHandlersCode, exception.HResult);
        }

        [Fact]
        public void Constructor_HandlersDefined_LoadsHandlerNames()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string>
            {
                { "AgentApplication:UserAuthorization:Handlers:auto:Settings:AzureBotOAuthConnectionName", "auto-connection" }
            });

            var dispatcher = CreateDispatcher(configuration);

            Assert.False(dispatcher.TryGet("unknown", out var handler));
            Assert.Null(handler);
        }

        private static UserAuthorizationDispatcher CreateDispatcher(IConfiguration configuration)
        {
            return new UserAuthorizationDispatcher(
                new Mock<IServiceProvider>().Object,
                NullLoggerFactory.Instance,
                configuration,
                new MemoryStorage(),
                configKey: "AgentApplication:UserAuthorization:Handlers");
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
