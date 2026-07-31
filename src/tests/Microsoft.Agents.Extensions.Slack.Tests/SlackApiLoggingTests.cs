// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackApiLoggingTests
{
    [Fact]
    public async Task CallAsync_Success_LogsSanitizedRequestAndResponse()
    {
        var logger = new RecordingLogger<SlackApi>();
        var slackApi = CreateSlackApi(
            (_, _) => Task.FromResult(CreateJsonResponse(
                """{"ok":true,"ts":"1712345678.123456","access_token":"xoxb-response-secret"}""")),
            logger);

        await slackApi.CallAsync(
            "chat.postMessage",
            new
            {
                text = "hello",
                thread_ts = "1712345000.000001",
                token = "xoxp-options-secret",
                client_secret = "client-secret"
            },
            "xoxb-bearer-secret");

        var requestLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 1);
        var responseLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 2);

        Assert.Equal(LogLevel.Debug, requestLog.Level);
        Assert.Contains("chat.postMessage", requestLog.Message, StringComparison.Ordinal);
        Assert.Contains("hello", requestLog.Message, StringComparison.Ordinal);
        Assert.Contains("1712345000.000001", requestLog.Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", requestLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("xoxp-options-secret", requestLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("client-secret", requestLog.Message, StringComparison.Ordinal);

        Assert.Equal(LogLevel.Debug, responseLog.Level);
        Assert.Contains("chat.postMessage", responseLog.Message, StringComparison.Ordinal);
        Assert.Contains("200", responseLog.Message, StringComparison.Ordinal);
        Assert.Contains("1712345678.123456", responseLog.Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", responseLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("xoxb-response-secret", responseLog.Message, StringComparison.Ordinal);

        var allLogs = string.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain("xoxb-bearer-secret", allLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", allLogs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallAsync_SlackError_LogsResponseBeforeThrowing()
    {
        var logger = new RecordingLogger<SlackApi>();
        var slackApi = CreateSlackApi(
            (_, _) => Task.FromResult(CreateJsonResponse(
                """{"ok":false,"error":"invalid_auth","access_token":"xoxb-error-secret"}""")),
            logger);

        await Assert.ThrowsAsync<SlackResponseException>(() => slackApi.CallAsync("auth.test"));

        var responseLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 2);
        Assert.Contains("auth.test", responseLog.Message, StringComparison.Ordinal);
        Assert.Contains("200", responseLog.Message, StringComparison.Ordinal);
        Assert.Contains("invalid_auth", responseLog.Message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", responseLog.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("xoxb-error-secret", responseLog.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallAsync_TransportFailure_LogsRequestOnly()
    {
        var logger = new RecordingLogger<SlackApi>();
        var slackApi = CreateSlackApi(
            (_, _) => throw new HttpRequestException("transport failed"),
            logger);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => slackApi.CallAsync("conversations.info", new { channel = "C123" }));

        var requestLog = Assert.Single(logger.Entries);
        Assert.Equal(1, requestLog.EventId.Id);
        Assert.Contains("conversations.info", requestLog.Message, StringComparison.Ordinal);
        Assert.Contains("C123", requestLog.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallAsync_SendsAuthorizationHeaderWithoutLoggingIt()
    {
        const string token = "xoxb-authorization-secret";
        AuthenticationHeaderValue? authorization = null;
        var logger = new RecordingLogger<SlackApi>();
        var slackApi = CreateSlackApi(
            (request, _) =>
            {
                authorization = request.Headers.Authorization;
                return Task.FromResult(CreateJsonResponse("""{"ok":true}"""));
            },
            logger);

        await slackApi.CallAsync("auth.test", token: token);

        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal(token, authorization.Parameter);

        var allLogs = string.Join(Environment.NewLine, logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(token, allLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", allLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", allLogs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallAsync_RequestLoggerFailure_DoesNotPreventSend()
    {
        var sendCount = 0;
        var logger = new ThrowingLogger<SlackApi>(
            isEnabledException: new InvalidOperationException("Request logger failed"));
        var slackApi = CreateSlackApi(
            (_, _) =>
            {
                sendCount++;
                return Task.FromResult(CreateJsonResponse("""{"ok":true}"""));
            },
            logger);

        var response = await slackApi.CallAsync("auth.test");

        Assert.True(response.ok);
        Assert.Equal(1, sendCount);
    }

    [Fact]
    public async Task CallAsync_ResponseLoggerFailure_DoesNotFailSuccessfulCall()
    {
        var logger = new ThrowingLogger<SlackApi>(
            logException: eventId => eventId.Id == 2
                ? new InvalidOperationException("Response logger failed")
                : null);
        var slackApi = CreateSlackApi(
            (_, _) => Task.FromResult(CreateJsonResponse("""{"ok":true}""")),
            logger);

        var response = await slackApi.CallAsync("auth.test");

        Assert.True(response.ok);
    }

    [Fact]
    public async Task CallAsync_LoggerOperationCanceledException_Propagates()
    {
        var logger = new ThrowingLogger<SlackApi>(
            isEnabledException: new OperationCanceledException("Logger canceled"));
        var slackApi = CreateSlackApi(
            (_, _) => Task.FromResult(CreateJsonResponse("""{"ok":true}""")),
            logger);

        await Assert.ThrowsAsync<OperationCanceledException>(() => slackApi.CallAsync("auth.test"));
    }

    private static SlackApi CreateSlackApi(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc,
        ILogger<SlackApi> logger)
    {
        var handler = new TestHandler(sendFunc);
        var httpClient = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(nameof(SlackApi)))
            .Returns(httpClient);

        return new SlackApi(factory.Object, logger);
    }

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class TestHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendFunc;

        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
        {
            _sendFunc = sendFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _sendFunc(request, cancellationToken);
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        private readonly Exception? _isEnabledException;
        private readonly Func<EventId, Exception?>? _logException;

        public ThrowingLogger(
            Exception? isEnabledException = null,
            Func<EventId, Exception?>? logException = null)
        {
            _isEnabledException = isEnabledException;
            _logException = logException;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (_isEnabledException != null)
            {
                throw _isEnabledException;
            }

            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var logException = _logException?.Invoke(eventId);
            if (logException != null)
            {
                throw logException;
            }
        }
    }
}
