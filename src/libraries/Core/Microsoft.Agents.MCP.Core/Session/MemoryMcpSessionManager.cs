using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Core.Payloads;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;

namespace Microsoft.Agents.MCP.Core.Session;

public class MemoryMcpSessionManager : IMcpSessionManager
{
    private readonly IMcpPayloadFactory payloadFactory;
    private readonly ILogger<MemoryMcpSessionManager> _logger;

    public MemoryMcpSessionManager(
        IMcpPayloadFactory payloadFactory,
        ILogger<MemoryMcpSessionManager> logger)
    {
        this.payloadFactory = payloadFactory;
        _logger = logger;
    }

    public async Task<IMcpSession> CreateSessionAsync(IMcpTransport transport, CancellationToken ct)
    {
        _logger.LogInformation("Creating session for transport.");
        await Task.Yield();
        var session = new MemorySession(transport.CloseAsync, _logger);
        var id = Guid.NewGuid().ToString();
        var sessionWrapper = new McpSession(
            session.GetResponseMessagesAsync,
            session.GetIncomingMessagesAsync,
            session.WriteOutgoingPayload,
            session.Close,
            (applies, ct) =>
            {
                session.Properties = applies(session.Properties);
                return Task.CompletedTask;
            },
            () => session.Properties,
            id);
        _ = Task.Run(() => session.ListenToIncoming());
        _ = Task.Run(() => session.ListenToOutgoing());
        _ = Task.Run(() => DispatchMessagesToTransport(session, transport)); 
        await transport.Connect(id, (payload, ct) => DispatchAsync(session, payload, ct), session.Close);
        _logger.LogInformation("Session created with ID {SessionId}", id);
        return sessionWrapper;
    }

    private async Task DispatchMessagesToTransport(MemorySession session, IMcpTransport transport)
    {
        _logger.LogInformation("Dispatching messages to transport.");
        try
        {
            await foreach (var item in session.GetResponseMessagesAsync(CancellationToken.None))
            {
                var convertedPayload = payloadFactory.CreateJsonRpcPayload(item);
                await transport.SendOutgoingAsync(convertedPayload, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (session.IsClosed || transport.IsClosed)
        {
            _logger.LogWarning("Operation canceled: session or transport is closed.");
        }
    }

    private async Task DispatchAsync(MemorySession session, JsonRpcPayload payload, CancellationToken ct)
    {
        _logger.LogInformation("Dispatching payload to session.");
        if (session.IsClosed)
        {
            _logger.LogError("Session is closed. Cannot dispatch payload.");
            throw new ArgumentException("Session not found");
        }

        var parsedPayload = payloadFactory.CreatePayload(payload);
        await session.WriteIncomingPayload(parsedPayload, ct);
    }

    private record MemorySession
    {
        public CancellationTokenSource ctSource = new CancellationTokenSource();
        public McpContextProperties Properties { get; set; } = new();
        ConcurrentDictionary<StreamConsumer.StreamEnumerable, bool> incomingStreamConsumers = new();
        ConcurrentDictionary<StreamConsumer.StreamEnumerable, bool> outgoingStreamConsumers = new();
        private readonly ILogger _logger;

        public MemorySession(Func<CancellationToken, Task> closeAsync, ILogger logger)
        {
            this.closeAsync = closeAsync;
            _logger = logger;
        }

        private Func<CancellationToken, Task> closeAsync;
        private static readonly UnboundedChannelOptions s_channelOptions = new()
        {
            AllowSynchronousContinuations = true,
            SingleWriter = false,
            SingleReader = false,
        };

        public async Task WriteOutgoingPayload(McpPayload request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Writing outgoing payload.");
            await _outgoingQueue.Writer.WriteAsync(request, cancellationToken);
        }

        public async Task WriteIncomingPayload(McpPayload request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Writing incoming payload.");
            await _incomingQueue.Writer.WriteAsync(request, cancellationToken);
        }

        public IAsyncEnumerable<McpPayload> GetIncomingMessagesAsync(CancellationToken cancellationToken)
        {
            var consumer = new StreamConsumer(this, cancellationToken, true);
            return consumer;
        }

        public IAsyncEnumerable<McpPayload> GetResponseMessagesAsync(CancellationToken cancellationToken)
        {
            var consumer = new StreamConsumer(this, cancellationToken, false);
            return consumer;
        }

        public async Task ListenToIncoming()
        {
            var asyncEnumerable = _incomingQueue.Reader.ReadAllAsync(ctSource.Token);
            await foreach (var payload in asyncEnumerable)
            {
                foreach (var consumer in incomingStreamConsumers)
                {
                    await consumer.Key.NotifyAsync(payload);
                }
            }
        }

        public async Task ListenToOutgoing()
        {
            var asyncEnumerable = _outgoingQueue.Reader.ReadAllAsync(ctSource.Token);
            await foreach (var payload in asyncEnumerable)
            {
                foreach(var consumer in outgoingStreamConsumers)
                {
                    await consumer.Key.NotifyAsync(payload);
                }
            }
        }

        public async Task Close(CancellationToken ct)
        {
            if (!IsClosed)
            {
                _logger.LogInformation("Closing session.");
                IsClosed = true;
                _outgoingQueue.Writer.Complete();
                _incomingQueue.Writer.Complete();
                await closeAsync.Invoke(ct);

                foreach (var consumer in incomingStreamConsumers)
                {
                    consumer.Key.Cancel();
                }

                foreach (var consumer in outgoingStreamConsumers)
                {
                    consumer.Key.Cancel();
                }

                ctSource.Cancel();

                _logger.LogInformation("Session closed.");
            }
        }

        public bool IsClosed { get; internal set; }

        private readonly Channel<McpPayload> _outgoingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);

        private readonly Channel<McpPayload> _incomingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);

        internal void Register(StreamConsumer.StreamEnumerable streamEnumerable, bool isIncoming)
        {
            if(isIncoming)
            {
                incomingStreamConsumers.TryAdd(streamEnumerable, true);
            }
            else
            {
                outgoingStreamConsumers.TryAdd(streamEnumerable, true);
            }
        }

        internal void Remove(StreamConsumer.StreamEnumerable streamEnumerable, bool isIncoming)
        {
            if (isIncoming)
            {
                incomingStreamConsumers.TryRemove(streamEnumerable, out _);
            }
            else
            {
                outgoingStreamConsumers.TryRemove(streamEnumerable, out _);
            }
        }
    }

    private class StreamConsumer : IAsyncEnumerable<McpPayload>
    {
        private readonly MemorySession memorySession;
        private readonly bool isIncoming;
        private readonly CancellationToken cancellationToken;

        public StreamConsumer(MemorySession memorySession, CancellationToken ct, bool isIncoming)
        {
            this.memorySession = memorySession;
            this.cancellationToken = ct;
            this.isIncoming = isIncoming;
        }

        public IAsyncEnumerator<McpPayload> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new StreamEnumerable(this, CancellationTokenSource.CreateLinkedTokenSource(this.cancellationToken, cancellationToken));
        }

        internal class StreamEnumerable : IAsyncEnumerator<McpPayload>
        {
            private McpPayload? _current;
            private static readonly UnboundedChannelOptions s_channelOptions = new()
            {
                AllowSynchronousContinuations = true,
                SingleWriter = false,
                SingleReader = true,
            };


            private readonly Channel<McpPayload> _incomingQueue = Channel.CreateUnbounded<McpPayload>(s_channelOptions);
            private StreamConsumer streamConsumer;
            private CancellationTokenSource cancellationToken;

            public StreamEnumerable(StreamConsumer streamConsumer, CancellationTokenSource cancellationToken)
            {
                this.streamConsumer = streamConsumer;
                this.cancellationToken = cancellationToken;
                streamConsumer.memorySession.Register(this, streamConsumer.isIncoming);
            }

            public McpPayload Current
            {
                get
                {
                    if (_current == null)
                    {
                        throw new Exception("Move index before consuming");
                    }

                    return _current;
                }
            }

            public ValueTask DisposeAsync()
            {
                streamConsumer.memorySession.Remove(this, streamConsumer.isIncoming);
                cancellationToken.Dispose();
                return ValueTask.CompletedTask;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                var c = await _incomingQueue.Reader.ReadAsync(cancellationToken.Token);
                _current = c;
                return true;
            }

            internal async Task NotifyAsync(McpPayload payload)
            {
                await _incomingQueue.Writer.WriteAsync(payload);
            }

            internal void Cancel()
            {
                throw new NotImplementedException();
            }
        }
    }
}
