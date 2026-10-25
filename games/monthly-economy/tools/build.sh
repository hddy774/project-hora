#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
godot_bin="${GODOT_BIN:-godot}"
godot_bin="$(command -v "$godot_bin")"
target="${1:-linux}"
build_dir="$project_dir/build"
mkdir -p "$build_dir"
if [[ "$("$godot_bin" --version)" != 4.6.3.stable* ]]; then
  printf 'Godot 4.6.3 stable is required.\n' >&2
  exit 1
fi
import_log="$(mktemp)"
trap 'rm -f "$import_log"' EXIT
"$godot_bin" --headless --path "$project_dir" --editor --import --quit 2>&1 | tee "$import_log"
if grep -Eq 'SCRIPT ERROR:|^ERROR:' "$import_log"; then
  printf 'Godot import reported errors; refusing to package the project.\n' >&2
  exit 1
fi
"$godot_bin" --headless --path "$project_dir" --script res://tests/run.gd
"$godot_bin" --headless --path "$project_dir" --script res://tests/test_ui.gd
"$godot_bin" --headless --path "$project_dir" --script res://tests/test_game_flow.gd
build_linux() {
    "$godot_bin" --headless --path "$project_dir" --export-release "Linux experimental" "$build_dir/monthly-economy-linux.x86_64"
    chmod +x "$build_dir/monthly-economy-linux.x86_64"
    (cd "$build_dir" && "$godot_bin" --headless --main-pack "$build_dir/monthly-economy-linux.x86_64" --script "$project_dir/tools/verify_export.gd" -- "$project_dir")
    (cd "$build_dir" && "$build_dir/monthly-economy-linux.x86_64" --headless --quit-after 10 -- --demo) 2>&1 | tee "$import_log"
    if grep -Eq 'SCRIPT ERROR:|^ERROR:' "$import_log"; then
      printf 'Exported Linux runtime reported errors.\n' >&2
      exit 1
    fi
    (cd "$build_dir" && zip -q monthly-economy-linux-experimental.zip monthly-economy-linux.x86_64)
}
build_android() {
    python3 "$project_dir/tools/configure_android.py"
    # Godot creates its standard local debug keystore. This is an experimental
    # package with a distinct identity, never an update to Alpha Exchange.
    "$godot_bin" --headless --path "$project_dir" --export-debug "Android experimental" "$build_dir/monthly-economy-experimental.apk"
    python3 "$project_dir/tools/verify_apk.py" "$build_dir/monthly-economy-experimental.apk"
}
artifacts=()
case "$target" in
  linux) build_linux; artifacts=(monthly-economy-linux.x86_64 monthly-economy-linux-experimental.zip) ;;
  android) build_android; artifacts=(monthly-economy-experimental.apk) ;;
  all) build_linux; build_android; artifacts=(monthly-economy-linux.x86_64 monthly-economy-linux-experimental.zip monthly-economy-experimental.apk) ;;
  *) printf 'Usage: %s [linux|android|all]\n' "$0" >&2; exit 2 ;;
esac
python3 "$project_dir/tools/build_manifest.py" "$build_dir" "${artifacts[@]}"
