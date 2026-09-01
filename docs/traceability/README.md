# PC 이식 추적 정본

`pc-port-ledger.json`은 Alpha 1.1.0의 M01~M14 세로 요구사항 238개를 PC 저장소의
현재 증거와 일대일로 연결하는 기계 판독 원장이다. 원본 요구사항은 상위 저장소의
`data/alpha110/requirement_coverage_manifest.json`이며, 원문·Notion URL·SHA와 원본
근거 참조를 수정하지 않고 복사한다.

원본 C++/Android 근거는 PC 구현 완료 증거가 아니다. PC 행은 요구 문장 전체를 충족하는
파일과 자동 검사가 확인될 때만 완료 계열로 분류한다. 일부만 구현된 행은 기존 코드
근거가 있더라도 `Gap/Deferred`다.

## disposition 의미

| 값 | 의미 |
|---|---|
| `Port` | PC에서 요구 문장 전체의 권위·조회·검증 증거가 연결됨 |
| `PC Adapt` | 모바일 전달 표면을 PC로 교체했고 PC 범위 전체 증거가 연결됨 |
| `PC_TEMP_REVIEW` | 동작과 검증은 있으나 임시 공식 또는 수치가 안정 review ID로 남음 |
| `Platform Excluded` | 행 전체가 Android 전용이라 PC 범위에서 제외됨 |
| `Gap/Deferred` | 요구 문장의 일부 또는 전부를 아직 증명하지 못함 |

혼합 요구사항에 Android 실기기 같은 절이 포함된 경우 행 전체를 제외하지 않는다.
PC에서 구현할 부분은 계속 추적하고 Android 전용 절만 `excluded_platform_clauses`에
남긴다. 따라서 현재 `Platform Excluded` 행이 0개인 것은 누락이 아니다.

## 현재 기준선

- PC 기준: `0.0.13`, Git `558a184d2cf88b0f1a2318eb028125257786d261`
- 총 행: 238
- `Port`: 86
- `PC Adapt`: 14
- `PC_TEMP_REVIEW`: 29
- `Platform Excluded`: 0
- `Gap/Deferred`: 109

이 숫자는 0.0.18의 M05 수행 수직 기능과 기존 M03·M04·M06·M07 인물, M08·M10
Atlas·경제 수직 기능 반영 상태다. 일곱 Master의 119개 행은 모두 완료 계열로 분류되어
Gap이 0이며, 남은 109개는 후속 버전에서 권위 상태,
저장/hash, query, UI와 테스트 근거를 모두 연결한 다음 변경한다.

## 생성과 검증

상위 C++/Android 저장소와 PC 저장소가 현재 작업공간처럼 함께 있을 때:

```powershell
./scripts/generate-port-ledger.ps1
./scripts/validate-port-ledger.ps1
```

PC 저장소만 독립 clone한 환경에서는 생성 정본이 없으므로 검증만 실행한다. 이 경우
238행·ID·상태·PC 파일/테스트 근거는 모두 검사하고 원본 manifest 대조만 건너뛴다.
원본을 다른 위치에 둔 경우 `-SourceManifestPath`를 명시한다.

검증기는 다음을 강제한다.

- 238개 고유 ID와 M01~M14별 V01~V17 완전성
- 허용 disposition과 집계값 일치
- 원문·Notion/마스터 SHA와 선택적 원본 manifest SHA 대조
- 완료/임시 행의 실제 PC 파일 literal과 자동 테스트 이름 존재
- `PC_TEMP_REVIEW` 행의 실제 `PC-TEMP-*` 선언/문서 근거
- Gap 설명과 대상 버전, PC Adapt 설명, 플랫폼 제외 이유

생성 JSON을 직접 수정하지 말고 `scripts/generate-port-ledger.ps1`의 명시적 분류와
근거를 변경한 뒤 다시 생성한다. 모듈 owner와 허용 의존은
`docs/architecture/repository_manifest.json`을 함께 갱신한다.
