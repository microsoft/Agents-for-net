#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../../.." && pwd)"
script_path="$repo_root/.github/scripts/reconcile-sequence-diagram-audit.sh"
temp_root="$repo_root/.github/scripts/tests/.reconcile-sequence-diagram-audit.$$.$RANDOM"

cleanup() {
  rm -rf "$temp_root"
}

trap cleanup EXIT INT TERM

die() {
  printf 'FAIL: %s\n' "$1" >&2
  exit 1
}

run_audit() {
  local repo="$1"

  if [[ ! -f "$script_path" ]]; then
    die "implementation script does not exist: $script_path"
  fi

  (
    cd "$repo"
    bash "$script_path" copilot-output.log audit-summary.txt
  )
}

expect_success() {
  local repo="$1"
  local label="$2"

  if ! run_audit "$repo"; then
    die "$label: expected success"
  fi
}

expect_failure() {
  local repo="$1"
  local label="$2"

  if run_audit "$repo"; then
    die "$label: expected failure"
  fi
}

create_repo() {
  local name="$1"
  local repo="$temp_root/$name"

  mkdir -p "$repo/docs"
  git -C "$repo" init -q
  git -C "$repo" config user.email "copilot@example.com"
  git -C "$repo" config user.name "Copilot"

  cat >"$repo/docs/oauth-sequence-diagram.md" <<'EOF'
# OAuth sequence diagram

```mermaid
sequenceDiagram
  participant Client
  participant Agent
  Client->>Agent: start
```
EOF

  cat >"$repo/docs/streaming-sequence-diagram.md" <<'EOF'
# Streaming sequence diagram

```mermaid
sequenceDiagram
  participant Sender
  participant Stream
  Sender->>Stream: push
```
EOF

  git -C "$repo" add docs/oauth-sequence-diagram.md docs/streaming-sequence-diagram.md
  git -C "$repo" commit -q -m "fixture"
  printf '%s\n' "$repo"
}

write_log() {
  local repo="$1"
  shift

  {
    printf 'Copilot audit log\n'
    printf '=== DIAGRAM AUDIT SUMMARY ===\n'
    for line in "$@"; do
      printf '%s\n' "$line"
    done
  } >"$repo/copilot-output.log"
}

seed_summary() {
  local repo="$1"
  printf 'stale summary\n' >"$repo/audit-summary.txt"
}

assert_summary_line() {
  local repo="$1"
  local expected="$2"

  grep -Fx -- "$expected" "$repo/audit-summary.txt" >/dev/null
}

assert_path_clean() {
  local repo="$1"
  local path="$2"

  if git -C "$repo" diff --quiet -- "$path"; then
    return 0
  fi

  die "expected clean working tree for $path"
}

assert_path_dirty() {
  local repo="$1"
  local path="$2"

  if git -C "$repo" diff --quiet -- "$path"; then
    die "expected dirty working tree for $path"
  fi
}

case_1() {
  local repo
  repo="$(create_repo "case-1")"
  printf '\nincidental working-tree edit\n' >>"$repo/docs/oauth-sequence-diagram.md"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: up to date" \
    "docs/streaming-sequence-diagram.md: up to date"

  expect_success "$repo" "case 1"
  assert_path_clean "$repo" "docs/oauth-sequence-diagram.md"
  assert_path_clean "$repo" "docs/streaming-sequence-diagram.md"
  assert_summary_line "$repo" "docs/oauth-sequence-diagram.md: up to date"
  assert_summary_line "$repo" "docs/streaming-sequence-diagram.md: up to date"
}

case_2() {
  local repo
  repo="$(create_repo "case-2")"
  printf '\nupdated turn order\n' >>"$repo/docs/streaming-sequence-diagram.md"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: up to date" \
    "docs/streaming-sequence-diagram.md: corrected — restored canonical sequence order"

  expect_success "$repo" "case 2"
  assert_path_clean "$repo" "docs/oauth-sequence-diagram.md"
  assert_path_dirty "$repo" "docs/streaming-sequence-diagram.md"
  assert_summary_line "$repo" "docs/streaming-sequence-diagram.md: corrected — restored canonical sequence order"
}

case_3() {
  local repo
  repo="$(create_repo "case-3")"
  printf '\nupdated turn order\n' >>"$repo/docs/streaming-sequence-diagram.md"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: up to date"

  expect_failure "$repo" "case 3"
}

case_4() {
  local repo
  repo="$(create_repo "case-4")"
  printf '\nupdated turn order\n' >>"$repo/docs/oauth-sequence-diagram.md"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: corrected — restore streaming handoff" \
    "docs/oauth-sequence-diagram.md: corrected — keep canonical order" \
    "docs/streaming-sequence-diagram.md: up to date"

  expect_failure "$repo" "case 4"
}

case_5() {
  local repo
  repo="$(create_repo "case-5")"
  printf '\nupdated turn order\n' >>"$repo/docs/streaming-sequence-diagram.md"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: up to date" \
    "docs/streaming-sequence-diagram.md: corrected —"

  expect_failure "$repo" "case 5"
}

case_6() {
  local repo
  repo="$(create_repo "case-6")"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: up to date" \
    "docs/streaming-sequence-diagram.md: corrected — no diff exists"

  expect_failure "$repo" "case 6"
}

case_7() {
  local repo
  repo="$(create_repo "case-7")"
  seed_summary "$repo"
  write_log "$repo" \
    "docs/oauth-sequence-diagram.md: up to date" \
    "docs/streaming-sequence-diagram.md: up to date"

  expect_success "$repo" "case 7"
  assert_path_clean "$repo" "docs/oauth-sequence-diagram.md"
  assert_path_clean "$repo" "docs/streaming-sequence-diagram.md"
  assert_summary_line "$repo" "docs/oauth-sequence-diagram.md: up to date"
  assert_summary_line "$repo" "docs/streaming-sequence-diagram.md: up to date"
}

mkdir -p "$temp_root"

case_1
case_2
case_3
case_4
case_5
case_6
case_7

printf 'PASS: 7 reconciliation tests\n'
