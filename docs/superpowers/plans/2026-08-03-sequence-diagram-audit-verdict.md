# Sequence Diagram Audit Verdict Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent the sequence-diagram workflow from publishing incidental edits that Copilot itself classifies as `up to date`.

**Architecture:** Add a focused Bash reconciliation script that extracts the final audit summary, validates exactly one verdict per diagram, restores diffs marked `up to date`, and fails closed on inconsistent verdicts. Test the script in temporary Git repositories, then wire it into the workflow before Mermaid linting and issue creation.

**Tech Stack:** Bash, Git, GitHub Actions YAML, GitHub Copilot CLI prompt text

---

## File Structure

- Create `.github/scripts/reconcile-sequence-diagram-audit.sh`: Extract and enforce audit verdicts against the Git working tree.
- Create `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`: Dependency-free Bash tests using temporary Git repositories.
- Modify `.github/prompts/verify-sequence-diagrams.prompt.md`: Require exact machine-readable verdict lines.
- Modify `.github/workflows/verify-sequence-diagrams.yml`: Reconcile verdicts before linting and publish only the reconciled summary.

### Task 1: Add Failing Reconciliation Tests

**Files:**
- Create: `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`
- Test: `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`

- [ ] **Step 1: Create the test harness and behavior cases**

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
script="$repo_root/.github/scripts/reconcile-sequence-diagram-audit.sh"
tests_run=0

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

assert_equals() {
  local expected="$1"
  local actual="$2"
  local message="$3"
  [[ "$actual" == "$expected" ]] || fail "$message: expected '$expected', got '$actual'"
}

new_repo() {
  local root
  root="$(mktemp -d)"
  git -C "$root" init --quiet
  git -C "$root" config user.name "Sequence Audit Test"
  git -C "$root" config user.email "sequence-audit@example.invalid"
  mkdir -p "$root/docs"
  printf 'original oauth\n' > "$root/docs/oauth-sequence-diagram.md"
  printf 'original streaming\n' > "$root/docs/streaming-sequence-diagram.md"
  git -C "$root" add docs
  git -C "$root" commit --quiet -m "test fixture"
  printf '%s\n' "$root"
}

write_log() {
  local root="$1"
  shift
  {
    echo "Copilot output before summary"
    echo "=== DIAGRAM AUDIT SUMMARY ==="
    printf '%s\n' "$@"
  } > "$root/copilot-output.log"
}

run_reconcile() {
  local root="$1"
  (
    cd "$root"
    "$script" copilot-output.log audit-summary.txt
  )
}

test_up_to_date_diff_is_restored() {
  local root
  root="$(new_repo)"
  printf 'original oauth,\n' > "$root/docs/oauth-sequence-diagram.md"
  write_log "$root" \
    "- docs/oauth-sequence-diagram.md: up to date" \
    "- docs/streaming-sequence-diagram.md: up to date"

  run_reconcile "$root"

  assert_equals "" "$(git -C "$root" status --short)" "up-to-date diff should be restored"
  grep -Fxq -- "- docs/oauth-sequence-diagram.md: up to date" "$root/audit-summary.txt" ||
    fail "canonical summary should retain the up-to-date verdict"
  rm -rf "$root"
}

test_corrected_diff_is_retained() {
  local root
  root="$(new_repo)"
  printf 'corrected streaming\n' > "$root/docs/streaming-sequence-diagram.md"
  write_log "$root" \
    "- docs/oauth-sequence-diagram.md: up to date" \
    "- docs/streaming-sequence-diagram.md: corrected — timeout branch changed"

  run_reconcile "$root"

  assert_equals " M docs/streaming-sequence-diagram.md" \
    "$(git -C "$root" status --short)" "corrected diff should remain"
  grep -Fxq -- "- docs/streaming-sequence-diagram.md: corrected — timeout branch changed" \
    "$root/audit-summary.txt" || fail "canonical summary should retain the correction reason"
  rm -rf "$root"
}

test_missing_verdict_fails() {
  local root
  root="$(new_repo)"
  printf 'changed oauth\n' > "$root/docs/oauth-sequence-diagram.md"
  write_log "$root" "- docs/streaming-sequence-diagram.md: up to date"

  if run_reconcile "$root"; then
    fail "missing verdict should fail"
  fi
  rm -rf "$root"
}

test_duplicate_verdict_fails() {
  local root
  root="$(new_repo)"
  write_log "$root" \
    "- docs/oauth-sequence-diagram.md: up to date" \
    "- docs/oauth-sequence-diagram.md: corrected — contradictory duplicate" \
    "- docs/streaming-sequence-diagram.md: up to date"

  if run_reconcile "$root"; then
    fail "duplicate verdict should fail"
  fi
  rm -rf "$root"
}

test_malformed_changed_verdict_fails() {
  local root
  root="$(new_repo)"
  printf 'changed oauth\n' > "$root/docs/oauth-sequence-diagram.md"
  write_log "$root" \
    "- docs/oauth-sequence-diagram.md: corrected" \
    "- docs/streaming-sequence-diagram.md: up to date"

  if run_reconcile "$root"; then
    fail "malformed corrected verdict should fail"
  fi
  rm -rf "$root"
}

test_corrected_without_diff_fails() {
  local root
  root="$(new_repo)"
  write_log "$root" \
    "- docs/oauth-sequence-diagram.md: corrected — claims a change that does not exist" \
    "- docs/streaming-sequence-diagram.md: up to date"

  if run_reconcile "$root"; then
    fail "corrected verdict without a diff should fail"
  fi
  rm -rf "$root"
}

test_clean_audit_stays_clean() {
  local root
  root="$(new_repo)"
  write_log "$root" \
    "- docs/oauth-sequence-diagram.md: up to date" \
    "- docs/streaming-sequence-diagram.md: up to date"

  run_reconcile "$root"

  assert_equals "" "$(git -C "$root" status --short)" "clean audit should remain clean"
  rm -rf "$root"
}

for test_case in \
  test_up_to_date_diff_is_restored \
  test_corrected_diff_is_retained \
  test_missing_verdict_fails \
  test_duplicate_verdict_fails \
  test_malformed_changed_verdict_fails \
  test_corrected_without_diff_fails \
  test_clean_audit_stays_clean; do
  "$test_case"
  tests_run=$((tests_run + 1))
done

echo "PASS: $tests_run reconciliation tests"
```

- [ ] **Step 2: Make the test executable**

Run:

```bash
git update-index --add --chmod=+x .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected: no output.

- [ ] **Step 3: Run the test to verify it fails before implementation**

Run:

```bash
bash .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected: FAIL because `.github/scripts/reconcile-sequence-diagram-audit.sh` does not exist.

- [ ] **Step 4: Commit the failing tests**

```bash
git add .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
git commit -m "test: define sequence audit verdict contract" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" \
  -m "Copilot-Session: 821adcc6-6ee9-488d-8121-0172b1306076"
```

### Task 2: Implement Verdict Reconciliation

**Files:**
- Create: `.github/scripts/reconcile-sequence-diagram-audit.sh`
- Test: `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`

- [ ] **Step 1: Implement the reconciliation script**

```bash
#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <copilot-output.log> <reconciled-summary.txt>" >&2
  exit 2
fi

copilot_log="$1"
summary_output="$2"
marker="=== DIAGRAM AUDIT SUMMARY ==="

[[ -f "$copilot_log" ]] || {
  echo "Audit log not found: $copilot_log" >&2
  exit 1
}

mapfile -t diagrams < <(
  find docs -maxdepth 1 -type f -name '*sequence-diagram.md' -print | sort
)

if [[ "${#diagrams[@]}" -eq 0 ]]; then
  echo "No sequence diagrams found under docs/." >&2
  exit 1
fi

summary_block="$(awk -v marker="$marker" '
  $0 == marker {
    found = 1
    block = ""
    next
  }
  found {
    block = block $0 ORS
  }
  END {
    if (!found) {
      exit 1
    }
    printf "%s", block
  }
' "$copilot_log")" || {
  echo "Audit summary marker not found in $copilot_log." >&2
  exit 1
}

declare -A verdicts=()
declare -A reasons=()
declare -A counts=()

while IFS= read -r line; do
  if [[ "$line" =~ ^-\ (docs/[^:]+-sequence-diagram\.md):\ up\ to\ date$ ]]; then
    diagram="${BASH_REMATCH[1]}"
    verdicts["$diagram"]="up to date"
    counts["$diagram"]=$(( ${counts["$diagram"]:-0} + 1 ))
  elif [[ "$line" =~ ^-\ (docs/[^:]+-sequence-diagram\.md):\ corrected\ —\ (.+)$ ]]; then
    diagram="${BASH_REMATCH[1]}"
    verdicts["$diagram"]="corrected"
    reasons["$diagram"]="${BASH_REMATCH[2]}"
    counts["$diagram"]=$(( ${counts["$diagram"]:-0} + 1 ))
  fi
done <<< "$summary_block"

declare -A known_diagrams=()
for diagram in "${diagrams[@]}"; do
  known_diagrams["$diagram"]=1
done

for diagram in "${!counts[@]}"; do
  if [[ -z "${known_diagrams["$diagram"]:-}" ]]; then
    echo "Audit summary contains an unknown diagram: $diagram" >&2
    exit 1
  fi
done

mapfile -t changed < <(git diff --name-only -- 'docs/*sequence-diagram.md')
declare -A changed_diagrams=()
for diagram in "${changed[@]}"; do
  changed_diagrams["$diagram"]=1
done

errors=0
: > "$summary_output"

for diagram in "${diagrams[@]}"; do
  count="${counts["$diagram"]:-0}"
  if [[ "$count" -ne 1 ]]; then
    echo "Expected exactly one valid verdict for $diagram; found $count." >&2
    errors=1
    continue
  fi

  verdict="${verdicts["$diagram"]}"
  case "$verdict" in
    "up to date")
      if [[ -n "${changed_diagrams["$diagram"]:-}" ]]; then
        echo "Restoring incidental diff for up-to-date diagram: $diagram"
        git restore -- "$diagram"
      fi
      printf '%s\n' "- $diagram: up to date" >> "$summary_output"
      ;;
    "corrected")
      if [[ -z "${changed_diagrams["$diagram"]:-}" ]]; then
        echo "Diagram is marked corrected but has no diff: $diagram" >&2
        errors=1
        continue
      fi
      printf '%s\n' "- $diagram: corrected — ${reasons["$diagram"]}" >> "$summary_output"
      ;;
  esac
done

if [[ "$errors" -ne 0 ]]; then
  exit 1
fi
```

- [ ] **Step 2: Make the implementation executable**

Run:

```bash
git update-index --add --chmod=+x .github/scripts/reconcile-sequence-diagram-audit.sh
```

Expected: no output.

- [ ] **Step 3: Run the reconciliation tests**

Run:

```bash
bash .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected:

```text
PASS: 7 reconciliation tests
```

- [ ] **Step 4: Commit the implementation**

```bash
git add .github/scripts/reconcile-sequence-diagram-audit.sh
git commit -m "fix: enforce sequence audit verdicts" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" \
  -m "Copilot-Session: 821adcc6-6ee9-488d-8121-0172b1306076"
```

### Task 3: Require the Exact Verdict Protocol

**Files:**
- Modify: `.github/prompts/verify-sequence-diagrams.prompt.md`
- Test: `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`

- [ ] **Step 1: Add a prompt-format assertion to the test script**

Add before the test-case loop:

```bash
test_prompt_defines_exact_verdicts() {
  local prompt="$repo_root/.github/prompts/verify-sequence-diagrams.prompt.md"
  grep -Fxq -- "- docs/<name>-sequence-diagram.md: up to date" "$prompt" ||
    fail "prompt should define the exact up-to-date verdict"
  grep -Fxq -- "- docs/<name>-sequence-diagram.md: corrected — <specific reason>" "$prompt" ||
    fail "prompt should define the exact corrected verdict"
}
```

Add `test_prompt_defines_exact_verdicts` to the beginning of the test-case list.

- [ ] **Step 2: Run the test to verify the prompt assertion fails**

Run:

```bash
bash .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected: FAIL with `prompt should define the exact up-to-date verdict`.

- [ ] **Step 3: Replace the prompt's Output section with a strict contract**

```markdown
## Output

After you finish, print exactly one verdict line for every `docs/*sequence-diagram.md` file
under a line containing only:

`=== DIAGRAM AUDIT SUMMARY ===`

Each verdict must use exactly one of these forms:

```text
- docs/<name>-sequence-diagram.md: up to date
- docs/<name>-sequence-diagram.md: corrected — <specific reason>
```

Use `up to date` whenever the final file should be byte-for-byte unchanged. If you made a
temporary or cosmetic edit but found no factual discrepancy, restore the file and report
`up to date`. Use `corrected` only when the working tree contains a real correction, and state
the specific implementation fact that required it. Do not add Markdown emphasis, extra bullets,
source excerpts, or tool output to verdict lines.
```

- [ ] **Step 4: Run the tests**

Run:

```bash
bash .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected:

```text
PASS: 8 reconciliation tests
```

- [ ] **Step 5: Commit the prompt contract**

```bash
git add .github/prompts/verify-sequence-diagrams.prompt.md \
  .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
git commit -m "docs: define sequence audit verdict protocol" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" \
  -m "Copilot-Session: 821adcc6-6ee9-488d-8121-0172b1306076"
```

### Task 4: Enforce Reconciliation in the Workflow

**Files:**
- Modify: `.github/workflows/verify-sequence-diagrams.yml`
- Test: `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`

- [ ] **Step 1: Add reconciliation immediately after the Copilot audit**

Insert after `Audit diagrams against implementation` and before Mermaid linting:

```yaml
      - name: Reconcile audit verdicts
        run: |
          .github/scripts/reconcile-sequence-diagram-audit.sh \
            copilot-output.log \
            audit-summary.txt
```

- [ ] **Step 2: Remove the old best-effort summary extraction**

Delete this block from `Open issue for changed diagrams`:

```bash
          # Audit summary block (content after the LAST marker; Copilot may run more than
          # once due to the self-correction loop).
          if grep -q "=== DIAGRAM AUDIT SUMMARY ===" copilot-output.log; then
            tac copilot-output.log | sed -n '/=== DIAGRAM AUDIT SUMMARY ===/q;p' | tac > audit-summary.txt
          else
            : > audit-summary.txt
          fi
```

The issue step must consume the already reconciled `audit-summary.txt`.

- [ ] **Step 3: Make issue notes use exact per-file verdict lines**

Replace:

```bash
            note=$(grep -F "$f" audit-summary.txt || true)
```

with:

```bash
            note=$(grep -F -- "- $f: corrected — " audit-summary.txt || true)
```

The exact prefix is safe because reconciliation guarantees one canonical verdict per diagram.

- [ ] **Step 4: Update the workflow comments to describe enforced reconciliation**

Change the header comments to state that model edits are reconciled against explicit verdicts and
that `up to date` diffs are restored before linting or issue creation.

- [ ] **Step 5: Run targeted validation**

Run:

```bash
bash .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
git diff --check
```

Expected:

```text
PASS: 8 reconciliation tests
```

`git diff --check` produces no output.

- [ ] **Step 6: Inspect the final workflow diff**

Run:

```bash
git --no-pager diff -- \
  .github/workflows/verify-sequence-diagrams.yml \
  .github/prompts/verify-sequence-diagrams.prompt.md \
  .github/scripts/reconcile-sequence-diagram-audit.sh \
  .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected: reconciliation runs before linting, the old summary extraction is absent, and issue
creation reads only `audit-summary.txt`.

- [ ] **Step 7: Commit workflow integration**

```bash
git add .github/workflows/verify-sequence-diagrams.yml
git commit -m "ci: reject inconsistent diagram audit diffs" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" \
  -m "Copilot-Session: 821adcc6-6ee9-488d-8121-0172b1306076"
```

### Task 5: Verify the Issue #954 Regression

**Files:**
- Test: `.github/scripts/tests/reconcile-sequence-diagram-audit.test.sh`
- Verify: `.github/scripts/reconcile-sequence-diagram-audit.sh`
- Verify: `.github/workflows/verify-sequence-diagrams.yml`

- [ ] **Step 1: Add the exact punctuation regression fixture**

Rename `test_up_to_date_diff_is_restored` to
`test_issue_954_up_to_date_punctuation_diff_is_restored` and make the committed fixture line:

```bash
printf 'Note over UA: InvokeResponse is ensured; handler may still run\n' \
  > "$root/docs/oauth-sequence-diagram.md"
git -C "$root" add docs/oauth-sequence-diagram.md
git -C "$root" commit --quiet -m "issue 954 fixture"
```

Then replace it in the working tree with:

```bash
printf 'Note over UA: InvokeResponse is ensured, handler may still run\n' \
  > "$root/docs/oauth-sequence-diagram.md"
```

Keep the verdict `- docs/oauth-sequence-diagram.md: up to date`.

- [ ] **Step 2: Run the regression and contract tests**

Run:

```bash
bash .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
```

Expected:

```text
PASS: 8 reconciliation tests
```

- [ ] **Step 3: Run final repository checks**

Run:

```bash
git diff --check
git status --short
```

Expected: `git diff --check` produces no output. `git status --short` lists only the intended
uncommitted regression-test refinement before the final commit.

- [ ] **Step 4: Commit the regression refinement**

```bash
git add .github/scripts/tests/reconcile-sequence-diagram-audit.test.sh
git commit -m "test: cover issue 954 audit churn" \
  -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" \
  -m "Copilot-Session: 821adcc6-6ee9-488d-8121-0172b1306076"
```

- [ ] **Step 5: Confirm the branch is clean**

Run:

```bash
git status --short
```

Expected: no output.
