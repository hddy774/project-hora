#!/usr/bin/env bash
# C# 파일의 크기·헤더·추적 가능한 임시 표시·결정론 규칙을 검사한다.
# 관련 명세: docs/tasks/M0-05-ci.md. 선택 인자는 독립 시험용 저장소 루트다.
set -euo pipefail

script_directory=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
repository_directory=${1:-"$script_directory/.."}
if [ "$#" -gt 1 ] || [ ! -d "$repository_directory" ]; then
    printf '%s\n' '사용법: check-code-rules.sh [저장소 디렉터리]' >&2
    exit 2
fi
cd -- "$repository_directory"

# GNU 전용 find/awk 옵션을 피하고 macOS 기본 Bash 3.2도 지원한다.
set --
for directory in src tests tools; do
    if [ -d "$directory" ]; then
        set -- "$@" "$directory"
    fi
done
if [ "$#" -eq 0 ]; then
    printf '%s\n' '검사할 C# 소스 디렉터리가 없습니다.'
    exit 0
fi

failed=0
file_count=0
while IFS= read -r -d '' file; do
    file_count=$((file_count + 1))
    if ! LC_ALL=C awk '
        function fail(rule, detail) {
            printf "%s:%d: %s: %s\n", FILENAME, NR, rule, detail
            failed = 1
        }
        {
            line = $0
            sub(/\r$/, "", line)
            if (NR == 1 && line !~ /^\/\//) {
                fail("HEADER", "첫 줄에 // 파일 설명이 필요합니다.")
            }

            # UTF-8의 후속 바이트를 제외하면 한국어도 바이트가 아닌 문자로 센다.
            # LC_ALL=C는 macOS awk와 Linux mawk의 서로 다른 로캘 처리를 통일한다.
            characters = line
            gsub(/[\200-\277]/, "", characters)
            if (length(characters) > 120) {
                fail("LINE_LENGTH", "문자 " length(characters) "개 (최대 120)")
            }

            markers = line
            gsub(/(TODO|HACK)\(#[1-9][0-9]*\)/, "", markers)
            if (markers ~ /(^|[^[:alnum:]_])(TODO|HACK)([^[:alnum:]_]|$)/) {
                fail("ISSUE_REFERENCE", "TODO(#123) 또는 HACK(#123) 형식이 필요합니다.")
            }

            if (FILENAME ~ /^src\// && line ~ \
                /(^|[^[:alnum:]_])(System[[:space:]]*\.[[:space:]]*Random|DateTime[[:space:]]*\.[[:space:]]*Now|Guid[[:space:]]*\.[[:space:]]*NewGuid)([^[:alnum:]_]|$)/) {
                fail("NONDETERMINISM", "코어에서 비결정적 API를 사용할 수 없습니다.")
            }
        }
        END {
            if (NR == 0) {
                fail("HEADER", "빈 파일에 파일 설명이 없습니다.")
            }
            if (NR > 400) {
                fail("FILE_LENGTH", "줄 " NR "개 (최대 400)")
            }
            exit failed
        }
    ' "$file"; then
        failed=1
    fi
done < <(find "$@" -type d \( -name bin -o -name obj \) -prune \
    -o -type f -name '*.cs' -print0)

if [ "$failed" -ne 0 ]; then
    printf 'C# 규칙 검사 실패: %s개 파일을 검사했습니다.\n' "$file_count" >&2
    exit 1
fi
printf 'C# 규칙 검사 통과: %s개 파일.\n' "$file_count"
