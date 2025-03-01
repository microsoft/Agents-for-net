using Microsoft.Agents.MCP.Core.JsonRpc;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Core.Abstractions;

namespace Microsoft.Agents.MCP.Client.Transports
{
    /// <summary>
    /// Represents an HTTP callback client transport.
    /// </summary>
    public class HttpCallbackClientTransport : IMcpTransport
    {
        private Func<JsonRpcPayload, CancellationToken, Task>? ingestionFunc;
        private bool closed = false;
        private readonly Uri endpoint;
        private string? ServerSessionId;
        private readonly Func<string, string> callbackEndpointFunc;
        private readonly ITransportManager transportManager;
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpCallbackServerTransport"/> class.
        /// </summary>
        /// <param name="transportManager">The transport manager.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// /// <param name="endpoint">The endpoint to call.</param>
        /// <param name="callbackEndpointFunc">The callback URL.</param>
        public HttpCallbackClientTransport(
            ITransportManager transportManager,
            IHttpClientFactory httpClientFactory,
            Uri endpoint,
            Func<string, string> callbackEndpointFunc)
        {
            this.transportManager = transportManager;
            this.httpClientFactory = httpClientFactory;
            this.endpoint = endpoint;
            this.callbackEndpointFunc = callbackEndpointFunc;
        }

        /// <summary>
        /// Gets a value indicating whether the transport is closed.
        /// </summary>
        public bool IsClosed => closed;

        /// <summary>
        /// Gets the session ID.
        /// </summary>
        public string? SessionId { get; private set; }

        /// <summary>
        /// Closes the transport asynchronously.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous close operation.</returns>
        public async Task CloseAsync(CancellationToken ct)
        {
            closed = true;

            if (SessionId != null)
            {
                transportManager.RemoveTransport(SessionId);
            }

            using var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(
                GetPostUrl(),
                new JsonRpcPayload()
                {
                    Method = "disconnect",
                },
                ct);
        }

        /// <summary>
        /// Connects the transport.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="ingestMessage">The function to ingest messages.</param>
        /// <param name="close">The function to close the transport.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        public Task Connect(string sessionId, Func<JsonRpcPayload, CancellationToken, Task> ingestMessage, Func<CancellationToken, Task> close)
        {
            SessionId = sessionId;
            if (SessionId != null)
            {
                transportManager.AddTransport(SessionId, this);
            }
            ingestionFunc = ingestMessage;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends a payload to the server asynchronously.
        /// </summary>
        /// <param name="payload">The JSON-RPC payload.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public async Task SendOutgoingAsync(JsonRpcPayload payload, CancellationToken ct)
        {
            using var client = httpClientFactory.CreateClient();
            await client.PostAsJsonAsync(GetPostUrl(), CreateCallbackPayload(payload), ct);
        }

        private JsonNode CreateCallbackPayload(JsonRpcPayload payload)
        {
            var element = JsonSerializer.SerializeToNode(payload)?.AsObject() ?? throw new Exception("invalid payload");
            element.Add("callbackUrl", callbackEndpointFunc(SessionId ?? throw new Exception("session is not connected")));
            return element;
        }

        private string? GetPostUrl()
        {
            if (ServerSessionId == null)
            {
                return endpoint.ToString();
            }

            var uriBuilder = new UriBuilder(endpoint);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["sessionId"] = ServerSessionId;
            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }

        /// <summary>
        /// Sends a payload to the processor asynchronously.
        /// </summary>
        /// <param name="payload">The JSON-RPC payload.</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        public Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct)
        {
            // Extract session Id from Initialization Result for future posts
            if(payload.Params?.TryGetProperty("sessionInfo", out var info) == true
                && info.TryGetProperty("id", out var sessionId)
                && !string.IsNullOrEmpty(sessionId.ToString()))
            {
                ServerSessionId = sessionId.ToString();
            }

            return ingestionFunc?.Invoke(payload, ct) ?? throw new Exception("session is not connected");
        }
    }
}
