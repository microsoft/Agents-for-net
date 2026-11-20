// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Xunit;

namespace Microsoft.Agents.Builder.Testing.Tests
{
    public class HttpSimulationFactoryTests
    {
        private const string Url = "https://service.example.test/items";
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RegistrationPreservesDefaultNamedAndTypedClientConfiguration(bool simulationFirst)
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url).ForClient(string.Empty).WithHeader("X-Client", "default").RespondText("default");
            http.Expect(HttpMethod.Get, Url).ForClient("named").WithHeader("X-Client", "named").RespondText("named");
            http.Expect(HttpMethod.Get, Url).WithHeader("X-Client", "typed").RespondText("typed");
            var services = new ServiceCollection();
            if (simulationFirst)
            {
                services.AddHttpSimulation(http);
            }

            services.AddHttpClient(string.Empty, c => Configure(c, "default"));
            services.AddHttpClient("named", c => Configure(c, "named"));
            services.AddHttpClient<ProfileClient>(c => Configure(c, "typed"));
            if (!simulationFirst)
            {
                services.AddHttpSimulation(http);
            }

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            using var defaultClient = factory.CreateClient();
            using var namedClient = factory.CreateClient("named");
            Assert.Equal(TimeSpan.FromSeconds(37), namedClient.Timeout);
            Assert.Equal("default", await defaultClient.GetStringAsync("items"));
            Assert.Equal("named", await namedClient.GetStringAsync("items"));
            Assert.Equal("typed", await provider.GetRequiredService<ProfileClient>().ReadAsync());
            Assert.False(string.IsNullOrEmpty(http.Exchanges[2].ClientName));
            http.Verify();
        }

        [Fact]
        public async Task RetryAndAuthenticationHandlersIncludingPrimaryChainRemainInOrder()
        {
            using var http = new HttpSimulation();
            http.ExpectSequence(HttpMethod.Get, Url).ForClient("retry")
                .WithHeader("Authorization", "Bearer synthetic-token")
                .WithHeader("X-Order", "outer,primary")
                .Respond(HttpStatusCode.ServiceUnavailable)
                .RespondText("recovered");
            var services = new ServiceCollection();
            services.AddHttpSimulation(http);
            services.AddHttpClient("retry")
                .AddHttpMessageHandler(() => new RetryHandler())
                .AddHttpMessageHandler(() => new OrderHandler("outer"))
                .ConfigurePrimaryHttpMessageHandler(() => new AuthenticationHandler
                {
                    InnerHandler = new OrderHandler("primary") { InnerHandler = new HttpClientHandler() }
                });
            using var provider = services.BuildServiceProvider();
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("retry");

            Assert.Equal("recovered", await client.GetStringAsync(Url));
            Assert.Equal(2, http.Exchanges.Count);
            http.Verify();
        }

#if NET8_0_OR_GREATER
        [Fact]
        public async Task FreshSocketsTransportIsReplacedInsideAPrimaryChain()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url).WithHeader("Authorization", "Bearer synthetic-token").RespondText("sockets");
            var services = new ServiceCollection();
            services.AddHttpSimulation(http);
            services.AddHttpClient("sockets").ConfigurePrimaryHttpMessageHandler(() => new AuthenticationHandler
            {
                InnerHandler = new SocketsHttpHandler()
            });
            using var provider = services.BuildServiceProvider();
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("sockets");

            Assert.Equal("sockets", await client.GetStringAsync(Url));
            http.Verify();
        }
#endif

        [Fact]
        public async Task SimulationSubstitutionRunsAfterOtherFiltersPostConfiguration()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url).WithHeader("Authorization", "Bearer synthetic-token").RespondText("intercepted");
            var services = new ServiceCollection();
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, LatePrimaryFilter>();
            services.AddHttpSimulation(http);
            services.AddHttpClient("late");
            using var provider = services.BuildServiceProvider();
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("late");

            Assert.Equal("intercepted", await client.GetStringAsync(Url));
            http.Verify();
        }

        [Fact]
        public async Task DifferentPooledPipelinesShareStateAndDoNotOwnTheSimulation()
        {
            using var http = new HttpSimulation();
            http.ExpectSequence(HttpMethod.Get, Url).RespondText("a").RespondText("b").RespondText("after host");
            var services = new ServiceCollection();
            services.AddHttpSimulation(http);
            using (var provider = services.BuildServiceProvider())
            {
                var factory = provider.GetRequiredService<IHttpClientFactory>();
                using (var first = factory.CreateClient("first"))
                {
                    Assert.Equal("a", await first.GetStringAsync(Url));
                }

                using var second = factory.CreateClient("second");
                Assert.Equal("b", await second.GetStringAsync(Url));
            }

            using var explicitClient = http.CreateClient();
            Assert.Equal("after host", await explicitClient.GetStringAsync(Url));
            http.Verify();
        }

        [Fact]
        public async Task HostDisposalDoesNotCancelCallerOwnedSimulationWaits()
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Get, Url).WaitForCancellation();
            var services = new ServiceCollection();
            services.AddHttpSimulation(http);
            var provider = services.BuildServiceProvider();
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient();
            using var cancellation = new CancellationTokenSource();
            var pending = client.GetAsync(Url, cancellation.Token);
            await expected.Received.WaitAsync(Guard, TimeProvider.System);

            provider.Dispose();
            Assert.False(pending.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            http.Verify();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DisplacedTerminalIsDisposedExactlyOnceWithItsWrapper(bool primaryChain)
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url).Respond(HttpStatusCode.OK);
            var services = new ServiceCollection();
            services.AddHttpSimulation(http);
            using var provider = services.BuildServiceProvider();
            var terminal = new TrackingHttpClientHandler();
            var builder = new TestHandlerBuilder
            {
                PrimaryHandler = primaryChain
                    ? new AuthenticationHandler { InnerHandler = terminal }
                    : terminal,
                Name = "lifetime"
            };
            provider.GetServices<IHttpMessageHandlerBuilderFilter>().First().Configure(_ => { })(builder);
            var pipeline = builder.Build();
            using (var invoker = new HttpMessageInvoker(pipeline, false))
            using (var request = new HttpRequestMessage(HttpMethod.Get, Url))
            using (var response = await invoker.SendAsync(request, CancellationToken.None))
            {
                Assert.Equal(0, terminal.Disposals);
            }

            pipeline.Dispose();
            pipeline.Dispose();
            Assert.Equal(1, terminal.Disposals);
            using var independent = http.CreateClient();
            http.Verify();
        }

        [Fact]
        public void OpaquePrimaryTransportsAreRejectedInsteadOfDroppingTheirBehavior()
        {
            using var http = new HttpSimulation();
            var services = new ServiceCollection();
            services.AddHttpClient("opaque").ConfigurePrimaryHttpMessageHandler(() => new OpaqueHandler());
            services.AddHttpSimulation(http);
            using var provider = services.BuildServiceProvider();

            var failure = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("opaque"));
            Assert.Contains("explicit", failure.Message.ToLowerInvariant());
        }

        [Fact]
        public void CustomHttpClientHandlerBehaviorIsNotSilentlyDropped()
        {
            using var http = new HttpSimulation();
            var services = new ServiceCollection();
            services.AddHttpSimulation(http);
            services.AddHttpClient("custom").ConfigurePrimaryHttpMessageHandler(() => new CustomTransport());
            using var provider = services.BuildServiceProvider();

            Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("custom"));
        }

        [Fact]
        public void CyclicPrimaryChainsAreRejectedWithoutTraversingForever()
        {
            using var http = new HttpSimulation();
            var first = new AuthenticationHandler();
            var second = new AuthenticationHandler { InnerHandler = first };
            first.InnerHandler = second;
            var services = new ServiceCollection();
            services.AddHttpClient("cycle").ConfigurePrimaryHttpMessageHandler(() => first);
            services.AddHttpSimulation(http);
            using var provider = services.BuildServiceProvider();
            try
            {
                var failure = Assert.Throws<InvalidOperationException>(() =>
                    provider.GetRequiredService<IHttpClientFactory>().CreateClient("cycle"));
                Assert.Contains("cycl", failure.Message.ToLowerInvariant());
            }
            finally
            {
                second.InnerHandler = new HttpClientHandler();
                first.Dispose();
            }
        }

        [Fact]
        public async Task ActivePrimaryChainsAreRejectedWithAnActionableSetupError()
        {
            using var http = new HttpSimulation();
            using var primary = new AuthenticationHandler { InnerHandler = new HttpClientHandler() };
            using var invoker = new HttpMessageInvoker(primary, false);
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                invoker.SendAsync(request, new CancellationToken(true)));
            var services = new ServiceCollection();
            services.AddHttpClient("active").ConfigurePrimaryHttpMessageHandler(() => primary);
            services.AddHttpSimulation(http);
            using var provider = services.BuildServiceProvider();

            var failure = Assert.Throws<InvalidOperationException>(() =>
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("active"));
            Assert.Contains("fresh", failure.Message.ToLowerInvariant());
        }

        [Fact]
        public void ReusedDisplacedTransportsAreRejectedAcrossClientPipelines()
        {
            using var http = new HttpSimulation();
            using var sharedTerminal = new HttpClientHandler();
            var services = new ServiceCollection();
            services.AddHttpClient("one").ConfigurePrimaryHttpMessageHandler(() =>
                new AuthenticationHandler { InnerHandler = sharedTerminal });
            services.AddHttpClient("two").ConfigurePrimaryHttpMessageHandler(() =>
                new AuthenticationHandler { InnerHandler = sharedTerminal });
            services.AddHttpSimulation(http);
            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            using var first = factory.CreateClient("one");

            Assert.Throws<InvalidOperationException>(() => factory.CreateClient("two"));
        }

        [Fact]
        public async Task AgentTestHostUsesTheRealHttpClientThroughAnEntireConversation()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url).RespondJson(new { displayName = "Test User" });
            await using (var host = AgentTestHost.Create(builder =>
            {
                builder.Services.AddHttpSimulation(http);
                builder.Services.AddHttpClient<ProfileAgent>(client =>
                    client.BaseAddress = new Uri("https://service.example.test/"));
                builder.Services.AddTransient<IAgent>(services => services.GetRequiredService<ProfileAgent>());
            }))
            {
                await host.CreateTestFlow().Send("profile").AssertReply("Test User").StartTestAsync();
            }

            http.Verify();
        }

        private static void Configure(HttpClient client, string label)
        {
            client.BaseAddress = new Uri("https://service.example.test/");
            client.Timeout = TimeSpan.FromSeconds(37);
            client.DefaultRequestHeaders.Add("X-Client", label);
        }

        public sealed class ProfileClient(HttpClient client) : IDisposable
        {
            public Task<string> ReadAsync() => client.GetStringAsync("items");
            public void Dispose() => client.Dispose();
        }

        public sealed class ProfileAgent(HttpClient client) : IAgent, IDisposable
        {
            public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            {
                using var response = await client.GetAsync("items", cancellationToken);
                response.EnsureSuccessStatusCode();
                using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                await turnContext.SendActivityAsync(json.RootElement.GetProperty("displayName").GetString(),
                    cancellationToken: cancellationToken);
            }

            public void Dispose() => client.Dispose();
        }

        private sealed class RetryHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    response.Dispose();
                    request.Headers.Remove("X-Order");
                    response = await base.SendAsync(request, cancellationToken);
                }

                return response;
            }
        }

        private sealed class AuthenticationHandler : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-token");
                return base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class OrderHandler(string label) : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var previous = request.Headers.TryGetValues("X-Order", out var values)
                    ? string.Join(",", values) + ","
                    : string.Empty;
                request.Headers.Remove("X-Order");
                request.Headers.Add("X-Order", previous + label);
                return base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class OpaqueHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new InvalidOperationException("An opaque handler must never be reached.");
        }

        private sealed class LatePrimaryFilter : IHttpMessageHandlerBuilderFilter
        {
            public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
            {
                next(builder);
                builder.PrimaryHandler.Dispose();
                builder.PrimaryHandler = new AuthenticationHandler { InnerHandler = new HttpClientHandler() };
            };
        }

        private sealed class TrackingHttpClientHandler : HttpClientHandler
        {
            public int Disposals { get; private set; }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Disposals++;
                }

                base.Dispose(disposing);
            }
        }

        private sealed class CustomTransport : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }

        private sealed class TestHandlerBuilder : HttpMessageHandlerBuilder
        {
            public override string Name { get; set; }
            public override HttpMessageHandler PrimaryHandler { get; set; }
            public override IList<DelegatingHandler> AdditionalHandlers { get; } = new List<DelegatingHandler>();
            public override HttpMessageHandler Build() => CreateHandlerPipeline(PrimaryHandler, AdditionalHandlers);
        }
    }
}
