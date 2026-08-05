# PR Breaking-Change Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a required PR check that compares packable SDK libraries with their applicable stable NuGet releases, categorizes confirmed breaks as source, binary, or both, and permits an explicit label-plus-justification override.

**Architecture:** A BCL-only `net8.0` CLI performs discovery, baseline resolution, ApiCompat execution, classification, policy evaluation, and rendering. A local composite action invokes the trusted base-branch CLI against separately checked-out candidate code; a read-only `pull_request_target` workflow enforces the result, while a separate trusted `workflow_run` workflow safely updates one sticky PR comment for same-repository and fork PRs.

**Tech Stack:** .NET 8, xUnit, `Microsoft.DotNet.ApiCompat.Tool` 8.0.423, GitHub composite actions, GitHub Actions YAML, `actions/github-script`.

## Global Constraints

- Compare only packable projects under `src/libraries/`.
- For `main`, select the latest stable NuGet version overall.
- For `rel/vX.Y`, select the latest stable NuGet version in the `X.Y.*` line.
- Never select prerelease packages as baselines.
- Confirmed findings must be categorized as `Source`, `Binary`, or `SourceAndBinary`.
- Potential consumer-dependent source breaks are warnings and do not fail the check.
- Behavioral compatibility is outside scope; suppress ApiCompat rules that are not source or binary compatibility checks.
- A valid override requires both the `breaking-change-approved` label and a non-empty `## Breaking change justification` PR section.
- Missing stable packages are informational; network, tool, parser, pack, and unknown-diagnostic failures block.
- Detection runs with read-only permissions, no secrets, no persisted credentials, and no writable candidate cache.
- Reporter artifacts are untrusted: validate size/schema, escape Markdown/HTML/mentions, and never execute or post artifact Markdown directly.
- Use `System.Text.Json`; do not add Newtonsoft.Json.
- Add package versions only through Central Package Management; this implementation should require no new library package references.
- Treat warnings as errors and keep the tool/test projects `net8.0`.

## File Map

### CLI

- Create `src/tools/Microsoft.Agents.ApiCompat/Microsoft.Agents.ApiCompat.csproj` — executable project.
- Create `src/tools/Microsoft.Agents.ApiCompat/Program.cs` — command dispatch for `analyze` and `render-comment`.
- Create `src/tools/Microsoft.Agents.ApiCompat/Models.cs` — immutable report, package, finding, and policy records/enums.
- Create `src/tools/Microsoft.Agents.ApiCompat/PullRequestEvent.cs` — parse trusted fields from the GitHub event JSON.
- Create `src/tools/Microsoft.Agents.ApiCompat/ProjectDiscovery.cs` — discover packable project/package IDs.
- Create `src/tools/Microsoft.Agents.ApiCompat/NuGetBaselineResolver.cs` — query stable versions and download baselines.
- Create `src/tools/Microsoft.Agents.ApiCompat/ApiCompatRunner.cs` — invoke pinned local tool and capture output.
- Create `src/tools/Microsoft.Agents.ApiCompat/ApiCompatParser.cs` — parse diagnostics and comparison direction.
- Create `src/tools/Microsoft.Agents.ApiCompat/DiagnosticClassifier.cs` — exhaustive supported diagnostic policy.
- Create `src/tools/Microsoft.Agents.ApiCompat/OverridePolicy.cs` — label and PR-body justification validation.
- Create `src/tools/Microsoft.Agents.ApiCompat/ReportWriter.cs` — JSON, Markdown, job summary, annotations, and sanitized sticky-comment rendering.
- Create `src/tools/Microsoft.Agents.ApiCompat/CompatibilityAnalyzer.cs` — orchestrate per-package analysis and fail-closed results.

### Tests

- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/Microsoft.Agents.ApiCompat.Tests.csproj`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/BaselineResolverTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/ProjectDiscoveryTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/ApiCompatParserTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/DiagnosticClassifierTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/OverridePolicyTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/ReportWriterTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/CompatibilityAnalyzerIntegrationTests.cs`.
- Create `src/tests/Microsoft.Agents.ApiCompat.Tests/TestPackageBuilder.cs`.

### Repository and GitHub Integration

- Modify `.config/dotnet-tools.json` — pin `Microsoft.DotNet.ApiCompat.Tool` 8.0.423.
- Modify `src/Microsoft.Agents.SDK.sln` — include CLI and test projects.
- Create `.github/actions/detect-breaking-changes/action.yml`.
- Create `.github/workflows/api-compat.yml`.
- Create `.github/workflows/api-compat-report.yml`.
- Create `.github/pull_request_template.md`.
- Modify `.github/CODEOWNERS` — protect the workflow, action, and compatibility policy.

---

### Task 1: Scaffold the CLI, report model, and test project

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/Microsoft.Agents.ApiCompat.csproj`
- Create: `src/tools/Microsoft.Agents.ApiCompat/Models.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/Microsoft.Agents.ApiCompat.Tests.csproj`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/DiagnosticClassifierTests.cs`
- Modify: `.config/dotnet-tools.json`
- Modify: `src/Microsoft.Agents.SDK.sln`

**Interfaces:**
- Produces: `CompatibilityCategory`, `FindingSeverity`, `AnalysisDecision`, `CompatibilityFinding`, `PackageCompatibilityReport`, `OverrideResult`, and `CompatibilityReport`.
- Produces: local command `dotnet tool run apicompat` at version 8.0.423.

- [ ] **Step 1: Write the first failing model/classifier test**

```csharp
using Microsoft.Agents.ApiCompat;

namespace Microsoft.Agents.ApiCompat.Tests;

public class DiagnosticClassifierTests
{
    [Fact]
    public void Classify_ParameterRename_IsSourceOnly()
    {
        var result = DiagnosticClassifier.Classify("CP0017", ApiDifferenceDirection.BaselineToCandidate);

        Assert.Equal(CompatibilityCategory.Source, result.Category);
        Assert.Equal(FindingSeverity.Blocking, result.Severity);
    }
}
```

- [ ] **Step 2: Run the test to verify the projects do not exist**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj
```

Expected: FAIL because the test project has not been created.

- [ ] **Step 3: Create the executable and test projects**

`src/tools/Microsoft.Agents.ApiCompat/Microsoft.Agents.ApiCompat.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

`src/tests/Microsoft.Agents.ApiCompat.Tests/Microsoft.Agents.ApiCompat.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add the immutable report model**

`src/tools/Microsoft.Agents.ApiCompat/Models.cs`:

```csharp
namespace Microsoft.Agents.ApiCompat;

public enum CompatibilityCategory { Source, Binary, SourceAndBinary, PotentialSourceRisk, Infrastructure }
public enum FindingSeverity { Blocking, Warning, Informational }
public enum AnalysisDecision { Pass, Block, Overridden, InfrastructureFailure }
public enum ApiDifferenceDirection { BaselineToCandidate, CandidateAddition }

public sealed record Classification(CompatibilityCategory Category, FindingSeverity Severity);

public sealed record CompatibilityFinding(
    string PackageId,
    string BaselineVersion,
    string CandidateVersion,
    string? TargetFramework,
    string DiagnosticId,
    string Target,
    string Detail,
    CompatibilityCategory Category,
    FindingSeverity Severity);

public sealed record PackageCompatibilityReport(
    string PackageId,
    string CandidateVersion,
    string? BaselineVersion,
    string Status,
    IReadOnlyList<CompatibilityFinding> Findings);

public sealed record OverrideResult(bool IsValid, string? Justification, string Reason);

public sealed record CompatibilityReport(
    int SchemaVersion,
    long RunId,
    int PullRequestNumber,
    string BaseRef,
    AnalysisDecision Decision,
    OverrideResult Override,
    IReadOnlyList<PackageCompatibilityReport> Packages,
    IReadOnlyList<string> InfrastructureErrors);
```

- [ ] **Step 5: Pin ApiCompat and add both projects to the solution**

Add to `.config/dotnet-tools.json`:

```json
"microsoft.dotnet.apicompat.tool": {
  "version": "8.0.423",
  "commands": [ "apicompat" ],
  "rollForward": false
}
```

Run:

```powershell
dotnet sln src\Microsoft.Agents.SDK.sln add src\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj
dotnet tool restore
```

Expected: both projects are added and ApiCompat 8.0.423 restores.

- [ ] **Step 6: Run the focused test and confirm the expected missing classifier failure**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter DiagnosticClassifierTests
```

Expected: FAIL because `DiagnosticClassifier` is not defined.

- [ ] **Step 7: Commit the scaffold**

```powershell
git add .config\dotnet-tools.json src\Microsoft.Agents.SDK.sln src\tools\Microsoft.Agents.ApiCompat src\tests\Microsoft.Agents.ApiCompat.Tests
git commit -m "build: scaffold API compatibility tool" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 2: Implement exhaustive diagnostic classification

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/DiagnosticClassifier.cs`
- Modify: `src/tests/Microsoft.Agents.ApiCompat.Tests/DiagnosticClassifierTests.cs`

**Interfaces:**
- Consumes: `ApiDifferenceDirection`.
- Produces: `DiagnosticClassifier.Classify(string diagnosticId, ApiDifferenceDirection direction)`.
- Produces: `DiagnosticClassifier.ApiCompatNoWarn`, exactly `CP0011;CP0013`.

- [ ] **Step 1: Expand the failing classifier theory**

```csharp
[Theory]
[InlineData("CP0017", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Source, FindingSeverity.Blocking)]
[InlineData("CP0003", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("CP0010", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("PKV002", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("PKV003", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("PKV004", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("PKV005", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("PKV007", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("CP0001", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0002", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0004", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0005", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0006", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0007", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0008", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0009", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0012", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0018", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0019", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("PKV001", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("PKV006", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
[InlineData("CP0001", ApiDifferenceDirection.CandidateAddition, CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning)]
[InlineData("CP0002", ApiDifferenceDirection.CandidateAddition, CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning)]
[InlineData("CP0020", ApiDifferenceDirection.CandidateAddition, CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning)]
public void Classify_KnownDiagnostic_ReturnsPolicy(
    string id,
    ApiDifferenceDirection direction,
    CompatibilityCategory category,
    FindingSeverity severity)
{
    Assert.Equal(new Classification(category, severity), DiagnosticClassifier.Classify(id, direction));
}

[Fact]
public void Classify_UnknownDiagnostic_Throws()
{
    Assert.Throws<InvalidDataException>(
        () => DiagnosticClassifier.Classify("CP9999", ApiDifferenceDirection.BaselineToCandidate));
}
```

- [ ] **Step 2: Run the classifier tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter DiagnosticClassifierTests
```

Expected: FAIL because the classifier is missing.

- [ ] **Step 3: Implement the exhaustive mapping**

```csharp
namespace Microsoft.Agents.ApiCompat;

public static class DiagnosticClassifier
{
    public const string ApiCompatNoWarn = "CP0011;CP0013";

    private static readonly IReadOnlyDictionary<string, CompatibilityCategory> BaselineCategories =
        new Dictionary<string, CompatibilityCategory>(StringComparer.Ordinal)
        {
            ["CP0001"] = CompatibilityCategory.SourceAndBinary,
            ["CP0002"] = CompatibilityCategory.SourceAndBinary,
            ["CP0003"] = CompatibilityCategory.Binary,
            ["CP0004"] = CompatibilityCategory.SourceAndBinary,
            ["CP0005"] = CompatibilityCategory.SourceAndBinary,
            ["CP0006"] = CompatibilityCategory.SourceAndBinary,
            ["CP0007"] = CompatibilityCategory.SourceAndBinary,
            ["CP0008"] = CompatibilityCategory.SourceAndBinary,
            ["CP0009"] = CompatibilityCategory.SourceAndBinary,
            ["CP0010"] = CompatibilityCategory.Binary,
            ["CP0012"] = CompatibilityCategory.SourceAndBinary,
            ["CP0017"] = CompatibilityCategory.Source,
            ["CP0018"] = CompatibilityCategory.SourceAndBinary,
            ["CP0019"] = CompatibilityCategory.SourceAndBinary,
            ["PKV001"] = CompatibilityCategory.SourceAndBinary,
            ["PKV002"] = CompatibilityCategory.Binary,
            ["PKV003"] = CompatibilityCategory.Binary,
            ["PKV004"] = CompatibilityCategory.Binary,
            ["PKV005"] = CompatibilityCategory.Binary,
            ["PKV006"] = CompatibilityCategory.SourceAndBinary,
            ["PKV007"] = CompatibilityCategory.Binary,
        };

    public static Classification Classify(string diagnosticId, ApiDifferenceDirection direction)
    {
        if (direction == ApiDifferenceDirection.CandidateAddition &&
            diagnosticId is "CP0001" or "CP0002" or "CP0020")
        {
            return new(CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning);
        }

        if (!BaselineCategories.TryGetValue(diagnosticId, out var category))
        {
            throw new InvalidDataException($"Unsupported ApiCompat diagnostic '{diagnosticId}'.");
        }

        return new(category, FindingSeverity.Blocking);
    }
}
```

- [ ] **Step 4: Run classifier tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter DiagnosticClassifierTests
```

Expected: PASS.

- [ ] **Step 5: Commit the classification policy**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\DiagnosticClassifier.cs src\tests\Microsoft.Agents.ApiCompat.Tests\DiagnosticClassifierTests.cs
git commit -m "feat: classify API compatibility diagnostics" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 3: Implement PR event parsing and override policy

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/PullRequestEvent.cs`
- Create: `src/tools/Microsoft.Agents.ApiCompat/OverridePolicy.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/OverridePolicyTests.cs`

**Interfaces:**
- Produces: `PullRequestEvent.Load(string path)`.
- Produces: `OverridePolicy.Evaluate(IReadOnlyCollection<string> labels, string? body)`.

- [ ] **Step 1: Write failing override tests**

```csharp
namespace Microsoft.Agents.ApiCompat.Tests;

public class OverridePolicyTests
{
    [Theory]
    [InlineData(false, "## Breaking change justification\nIntentional removal.", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "## Breaking change justification\n<!-- explain -->", false)]
    [InlineData(true, "## Breaking change justification\nIntentional removal.\n## Testing\nDone.", true)]
    public void Evaluate_RequiresLabelAndVisibleJustification(bool hasLabel, string? body, bool expected)
    {
        var labels = hasLabel ? new[] { OverridePolicy.ApprovalLabel } : Array.Empty<string>();
        Assert.Equal(expected, OverridePolicy.Evaluate(labels, body).IsValid);
    }
}
```

- [ ] **Step 2: Run the override tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter OverridePolicyTests
```

Expected: FAIL because `OverridePolicy` is missing.

- [ ] **Step 3: Implement event parsing**

```csharp
using System.Text.Json;

namespace Microsoft.Agents.ApiCompat;

public sealed record PullRequestEvent(
    long RunId,
    int Number,
    string BaseRef,
    string Body,
    IReadOnlyList<string> Labels)
{
    public static PullRequestEvent Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var pullRequest = root.GetProperty("pull_request");
        var labels = pullRequest.GetProperty("labels")
            .EnumerateArray()
            .Select(label => label.GetProperty("name").GetString() ?? string.Empty)
            .Where(name => name.Length > 0)
            .ToArray();

        var runIdText = Environment.GetEnvironmentVariable("GITHUB_RUN_ID")
            ?? throw new InvalidDataException("GITHUB_RUN_ID is required.");

        return new(
            long.Parse(runIdText, System.Globalization.CultureInfo.InvariantCulture),
            pullRequest.GetProperty("number").GetInt32(),
            pullRequest.GetProperty("base").GetProperty("ref").GetString()
                ?? throw new InvalidDataException("PR base ref is missing."),
            pullRequest.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty,
            labels);
    }
}
```

- [ ] **Step 4: Implement the override parser**

```csharp
using System.Text.RegularExpressions;

namespace Microsoft.Agents.ApiCompat;

public static partial class OverridePolicy
{
    public const string ApprovalLabel = "breaking-change-approved";
    private const string Heading = "Breaking change justification";

    public static OverrideResult Evaluate(IReadOnlyCollection<string> labels, string? body)
    {
        var hasLabel = labels.Contains(ApprovalLabel, StringComparer.OrdinalIgnoreCase);
        var justification = ExtractJustification(body ?? string.Empty);

        if (!hasLabel)
        {
            return new(false, justification, $"Missing '{ApprovalLabel}' label.");
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            return new(false, null, $"Missing non-empty '## {Heading}' section.");
        }

        return new(true, justification, "Approved label and justification are present.");
    }

    private static string? ExtractJustification(string body)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line =>
            string.Equals(line.Trim(), $"## {Heading}", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return null;
        }

        var content = lines.Skip(start + 1)
            .TakeWhile(line => !HeadingRegex().IsMatch(line))
            .ToArray();
        var visible = HtmlCommentRegex().Replace(string.Join("\n", content), string.Empty).Trim();
        return visible.Length == 0 ? null : visible;
    }

    [GeneratedRegex(@"^#{1,2}\s+", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentRegex();
}
```

- [ ] **Step 5: Run override tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter OverridePolicyTests
```

Expected: PASS.

- [ ] **Step 6: Commit PR policy parsing**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\PullRequestEvent.cs src\tools\Microsoft.Agents.ApiCompat\OverridePolicy.cs src\tests\Microsoft.Agents.ApiCompat.Tests\OverridePolicyTests.cs
git commit -m "feat: validate breaking change overrides" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 4: Implement project discovery and stable NuGet baseline resolution

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/ProjectDiscovery.cs`
- Create: `src/tools/Microsoft.Agents.ApiCompat/NuGetBaselineResolver.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/ProjectDiscoveryTests.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/BaselineResolverTests.cs`

**Interfaces:**
- Produces: `PackageProject(string ProjectPath, string PackageId)`.
- Produces: `ProjectDiscovery.Discover(string repositoryRoot)`.
- Produces: `NuGetBaselineResolver.GetBaselineVersionAsync(string packageId, string baseRef, CancellationToken)`.
- Produces: `NuGetBaselineResolver.DownloadAsync(string packageId, string version, string destination, CancellationToken)`.

- [ ] **Step 1: Write failing discovery and version-selection tests**

```csharp
[Fact]
public void SelectBaseline_Main_ReturnsLatestStable()
{
    var versions = new[] { "1.2.0", "2.0.0-beta.1", "1.9.0", "2.0.0" };
    Assert.Equal("2.0.0", NuGetBaselineResolver.SelectBaseline("main", versions));
}

[Fact]
public void SelectBaseline_ReleaseBranch_StaysInMajorMinorLine()
{
    var versions = new[] { "1.2.0", "1.2.5", "1.3.0", "1.2.6-beta.1" };
    Assert.Equal("1.2.5", NuGetBaselineResolver.SelectBaseline("rel/v1.2", versions));
}

[Fact]
public void Discover_UsesPackageIdAndSkipsExplicitlyNonPackableProject()
{
    using var fixture = RepositoryFixture.Create(
        ("src/libraries/A/A.csproj", "<Project><PropertyGroup><PackageId>Contoso.A</PackageId></PropertyGroup></Project>"),
        ("src/libraries/B/B.csproj", "<Project><PropertyGroup><IsPackable>false</IsPackable></PropertyGroup></Project>"));

    var result = ProjectDiscovery.Discover(fixture.Root);
    Assert.Collection(result, package => Assert.Equal("Contoso.A", package.PackageId));
}
```

- [ ] **Step 2: Run the focused tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter "BaselineResolverTests|ProjectDiscoveryTests"
```

Expected: FAIL because discovery/resolution types are missing.

- [ ] **Step 3: Implement project discovery with XML evaluation limited to required properties**

```csharp
using System.Xml.Linq;

namespace Microsoft.Agents.ApiCompat;

public sealed record PackageProject(string ProjectPath, string PackageId);

public static class ProjectDiscovery
{
    public static IReadOnlyList<PackageProject> Discover(string repositoryRoot)
    {
        var libraryRoot = Path.Combine(repositoryRoot, "src", "libraries");
        return Directory.EnumerateFiles(libraryRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(ReadProject)
            .Where(project => project is not null)
            .Cast<PackageProject>()
            .OrderBy(project => project.PackageId, StringComparer.Ordinal)
            .ToArray();
    }

    private static PackageProject? ReadProject(string path)
    {
        var document = XDocument.Load(path);
        var properties = document.Descendants()
            .Where(element => element.Name.LocalName is "PackageId" or "IsPackable")
            .GroupBy(element => element.Name.LocalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim(), StringComparer.OrdinalIgnoreCase);

        if (properties.TryGetValue("IsPackable", out var isPackable) &&
            string.Equals(isPackable, "false", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var packageId = properties.TryGetValue("PackageId", out var configured)
            ? configured
            : Path.GetFileNameWithoutExtension(path);
        return new(path, packageId);
    }
}
```

- [ ] **Step 4: Implement NuGet flat-container resolution**

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.ApiCompat;

public sealed partial class NuGetBaselineResolver(HttpClient httpClient)
{
    public async Task<string?> GetBaselineVersionAsync(
        string packageId,
        string baseRef,
        CancellationToken cancellationToken)
    {
        var id = packageId.ToLowerInvariant();
        using var response = await httpClient.GetAsync(
            $"https://api.nuget.org/v3-flatcontainer/{id}/index.json",
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var versions = document.RootElement.GetProperty("versions")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty);
        return SelectBaseline(baseRef, versions);
    }

    public async Task DownloadAsync(
        string packageId,
        string version,
        string destination,
        CancellationToken cancellationToken)
    {
        var id = packageId.ToLowerInvariant();
        var normalizedVersion = version.ToLowerInvariant();
        using var response = await httpClient.GetAsync(
            $"https://api.nuget.org/v3-flatcontainer/{id}/{normalizedVersion}/{id}.{normalizedVersion}.nupkg",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var output = File.Create(destination);
        await response.Content.CopyToAsync(output, cancellationToken);
    }

    public static string? SelectBaseline(string baseRef, IEnumerable<string> versions)
    {
        var stable = versions
            .Where(version => !version.Contains('-', StringComparison.Ordinal))
            .Select(version => (Text: version, Value: Version.Parse(version)))
            .ToArray();

        var match = ReleaseBranchRegex().Match(baseRef);
        if (match.Success)
        {
            var major = int.Parse(match.Groups["major"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var minor = int.Parse(match.Groups["minor"].Value, System.Globalization.CultureInfo.InvariantCulture);
            stable = stable.Where(version => version.Value.Major == major && version.Value.Minor == minor).ToArray();
        }

        return stable.OrderByDescending(version => version.Value).Select(version => version.Text).FirstOrDefault();
    }

    [GeneratedRegex(@"^rel/v(?<major>\d+)\.(?<minor>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseBranchRegex();
}
```

- [ ] **Step 5: Run discovery and baseline tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter "BaselineResolverTests|ProjectDiscoveryTests"
```

Expected: PASS, including mocked `HttpMessageHandler` tests for 404 versus non-success feed responses.

- [ ] **Step 6: Commit discovery and baseline resolution**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\ProjectDiscovery.cs src\tools\Microsoft.Agents.ApiCompat\NuGetBaselineResolver.cs src\tests\Microsoft.Agents.ApiCompat.Tests\ProjectDiscoveryTests.cs src\tests\Microsoft.Agents.ApiCompat.Tests\BaselineResolverTests.cs
git commit -m "feat: resolve API compatibility baselines" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 5: Characterize, invoke, and parse pinned ApiCompat output

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/ApiCompatRunner.cs`
- Create: `src/tools/Microsoft.Agents.ApiCompat/ApiCompatParser.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/TestPackageBuilder.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/ApiCompatParserTests.cs`

**Interfaces:**
- Produces: `ApiCompatExecution(int ExitCode, string StandardOutput, string StandardError)`.
- Produces: `ApiCompatRunner.RunAsync(string candidatePackage, string baselinePackage, bool strict, CancellationToken)`.
- Produces: `ParsedDiagnostic(string Id, string Target, string Detail, string? TargetFramework, ApiDifferenceDirection Direction)`.
- Produces: `ApiCompatParser.Parse(ApiCompatExecution execution, bool strict)`.

- [ ] **Step 1: Add fixture package builder and characterization tests**

Use a test helper that writes this minimal SDK project and configurable `Api.cs`, then executes
`dotnet pack -c Release -o <temp>`:

```csharp
public static async Task<string> BuildAsync(string packageId, string version, string source)
{
    var root = Path.Combine(Path.GetTempPath(), "agents-apicompat-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    await File.WriteAllTextAsync(Path.Combine(root, "Fixture.csproj"), $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net8.0</TargetFramework>
            <PackageId>{packageId}</PackageId>
            <Version>{version}</Version>
          </PropertyGroup>
        </Project>
        """);
    await File.WriteAllTextAsync(Path.Combine(root, "Api.cs"), source);
    await ProcessAssert.SuccessAsync("dotnet", $"pack \"{Path.Combine(root, "Fixture.csproj")}\" -c Release -o \"{root}\"");
    return Path.Combine(root, $"{packageId}.{version}.nupkg");
}
```

Add tests that create:

```csharp
const string Baseline = "namespace Fixture; public class Api { public void Removed() { } public void Named(int value) { } }";
const string Candidate = "namespace Fixture; public class Api { public void Named(int renamed) { } public void Added(string value) { } }";
```

Assert the normal run yields CP0002 and CP0017 and the strict run identifies `Added(string)` as a candidate addition.

- [ ] **Step 2: Run characterization tests and capture the exact 8.0.423 output**

Run:

```powershell
dotnet tool restore
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter ApiCompatParserTests --logger "console;verbosity=detailed"
```

Expected: FAIL with captured output showing the exact diagnostic line shapes. Update only the test input/expected literal if ApiCompat's documented 8.0.423 wording differs; do not weaken matching to accept arbitrary text.

- [ ] **Step 3: Implement process invocation**

```csharp
using System.Diagnostics;

namespace Microsoft.Agents.ApiCompat;

public sealed record ApiCompatExecution(int ExitCode, string StandardOutput, string StandardError);

public static class ApiCompatRunner
{
    public static async Task<ApiCompatExecution> RunAsync(
        string candidatePackage,
        string baselinePackage,
        bool strict,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "tool", "run", "apicompat", "package", candidatePackage,
            "--baseline-package", baselinePackage,
            "--enable-rule-cannot-change-parameter-name",
            "--noWarn", DiagnosticClassifier.ApiCompatNoWarn,
            "--verbosity", "normal",
        };
        if (strict)
        {
            arguments.Add("--enable-strict-mode-for-baseline-validation");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start ApiCompat.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new(process.ExitCode, await standardOutput, await standardError);
    }
}
```

- [ ] **Step 4: Implement strict parsing with fail-closed unknown output**

Implement anchored generated regular expressions for the exact characterized lines:

```csharp
public static IReadOnlyList<ParsedDiagnostic> Parse(ApiCompatExecution execution, bool strict)
{
    var diagnostics = new List<ParsedDiagnostic>();
    foreach (var line in EnumerateLines(execution.StandardOutput, execution.StandardError))
    {
        var match = DiagnosticRegex().Match(line);
        if (!match.Success)
        {
            continue;
        }

        var id = match.Groups["id"].Value;
        var detail = match.Groups["detail"].Value.Trim();
        var direction = strict && CandidateOnlyRegex().IsMatch(detail)
            ? ApiDifferenceDirection.CandidateAddition
            : ApiDifferenceDirection.BaselineToCandidate;
        var target = TargetRegex().Match(detail).Groups["target"].Value;
        diagnostics.Add(new(id, target, detail, ExtractTargetFramework(detail), direction));
    }

    if (execution.ExitCode != 0 && diagnostics.Count == 0)
    {
        throw new InvalidDataException(
            $"ApiCompat exited with {execution.ExitCode} without parseable diagnostics: {execution.StandardError}");
    }

    return diagnostics;
}
```

Ensure normal and strict results are deduplicated by `(Id, Target, Direction, TargetFramework)`.

- [ ] **Step 5: Run parser and fixture tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter ApiCompatParserTests
```

Expected: PASS for removal, parameter rename, addition direction, no-break exit zero, and unparseable nonzero output.

- [ ] **Step 6: Commit ApiCompat execution and parsing**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\ApiCompatRunner.cs src\tools\Microsoft.Agents.ApiCompat\ApiCompatParser.cs src\tests\Microsoft.Agents.ApiCompat.Tests\TestPackageBuilder.cs src\tests\Microsoft.Agents.ApiCompat.Tests\ApiCompatParserTests.cs
git commit -m "feat: run and parse pinned ApiCompat" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 6: Implement analysis orchestration and report rendering

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/CompatibilityAnalyzer.cs`
- Create: `src/tools/Microsoft.Agents.ApiCompat/ReportWriter.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/ReportWriterTests.cs`
- Create: `src/tests/Microsoft.Agents.ApiCompat.Tests/CompatibilityAnalyzerIntegrationTests.cs`

**Interfaces:**
- Produces: `CompatibilityAnalyzer.AnalyzeAsync(AnalysisOptions, CancellationToken)`.
- Produces: `ReportWriter.WriteAsync(CompatibilityReport, string outputDirectory, CancellationToken)`.
- Produces: `ReportWriter.RenderStickyComment(CompatibilityReport)`.

- [ ] **Step 1: Write failing decision and rendering tests**

Cover these exact cases:

```csharp
[Theory]
[InlineData(0, false, AnalysisDecision.Pass)]
[InlineData(1, false, AnalysisDecision.Block)]
[InlineData(1, true, AnalysisDecision.Overridden)]
public void Decide_UsesBlockingFindingsAndOverride(int blockingCount, bool overrideValid, AnalysisDecision expected)
{
    Assert.Equal(expected, CompatibilityAnalyzer.Decide(blockingCount, overrideValid, Array.Empty<string>()));
}

[Fact]
public void RenderStickyComment_EscapesMentionsAndHtml()
{
    var report = ReportFixture.WithDetail("@team <script>|value");
    var comment = ReportWriter.RenderStickyComment(report);
    Assert.DoesNotContain("@team", comment, StringComparison.Ordinal);
    Assert.DoesNotContain("<script>", comment, StringComparison.Ordinal);
    Assert.Contains("&#64;team", comment, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run report/analyzer tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter "ReportWriterTests|CompatibilityAnalyzerIntegrationTests"
```

Expected: FAIL because analyzer and writer are missing.

- [ ] **Step 3: Implement package matching and fail-closed orchestration**

`AnalysisOptions` must contain repository root, candidate package directory, output directory,
event data, and cancellation token. For each discovered `PackageProject`:

```csharp
var matches = Directory.GetFiles(options.CandidatePackageDirectory, $"{project.PackageId}.*.nupkg");
if (matches.Length != 1)
{
    infrastructureErrors.Add(
        $"Expected one candidate package for '{project.PackageId}', found {matches.Length}.");
    continue;
}

var baselineVersion = await resolver.GetBaselineVersionAsync(project.PackageId, options.Event.BaseRef, cancellationToken);
if (baselineVersion is null)
{
    packages.Add(new(project.PackageId, ReadPackageVersion(matches[0]), null, "NoBaseline", []));
    continue;
}
```

Download the baseline, run normal and strict comparisons, classify all parsed diagnostics,
deduplicate them, and catch exceptions per package into `InfrastructureErrors`. Do not return
an empty-success report after any exception.

Decision logic:

```csharp
public static AnalysisDecision Decide(int blockingCount, bool overrideValid, IReadOnlyCollection<string> errors)
{
    if (errors.Count > 0) return AnalysisDecision.InfrastructureFailure;
    if (blockingCount == 0) return AnalysisDecision.Pass;
    return overrideValid ? AnalysisDecision.Overridden : AnalysisDecision.Block;
}
```

- [ ] **Step 4: Implement JSON, Markdown, summary, annotation, and comment rendering**

Write:

- `report.json` with `SchemaVersion = 1`.
- `report.md` grouped by package/category.
- `summary.md` with counts and decision.
- `annotations.txt` containing escaped GitHub workflow commands.
- `comment.md` only from `RenderStickyComment`.

Use:

```csharp
private static string Escape(string value) => value
    .Replace("&", "&amp;", StringComparison.Ordinal)
    .Replace("<", "&lt;", StringComparison.Ordinal)
    .Replace(">", "&gt;", StringComparison.Ordinal)
    .Replace("@", "&#64;", StringComparison.Ordinal)
    .Replace("|", "&#124;", StringComparison.Ordinal);
```

Limit every detail/target field to 2,000 characters and the rendered comment to 60,000
characters. End truncated content with `... (truncated)`.

- [ ] **Step 5: Run unit and integration tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter "ReportWriterTests|CompatibilityAnalyzerIntegrationTests"
```

Expected: PASS for no break, source-only rename, both-category removal, warning-only addition,
valid override, invalid override, no baseline, mixed packages, and infrastructure failure.

- [ ] **Step 6: Commit analyzer and reports**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\CompatibilityAnalyzer.cs src\tools\Microsoft.Agents.ApiCompat\ReportWriter.cs src\tests\Microsoft.Agents.ApiCompat.Tests\ReportWriterTests.cs src\tests\Microsoft.Agents.ApiCompat.Tests\CompatibilityAnalyzerIntegrationTests.cs
git commit -m "feat: analyze and report package compatibility" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 7: Add CLI commands and the composite action

**Files:**
- Create: `src/tools/Microsoft.Agents.ApiCompat/Program.cs`
- Create: `.github/actions/detect-breaking-changes/action.yml`
- Modify: `src/tests/Microsoft.Agents.ApiCompat.Tests/ReportWriterTests.cs`

**Interfaces:**
- Produces command:
  `analyze --repo-root <path> --packages <path> --event <path> --output <path> [--candidate-error-file <path>]`.
- Produces command:
  `render-comment --report <path> --output <path> --run-id <id> --pr-number <number>`.
- Composite action outputs: `decision`, `blocking-count`, `warning-count`, `report-directory`.

- [ ] **Step 1: Write failing command validation tests**

Test that missing required options return exit code 2 and that `render-comment` rejects:

- Files larger than 5 MB.
- `SchemaVersion != 1`.
- Report `RunId` or `PullRequestNumber` differing from command arguments.

- [ ] **Step 2: Implement explicit command parsing without a new dependency**

```csharp
return args.FirstOrDefault() switch
{
    "analyze" => await AnalyzeAsync(ParseOptions(args[1..])),
    "render-comment" => await RenderCommentAsync(ParseOptions(args[1..])),
    _ => Usage(),
};
```

`analyze` always writes a report when analysis reaches the orchestrator. When
`--candidate-error-file` names a non-empty file, it skips package analysis and writes an
`InfrastructureFailure` report containing the sanitized restore/pack failure. It returns zero for
`Pass`, `Block`, `Overridden`, and represented `InfrastructureFailure`; it returns nonzero
only when no valid report can be written. Write these lines to `$GITHUB_OUTPUT` when set:

```text
decision=<Pass|Block|Overridden|InfrastructureFailure>
blocking-count=<integer>
warning-count=<integer>
report-directory=<absolute path>
```

- [ ] **Step 3: Create the composite action**

`.github/actions/detect-breaking-changes/action.yml`:

```yaml
name: Detect .NET API breaking changes
description: Compare candidate SDK packages with stable NuGet baselines
inputs:
  trusted-root:
    required: true
  candidate-root:
    required: true
  event-path:
    required: true
  output-directory:
    required: true
outputs:
  decision:
    value: ${{ steps.analyze.outputs.decision }}
  blocking-count:
    value: ${{ steps.analyze.outputs.blocking-count }}
  warning-count:
    value: ${{ steps.analyze.outputs.warning-count }}
  report-directory:
    value: ${{ steps.analyze.outputs.report-directory }}
runs:
  using: composite
  steps:
    - name: Restore compatibility tool
      shell: pwsh
      working-directory: ${{ inputs.trusted-root }}
      run: dotnet tool restore
    - name: Build compatibility CLI
      shell: pwsh
      working-directory: ${{ inputs.trusted-root }}
      run: dotnet build src\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj -c Release
    - id: candidate
      name: Restore and pack candidate
      shell: pwsh
      working-directory: ${{ inputs.candidate-root }}
      run: |
        $errorFile = "${{ inputs.output-directory }}\candidate-error.txt"
        New-Item -ItemType Directory -Force -Path "${{ inputs.output-directory }}\packages" | Out-Null
        dotnet restore AgentSdk.proj 2>&1 | Tee-Object -Variable restoreOutput
        if ($LASTEXITCODE -ne 0) {
          $restoreOutput | Set-Content $errorFile
          "candidate-error-file=$errorFile" >> $env:GITHUB_OUTPUT
          exit 0
        }
        dotnet pack src\Microsoft.Agents.SDK.sln -c Release --no-restore -p:PackageOutputPath=${{ inputs.output-directory }}\packages 2>&1 | Tee-Object -Variable packOutput
        if ($LASTEXITCODE -ne 0) {
          $packOutput | Set-Content $errorFile
          "candidate-error-file=$errorFile" >> $env:GITHUB_OUTPUT
          exit 0
        }
        "candidate-error-file=" >> $env:GITHUB_OUTPUT
    - id: analyze
      name: Analyze compatibility
      shell: pwsh
      working-directory: ${{ inputs.trusted-root }}
      run: >
        dotnet run --no-build -c Release
        --project src\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj --
        analyze
        --repo-root "${{ inputs.candidate-root }}"
        --packages "${{ inputs.output-directory }}\packages"
        --event "${{ inputs.event-path }}"
        --output "${{ inputs.output-directory }}"
        --candidate-error-file "${{ steps.candidate.outputs.candidate-error-file }}"
```

- [ ] **Step 4: Run CLI tests and a local fixture invocation**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj
dotnet run --project src\tools\Microsoft.Agents.ApiCompat\Microsoft.Agents.ApiCompat.csproj -- --help
```

Expected: all tests PASS; help prints both commands and exits zero.
Also run a fixture invocation with a populated `--candidate-error-file` and verify it writes
`report.json`, `summary.md`, and decision `InfrastructureFailure`.

- [ ] **Step 5: Commit CLI and action**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\Program.cs src\tests\Microsoft.Agents.ApiCompat.Tests\ReportWriterTests.cs .github\actions\detect-breaking-changes\action.yml
git commit -m "feat: expose breaking change detection action" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 8: Add the read-only PR enforcement workflow

**Files:**
- Create: `.github/workflows/api-compat.yml`

**Interfaces:**
- Consumes composite action outputs from Task 7.
- Produces artifact `api-compat-report-<pr>-<run-id>`.
- Produces required check `API compatibility / detect`.

- [ ] **Step 1: Create the workflow**

```yaml
name: API compatibility

on:
  pull_request_target:
    branches: [main, "rel/**"]
    types: [opened, synchronize, reopened, edited, labeled, unlabeled]

permissions:
  contents: read

concurrency:
  group: api-compat-${{ github.event.pull_request.number }}
  cancel-in-progress: true

jobs:
  detect:
    name: detect
    runs-on: windows-latest
    steps:
      - name: Checkout trusted base
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.pull_request.base.sha }}
          path: trusted
          persist-credentials: false
          fetch-depth: 1
      - name: Checkout candidate
        uses: actions/checkout@v4
        with:
          repository: ${{ github.event.pull_request.head.repo.full_name }}
          ref: ${{ github.event.pull_request.head.sha }}
          path: candidate
          persist-credentials: false
          fetch-depth: 0
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - id: compatibility
        name: Detect breaking changes
        uses: ./trusted/.github/actions/detect-breaking-changes
        with:
          trusted-root: ${{ github.workspace }}\trusted
          candidate-root: ${{ github.workspace }}\candidate
          event-path: ${{ github.event_path }}
          output-directory: ${{ runner.temp }}\api-compat
      - name: Publish job summary
        if: always()
        shell: pwsh
        run: |
          $summary = "${{ runner.temp }}\api-compat\summary.md"
          if (Test-Path $summary) { Get-Content $summary | Add-Content $env:GITHUB_STEP_SUMMARY }
      - name: Publish annotations
        if: always()
        shell: pwsh
        run: |
          $annotations = "${{ runner.temp }}\api-compat\annotations.txt"
          if (Test-Path $annotations) { Get-Content $annotations }
      - name: Upload compatibility report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: api-compat-report-${{ github.event.pull_request.number }}-${{ github.run_id }}
          path: |
            ${{ runner.temp }}\api-compat\report.json
            ${{ runner.temp }}\api-compat\report.md
            ${{ runner.temp }}\api-compat\summary.md
          if-no-files-found: error
          retention-days: 14
      - name: Enforce compatibility result
        if: always()
        shell: pwsh
        run: |
          $decision = "${{ steps.compatibility.outputs.decision }}"
          if ($decision -in @("Pass", "Overridden")) { exit 0 }
          Write-Error "API compatibility decision: $decision"
          exit 1
```

- [ ] **Step 2: Validate workflow paths and YAML**

Run:

```powershell
git diff --check
Get-Content .github\workflows\api-compat.yml | ConvertFrom-Yaml | Out-Null
```

Expected: no whitespace errors and valid YAML. If `ConvertFrom-Yaml` is unavailable, use the
repository's existing YAML parser; do not install a new linter solely for this task.

- [ ] **Step 3: Commit enforcement workflow**

```powershell
git add .github\workflows\api-compat.yml
git commit -m "ci: enforce API compatibility on pull requests" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 9: Add the trusted sticky-comment reporter

**Files:**
- Create: `.github/workflows/api-compat-report.yml`
- Modify: `src/tools/Microsoft.Agents.ApiCompat/Program.cs`
- Modify: `src/tests/Microsoft.Agents.ApiCompat.Tests/ReportWriterTests.cs`

**Interfaces:**
- Consumes only `workflow_run` payload metadata and `report.json`.
- Produces one comment containing `<!-- agents-sdk-api-compat -->` and `<!-- api-compat-run:<id> -->`.

- [ ] **Step 1: Add reporter validation tests**

Add tests proving `render-comment` rejects mismatched run/PR IDs and emits:

```markdown
<!-- agents-sdk-api-compat -->
<!-- api-compat-run:12345 -->
# API compatibility
```

Also assert user-controlled `@`, `<`, `>`, and `|` remain encoded.

- [ ] **Step 2: Add the trusted reporter workflow**

```yaml
name: API compatibility report

on:
  workflow_run:
    workflows: ["API compatibility"]
    types: [completed]

permissions:
  actions: read
  contents: read
  pull-requests: write

concurrency:
  group: api-compat-report-${{ github.event.workflow_run.pull_requests[0].number }}
  cancel-in-progress: false

jobs:
  comment:
    if: ${{ github.event.workflow_run.pull_requests[0].number != null }}
    runs-on: ubuntu-latest
    steps:
      - name: Checkout trusted default branch
        uses: actions/checkout@v4
        with:
          persist-credentials: false
          fetch-depth: 1
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: Download report artifact
        uses: actions/download-artifact@v4
        with:
          name: api-compat-report-${{ github.event.workflow_run.pull_requests[0].number }}-${{ github.event.workflow_run.id }}
          path: ${{ runner.temp }}/api-compat
          repository: ${{ github.repository }}
          run-id: ${{ github.event.workflow_run.id }}
          github-token: ${{ github.token }}
      - name: Render sanitized comment
        run: >
          dotnet run -c Release
          --project src/tools/Microsoft.Agents.ApiCompat/Microsoft.Agents.ApiCompat.csproj --
          render-comment
          --report "${{ runner.temp }}/api-compat/report.json"
          --output "${{ runner.temp }}/api-compat/comment.md"
          --run-id "${{ github.event.workflow_run.id }}"
          --pr-number "${{ github.event.workflow_run.pull_requests[0].number }}"
      - name: Update sticky comment
        uses: actions/github-script@v7
        env:
          COMMENT_PATH: ${{ runner.temp }}/api-compat/comment.md
          PR_NUMBER: ${{ github.event.workflow_run.pull_requests[0].number }}
          RUN_ID: ${{ github.event.workflow_run.id }}
        with:
          script: |
            const fs = require('fs');
            const marker = '<!-- agents-sdk-api-compat -->';
            const body = fs.readFileSync(process.env.COMMENT_PATH, 'utf8');
            const issue_number = Number(process.env.PR_NUMBER);
            const runId = Number(process.env.RUN_ID);
            const comments = await github.paginate(github.rest.issues.listComments, {
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number,
              per_page: 100
            });
            const existing = comments.find(comment => comment.body?.includes(marker));
            const prior = existing?.body?.match(/<!-- api-compat-run:(\d+) -->/);
            if (prior && Number(prior[1]) > runId) {
              core.info(`Skipping stale run ${runId}; comment already has run ${prior[1]}.`);
              return;
            }
            if (existing) {
              await github.rest.issues.updateComment({
                owner: context.repo.owner,
                repo: context.repo.repo,
                comment_id: existing.id,
                body
              });
            } else {
              await github.rest.issues.createComment({
                owner: context.repo.owner,
                repo: context.repo.repo,
                issue_number,
                body
              });
            }
```

- [ ] **Step 3: Run reporter rendering tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter ReportWriterTests
```

Expected: PASS.

- [ ] **Step 4: Commit trusted reporting**

```powershell
git add .github\workflows\api-compat-report.yml src\tools\Microsoft.Agents.ApiCompat\Program.cs src\tests\Microsoft.Agents.ApiCompat.Tests\ReportWriterTests.cs
git commit -m "ci: report API compatibility results on PRs" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 10: Add contributor guidance, repository label, and final validation

**Files:**
- Create: `.github/pull_request_template.md`
- Modify: `.github/CODEOWNERS`
- Modify only if required by build discovery: `AgentSdk.proj`

**Interfaces:**
- Produces repository label `breaking-change-approved`.
- Documents the exact override heading consumed by `OverridePolicy`.

- [ ] **Step 1: Add the PR template section**

```markdown
## Breaking change justification

<!--
Leave this section empty when the PR has no intentional public API break.
For an intentional break, explain the affected packages/APIs, migration path, and why the
change is necessary. A maintainer must also apply the `breaking-change-approved` label.
-->
```

- [ ] **Step 2: Create or update the repository label**

Run:

```powershell
gh label create breaking-change-approved --color B60205 --description "Intentional API break reviewed and approved" --force
```

Expected: label exists with the specified color and description.

- [ ] **Step 3: Run focused tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj -c Debug
```

Expected: all compatibility tool tests PASS.

- [ ] **Step 4: Build the solution**

Run:

```powershell
dotnet restore AgentSdk.proj
dotnet build AgentSdk.proj -c Debug --no-restore
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 5: Exercise the CLI against integration fixtures**

Run the integration test class without network access:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj -c Debug --no-build --filter CompatibilityAnalyzerIntegrationTests
```

Expected: PASS for no-break, source-only, binary-only, source-and-binary, warning-only,
override, missing-baseline, mixed-package, and infrastructure-failure fixtures.

- [ ] **Step 6: Verify repository state and workflow text**

Run:

```powershell
git diff --check
git status --short
git --no-pager diff --stat HEAD~10..HEAD
```

Expected: no whitespace errors; only intended CLI, tests, action, workflows, tool manifest,
solution, and PR template changes are present.

- [ ] **Step 7: Commit contributor guidance**

```powershell
git add .github\pull_request_template.md
git commit -m "docs: document breaking change override" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

- [ ] **Step 8: Add explicit CODEOWNERS coverage**

Append:

```text
/.github/workflows/api-compat*.yml @microsoft/agents-sdk
/.github/actions/detect-breaking-changes/ @microsoft/agents-sdk
/src/tools/Microsoft.Agents.ApiCompat/ @microsoft/agents-sdk
```

Commit:

```powershell
git add .github\CODEOWNERS
git commit -m "build: protect API compatibility policy" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

- [ ] **Step 9: Configure branch protection after the workflow appears**

In the repository ruleset, require the status check named:

```text
API compatibility / detect
```

Expected: PRs cannot merge while the compatibility check is `Block` or
`InfrastructureFailure`, but merge normally when it is `Pass` or `Overridden`.
