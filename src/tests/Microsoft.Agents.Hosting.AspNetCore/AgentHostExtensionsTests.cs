// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
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

        [Fact]
        public async Task AddAgentM365AttachmentDownloader_BindsInlineImagesWithoutTeamsExtension()
        {
            var builder = CreateBuilder(Environments.Development);
            var connections = new Mock<IConnections>(MockBehavior.Strict);
            var httpFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
            builder.Object.Services.AddSingleton(connections.Object);
            builder.Object.Services.AddSingleton(httpFactory.Object);
            builder.Object.Services.AddLogging();
            builder.Object.AddAgentM365AttachmentDownloader();
            using var services = builder.Object.Services.BuildServiceProvider();
            var downloader = Assert.Single(services.GetRequiredService<IList<IInputFileDownloader>>());
            var context = new Mock<ITurnContext>();
            context.SetupGet(value => value.Activity).Returns(new Activity
            {
                ChannelId = Channels.Msteams,
                ServiceUrl = "https://files.contoso.com/amer/",
                Attachments =
                [
                    new Attachment
                    {
                        ContentType = "image/png",
                        ContentUrl = "https://evil.example/amer/v3/attachments/pic-1/views/original"
                    }
                ]
            });

            var result = await downloader.DownloadFilesAsync(context.Object, null, CancellationToken.None);

            Assert.Empty(result);
            connections.VerifyNoOtherCalls();
            httpFactory.VerifyNoOtherCalls();
        }
    }
}
