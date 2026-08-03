# Sequence Diagram Audit Verdict Enforcement

## Problem

The scheduled sequence-diagram audit currently treats every working-tree diff as confirmed
diagram drift. Copilot can leave an incidental edit while reporting the diagram as `up to date`.
The workflow then publishes that edit as a proposed fix.

Issue #954 comment 5164524778 demonstrates the failure: the audit reported
`docs/oauth-internal-sequence-diagram.md: up to date` but changed a semicolon to a comma. The
workflow filed the punctuation-only diff because it never reconciled Copilot's verdict with the
working tree.

## Design

Make the audit summary a machine-enforced contract.

Copilot must emit exactly one summary line for every sequence diagram using one of these forms:

```text
- docs/<name>-sequence-diagram.md: up to date
- docs/<name>-sequence-diagram.md: corrected — <specific reason>
```

After the audit and before Mermaid linting, the workflow parses the final summary and reconciles it
with `git diff`:

1. Restore any changed diagram whose verdict is `up to date`.
2. Keep a changed diagram only when it has a valid `corrected — <reason>` verdict.
3. Fail the job when a changed diagram has a missing, duplicate, or malformed verdict.
4. Fail the job when a `corrected` verdict has no corresponding file change.
5. Lint and publish only the reconciled changes.

This makes accidental edits harmless even when the model violates the prompt. The workflow no
longer relies on prose instructions alone to distinguish drift from churn.

## Components

- `.github/prompts/verify-sequence-diagrams.prompt.md` defines the strict summary format.
- A repository script parses the summary and reconciles changed files. Keeping this logic outside
  the workflow makes it directly testable.
- `.github/workflows/verify-sequence-diagrams.yml` invokes reconciliation before Mermaid linting
  and uses the reconciled summary when creating issue comments.

## Error Handling

Contract violations fail closed. The workflow must not publish a proposed change when the model's
verdict is ambiguous or inconsistent with the working tree. Logs identify the affected diagram and
the invalid or missing verdict.

## Validation

Automated tests cover:

- an `up to date` diagram with an incidental diff is restored;
- a `corrected` diagram with a diff is retained;
- changed files with missing, duplicate, or malformed verdicts fail;
- `corrected` verdicts without diffs fail;
- a fully up-to-date audit leaves no diff and produces no issue.

The existing Mermaid lint remains the final syntax validation for retained diagram changes.
