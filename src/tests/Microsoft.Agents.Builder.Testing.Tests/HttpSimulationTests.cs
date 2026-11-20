// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.Testing.Http;
using Xunit;

namespace Microsoft.Agents.Builder.Testing.Tests
{
    public class HttpSimulationTests
    {
        private const string Url = "https://service.example.test/items";
        private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task IndependentExpectationsAreUnorderedAndReturnUninterpretedResponses()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url).RespondJson(
                new { ErrorCode = "Forbidden" }, HttpStatusCode.Forbidden,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            http.Expect(HttpMethod.Get, Url + "/other").RespondText("not json", "application/json");
            using var client = http.CreateClient();

            using var second = await client.GetAsync(Url + "/other");
            Assert.Equal("not json", await second.Content.ReadAsStringAsync());
            Assert.Equal("application/json", second.Content.Headers.ContentType.MediaType);
            using var first = await client.GetAsync(Url);
            Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
            Assert.Equal("{\"errorCode\":\"Forbidden\"}", await first.Content.ReadAsStringAsync());
            Assert.Equal("application/json", first.Content.Headers.ContentType.MediaType);
            http.Verify();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ResponsesAreAssociatedWithTheDispatchedRequest(bool callback)
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Get, Url);
            if (callback)
            {
                expected.RespondAsync((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            }
            else
            {
                expected.RespondText("response");
            }
            using var client = http.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);

            using var response = await client.SendAsync(request);

            Assert.Same(request, response.RequestMessage);
            http.Verify();
        }

        [Fact]
        public async Task QueryMatchingDecodesPairsIgnoresOrderAndPreservesDuplicates()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url + "?a=1&a=2&space=hello+world&plus=%2B")
                .WithQuery("path", "a/b")
                .Respond(HttpStatusCode.NoContent);
            using var client = http.CreateClient();
            using var response = await client.GetAsync(
                "https://SERVICE.example.test:443/items?path=a%2Fb&plus=%2b&a=2&space=hello%20world&a=1");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            http.Verify();
        }

        [Theory]
        [InlineData("https://other.example.test/items?a=1&a=2")]
        [InlineData("http://service.example.test/items?a=1&a=2")]
        [InlineData("https://service.example.test:444/items?a=1&a=2")]
        [InlineData("https://service.example.test/Items?a=1&a=2")]
        [InlineData("https://service.example.test/items?a=1&a=1")]
        [InlineData("https://service.example.test/items?a=1&a=2&extra=3")]
        public async Task UriMismatchIsStickyEvenAfterTheExpectedRequestSucceeds(string actual)
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url + "?a=1&a=2").Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(actual));
            using var response = await client.GetAsync(Url + "?a=2&a=1");

            Assert.Throws<HttpSimulationException>(() => http.Verify());
            Assert.Equal(typeof(HttpSimulationException).FullName, http.Exchanges[0].ExceptionType);
        }

        [Fact]
        public async Task MethodClientHeadersAndStructuralJsonRefineTheMatch()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Post, Url).ForClient("alpha")
                .WithHeader("X-Correlation", "one")
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new { name = "Ada", values = new[] { 1, 2 } })
                .RespondText("alpha");
            http.Expect(HttpMethod.Post, Url).ForClient("beta")
                .WithJsonBody(json => json.GetProperty("name").GetString() == "Bob")
                .RespondText("beta");
            using var alpha = http.CreateClient("alpha");
            using var beta = http.CreateClient("beta");
            using var request = new HttpRequestMessage(HttpMethod.Post, Url)
            {
                Content = new StringContent("{\"values\":[1,2],\"name\":\"Ada\"}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-correlation", "one");

            using var betaResponse = await beta.PostAsync(Url, new StringContent("{\"name\":\"Bob\"}"));
            using var alphaResponse = await alpha.SendAsync(request);
            Assert.Equal("alpha", await alphaResponse.Content.ReadAsStringAsync());
            Assert.Equal("beta", await betaResponse.Content.ReadAsStringAsync());
            http.Verify();
        }

        [Theory]
        [InlineData("method")]
        [InlineData("client")]
        [InlineData("header")]
        [InlineData("array")]
        [InlineData("extra-property")]
        [InlineData("malformed-json")]
        public async Task ConstraintsRejectRequestsThatOnlyMatchTheUri(string mismatch)
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Post, Url).ForClient("named")
                .WithHeader("X-Correlation", "one").WithJsonBody(new { values = new[] { 1, 2 } })
                .Respond(HttpStatusCode.OK);
            using var client = http.CreateClient(mismatch == "client" ? "NAMED" : "named");
            var json = mismatch switch
            {
                "array" => "{\"values\":[2,1]}",
                "extra-property" => "{\"values\":[1,2],\"extra\":true}",
                "malformed-json" => "{invalid",
                _ => "{\"values\":[1,2]}"
            };
            using var request = new HttpRequestMessage(mismatch == "method" ? HttpMethod.Put : HttpMethod.Post, Url)
            {
                Content = new StringContent(json)
            };
            request.Headers.Add("X-Correlation", mismatch == "header" ? "One" : "one");

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.SendAsync(request));
            Assert.False(expected.Received.IsCompleted);
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Theory]
        [InlineData("1e100", "10e99", true)]
        [InlineData("0.123456789012345678901234567891", "0.123456789012345678901234567892", false)]
        public async Task StructuralNumbersAreComparedWithoutDecimalRounding(string expectedJson, string actualJson, bool equal)
        {
            using var http = new HttpSimulation();
            using var expected = JsonDocument.Parse(expectedJson);
            http.Expect(HttpMethod.Post, Url).WithJsonBody(expected.RootElement).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var content = new StringContent(actualJson);
            if (equal)
            {
                using var response = await client.PostAsync(Url, content);
                http.Verify();
            }
            else
            {
                await Assert.ThrowsAsync<HttpSimulationException>(() => client.PostAsync(Url, content));
                Assert.Throws<HttpSimulationException>(() => http.Verify());
            }
        }

        [Fact]
        public async Task AmbiguityDoesNotReserveAnExpectation()
        {
            using var http = new HttpSimulation();
            var first = http.Expect(HttpMethod.Get, Url).Respond(HttpStatusCode.OK);
            var second = http.Expect(HttpMethod.Get, Url).Respond(HttpStatusCode.Accepted);
            using var client = http.CreateClient();

            var failure = await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            Assert.Contains("ambiguous", failure.Message.ToLowerInvariant());
            Assert.False(first.Received.IsCompleted);
            Assert.False(second.Received.IsCompleted);
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task SequenceConsumesEachOutcomeOnceAndNeverRepeatsTheLastResponse()
        {
            using var http = new HttpSimulation();
            http.ExpectSequence(HttpMethod.Get, Url)
                .Respond((HttpStatusCode)429, r =>
                    r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero))
                .RespondJson(new { ok = true });
            using var client = http.CreateClient();

            using var first = await client.GetAsync(Url);
            using var second = await client.GetAsync(Url);
            Assert.Equal((HttpStatusCode)429, first.StatusCode);
            Assert.Equal(TimeSpan.Zero, first.Headers.RetryAfter.Delta);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            http.Verify();

            var failure = await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            Assert.Contains("exhausted", failure.Message.ToLowerInvariant());
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task ConcurrentCallsReserveDifferentStepsBeforeRunningCallbacks()
        {
            using var http = new HttpSimulation();
            var bothStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = 0;
            async Task<HttpResponseMessage> Respond(HttpStatusCode status)
            {
                if (Interlocked.Increment(ref started) == 2)
                {
                    bothStarted.TrySetResult(true);
                }

                await release.Task;
                return new HttpResponseMessage(status);
            }

            http.ExpectSequence(HttpMethod.Get, Url)
                .RespondAsync((_, _) => Respond(HttpStatusCode.Accepted))
                .RespondAsync((_, _) => Respond(HttpStatusCode.Created));
            using var firstClient = http.CreateClient();
            using var secondClient = http.CreateClient();
            var first = firstClient.GetAsync(Url);
            var second = secondClient.GetAsync(Url);
            try
            {
                await bothStarted.Task.WaitAsync(Guard, TimeProvider.System);
                var beforeCompletion = http.Exchanges;
                Assert.All(beforeCompletion, e => Assert.Null(e.StatusCode));
                Assert.Equal(new[] { 1, 2 }, beforeCompletion.Select(e => e.SequenceStep));
                Assert.Throws<HttpSimulationException>(() => http.Verify());
                release.TrySetResult(true);
                using var firstResponse = await first;
                using var secondResponse = await second;
                Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
                Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
                Assert.All(beforeCompletion, e => Assert.Null(e.StatusCode));
                http.Verify();
            }
            finally
            {
                release.TrySetResult(true);
            }
        }

        [Fact]
        public async Task JsonPredicateRunsOutsideTheSimulationLock()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Post, Url)
                .WithJsonBody(_ => Task.Run(() => http.Exchanges.Count)
                    .WaitAsync(Guard, TimeProvider.System).GetAwaiter().GetResult() == 1)
                .Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var response = await client.PostAsync(Url, new StringContent("{}"));
            http.Verify();
        }

        [Fact]
        public async Task CancellationBeforeReservationDoesNotConsumeAnAttempt()
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Get, Url).Respond(HttpStatusCode.OK);
            using var handler = http.CreateHandler();
            using var invoker = new HttpMessageInvoker(handler, false);
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                invoker.SendAsync(request, new CancellationToken(true)));
            Assert.False(expected.Received.IsCompleted);
            using var client = http.CreateClient();
            using var response = await client.GetAsync(Url);
            http.Verify();
        }

        [Fact]
        public async Task CancellationAfterReservationConsumesTheAttemptAndPendingFailsVerification()
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Get, Url).WaitForCancellation();
            using var client = http.CreateClient();
            using var cancellation = new CancellationTokenSource();

            var pending = client.GetAsync(Url, cancellation.Token);
            await expected.Received.WaitAsync(Guard, TimeProvider.System);
            Assert.Contains("pending", Assert.Throws<HttpSimulationException>(() => http.Verify()).Message.ToLowerInvariant());
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            http.Verify();
        }

        [Fact]
        public async Task DisposalCancelsBuiltInWaitsWithoutAsserting()
        {
            var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Get, Url).WaitForCancellation();
            http.Expect(HttpMethod.Get, Url + "/missing").Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            var pending = client.GetAsync(Url);
            await expected.Received.WaitAsync(Guard, TimeProvider.System);

            http.Dispose();
            http.Dispose();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                pending.WaitAsync(Guard, TimeProvider.System));
        }

        [Fact]
        public async Task DisposingAClientOrHandlerDoesNotDisposeSharedState()
        {
            using var http = new HttpSimulation();
            http.ExpectSequence(HttpMethod.Get, Url).RespondText("first").RespondText("second");
            using var remaining = http.CreateClient();
            using (var first = http.CreateClient())
            {
                Assert.Equal("first", await first.GetStringAsync(Url));
            }

            http.CreateHandler().Dispose();
            Assert.Equal("second", await remaining.GetStringAsync(Url));
            http.Verify();
        }

        [Fact]
        public async Task ExplicitFaultsIncludingSyntheticTimeoutAreExpectedAttempts()
        {
            using var http = new HttpSimulation();
            http.ExpectSequence(HttpMethod.Get, Url)
                .Throw(() => new HttpRequestException("synthetic connection failure"))
                .Throw(() => new TaskCanceledException("synthetic timeout"))
                .Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(Url));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(Url));
            using var response = await client.GetAsync(Url);
            Assert.Equal(typeof(HttpRequestException).FullName, http.Exchanges[0].ExceptionType);
            http.Verify();
        }

        [Fact]
        public async Task UnplannedCallbackExceptionsAreStickySanitizedFixtureFailures()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Get, Url)
                .RespondAsync((_, _) => throw new InvalidOperationException("private-fixture-value"));
            using var client = http.CreateClient();

            var failure = await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            var verification = Assert.Throws<HttpSimulationException>(() => http.Verify());
            Assert.DoesNotContain("private-fixture-value", failure.ToString());
            Assert.DoesNotContain("private-fixture-value", verification.ToString());
        }

        [Fact]
        public async Task RequestedCallbackCancellationIsNotAFixtureFailure()
        {
            using var http = new HttpSimulation();
            using var cancellation = new CancellationTokenSource();
            http.Expect(HttpMethod.Get, Url).RespondAsync((_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
            using var client = http.CreateClient();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(Url, cancellation.Token));
            http.Verify();
        }

        [Fact]
        public async Task ResponsesAndContentAreFreshAcrossAttempts()
        {
            using var http = new HttpSimulation();
            http.ExpectSequence(HttpMethod.Get, Url)
                .RespondJson(new { value = 1 })
                .RespondJson(new { value = 2 });
            using var client = http.CreateClient();
            var first = await client.GetAsync(Url);
            var firstContent = first.Content;
            first.Dispose();

            using var second = await client.GetAsync(Url);
            Assert.NotSame(first, second);
            Assert.NotSame(firstContent, second.Content);
            Assert.Equal("{\"value\":2}", await second.Content.ReadAsStringAsync());
            http.Verify();
        }

        [Fact]
        public async Task ReusedCallbackResponsesAreRejectedAsFixtureFailures()
        {
            using var http = new HttpSimulation();
            using var shared = new HttpResponseMessage(HttpStatusCode.OK);
            http.ExpectSequence(HttpMethod.Get, Url)
                .RespondAsync((_, _) => Task.FromResult(shared))
                .RespondAsync((_, _) => Task.FromResult(shared));
            using var client = http.CreateClient();
            await client.GetAsync(Url);

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task ReusingContentInDifferentResponsesIsAlsoAFixtureFailure()
        {
            using var http = new HttpSimulation();
            using var content = new StringContent("shared");
            http.ExpectSequence(HttpMethod.Get, Url)
                .Respond(HttpStatusCode.OK, response => response.Content = content)
                .Respond(HttpStatusCode.OK, response => response.Content = content);
            using var client = http.CreateClient();
            using var first = await client.GetAsync(Url);

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task ReusingAnExplicitExceptionIsAFixtureFailure()
        {
            using var http = new HttpSimulation();
            var fault = new HttpRequestException("synthetic");
            http.ExpectSequence(HttpMethod.Get, Url).Throw(() => fault).Throw(() => fault);
            using var client = http.CreateClient();
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(Url));

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Theory]
        [InlineData("null-response")]
        [InlineData("null-exception")]
        [InlineData("throwing-exception-factory")]
        [InlineData("throwing-predicate")]
        [InlineData("unrequested-cancellation")]
        public async Task InvalidFixturesCannotBeHiddenByCatchingTheirExceptions(string fixture)
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Post, Url);
            switch (fixture)
            {
                case "null-response":
                    expected.RespondAsync((_, _) => Task.FromResult<HttpResponseMessage>(null));
                    break;
                case "null-exception":
                    expected.Throw(() => null);
                    break;
                case "throwing-exception-factory":
                    expected.Throw(() => throw new InvalidOperationException("private-value"));
                    break;
                case "throwing-predicate":
                    expected.WithJsonBody(_ => throw new InvalidOperationException("private-value")).Respond(HttpStatusCode.OK);
                    break;
                default:
                    expected.RespondAsync((_, _) => throw new OperationCanceledException("private-value"));
                    break;
            }

            using var client = http.CreateClient();
            var failure = await Assert.ThrowsAsync<HttpSimulationException>(() =>
                client.PostAsync(Url, new StringContent("{}")));
            var verification = Assert.Throws<HttpSimulationException>(() => http.Verify());
            Assert.DoesNotContain("private-value", failure.ToString());
            Assert.DoesNotContain("private-value", verification.ToString());
        }

        [Fact]
        public async Task FirstRequestFreezesAllExpectationConfiguration()
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Get, Url).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var response = await client.GetAsync(Url);

            Assert.Throws<InvalidOperationException>(() => http.Expect(HttpMethod.Get, Url + "/later"));
            Assert.Throws<InvalidOperationException>(() => expected.WithHeader("X-Later", "value"));
            Assert.Throws<InvalidOperationException>(() => expected.Respond(HttpStatusCode.OK));
            http.Verify();
        }

        [Fact]
        public async Task BodiesAreNotReadUnlessMatchingOrCaptureRequiresThem()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Post, Url).Respond(HttpStatusCode.OK);
            http.Expect(HttpMethod.Post, Url + "/other").WithJsonBody(_ => true).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var content = new ProbeContent(_ => throw new InvalidOperationException("body must remain streaming"));

            using var response = await client.PostAsync(Url, content);
            Assert.Equal(0, content.Serializations);
            Assert.Null(Assert.Single(http.Exchanges).Body);
            using var other = await client.PostAsync(Url + "/other", new StringContent("{}"));
            http.Verify();
        }

        [Fact]
        public async Task BoundedBodyMatchingPreservesNonSeekableContentForTheResponseCallback()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Post, Url).WithJsonBody(new { value = 1 })
                .RespondAsync(async (request, _) =>
                {
                    Assert.Equal("{\"value\":1}", await request.Content.ReadAsStringAsync());
                    Assert.Equal("application/json", request.Content.Headers.ContentType.MediaType);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
            using var client = http.CreateClient();
            var bytes = Encoding.UTF8.GetBytes("{\"value\":1}");
            using var content = new ProbeContent(s => s.WriteAsync(bytes, 0, bytes.Length));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using var response = await client.PostAsync(Url, content);
            Assert.Equal(1, content.Serializations);
            Assert.Null(Assert.Single(http.Exchanges).Body);
            http.Verify();
        }

        [Fact]
        public async Task OversizedUnbufferedBodiesFailAtTheConfiguredBound()
        {
            using var http = new HttpSimulation(new HttpSimulationOptions { MaxRequestBodyBytes = 32 });
            http.Expect(HttpMethod.Post, Url).WithJsonBody(_ => true).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            var produced = 0;
            using var content = new ProbeContent(async stream =>
            {
                var chunk = new byte[8];
                for (var i = 0; i < 1000; i++)
                {
                    produced += chunk.Length;
                    await stream.WriteAsync(chunk, 0, chunk.Length);
                }
            });

            var failure = await Assert.ThrowsAsync<HttpSimulationException>(() => client.PostAsync(Url, content));
            Assert.Contains("32", failure.Message);
            Assert.InRange(produced, 32, 40);
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task BodyCaptureEnforcesTheLimitForSingleByteWrites()
        {
            using var http = new HttpSimulation(new HttpSimulationOptions
            {
                CaptureRequestBody = true,
                MaxRequestBodyBytes = 2
            });
            http.Expect(HttpMethod.Post, Url).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var content = new ProbeContent(stream =>
            {
                stream.WriteByte(1);
                stream.WriteByte(2);
                stream.WriteByte(3);
                return Task.CompletedTask;
            });

            var failure = await Assert.ThrowsAsync<HttpSimulationException>(() => client.PostAsync(Url, content));
            Assert.Contains("limit", failure.Message.ToLowerInvariant());
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task StreamBodyReadingStopsAfterTheFirstByteBeyondTheLimit()
        {
            using var http = new HttpSimulation(new HttpSimulationOptions
            {
                CaptureRequestBody = true,
                MaxRequestBodyBytes = 32
            });
            http.Expect(HttpMethod.Post, Url).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var source = new NonSeekableReadStream(new byte[1000]);
            using var content = new StreamContent(source);

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.PostAsync(Url, content));
            Assert.Equal(33, source.BytesRead);
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task ContentLengthOverTheLimitIsRejectedWithoutReadingTheBody()
        {
            using var http = new HttpSimulation(new HttpSimulationOptions
            {
                CaptureRequestBody = true,
                MaxRequestBodyBytes = 32
            });
            http.Expect(HttpMethod.Post, Url).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var content = new ProbeContent(_ => throw new InvalidOperationException("must not read"));
            content.Headers.ContentLength = 1000;

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.PostAsync(Url, content));
            Assert.Equal(0, content.Serializations);
            Assert.Throws<HttpSimulationException>(() => http.Verify());
        }

        [Fact]
        public async Task RequestedCancellationThrownByAnExceptionFactoryIsNotAFixtureFailure()
        {
            using var http = new HttpSimulation();
            using var cancellation = new CancellationTokenSource();
            http.Expect(HttpMethod.Get, Url).Throw(() =>
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
                return new HttpRequestException();
            });
            using var client = http.CreateClient();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(Url, cancellation.Token));
            http.Verify();
        }

        [Fact]
        public async Task CancellationWhileReadingABodyDoesNotReserveTheStep()
        {
            using var http = new HttpSimulation();
            var expected = http.Expect(HttpMethod.Post, Url).WithJsonBody(_ => true).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var cancellation = new CancellationTokenSource();
            using var source = new CancelableReadStream();
            using var content = new StreamContent(source);
            var pending = client.PostAsync(Url, content, cancellation.Token);
            await source.Started.Task.WaitAsync(Guard, TimeProvider.System);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Guard, TimeProvider.System));
            Assert.False(expected.Received.IsCompleted);

            using var response = await client.PostAsync(Url, new StringContent("{}"));
            http.Verify();
        }

        [Fact]
        public async Task CaptureIsOptInAndSnapshotsRedactCredentialsWithoutRetainingMutableRequests()
        {
            using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
            const string address = Url + "?access_token=query-secret&key=key-secret&safe=yes";
            http.Expect(HttpMethod.Post, address).WithHeader("Authorization", "Bearer header-secret").Respond(HttpStatusCode.OK);
            using var client = http.CreateClient("profiles");
            using var request = new HttpRequestMessage(HttpMethod.Post, address)
            {
                Content = new StringContent("{\"value\":1}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", "Bearer header-secret");
            request.Headers.Add("Cookie", "cookie-secret");
            request.Headers.Add("X-Api-Key", "api-secret");
            request.Headers.Add("X-Visible", "original");
            using var response = await client.SendAsync(request);
            var snapshot = Assert.Single(http.Exchanges);
            request.Headers.Remove("X-Visible");
            request.Headers.Add("X-Visible", "mutated");

            Assert.Equal("profiles", snapshot.ClientName);
            Assert.Equal(HttpMethod.Post, snapshot.Method);
            Assert.Equal("{\"value\":1}", snapshot.Body);
            Assert.Equal("original", Assert.Single(snapshot.Headers["X-Visible"]));
            var diagnostics = snapshot.RequestUri.AbsoluteUri + string.Join(";", snapshot.Headers.SelectMany(p => p.Value));
            Assert.DoesNotContain("query-secret", diagnostics);
            Assert.DoesNotContain("key-secret", diagnostics);
            Assert.DoesNotContain("header-secret", diagnostics);
            Assert.DoesNotContain("cookie-secret", diagnostics);
            Assert.DoesNotContain("api-secret", diagnostics);
            Assert.Contains("safe=yes", snapshot.RequestUri.AbsoluteUri);
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, IReadOnlyList<string>>)snapshot.Headers).Add("Injected", new[] { "value" }));
            http.Verify();
        }

        [Fact]
        public void MissingExpectationDiagnosticsNeverPrintCredentialConstraints()
        {
            using var http = new HttpSimulation();
            http.Expect(HttpMethod.Post, "https://user:password-secret@service.example.test/items?client_secret=query-secret")
                .WithQuery("access_token", "extra-secret")
                .WithHeader("Authorization", "Bearer header-secret")
                .WithJsonBody(new { password = "body-secret" })
                .Respond(HttpStatusCode.OK);

            var failure = Assert.Throws<HttpSimulationException>(() => http.Verify());
            foreach (var secret in new[] { "password-secret", "query-secret", "extra-secret", "header-secret", "body-secret" })
            {
                Assert.DoesNotContain(secret, failure.ToString());
            }

            Assert.Contains("POST", failure.Message);
            Assert.Contains("zero", failure.Message.ToLowerInvariant());
        }

        [Theory]
        [InlineData("application/json", "{\"nested\":{\"password\":\"body-secret\"},\"value\":1}")]
        [InlineData("application/x-www-form-urlencoded", "client_secret=body-secret&value=1")]
        public async Task OptInBodyCaptureStillRedactsCredentialFields(string mediaType, string body)
        {
            using var http = new HttpSimulation(new HttpSimulationOptions { CaptureRequestBody = true });
            http.Expect(HttpMethod.Post, Url).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var content = new StringContent(body, Encoding.UTF8, mediaType);
            using var response = await client.PostAsync(Url, content);

            var captured = Assert.Single(http.Exchanges).Body;
            Assert.DoesNotContain("body-secret", captured);
            Assert.Contains("value", captured);
            http.Verify();
        }

        [Fact]
        public async Task SimulationsWithTheSameUriAreIsolated()
        {
            using var first = new HttpSimulation();
            using var second = new HttpSimulation();
            first.Expect(HttpMethod.Get, Url).RespondText("first");
            second.Expect(HttpMethod.Get, Url).RespondText("second");
            using var firstClient = first.CreateClient();
            using var secondClient = second.CreateClient();

            Assert.Equal("second", await secondClient.GetStringAsync(Url));
            Assert.Equal("first", await firstClient.GetStringAsync(Url));
            first.Verify();
            second.Verify();
        }

        [Fact]
        public async Task AttemptLimitStopsJournalGrowthAndRemainsSticky()
        {
            using var http = new HttpSimulation(new HttpSimulationOptions { MaxRequests = 2 });
            http.ExpectSequence(HttpMethod.Get, Url).Respond(HttpStatusCode.OK).Respond(HttpStatusCode.OK);
            using var client = http.CreateClient();
            using var first = await client.GetAsync(Url);
            using var second = await client.GetAsync(Url);

            await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            await Assert.ThrowsAsync<HttpSimulationException>(() => client.GetAsync(Url));
            Assert.Equal(new long[] { 1, 2 }, http.Exchanges.Select(e => e.Id));
            Assert.Contains("limit", Assert.Throws<HttpSimulationException>(() => http.Verify()).Message.ToLowerInvariant());
        }

        private sealed class ProbeContent(Func<Stream, Task> serialize) : HttpContent
        {
            public int Serializations { get; private set; }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                Serializations++;
                return serialize(stream);
            }

            protected override Task<Stream> CreateContentReadStreamAsync()
                => throw new InvalidOperationException("Do not use the unbounded HttpContent buffering path.");

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }

        private sealed class CancelableReadStream : Stream
        {
            public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(() => completion.TrySetCanceled());
                Started.TrySetResult(true);
                return await completion.Task;
            }
        }

        private sealed class NonSeekableReadStream(byte[] bytes) : MemoryStream(bytes)
        {
            public int BytesRead { get; private set; }
            public override bool CanSeek => false;

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = base.Read(buffer, offset, count);
                BytesRead += read;
                return Task.FromResult(read);
            }

#if NET8_0_OR_GREATER
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = base.Read(buffer.Span);
                BytesRead += read;
                return new ValueTask<int>(read);
            }
#endif
        }
    }
}
