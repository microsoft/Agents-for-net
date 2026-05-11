// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseFactoryTests
    {
        [Fact]
        public void Registry_Register_And_TryGet_ReturnsFactory()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory = new Mock<IStreamingResponseFactory>().Object;

            registry.Register("slack", factory);

            Assert.True(registry.TryGet("slack", out var result));
            Assert.Same(factory, result);
        }

        [Fact]
        public void Registry_TryGet_UnknownChannel_ReturnsFalse()
        {
            var registry = new StreamingResponseFactoryRegistry();

            Assert.False(registry.TryGet("unknown", out var result));
            Assert.Null(result);
        }

        [Fact]
        public void Registry_Register_OverwritesPrevious()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory1 = new Mock<IStreamingResponseFactory>().Object;
            var factory2 = new Mock<IStreamingResponseFactory>().Object;

            registry.Register("slack", factory1);
            registry.Register("slack", factory2);

            registry.TryGet("slack", out var result);
            Assert.Same(factory2, result);
        }

        [Fact]
        public void AddStreamingResponseFactory_PopulatesRegistry()
        {
            var services = new ServiceCollection();
            services.AddStreamingResponseFactory<TestFactory>("slack");

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

            Assert.True(registry.TryGet("slack", out var factory));
            Assert.NotNull(factory);
        }

        [Fact]
        public void AddStreamingResponseFactory_TwoChannels_BothInRegistry()
        {
            var services = new ServiceCollection();
            services.AddStreamingResponseFactory<TestFactory>("slack");
            services.AddStreamingResponseFactory<TestFactory2>("teams");

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

            Assert.True(registry.TryGet("slack", out _));
            Assert.True(registry.TryGet("teams", out _));
        }

        [Fact]
        public void AddStreamingResponseFactory_InstanceOverload_Works()
        {
            var services = new ServiceCollection();
            var factory = new TestFactory();
            services.AddStreamingResponseFactory("slack", factory);

            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

            Assert.True(registry.TryGet("slack", out var resolved));
            Assert.Same(factory, resolved);
        }

        [Fact]
        public void Resolve_ExplicitRegistration_TakesPrecedence()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory = new Mock<IStreamingResponseFactory>().Object;
            registry.Register("slack", factory);

            var result = registry.Resolve("slack", services: null);

            Assert.Same(factory, result);
        }

        [Fact]
        public void Resolve_UnknownChannel_ReturnsNull()
        {
            var registry = new StreamingResponseFactoryRegistry();

            var result = registry.Resolve("unknown", services: null);

            Assert.Null(result);
        }

        [Fact]
        public void Resolve_DiscoveredFactories_AreSharedAcrossRegistryInstances()
        {
            var registry1 = new StreamingResponseFactoryRegistry();
            var registry2 = new StreamingResponseFactoryRegistry();
            ResetRegistryStatics();

            try
            {
                SetDiscoveredType(registry1, "shared", typeof(TestFactory));
                SetScanned(true);

                var result = registry2.Resolve("shared", services: null);

                Assert.NotNull(result);
                Assert.IsType<TestFactory>(result);
            }
            finally
            {
                ResetRegistryStatics();
            }
        }

        [Fact]
        public void Resolve_WhenConcurrentCacheInsertHappens_ReturnsCachedInstance()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var cachedFactory = new TestFactory();
            ResetRegistryStatics();

            try
            {
                SetDiscoveredType(registry, "shared", typeof(TestFactory));
                SetScanned(true);

                var services = new RegisteringServiceProvider(registry, "shared", cachedFactory, new TestFactory());

                var result = registry.Resolve("shared", services);

                Assert.Same(cachedFactory, result);
            }
            finally
            {
                ResetRegistryStatics();
            }
        }

        private static void ResetRegistryStatics()
        {
            SetScanned(false);

            var discoveredTypesField = typeof(StreamingResponseFactoryRegistry).GetField("_discoveredTypes", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (discoveredTypesField?.IsStatic == true && discoveredTypesField.GetValue(null) is ConcurrentDictionary<string, Type> discoveredTypes)
            {
                discoveredTypes.Clear();
            }
        }

        private static void SetScanned(bool value)
        {
            var scannedField = typeof(StreamingResponseFactoryRegistry).GetField("_scanned", BindingFlags.NonPublic | BindingFlags.Static);
            scannedField?.SetValue(null, value);
        }

        private static void SetDiscoveredType(StreamingResponseFactoryRegistry registry, string channelId, Type factoryType)
        {
            var discoveredTypesField = typeof(StreamingResponseFactoryRegistry).GetField("_discoveredTypes", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Assert.NotNull(discoveredTypesField);

            if (discoveredTypesField.IsStatic)
            {
                var discoveredTypes = Assert.IsType<ConcurrentDictionary<string, Type>>(discoveredTypesField.GetValue(null));
                discoveredTypes[channelId] = factoryType;
                return;
            }

            var instanceDiscoveredTypes = Assert.IsType<ConcurrentDictionary<string, Type>>(discoveredTypesField.GetValue(registry));
            instanceDiscoveredTypes[channelId] = factoryType;
        }

        private class RegisteringServiceProvider : IServiceProvider
        {
            private readonly StreamingResponseFactoryRegistry _registry;
            private readonly string _channelId;
            private readonly IStreamingResponseFactory _registeredFactory;
            private readonly object _returnedService;

            public RegisteringServiceProvider(StreamingResponseFactoryRegistry registry, string channelId, IStreamingResponseFactory registeredFactory, object returnedService)
            {
                _registry = registry;
                _channelId = channelId;
                _registeredFactory = registeredFactory;
                _returnedService = returnedService;
            }

            public object GetService(Type serviceType)
            {
                _registry.Register(_channelId, _registeredFactory);
                return _returnedService;
            }
        }

        private class TestFactory : IStreamingResponseFactory
        {
            public IStreamingResponse Create(ITurnContext ctx) => new Mock<IStreamingResponse>().Object;
        }
        private class TestFactory2 : IStreamingResponseFactory
        {
            public IStreamingResponse Create(ITurnContext ctx) => new Mock<IStreamingResponse>().Object;
        }
    }
}
