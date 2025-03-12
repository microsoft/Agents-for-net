using Microsoft.Agents.Mcp.Core.JsonRpc;
using System.Net.Http.Json;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Agents.Mcp.Core.Abstractions;

namespace Microsoft.Agents.Mcp.Server.AspNet;

/// <summary>
/// Represents an HTTP callback server transport.
/// </summary>
public class HttpCallbackServerTransport : IMcpTransport
{
    private Func<JsonRpcPayload, CancellationToken, Task>? ingestionFunc;
    private bool closed = false;
    private string callbackUrl;

    private readonly ITransportManager transportManager;
    private readonly IHttpClientFactory httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpCallbackServerTransport"/> class.
    /// </summary>
    /// <param name="transportManager">The transport manager.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="callbackUrl">The callback URL.</param>
    public HttpCallbackServerTransport(
        ITransportManager transportManager,
        IHttpClientFactory httpClientFactory,
        string callbackUrl)
    {
        this.transportManager = transportManager;
        this.httpClientFactory = httpClientFactory;
        this.callbackUrl = callbackUrl;
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
            callbackUrl,
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
        ingestionFunc = ingestMessage;
        SessionId = sessionId;
        if (SessionId != null)
        {
            transportManager.AddTransport(SessionId, this);
        }
        return Task.CompletedTask;
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
        await client.PostAsJsonAsync(callbackUrl, payload, ct);
    }

    /// <summary>
    /// Sends a payload to the server asynchronously.
    /// </summary>
    /// <param name="payload">The JSON-RPC payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    public Task ProcessPayloadAsync(JsonRpcPayload payload, CancellationToken ct) => ingestionFunc?.Invoke(payload, ct) ?? throw new Exception("Transport is not initialized");
}
