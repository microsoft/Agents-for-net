# Agent Runner

The `Microsoft.Agents.Hosting.AgentRunner` .NET tool hosts an Agents SDK
application using shared service registration. Develop and test the application
with its normal entry point; use AgentRunner as the entry point in the container.
The runner requires the .NET 10 **ASP.NET Core runtime**.

Install an exact published version separately from the agent application:

```powershell
dotnet tool install Microsoft.Agents.Hosting.AgentRunner --version PUBLISHED_VERSION --tool-path .\runner
```

Do not add this package as an application `PackageReference`; NuGet rejects a
.NET tool as a regular project dependency. Installing the tool independently
leaves the application's SDK references and normal publish output unchanged.

The application must expose this exact contract in the global namespace:

```csharp
public partial class Program
{
    public static void RegisterServices(WebApplicationBuilder builder)
    {
        builder.AddAgentDefaults().AddAgent<MyAgent>();
        builder.Services.AddSingleton<IStorage, MemoryStorage>();
        // Register the agent's connection/authentication services here as needed.
    }
}
```

Call `Program.RegisterServices(builder)` from the application's existing entry
point after creating its `WebApplicationBuilder`. Keep its normal middleware,
endpoints, and `app.Run()` for local development. Include the application's other
required service registrations in the shared method. AgentRunner invokes this
method instead of executing the application's entry point and supplies its own
HTTP pipeline.

Publish the application with its `.deps.json` and private dependencies:

```powershell
dotnet publish .\MyAgent.csproj -c Release -o .\publish
.\runner\agent-runner --agent-assembly .\publish\MyAgent.dll --port 3978
```

Use the second command as the container's entry point, with paths appropriate
to the image. Install the tool on the container's OS/architecture so the installer
generates the appropriate command launcher. The command starts the managed runner
DLL; a native executable included by a platform build only works on its matching
platform.

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

For container builds, install an **exact published package version at image build
time** with `--tool-path`, not at process startup. Include the application's
publish output and the complete tool installation, including its hidden `.store`
directory, in the image. Do not copy just the launcher or depend on a global tool
cache. The runner's existing loopback-only listener and authentication behavior
are unchanged; retain the deployment's existing proxy/network arrangement.

Local development:

```powershell
dotnet pack src\libraries\Hosting\AgentRunner\Microsoft.Agents.Hosting.AgentRunner.csproj -c Release -o .\artifacts
dotnet test src\tests\Microsoft.Agents.Hosting.AgentRunner.Tests\Microsoft.Agents.Hosting.AgentRunner.Tests.csproj
```

Package publication/signing and Linux container validation must precede deployment.
