# 작업 명세 목록

Claude가 작성하고 Codex가 구현하는 작업 명세. 형식과 흐름은 [개발 규칙 §2~3](../standards/development-process.md).
**상태가 `승인`인 작업만 구현한다.** (GitHub 이슈의 `status:ready` 라벨과 같은 의미)

## M0. 기반 (엔진과 무관한 순수 C# 코어)

| ID | 작업 | 선행 | 상태 |
|---|---|---|---|
| [M0-01](M0-01-solution-scaffold.md) | .NET 솔루션·프로젝트 구조 생성 | — | 승인 대기 |
| [M0-02](M0-02-money-quantity.md) | `Money`, `Quantity` 값 타입 | M0-01 | 승인 대기 |
| [M0-03](M0-03-ids-random.md) | 엔티티 ID 타입과 결정론적 난수기 | M0-01 | 승인 대기 |
| [M0-04](M0-04-month-pipeline.md) | 월 정산 파이프라인 골격 | M0-02, M0-03 | 승인 대기 |
| [M0-05](M0-05-ci.md) | CI 워크플로 (format·build·test) | M0-01 | 승인 대기 |

M0-02, M0-03, M0-05는 M0-01 이후 **병렬 진행 가능**하다.
엔진(Unity/Godot) 결정 전에도 M0는 모두 진행할 수 있다.

## XO. 선협 관찰 (Xianxia Observer) 안정화

검토 결과와 결정 요청: [games/xianxia-observer/docs/13-claude-review.md](../../games/xianxia-observer/docs/13-claude-review.md).
이 게임의 코드는 `games/xianxia-observer/` 아래에 있고 루트 Hora 코어 규칙(.NET Standard 2.1·C# 9·Money)과 별개다.
가까운 `Directory.Build.props`(net10.0)와 `tools/check-code-rules.sh`(파일 400줄·줄 120자 등)를 따른다.
검사 명령(게임 폴더에서): `dotnet restore XianxiaObserver.slnx --locked-mode && dotnet format XianxiaObserver.slnx --verify-no-changes --no-restore && bash ../../tools/check-code-rules.sh . && dotnet build XianxiaObserver.slnx -c Release --no-restore -warnaserror && dotnet test XianxiaObserver.slnx -c Release --no-build`.

| ID | 작업 | 우선 | 선행 | 상태 |
|---|---|---|---|---|
| [XO-01](XO-01-release-completion.md) | 선협 관찰 완성·APK 배포 | — | — | 완료(PR #28) |
| [XO-02](XO-02-documentation-review.md) | 기획·기술 문서와 검토 자료 | — | — | 완료(PR #28) |
| [XO-03](XO-03-save-validation.md) | 저장 의미 검증과 손상 저장 보존 | P1 | — | 승인 대기 |
| [XO-04](XO-04-technique-reward-monotonic.md) | 공법 보상 단조성 | P1 | — | 승인 대기 |
| [XO-05](XO-05-region-event-overlap.md) | 지역 사건 보정 재계산과 개입 입력 검증 | P1 | — | 승인 대기 |
| [XO-06](XO-06-follow-rng-isolation.md) | 관심 표시와 세계 난수 분리 | P1 | 결정 D-01(승인됨) | **승인** |
| [XO-07](XO-07-runner-fault-and-autosave.md) | 실행기 오류 격리·정지 중 자동 저장·저장 내구성 | P1 | XO-03 | 승인 대기 |
| [XO-08](XO-08-labels-and-records.md) | 표시·집계 용어 정합 | P2 | 결정 D-04·D-05 | 승인 대기 |
| [XO-09](XO-09-android-input-acceptance.md) | Android 실입력 수용 검사 | P1(검증) | 결정 D-07 | 승인 대기 |
| [XO-10](XO-10-balance-report.md) | 장기 밸런스·저장 크기 측정 보고서 | P2 | XO-06 | 승인 대기 |
| [XO-11](XO-11-woodblock-navigation.md) | 고서 목판 테마·공통 배치·탐색 | UI | XO-07 | 구현 승인 대기(테마 사용자 지정) |
| [XO-12](XO-12-people-and-detail-ux.md) | 인물 탐색·상세·개입·저장 UX | UI | XO-11·XO-06·XO-05 | 구현 승인 대기 |
| [XO-13](XO-13-observation-and-history.md) | 천하 관찰 진입·사기·기록 탐색 | UI | XO-12·XO-08 | 구현 승인 대기 |

XO-03·04·05의 구현 준비는 독립적으로 할 수 있지만 저장 진입점의 통합은 아래 개발 계획 순서를 따른다.
XO-04·05·06은 같은 시드의 미래를 바꾸므로 같은 릴리스 후보에 모아 버전 노트에 적는다(D-09 승인).

후속 통합 순서·결정·새 배치 검증은 [개발 계획](../../games/xianxia-observer/docs/14-development-plan.md)을 따른다.
실행 상태 원본은 [진행표 JSON](xianxia-progress.json), 공개 체크표는 [PR #29](https://github.com/hddy774/project-hora/pull/29)다. 승인과 실행 상태를 구분한다.
