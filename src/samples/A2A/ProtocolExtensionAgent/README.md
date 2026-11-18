# Optional A2A protocol extension sample

This deliberately contrived sample adds a "debate" extension to an otherwise
ordinary echo agent. It needs .NET 10, but no model, Azure resources, or credentials.
It is anonymous and intended only for local development.

Most agents do not need custom protocol extensions. Prefer normal skills and
message routes unless a client and server need an additional protocol contract.
This sample does not replace SDK-owned extensions such as In-Task Authorization.

## Run

From the repository root:

```powershell
dotnet run --project src\samples\A2A\ProtocolExtensionAgent\ProtocolExtensionAgent.csproj
```

The sample listens at `http://localhost:3978`. Inspect the Agent Card:

```powershell
Invoke-RestMethod http://localhost:3978/a2a/card
```

Its extension declaration is composed from the same registration that handles
requests, before any `IAgentCardHandler` customization or signing.

## Try the contrived operation

In another PowerShell terminal:

```powershell
$headers = @{
    "A2A-Extensions" = "https://example.org/extensions/debate/v1"
}
$body = @{
    topic = "tabs or spaces"
    argument = "tabs are best"
} | ConvertTo-Json

Invoke-RestMethod http://localhost:3978/a2a/debate:resume `
    -Method Post -ContentType application/json -Headers $headers -Body $body
```

The reply is:

```text
Counterpoint on tabs or spaces: what if the opposite of 'tabs are best' were true?
```

The equivalent JSON-RPC call uses the same typed operation:

```powershell
$rpc = @{
    jsonrpc = "2.0"
    id = "1"
    method = "debate/resume"
    params = @{
        topic = "tabs or spaces"
        argument = "tabs are best"
    }
} | ConvertTo-Json -Depth 4

Invoke-RestMethod http://localhost:3978/a2a `
    -Method Post -ContentType application/json -Headers $headers -Body $rpc
```

Omitting `-Headers $headers` rejects the custom operation because its owning
extension has not been activated. Empty fields or malformed JSON return an
A2A invalid-parameters error.

This operation only manufactures a counterargument. It does not resume a stored
A2A task or implement a real debate workflow.

## How it works

`Program.cs` opts in with one registration:

```csharp
builder.Services.AddA2AProtocolExtension<DebateExtension>();
```

`DebateExtension.cs` declares support, registers a typed operation using
source-generated JSON metadata, and binds it to JSON-RPC and HTTP+JSON.
The host finalizes the registry only after all contributors have run.

`DebateAgent.cs` also reads the upstream activation snapshot for normal messages.
Without the header it echoes the message. With the header it playfully disagrees.
There is no second header parser or negotiation implementation.

An application can register several contributors. For a multi-agent application,
use `AddA2AProtocolExtension<DebateAgent, DebateExtension>()` so support belongs
only to that mapped agent. A duplicate extension URI, operation ID, or transport
binding fails at startup rather than replacing an SDK-owned registration.

See the [A2A developer guide](../A2AAgent/A2A-DEVELOPER-GUIDE.md#advanced-add-a-protocol-extension)
for availability policies, declaration-only extensions, and native gRPC.
