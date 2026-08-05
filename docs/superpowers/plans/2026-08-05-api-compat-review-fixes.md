# API Compatibility Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Block public enum value changes and reliably post API compatibility reports for fork pull requests.

**Architecture:** Keep diagnostic policy in `DiagnosticClassifier` and make `report.json` the trusted source of the pull request number. The detection workflow uploads a run-ID-addressable artifact; the trusted report workflow downloads it, validates the embedded pull request number, and uses that value for rendering and commenting.

**Tech Stack:** C#/.NET 8, xUnit, GitHub Actions YAML, PowerShell.

## Global Constraints

- Keep `CP0013` suppressed.
- Treat `CP0011` as a blocking binary compatibility diagnostic.
- Name the report artifact `api-compat-report-<run-id>`.
- Fail the report workflow if `PullRequestNumber` is missing or is not a positive integer.
- Do not depend on `github.event.workflow_run.pull_requests`.

---

### Task 1: Enforce Enum Member Value Compatibility

**Files:**
- Modify: `src/tools/Microsoft.Agents.ApiCompat/DiagnosticClassifier.cs:5-25`
- Test: `src/tests/Microsoft.Agents.ApiCompat.Tests/DiagnosticClassifierTests.cs:7-32`

**Interfaces:**
- Consumes: ApiCompat diagnostic ID `CP0011`.
- Produces: `DiagnosticClassifier.ApiCompatNoWarn == "CP0013"` and `Classify("CP0011", BaselineToCandidate)` returning binary/blocking.

- [ ] **Step 1: Write failing classifier tests**

Change the pinned suppression test and add `CP0011` to the policy theory:

```csharp
[Fact]
public void ApiCompatNoWarn_HasPinnedLiteral()
{
    Assert.Equal("CP0013", DiagnosticClassifier.ApiCompatNoWarn);
}

[Theory]
[InlineData("CP0011", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
[InlineData("CP0017", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Source, FindingSeverity.Blocking)]
```

Keep the remaining existing inline data unchanged.

- [ ] **Step 2: Run the targeted test and verify it fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter "FullyQualifiedName~DiagnosticClassifierTests"
```

Expected: failure because `ApiCompatNoWarn` still contains `CP0011`, and `Classify` rejects `CP0011`.

- [ ] **Step 3: Implement the diagnostic policy**

Update `DiagnosticClassifier`:

```csharp
public const string ApiCompatNoWarn = "CP0013";

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
        ["CP0011"] = CompatibilityCategory.Binary,
        ["CP0012"] = CompatibilityCategory.SourceAndBinary,
```

Keep the remaining mappings unchanged.

- [ ] **Step 4: Run the targeted test and verify it passes**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj --filter "FullyQualifiedName~DiagnosticClassifierTests"
```

Expected: all `DiagnosticClassifierTests` pass.

- [ ] **Step 5: Commit the classifier fix**

```powershell
git add src\tools\Microsoft.Agents.ApiCompat\DiagnosticClassifier.cs src\tests\Microsoft.Agents.ApiCompat.Tests\DiagnosticClassifierTests.cs
git commit -m "fix: enforce enum value compatibility"
```

---

### Task 2: Resolve Fork Pull Request Numbers from the Report

**Files:**
- Modify: `.github/workflows/api-compat.yml:65-73`
- Modify: `.github/workflows/api-compat-report.yml:12-49`

**Interfaces:**
- Consumes: `report.json` containing integer property `PullRequestNumber`.
- Produces: step output `steps.report.outputs.pr-number`, used by render and comment steps.

- [ ] **Step 1: Demonstrate the current workflow dependency**

Run:

```powershell
$matches = rg -n "workflow_run\.pull_requests" .github\workflows\api-compat-report.yml
if (-not $matches) { throw "Expected the current workflow to depend on workflow_run.pull_requests." }
$matches
```

Expected: matches in the concurrency key, job condition, artifact name, renderer arguments, and comment environment.

- [ ] **Step 2: Make the detection artifact addressable by run ID**

In `.github/workflows/api-compat.yml`, change the upload name to:

```yaml
      - name: Upload compatibility report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: api-compat-report-${{ github.run_id }}
```

Keep the existing paths, missing-file behavior, and retention unchanged.

- [ ] **Step 3: Download the artifact without pull request metadata**

In `.github/workflows/api-compat-report.yml`, replace the concurrency key and remove the job-level pull request condition:

```yaml
concurrency:
  group: api-compat-report-${{ github.event.workflow_run.id }}
  cancel-in-progress: false

jobs:
  comment:
    runs-on: ubuntu-latest
```

Update the artifact name:

```yaml
      - name: Download report artifact
        uses: actions/download-artifact@v4
        with:
          name: api-compat-report-${{ github.event.workflow_run.id }}
```

Keep the repository, run ID, token, and destination settings unchanged.

- [ ] **Step 4: Validate and expose the embedded pull request number**

Add this step immediately after artifact download:

```yaml
      - id: report
        name: Read report metadata
        shell: pwsh
        run: |
          $reportPath = "${{ runner.temp }}/api-compat/report.json"
          if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
            throw "Compatibility report '$reportPath' does not exist."
          }

          $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
          $pullRequestNumber = 0
          if (-not [int]::TryParse(
              [string]$report.PullRequestNumber,
              [ref]$pullRequestNumber) -or
              $pullRequestNumber -le 0) {
            throw "Compatibility report has an invalid PullRequestNumber."
          }

          "pr-number=$pullRequestNumber" >> $env:GITHUB_OUTPUT
```

This fails closed for missing, malformed, zero, or negative values.

- [ ] **Step 5: Use the validated output for rendering and commenting**

Update the renderer argument:

```yaml
          --pr-number "${{ steps.report.outputs.pr-number }}"
```

Update the sticky-comment environment:

```yaml
        env:
          COMMENT_PATH: ${{ runner.temp }}/api-compat/comment.md
          PR_NUMBER: ${{ steps.report.outputs.pr-number }}
          RUN_ID: ${{ github.event.workflow_run.id }}
```

- [ ] **Step 6: Verify no workflow expression depends on `pull_requests`**

Run:

```powershell
$matches = rg -n "workflow_run\.pull_requests" .github\workflows\api-compat-report.yml
if ($matches) {
  $matches
  throw "Report workflow still depends on workflow_run.pull_requests."
}
```

Expected: command completes without output.

- [ ] **Step 7: Verify the workflow diff**

Run:

```powershell
git --no-pager diff --check
git --no-pager diff -- .github\workflows\api-compat.yml .github\workflows\api-compat-report.yml
```

Expected: no whitespace errors; the artifact name is run-ID-only, report metadata is validated, and all renderer/comment PR references use `steps.report.outputs.pr-number`.

- [ ] **Step 8: Commit the workflow fix**

```powershell
git add .github\workflows\api-compat.yml .github\workflows\api-compat-report.yml
git commit -m "fix: report API compatibility for fork PRs"
```

---

### Task 3: Validate the Combined Change

**Files:**
- Verify: `src/tools/Microsoft.Agents.ApiCompat/DiagnosticClassifier.cs`
- Verify: `src/tests/Microsoft.Agents.ApiCompat.Tests/DiagnosticClassifierTests.cs`
- Verify: `.github/workflows/api-compat.yml`
- Verify: `.github/workflows/api-compat-report.yml`

**Interfaces:**
- Consumes: completed Tasks 1 and 2.
- Produces: a branch where both reviewed defects are corrected.

- [ ] **Step 1: Run the complete API compatibility tool test project**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.ApiCompat.Tests\Microsoft.Agents.ApiCompat.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 2: Check the final branch diff**

Run:

```powershell
git --no-pager diff --check HEAD~2..HEAD
git status --short
```

Expected: no whitespace errors and a clean worktree.
