using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Client.Initialization;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Client.Transports;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System;

namespace Microsoft.Agents.MCP.Client.Sample;

[Route("/")]
[ApiController]
public class McpController : Controller
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMcpProcessor mcpProcessor;
    private readonly ITransportManager transportManager;

    public McpController(
        IHttpClientFactory httpClientFactory,
        IMcpProcessor mcpProcessor,
        ITransportManager transportManager,
        IMcpHandler mcpHandler)
    {
        this.httpClientFactory = httpClientFactory;
        this.mcpProcessor = mcpProcessor;
        this.transportManager = transportManager;
    }

    [HttpGet("/ping")]
    public IActionResult Ping()
    {
        return Ok(new { message = "Pong", timestamp = DateTime.UtcNow });
    }


    [HttpPost("/mcp/test")]
    public async Task<IActionResult> Test([FromBody] JsonRpcPayload request, CancellationToken ct)
    {
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();
        var transport = new HttpSseClientTransport("", httpClientFactory);
        var session = await mcpProcessor.CreateSessionAsync(transport, ct);
        await ClientRequestHelpers.InitializeAsync(session, new InitializationParameters() { }, ct);
        var ping = await ClientRequestHelpers.SendAsync<PingResponse>(session, new McpPingRequest(PingRequestParameters.Instance), ct);
        return Ok(ping);
    }
}

