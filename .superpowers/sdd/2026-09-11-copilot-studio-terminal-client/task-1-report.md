# Task 1 report: Copilot Studio terminal sample skeleton

## Files changed

- `Directory.Packages.props`
- `src/Microsoft.Agents.SDK.sln`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/CopilotStudioClient.Terminal.csproj`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Program.cs`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Properties/AssemblyInfo.cs`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/TerminalOptions.cs`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalOptionsTests.cs`

## Implementation decisions

- Added `Terminal.Gui` as a centrally managed package at version `2.5.0`.
- Created the new `CopilotStudioClient.Terminal` executable project with the requested hosting/MSAL/Terminal.Gui package references and a project reference to `Microsoft.Agents.CopilotStudio.Client`.
- Added a minimal top-level `Program.cs` so the new `Exe` project builds immediately; it parses command-line options and prints usage when `--help` is supplied.
- Implemented `TerminalOptions`, `TerminalLayout`, and `TerminalOptionException` with strict parsing and the requested usage text.
- Added friend access from the terminal sample assembly to `Microsoft.Agents.CopilotStudio.Terminal.Tests`.
- Added the new test project and the requested option-parsing tests.
- Added the new sample and test projects to `src/Microsoft.Agents.SDK.sln` so the skeleton is part of the solution.
- `TerminalLayout` is `public` instead of `internal` because xUnit theory methods with a public signature cannot compile against an internal theory parameter type.

## Test commands and outcomes

1. `dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalOptionsTests"`
   - Expected failure before implementation: the sample project path did not exist and the option types were missing.

2. `dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalOptionsTests"`
   - Passed after implementation: 5 tests passed, 0 failed, 0 skipped.

## Commit SHA(s)

- `8683e3d7`

## Self-review results

- Confirmed the package version is centrally managed in `Directory.Packages.props`.
- Confirmed the solution contains the new terminal sample and test projects.
- Confirmed the sample assembly exposes internals to the test project only.
- Confirmed the strict parse cases match the brief and the focused test suite passes.
- Confirmed no extra tracked files were introduced beyond the requested skeleton and solution wiring.

## Concerns

- The requested `internal enum TerminalLayout` could not be kept verbatim without breaking the xUnit theory test signature; it is public for now so the test project compiles cleanly.
- `Program.cs` is only a minimal executable stub for Task 1 and will need to be expanded by later tasks.

## Fix round 1/5

### Files changed

- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/TerminalOptions.cs`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalOptionsTests.cs`

### Test command and output

Command:

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalOptionsTests"
```

Output:

```text
Determining projects to restore...
  All projects are up-to-date for restore.
  Microsoft.Agents.Core.Analyzers -> C:\s\gh\an\5\bin\Debug\CplCoreAnalyzers\netstandard2.0\Microsoft.Agents.Core.Analyzers.dll
  Microsoft.Agents.Core -> C:\s\gh\an\5\bin\Debug\CplCore\net10.0\Microsoft.Agents.Core.dll
  Microsoft.Agents.CopilotStudio.Client -> C:\s\gh\an\5\bin\Debug\DirectToEngineClient\net10.0\Microsoft.Agents.CopilotStudio.Client.dll
  CopilotStudioClient.Terminal -> C:\s\gh\an\5\src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\bin\Debug\net10.0\CopilotStudioClient.Terminal.dll
  Microsoft.Agents.CopilotStudio.Terminal.Tests -> C:\s\gh\an\5\bin\Debug\CplTests.CopilotStudioTerminal\net10.0\Microsoft.Agents.CopilotStudio.Terminal.Tests.dll
Test run for C:\s\gh\an\5\bin\Debug\CplTests.CopilotStudioTerminal\net10.0\Microsoft.Agents.CopilotStudio.Terminal.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 141 ms - Microsoft.Agents.CopilotStudio.Terminal.Tests.dll (net10.0)
```

### Self-review

- Restored `TerminalLayout` to `internal` so the sample assembly surface matches the brief.
- Removed the public theory signature that referenced `TerminalLayout` and replaced it with separate fact tests.
- Kept the existing parsing assertions intact, including the help and invalid-layout cases.

### Commit SHA

- `0dfcf56431a8c344df8fa157c92baa59583f7f02`
