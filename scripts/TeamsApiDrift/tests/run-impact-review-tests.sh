#!/usr/bin/env bash
# Offline orchestration regressions: no model calls, credentials, or package downloads.
set -euo pipefail

repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$repo_dir"

# Reuse the output parser built by the preceding drift-tool tests.
export OUTPUT_TOOL_DLL="$repo_dir/bin/Release/Microsoft.Agents.TeamsApiDrift/Release/net10.0/Microsoft.Agents.TeamsApiDrift.dll"

[[ -s "$OUTPUT_TOOL_DLL" ]] || {
    echo 'Build the drift tool in Release before running these tests.' >&2
    exit 1
}

test_root=$(mktemp -d "${TMPDIR:-/tmp}/teams-review-tests.XXXXXX")

cleanup() {
    # Only remove the directory created by mktemp for this test invocation.
    if [[ -d "$test_root" && "${test_root##*/}" == teams-review-tests.* ]]; then
        rm -rf -- "$test_root"
    fi
}

trap cleanup EXIT

copilot() {
    if [[ "${1:-}" == --version ]]; then
        printf 'GitHub Copilot CLI mock-version\n'
        return 0
    fi

    local count=0 arg

    [[ "$(pwd -P)" == "$(cd "$REVIEW_WORKSPACE" && pwd -P)" ]] || return 94
    [[ ! -f "$MOCK_STATE/calls" ]] || count=$(cat "$MOCK_STATE/calls")
    count=$((count + 1))
    printf '%s' "$count" > "$MOCK_STATE/calls"

    for arg in "--model $COPILOT_MODEL" "--output-format json" "--stream off" \
        --silent --no-ask-user --no-custom-instructions --available-tools=view,grep,glob \
        --allow-tool=read --deny-tool=shell,write,url,memory --disable-builtin-mcps --disallow-temp-dir; do
        [[ " $* " == *" $arg "* ]] || {
            printf 'Missing permission constraint: %s\n' "$arg" >&2
            return 98
        }
    done

    if (( count > 1 )); then
        [[ -s "$REVIEW_WORKSPACE/review-feedback.json" && -s "$REVIEW_WORKSPACE/previous-review.txt" ]] || return 97
        [[ " $* " == *'Correction attempt: read review-feedback.json and previous-review.txt'* ]] || return 96
    fi

    if [[ "$MOCK_CASE" == invalid-stream ]]; then
        printf '{"type":"assistant.message","data":{"content":"stale safe draft"}}\n'
        return 0
    fi

    if [[ "$MOCK_CASE" == agent-failure ]]; then
        printf 'partial failed response'
        return 42
    fi

    printf '{"type":"assistant.message","data":{"content":"response attempt %s","model":"test-model"}}\n' "$count"
    printf '{"type":"result","sessionId":"test-session","exitCode":0}\n'
}

dotnet() {
    while [[ "$1" != -- ]]; do
        shift
    done

    shift

    if [[ "$1" == write-agent-metadata || "$1" == extract-agent-response ]]; then
        command dotnet "$OUTPUT_TOOL_DLL" "$@"
        return
    fi

    [[ "$1" == "$REVIEW_COMMAND" ]] || return 93
    local output='' report='' count
    count=$(cat "$MOCK_STATE/calls")
    printf '%s\n' "$count" >> "$MOCK_STATE/validations"
    [[ "$MOCK_CASE" != validator-failure ]] || return 2

    while (( $# )); do
        case "$1" in
            --output)
                output=$2
                shift
                ;;
            --report)
                report=$2
                shift
                ;;
        esac

        shift
    done

    [[ -n "$output" && -n "$report" ]] || return 95
    printf 'attempt %s report' "$count" > "$report"

    if [[ "$MOCK_CASE" == always-invalid || ( "$MOCK_CASE" == correction && "$count" == 1 ) ]]; then
        printf '{"valid":false,"canProceed":false,"errors":["Invalid source quote"]}' > "$output"
        return 1
    fi

    # A valid blocked review must not be retried in search of a safe verdict.
    printf '{"valid":true,"canProceed":false,"hasFindings":true,"errors":[]}' > "$output"
}

export -f copilot dotnet

for review_kind in impact behavior; do
export REVIEW_PREFIX="$review_kind-review"
export REVIEW_INPUT="$review_kind-input.json"
[[ "$review_kind" != impact ]] || export REVIEW_INPUT=review-input.json
export REVIEW_COMMAND="validate-$review_kind-review"
export REVIEW_INSTRUCTIONS=".github/instructions/teams-api-impact-review-instructions.md"
for scenario in first-valid correction always-invalid agent-failure validator-failure invalid-stream; do
    export MOCK_CASE=$scenario MOCK_STATE="$test_root/$review_kind-$scenario"
    export ARTIFACTS="$MOCK_STATE/artifacts" REVIEW_WORKSPACE="$MOCK_STATE/snapshot" TOOL=mock-project COPILOT_MODEL=test-model
    export GITHUB_STEP_SUMMARY="$MOCK_STATE/summary.md"

    mkdir -p "$ARTIFACTS" "$REVIEW_WORKSPACE"
    printf 'stale successful response' > "$ARTIFACTS/$REVIEW_PREFIX.raw.json"

    status=0
    bash "scripts/TeamsApiDrift/run-$review_kind-review.sh" > "$MOCK_STATE/log" 2>&1 || status=$?

    expected_calls=1 expected_status=0

    case "$scenario" in
        correction)
            expected_calls=2
            ;;
        always-invalid)
            expected_calls=3
            expected_status=1
            ;;
        agent-failure)
            expected_status=42
            ;;
        validator-failure)
            expected_status=2
            ;;
        invalid-stream)
            expected_status=1
            ;;
    esac

    [[ "$status" == "$expected_status" && "$(cat "$MOCK_STATE/calls")" == "$expected_calls" ]] || {
        cat "$MOCK_STATE/log"
        exit 1
    }

    [[ -s "$ARTIFACTS/$REVIEW_PREFIX-agent.json" ]]
    [[ "$(cat "$GITHUB_STEP_SUMMARY")" == *test-model* ]]

    if [[ "$scenario" == agent-failure || "$scenario" == invalid-stream ]]; then
        [[ ! -s "$ARTIFACTS/$REVIEW_PREFIX.raw.json" ]]
        [[ -s "$ARTIFACTS/$REVIEW_PREFIX.attempt-1.events.jsonl" ]]
        [[ ! -f "$MOCK_STATE/validations" ]]
    elif [[ "$scenario" == validator-failure ]]; then
        [[ "$(cat "$MOCK_STATE/validations")" == 1 ]]
        [[ ! -f "$REVIEW_WORKSPACE/review-feedback.json" ]]
    else
        [[ "$(wc -l < "$MOCK_STATE/validations" | tr -d ' ')" == "$expected_calls" ]]

        for ((i=1; i<=expected_calls; i++)); do
            [[ -s "$ARTIFACTS/$REVIEW_PREFIX.attempt-$i.raw.json" && -s "$ARTIFACTS/$REVIEW_PREFIX.attempt-$i.validation.json" && -s "$ARTIFACTS/$REVIEW_PREFIX.attempt-$i.report.md" ]]
        done
    fi

    if [[ "$scenario" != agent-failure && "$scenario" != invalid-stream ]]; then
        cmp "$ARTIFACTS/$REVIEW_PREFIX.raw.json" "$ARTIFACTS/$REVIEW_PREFIX.attempt-$expected_calls.raw.json"
        [[ -s "$ARTIFACTS/$REVIEW_PREFIX.attempt-$expected_calls.raw.json.metadata.json" ]]
    fi

    printf 'PASS: %s: %s (%s attempts, exit %s)\n' "$review_kind" "$scenario" "$expected_calls" "$status"
done

done
