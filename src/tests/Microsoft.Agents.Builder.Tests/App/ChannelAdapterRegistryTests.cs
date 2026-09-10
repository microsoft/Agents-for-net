// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class ChannelAdapterRegistryTests
    {
        private abstract class FakeAdapter : ChannelAdapter
        {
            public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
                => Task.FromResult(Array.Empty<ResourceResponse>());
        }

        private sealed class DefaultAdapter : FakeAdapter { }

        private sealed class TeamsAdapter : FakeAdapter { }

        private static IServiceProvider BuildProvider(Action<ServiceCollection> configure = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IChannelAdapter, DefaultAdapter>();
            configure?.Invoke(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void GetDefault_ReturnsRegisteredChannelAdapter()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(sp, Enumerable.Empty<ChannelAdapterRegistration>());

            Assert.IsType<DefaultAdapter>(registry.GetDefault());
        }

        [Fact]
        public void NoChannelSpecificAdapters_HasChannelSpecificAdaptersIsFalse()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(sp, Enumerable.Empty<ChannelAdapterRegistration>());

            Assert.False(registry.HasChannelSpecificAdapters);
        }

        [Fact]
        public void ExplicitRegistration_ResolvesChannelAdapter_CreatedWhenNotInDI()
        {
            // TeamsAdapter is NOT registered in DI; the registry creates it via ActivatorUtilities.
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(
                sp,
                new[] { new ChannelAdapterRegistration("msteams", typeof(TeamsAdapter)) });

            Assert.True(registry.HasChannelSpecificAdapters);
            Assert.True(registry.TryGetAdapter("msteams", out var adapter));
            Assert.IsType<TeamsAdapter>(adapter);
            Assert.IsType<TeamsAdapter>(registry.GetAdapter("MSTEAMS")); // case-insensitive
        }

        [Fact]
        public void ResolvedChannelAdapter_IsCached()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(
                sp,
                new[] { new ChannelAdapterRegistration("msteams", typeof(TeamsAdapter)) });

            var first = registry.GetAdapter("msteams");
            var second = registry.GetAdapter("msteams");

            Assert.Same(first, second);
        }

        [Fact]
        public void SuppliedDefaultAdapter_IsReusedForMatchingChannelRegistration()
        {
            var sp = BuildProvider();
            var adapter = new TeamsAdapter();
            var registry = new ChannelAdapterRegistry(
                sp,
                new[] { new ChannelAdapterRegistration("msteams", typeof(TeamsAdapter)) },
                adapter);

            Assert.Same(adapter, registry.GetAdapter("msteams"));
        }

        [Fact]
        public void TryGetAdapter_UnknownChannel_ReturnsFalse()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(
                sp,
                new[] { new ChannelAdapterRegistration("msteams", typeof(TeamsAdapter)) });

            Assert.False(registry.TryGetAdapter("slack", out var adapter));
            Assert.Null(adapter);
        }

        [Fact]
        public void GetAdapter_UnknownChannel_Throws()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(sp, Enumerable.Empty<ChannelAdapterRegistration>());

            Assert.Throws<InvalidOperationException>(() => registry.GetAdapter("slack"));
        }

        [Fact]
        public void GetAll_ReturnsDefaultAndChannelAdapters()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(
                sp,
                new[] { new ChannelAdapterRegistration("msteams", typeof(TeamsAdapter)) });

            var all = registry.GetAll().ToList();

            Assert.Contains(all, a => a is DefaultAdapter);
            Assert.Contains(all, a => a is TeamsAdapter);
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void ExplicitRegistration_OverridesForSameChannel()
        {
            var sp = BuildProvider();
            var registry = new ChannelAdapterRegistry(
                sp,
                new[]
                {
                    new ChannelAdapterRegistration("msteams", typeof(DefaultAdapter)),
                    new ChannelAdapterRegistration("msteams", typeof(TeamsAdapter)),
                });

            // Last explicit registration for a channelId wins.
            Assert.IsType<TeamsAdapter>(registry.GetAdapter("msteams"));
        }

        [Fact]
        public void DefaultRegistration_ResolvesSelectedAdapter()
        {
            var services = new ServiceCollection();
            services.AddChannelAdapter<TeamsAdapter>("msteams");
            services.SetDefaultChannelAdapter<TeamsAdapter>();
            using var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<IChannelAdapterRegistry>();

            Assert.IsType<TeamsAdapter>(registry.GetDefault());
            Assert.Same(registry.GetDefault(), provider.GetRequiredService<IChannelAdapter>());
            Assert.Single(registry.GetAll());
        }

        [Fact]
        public void SetDefaultChannelAdapter_ReplacesFrameworkDefault()
        {
            var services = new ServiceCollection();
            services.TrySetDefaultChannelAdapter<DefaultAdapter>();
            services.SetDefaultChannelAdapter<TeamsAdapter>();
            using var provider = services.BuildServiceProvider();

            Assert.IsType<TeamsAdapter>(provider.GetRequiredService<IChannelAdapter>());
        }

        [Fact]
        public void TrySetDefaultChannelAdapter_DoesNotReplaceExplicitDefault()
        {
            var services = new ServiceCollection();
            services.SetDefaultChannelAdapter<TeamsAdapter>();
            services.TrySetDefaultChannelAdapter<DefaultAdapter>();
            using var provider = services.BuildServiceProvider();

            Assert.IsType<TeamsAdapter>(provider.GetRequiredService<IChannelAdapter>());
        }

        [Fact]
        public void AddChannelAdapter_RejectsScopedAdapterRegistration()
        {
            var services = new ServiceCollection();
            services.AddScoped<TeamsAdapter>();

            Assert.Throws<InvalidOperationException>(() =>
                services.AddChannelAdapter<TeamsAdapter>("msteams"));
        }
    }
}
