# 0.2.1 최종 성능 근거의 정본 범위

이 문서는 **검증기와 harness 소스 계약**이다. 성능·성장·장기 실행·프로파일링을 실행한 결과가 아니다. 모든 기능 완료 선언 전에는 해당 측정을 실행하지 않는다. 원장 완료 상태, 게시 허용 상태, 수치 예산을 이 변경으로 채우지 않는다.

## 정본 근거와 옛 목표의 구분

- [개정 6 §11: 성장하는 인구의 성능](../plans/revisions/v021-notion-revision6-source.md#L1934)은 여러 인구 단계와 10,000명 규모를 검증하고, 대규모 합성 fixture와 실제 두 시조의 성장 역사를 구분한다. “허용 회귀율·메모리·저장 크기는 실제 기준선과 인구 단계별 측정 뒤 정”한다.
- [§11: 계측 방법](../plans/revisions/v021-notion-revision6-source.md#L2072)은 Release, seed/법칙/작업량, 기기/OS/런타임/commit, 워밍업/반복, 열/전원 상태를 기록하고 Android 실기기와 PC 결과를 구분한다.
- [L04의 인지 비용 범위](../plans/revisions/v021-notion-revision6-source.md#L1142)는 2·100·1,000·10,000명에서 인출/계획 비용·메모리·저장 증가를 실측하며, 수치 예산은 미확정이라고 명시한다.
- [T04](../plans/revisions/v021-notion-revision6-source.md#L2205)는 단계별 측정·분포/장기 안정성·장치/런타임 구분을 요구한다. 7,200개월/60초/100배라는 현재 필수 수치는 이 조항에 없다.
- [V020-G03 대응표](../plans/revisions/v021-notion-revision6-source.md#L2292)의 과거 100배/60초 목표는 비교 항목으로 보존한다. 기존 [실패 원본](../evidence/0.2.1/soak.json)은 131/7,200개월, 약 60.343초, 약 1.809배, `passed: false`이다. 원본 SHA256은 `7560f882822979077b51f1ec97a6596f143c1870836515aceb9ad8a252749da8`이며 검증기가 변경·재명명을 거절한다.

정본 게이트를 충족한 결과와 옛 속도 목표에 미달한 결과는 동시에 존재할 수 있다. 옛 실패를 정본 통과로 바꾸거나, 미달 수치를 감추지 않는다.

## 측정 진입점 — 기능 완료 후에만 사용

| 진입점 | 범위와 완료 표시 |
| --- | --- |
| `--performance <report.json>` | 기본 2/100/1000/10000명. 규모별 별도 워밍업·새 측정 세계·12개월 표본·조회·메모리·저장 왕복. 각 규모 직후 원자적으로 보고서를 저장한다. |
| `--performance-size <인구>` 추가 | 지원하는 한 규모만 준비·측정한다. `completedSizes`에 실제 완료 규모를 기록하며 `complete: false`이다. 부분 결과는 최종 통과 근거가 아니다. 별도 파일 이름을 사용한다. |
| `--stability <report.json> --stability-plan <plan.json>` | 실측 기준선 뒤 작성한 계획의 인구/기간/체크포인트/불변식 검사 간격/실행시간 상한을 사용한다. 10,000명 초기 fixture를 포함한다. 실제 AI·물리 작업, 공개 조회/명령, `GameSaveStore`의 실제 원자 파일 저장·재개와 hash/불변식을 검사한다. 체크포인트마다 인구·월/목표월·생존 수·실제 경과 시간을 출력하고, case 종료 직후 보고서를 원자적으로 저장한다. |
| 기존 `--soak <report.json>` | 원래 7,200개월/60초/100배 비교를 동일한 성공/실패 의미로 기록한다. `seonro-legacy-speed-comparison`, `mandatoryForV021: false`로 명시한다. 정본 장기 안정성 결과를 대신하지 않는다. |

표본은 실제 살아 있는 AI fixture를 사용한다. 인물을 죽이거나 장기 `Wait`/강제 `Idle`로 잠그지 않는다. 장기 진행에서 실제 자연사·출생·승천·인구 변화가 발생할 수 있으며, 10,000명 전원 생존은 필수 조건이 아니다. 실제 초기 인구와 각 checkpoint의 생존·출생·사망·전체 인물 수를 보존한다.

`Run`의 월별 표본은 한 프로세스에서 한 회 측정한 12개월의 시간 분포다. 독립 프로세스 반복을 수행한 것처럼 보고하지 않는다. 앞서 실행된 기능 검사 선택과 같은 프로세스/JIT·공유 캐시 범위를 환경에 명시한다. fixture/워밍업/복원/full GC/보고서 작성 비용은 월별 표본 밖에 두고, 전체 소요 시간은 별도로 기록한다. 부분 보고서는 이후 규모가 오래 걸리거나 실패해도 보존되지만, 네 규모와 `complete: true`가 모두 없으면 거절한다.

장기 보고서는 `complete`, `status`(`running`/`failed`/`completed`), `runningPopulation`, `finishedCases`, 실제 오류를 구분한다. 전체 계획을 정상 종료한 `complete: true`·`status: completed`만 수용한다. 이 플래그만 바꾸어도 실제 case/기간/checkpoint 검증을 대체할 수 없다. 소요 시간은 각 원자 보고서 쓰기 직전까지이며 마지막 보고서 직렬화·쓰기는 그 값 밖이다. case 시간은 마지막 결과 투영·보고서 작성을 제외하고, 그 비용은 상위 총시간에 포함한다.

## L04 메모리와 인지 범위

- `allocatedBytes`: 측정한 12개월 동안 프로세스 누적 할당량의 차이. 보유 메모리가 아니다.
- `openingRetainedManagedBytes` / `retainedManagedBytes`: 실제 세계를 참조한 상태에서 blocking·compacting full GC → finalizer 대기 → full GC 후 `GC.GetTotalMemory(false)`로 읽은 관리 힙. 마지막 세계 전체 clone을 만들기 **전** 측정한다. 실제 세계·공유/runtime 캐시·작은 앞선 결과 행 등을 포함한 프로세스 범위이며, 네이티브/RSS나 인물별 객체 크기로 주장하지 않는다. GC 비용은 총시간에 포함한다.
- `cognition`: 인물당 실제 믿음 예산과 활성 주장 예산, 실제 budget units의 최대값/초과자 수, 믿음·저장/활성 주장·사람/장소/원천 관측·알려진 인물/지점의 총계와 인물별 최대값.
- `serializedCognitionBytes`: 보고서에 명시한 개인 인지/판단/계획/일지/숙련 투영, 관측/평판, 지식 기억/실습의 압축하지 않은 UTF-8 JSON 투영 크기. 육신/유전형·실물·세계 연대기는 제외한다. 투영 필드 오버헤드를 포함하므로 실제 관리 힙이나 전체 저장 크기와 같다고 주장하지 않는다. 전체 저장은 별도 `saveBytes`로 측정한다.

환경에는 실제 소스 commit, CPU 수·프로세스 아키텍처, 런타임 가용 메모리, 기기 이름, OS/런타임을 남긴다. 열/전원 상태를 제공하지 않은 호스트는 미측정임을 명시한다. 이 호스트 결과로 Android 발열·배터리·성능 통과를 표시하지 않는다.

월별 elapsed는 인출/계획을 포함한 실제 세계 진행 비용이다. 보존된 판단 수·추론 확장 수·인물별 최대값도 기록하지만, 별도 planner CPU 시간이나 전체 생애 누적 추론량으로 주장하지 않는다. 살아 있는 측정 세계를 인물 수와 함께 기록하며, 측정 시작 시 수정/미추적 소스가 있으면 거절한다.

## 최종 검증기 계약

최종 `seonro-final-performance` 감사는 schema 2와 policy `v021-revision6-section11-T04`를 사용한다. 모든 경로는 체크인한 `docs/evidence/0.2.1/` 아래 실제 파일 및 SHA256으로 연결한다.

| 필드 | 요구하는 근거 |
| --- | --- |
| `baseline_report` / `baseline_report_sha256` | 네 규모가 완료된 실제 기준선 schema 2. 각 규모의 실제 월별 표본, 환경, 살아 있는 AI 작업, L04 집계·인지 bytes·수집한 보유 메모리·전체 저장 크기가 있어야 한다. |
| `population_report` / `population_report_sha256` | 최종 측정. 최초 예산 도입 시 기준선과 같은 보고서를 참조할 수도 있다. 이후 코드 변경은 그 기준선 대비 실제 회귀율과 절대 예산을 함께 검사한다. |
| `baseline_product_source_fingerprint` / 기존 `product_source_fingerprint` | 실제 기준선 commit과 최종 소스의 각 fingerprint. 기준선 commit의 계보 및 실제 측정 소스를 검증하며, 최종 인구/장기 측정은 최종 제품 입력과 일치해야 한다. |
| `budget_report` / `budget_report_sha256` | schema 1, kind `seonro-performance-budget`, 동일 policy, `established_after_baseline: true`, 실제 기준선 SHA256, 근거 `rationale`, `maximum_regression_percent`, 인구별 `limits`, 장기 인구별 `stability_limits`. 수치는 실측 뒤 문서화한다. 환경이 다르면 `environment_difference_rationale`에 비교 한계를 남긴다. |
| `stability_plan` / `stability_plan_sha256` | 아래 명시 계획. 전제와 검사 기간은 기준선 이후 근거를 적어 채택한다. 정본에 없는 고정 수치를 기본 통과 조건으로 넣지 않는다. |
| `stability_report` / `stability_report_sha256` | 계획과 같은 실제 장기 진행. 기간 완료·실행 상한·중간 불변식·실제 파일 저장/재개·공개 조회/명령·메모리/저장 증가 예산을 검사한다. 실패/시간 초과를 통과로 바꾸지 않는다. |
| `legacy_speed_history_report` / `legacy_speed_history_report_sha256` | 위 과거 실패 원본을 정확히 참조한다. 선택적인 새 `legacy_speed_comparison_report`도 원래 목표의 실제 결과와 수학적 일치 여부를 검사한다. 미달 자체는 정본 예산 판정이 아니다. |

인구별 `limits` 키는 `monthlyP95Ms`, `queryMs`, `saveMs`, `loadMs`, `allocatedBytes`, `saveBytes`, `retainedManagedBytes`, `serializedCognitionBytes`다. 장기 `stability_limits`는 `retainedManagedBytes`와 `saveBytes`다. 양수 상한 및 기준선 대비 허용 회귀율을 실제 보고서 뒤 정해야 하며 이 소스 변경은 숫자를 채택하지 않는다.

계획 JSON은 `schemaVersion: 1`, `policyId`, `baselineReportSha256`, `rationale`, `cases`를 갖는다. 각 case는 `population`, `horizonMonths`, `checkpointEveryMonths`, `validateEveryMonths`, `wallTimeLimitSeconds`를 명시한다. 기간은 단기 기준선보다 길고, 시작/중간/끝 저장·재개가 있어야 한다. 수치와 장기 경계 커버리지가 충분한지는 실제 계획 검토와 T04 감사에서 판단한다. JSON 형태만 맞춘 임의의 짧은 계획을 장기 생태계나 보정 검증 완료로 주장하지 않는다.

기능 완료와 현재 기능 검사 fingerprint 연결, 플랫폼/서명/APK 검증, 게시 허용의 기존 보호 조건은 유지한다. 이 검증기는 실제 측정을 시작하지 않는다. T04의 실제 성장 분포·PC-TEMP 보정/민감도 검토와 최종 기능 감사도 여전히 별도 실제 근거가 필요하다.
