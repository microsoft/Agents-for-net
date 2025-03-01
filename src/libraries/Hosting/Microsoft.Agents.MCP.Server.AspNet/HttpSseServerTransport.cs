using Microsoft.Agents.MCP.Core.JsonRpc;
using System.Text.Json;
using System.Text;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.MCP.Server.Transports
{
    /// <summary>
    /// Represents a Http SSE server transport for Model Context Protocol (MCP).
    /// </summary>
    public class HttpSseServerTransport : IMcpTransport, IDisposable
    {
        private readonly ITransportManager _transportManager;
        private readonly HttpResponse _response;
        private readonly CancellationToken _cancellationToken;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger _logger;
        private string _sessionId = string.Empty;
        private bool _isClosed;
        private bool _disposed;
        private Func<JsonRpcPayload, CancellationToken, Task> _ingestMessage;
        private Func<CancellationToken, Task> _closeCallback;
        private TaskCompletionSource _closeCompletionSource;
        private readonly CancellationTokenRegistration _cancellationRegistration;

        private readonly Func<string, string> _getMessageEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSseServerTransport"/> class.
        /// </summary>
        /// <param name="transportManager">The transport manager to register with.</param>
        /// <param name="getMessageEndpoint">The endpoint path where clients will send their messages.</param>
        /// <param name="response">The HTTP response to write SSE messages to.</param>
        /// <param name="cancellationToken">Cancellation token for the transport lifetime.</param>
        /// <param name="logger">The logger instance.</param>
        public HttpSseServerTransport(ITransportManager transportManager, Func<string, string> getMessageEndpoint, HttpResponse response, CancellationToken cancellationToken, ILogger logger)
        {
            _response = response;
            _cancellationToken = cancellationToken;
            _transportManager = transportManager;
            _getMessageEndpoint = getMessageEndpoint;
            _jsonOptions = Serialization.GetDefaultMcpSerializationOptions();
            _logger = logger;

            _isClosed = false;
            _ingestMessage = (_, _) => Task.CompletedTask;
            _closeCallback = (_) => Task.CompletedTask;
            _closeCompletionSource = new TaskCompletionSource();

            // Configure HTTP response for SSE
            _response.Headers.Append("Content-Type", "text/event-stream");
            _response.Headers.Append("Cache-Control", "no-cache");
            _response.Headers.Append("Connection", "keep-alive");

            // Register cancellation callback to close the transport if the request is aborted
            _cancellationRegistration = _cancellationToken.Register(async () =>
            {
                if (!_isClosed)
                {
                    await CloseAsync(CancellationToken.None);
                }
            });

            _logger.LogInformation("HttpSseServerTransport initialized.");
        }

        /// <summary>
        /// Gets a value indicating whether the transport is closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                return _isClosed;
            }
        }

        /// <summary>
        /// Connects the transport with the specified session ID and message handlers.
        /// </summary>
        /// <param name="sessionId">The session ID to associate with this transport.</param>
        /// <param name="ingestMessage">Callback to process incoming messages.</param>
        /// <param name="close">Callback to invoke when the transport is closed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            _sessionId = sessionId;
            _ingestMessage = ingestMessage ?? throw new ArgumentNullException(nameof(ingestMessage));
            _closeCallback = close ?? throw new ArgumentNullException(nameof(close));

            // Add the transport to the transport manager
            bool isTransportAdded = _transportManager.AddTransport(sessionId, this);

            if (!isTransportAdded)
            {
                throw new InvalidOperationException($"Transport with session ID '{sessionId}' already exists.");
            }

            // Register cancellation callback to close the transport if the request is aborted
            _cancellationToken.Register(async () =>
            {
                if (!_isClosed)
                {
                    await CloseAsync(CancellationToken.None);
                }
            });

            // Send initial SSE connection established message
            await SendSseMessageAsync(SseEventTypes.Endpoint, _getMessageEndpoint(sessionId), _cancellationToken, serialize: false);

            _logger.LogInformation($"Transport connected with session ID: {sessionId}");
        }

        /// <summary>
        /// Closes the transport.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;

            try
            {
                _transportManager.RemoveTransport(_sessionId);
                await _closeCallback(cancellationToken);

                // Send a final SSE message indicating the connection is closed
                await SendSseMessageAsync(SseEventTypes.Close, null, cancellationToken);

                // Complete the response
                await _response.Body.FlushAsync(cancellationToken);

                // Signal that the transport is now closed
                _closeCompletionSource.TrySetResult();

                _logger.LogInformation($"Transport with session ID: {_sessionId} closed.");
            }
            catch (Exception ex)
            {
                // Complete with exception if closing failed
                _closeCompletionSource.TrySetException(ex);
                _logger.LogError(ex, "Error while closing transport.");
                // Ignore exceptions during shutdown
            }
        }

        /// <summary>
        /// Sends a message to the client over SSE.
        /// </summary>
        /// <param name="payload">The JSON RPC payload to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken cancellationToken)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Connection is closed");
            }

            await SendSseMessageAsync(SseEventTypes.Message, payload, cancellationToken);
            _logger.LogDebug($"Sent message: {JsonSerializer.Serialize(payload, _jsonOptions)}");
        }

        /// <summary>
        /// Process a message from the client to be sent to the server.
        /// In SSE context, this method is called when messages are received via other means (e.g., POST requests)
        /// and need to be processed by the server.
        /// </summary>
        /// <param name="payload">The JSON RPC payload received from the client.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken cancellationToken)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Connection is closed");
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            try
            {
                if (_ingestMessage != null)
                {
                    await _ingestMessage(payload, cancellationToken);
                    _logger.LogDebug($"Processed payload: {JsonSerializer.Serialize(payload, _jsonOptions)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payload to server.");
                throw new InvalidOperationException("Failed to send payload to server.", ex);
            }
        }

        /// <summary>
        /// Waits until the transport is closed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the wait operation.</param>
        /// <returns>A task that completes when the transport is closed.</returns>
        public Task WaitTillCloseAsync(CancellationToken cancellationToken = default)
        {
            if (_isClosed)
            {
                return Task.CompletedTask;
            }

            if (cancellationToken == default)
            {
                return _closeCompletionSource.Task;
            }

            // If a cancellation token is provided, we need to handle the case where
            // waiting is canceled without the transport being closed
            return Task.WhenAny(_closeCompletionSource.Task, Task.Delay(Timeout.Infinite, cancellationToken))
                .ContinueWith(t =>
                {
                    if (t.IsCanceled || (t.IsCompleted && t.Result != _closeCompletionSource.Task))
                    {
                        throw new TaskCanceledException();
                    }
                    return t;
                }).Unwrap();
        }

        /// <summary>
        /// Sends a Server-Sent Event message.
        /// </summary>
        /// <param name="eventType">The type of event.</param>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="serialize">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SendSseMessageAsync(string eventType, object? data, CancellationToken cancellationToken, bool serialize = true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine($"event: {eventType}");

                if (data != null)
                {
                    string jsonData = serialize ? JsonSerializer.Serialize(data, _jsonOptions) : data.ToString() ?? throw new Exception("null object");
                    // Split data by lines to conform to SSE format (each line prefixed with "data: ")
                    foreach (var line in jsonData.Split('\n'))
                    {
                        messageBuilder.AppendLine($"data: {line}");
                    }
                }

                messageBuilder.AppendLine(); // Empty line to complete the message

                byte[] messageBytes = Encoding.UTF8.GetBytes(messageBuilder.ToString());
                await _response.Body.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
                await _response.Body.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending SSE message.");
                // If we can't write to the response, the connection is likely broken
                if (!_isClosed)
                {
                    await CloseAsync(CancellationToken.None);
                }
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="HttpSseServerTransport"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Close the transport if it's not already closed
                    if (!_isClosed)
                    {
                        CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
                    }

                    // Unregister the cancellation token registration
                    _cancellationRegistration.Dispose();

                    // Clear the completion source if it hasn't been set yet
                    if (!_closeCompletionSource.Task.IsCompleted)
                    {
                        _closeCompletionSource.TrySetCanceled();
                    }
                }

                _disposed = true;
                _logger.LogInformation("HttpSseServerTransport disposed.");
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="HttpSseServerTransport"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
