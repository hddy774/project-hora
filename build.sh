#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
# Retain the old no-argument entry point while isolating each game's outputs.
game="${1:-alpha-exchange}"
if (( $# > 1 )); then echo 'Usage: ./build.sh [game-slug]' >&2; exit 2; fi
python3 scripts/games.py check "$game"
python3 scripts/games.py build "$game"
