using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace Microsoft.Agents.Mcp.Client.Transports
{
    /// <summary>
    /// Represents an HTTP callback client transport.
    /// </summary>
    public class HttpSseClientTransport : IMcpTransport, IAsyncDisposable
    {
        private static JsonSerializerOptions _options = Serialization.GetDefaultMcpSerializationOptions();

        public string? Endpoint { get; private set; }
        private CancellationTokenSource tokenSource = new();
        private Func<JsonRpcPayload, CancellationToken, Task>? ingestionFunc;
        private bool closed = false;
        private Stream? _stream;
        private readonly string initializationEndpoint;
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpCallbackServerTransport"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="initializationEndpoint">The endpoint to call.</param>
        public HttpSseClientTransport(
            string initializationEndpoint,
            IHttpClientFactory httpClientFactory)
        {
            this.initializationEndpoint = initializationEndpoint;
            this.httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Gets a value indicating whether the transport is closed.
        /// </summary>
        public bool IsClosed => closed;

        /// <summary>
        /// Closes the transport asynchronously.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous close operation.</returns>
        public Task CloseAsync(CancellationToken ct)
        {
            closed = true;
            tokenSource.Cancel();
            _stream?.Dispose();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Connects the transport.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="ingestMessage">The function to ingest messages.</param>
        /// <param name="close">The function to close the transport.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        public async Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close)
        {
            // Call endpoint to connect to
            var client = httpClientFactory.CreateClient();

            var response = await client.GetAsync(initializationEndpoint, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to connect to endpoint");
            }

            _stream = await response.Content.ReadAsStreamAsync(tokenSource.Token);
            StreamReader sr = new(_stream);

            // Get Endpoint
            string? line = await sr.ReadLineAsync();
            if (line?.StartsWith("event: endpoint", StringComparison.InvariantCulture) != true)
            {
                throw new Exception("SSE connect did not start with an endpoint payload ");
            }

            line = await sr.ReadLineAsync();
            if (line?.StartsWith("data:", StringComparison.InvariantCulture) != true)
            {
                throw new Exception("SSE connect did not send an endpoint payload ");
            }

            Endpoint = line[7..];
            ingestionFunc = ingestMessage;

            // Stream payloads
            _ = Task.Run(() => ConsumeResponseAsync(sr));
        }

        private async Task ConsumeResponseAsync(StreamReader reader)
        {
            string streamType = string.Empty;
            while (!closed && !reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync(tokenSource.Token);
                
                if(line == null)
                {
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.InvariantCulture))
                {
                    streamType = line[7..];
                }
                else if (line.StartsWith("data:", StringComparison.InvariantCulture) && streamType == "message")
                {
                    string jsonRaw = line[6..];
                    var payload = JsonSerializer.Deserialize<JsonRpcPayload>(jsonRaw, _options);
                    if(payload != null)
                    {
                        await ProcessPayloadAsync(payload, tokenSource.Token);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a payload to the client asynchronously.
        /// </summary>
        /// <param name="payload">The JSON-RPC payload.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public async Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken ct)
        {
            using var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(Endpoint, payload, ct);
        }

        /// <summary>
        /// Sends a payload to the server asynchronously.
        /// </summary>
        /// <param name="payload">The JSON-RPC payload.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct) => ingestionFunc?.Invoke(payload, ct) ?? throw new Exception("Transport has not been initialized");

        public async ValueTask DisposeAsync()
        {
            await CloseAsync(CancellationToken.None);
        }
    }
}
