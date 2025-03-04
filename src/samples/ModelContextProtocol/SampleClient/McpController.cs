using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Client.Initialization;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Agents.Mcp.Client.Transports;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ServerMethods.Initialize;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System;

namespace Microsoft.Agents.Mcp.Client.Sample;

[Route("/")]
[ApiController]
public class McpController : Controller
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMcpProcessor McpProcessor;
    private readonly ITransportManager transportManager;

    public McpController(
        IHttpClientFactory httpClientFactory,
        IMcpProcessor McpProcessor,
        ITransportManager transportManager,
        IMcpHandler McpHandler)
    {
        this.httpClientFactory = httpClientFactory;
        this.McpProcessor = McpProcessor;
        this.transportManager = transportManager;
    }

    [HttpGet("/ping")]
    public IActionResult Ping()
    {
        return Ok(new { message = "Pong", timestamp = DateTime.UtcNow });
    }


    [HttpPost("/Mcp/test")]
    public async Task<IActionResult> Test([FromBody] JsonRpcPayload request, CancellationToken ct)
    {
        using var inputStream = new MemoryStream();
        using var outputStream = new MemoryStream();
        var transport = new HttpSseClientTransport("", httpClientFactory);
        var session = await McpProcessor.CreateSessionAsync(transport, ct);
        await ClientRequestHelpers.InitializeAsync(session, new InitializationParameters() { }, ct);
        var ping = await ClientRequestHelpers.SendAsync<PingResponse>(session, new McpPingRequest(PingRequestParameters.Instance), ct);
        return Ok(ping);
    }
}

