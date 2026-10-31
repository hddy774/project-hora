# 값 순서만 반전한 민감도 보충 관측

[실제 실행 41159864831](https://github.com/hddy774/project-hora/actions/runs/41159864831)은 **failure**로 종료했다. 사전 선택한 1500·시드42/2026은 각각6000개월을 관측한 뒤, 둘째 값900 실행 중240분 상한으로 프로세스가 종료됐다. 기존4조건과 합친 [고유6조건 색인](unique-conditions.json)은 케이스별 출처이며, 서로 다른 계획을 하나의 정상 종료 집계 PASS로 합성한 보고서가 아니다.

이 폴더는 작은 검증·출처·명령·실제 실패 로그만 보존한다. ZIP/DLL/대형 성장 원문, 기존 보고서·원장·readiness·제품은 변경하지 않았다. 원래 결과와 수용 당시 판정은 [이전 보고](../growth-sensitivity-results-41131479924/README.md)에 그대로 있다.

## 입력과 출처

- 실제 측정 소스는 `7ce66943b539420c9dc1e3fa8a611daae58020ff`, 제품FP는 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`다. 최신 제품의 별도 기능/성능 결과로 승격하지 않는다.
- 입력 head는 `19c1faa3a1e6b8a921fd3cbe1642ac3824c955b6`, 실제 workflow 소스는 `836ff97e930e82af36a6f5fae6dae575c109f7d9`다. [출처 기록](provenance.json)은 실제 소스와 문서 기준 `ae835340`을 구분한다.
- [계획 원문](plan.json)의 SHA256은 `74068850c36350beaf547665fc15aeb2da799899d47fedb9bd8f2a0568a6b653`이다. 원래 계획2533…과 값 순서 `[900,1500] → [1500,900]`만 다르다. 기준 집계·상한·9raw의 나머지11입력은 byte 동일하다.
- 한 법칙 `cultivation.foundation`, 기본a·혈연 허용·양친 금단·유입0, 시드42/98/2026,6000개월/120개월 간격, CLI 상한240분을 유지했다. SDK10.0.403/runtime.NET10.0.14다. 실제 argv와 시작/종료 원문은 [metadata](metadata/seed-42/process-start.json)에 있다.

## 실제 관측과 종료

| 시드 | 첫 값1500 | 둘째 값900 | 실제 프로세스 종료 UTC | exit |
|---:|---|---|---|---:|
|42|6000개월·51행 완주|진행3550/마지막 표본3480개월|06:25:19.302518|124|
|98|6000개월·51행 완주·중복|6000개월·51행 완주·중복|04:32:30.652888|0|
|2026|6000개월·51행 완주|진행5052/마지막 표본5040개월|06:25:17.959429|124|

모든 시각은2026-10-31이다.42/2026의 실제 프로세스 wall은14400.077843/14400.124297초다. case 경과·AdvanceTo 시간과 프로세스 wall은 구분하며 새 최종 제품의 비용 측정으로 해석하지 않는다.

세 원형 모두 `complete=false`다. `requested_complete`는98만 true이고42/2026은 false다. 실행 API의 failure 완료 갱신 시각은06:25:39UTC, 수집 담당의 최종 API 관측은06:26:36UTC다. 실제 종료·게시/수집 시각은 서로 다르다.

- 고유조건은 기존900×42/98/2026·1500×98과 이번1500×42/2026만 선택했다. 새900/98 중복은 가산하지 않는다.
- 새1500의 각51행 전체 비시간 표본은 기본1200 동시드와 같다. 원래1500 부분 접두부는42의22행/2520개월,2026의46행/5400개월까지 같다. 서로 다른 진행월의 상태 해시는 비교하지 않았다.
- 새900 부분은 원래900의42·30행/3480개월,2026·43행/5040개월 접두부와 같다.98의 두 조건은 기존98과 각51행 및 모든 비시간 케이스 필드·최종 해시가 같다.
- 새1500의 후손 Qi 형성은42/2026=9/3개이며 Foundation/Core는 모두0이다. 실제 People의 tuning/evaluation/reputation/social 정책은 선언 기본값과 같았다. 선언된 전 catalog를 실제 사용으로 세지 않는다.
- 이 자료에서900/1500의 새로운 성장 우월성 정보는 확인하지 못했다. 후손 Foundation 이후 직접 효과는 식별되지 않았고, 시조 초기 절대 구조·상위 목표도 함께 바뀌는 한계가 있다. 현행1200을 잠정 유지한 root의 판정을 최적값 증명으로 바꾸거나, 이전 Z04 수용 당시 기록을 다시 쓰지 않는다.

각 시드의 [검증42](verification/seed-42.json)·[검증98](verification/seed-98.json)·[검증2026](verification/seed-2026.json)은 원형 SHA와 실제 PE/before/loaded의 네 DLL 귀속을 보존한다. 빌드 간 DLL byte가 같다는 주장은 하지 않는다.

## Collector 실패와 중복 범위

[실제 collector 로그](collector/job-124465144337.log)는 새로 입수한 원형18953바이트를 그대로 복사했다. [색인](collector/index.json)에 선택한3raw·SHA와 실패 위치가 있다. 첫 정렬 입력인2026의 `requested_complete=false`가 기존 guard에서 거절됐으며, Python line12 AssertionError/exit1과 matrix failure를 유지했다. `verified-six.txt`는 없다.

collection ZIP의90개 파일 중89개는 세 개별 시드 ZIP과 byte가 같은 중복이며, 유일한 추가는 `provenance/request.json`이다.42/2026의 원형 `.lock`도 보존했으며 관측 결과로 선택하지 않았다. 다른 배치·중복 파일은 새 고유조건을 만들지 않는다.

## 원형 보관과 읽기 검증

[6 ZIP 실물 검증](verification/artifacts.json)의 3seed·inputs·prepare·collection은 [별도 근거 사전릴리스](https://github.com/hddy774/project-hora/releases/tag/evidence/v0.2.1-sensitivity-41159864831)에 원형 그대로 보관됐다. [공개 색인](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-sensitivity-41159864831/sensitivity-artifact-index-41159864831.json)은 asset694601640·24,058바이트, SHA256 `20168d0596fa0f9d17f933d37a732e0af3725513c3aeb897ec769efc29c7d9cd`다. [영속보관 상태](publication.json)는 **verified-complete**이며 6 ZIP과 색인 총7개를 실제 무인증 whole-byte 다운로드로 확인했다. 7개 자산의 최종 응답은 HTTP200이었다. 리다이렉트를 포함한14요청 모두 Authorization/Cookie 없이 진행됐고, ZIP6개의 manifest SHA와 색인의 server SHA가 일치했다. 전체 검증 원형 폴더는 root의 별도 증거 PR이 소유하며 여기에는 작은 참조만 둔다.

실제 보관 실행은41177374470/attempt1, release429767706이며 보관 도구 소스는 PR263의 `46c484e3ffb4bdac69725ff789c959ef3721c475`다. 실제 측정 소스7ce와 구분한다. 사전릴리스/비latest 계약과 기존 릴리스 보호3항목의 원문은 유지됐다. 수집기 오류 로그18,953바이트는 이 Git 폴더의 원형 사본이며, collection ZIP에만 있는 고유 파일은 request다. 보관 완료가 두 배치의 FAIL·raw false나 다른 계획에 걸친 고유6관측의 의미를 바꾸지 않는다.

```text
python3 docs/evidence/0.2.1/growth-sensitivity-results-41159864831/verify-evidence.py
python3 docs/evidence/0.2.1/growth-sensitivity-results-41159864831/verify-evidence.py --archives /workspace/scratch/sensitivity-41159864831-7ce
```

기본 검증은 manifest·출처·실패/부분·고유조건·링크를 읽는다. 선택 인수는 기존6ZIP의 실물 크기/SHA/CRC와 raw SHA를 대조한다. 새 Game/.NET/성장/측정을 실행하지 않는다. Codex가 작성·자체 검토했으며 root가 통합 검토한다. 원장/제품값/릴리스 판정은 이 문서가 쓰지 않는다.

상위 PR84, 관련212·238·241·261.
