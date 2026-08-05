# Task 1 Report: Scaffold the CLI, report model, and test project

## Implementation

Created the ApiCompat tool scaffold and matching test project:

- `src/tools/Microsoft.Agents.ApiCompat/Microsoft.Agents.ApiCompat.csproj`
- `src/tools/Microsoft.Agents.ApiCompat/Program.cs`
- `src/tools/Microsoft.Agents.ApiCompat/Models.cs`
- `src/tests/Microsoft.Agents.ApiCompat.Tests/Microsoft.Agents.ApiCompat.Tests.csproj`
- `src/tests/Microsoft.Agents.ApiCompat.Tests/DiagnosticClassifierTests.cs`
- `.config/dotnet-tools.json`
- `src/Microsoft.Agents.SDK.sln`

Notes:

- The report model matches the brief verbatim.
- `Program.cs` is a minimal executable entry point so the CLI project builds.
- The test project uses `TargetFrameworks>net8.0</TargetFrameworks>` because `src/tests/Directory.Build.props` applies a default `net8.0;net4.8` multi-targeting rule to test projects.

## RED evidence

Command:

```powershell
dotnet test C:\code\Agents-for-net\src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj
```

Result:

- `MSB1009: Project file does not exist.` before scaffolding.

Command:

```powershell
dotnet test C:\code\Agents-for-net\src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter DiagnosticClassifierTests
```

Result:

- `CS0103: The name 'DiagnosticClassifier' does not exist in the current context`

## GREEN evidence

Command:

```powershell
dotnet build C:\code\Agents-for-net\src\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj
```

Result:

- Build succeeded with 0 warnings and 0 errors.

Command:

```powershell
dotnet sln C:\code\Agents-for-net\src\Microsoft.Agents.SDK.sln add C:\code\Agents-for-net\src\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj C:\code\Agents-for-net\src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj
dotnet tool restore
```

Result:

- Both projects were added to the solution.
- `microsoft.dotnet.apicompat.tool` version `8.0.423` restored successfully.

## Self-review

- Verified the new CLI project builds cleanly.
- Verified the tool manifest is pinned to the requested ApiCompat version.
- Verified the solution contains both new projects.
- Verified the focused test still fails for the intended missing classifier.

## Concerns

- The diagnostic classifier is intentionally not implemented in this scaffold task, so the focused test remains red by design.
