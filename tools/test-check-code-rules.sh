#!/usr/bin/env bash
# 규칙 검사가 위반을 실제로 거절하며 한글·생성 산출물을 잘못 거절하지 않는지 검증한다.
# 관련 명세: docs/tasks/M0-05-ci.md. 시험 파일은 임시 디렉터리에만 생성한다.
set -euo pipefail

script_directory=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
checker="$script_directory/check-code-rules.sh"
temporary_directory=$(mktemp -d "${TMPDIR:-/tmp}/hora-code-rules.XXXXXX")
trap 'rm -rf -- "$temporary_directory"' EXIT
mkdir -p "$temporary_directory/src/fixture" "$temporary_directory/tests" \
    "$temporary_directory/tools" "$temporary_directory/src/fixture/obj" \
    "$temporary_directory/tools/bin"

# 120문자 경계의 한글, 공백 포함 경로, 이슈가 연결된 표시, 빌드 생성 파일 제외.
{
    printf '%s\n' '// 한국어 파일 헤더'
    awk 'BEGIN { printf "//"; for (i = 0; i < 118; i++) printf "한"; printf "\n" }'
    printf '%s\n' '// TODO(#19): 명세 단계에서 추적하는 작업.' '// HACK(#19): 제거 조건을 기록한 우회.'
} > "$temporary_directory/src/fixture/Valid name.cs"
printf '%s\n' 'System.Random TODO' > "$temporary_directory/src/fixture/obj/Generated.cs"
printf '%s\n' 'header missing' > "$temporary_directory/tools/bin/Generated.cs"
bash "$checker" "$temporary_directory"

# 다섯 규칙이 같은 실행에서 모두 보고되는지 검사한다.
awk 'BEGIN { print "// 크기 검사"; for (i = 0; i < 400; i++) print "//" }' \
    > "$temporary_directory/src/fixture/TooManyLines.cs"
{
    printf '%s\n' '// 줄 길이 검사'
    awk 'BEGIN { printf "//"; for (i = 0; i < 119; i++) printf "한"; printf "\n" }'
} > "$temporary_directory/src/fixture/TooLong.cs"
printf '%s\n' '// 임시 표시 검사' '// TODO: 연결되지 않은 작업' '// HACK: 연결되지 않은 우회' \
    > "$temporary_directory/src/fixture/MissingIssue.cs"
printf '%s\n' 'namespace MissingHeader {}' > "$temporary_directory/src/fixture/MissingHeader.cs"
printf '%s\n' '// 결정론 검사' 'System.Random random;' 'DateTime.Now;' 'Guid.NewGuid();' \
    > "$temporary_directory/src/fixture/Forbidden.cs"
if bash "$checker" "$temporary_directory" > "$temporary_directory/output.log" 2>&1; then
    printf '%s\n' '실패: 규칙 위반을 검사기가 허용했습니다.' >&2
    exit 1
fi
cat "$temporary_directory/output.log"
for rule in FILE_LENGTH LINE_LENGTH ISSUE_REFERENCE HEADER NONDETERMINISM; do
    if ! LC_ALL=C grep -q ": $rule:" "$temporary_directory/output.log"; then
        printf '실패: %s 위반 보고가 없습니다.\n' "$rule" >&2
        exit 1
    fi
done

# 401줄 위반을 제거한 뒤 정확히 400줄 파일은 허용한다.
rm -- "$temporary_directory/src/fixture/TooManyLines.cs" \
    "$temporary_directory/src/fixture/TooLong.cs" \
    "$temporary_directory/src/fixture/MissingIssue.cs" \
    "$temporary_directory/src/fixture/MissingHeader.cs" \
    "$temporary_directory/src/fixture/Forbidden.cs"
awk 'BEGIN { print "// 크기 경계 검사"; for (i = 0; i < 399; i++) print "//" }' \
    > "$temporary_directory/tests/Exactly400.cs"
bash "$checker" "$temporary_directory"
printf '%s\n' '규칙 검사기 자체 시험 통과: 양성 경계와 5종 위반을 확인했습니다.'
