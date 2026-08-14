// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class HostedActivityServiceTests
    {
        [Fact]
        public void Constructor_ShouldThrowWithNullConfig()
        {
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();
            var queue = new ActivityTaskQueue();
            var logger = new Mock<ILogger<HostedActivityService>>();
            var sp = new Mock<IServiceProvider>();
            var options = new HostedActivityServiceOptions(new ConfigurationBuilder().Build());

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(sp.Object, null, queue, logger.Object, options));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullServiceProvider()
        {
            var config = new ConfigurationBuilder().Build();
            var adapter = new TestAdapter();
            var queue = new ActivityTaskQueue();
            var logger = new Mock<ILogger<HostedActivityService>>();
            var options = new HostedActivityServiceOptions(config);

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(null, config, queue, logger.Object, options));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();
            var logger = new Mock<ILogger<HostedActivityService>>();
            var sp = new Mock<IServiceProvider>();
            var options = new HostedActivityServiceOptions(config);

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(sp.Object, config, null, logger.Object, options));
        }

        [Fact]
        public async Task Constructor_ShouldInstantiateNullLogger()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();
            var queue = new ActivityTaskQueue();
            var sp = new Mock<IServiceProvider>();
            var options = new HostedActivityServiceOptions(config);

            try
            {
                var service = new HostedActivityService(sp.Object, config, queue, null, options);
                await service.StopAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("NullLogger wasn't instantiated.");
            }
        }

        [Fact]
        public async Task ExecuteAsync_ShouldProcessQueuedActivity()
        {
            var record = UseRecord(new ActivityHandler());
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var source = new CancellationTokenSource();

            record.Adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<AgentCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Verifiable(Times.Once);

            record.Queue.QueueBackgroundActivity(claims, record.Adapter.Object, activity);
            await record.Service.StartAsync(source.Token).ContinueWith(async e =>
            {
                // Start and stop the service, waiting for the activity to be processed.
                await record.Service.StopAsync(source.Token);
                record.VerifyMocks();
            });
        }


        [Fact]
        public async Task ExecuteAsync_ShouldLogErrorWhenProcessingQueuedActivity()
        {
            var record = UseRecord(new ActivityHandler());
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var source = new CancellationTokenSource();

            record.Adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<AgentCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception())
                .Verifiable(Times.Once);
            record.Logger.Setup(e => e.Log(LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Verifiable(Times.Once);

            record.Queue.QueueBackgroundActivity(claims, record.Adapter.Object, activity);
            await record.Service.StartAsync(source.Token).ContinueWith(async e =>
            {
                // Start and stop the service, waiting for the activity to be processed.
                await record.Service.StopAsync(source.Token);
                record.VerifyMocks();
            });
        }

        [Fact]
        public void ExecuteAsync_ShouldCancelBackgroundProcess()
        {
            var record = UseRecord();
            var source = new CancellationTokenSource();

            record.Adapter.Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<AgentCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Verifiable(Times.Never);

            source.Cancel();
            var task = record.Service.StartAsync(source.Token);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            record.VerifyMocks();
        }

        [Fact]
        public async Task StopAsync_ShouldBeIdempotent()
        {
            var record = UseRecord();
            var token = CancellationToken.None;

            // Calling StopAsync more than once (as WebApplicationFactory/TestServer teardown can do)
            // must not throw LockRecursionException. See https://github.com/dotnet/aspnetcore/issues/40271.
            await record.Service.StopAsync(token);
            await record.Service.StopAsync(token);
        }

        [Fact]
        public void Constructor_WithHostedOptions_UsesHostedShutdownTimeout()
        {
            var config = new ConfigurationBuilder().Build();
            var hostedOptions = new HostedActivityServiceOptions(config)
            {
                ShutdownTimeoutSeconds = 17
            };

            var service = new HostedActivityService(
                new Mock<IServiceProvider>().Object,
                config,
                new ActivityTaskQueue(),
                Mock.Of<ILogger<HostedActivityService>>(),
                hostedOptions);

            Assert.Equal(17, GetShutdownTimeoutSeconds(service));
        }

        [Fact]
        public void Constructor_WithHostedOptions_DoesNotAcceptAdapterOptions()
        {
            var constructor = typeof(HostedActivityService).GetConstructor(
                [
                    typeof(IServiceProvider),
                    typeof(IConfiguration),
                    typeof(IActivityTaskQueue),
                    typeof(ILogger<HostedActivityService>),
                    typeof(HostedActivityServiceOptions)
                ]);

            Assert.NotNull(constructor);
        }

        [Fact]
        public void LegacyConstructor_WithAdapterOptions_UsesLegacyShutdownTimeout()
        {
            var config = new ConfigurationBuilder().Build();
#pragma warning disable CS0618
            var service = new HostedActivityService(
                new Mock<IServiceProvider>().Object,
                config,
                new ActivityTaskQueue(),
                Mock.Of<ILogger<HostedActivityService>>(),
                new AdapterOptions { ShutdownTimeoutSeconds = 29 });
#pragma warning restore CS0618

            Assert.Equal(29, GetShutdownTimeoutSeconds(service));
        }

        private static int GetShutdownTimeoutSeconds(HostedActivityService service)
        {
            var field = typeof(HostedActivityService).GetField(
                "_serviceOptions",
                BindingFlags.Instance | BindingFlags.NonPublic);

            var options = (HostedActivityServiceOptions)field.GetValue(service);
            return options.ShutdownTimeoutSeconds;
        }

        private static Record UseRecord(IAgent agent = null)
        {
            var config = new ConfigurationBuilder().Build();
            var queue = new ActivityTaskQueue();
            var bot = new Mock<ActivityHandler>();
            var adapter = new Mock<IChannelAdapter>();
            var logger = new Mock<ILogger<HostedActivityService>>();

            var sp = new Mock<IServiceProvider>();
            sp
                .Setup(s => s.GetService(It.IsAny<Type>()))
                .Returns(agent);

            var service = new HostedActivityService(sp.Object, config, queue, logger.Object, new HostedActivityServiceOptions(config));
            return new(service, queue, bot, adapter, logger);
        }

        private record Record(
            HostedActivityService Service,
            ActivityTaskQueue Queue,
            Mock<ActivityHandler> Bot,
            Mock<IChannelAdapter> Adapter,
            Mock<ILogger<HostedActivityService>> Logger)
        {
            public void VerifyMocks()
            {
                Mock.Verify(Bot, Adapter, Logger);
            }
        }
    }
}