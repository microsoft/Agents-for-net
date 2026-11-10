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

    assessment_gate=$(step 'Enforce implementation review' | awk '/^        run: \|$/ { run=1; next } run && /^  [a-z_]+:$/ { exit } run { sub(/^          /, ""); print }' |
        sed -E 's/\$\{\{ steps\.[^}]+\.outcome \}\}/success/g; s/\$\{\{ steps\.prepare-review\.outputs\.count \}\}/1/g')
    [[ -n "$assessment_gate" && "$assessment_gate" != *'${{'* ]] || fail 'Missing or unsupported assessment gate'
    export CAN_REVIEW=true
    for scenario in safe upstream-failure findings; do
        new_case
        case "$scenario" in
            safe) printf '{"canProceed":true,"summary":{"safe":1,"needsAdaptation":0,"needsHumanReview":0},"decisions":[]}' ;;
            upstream-failure) printf '{"canProceed":false,"summary":{"safe":1,"needsAdaptation":0,"needsHumanReview":0},"decisions":[]}' ;;
            findings) printf '{"canProceed":false,"summary":{"safe":0,"needsAdaptation":1,"needsHumanReview":1},"decisions":[{"changeId":"change-1","verdict":"needs-adaptation","reason":"Adapt the integration."}]}' ;;
        esac > "$ARTIFACTS/impact-review-validation.json"
        printf '%s\n' "$assessment_gate" > "$test_root/$kind-assessment.sh"
        bash -e -o pipefail "$test_root/$kind-assessment.sh" > "$ARTIFACTS/assessment.log" 2>&1 || {
            cat "$ARTIFACTS/assessment.log"
            fail "Assessment inherited $scenario as a job failure"
        }
        if [[ "$scenario" == findings ]]; then
            contains "$(cat "$ARTIFACTS/assessment.log")" 'change-1: needs-adaptation'
        fi
    done
    pass 'assessment reports findings and upstream failures without inheriting them'

    for failed_check in review-tool evidence snapshot review-validation assessment-transfer agent-install implementation-review; do
        failing_gate=${assessment_gate//"check $failed_check \"success\""/"check $failed_check \"failure\""}
        printf '%s\n' "$failing_gate" > "$test_root/$kind-assessment.sh"
        if bash -e -o pipefail "$test_root/$kind-assessment.sh" > "$ARTIFACTS/assessment.log" 2>&1; then
            fail "Assessment ignored its own failed $failed_check step"
        fi
        contains "$(cat "$ARTIFACTS/assessment.log")" "Implementation review FAILED: $failed_check"
    done
    printf '%s\n' "$assessment_gate" > "$test_root/$kind-assessment.sh"
    mv "$ARTIFACTS/impact-review-validation.json" "$ARTIFACTS/impact-review-validation.saved.json"
    if bash -e -o pipefail "$test_root/$kind-assessment.sh" > "$ARTIFACTS/assessment.log" 2>&1; then
        fail 'Assessment ignored missing review output'
    fi
    contains "$(cat "$ARTIFACTS/assessment.log")" 'missing-impact-review'
    pass 'assessment fails for each required local step and missing review output'

    if [[ "$kind" == scheduled ]]; then
        contains "$(step 'Resolve baseline and candidate')" 'DISPATCH_FROM_VERSION: ${{ inputs.from_version }}'
        contains "$(step 'Resolve baseline and candidate')" 'DISPATCH_TO_VERSION: ${{ inputs.to_version }}'
        step 'Resolve baseline and candidate' | awk '/^        run: \|$/ { run=1; next } run { sub(/^          /, ""); print }' > "$test_root/scheduled-versions.sh"
        (
            # Exercise the actual workflow resolver without network or package downloads.
            dotnet() {
                while [[ "${1:-}" != -- ]]; do
                    (( $# )) || return 92
                    shift
                done
                shift
                case "$1" in
                    resolve-version)
                        printf 'declared\n' >> "$ARTIFACTS/resolver-calls"
                        printf '2.1.2\n'
                        ;;
                    resolve-comparison)
                        shift
                        local from='' to='' output=''
                        while (( $# )); do
                            case "$1" in
                                --from) from=$2 ;;
                                --to) to=$2 ;;
                                --output) output=$2 ;;
                                *) return 93 ;;
                            esac
                            shift 2
                        done
                        if [[ -z "$to" ]]; then
                            printf 'latest\n' >> "$ARTIFACTS/resolver-calls"
                            to=2.3.0
                        fi
                        jq -n --arg from "$from" --arg to "$to"                             '{fromVersion: $from, toVersion: $to, sameVersion: ($from == $to)}' > "$output"
                        ;;
                    *) return 94 ;;
                esac
            }
            export -f dotnet
            export TOOL=mock-project
            for pair in '|' '2.0.0|' '|2.2.0' '2.0.0|2.2.0' '2.2.0|2.2.0'; do
                new_case
                IFS='|' read -r DISPATCH_FROM_VERSION DISPATCH_TO_VERSION <<< "$pair"
                export DISPATCH_FROM_VERSION DISPATCH_TO_VERSION
                bash -e -o pipefail "$test_root/scheduled-versions.sh"
                expected_from=${DISPATCH_FROM_VERSION:-2.1.2}
                expected_to=${DISPATCH_TO_VERSION:-2.3.0}
                contains "$(cat "$GITHUB_OUTPUT")" "from=$expected_from"
                contains "$(cat "$GITHUB_OUTPUT")" "to=$expected_to"
                expected_same=false
                [[ "$expected_from" != "$expected_to" ]] || expected_same=true
                contains "$(cat "$GITHUB_OUTPUT")" "same=$expected_same"
                if [[ -n "$DISPATCH_FROM_VERSION" && -n "$DISPATCH_TO_VERSION" ]]; then
                    [[ ! -f "$ARTIFACTS/resolver-calls" ]] || fail 'Explicit versions queried defaults'
                else
                    expected_calls=0
                    if [[ -z "$DISPATCH_FROM_VERSION" ]]; then
                        contains "$(cat "$ARTIFACTS/resolver-calls")" declared
                        expected_calls=$((expected_calls + 1))
                    fi
                    if [[ -z "$DISPATCH_TO_VERSION" ]]; then
                        contains "$(cat "$ARTIFACTS/resolver-calls")" latest
                        expected_calls=$((expected_calls + 1))
                    fi
                    [[ "$(wc -l < "$ARTIFACTS/resolver-calls" | tr -d ' ')" == "$expected_calls" ]] || fail 'Unexpected default resolution'
                fi
            done
        )
        pass 'dispatch versions independently override defaults and preserve equality'

        contains "$(step 'Validate baseline manifest')" 'EVENT_NAME: ${{ github.event_name }}'
        contains "$(step 'Validate baseline manifest')" 'DISPATCH_FROM_VERSION: ${{ inputs.from_version }}'
        step 'Validate baseline manifest' | awk '/^        run: \|$/ { run=1; next } run { sub(/^          /, ""); print }' > "$test_root/scheduled-manifest.sh"
        (
            dotnet() { printf '%s\n' "$@" > "$ARTIFACTS/manifest-arguments"; }
            export -f dotnet
            export TOOL=mock-project MANIFEST=mock-manifest GITHUB_WORKSPACE=mock-root
            for pair in 'schedule|' 'schedule|2.0.0' 'workflow_dispatch|' 'workflow_dispatch|2.0.0'; do
                new_case
                IFS='|' read -r EVENT_NAME DISPATCH_FROM_VERSION <<< "$pair"
                export EVENT_NAME DISPATCH_FROM_VERSION
                bash -e -o pipefail "$test_root/scheduled-manifest.sh"
                arguments=$(cat "$ARTIFACTS/manifest-arguments")
                contains "$arguments" validate-usage
                if [[ "$pair" == 'workflow_dispatch|2.0.0' ]]; then
                    contains "$arguments" --skip-manifest-version-check
                else
                    [[ "$arguments" != *--skip-manifest-version-check* ]] || fail 'Default run skipped manifest version validation'
                fi
            done
        )
        pass 'only dispatch with an explicit baseline skips manifest version validation'
    fi

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

    if [[ "$kind" == scheduled ]]; then
        jobs true success skipped skipped
        if should_run behavior_analysis; then fail 'Matching versions ran behavior analysis'; fi
        jobs false success failure failure
        should_run behavior_analysis || fail 'API failures skipped behavior analysis'
        contains "$(job behavior_analysis)" 'needs: versions'
        contains "$(step 'Collect behavior diagnostics')" 'needs.behavior_analysis.outputs.artifact_id'
        jobs unresolved failure skipped skipped
        if should_run behavior_analysis; then fail 'Version resolution failure ran behavior analysis'; fi
        pass 'behavior analysis depends only on changed resolved versions'

        new_case
        printf '{"changes":[]}' > "$ARTIFACTS/raw-api-diff.json"
        mkdir -p "$ARTIFACTS/behavior"
        printf '## Teams specific behavior analysis\nAdvisory difference.\n' > "$ARTIFACTS/behavior/behavior-report.md"
        printf '{"schemaVersion":1,"valid":true,"hasFindings":true}' > "$ARTIFACTS/behavior/behavior-review-validation.json"
        printf '# Implementation impact review\nAPI findings.\n\n## Independent check outcomes\nCheck details.\n' > "$ARTIFACTS/impact-report.md"
        run_report
        issue_report=$(cat "$ARTIFACTS/issue-report.md")
        contains "$issue_report" 'API findings.'
        contains "$issue_report" 'Teams specific behavior analysis'
        contains "$issue_report" 'Advisory difference'
        for removed in 'Teams wire-behavior summary' 'Independent check outcomes' 'Workflow job outcomes' 'Recorded independent checks'; do
            [[ "$issue_report" != *"$removed"* ]] || fail "Issue retained $removed"
        done
        [[ "$issue_report" == '# Implementation impact review'* ]] || fail 'Issue must start with implementation review'
        contains "$(step 'Create or update implementation review issue')" 'REPORT_PATH: artifacts/teams-api-drift/issue-report.md'
        contains "$(cat "$GITHUB_OUTPUT")" 'publish_issue=true'
        contains "$(cat "$ARTIFACTS/impact-report.md")" 'Advisory difference'
        contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Report publication'
        [[ "$(cat "$GITHUB_STEP_SUMMARY")" != *'Advisory difference'* ]] || fail 'Publication duplicated behavior analysis'
        [[ "$(cat "$GITHUB_STEP_SUMMARY")" != *'Workflow job outcomes'* ]] || fail 'Publication duplicated upstream job outcomes'
        contains "$(step 'Validate and render advisory behavior report')" 'cat "$ARTIFACTS/behavior-report.md" >> "$GITHUB_STEP_SUMMARY"'
        contains "$(step 'Enforce deterministic checks')" 'Deterministic API checks'
        pass 'behavior findings publish with an empty API diff'

        new_case
        JOB_RESULTS=$(jq '. + {behavior_analysis: {result: "failure"}}' <<< "$JOB_RESULTS")
        should_run publish || fail 'Behavior failure skipped publication'
        run_report
        contains "$(cat "$GITHUB_OUTPUT")" 'publish_issue=true'
        contains "$(cat "$ARTIFACTS/impact-report.md")" 'behavior_analysis: failure'
        pass 'behavior failure preserves publication diagnostics'
    fi


    new_case
    jobs unresolved failure skipped skipped
    run_report

    [[ -s "$ARTIFACTS/pipeline-status.json" ]] || fail 'Missing pipeline diagnostics'
    contains "$(cat "$ARTIFACTS/impact-report.md")" 'versions: failure'
    contains "$(cat "$ARTIFACTS/impact-report.md")" 'did not produce a validated implementation report'
    contains "$(cat "$GITHUB_OUTPUT")" 'publish_issue=true'
    pass 'missing evidence produces fallback report, summary, and pipeline diagnostics'

    new_case
    jobs false success failure failure
    printf '# Review\nNeeds adaptation.\n' > "$ARTIFACTS/impact-report.md"
    printf '{"checks":{"build":"failure"}}' > "$ARTIFACTS/test-summary.json"
    DOWNLOAD_RESULTS='{"download_assessment":{"outcome":"failure"}}'
    run_report

    contains "$(cat "$ARTIFACTS/impact-report.md")" 'Needs adaptation'
    contains "$(cat "$ARTIFACTS/impact-report.md")" 'build: failure'
    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Report publication'
    contains "$(cat "$GITHUB_STEP_SUMMARY")" 'Evidence download failed: download_assessment'
    [[ "$(cat "$GITHUB_STEP_SUMMARY")" != *'Needs adaptation'* ]] || fail 'Publication duplicated API analysis'
    [[ "$(cat "$GITHUB_STEP_SUMMARY")" != *'build: failure'* ]] || fail 'Publication duplicated deterministic checks'
    contains "$(step 'Enforce deterministic checks')" 'Deterministic API checks'
    if [[ "$kind" == pr ]]; then
        contains "$(step 'Publish implementation review comment')" 'Published review comment'
        contains "$(job publish)" 'id: publish_comment'
    fi
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
    contains "$(cat "$ARTIFACTS/impact-report.md")" 'Workflow job outcomes'
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
