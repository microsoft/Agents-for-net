# Agent Runner

The `Microsoft.Agents.Hosting.AgentRunner` .NET tool hosts an Agents SDK assembly
without requiring that assembly to supply its own executable entry point.
It requires the .NET 10 **ASP.NET Core runtime**.

```powershell
dotnet tool install Microsoft.Agents.Hosting.AgentRunner --version <published-version> --tool-path .\runner
.\runner\agent-runner --agent-assembly C:\agents\MyAgent.dll --port 3978
```

The customer assembly must expose this exact contract in the global namespace:

```csharp
public static class Program
{
    public static void RegisterServices(WebApplicationBuilder builder)
    {
        builder.AddAgentDefaults().AddAgent<MyAgent>();
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
        // Register the agent's connection/authentication services here as needed.
    }
}
```

Publish the customer class library with its `.deps.json` and private dependencies.
Use SDK dependencies compatible with the runner's SDK release. The runner shares
`Microsoft.Agents.*`, `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`,
`Microsoft.IdentityModel.*` and `System.IdentityModel.Tokens.Jwt`
contracts with its default load context. A separate load context is dependency
isolation, **not a security sandbox**; customer code executes with host privileges.
Cross-major SDK compatibility is not guaranteed.

The runner reads the customer's optional `appsettings.json`; environment variables
override file values. Set these required GUID values before starting it:

```powershell
$env:Connections__ServiceConnection__Settings__ClientId = "<agent-client-id>"
$env:Connections__ServiceConnection__Settings__TenantId = "<agent-tenant-id>"
```

The runner enforces authentication in Production regardless of the development
environment. `POST http://127.0.0.1:<port>/api/messages` accepts signed, unexpired
Azure Bot Service or Microsoft Entra tokens for the configured client audience.
Entra issuers default to the configured tenant's v1/v2 issuers. Deployments that
trust additional issuers must explicitly provide
`AgentRunner:Authentication:AdditionalEntraIssuers` (for example
`AgentRunner__Authentication__AdditionalEntraIssuers__0`). These exact HTTPS
issuer values extend trust; configure them only through trusted deployment policy.
Metadata/signing-key discovery requires access to the relevant Microsoft endpoints.
Customer registration must provide `IAgent` and `IAgentHttpAdapter`.

For container builds, acquire an **exact published package version at build time**
and copy the entire tool payload (`tools/net10.0/any`) into the image. Start its
`Microsoft.Agents.Hosting.AgentRunner.dll` with `dotnet`. Do not copy just the DLL,
download packages at process startup, or depend on a global tool cache.

Local development:

```powershell
dotnet pack src\libraries\Hosting\AgentRunner\Microsoft.Agents.Hosting.AgentRunner.csproj -c Release -o .\artifacts
dotnet test src\tests\Microsoft.Agents.Hosting.AgentRunner.Tests\Microsoft.Agents.Hosting.AgentRunner.Tests.csproj
```

Package publication/signing and Linux container validation must precede deployment.
