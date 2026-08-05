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

Resolve the pull request number from trusted `workflow_run` metadata in the
privileged report workflow:

1. The detection workflow uploads an artifact named only with the workflow run
   ID: `api-compat-report-<run-id>`.
2. The report workflow downloads that artifact without requiring pull request
   metadata from the `workflow_run` event.
3. An `actions/github-script` step lists open pull requests in
   `microsoft/Agents-for-net` and matches them against
   `workflow_run.head_repository.full_name` and `workflow_run.head_sha`.
4. The resolver verifies that the sole match targets
   `microsoft/Agents-for-net` and has the exact triggering head repository and
   SHA, then exposes its number as a step output.
5. Rendering and sticky-comment steps use the API-resolved output. The renderer
   cross-checks it against `report.PullRequestNumber`, preserving artifact
   integrity validation without trusting the artifact to select the comment
   target.

The report workflow concurrency key uses the triggering run ID because the pull
request number is not available until after the job starts. The existing stale
run marker prevents an older report from replacing a newer comment.

## Error Handling

The resolver fails closed if trusted head metadata is missing or if zero or
multiple open pull requests match. Rendering fails if the artifact is missing,
malformed, or names a different pull request than the API-resolved value. The
privileged workflow alone retains `pull-requests: write`; the detection workflow
remains read-only.

## Validation

Run the targeted `Microsoft.Agents.ApiCompat.Tests` test project. Inspect the
workflow diff to confirm that no expression depends on
`workflow_run.pull_requests`, no artifact field selects the pull request, and
the resolver uses exact head repository and SHA checks.
