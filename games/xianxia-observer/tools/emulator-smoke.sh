#!/usr/bin/env bash
# 실제 포커스·새 전환 로그·렌더 좌표로 판정하는 Android 수용 검사.
set -euo pipefail
script_directory=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
python3 "$script_directory/emulator-smoke.py" "$@"
