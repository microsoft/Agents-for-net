# NamedPipeAgent Sample

This sample demonstrates a **pipe-only** agent — it accepts activities exclusively over named pipes via the `Microsoft.Agents.Hosting.DirectLine.NamedPipes` library. It is the canonical shape used when deploying behind the **DirectLine App Service extension** (a.k.a. DirectLineFlex), where the sidecar relays traffic to the agent over a named pipe instead of HTTP.

## What's Different from EmptyAgent

Compared to [EmptyAgent](../EmptyAgent/README.md), this sample:

- **Adds** `builder.AddAgentNamedPipeTransport();`
- **Omits** HTTP endpoint mapping (`MapAgentRootEndpoint`, `MapAgentApplicationEndpoints`).
- **Omits** ASP.NET authentication wiring (`AddAgentAspNetAuthentication`, `UseAuthentication`, `UseAuthorization`).

`AddAgentNamedPipeTransport()` adds:

- A `NamedPipeHostedService` that listens on `bfv4.pipes.incoming` / `bfv4.pipes.outgoing`.
- A `DelegatingHandler` that routes outbound HTTP calls to `urn:botframework:namedpipe:*` back through the pipe.

> **Note:** Because no HTTP endpoint is mapped, this sample is not directly reachable from the Bot Framework Emulator or Agents Playground. If you need an HTTP endpoint as well (for health checks, local Emulator testing, or hybrid scenarios), start from [EmptyAgent](../EmptyAgent/README.md) and add `builder.AddAgentNamedPipeTransport()` to that project.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Running Locally

```bash
dotnet run
```

When the process starts, the agent:

- **Does not** expose `/api/messages` (no HTTP handlers are mapped).
- **Does not** require external authentication configuration.
- Creates the named-pipe server pair (`bfv4.pipes.incoming` / `bfv4.pipes.outgoing`) and waits for a client to connect.

To exercise the agent locally over the named pipe, you need a process on the same machine that connects as the named-pipe client (the role normally played by the DirectLine App Service extension sidecar). For a no-pipe local interactive loop, use the EmptyAgent sample instead.

### Custom Pipe Name

 > **Note:** The DirectLine App Service extension uses the pipe name `{WEBSITE_SITE_NAME}.directline`. See the library [README](../../libraries/Hosting/DirectLine.NamedPipes/README.md#pipe-name) for details.

To use a different pipe name, pass it to the extension:
```csharp
builder.AddAgentNamedPipeTransport("my-custom-pipe");
```


## Deployment to Azure App Service

When deployed to Azure App Service with the DirectLine App Service extension enabled:

1. The App Service sidecar connects to your agent over the named pipe pair.
2. External traffic, authentication, and TLS are handled by the sidecar.
3. The pipe connection is treated as trusted; no JWT validation is performed on the pipe.

See the library [README](../../libraries/Hosting/DirectLine.NamedPipes/README.md) for end-to-end App Service setup, pipe-name configuration, and the private-flow architecture diagram.

## Further reading

- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents)
- [`Microsoft.Agents.Hosting.DirectLine.NamedPipes` library README](../../libraries/Hosting/DirectLine.NamedPipes/README.md)
- [EmptyAgent Sample](../EmptyAgent/README.md) — HTTP-only base sample
