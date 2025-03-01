using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.MCP.Core.JsonRpc;
using System.Text.Json;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Server.Transports;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.IO;

namespace Microsoft.Agents.MCP.Server.Sample;

[Route("/")]
[ApiController]
public class McpController : Controller
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMcpProcessor mcpProcessor;
    private readonly ITransportManager transportManager;
    private readonly ILogger<McpController> logger;

    public McpController(
        IHttpClientFactory httpClientFactory,
        IMcpProcessor mcpProcessor,
        ITransportManager transportManager,
        ILogger<McpController> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.mcpProcessor = mcpProcessor;
        this.transportManager = transportManager;
        this.logger = logger;
    }

    [HttpGet("/ping")]
    public IActionResult Ping()
    {
        return Ok(new { message = "Pong", timestamp = DateTime.UtcNow });
    }

    [HttpGet("/mcp/sse")]
    public async Task SseGet(CancellationToken ct)
    {
        logger.LogInformation("Starting SSE connection.");
        var transport = new HttpSseServerTransport(transportManager, (string session) => $"/mcp/sse/message?sessionId={session}", Response, ct, logger);
        var session = await mcpProcessor.CreateSessionAsync(transport, ct);
        await transport.WaitTillCloseAsync(ct);
        logger.LogInformation("SSE connection closed.");
    }

    [HttpPost("/mcp/sse/message")]
    public Task<IActionResult> SsePost(JsonRpcPayload request, [FromQuery] string sessionId, CancellationToken ct)
    {
        logger.LogInformation($"Received SSE POST request for session {sessionId}.");
        return DispatchRequest(request, sessionId, ct);
    }

    [HttpPost("/mcp/http")]
    public async Task<IActionResult> Index(CallbackJsonRpcPayload request, [FromQuery] string? sessionId, CancellationToken ct)
    {
        if (sessionId == null)
        {
            var transport = new HttpCallbackServerTransport(transportManager, httpClientFactory, request.CallbackUrl);
            var session = await mcpProcessor.CreateSessionAsync(transport, ct);
            sessionId = session.SessionId;
        }

        return await DispatchRequest(request, sessionId, ct);
    }

    [HttpPost("/mcp/stdio")]
    public async Task<IActionResult> Stdio([FromBody] JsonRpcPayload request, CancellationToken ct)
    {
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();
        var transport = new StdioServerTransport(inputStream, outputStream);
        var session = await mcpProcessor.CreateSessionAsync(transport, ct);

        // Simulate writing the request to the input stream
        var writer = new StreamWriter(inputStream) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request));
        inputStream.Position = 0;

        // Process the request
        await transport.ProcessPayloadAsync(request, ct);

        // Read the response from the output stream
        outputStream.Position = 0;
        var reader = new StreamReader(outputStream);
        var response = await reader.ReadToEndAsync();

        return Ok(response);
    }

    private async Task<IActionResult> DispatchRequest(JsonRpcPayload request, string sessionId, CancellationToken ct)
    {
        if (transportManager.TryGetTransport(sessionId, out var transport))
        {
            await transport.ProcessPayloadAsync(request, ct);
            return Ok();
        }

        logger.LogWarning($"Transport not found for session {sessionId}.");
        return NotFound();
    }
}

