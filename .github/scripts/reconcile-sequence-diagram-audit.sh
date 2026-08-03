#!/usr/bin/env bash
set -euo pipefail

die() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

if [[ "$#" -ne 2 ]]; then
  die "usage: reconcile-sequence-diagram-audit.sh <copilot-output.log> <reconciled-summary.txt>"
fi

log_path="$1"
summary_path="$2"
marker='=== DIAGRAM AUDIT SUMMARY ==='
end_marker='=== END DIAGRAM AUDIT SUMMARY ==='

if [[ ! -f "$log_path" ]]; then
  die "copilot log does not exist: $log_path"
fi

if [[ -d docs ]]; then
  mapfile -t diagrams < <(find docs -maxdepth 1 -type f -name '*sequence-diagram.md' -print 2>/dev/null | LC_ALL=C sort)
else
  diagrams=()
fi

if [[ "${#diagrams[@]}" -eq 0 ]]; then
  die "no docs/*sequence-diagram.md files found"
fi

declare -A known_diagrams=()
for diagram in "${diagrams[@]}"; do
  known_diagrams["$diagram"]=1
done

summary_block="$(
  awk -v marker="$marker" -v end_marker="$end_marker" '
    $0 == marker {
      capture = 1
      complete = 0
      count = 0
      next
    }
    capture && $0 == end_marker {
      capture = 0
      complete = 1
      next
    }
    capture {
      block[count++] = $0
    }
    END {
      if (!complete) {
        exit 1
      }
      for (i = 0; i < count; ++i) {
        print block[i]
      }
    }
  ' "$log_path"
)" || die "complete audit summary block not found in $log_path"
mapfile -t summary_lines <<<"$summary_block"

declare -A verdict_types=()
declare -A verdict_reasons=()

for line in "${summary_lines[@]}"; do
  if [[ "$line" =~ ^(-\ )?(docs/[^:]+-sequence-diagram\.md):\ up\ to\ date$ ]]; then
    diagram="${BASH_REMATCH[2]}"
    verdict_type='up to date'
    verdict_reason=''
  elif [[ "$line" =~ ^(-\ )?(docs/[^:]+-sequence-diagram\.md):\ corrected\ —\ (.+)$ ]]; then
    diagram="${BASH_REMATCH[2]}"
    verdict_type='corrected'
    verdict_reason="${BASH_REMATCH[3]}"
    if [[ ! "$verdict_reason" =~ [^[:space:]] ]]; then
      die "corrected verdict reason must be non-empty for $diagram"
    fi
  else
    die "malformed verdict line: $line"
  fi

  if [[ ! -v "known_diagrams[$diagram]" ]]; then
    die "verdict references unknown diagram: $diagram"
  fi

  if [[ -v "verdict_types[$diagram]" ]]; then
    die "duplicate verdict for $diagram"
  fi

  verdict_types["$diagram"]="$verdict_type"
  verdict_reasons["$diagram"]="$verdict_reason"
done

for diagram in "${diagrams[@]}"; do
  if [[ ! -v "verdict_types[$diagram]" ]]; then
    die "missing verdict for $diagram"
  fi
done

mapfile -t changed_diagram_lines < <(git diff --name-only -- 'docs/*sequence-diagram.md')
declare -A changed_diagrams=()
for diagram in "${changed_diagram_lines[@]}"; do
  if [[ -n "$diagram" ]]; then
    changed_diagrams["$diagram"]=1
  fi
done

summary_lines_out=()
for diagram in "${diagrams[@]}"; do
  verdict_type="${verdict_types[$diagram]}"
  if [[ "$verdict_type" == 'up to date' ]]; then
    if [[ -v "changed_diagrams[$diagram]" ]]; then
      printf 'Restoring %s because the audit marked it up to date.\n' "$diagram"
      git restore -- "$diagram"
    fi
    summary_lines_out+=("$diagram: up to date")
    continue
  fi

  if [[ ! -v "changed_diagrams[$diagram]" ]]; then
    die "corrected verdict requires a diff for $diagram"
  fi

  summary_lines_out+=("$diagram: corrected — ${verdict_reasons[$diagram]}")
done

printf '%s\n' "${summary_lines_out[@]}" > "$summary_path"
