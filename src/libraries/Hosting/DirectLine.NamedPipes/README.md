# Microsoft.Agents.Hosting.DirectLine.NamedPipes

Named pipe transport for the Microsoft Agents SDK, enabling agents to communicate with DirectLineFlex (Azure App Service sidecar) over named pipes without HTTP roundtrips.

For Azure App Service setup and troubleshooting guidance, see [Enable Direct Line App Service extension for a .NET bot](https://learn.microsoft.com/en-us/azure/bot-service/bot-service-channel-directline-extension-net-bot). That article targets Bot Framework SDK v4; for Microsoft Agents SDK apps, use `AddAgentNamedPipeTransport` instead of the Bot Framework `UseNamedPipes` middleware shown there.

## Usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddAgent<MyAgent>();
builder.AddAgentNamedPipeTransport(); // default pipe name: "bfv4.pipes"

var app = builder.Build();
app.MapAgentApplicationEndpoints();
app.Run();
```

## How It Works

When deployed in Azure App Service with DirectLineFlex, the agent communicates via a pair of named pipes (`{pipeName}.incoming` and `{pipeName}.outgoing`) using the Bot Framework wire protocol (48-byte ASCII framed headers with JSON payloads).

- **Inbound**: Activities arrive over the pipe and are dispatched to the agent's turn pipeline via `IChannelAdapter.ProcessActivityAsync`.
- **Outbound**: Reply activities sent via `ITurnContext.SendActivityAsync` are routed back through the pipe (intercepting `urn:botframework:namedpipe:*` service URLs).

## DirectLineFlex Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant ABS as Azure Bot Service
    participant DLE as DirectLineFlex
    participant Pipe as Named Pipes
    participant Bot as Bot Process

    rect rgba(220, 235, 255, 0.35)
        Note over C,Bot: === INITIALIZATION ===
        DLE->>+ABS: GET /v3/extension (Bearer {ExtensionKey})
        ABS-->>-DLE: {BotId, TokenMakerSecret, BotMessageEndpoint}

        Bot->>Pipe: Create .incoming + .outgoing servers
        DLE->>Pipe: Connect client to both pipes
        Note over DLE,Bot: Pipes established (ib=true, ob=true)
    end

    Note over C,Bot: === TOKEN GENERATION ===
    C->>DLE: POST /.bot/v3/directline/tokens/generate<br/>Authorization: Bearer {DirectLineSecret}
    DLE->>DLE: Validate secret, mint token with TokenMakerSecret
    DLE-->>C: {token, conversationId}

    Note over C,Bot: === WEBSOCKET CONNECTION ===
    C->>DLE: WebSocket CONNECT /.bot/v3/directline/conversations/{id}/stream
    DLE->>DLE: Validate token, create ClientConnection session

    Note over C,Bot: === MESSAGE: CLIENT -> BOT ===
    C->>DLE: WebSocket: Activity{type:"message", text:"Hello"}
    DLE->>DLE: Validate token, store in ActivityStore
    DLE->>Pipe: 'A' frame (requestId) + 'S' frame (Activity JSON)
    Pipe->>Bot: ReadLoop: assemble StreamingRequest
    Bot->>Bot: NamedPipeActivityHandler -> CloudAdapter -> MyAgent.OnTurnAsync()
    Bot->>Pipe: 'B' frame (requestId, 202) - acknowledge receipt

    Note over C,Bot: === MESSAGE: BOT -> CLIENT ===
    Bot->>Bot: turnContext.SendActivityAsync(reply)
    Bot->>Bot: SDK -> PipeRoutingDelegatingHandler -> NamedPipeMessageHandler
    Bot->>Pipe: 'A' frame (newRequestId) + 'S' frame (reply JSON)
    Pipe->>DLE: DirectLineRequestHandler processes reply
    DLE->>DLE: ActivityProcessor -> ClientConnection
    DLE->>C: WebSocket push: ActivitySet{reply}
    DLE->>Pipe: 'B' frame (newRequestId, 200) - acknowledge delivery
    Pipe->>Bot: HandleResponseFrame -> TCS resolves -> SendActivityAsync returns
```

## Configuration

The pipe name defaults to `"bfv4.pipes"` and can be customized:

```csharp
builder.AddAgentNamedPipeTransport(pipeName: "my-custom-pipe");
```

The Bot Framework Direct Line App Service extension configures named pipes with:

```csharp
Environment.GetEnvironmentVariable("APPSETTING_WEBSITE_SITE_NAME") + ".directline"
```

The App Service extension deployment expects that pipe name, configure this library with the same value:

```csharp
var siteName = Environment.GetEnvironmentVariable("APPSETTING_WEBSITE_SITE_NAME");
builder.AddAgentNamedPipeTransport(pipeName: $"{siteName}.directline");
```

The App Service extension also requires the Direct Line channel to be enabled and the App Service application settings `DirectLineExtensionKey` and `DIRECTLINE_EXTENSION_VERSION` to be configured. Enable WebSockets on the App Service, then verify the extension at `https://<app-service-name>.azurewebsites.net/.bot`; successful pipe connection reports `ib: true` and `ob: true`.
