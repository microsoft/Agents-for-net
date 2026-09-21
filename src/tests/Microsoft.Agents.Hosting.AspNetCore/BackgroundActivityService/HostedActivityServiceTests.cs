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
using System.Linq;
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
        public async Task ExecuteAsync_WithDefaultOptions_ShouldResolveAndDisposeDependencyPerActivity()
        {
            var record = UseScopedRecord(useScopedServices: null, expectedActivities: 2);

            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());
            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());

            await record.Service.StartAsync(CancellationToken.None);
            await record.AllActivitiesProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await record.Service.StopAsync(CancellationToken.None);

            var probes = record.Collector.Resolved.ToArray();
            Assert.Equal(2, probes.Length);
            Assert.NotSame(probes[0], probes[1]);
            Assert.All(probes, probe => Assert.True(probe.IsDisposed));
        }

        [Fact]
        public async Task ExecuteAsync_WithoutScopedServices_ShouldResolveDependencyFromRootProvider()
        {
            var record = UseScopedRecord(useScopedServices: false, expectedActivities: 2);

            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());
            record.Queue.QueueBackgroundActivity(new ClaimsIdentity(), record.Adapter.Object, new Activity());

            await record.Service.StartAsync(CancellationToken.None);
            await record.AllActivitiesProcessed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await record.Service.StopAsync(CancellationToken.None);

            var probes = record.Collector.Resolved.ToArray();
            Assert.Equal(2, probes.Length);
            Assert.Same(probes[0], probes[1]);
            Assert.All(probes, probe => Assert.False(probe.IsDisposed));
        }

        [Fact]
        public async Task ExecuteAsync_WithScopedServices_ShouldDisposeAsyncDependencyAndCompleteOnce()
        {
            var collector = new AsyncProbeCollector();
            var services = new ServiceCollection();
            services.AddSingleton(collector);
            services.AddScoped<AsyncScopedProbe>();
            services.AddTransient<IAgent, AsyncProbeAgent>();

            await using var provider = services.BuildServiceProvider();
            var queue = new ActivityTaskQueue();
            var adapter = new Mock<IChannelAdapter>();
            var adapterCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            adapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<Activity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Callback(() => adapterCalled.TrySetResult());

            var configuration = new ConfigurationBuilder().Build();
            var service = new HostedActivityService(
                provider,
                configuration,
                queue,
                Mock.Of<ILogger<HostedActivityService>>(),
                new HostedActivityServiceOptions(configuration) { UseScopedServices = true });
            var completionCount = 0;
            queue.QueueBackgroundActivity(
                new ClaimsIdentity(),
                adapter.Object,
                new Activity(),
                onComplete: _ =>
                {
                    Interlocked.Increment(ref completionCount);
                    return Task.CompletedTask;
                });

            await service.StartAsync(CancellationToken.None);
            await adapterCalled.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await service.StopAsync(CancellationToken.None);

            var probe = Assert.Single(collector.Resolved);
            Assert.True(probe.IsDisposed);
            Assert.Equal(1, completionCount);
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

        private static ScopedRecord UseScopedRecord(bool? useScopedServices, int expectedActivities)
        {
            var collector = new ProbeCollector();
            var services = new ServiceCollection();
            services.AddSingleton(collector);
            services.AddScoped<ScopedProbe>();
            services.AddTransient<IAgent, ProbeAgent>();

            var provider = services.BuildServiceProvider();
            var queue = new ActivityTaskQueue();
            var adapter = new Mock<IChannelAdapter>();
            var processed = 0;
            var allActivitiesProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            adapter
                .Setup(a => a.ProcessActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<Activity>(),
                    It.IsAny<AgentCallbackHandler>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvokeResponse())
                .Callback(() =>
                {
                    if (Interlocked.Increment(ref processed) == expectedActivities)
                    {
                        allActivitiesProcessed.TrySetResult();
                    }
                });

            var options = new HostedActivityServiceOptions(new ConfigurationBuilder().Build());
            if (useScopedServices.HasValue)
            {
                options.UseScopedServices = useScopedServices.Value;
            }
            var service = new HostedActivityService(
                provider,
                new ConfigurationBuilder().Build(),
                queue,
                Mock.Of<ILogger<HostedActivityService>>(),
                options);

            return new(service, queue, adapter, collector, allActivitiesProcessed);
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

            var options = new HostedActivityServiceOptions(config)
            {
                UseScopedServices = false
            };
            var service = new HostedActivityService(sp.Object, config, queue, logger.Object, options);
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

        private record ScopedRecord(
            HostedActivityService Service,
            ActivityTaskQueue Queue,
            Mock<IChannelAdapter> Adapter,
            ProbeCollector Collector,
            TaskCompletionSource AllActivitiesProcessed);

        private sealed class ScopedProbe : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
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

        private sealed class AsyncScopedProbe : IAsyncDisposable
        {
            public bool IsDisposed { get; private set; }

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class AsyncProbeCollector
        {
            public ConcurrentQueue<AsyncScopedProbe> Resolved { get; } = new();
        }

        private sealed class AsyncProbeAgent : IAgent
        {
            public AsyncProbeAgent(AsyncScopedProbe probe, AsyncProbeCollector collector)
            {
                collector.Resolved.Enqueue(probe);
            }

            public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }
}