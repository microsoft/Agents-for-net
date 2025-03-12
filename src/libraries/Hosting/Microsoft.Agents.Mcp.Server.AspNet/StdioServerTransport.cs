using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Server.AspNet
{
    /// <summary>
    /// Represents a Stdio server transport for Model Context Protocol (Mcp).
    /// </summary>
    public class StdioServerTransport : IMcpTransport, IAsyncDisposable, IDisposable
    {
        private readonly StreamReader _inputStream;
        private readonly StreamWriter _outputStream;
        private readonly JsonSerializerOptions _jsonOptions;
        private Func<JsonRpcPayload, CancellationToken, Task>? _ingestionFunc;
        private Func<CancellationToken, Task>? _closeHandler;
        private bool _isClosed;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private CancellationTokenSource? _listenerCancellationSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="StdioServerTransport"/> class.
        /// </summary>
        public StdioServerTransport(Stream inputStream, Stream outputStream)
        {
            _inputStream = new StreamReader(inputStream, Encoding.UTF8, leaveOpen: true);
            _outputStream = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            _jsonOptions = Serialization.GetDefaultMcpSerializationOptions();
        }

        /// <summary>
        /// Convenience constructor using Console.OpenStandardInput and Console.OpenStandardOutput
        /// </summary>
        public StdioServerTransport()
            : this(Console.OpenStandardInput(), Console.OpenStandardOutput())
        {
        }

        /// <summary>
        /// Gets a value indicating whether the transport is closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                try
                {
                    _semaphore.Wait();
                    return _isClosed;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        /// <summary>
        /// Connects the transport and starts listening for messages.
        /// </summary>
        public async Task Connect(string sessionId,
            Func<JsonRpcPayload, CancellationToken, Task> ingestMessage,
            Func<CancellationToken, Task> closeHandler)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_isClosed)
                {
                    throw new InvalidOperationException("Transport is already closed.");
                }

                _ingestionFunc = ingestMessage ?? throw new ArgumentNullException(nameof(ingestMessage));
                _closeHandler = closeHandler ?? throw new ArgumentNullException(nameof(closeHandler));

                _listenerCancellationSource = new CancellationTokenSource();
                _ = Task.Run(() => ListenForMessagesAsync(_listenerCancellationSource.Token));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Sends a payload to the client asynchronously.
        /// </summary>
        public async Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken cancellationToken)
        {
            if (IsClosed) return;

            try
            {
                string json = JsonSerializer.Serialize(payload, _jsonOptions);
                await _outputStream.WriteLineAsync(json.AsMemory(), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log or handle the error appropriately
                Console.Error.WriteLine($"Error sending message: {ex}");
            }
        }

        /// <summary>
        /// Sends a payload to the server asynchronously.
        /// </summary>
        public Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken cancellationToken)
        {
            return _ingestionFunc?.Invoke(payload, cancellationToken)
                ?? Task.CompletedTask;
        }

        /// <summary>
        /// Closes the transport asynchronously.
        /// </summary>
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isClosed) return;
                _isClosed = true;

                // Signal disconnection
                await SendOutgoingAsync(new JsonRpcPayload { Method = "disconnect" }, cancellationToken);

                // Cancel ongoing message listening
                _listenerCancellationSource?.Cancel();

                // Invoke close handler
                if (_closeHandler != null)
                {
                    await _closeHandler(cancellationToken);
                }
            }
            finally
            {
                _semaphore.Release();
                Dispose();
            }
        }

        /// <summary>
        /// Listens for incoming messages.
        /// </summary>
        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                EnsureTransportReady();

                while (!cancellationToken.IsCancellationRequested &&
                       !_inputStream.EndOfStream)
                {
                    string? line = await _inputStream.ReadLineAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var payload = JsonSerializer.Deserialize<JsonRpcPayload>(line, _jsonOptions);
                    if (payload != null)
                    {
                        await ProcessPayloadAsync(payload, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                // Log or handle the error appropriately
                Console.Error.WriteLine($"Error processing messages: {ex}");
            }
            finally
            {
                await CloseAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// Ensures the transport is in a valid state before processing.
        /// </summary>
        private void EnsureTransportReady()
        {
            if (_ingestionFunc == null)
            {
                throw new InvalidOperationException("Transport is not enabled. Call ConnectAsync first.");
            }
        }

        /// <summary>
        /// Disposes resources synchronously.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await CloseAsync(CancellationToken.None);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Actual disposal implementation.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _semaphore.Wait();
                try
                {
                    if (_isClosed) return;
                    _isClosed = true;

                    _listenerCancellationSource?.Dispose();
                    _inputStream.Dispose();
                    _outputStream.Dispose();
                    _semaphore.Dispose();
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
    }
}