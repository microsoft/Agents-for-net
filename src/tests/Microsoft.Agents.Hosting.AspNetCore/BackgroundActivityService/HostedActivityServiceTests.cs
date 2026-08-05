// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(sp.Object, null, queue, logger.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullServiceProvider()
        {
            var config = new ConfigurationBuilder().Build();
            var adapter = new TestAdapter();
            var queue = new ActivityTaskQueue();
            var logger = new Mock<ILogger<HostedActivityService>>();

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(null, config, queue, logger.Object));
        }

        [Fact]
        public void Constructor_ShouldThrowWithNullActivityTaskQueue()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();
            var logger = new Mock<ILogger<HostedActivityService>>();
            var sp = new Mock<IServiceProvider>();

            Assert.Throws<ArgumentNullException>(() => new HostedActivityService(sp.Object, config, null, logger.Object));
        }

        [Fact]
        public async Task Constructor_ShouldInstantiateNullLogger()
        {
            var config = new ConfigurationBuilder().Build();
            var bot = new ActivityHandler();
            var adapter = new TestAdapter();
            var queue = new ActivityTaskQueue();
            var sp = new Mock<IServiceProvider>();

            try
            {
                var service = new HostedActivityService(sp.Object, config, queue, null);
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
        public async Task ExecuteAsync_WithScopePerTurn_ShouldResolveScopedDependencyPerTurn()
        {
            var record = UseScopedRecord(useScopePerTurn: true, expectedTurns: 2);

            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());
            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());

            await record.Service.StartAsync(CancellationToken.None);
            await record.AllTurnsProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await record.Service.StopAsync(CancellationToken.None);

            var probes = record.Collector.Resolved.ToArray();
            Assert.Equal(2, probes.Length);
            Assert.NotSame(probes[0], probes[1]);
            Assert.All(probes, probe => Assert.True(probe.IsDisposed, "Turn scope was not disposed."));
        }

        [Fact]
        public async Task ExecuteAsync_WithoutScopePerTurn_ShouldShareScopedDependencyAcrossTurns()
        {
            var record = UseScopedRecord(useScopePerTurn: false, expectedTurns: 2);

            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());
            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());

            await record.Service.StartAsync(CancellationToken.None);
            await record.AllTurnsProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await record.Service.StopAsync(CancellationToken.None);

            // Default behavior: resolving from the root provider promotes the scoped registration to the
            // root scope, so every turn shares one instance and it is not disposed between turns.
            var probes = record.Collector.Resolved.ToArray();
            Assert.Equal(2, probes.Length);
            Assert.Same(probes[0], probes[1]);
            Assert.All(probes, probe => Assert.False(probe.IsDisposed));
        }

        [Fact]
        public async Task ExecuteAsync_WithScopePerTurnFromConfiguration_ShouldResolveScopedDependencyPerTurn()
        {
            // AdapterOptions is not registered in DI, so the CloudAdapterOptions configuration section
            // is the path applications actually use to enable this.
            var record = UseScopedRecord(useScopePerTurn: true, expectedTurns: 2, viaConfiguration: true);

            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());
            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());

            await record.Service.StartAsync(CancellationToken.None);
            await record.AllTurnsProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await record.Service.StopAsync(CancellationToken.None);

            var probes = record.Collector.Resolved.ToArray();
            Assert.Equal(2, probes.Length);
            Assert.NotSame(probes[0], probes[1]);
            Assert.All(probes, probe => Assert.True(probe.IsDisposed, "Turn scope was not disposed."));
        }

        private static ScopedRecord UseScopedRecord(bool useScopePerTurn, int expectedTurns, bool viaConfiguration = false)
        {
            var collector = new ProbeCollector();
            var services = new ServiceCollection();
            services.AddSingleton(collector);
            services.AddScoped<ScopedProbe>();
            services.AddTransient<IAgent, ProbeAgent>();

            var serviceProvider = services.BuildServiceProvider();
            var queue = new ActivityTaskQueue();

            // AdapterOptions is not registered in DI, so applications configure it through the
            // "CloudAdapterOptions" section. Exercise both that path and the explicit options parameter.
            var configBuilder = new ConfigurationBuilder();
            if (viaConfiguration)
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["CloudAdapterOptions:UseScopePerTurn"] = useScopePerTurn.ToString()
                });
            }

            var service = new HostedActivityService(
                serviceProvider,
                configBuilder.Build(),
                queue,
                new Mock<ILogger<HostedActivityService>>().Object,
                viaConfiguration ? null : new AdapterOptions { UseScopePerTurn = useScopePerTurn });

            var allTurnsProcessed = new TaskCompletionSource();
            var processed = 0;
            var adapter = new Mock<IChannelAdapter>();
            adapter
                .Setup(a => a.ProcessActivityAsync(It.IsAny<ClaimsIdentity>(), It.IsAny<Activity>(), It.IsAny<AgentCallbackHandler>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Callback(() =>
                {
                    if (Interlocked.Increment(ref processed) == expectedTurns)
                    {
                        allTurnsProcessed.TrySetResult();
                    }
                });

            return new(service, queue, adapter, collector, allTurnsProcessed);
        }

        private record ScopedRecord(
            HostedActivityService Service,
            ActivityTaskQueue Queue,
            Mock<IChannelAdapter> Adapter,
            ProbeCollector Collector,
            TaskCompletionSource AllTurnsProcessed);

        private sealed class ScopedProbe : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }

        private sealed class ProbeCollector
        {
            public ConcurrentQueue<ScopedProbe> Resolved { get; } = new();
        }

        private sealed class ProbeAgent : IAgent
        {
            public ProbeAgent(ScopedProbe probe, ProbeCollector collector)
            {
                collector.Resolved.Enqueue(probe);
            }

            public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
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

            var service = new HostedActivityService(sp.Object, config, queue, logger.Object);
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