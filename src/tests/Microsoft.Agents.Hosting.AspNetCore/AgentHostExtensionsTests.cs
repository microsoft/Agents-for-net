// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class AgentHostExtensionsTests
    {
        private static Mock<IHostApplicationBuilder> CreateBuilder(string environmentName)
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

            var builder = new Mock<IHostApplicationBuilder>();
            builder.SetupGet(e => e.Services).Returns(new ServiceCollection());
            builder.SetupGet(e => e.Environment).Returns(environment.Object);
            return builder;
        }

        [Fact]
        public void AddAgentAuthorization_ForceEnableTrue_RegistersMarkerAndInvokesConfigure()
        {
            var builder = CreateBuilder(Environments.Development);
            var configureInvoked = false;

            builder.Object.AddAgentAuthorization(b => configureInvoked = true, forceEnable: true);

            Assert.True(configureInvoked);
            Assert.Contains(builder.Object.Services, s => s.ServiceType == typeof(AgentAuthConfigured));
        }

        [Fact]
        public void AddAgentAuthorization_ForceEnableFalse_DoesNotRegisterMarkerOrInvokeConfigure()
        {
            var builder = CreateBuilder(Environments.Production);
            var configureInvoked = false;

            builder.Object.AddAgentAuthorization(b => configureInvoked = true, forceEnable: false);

            Assert.False(configureInvoked);
            Assert.DoesNotContain(builder.Object.Services, s => s.ServiceType == typeof(AgentAuthConfigured));
        }

        [Fact]
        public void AddAgentAuthorization_DevelopmentEnvironment_DisabledByDefault()
        {
            var builder = CreateBuilder(Environments.Development);

            builder.Object.AddAgentAuthorization(b => { });

            Assert.DoesNotContain(builder.Object.Services, s => s.ServiceType == typeof(AgentAuthConfigured));
        }

        [Fact]
        public void AddAgentAuthorization_NonDevelopmentEnvironment_EnabledByDefault()
        {
            var builder = CreateBuilder(Environments.Production);

            builder.Object.AddAgentAuthorization(b => { });

            Assert.Contains(builder.Object.Services, s => s.ServiceType == typeof(AgentAuthConfigured));
        }

        [Fact]
        public void AddAgentAuthorization_NullBuilder_Throws()
        {
            IHostApplicationBuilder builder = null;

            Assert.Throws<ArgumentNullException>(() => builder.AddAgentAuthorization(b => { }));
        }

        [Fact]
        public void AddAgentAuthorization_NullConfigure_Throws()
        {
            var builder = CreateBuilder(Environments.Production);

            Assert.Throws<ArgumentNullException>(() => builder.Object.AddAgentAuthorization(null));
        }
    }
}
