#!/usr/bin/env bash
set -euo pipefail
export REVIEW_PREFIX=behavior-review
export REVIEW_INPUT=behavior-input.json
export REVIEW_COMMAND=validate-behavior-review
export REVIEW_INSTRUCTIONS=.github/instructions/teams-behavior-review-instructions.md
bash scripts/TeamsApiDrift/run-review.sh
