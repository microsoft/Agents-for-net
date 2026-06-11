// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Background service that manages the named pipe server lifecycle.
    /// Accepts client connections, wires up the protocol, and handles reconnection.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="NamedPipeHostedService"/> class.
    /// </remarks>
    /// <param name="activityHandler">The activity handler for processing inbound requests.</param>
    /// <param name="messageHandler">The message handler for outbound pipe routing.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configuration">The application configuration.</param>
    internal sealed class NamedPipeHostedService(
        NamedPipeActivityHandler activityHandler,
        NamedPipeMessageHandler messageHandler,
        ILogger<NamedPipeHostedService> logger,
        IConfiguration configuration) : BackgroundService
    {
        private readonly NamedPipeActivityHandler _activityHandler = activityHandler ?? throw new ArgumentNullException(nameof(activityHandler));
        private readonly NamedPipeMessageHandler _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        private readonly ILogger<NamedPipeHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly string _pipeName = configuration?.GetValue("NamedPipe:PipeName", "bfv4.pipes") ?? "bfv4.pipes";

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NamedPipeHostedService: Starting with pipe name '{PipeName}'.", _pipeName);

            while (!stoppingToken.IsCancellationRequested)
            {
                NamedPipeConnection connection = null;
                NamedPipeProtocol protocol = null;

                try
                {
                    connection = new NamedPipeConnection(_pipeName, _logger);
                    await connection.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);

                    protocol = new NamedPipeProtocol(connection.Reader, connection.Writer, _logger)
                    {
                        OnRequestReceived = _activityHandler.HandleAsync
                    };

                    // Start read loop before publishing to outbound callers.
                    protocol.Start();
                    _messageHandler.SetProtocol(protocol);

                    _logger.LogInformation("NamedPipeHostedService: Protocol active on '{PipeName}'.", _pipeName);

                    try
                    {
                        // Wait for read loop to exit (pipe disconnect or error).
                        await protocol.Completion.WaitAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    _logger.LogInformation("NamedPipeHostedService: Connection lost, will reconnect.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("NamedPipeHostedService: Shutting down.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "NamedPipeHostedService: Error, retrying in 2s.");
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    _messageHandler.SetProtocol(null);
                    if (protocol != null)
                    {
                        await protocol.DisposeAsync().ConfigureAwait(false);
                    }

                    if (connection != null)
                    {
                        await connection.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
