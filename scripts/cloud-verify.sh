#!/usr/bin/env bash
# Use the canonical Linux verifier so cloud and source checks cannot drift.
set -euo pipefail
exec bash "$(dirname "$0")/verify-source.sh" "$@"
