// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.CopilotStudio.Client.Discovery;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.CopilotStudio.Client.Tests
{
    public class ConversationEndpointDiscoveryTests
    {
        private const string Host = "default5cae182f63ff49f99f353acc2724e73.8.environment.api.test.powerplatform.com";
        private static readonly Uri DiscoveryUri = new("https://" + Host + "/copilotstudio/agents/test/conversation-endpoints");

        private static ConnectionSettings Settings() => new()
        {
            EnvironmentId = "Default-5cae182f-63ff-49f9-9f35-3acc2724e738",
            SchemaName = "test",
            Cloud = PowerPlatformCloud.Test
        };

        private static string Root(bool agentic = false) =>
            "https://" + Host + "/copilotstudio" + (agentic ? "/agenticruntime/3p" : "") +
            "/dataverse-backed/authenticated/bots/test/conversations";

        private static HttpResponseMessage DiscoveryResponse(bool agentic = false, bool subscribe = true, string cacheControl = "private, max-age=3600")
        {
            var root = Root(agentic);
            var urls = new Dictionary<string, string>
            {
                ["createConversation"] = root + "?api-version=create-version&extra=1",
                ["executeTurn"] = root + "/{conversationId}?api-version=execute-version&extra=2",
                ["continueTurn"] = root + "/{conversationId}/continue?api-version=continue-version"
            };
            if (subscribe)
            {
                urls["subscribe"] = root + "/{conversationId}/subscribe?api-version=subscribe-version";
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(urls))
            };
            if (cacheControl != null)
            {
                response.Headers.CacheControl = CacheControlHeaderValue.Parse(cacheControl);
            }
            return response;
        }

        private static CopilotClient Client(ConnectionSettings settings, HttpMessageHandler handler, Func<string, Task<string>> tokenProvider = null)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("mcs")).Returns(new HttpClient(handler));
            return tokenProvider == null
                ? new CopilotClient(settings, factory.Object, NullLogger.Instance)
                : new CopilotClient(settings, factory.Object, tokenProvider, NullLogger.Instance, "mcs");
        }

        private static async Task DrainAsync(IAsyncEnumerable<IActivity> activities)
        {
            await foreach (var activity in activities)
            {
                Assert.NotNull(activity);
            }
        }

        [Theory]
        [InlineData(PowerPlatformCloud.Test, "Default-5cae182f-63ff-49f9-9f35-3acc2724e738", Host)]
        [InlineData(PowerPlatformCloud.Prod, "11111111-1111-1111-1111-111111111111", "111111111111111111111111111111.11.environment.api.powerplatform.com")]
        public void DiscoveryUrlUsesEnvironmentCloudAndEscapedSchema(PowerPlatformCloud cloud, string environment, string host)
        {
            var settings = Settings();
            settings.Cloud = cloud;
            settings.EnvironmentId = environment;
            settings.SchemaName = "bot/name ?#";
            Assert.Equal("https://" + host + "/copilotstudio/agents/bot%2Fname%20%3F%23/conversation-endpoints",
                PowerPlatformEnvironment.GetConversationEndpointsDiscoveryUrl(settings).AbsoluteUri);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ClientPersistsDiscoveredCreateUrlAndThenUsesDirectConnection(bool agentic)
        {
            var requests = new List<(HttpMethod Method, string Url, string Token)>();
            var handler = new Handler((request, ct) =>
            {
                requests.Add((request.Method, request.RequestUri.AbsoluteUri, request.Headers.Authorization?.Parameter));
                return Task.FromResult(request.Method == HttpMethod.Get
                    ? DiscoveryResponse(agentic, !agentic)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            });
            var client = Client(Settings(), handler, _ => Task.FromResult("caller-token"));
            await DrainAsync(client.StartConversationAsync());
            Assert.Equal(Root(agentic) + "?api-version=create-version&extra=1", client.Settings.DirectConnectUrl);
            await DrainAsync(client.ExecuteAsync("conversation/a ?#%", new Activity { Type = "message", Text = "hello" }, CancellationToken.None));
            Assert.Equal(3, requests.Count);
            Assert.Equal((HttpMethod.Get, DiscoveryUri.AbsoluteUri, "caller-token"), requests[0]);
            Assert.Equal((HttpMethod.Post, Root(agentic) + "?api-version=create-version&extra=1", "caller-token"), requests[1]);
            Assert.Equal((HttpMethod.Post, PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(client.Settings, "conversation/a ?#%").AbsoluteUri, "caller-token"), requests[2]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExecuteFirstPersistsCreateUrlAndAnotherClientCanReuseIt(bool agentic)
        {
            var settings = Settings();
            var requests = new List<string>();
            var client = Client(settings, new Handler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    return Task.FromResult(DiscoveryResponse(agentic, !agentic));
                }
                requests.Add(request.RequestUri.AbsoluteUri);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }), _ => Task.FromResult("caller-token"));
            await DrainAsync(client.ExecuteAsync("first/id", new Activity(), CancellationToken.None));
            Assert.Equal(Root(agentic) + "/first%2Fid?api-version=execute-version&extra=2", requests[0]);
            Assert.Equal(Root(agentic) + "?api-version=create-version&extra=1", settings.DirectConnectUrl);

            var secondClient = Client(settings, new Handler((request, ct) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                requests.Add(request.RequestUri.AbsoluteUri);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }));
            await DrainAsync(secondClient.StartConversationAsync());
            Assert.Equal(Root(agentic) + "?api-version=2022-03-01-preview", requests[1]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExplicitUrlAndPrebuiltAgentsBypassDiscovery(bool prebuilt)
        {
            var settings = Settings();
            if (prebuilt)
            {
                settings.CopilotAgentType = AgentType.Prebuilt;
            }
            else
            {
                settings = new ConnectionSettings { DirectConnectUrl = "https://explicit.example/bot/conversations" };
            }
            var expected = PowerPlatformEnvironment.GetCopilotStudioConnectionUrl(settings, null);
            var calls = 0;
            var client = Client(settings, new Handler((request, ct) =>
            {
                calls++;
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(expected, request.RequestUri);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }));
            await DrainAsync(client.StartConversationAsync());
            Assert.Equal(1, calls);
        }

        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(500)]
        public async Task DiscoveryFailuresNeverInvokeOrPersistGuessedRoutes(int status)
        {
            var calls = 0;
            var client = Client(Settings(), new Handler((request, ct) =>
            {
                calls++;
                Assert.Equal(HttpMethod.Get, request.Method);
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
                {
                    Content = new StringContent("{\"error\":{\"code\":\"AgentNotFoundOrAccessDenied\"}}")
                });
            }), _ => Task.FromResult("caller-token"));
            for (var i = 0; i < 2; i++)
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => DrainAsync(client.StartConversationAsync()));
                Assert.Null(client.Settings.DirectConnectUrl);
            }
            Assert.Equal(2, calls);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task EmptyExternalTokenFailsBeforeSending(string token)
        {
            var client = Client(Settings(), new Handler((request, ct) => throw new InvalidOperationException("Must not send")),
                _ => Task.FromResult(token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => DrainAsync(client.StartConversationAsync()));
        }

        [Fact]
        public async Task HandlerAuthenticationReusesPersistedDirectUrl()
        {
            var discoveryCalls = 0;
            var handler = new AuthenticationHandler(new Handler((request, ct) =>
            {
                Assert.Equal("handler-token", request.Headers.Authorization?.Parameter);
                if (request.Method == HttpMethod.Get)
                {
                    discoveryCalls++;
                    return Task.FromResult(DiscoveryResponse());
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }));
            var client = Client(Settings(), handler);
            await DrainAsync(client.StartConversationAsync());
            await DrainAsync(client.ExecuteAsync("conversation", new Activity(), CancellationToken.None));
            Assert.Equal(1, discoveryCalls);
        }

        [Fact]
        public async Task TokenChangeDoesNotRediscoverAfterDirectUrlIsPersisted()
        {
            var token = "first";
            var discoveryCalls = 0;
            var client = Client(Settings(), new Handler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    discoveryCalls++;
                    return Task.FromResult(DiscoveryResponse());
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }), _ => Task.FromResult(token));
            await DrainAsync(client.StartConversationAsync());
            token = "second";
            await DrainAsync(client.StartConversationAsync());
            Assert.Equal(1, discoveryCalls);
        }

        [Theory]
        [InlineData("private, max-age=3600")]
        [InlineData("no-store")]
        [InlineData("no-cache")]
        [InlineData(null)]
        public async Task ClearingDirectUrlTriggersFreshDiscoveryRegardlessOfCacheHeaders(string cacheControl)
        {
            var settings = Settings();
            var discoveryCalls = 0;
            var client = Client(settings, new Handler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    discoveryCalls++;
                    return Task.FromResult(DiscoveryResponse(agentic: discoveryCalls == 2, cacheControl: cacheControl));
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }), _ => Task.FromResult("caller-token"));
            await DrainAsync(client.StartConversationAsync());
            await DrainAsync(client.StartConversationAsync());
            Assert.Equal(1, discoveryCalls);
            settings.DirectConnectUrl = null;
            await DrainAsync(client.StartConversationAsync());
            Assert.Equal(2, discoveryCalls);
            Assert.Equal(Root(true) + "?api-version=create-version&extra=1", settings.DirectConnectUrl);
        }

        [Fact]
        public async Task ConcurrentRequestsCoalesceAndCancelledWaiterDoesNotSend()
        {
            var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var discoveryCalls = 0;
            var postCalls = 0;
            var client = Client(Settings(), new Handler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    Interlocked.Increment(ref discoveryCalls);
                    return completion.Task;
                }
                Interlocked.Increment(ref postCalls);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }));
            var first = DrainAsync(client.StartConversationAsync());
            var second = DrainAsync(client.ExecuteAsync("id", new Activity(), CancellationToken.None));
            using var cts = new CancellationTokenSource();
            var cancelled = DrainAsync(client.ExecuteAsync("cancelled", new Activity(), cts.Token));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            completion.SetResult(DiscoveryResponse());
            await Task.WhenAll(first, second);
            Assert.Equal(1, discoveryCalls);
            Assert.Equal(2, postCalls);
            Assert.Equal(Root() + "?api-version=create-version&extra=1", client.Settings.DirectConnectUrl);
        }

        [Fact]
        public async Task FailedDiscoveryReleasesGateAndNextCallCanRetry()
        {
            var discoveryCalls = 0;
            var client = Client(Settings(), new Handler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    discoveryCalls++;
                    return Task.FromResult(discoveryCalls == 1
                        ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("Unavailable") }
                        : DiscoveryResponse());
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }));
            await Assert.ThrowsAsync<HttpRequestException>(() => DrainAsync(client.StartConversationAsync()));
            Assert.Null(client.Settings.DirectConnectUrl);
            await DrainAsync(client.StartConversationAsync());
            Assert.Equal(2, discoveryCalls);
            Assert.Equal(Root() + "?api-version=create-version&extra=1", client.Settings.DirectConnectUrl);
        }

        [Fact]
        public async Task SubscribeIsOptionalAndReturnedVersionsArePreserved()
        {
            var uri = await ConversationEndpointDiscovery.ResolveAsync(DiscoveryUri, "subscribe", "id/one",
                (request, ct) => Task.FromResult(DiscoveryResponse()), CancellationToken.None);
            Assert.Equal(Root() + "/id%2Fone/subscribe?api-version=subscribe-version", uri.OperationUri.AbsoluteUri);
            Assert.Equal(Root() + "?api-version=create-version&extra=1", uri.DirectConnectUri.AbsoluteUri);
            await Assert.ThrowsAsync<NotSupportedException>(() => ConversationEndpointDiscovery.ResolveAsync(DiscoveryUri, "subscribe", "id",
                (request, ct) => Task.FromResult(DiscoveryResponse(true, false)), CancellationToken.None));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ClientSubscribeUsesDiscoveryOrRejectsUnsupportedOperation(bool agentic)
        {
            var postCalls = 0;
            var client = Client(Settings(), new Handler((request, ct) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    return Task.FromResult(DiscoveryResponse(agentic, !agentic));
                }
                postCalls++;
                Assert.Equal(Root() + "/id%2Fone/subscribe?api-version=subscribe-version", request.RequestUri.AbsoluteUri);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") });
            }), _ => Task.FromResult("caller-token"));

            async Task Subscribe()
            {
#pragma warning disable CS0618 // Exercise the existing internal-only subscription surface.
                await foreach (var item in client.SubscribeAsync("id/one", "last-event", CancellationToken.None))
#pragma warning restore CS0618
                {
                    Assert.NotNull(item);
                }
            }

            if (agentic)
            {
                await Assert.ThrowsAsync<NotSupportedException>(Subscribe);
                Assert.Null(client.Settings.DirectConnectUrl);
                Assert.Equal(0, postCalls);
            }
            else
            {
                await Subscribe();
                Assert.Equal(1, postCalls);
            }
        }

        [Fact]
        public async Task InvocationHeadersStillTrackConversationButDiscoveryHeadersDoNotOverrideSettings()
        {
            var settings = Settings();
            settings.UseExperimentalEndpoint = true;
            var calls = 0;
            var client = Client(settings, new Handler((request, ct) =>
            {
                calls++;
                if (request.Method == HttpMethod.Get)
                {
                    var discovery = DiscoveryResponse();
                    discovery.Headers.Add(CopilotStudioHeaderNames.D2EExperimentalUrl, "https://not-an-invocation.example");
                    discovery.Headers.Add(CopilotStudioHeaderNames.D2EConversationId, "wrong-conversation");
                    return Task.FromResult(discovery);
                }
                if (calls == 3)
                {
                    Assert.Equal(Root() + "/returned-conversation?api-version=2022-03-01-preview", request.RequestUri.AbsoluteUri);
                }
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"activities\":[]}") };
                response.Headers.Add(CopilotStudioHeaderNames.D2EConversationId, "returned-conversation");
                return Task.FromResult(response);
            }), _ => Task.FromResult("caller-token"));
            await DrainAsync(client.StartConversationAsync());
            await DrainAsync(client.AskQuestionAsync("hello"));
            Assert.Equal(Root() + "?api-version=create-version&extra=1", settings.DirectConnectUrl);
            Assert.Equal(3, calls);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("null")]
        [InlineData("not json")]
        public async Task MalformedResponsesAreRejected(string json)
        {
            var calls = 0;
            Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            }
            for (var i = 0; i < 2; i++)
            {
                await Assert.ThrowsAsync<JsonException>(() => ConversationEndpointDiscovery.ResolveAsync(DiscoveryUri, "createConversation", null, Send, CancellationToken.None));
            }
            Assert.Equal(2, calls);
        }

        [Theory]
        [InlineData("http://host/conversations")]
        [InlineData("https://untrusted.example/conversations")]
        [InlineData("/relative/conversations")]
        [InlineData("https://" + Host + "/conversations/{unknown}")]
        [InlineData("https://" + Host + "/conversations#fragment")]
        public async Task InvalidDiscoveredUrlsAreRejected(string createUrl)
        {
            using var template = DiscoveryResponse();
            var json = await template.Content.ReadAsStringAsync();
            var urls = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            urls["createConversation"] = createUrl;
            await Assert.ThrowsAsync<JsonException>(() => ConversationEndpointDiscovery.ResolveAsync(DiscoveryUri, "createConversation", null,
                (request, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(urls)) }),
                CancellationToken.None));
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

            internal Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            {
                _send = send;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                _send(request, cancellationToken);
        }

        private sealed class AuthenticationHandler : DelegatingHandler
        {
            internal AuthenticationHandler(HttpMessageHandler inner) : base(inner) { }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "handler-token");
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
