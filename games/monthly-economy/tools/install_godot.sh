#!/usr/bin/env bash
set -euo pipefail

# Official pinned Godot binaries. SDKs, templates and caches stay outside git.
version=4.6.3
release_base="https://github.com/godotengine/godot-builds/releases/download/${version}-stable"
task_cache="${HORA_TOOL_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/hora-tools}"
task_data="${XDG_DATA_HOME:-$HOME/.local/share}/godot/export_templates/${version}.stable"
mkdir -p "$task_cache" "$task_data"

download_checked() {
  local asset="$1" expected="$2" target="$task_cache/$1"
  if [[ ! -f "$target" ]]; then
    curl --fail --location --retry 3 --output "$target.part" "$release_base/$asset"
    mv "$target.part" "$target"
  fi
  printf '%s  %s\n' "$expected" "$target" | sha512sum --check --status
}

download_checked "Godot_v${version}-stable_linux.x86_64.zip" a035258da32b77f966a5376f9fa29c30a6adde826a85ba918e1605bd1fc9823eba7d85f1dd5e748956bd2ba72827c0025ffa11bb82aec91128c407a2e723c99c
unzip -qo "$task_cache/Godot_v${version}-stable_linux.x86_64.zip" -d "$task_cache"
chmod +x "$task_cache/Godot_v${version}-stable_linux.x86_64"

if [[ ! -f "$task_data/android_debug.apk" || ! -f "$task_data/linux_release.x86_64" ]]; then
  download_checked "Godot_v${version}-stable_export_templates.tpz" da606b61c10157844f8300172df374472665f95015495cb1a7cd132c40ede404faa96cc1016a4b9662db9909ddea69632c4948b2cd11163438dad4808881fb68
  for template in android_debug.apk android_release.apk linux_debug.x86_64 linux_release.x86_64 version.txt; do
    unzip -p "$task_cache/Godot_v${version}-stable_export_templates.tpz" "templates/$template" > "$task_data/$template"
  done
fi
printf 'Godot executable: %s\nTemplates: %s\n' "$task_cache/Godot_v${version}-stable_linux.x86_64" "$task_data"
