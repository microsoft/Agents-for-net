// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder.Testing.Http
{
    /// <summary>A required request, or an explicit sequence of required transport outcomes.</summary>
    public sealed class HttpExpectation
    {
        private readonly HttpSimulation _simulation;
        private readonly bool _sequence;
        private readonly TaskCompletionSource<bool> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal HttpExpectation(HttpSimulation simulation, int id, HttpMethod method, Uri uri, bool sequence)
        {
            _simulation = simulation;
            _sequence = sequence;
            Id = id;
            Method = method;
            Uri = uri;
            Query = HttpRequestMatcher.ParseQuery(uri.Query);
        }

        internal int Id { get; }
        internal HttpMethod Method { get; }
        internal Uri Uri { get; }
        internal string ClientName { get; private set; }
        internal List<KeyValuePair<string, string>> Query { get; }
        internal List<KeyValuePair<string, string>> Headers { get; } = [];
        internal List<Func<JsonElement, bool>> JsonPredicates { get; } = [];
        internal List<HttpOutcome> Outcomes { get; } = [];
        internal int Reserved { get; set; }
        internal string Description => $"{Method} {HttpSimulationDiagnostics.SanitizeUri(Uri)}";

        /// <summary>Gets a signal completed when this expectation's first step is reserved, before its outcome runs.</summary>
        public Task Received => _received.Task;

        /// <summary>Requires an ordinal header value in the request or content headers. Names are case insensitive.</summary>
        public HttpExpectation WithHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A header name is required.", nameof(name));
            }

            AssertionHelpers.ThrowIfNull(value, nameof(value));

            _simulation.Change(() => Headers.Add(new KeyValuePair<string, string>(name, value)));
            return this;
        }

        /// <summary>Adds one decoded name/value pair to the exact expected query multiset.</summary>
        public HttpExpectation WithQuery(string name, string value)
        {
            AssertionHelpers.ThrowIfNull(name, nameof(name));
            AssertionHelpers.ThrowIfNull(value, nameof(value));

            _simulation.Change(() => Query.Add(new KeyValuePair<string, string>(name, value)));
            return this;
        }

        /// <summary>Requires an ordinal factory or explicitly supplied client name. The factory default name is empty.</summary>
        public HttpExpectation ForClient(string clientName)
        {
            AssertionHelpers.ThrowIfNull(clientName, nameof(clientName));

            _simulation.Change(() => ClientName = clientName);
            return this;
        }

        /// <summary>Requires a bounded JSON body satisfying a predicate, invoked outside the simulation lock.</summary>
        /// <remarks>The predicate must not retain its document-backed argument.</remarks>
        public HttpExpectation WithJsonBody(Func<JsonElement, bool> predicate)
        {
            AssertionHelpers.ThrowIfNull(predicate, nameof(predicate));

            _simulation.Change(() => JsonPredicates.Add(predicate));
            return this;
        }

        /// <summary>Requires structural JSON equality, ignoring object property order but preserving array order.</summary>
        public HttpExpectation WithJsonBody(object? body, JsonSerializerOptions? options = null)
        {
            using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(body, options));
            var expected = document.RootElement.Clone();
            return WithJsonBody(actual => JsonElement.DeepEquals(expected, actual));
        }

        /// <summary>Adds a fresh response with the specified status and optional per-attempt configuration.</summary>
        public HttpExpectation Respond(HttpStatusCode status, Action<HttpResponseMessage>? configure = null)
            => RespondAsync((_, _) =>
            {
                var response = new HttpResponseMessage(status);
                try
                {
                    configure?.Invoke(response);
                    return Task.FromResult(response);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            });

        /// <summary>Adds fresh JSON content serialized by System.Text.Json without protocol-specific conversion.</summary>
        public HttpExpectation RespondJson(object? body, HttpStatusCode status = HttpStatusCode.OK, JsonSerializerOptions? options = null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(body, options);
            return Respond(status, response =>
            {
                response.Content = new ByteArrayContent(bytes);
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8"
                };
            });
        }

        /// <summary>Adds fresh UTF-8 text content without interpreting its media type or status.</summary>
        public HttpExpectation RespondText(string text, string mediaType = "text/plain", HttpStatusCode status = HttpStatusCode.OK)
        {
            AssertionHelpers.ThrowIfNull(text, nameof(text));
            AssertionHelpers.ThrowIfNull(mediaType, nameof(mediaType));

            return Respond(status, response => response.Content = new StringContent(text, Encoding.UTF8, mediaType));
        }

        /// <summary>Adds a response callback that must return a fresh response and content on each attempt.</summary>
        /// <remarks>Do not retain the live request. Unrequested callback exceptions are sticky fixture failures; use <c>Throw</c> for simulated faults.</remarks>
        public HttpExpectation RespondAsync(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            AssertionHelpers.ThrowIfNull(responseFactory, nameof(responseFactory));

            return AddOutcome(new HttpOutcome(responseFactory, null, false));
        }

        /// <summary>Adds a deliberate transport fault. Its factory must return a fresh exception.</summary>
        public HttpExpectation Throw(Func<Exception> exceptionFactory)
        {
            AssertionHelpers.ThrowIfNull(exceptionFactory, nameof(exceptionFactory));

            return AddOutcome(new HttpOutcome(null, exceptionFactory, false));
        }

        /// <summary>Adds an operation that waits for request cancellation or simulation disposal.</summary>
        public HttpExpectation WaitForCancellation() => AddOutcome(new HttpOutcome(null, null, true));

        internal void SignalReceived() => _received.TrySetResult(true);

        private HttpExpectation AddOutcome(HttpOutcome outcome)
        {
            _simulation.Change(() =>
            {
                if (!_sequence && Outcomes.Count != 0)
                {
                    throw new InvalidOperationException("Use ExpectSequence to arrange multiple outcomes for one match.");
                }

                Outcomes.Add(outcome);
            });
            return this;
        }
    }

    internal sealed class HttpOutcome(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response,
        Func<Exception> exception, bool waitForCancellation)
    {
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Response { get; } = response;
        internal Func<Exception> Exception { get; } = exception;
        internal bool WaitForCancellation { get; } = waitForCancellation;
    }
}
