# Microsoft 365 Agents SDK — .NET Samples

This folder contains the sample agents and clients for the Microsoft 365 Agents SDK. Each sample has
its own `README.md` with setup and run instructions. This document describes the build conventions
that are **shared across all samples** via the `Directory.Build.props` / `Directory.Build.targets`
files in this folder.

## Shared sample code

Rather than duplicating boilerplate in every project, the samples share a small set of helper files
under [`Shared/`](./Shared). Each shared file is *linked* into a sample by the samples-level
`Directory.Build.targets` (so it shows up in the project as a normal file, but there is only one copy
on disk). Whether a given helper is linked in is controlled by an MSBuild property, letting each
sample opt in or out.

| Shared file | MSBuild property | Default | Purpose |
| ----------- | ---------------- | ------- | ------- |
| `Shared/AspNetExtensions.cs` | `IncludeAspNetSampleHelpers` | `true` | ASP.NET JWT bearer authentication helpers for Azure Bot Service / agent-to-agent requests (`AddAgentAspNetAuthentication`). |
| `Shared/AgentOtelExtension.cs` | `IncludeOtelSampleHelpers` | `false` | OpenTelemetry wiring (`ConfigureOtelProviders`) for tracing, metrics, and logs, exportable to the local .NET Aspire dashboard or Azure Monitor. |

### `IncludeAspNetSampleHelpers`

`Shared/AspNetExtensions.cs` provides the JWT bearer token validation used by the ASP.NET Core–hosted
agent samples. Because almost every sample needs it, it is linked in **by default**
(`IncludeAspNetSampleHelpers` defaults to `true`).

Samples that don't host an ASP.NET pipeline — the console/worker clients under
`CopilotStudioClient/`, and a few web apps that don't use this helper — opt out in their `.csproj`:

```xml
<PropertyGroup>
  <IncludeAspNetSampleHelpers>false</IncludeAspNetSampleHelpers>
</PropertyGroup>
```

A brand-new ASP.NET agent sample needs to do nothing — it gets the helper automatically.

### `IncludeOtelSampleHelpers` (OpenTelemetry extension)

`Shared/AgentOtelExtension.cs` provides `ConfigureOtelProviders`, an `IHostApplicationBuilder`
extension that configures OpenTelemetry tracing, metrics, and logging for the SDK
(`Microsoft.Agents.Core.Telemetry`). It exports over OTLP, which the local
[.NET Aspire dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone?tabs=bash)
can display, and includes commented-out Azure Monitor / console exporters.

Unlike the ASP.NET helper, this file is **opt-in** (`IncludeOtelSampleHelpers` defaults to `false`)
because it depends on the `OpenTelemetry.*` NuGet packages and the SDK telemetry APIs. To use it in a
sample:

1. Set the property in the sample's `.csproj`:

   ```xml
   <PropertyGroup>
     <IncludeOtelSampleHelpers>true</IncludeOtelSampleHelpers>
   </PropertyGroup>
   ```

2. Add the required OpenTelemetry package references (versions are managed centrally in
   `Directory.Packages.props`). See `TelemetryAgent/TelemetryAgent.csproj` for the exact set —
   `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting`, the ASP.NET Core / HTTP / Runtime
   instrumentation packages, and the OTLP (and optionally Console / Azure Monitor) exporters.

3. Call it during host setup (the helper is in the global namespace, like `AspNetExtensions`):

   ```csharp
   var builder = WebApplication.CreateBuilder(args);
   builder.ConfigureOtelProviders();
   ```

`TelemetryAgent` is the reference sample for this helper — see its `README.md` for how to view the
telemetry in the Aspire dashboard.

## Adding a new sample

- Place ASP.NET agent samples so they inherit the shared `Directory.Build.props` /
  `Directory.Build.targets` in this folder (i.e. anywhere under `src/samples/`).
- The ASP.NET auth helper is included automatically; set `IncludeAspNetSampleHelpers` to `false` if
  your sample is a non-web console/worker client.
- Opt into OpenTelemetry with `IncludeOtelSampleHelpers` plus the package references described above.
