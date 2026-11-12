#!/usr/bin/env bash
set -euo pipefail
export REVIEW_PREFIX=impact-review
export REVIEW_INPUT=review-input.json
export REVIEW_COMMAND=validate-impact-review
export REVIEW_INSTRUCTIONS=.github/instructions/teams-api-impact-review-instructions.md
bash scripts/TeamsApiDrift/run-review.sh
