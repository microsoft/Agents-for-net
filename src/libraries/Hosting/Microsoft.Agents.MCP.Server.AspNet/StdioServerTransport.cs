using Microsoft.Agents.MCP.Core.JsonRpc;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.MCP.Core.Abstractions;

namespace Microsoft.Agents.MCP.Server.Transports
{
    /// <summary>
    /// Represents a Stdio server transport for Model Context Protocol (MCP).
    /// </summary>
    public class StdioServerTransport : IMcpTransport, IDisposable
    {
        private readonly StreamReader _inputStream;
        private readonly StreamWriter _outputStream;
        private readonly JsonSerializerOptions _jsonOptions;
        private Func<JsonRpcPayload, CancellationToken, Task>? _ingestionFunc;
        private bool _isClosed = false;
        private readonly object _lock = new();

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
        /// Gets a value indicating whether the transport is closed.
        /// </summary>
        public bool IsClosed
        {
            get
            {
                lock (_lock)
                {
                    return _isClosed;
                }
            }
        }

        /// <summary>
        /// Connects the transport and starts listening for messages.
        /// </summary>
        public Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close)
        {
            _ingestionFunc = ingestMessage;
            Task.Run(() => ListenForMessages(close));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a payload to the client asynchronously.
        /// </summary>
        public async Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken ct)
        {
            if (IsClosed) return;

            try
            {
                string json = JsonSerializer.Serialize(payload, _jsonOptions);
                await _outputStream.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error sending message: {ex}");
            }
        }

        /// <summary>
        /// Sends a payload to the server asynchronously.
        /// </summary>
        public Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct)
        {
            return _ingestionFunc?.Invoke(payload, ct) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Closes the transport asynchronously.
        /// </summary>
        public async Task CloseAsync(CancellationToken ct)
        {
            lock (_lock)
            {
                if (_isClosed) return;
                _isClosed = true;
            }

            await _outputStream.WriteLineAsync(JsonSerializer.Serialize(new JsonRpcPayload { Method = "disconnect" }, _jsonOptions));
            Dispose();
        }

        /// <summary>
        /// Listens for incoming messages.
        /// </summary>
        private async Task ListenForMessages(Func<CancellationToken, Task> close)
        {
            try
            {
                var func = _ingestionFunc ?? throw new Exception("Transport is not enabled");
                while (!IsClosed && !_inputStream.EndOfStream)
                {
                    string? line = await _inputStream.ReadLineAsync();
                    if (line != null)
                    {
                        var payload = JsonSerializer.Deserialize<JsonRpcPayload>(line, _jsonOptions);
                        if (payload != null)
                        {
                            await func.Invoke(payload, CancellationToken.None);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing messages: {ex}");
            }
            finally
            {
                await close(CancellationToken.None);
                Dispose();
            }
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_isClosed) return;
                _isClosed = true;
            }

            _inputStream.Dispose();
            _outputStream.Dispose();
        }
    }
}
