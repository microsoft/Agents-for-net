#!/usr/bin/env bash
# Read/search review with bounded correction of structural and evidence errors.
set -euo pipefail

: "${ARTIFACTS:?ARTIFACTS is required}"
: "${REVIEW_WORKSPACE:?REVIEW_WORKSPACE is required}"
: "${TOOL:?TOOL is required}"
: "${COPILOT_MODEL:?COPILOT_MODEL is required}"

artifacts_dir=$(cd "$ARTIFACTS" && pwd)
workspace_dir=$(cd "$REVIEW_WORKSPACE" && pwd)
repository_dir=${GITHUB_WORKSPACE:-$PWD}
instructions=$(cat .github/instructions/teams-api-impact-review-instructions.md)
cli_version=$(copilot --version)

dotnet run --project "$TOOL" -c Release --no-build -- write-agent-metadata \
    --output "$artifacts_dir/impact-review-agent.json" --model "$COPILOT_MODEL" --cli-version "$cli_version"

# Prevent a failed invocation from leaving an earlier response as the current result.
: > "$artifacts_dir/impact-review.raw.json"

for attempt in 1 2 3; do
    prompt="$instructions"

    if (( attempt > 1 )); then
        prompt+=$'\nCorrection attempt: read review-feedback.json and previous-review.txt in the snapshot. Correct the validation errors against actual source, and return the complete review. Do not change impact verdicts merely to pass the gate.'
    fi

    printf 'Starting implementation review attempt %s/3 with read/search tools in %s\n' "$attempt" "$workspace_dir"

    # No shell, editing, network tools, MCPs, custom instructions, or delegation.
    status=0
    : > "$artifacts_dir/impact-review.raw.json"

    (
        cd "$workspace_dir"
        copilot -C "$workspace_dir" --model "$COPILOT_MODEL" --output-format json --stream off --silent --no-ask-user --no-custom-instructions \
            --available-tools='view,grep,glob' --allow-tool=read \
            --deny-tool='shell,write,url,memory' --disable-builtin-mcps --disallow-temp-dir \
            --log-level none -p "$prompt"
    ) > "$artifacts_dir/impact-review.attempt-$attempt.events.jsonl" \
        2> "$artifacts_dir/impact-review.attempt-$attempt.stderr.log" || status=$?

    if (( status != 0 )); then
        printf 'Summary: AI implementation review FAILED (attempt %s, exit %s); see impact-review.attempt-%s.stderr.log.\n' "$attempt" "$status" "$attempt" >&2
        exit "$status"
    fi

    # Treat malformed/incomplete event streams as execution failures. Never fall back
    # to a draft, tool result, or earlier attempt when there is no final response.
    if ! dotnet run --project "$TOOL" -c Release --no-build -- extract-agent-response \
        --input "$artifacts_dir/impact-review.attempt-$attempt.events.jsonl" \
        --output "$artifacts_dir/impact-review.attempt-$attempt.raw.json"; then
        printf 'Summary: AI implementation review FAILED to capture a completed response; see attempt %s events/diagnostics.\n' "$attempt" >&2
        exit 1
    fi

    cp "$artifacts_dir/impact-review.attempt-$attempt.raw.json" "$artifacts_dir/impact-review.raw.json"

    # Retry invalid structure/evidence only. A valid non-safe review or failed independent
    # check must reach the final policy gate as-is, without fishing for a safe verdict.
    if dotnet run --project "$TOOL" -c Release --no-build -- validate-impact-review \
        --input "$artifacts_dir/review-input.json" --review "$artifacts_dir/impact-review.raw.json" \
        --workspace "$workspace_dir" --repository-root "$repository_dir" \
        --output "$artifacts_dir/impact-review.attempt-$attempt.validation.json" \
        --report "$artifacts_dir/impact-review.attempt-$attempt.report.md"; then
        printf 'Summary: AI implementation review produced valid decisions and evidence on attempt %s; the final step evaluates the impact gate.\n' "$attempt"
        exit 0
    else
        validation_status=$?

        if (( validation_status != 1 )); then
            printf 'Summary: Review validator could not run (exit %s); stopping without requesting a new assessment.\n' "$validation_status" >&2
            exit "$validation_status"
        fi
    fi

    if (( attempt < 3 )); then
        cp "$artifacts_dir/impact-review.raw.json" "$workspace_dir/previous-review.txt"
        cp "$artifacts_dir/impact-review.attempt-$attempt.validation.json" "$workspace_dir/review-feedback.json"
        printf 'Summary: Attempt %s failed validation; requesting evidence/format corrections.\n' "$attempt"
    fi
done

printf 'Summary: AI implementation review FAILED validation after 3 attempts; all responses and validation reports are retained.\n' >&2
exit 1
