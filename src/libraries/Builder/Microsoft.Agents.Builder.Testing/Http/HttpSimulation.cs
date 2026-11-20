// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder.Testing.Http
{
    /// <summary>Strict, per-test simulation of intercepted outbound HTTP attempts.</summary>
    /// <remarks>There is no network fallback. Arrange before the first request, await application work, and verify explicitly before disposal.</remarks>
    public sealed class HttpSimulation : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<HttpExpectation> _expectations = [];
        private readonly List<ExchangeState> _exchanges = [];
        private readonly List<string> _violations = [];
        private readonly CancellationTokenSource _lifetime = new();
        private readonly ConditionalWeakTable<HttpResponseMessage, object> _responses = new();
        private readonly ConditionalWeakTable<HttpContent, object> _contents = new();
        private readonly ConditionalWeakTable<Exception, object> _exceptions = new();
        private readonly int _maxRequests;
        private readonly int _maxBodyBytes;
        private readonly bool _captureBody;
        private bool _frozen;
        private bool _disposed;
        private bool _cancellationCompleted;
        private bool _lifetimeDisposed;
        private bool _limitExceeded;
        private int _pending;

        /// <summary>Initializes an isolated simulation, copying the supplied limits and capture setting.</summary>
        public HttpSimulation(HttpSimulationOptions? options = null)
        {
            options ??= new HttpSimulationOptions();
            if (options.MaxRequests <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MaxRequests must be positive.");
            }

            if (options.MaxRequestBodyBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "MaxRequestBodyBytes must be positive.");
            }

            _maxRequests = options.MaxRequests;
            _maxBodyBytes = options.MaxRequestBodyBytes;
            _captureBody = options.CaptureRequestBody;
        }

        /// <summary>Gets immutable, sanitized snapshots in attempt-ID order. Earlier snapshots do not change as requests complete.</summary>
        public IReadOnlyList<HttpExchange> Exchanges
        {
            get
            {
                lock (_gate)
                {
                    return Array.AsReadOnly(_exchanges.Select(e => e.Snapshot()).ToArray());
                }
            }
        }

        /// <summary>Arranges one required attempt, independently ordered from other expectations.</summary>
        public HttpExpectation Expect(HttpMethod method, string absoluteUri) => AddExpectation(method, absoluteUri, false);

        /// <summary>Arranges successive required outcomes for one match. Exhausted sequences never repeat.</summary>
        public HttpExpectation ExpectSequence(HttpMethod method, string absoluteUri) => AddExpectation(method, absoluteUri, true);

        /// <summary>Creates a caller-owned client with a fresh simulation handler. Disposing it does not dispose this simulation.</summary>
        public HttpClient CreateClient(string? clientName = null) => new(CreateHandler(clientName));

        /// <summary>Creates a fresh terminal handler over shared simulation state, suitable for an application-owned pipeline.</summary>
        public HttpMessageHandler CreateHandler(string? clientName = null)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                return new HttpSimulationHandler(this, clientName, null);
            }
        }

        /// <summary>Fails for missing outcomes, sticky fixture/strictness violations, or pending terminal operations.</summary>
        public void Verify()
        {
            lock (_gate)
            {
                var failures = new List<string>(_violations);
                if (_limitExceeded)
                {
                    failures.Add($"The HTTP simulation request limit of {_maxRequests} attempts was exceeded.");
                }

                foreach (var expectation in _expectations)
                {
                    if (expectation.Outcomes.Count == 0)
                    {
                        failures.Add($"Expected {expectation.Description}; no outcome was configured.");
                    }
                    else if (expectation.Reserved < expectation.Outcomes.Count)
                    {
                        var received = expectation.Reserved == 0 ? "zero" : expectation.Reserved.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        failures.Add($"Expected {expectation.Description}, {expectation.Outcomes.Count} required attempt(s); received {received}.");
                    }
                }

                if (_pending != 0)
                {
                    failures.Add($"{_pending} HTTP terminal operation(s) are still pending.");
                }

                if (failures.Count != 0)
                {
                    throw new HttpSimulationException(string.Join(Environment.NewLine, failures));
                }
            }
        }

        /// <summary>Cancels built-in pending operations without asserting. Does not dispose application clients.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            try
            {
                _lifetime.Cancel();
            }
            catch (AggregateException)
            {
                // A cancellation registration must not mask the test's original failure during cleanup.
            }
            finally
            {
                lock (_gate)
                {
                    _cancellationCompleted = true;
                    DisposeLifetimeIfIdle();
                }
            }
        }

        internal void Change(Action change)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_frozen)
                {
                    throw new InvalidOperationException("HTTP simulation configuration is frozen after the first request.");
                }

                change();
            }
        }

        internal async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string clientName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExchangeState exchange;
            HttpExpectation[] expectations;
            CancellationTokenSource linked;
            lock (_gate)
            {
                ThrowIfDisposed();
                _frozen = true;
                if (_exchanges.Count == _maxRequests)
                {
                    _limitExceeded = true;
                    throw new HttpSimulationException($"The HTTP simulation request limit of {_maxRequests} attempts was exceeded.");
                }

                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
                exchange = new ExchangeState(_exchanges.Count + 1L, clientName, request);
                _exchanges.Add(exchange);
                _pending++;
                expectations = _expectations.ToArray();
            }

            try
            {
                var token = linked.Token;
                token.ThrowIfCancellationRequested();
                if (expectations.Any(e => e.Outcomes.Count == 0))
                {
                    throw Fail("Every HTTP expectation must have an outcome configured before requests start.");
                }

                var candidates = expectations.Where(e => HttpRequestMatcher.Matches(e, request, clientName)).ToList();
                byte[] body = null;
                if (_captureBody || candidates.Any(e => e.JsonPredicates.Count != 0))
                {
                    try
                    {
                        body = await HttpRequestBody.ReadAsync(request, _maxBodyBytes, token).ConfigureAwait(false);
                        if (_captureBody)
                        {
                            var captured = HttpSimulationDiagnostics.CaptureBody(body, request.Content);
                            lock (_gate)
                            {
                                exchange.Body = captured;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (HttpRequestBody.BodyLimitException)
                    {
                        throw Fail($"HTTP request body exceeds the configured limit of {_maxBodyBytes} bytes.");
                    }
                    catch (Exception exception)
                    {
                        throw FixtureFailure("Reading or capturing an HTTP request body", exception);
                    }
                }

                candidates = MatchJson(candidates, body, token);
                HttpExpectation expectation;
                HttpOutcome outcome;
                lock (_gate)
                {
                    token.ThrowIfCancellationRequested();
                    var available = candidates.Where(e => e.Reserved < e.Outcomes.Count).ToArray();
                    if (available.Length > 1)
                    {
                        throw Fail($"Ambiguous HTTP request {exchange.Description}; multiple expectations matched.");
                    }

                    if (available.Length == 0)
                    {
                        throw Fail(candidates.Count == 0
                            ? $"Unexpected {exchange.Description}; no expectation matched."
                            : $"{exchange.Description}: expectation or sequence exhausted after {candidates[0].Reserved} attempt(s).");
                    }

                    expectation = available[0];
                    outcome = expectation.Outcomes[expectation.Reserved++];
                    exchange.ExpectationId = expectation.Id;
                    exchange.SequenceStep = expectation.Reserved;
                }

                expectation.SignalReceived();
                var response = await DispatchAsync(outcome, request, token).ConfigureAwait(false);
                lock (_gate)
                {
                    exchange.StatusCode = response.StatusCode;
                }

                return response;
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    exchange.ExceptionType = exception.GetType().FullName;
                }

                throw;
            }
            finally
            {
                linked.Dispose();
                lock (_gate)
                {
                    _pending--;
                    DisposeLifetimeIfIdle();
                }
            }
        }

        private HttpExpectation AddExpectation(HttpMethod method, string absoluteUri, bool sequence)
        {
            AssertionHelpers.ThrowIfNull(method, nameof(method));

            if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("An absolute HTTP or HTTPS expectation URI is required.", nameof(absoluteUri));
            }

            HttpExpectation expectation = null;
            Change(() =>
            {
                expectation = new HttpExpectation(this, _expectations.Count + 1, method, uri, sequence);
                _expectations.Add(expectation);
            });
            return expectation;
        }

        private List<HttpExpectation> MatchJson(List<HttpExpectation> candidates, byte[] body, CancellationToken token)
        {
            if (!candidates.Any(e => e.JsonPredicates.Count != 0))
            {
                return candidates;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                return candidates.Where(e => e.JsonPredicates.Count == 0).ToList();
            }

            using (document)
            {
                try
                {
                    return candidates.Where(e => e.JsonPredicates.All(p => p(document.RootElement))).ToList();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw FixtureFailure("An HTTP JSON predicate", exception);
                }
            }
        }

        private async Task<HttpResponseMessage> DispatchAsync(HttpOutcome outcome, HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (outcome.WaitForCancellation)
            {
                var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = token.Register(() => completion.TrySetCanceled());
                return await completion.Task.ConfigureAwait(false);
            }

            if (outcome.Exception != null)
            {
                Exception fault;
                try
                {
                    fault = outcome.Exception();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw FixtureFailure("An HTTP exception factory", exception);
                }

                if (fault == null)
                {
                    throw Fail("An HTTP exception factory returned null.");
                }

                lock (_gate)
                {
                    if (_exceptions.TryGetValue(fault, out _))
                    {
                        throw Fail("An HTTP exception factory reused an exception. Return a fresh exception for every attempt.");
                    }

                    _exceptions.Add(fault, new object());
                }

                throw fault;
            }

            HttpResponseMessage response;
            try
            {
                response = await outcome.Response(request, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw FixtureFailure("An HTTP response callback", exception);
            }

            if (response == null)
            {
                throw Fail("An HTTP response callback returned null.");
            }

            lock (_gate)
            {
                var content = response.Content;
                if (_responses.TryGetValue(response, out _) || (content != null && _contents.TryGetValue(content, out _)))
                {
                    throw Fail("An HTTP response callback reused a response or content. Return fresh instances for every attempt.");
                }

                _responses.Add(response, new object());
                if (content != null)
                {
                    _contents.Add(content, new object());
                }
            }

            response.RequestMessage ??= request;
            return response;
        }

        private HttpSimulationException FixtureFailure(string operation, Exception exception)
            => Fail($"{operation} failed ({exception.GetType().FullName}); callback details are omitted to protect credentials.");

        private HttpSimulationException Fail(string message)
        {
            lock (_gate)
            {
                if (_violations.Count < _maxRequests)
                {
                    _violations.Add(message);
                }
            }

            return new HttpSimulationException(message);
        }

        private void ThrowIfDisposed()
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(HttpSimulation));
        }

        private void DisposeLifetimeIfIdle()
        {
            if (_cancellationCompleted && _pending == 0 && !_lifetimeDisposed)
            {
                _lifetime.Dispose();
                _lifetimeDisposed = true;
            }
        }

        private sealed class ExchangeState(long id, string clientName, HttpRequestMessage request)
        {
            private readonly HttpMethod _method = request.Method;
            private readonly Uri _uri = HttpSimulationDiagnostics.SanitizeUri(request.RequestUri);
            private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _headers = HttpSimulationDiagnostics.SnapshotHeaders(request);
            internal string Body { get; set; }
            internal int ExpectationId { get; set; }
            internal int SequenceStep { get; set; }
            internal HttpStatusCode? StatusCode { get; set; }
            internal string ExceptionType { get; set; }
            internal string Description => $"{_method} {_uri}";

            internal HttpExchange Snapshot() => new(id, clientName, _method, _uri, _headers, Body,
                ExpectationId, SequenceStep, StatusCode, ExceptionType);
        }
    }
}
