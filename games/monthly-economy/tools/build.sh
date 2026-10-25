#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
godot_bin="$(command -v "${GODOT_BIN:-godot}")"
target="${1:-all}"
build_dir="$project_dir/build"
version=0.2.0
stem="HoraEconomicLab-v$version"
mkdir -p "$build_dir"
if [[ "$("$godot_bin" --version)" != 4.6.3.stable* ]]; then
  printf 'Godot 4.6.3 stable is required.\n' >&2
  exit 1
fi
check_log="$(mktemp)"
trap 'rm -f "$check_log"' EXIT
checked() {
  "$@" 2>&1 | tee "$check_log"
  if grep -Eq 'SCRIPT ERROR:|Parse Error:|^ERROR:' "$check_log"; then
    printf 'Game verification reported errors; refusing to package.\n' >&2
    exit 1
  fi
}
checked "$godot_bin" --headless --path "$project_dir" --editor --import --quit
checked "$godot_bin" --headless --path "$project_dir" --script res://tests/run.gd
checked "$godot_bin" --headless --path "$project_dir" --script res://tests/test_ui.gd
checked "$godot_bin" --headless --path "$project_dir" --script res://tests/test_ui_forms.gd
checked "$godot_bin" --headless --path "$project_dir" --script res://tests/test_ui_input.gd
checked "$godot_bin" --headless --path "$project_dir" --script res://tests/test_game_flow.gd
build_linux() {
  checked "$godot_bin" --headless --path "$project_dir" --export-release "Hora Linux" "$build_dir/$stem-linux.x86_64"
  chmod +x "$build_dir/$stem-linux.x86_64"
  checked "$godot_bin" --headless --main-pack "$build_dir/$stem-linux.x86_64" --script "$project_dir/tools/verify_export.gd" -- "$project_dir"
  (cd "$build_dir" && checked "$build_dir/$stem-linux.x86_64" --headless --quit-after 10 -- --demo)
  rm -f "$build_dir/$stem-linux.zip"
  (cd "$build_dir" && zip -q "$stem-linux.zip" "$stem-linux.x86_64")
}
build_android() {
  python3 "$project_dir/tools/configure_android.py"
  # Release template, non-debuggable and intentionally UNSIGNED. Only the
  # trusted promotion job receives the retained publisher signing credential.
  checked "$godot_bin" --headless --path "$project_dir" --export-release "Hora Android" "$build_dir/$stem-UNSIGNED.apk"
  python3 "$project_dir/tools/verify_apk.py" "$build_dir/$stem-UNSIGNED.apk" --unsigned
}
artifacts=()
case "$target" in
  verify) printf 'All source verification suites passed.\n'; exit 0 ;;
  linux) build_linux; artifacts=("$stem-linux.x86_64" "$stem-linux.zip") ;;
  android) build_android; artifacts=("$stem-UNSIGNED.apk") ;;
  all) build_linux; build_android; artifacts=("$stem-linux.x86_64" "$stem-linux.zip" "$stem-UNSIGNED.apk") ;;
  *) printf 'Usage: %s [verify|linux|android|all]\n' "$0" >&2; exit 2 ;;
esac
python3 "$project_dir/tools/build_manifest.py" "$build_dir" "${artifacts[@]}"
