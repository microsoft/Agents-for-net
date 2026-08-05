# API Compatibility Review Fixes Design

## Goal

Correct two defects in the API compatibility branch:

1. Public enum member value changes must block compatibility checks.
2. Compatibility reports must be posted for pull requests from forks.

## Diagnostic Policy

Remove `CP0011` from the ApiCompat suppression list and classify it as a
blocking binary compatibility diagnostic. Keep `CP0013` suppressed. Update the
classifier tests so they pin the remaining suppression and verify the `CP0011`
classification.

## Pull Request Report Flow

Use the generated compatibility report as the source of truth for the pull
request number:

1. The detection workflow uploads an artifact named only with the workflow run
   ID: `api-compat-report-<run-id>`.
2. The report workflow downloads that artifact without requiring pull request
   metadata from the `workflow_run` event.
3. A trusted PowerShell step reads `report.json`, validates that
   `PullRequestNumber` is a positive integer, and exposes it as a step output.
4. Rendering and sticky-comment steps use that validated output.

The report workflow concurrency key uses the triggering run ID because the pull
request number is not available until after the job starts. The existing stale
run marker prevents an older report from replacing a newer comment.

## Error Handling

If the artifact is missing, malformed, or lacks a valid pull request number,
the report workflow fails rather than commenting on an unintended issue.

## Validation

Run the targeted `Microsoft.Agents.ApiCompat.Tests` test project. Inspect the
workflow diff to confirm that no expression depends on
`workflow_run.pull_requests`.
