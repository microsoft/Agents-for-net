#!/usr/bin/env bash
# Exercise the publication scripts embedded in both workflows without GitHub calls.
set -euo pipefail

repo_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$repo_dir"

command -v jq > /dev/null || {
    echo 'jq is required for workflow tests.' >&2
    exit 1
}

temp_parent=$(cd "${TMPDIR:-/tmp}" && pwd -L)
test_root=$(mktemp -d "$temp_parent/teams-workflow-tests.XXXXXX")

cleanup() {
    case "$test_root" in
        "$temp_parent"/teams-workflow-tests.*)
            rm -rf -- "$test_root"
            ;;
    esac
}

trap cleanup EXIT

fail() {
    printf 'FAIL: %s\n' "$*" >&2
    exit 1
}

contains() {
    [[ "$1" == *"$2"* ]] || fail "Expected '$2' in: $1"
}

pass() {
    printf 'PASS: %s: %s\n' "$kind" "$1"
}

job() {
    awk -v name="$1" '$0 == "  " name ":" { found=1; next } found && /^  [a-z_]+:$/ { exit } found { print }' "$workflow"
}

step() {
    awk -v name="$1" '$0 == "      - name: " name { found=1; next } found && /^      - name:/ { exit } found { print }' "$workflow"
}

jobs() {
    JOB_RESULTS=$(jq -cn --arg same "${1:-false}" --arg v "${2:-success}" --arg d "${3:-success}" --arg a "${4:-success}" '
    {versions: {result: $v, outputs: {same: $same, from: "2.1.0", to: "2.1.2"}},
     deterministic: {result: $d, outputs: {evidence_ready: "true"}}, assessment: {result: $a}}')
    export JOB_RESULTS
}

should_run() {
    local name=$1 section condition dependencies dependency value token
    section=$(job "$name")
    condition=$(sed -n 's/^    if: //p' <<< "$section")
    [[ -n "$condition" ]] || fail "Missing condition: $name"

    if [[ "$condition" != *'always()'* ]]; then
        dependencies=$(sed -n 's/^    needs: //p' <<< "$section" | tr -d '[],')

        for dependency in $dependencies; do
            [[ "$(jq -r --arg name "$dependency" '.[$name].result' <<< "$JOB_RESULTS")" == success ]] || return 1
        done
    fi

    # Interpret the workflow's restricted string comparisons and status function.
    # Reject unknown syntax rather than pretending to implement all GitHub expressions.
    for token in needs.versions.outputs.same needs.deterministic.outputs.evidence_ready; do
        value=$(jq -r "${token#needs}" <<< "$JOB_RESULTS")
        condition=${condition//"$token"/\"$value\"}
    done

    condition=${condition//'always()'/true}
    [[ "$condition" =~ ^[\ a-zA-Z0-9\"\'=\!\&\|\(\)]+$ ]] || fail "Unsupported condition: $condition"
    eval "[[ $condition ]]"
}

new_case() {
    case_index=$((case_index + 1))
    ARTIFACTS="$test_root/$kind-$case_index"
    mkdir -p "$ARTIFACTS"

    export ARTIFACTS GITHUB_OUTPUT="$ARTIFACTS/outputs" GITHUB_STEP_SUMMARY="$ARTIFACTS/summary"
    export RUN_URL='https://github.com/southworks/Agents-for-net/actions/runs/123'
    export DOWNLOAD_RESULTS='{}' STEP_RESULTS='{}'
    jobs
}

run_report() {
    bash -e -o pipefail "$test_root/$kind-report.sh" > "$ARTIFACTS/report.log" 2>&1 || {
        cat "$ARTIFACTS/report.log"
        fail 'Report preparation failed'
    }
}

expect_gate() {
    local expected=$1 fragment=${2:-} status=0
    bash -e -o pipefail "$test_root/$kind-gate.sh" > "$ARTIFACTS/gate.log" 2>&1 || status=$?

    [[ "$status" == "$expected" ]] || {
        cat "$ARTIFACTS/gate.log"
        fail "Gate exit $status, expected $expected"
    }

    [[ -z "$fragment" ]] || contains "$(cat "$ARTIFACTS/gate.log")" "$fragment"
}

for kind in pr scheduled; do
    workflow=".github/workflows/teams-api-drift-$kind.yml"
    case_index=0

    step 'Prepare final report and pipeline diagnostics' | awk '/^        run: \|$/ { run=1; next } run { sub(/^          /, ""); print }' > "$test_root/$kind-report.sh"
    step 'Enforce publication result' | awk '/^        run: \|$/ { run=1; next } run { sub(/^          /, ""); print }' > "$test_root/$kind-gate.sh"
    [[ -s "$test_root/$kind-report.sh" && -s "$test_root/$kind-gate.sh" ]] || fail 'Missing publication shell scripts'

    jobs true success skipped skipped

    for name in deterministic assessment publish; do
        if should_run "$name"; then
            fail "Matching versions ran $name"
        fi
    done

    pass 'matching versions skip all later jobs'

    jobs false

    for name in deterministic assessment publish; do
        should_run "$name" || fail "Changed versions skipped $name"
    done

    pass 'changed versions run all jobs'

    jobs unresolved failure skipped skipped

    for name in deterministic assessment; do
        if should_run "$name"; then
            fail "Version failure ran $name"
        fi
    done

    should_run publish || fail 'Version failure skipped publication'
    contains "$(step 'Publish complete comparison and review artifacts')" 'if: always()'
    pass 'version failure still reaches final artifacts'

    jobs false success failure
    should_run assessment || fail 'Assessment cannot explain independent failure'
    JOB_RESULTS=$(jq '.deterministic.outputs.evidence_ready="false"' <<< "$JOB_RESULTS")

    if should_run assessment; then
        fail 'Assessment ran without comparison evidence'
    fi

    should_run publish || fail 'Missing comparison evidence skipped publication'
    pass 'assessment requires transferable evidence; diagnostics are still published'

    jobs false success success failure
    should_run publish || fail 'Failed assessment skipped publication'
    pass 'assessment failure still publishes'

    new_case
    jobs unresolved failure skipped skipped
    run_report

    [[ -s "$ARTIFACTS/pipeline-status.json" ]] || fail 'Missing pipeline diagnostics'
    contains "$(cat "$ARTIFACTS/impact-report.md")" 'versions: failure'
    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'did not produce a validated implementation report'
    contains "$(cat "$GITHUB_OUTPUT")" 'publish_issue=true'
    pass 'missing evidence produces fallback report, summary, and pipeline diagnostics'

    new_case
    jobs false success failure failure
    printf '# Review\nNeeds adaptation.\n' > "$ARTIFACTS/impact-report.md"
    printf '{"checks":{"build":"failure"}}' > "$ARTIFACTS/test-summary.json"
    DOWNLOAD_RESULTS='{"download_assessment":{"outcome":"failure"}}'
    run_report

    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Needs adaptation'
    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'build: failure'
    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Evidence download failed: download_assessment'
    pass 'existing report and failed independent/download outcomes survive publication'

    new_case
    printf '{broken' > "$ARTIFACTS/versions.json"
    printf '{"changes":null}' > "$ARTIFACTS/raw-api-diff.json"
    printf '{"checks":[]}' > "$ARTIFACTS/test-summary.json"
    run_report

    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Invalid JSON artifact: versions.json'
    [[ "$(jq '.errors | length' "$ARTIFACTS/pipeline-status.json")" == 3 ]] || fail 'Missing malformed-artifact diagnostics'
    pass 'malformed or incorrectly shaped JSON artifacts do not prevent diagnostics'

    new_case
    printf '{"changes":[]}' > "$ARTIFACTS/raw-api-diff.json"
    run_report

    contains "$(cat "$GITHUB_OUTPUT")" 'publish_issue=false'
    jobs false success failure
    run_report

    contains "$(tail -3 "$GITHUB_OUTPUT")" 'publish_issue=true'
    pass 'no-change issue suppression requires successful jobs'

    new_case
    jq -n '{changes: [range(0; 400) | {
        id: ("MTAPI-" + tostring), kind: "member-signature-changed",
        symbol: "Microsoft.Teams.Apps.Meetings.MeetingDetails", member: "Title",
        before: ("x" * 256), after: ("y" * 256)
    }]}' > "$ARTIFACTS/raw-api-diff.json"
    [[ "$(wc -c < "$ARTIFACTS/raw-api-diff.json")" -gt 131072 ]] || fail 'Comparison fixture must exceed the Linux per-argument limit'
    run_report

    contains "$(cat "$GITHUB_OUTPUT")" 'publish_issue=true'
    contains "$(cat "$GITHUB_OUTPUT")" 'from=2.1.0'
    contains "$(cat "$GITHUB_OUTPUT")" 'to=2.1.2'
    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Workflow job outcomes'
    [[ "$(jq '.errors | length' "$ARTIFACTS/pipeline-status.json")" == 0 ]] || fail 'Large comparison produced publication errors'
    pass 'large comparisons still produce publication outputs, summary, and diagnostics'

    new_case

    for failed_step in download_assessment report publish_comment publish_issue final_artifact; do
        STEP_RESULTS=$(jq -cn --arg name "$failed_step" '{($name): {outcome: "failure"}}')
        expect_gate 1 "$failed_step"
    done

    STEP_RESULTS='{}'
    expect_gate 0 'Report publication and final artifacts completed'
    pass 'publication fails for its own downloads, reporting, comments/issues, or artifacts'

    new_case
    jobs false success failure failure
    expect_gate 0 'Report publication and final artifacts completed'

    for validation in '{broken' 'null' '{"valid":true,"canProceed":false}' '{"valid":"true","canProceed":"false"}'; do
        printf '%s' "$validation" > "$ARTIFACTS/impact-review-validation.json"
        expect_gate 0 'Report publication and final artifacts completed'
    done

    jobs unresolved failure skipped skipped
    expect_gate 0 'Report publication and final artifacts completed'
    pass 'upstream job failures and compatibility decisions do not turn successful publication red'
done
