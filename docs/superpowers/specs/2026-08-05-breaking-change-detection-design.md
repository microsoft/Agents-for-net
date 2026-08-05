# PR Breaking-Change Detection Design

## Goal

Add a required GitHub PR check that compares every packable SDK library with its latest
applicable stable NuGet release and reports public API breaking changes as:

- **Source**
- **Binary**
- **Source + Binary**

The check blocks unapproved high-confidence breaks. An intentional break can proceed when
the PR includes both an approval label and a written justification. Potential source risks
that depend on consumer code, such as overload ambiguity, are reported as warnings rather
than treated as confirmed breaks.

Behavioral changes are outside this design because they cannot be determined reliably from
package API metadata.

## Selected Approach

Create a tested .NET CLI in the repository and wrap it in a local composite GitHub Action.
The CLI owns package discovery, baseline resolution, ApiCompat execution, diagnostic
classification, override validation, and report generation.

This is preferred over:

- A PowerShell-only action, because parsing, classification, and fixture-based testing would
  be more brittle.
- Per-project MSBuild package validation, because dynamic baselines, unified reporting,
  source/binary categorization, and PR-level overrides are difficult to coordinate there.

## Components

### Compatibility CLI

Add a `net8.0` tool under `src/tools/` with focused components for:

1. Discovering packable projects under `src/libraries/`.
2. Reading package IDs and locating the candidate `.nupkg` files.
3. Resolving the applicable stable baseline version.
4. Downloading baseline packages from NuGet.org.
5. Running a pinned `Microsoft.DotNet.ApiCompat.Tool` version.
6. Parsing and classifying diagnostics.
7. Evaluating the PR override policy.
8. Writing JSON, Markdown, GitHub annotations, and job-summary content.

The tool version and diagnostic classification table are versioned together. If a pinned
ApiCompat upgrade introduces an unknown diagnostic, the run fails as an infrastructure
error until the diagnostic is deliberately classified.

### Local Composite Action

Add a local action under `.github/actions/` that:

1. Restores and builds the trusted compatibility CLI.
2. Packs the candidate SDK libraries.
3. Runs compatibility analysis.
4. Emits action outputs for the result, finding counts, override state, and report paths.
5. Ensures reports remain available to later `always()` steps.

The action is a thin orchestration layer. Detection and policy logic remain in the tested
.NET CLI.

### PR Detection Workflow

Add a required PR workflow that:

- Runs for PR creation, synchronization, reopening, description edits, and label changes.
- Uses a GitHub-hosted runner.
- Has read-only repository permissions and receives no secrets.
- Loads the analyzer and action from a separate checkout of the PR base branch.
- Checks out candidate source separately with persisted Git credentials disabled.
- Does not save writable caches from candidate execution.
- Uploads JSON and Markdown report artifacts even when analysis or enforcement fails.
- Writes a job summary and GitHub annotations.
- Fails when confirmed breaks exist without a valid override.

Repository rules and CODEOWNERS should protect the workflow, composite action, CLI, and
classification policy from unreviewed changes.

### Trusted Reporter Workflow

Add a `workflow_run` workflow that reacts to completion of the detection workflow. It runs
trusted code from the default branch and:

1. Determines the PR from the `workflow_run` payload rather than artifact-provided metadata.
2. Downloads the compatibility report artifact for that exact run.
3. Validates the JSON schema, expected workflow identity, and a conservative artifact size
   limit.
4. Renders and updates one marker-based sticky PR comment.

The reporter never checks out or executes PR code. It treats every artifact field as
untrusted, escapes Markdown/HTML control content and mentions, and does not post artifact
Markdown verbatim. This permits safe reporting for fork PRs while keeping the detection
workflow read-only.

## Baseline Selection

For each packable library package:

1. Determine the PR target branch.
2. Query stable versions from NuGet.org.
3. Select the baseline:
   - For `main`, use the latest stable version overall.
   - For `rel/vX.Y`, use the latest stable version in the `X.Y.*` line.
4. Download the selected `.nupkg`.
5. Compare it with the candidate package across all shipped target frameworks.

Prerelease versions are never selected as baselines.

If the package ID has no applicable stable release, classify it as a new package with
`NoBaseline` status. That is informational and does not fail the PR. Feed, download, or
parsing failures are not treated as missing baselines; they fail closed as infrastructure
errors.

## Detection and Classification

Run ApiCompat package baseline validation for confirmed compatibility breaks. Also run
strict baseline comparison to identify API additions that can create source compatibility
risks.

The classification table maps every supported ApiCompat diagnostic and comparison direction
to one of these results:

| Result | Meaning | Enforcement |
| --- | --- | --- |
| Source | Recompiling existing consumer source can fail, while existing binaries remain callable | Blocking |
| Binary | Existing compiled consumers can fail without recompilation | Blocking |
| Source + Binary | Both existing source and existing binaries are incompatible | Blocking |
| Potential source risk | Compatibility depends on consumer source, such as overload resolution | Warning |
| Infrastructure error | Analysis was incomplete or an output was not understood | Blocking |

Examples of policy mapping:

- A public parameter rename detected by the enabled parameter-name rule is **Source**.
- An assembly identity change is **Binary**.
- Removed public types or members, reduced visibility, changed signatures, removed base
  contracts, and incompatible virtual/interface contract changes are generally
  **Source + Binary**.
- Public additions that could affect name or overload resolution are **Potential source
  risk** warnings.
- Dropping a shipped target framework is **Source + Binary**.

The implementation must define and test the complete mapping for the pinned ApiCompat
diagnostic set rather than relying on message text alone. Direction-sensitive strict-mode
diagnostics must distinguish additions from removals.

## Reporting

The report groups results by package and category. Each finding includes:

- Package ID.
- Baseline and candidate versions.
- Target framework or package asset.
- Category and enforcement level.
- ApiCompat diagnostic ID.
- Affected API target.
- Sanitized diagnostic detail.

The same result is exposed through:

- The GitHub job summary.
- GitHub workflow annotations.
- A downloadable JSON report for automation.
- A downloadable Markdown report for human inspection.
- One sticky PR comment updated after every completed run.

The sticky comment must show the final enforcement state:

- No breaking changes.
- Breaking changes detected and blocking.
- Breaking changes detected but explicitly overridden.
- Analysis failed due to infrastructure.

Warning-only potential source risks do not fail the check but remain visible in every report.

## Override Policy

Confirmed breaks are overridden only when both conditions are true:

1. The PR has the `breaking-change-approved` label.
2. The PR description contains a non-empty `## Breaking change justification` section.

The justification extends until the next Markdown heading of the same or higher level, or
the end of the description. Whitespace-only content is invalid.

When the override is valid:

- The required compatibility check succeeds.
- All findings remain in the summary, annotations, artifacts, and sticky comment.
- The report clearly states that the breaks were approved rather than absent.
- The justification is included in the report after sanitization.

Editing the description or adding/removing the label triggers reevaluation. A label without
a justification, or a justification without the label, does not override the failure.

## Failure Handling

The workflow must never silently convert an incomplete analysis into success.

Blocking infrastructure conditions include:

- NuGet feed or baseline download failures.
- Candidate pack failures.
- ApiCompat installation or execution failures.
- Malformed or unexpected ApiCompat output.
- An unmapped diagnostic.
- Missing expected candidate packages.
- Invalid or oversized reporter artifacts.

Reports are written incrementally so an infrastructure failure still provides actionable
context. Upload and summary steps use `always()` semantics.

Cancellation of superseded runs is allowed through workflow concurrency, but a canceled run
must not overwrite the sticky comment from a newer run.

## Security

Candidate code is untrusted and can execute during restore, build, and pack. Therefore:

- The detection runner is ephemeral and GitHub-hosted.
- The workflow grants only read permissions and exposes no repository or cloud secrets.
- Checkout uses `persist-credentials: false`.
- Candidate output is never executed by the trusted reporter.
- The reporter uses the workflow payload as the authority for repository, run, and PR
  identity.
- Artifact fields are schema-validated, bounded, escaped, and rendered by trusted code.
- Reporter permissions are limited to `actions: read`, `contents: read`, and
  `pull-requests: write`.

## Testing

### Unit Tests

Cover:

- `main` and `rel/vX.Y` baseline version selection.
- Stable versus prerelease filtering.
- Packable project and package discovery.
- New-package/no-baseline handling.
- Every pinned diagnostic classification.
- Direction-sensitive addition/removal classification.
- PR justification parsing.
- All label/justification override combinations.
- JSON schema and Markdown rendering.
- Markdown, HTML, and mention escaping.
- Malformed, unknown, and oversized input handling.

### Integration Tests

Use locally generated fixture packages so tests do not depend on live NuGet availability.
Include:

- No public API break.
- Source-only parameter rename.
- Binary-only assembly identity change.
- Source-and-binary type/member removal.
- Warning-only public addition.
- Dropped target framework.
- New package without a baseline.
- Multiple packages with mixed results.

### Workflow Validation

Exercise the composite action against fixture packages and verify:

- Report artifacts are produced on pass, detected break, valid override, and infrastructure
  failure.
- The final gate exit code matches the report decision.
- The trusted reporter updates one existing marker comment rather than creating duplicates.
- An older completed run cannot replace a newer run's comment.

## Success Criteria

The design is complete when a PR build can:

1. Compare every packable SDK library with the correct stable release line.
2. Categorize every confirmed break as source, binary, or both.
3. Report high-confidence breaks and warning-only source risks in all agreed surfaces.
4. Block unapproved confirmed breaks.
5. Allow an intentional break through the explicit label-plus-justification override.
6. Safely update sticky comments for same-repository and fork PRs.
7. Fail closed when compatibility analysis is incomplete.
