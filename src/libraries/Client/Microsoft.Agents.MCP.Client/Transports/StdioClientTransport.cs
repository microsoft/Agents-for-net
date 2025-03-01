using Microsoft.Agents.MCP.Core.JsonRpc;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System;
using Microsoft.Agents.MCP.Core.Abstractions;
using System.Linq.Expressions;
using System.Data;

namespace Microsoft.Agents.MCP.Client.Transports
{
    /// <summary>
    /// Represents a Stdio server transport for Model Context Protocol (MCP).
    /// </summary>
    public class StdioClientTransport : IMcpTransport, IDisposable
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private Func<JsonRpcPayload, CancellationToken, Task>? _ingestionFunc;
        private bool _isClosed = false;
        private Process? process;
        private readonly object _lock = new();
        private readonly string program;
        private readonly string arguments;

        /// <summary>
        /// Initializes a new instance of the <see cref="StdioServerTransport"/> class.
        /// </summary>
        public StdioClientTransport(string program, string arguments)
        {
            _jsonOptions = Serialization.GetDefaultMcpSerializationOptions();
            this.program = program;
            this.arguments = arguments;
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
            if (process != null)
            {
                throw new InvalidOperationException("Transport is already connected.");
            }

            // Start Process
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = program,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
            };

            process.Start();

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
                if (process == null)
                {
                    throw new Exception("transport is not initialized");
                }

                await process.StandardInput.WriteLineAsync(json);
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
        public Task CloseAsync(CancellationToken ct)
        {
            lock (_lock)
            {
                if (_isClosed)
                {
                    return Task.CompletedTask;
                }
                _isClosed = true;
            }

            process?.Close();
            Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Listens for incoming messages.
        /// </summary>
        private async Task ListenForMessages(Func<CancellationToken, Task> close)
        {
            var process = this.process ?? throw new Exception("Process has not been initialized");
            var ingestionFunc = this._ingestionFunc ?? throw new Exception("Ingestion function has not been initialized");
            try
            {
                while (!IsClosed && !process.StandardOutput.EndOfStream)
                {
                    string? line = await process.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        var payload = JsonSerializer.Deserialize<JsonRpcPayload>(line, _jsonOptions);
                        if (payload != null)
                        {
                            await _ingestionFunc.Invoke(payload, CancellationToken.None);
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

            process?.Dispose();
        }
    }
}
