// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport
{
    /// <summary>
    /// Manages a named pipe server connection using the Bot Framework convention:
    /// <list type="bullet">
    /// <item><description>{pipeName}.incoming = server reads (client writes)</description></item>
    /// <item><description>{pipeName}.outgoing = server writes (client reads)</description></item>
    /// </list>
    /// </summary>
    internal sealed class NamedPipeConnection : IAsyncDisposable
    {
        private readonly string _pipeName;
        private readonly ILogger _logger;
        private NamedPipeServerStream _incomingPipe;
        private NamedPipeServerStream _outgoingPipe;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipeConnection"/> class.
        /// </summary>
        /// <param name="pipeName">The base pipe name. Must not be null or empty.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pipeName"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public NamedPipeConnection(string pipeName, ILogger logger)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
            ArgumentNullException.ThrowIfNull(logger);

            _pipeName = pipeName;
            _logger = logger;
        }

        /// <summary>
        /// Gets the reader transport (server reads from this pipe).
        /// </summary>
        public NamedPipeTransport Reader { get; private set; }

        /// <summary>
        /// Gets the writer transport (server writes to this pipe).
        /// </summary>
        public NamedPipeTransport Writer { get; private set; }

        /// <summary>
        /// Gets a value indicating whether both pipes are connected.
        /// </summary>
        public bool IsConnected => Reader?.IsConnected == true && Writer?.IsConnected == true;

        /// <summary>
        /// Creates the pipe pair and waits for a client to connect to both.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task WaitForConnectionAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("NamedPipeConnection: Creating pipe pair '{PipeName}.incoming/.outgoing'", _pipeName);

            _incomingPipe = new NamedPipeServerStream(
                $"{_pipeName}.incoming",
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.WriteThrough);

            _outgoingPipe = new NamedPipeServerStream(
                $"{_pipeName}.outgoing",
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.WriteThrough);

            _logger.LogInformation("NamedPipeConnection: Waiting for client connection on '{PipeName}'", _pipeName);

            await Task.WhenAll(
                _incomingPipe.WaitForConnectionAsync(cancellationToken),
                _outgoingPipe.WaitForConnectionAsync(cancellationToken)
            ).ConfigureAwait(false);

            Reader = new NamedPipeTransport(_incomingPipe);
            Writer = new NamedPipeTransport(_outgoingPipe);

            _logger.LogInformation("NamedPipeConnection: Client connected on '{PipeName}'", _pipeName);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_incomingPipe != null)
            {
                TryDisconnect(_incomingPipe);
                await _incomingPipe.DisposeAsync().ConfigureAwait(false);
            }

            if (_outgoingPipe != null)
            {
                TryDisconnect(_outgoingPipe);
                await _outgoingPipe.DisposeAsync().ConfigureAwait(false);
            }

            Reader = null;
            Writer = null;
        }

        private static void TryDisconnect(NamedPipeServerStream pipe)
        {
            try
            {
                if (pipe.IsConnected)
                {
                    pipe.Disconnect();
                }
            }
            catch (Exception)
            {
                // Client may have already disconnected between the check and the call.
            }
        }
    }
}
