# 선로만상록 다음 버전 통합 기획서

> 작성일: 2026-09-02 KST
>
> PC 공식 기준선: `VERSION 0.0.22`,
> `main@9b0fda8134c01b387bdef28caf281d99a7095190`
>
> PC 진행 기준선: 로컬 `codex/pc-0.0.23-ui-migration`의 VERSION 완료
> `6d3d8bdbdfa6e10ae860175483b1308083ebe186`, 구현 코드 종점
> `615d19eda1fb72a851d50b6e79dfbb32095a8c1f`
>
> 모바일 공개 기준선: Alpha `v1.1.2`, source
> `761aff4fb085a673def9f2c69898991a66e68e81`

이 문서는 2026-09-02 기술 감사를 실제 구현 순서로 바꾼 다음 버전 기획서다. 다음 공식
PC 목표는 `0.0.23`이며, 이후 `0.0.24+`의 미충족 요구와 모바일 Alpha 1.1.3을 별도
제품선으로 순차 배치한다. 숫자·공식·세계관·임시 규칙을 새로 승인하는 문서가 아니다.

- [Notion 다음 버전 통합 기획서](https://app.notion.com/p/3cfacc9e6175812e8494ea08c2dad6fb)
- [Notion 순차 구현 작업 DB](https://app.notion.com/p/e70c78a6fca1440988f9e224c3be1be4)
- [2026-09-02 기술 감사](TECHNICAL_AUDIT_2026-09-02.md)
- [개발 가이드](DEVELOPMENT_GUIDE.md)
- [리팩터링·성능 로드맵](REFACTORING_PERFORMANCE_ROADMAP.md)

## 1. 결론과 제품선 경계

### PC

- `0.0.22`는 `main@9b0fda8`에서 로컬 Release `66/66`, 구조·XAML·238행 원장,
  화면 자동 조작, self-contained package를 완료했다. GitHub Actions와 GitHub Release는
  만들지 않았다.
- `0.0.23` 로컬 브랜치는 `origin/main` 이후 27개 커밋으로 관찰 UI, 대용량 저장,
  저장 실패 복구, 기록 압축, 수행 병렬화, 진단, 100배속 cadence, 테스트 suite 1차
  분리를 구현했다.
- `VERSION 0.0.23` 전환과 Release `73/73`, 결정론·장기 저장, 100배속 600초 SOAK는
  완료됐다. 최종 SOAK는 11,401표본, p95 48.27ms, p99 57.77ms, 정상 99.89%, 최종
  backlog 0ms, 12,000,000 tick 100%다.
- 화면 행렬, M14 10행 재판정, self-contained package·직접 실행과 final evidence
  commit은 아직 남았다. 따라서 커밋 메시지의 `complete 0.0.23`을 릴리스 완료로 읽지
  않는다.
- 최종 재확인 시 사용자 미커밋 작업은 `docs/evidence/0.0.23/`과
  `tests/Chronicles.Tests/`의 기존 실행기·권위 테스트 수정 및 6개 기능별 suite 추가다.
  이는 `6d3d8bd` 이후 작업 트리 전용 변경이며 고정 검증 증거가 아니다. 후속 Codex는
  이를 삭제·덮어쓰기·reset하지 않고 `PC23-001`에서 먼저 검토한다.

### 모바일

- 공개 `v1.1.2`와 source `761aff4...`는 same-SHA 검증·공개 APK를 완료했다.
- 물리 Android 설치·프레임·발열·배터리·저장 시간은 `PENDING`이다.
- private source 최신 `main`·PR·Issue는 현재 연결 권한으로 라이브 확인하지 못한다.
- Alpha 1.1.3의 `01`, `02`, `02-A`~`02-G`는 사용자 확정이고 `02-H`는 사용자
  선택 대기다. PC 구현과 같은 PR에 섞지 않는다.

## 2. 우선 개선 항목 전체 지도

| 우선 | 범위 | 현재 증거 | 종료 조건 |
|---|---|---|---|
| P0 | PC 0.0.23 정확성·데이터 안전·완료 증거 | 로컬 27개 커밋, `73/73`, SOAK PASS | GUI, package, final evidence, M14 판정 |
| P1 | 공통 query·UI·test·benchmark 작업집합 | 일부 구현·일부 감사 backlog | owner 경계와 전후 계약 증거 |
| P1 | PC 0.0.24 콘텐츠·지식 | M11/M12 Gap 28행 | 행별 authority→save→query/UI→test |
| P1 | PC 0.0.25 조직·사회 | M09 Gap 10행 | 승인 gate와 10,000명 증거 |
| P2 | PC 0.0.26 법칙 | M13 Gap 15행, 02-H 대기 | 사용자 확정과 Stable Law contract |
| P2 | 모바일 Alpha 1.1.3 | public v1.1.2, private 라이브 미확인 | 고정 SHA·emulator·실기기·same-SHA release |

238행 이식 원장의 현재 집계는 `Port 128 / PC Adapt 16 / PC_TEMP_REVIEW 31 /
Platform Excluded 0 / Gap·Deferred 63`이다. 63개 Gap은 다음처럼 묶인다.

| 대상 | master | 행 수 | 핵심 미충족 |
|---|---:|---:|---|
| 0.0.23 | M14 | 10 | Queries/Presentation 경계, 전체 탐색, 관심 목록·설정, 접근성·오류 상태 증거 |
| 0.0.24 | M11/M12 | 28 | 콘텐츠 창작·변형·사용, AI 사용, 판본·전승·Teaching |
| 0.0.25 | M09 | 10 | 조직 전승, 사회 자질, 가문 승격, 왕조 붕괴, 전체 분포 |
| 0.0.26 | M13 | 15 | Stable Law ID, adapter·metric·state I/O, 개인 이해·세계 AI |

`PC_TEMP_REVIEW 31행`은 요구사항 단위이고 `PC-TEMP-* 55개`는 코드·문서의 고유 임시
규칙 ID 단위다. 행별 사용자 근거 없이 둘 중 어느 집계도 일괄 승격하지 않는다.

## 3. 단계별 구현 순서

### 단계 0 — 기준선과 인계 원장

1. 공식 `main`, 진행 브랜치, `VERSION`, dirty 파일, Notion 상태를 고정한다.
2. 63 Gap과 55 임시 ID의 누락·중복·대상 버전을 재생성한다.
3. 각 작업을 1~2일 크기의 독립 vertical로 관리한다.
4. 작업 중 source가 바뀌면 기획 기준 SHA와 현재 SHA를 분리한다.

### 단계 1 — PC 0.0.23 종료

1. 사용자 미커밋 파일을 검토하고 기능·문서·evidence 커밋을 분리한다.
2. final candidate에서 Release build, 전체 test, architecture, XAML, port-ledger를 실행한다.
3. 단일/분할/pulse/checkpoint와 직렬/병렬 수행의 hash·RNG·event·ranking을 비교한다.
4. 대용량 저장, primary/temp/backup 후보 복원, 실패 뒤 재저장을 검증한다.
5. 100배속 600초 SOAK와 p95/p99·정상 표본·backlog를 기록한다.
6. 1920×1080, 1280×780, UI 90/100/140%, 창·전체화면, 9개 세이브 카드,
   관심 목록, 통계·성능 화면을 직접 조작한다.
7. M14 10행을 요구 문장별로 재판정한다.
8. 모든 gate 뒤에만 `VERSION 0.0.23`, package SHA와 evidence SHA를 기록한다.

0.0.23의 비목표는 새 게임 수치, save wire 변경, 63 Gap 전체 강제 포함, GitHub Release다.

### 단계 2 — 공통 owner·성능 경계

- world gate 안에서는 짧은 immutable snapshot만 만들고 projection은 gate 밖에서 한다.
- WPF는 권위 명령, query state, 표시 state를 화면별 ViewModel/Presenter 작업집합으로
  점진 분리한다.
- 단일 test 진입점과 결과 형식은 유지하고 owner별 suite를 계속 추출한다.
- 성능 evidence를 동일 seed·동일 harness·독립 프로세스 형식으로 고정한다.
- hot authority와 cold history의 retention·compaction은 계측과 사용자 결정 뒤 바꾼다.
- 무변경 pulse·닫힌 화면·자동 로그 OFF에서 DTO·문자열·배열·feed 생성을 억제한다.

### 단계 3 — PC 0.0.24 콘텐츠·지식

1. 콘텐츠 create/transform/use 원자 명령.
2. AI 발견·획득·보유·사용·창작.
3. 시장·수행·조직 adapter와 미승인 법칙 `DataUnavailable`.
4. Content/Edition/실물 binding·접근·복원·재구성 provenance.
5. Teaching과 실제 Edition·조직 전승 연결.
6. AI 저술·필사·학습·오해·교정.
7. 지연 query/UI와 save·결정론·성능.
8. M11 16행·M12 12행의 행별 종료 감사.

### 단계 4 — PC 0.0.25 조직·사회

1. 사회 자질·설립·승계 gate를 사용자 결정 묶음으로 분리한다.
2. 조직 역할·권리·자산과 실제 Edition 접근·전승을 연결한다.
3. 가문 준비집단의 승격·해산을 typed transition으로 만든다.
4. 왕조 조건 붕괴·회복과 후속 자산·역할·역사를 연결한다.
5. 10,000명 분포·후보 index·save/query/UI를 측정하고 M09 10행을 판정한다.

### 단계 5 — PC 0.0.26 법칙

1. Alpha 1.1.3 `02-H` 사용자 선택을 기다린다.
2. 승인된 source만 Stable Law ID와 revision을 갖게 한다.
3. 객관 법칙 상태, 개인 이해, 세계 AI와 천도 authority를 분리한다.
4. Attempt에 실제 관여한 sparse source만 revision/time-frozen snapshot에 담는다.
5. save/replay·DataUnavailable·UI trace·결정론을 검증하고 M13 15행을 판정한다.

현상 종류·주기·Law ID·threshold·보정·자원·시간·세계 호의 보너스는 발명하지 않는다.

### 단계 6 — 모바일 Alpha 1.1.3

1. private source 접근 권한과 fixed SHA를 확보한다.
2. `02-H`를 사용자 확정한 뒤 `02-I`를 기획한다.
3. `03`~`08` 게임 규칙을 한 항목씩 확정한다.
4. `09`~`13`의 기록·UI·성능·저장·bridge·release 측정 계약을 고정한다.
5. fixed SHA에서 모바일 구조 backlog 11개를 재감사한다.
6. 양 ABI, emulator, physical device, same-SHA public release를 연결한다.

## 4. 순차 작업 원장

Notion 작업 DB에는 43개 Task ID가 있고 모두 선행 작업·owner·우선순위·결정 gate·완료
증거·검토 SHA를 가진다. `실행 순서` view는 Task ID 순서, `상태 보드`는 상태별로 본다.

| 단계 | Task ID | 작업 | 선행 |
|---|---|---|---|
| 기준선 | NV-BASE-001 | 공식·진행 브랜치 기준선 고정 | 없음 |
| 기준선 | NV-BASE-002 | 238행·63 Gap·55 임시 규칙 정합 | NV-BASE-001 |
| 기준선 | NV-BASE-003 | Codex 순차 실행·인계 체크리스트 | NV-BASE-001/002 |
| 0.0.23 | PC23-001 | 미커밋 작업 검토·응집 커밋 | NV-BASE-001 |
| 0.0.23 | PC23-002 | final candidate Release 전체 검증 | PC23-001 |
| 0.0.23 | PC23-003 | 분할·병렬·checkpoint 결정론 | PC23-002 |
| 0.0.23 | PC23-004 | 대용량 저장·복원·실패 회복 | PC23-002 |
| 0.0.23 | PC23-005 | 100배속 600초 SOAK | PC23-003/004 |
| 0.0.23 | PC23-006 | WPF 화면·접근성 조작 행렬 | PC23-002 |
| 0.0.23 | PC23-007 | M14 10행 증거 재판정 | PC23-003/006 |
| 0.0.23 | PC23-008 | VERSION·package·evidence 동기화 | PC23-005/006/007 |
| 공통 | ARCH-001 | query snapshot과 gate 밖 projection | PC23-008 |
| 공통 | ARCH-002 | WPF ViewModel·Presenter 작업집합 | PC23-008 |
| 공통 | ARCH-003 | test registry·suite 추가 분리 | PC23-008 |
| 공통 | ARCH-004 | 성능 evidence schema·harness | PC23-008 |
| 공통 | ARCH-005 | hot/cold history retention | ARCH-004 |
| 공통 | ARCH-006 | manifest·Notion 드리프트 검사 | NV-BASE-003 |
| 0.0.24 | PC24-001 | 콘텐츠 원자 명령 계약 | PC23-008 |
| 0.0.24 | PC24-002 | AI 발견·획득·사용·창작 | PC24-001 |
| 0.0.24 | PC24-003 | 시장·수행·조직·법칙 adapter | PC24-001/002 |
| 0.0.24 | PC24-004 | 판본·실물·복원·재구성 lifecycle | PC24-001 |
| 0.0.24 | PC24-005 | Teaching 실제 Edition·조직 전승 | PC24-004 |
| 0.0.24 | PC24-006 | AI 저술·필사·학습·교정 | PC24-004/005 |
| 0.0.24 | PC24-007 | 콘텐츠·지식 query/UI·성능 | PC24-002/004/ARCH-001 |
| 0.0.24 | PC24-008 | M11/M12 28행·package 감사 | PC24-003/006/007 |
| 0.0.25 | PC25-001 | 사회 자질·조직 gate 사용자 결정 | PC24-008 |
| 0.0.25 | PC25-002 | 조직 전승·지식 권한 | PC24-005/PC25-001 |
| 0.0.25 | PC25-003 | 가문 준비집단 lifecycle | PC25-001 |
| 0.0.25 | PC25-004 | 왕조 붕괴·회복 | PC25-001/002 |
| 0.0.25 | PC25-005 | 10,000명 분포·M09 10행 | PC25-002/003/004 |
| 0.0.26 | PC26-001 | Alpha 1.1.3 02-H 사용자 선택 | PC25-005 |
| 0.0.26 | PC26-002 | Law source·Stable ID registry | PC26-001 |
| 0.0.26 | PC26-003 | adapter·metric·state I/O | PC26-002 |
| 0.0.26 | PC26-004 | sparse manifestation snapshot | PC26-003 |
| 0.0.26 | PC26-005 | Law query/UI·M13 15행 | PC26-004 |
| 모바일 | MOB113-001 | private source 권한·fixed SHA | 없음 |
| 모바일 | MOB113-002 | 02-H 뒤 02-I 기획 | PC26-001 |
| 모바일 | MOB113-003 | Alpha 1.1.3 03~08 기획 | MOB113-002 |
| 모바일 | MOB113-004 | Alpha 1.1.3 09~13 기획 | MOB113-001/003 |
| 모바일 | MOB113-005 | alpha110 구조 backlog 재감사 | MOB113-001 |
| 모바일 | MOB113-006 | emulator·실기기·same-SHA release | MOB113-004/005 |
| 통합 | REL-001 | PC·모바일·Notion·GitHub 감사 | PC26-005/MOB113-006 |
| 통합 | REL-002 | 최종 Actions·릴리스 결정 | REL-001 |

`PC24-005`에서는 기존 Organization Core를 읽는 최소 read-only 조직 접근 adapter만
만든다. PC25에서 조직 전승 lifecycle을 완성하는 `PC25-002`가 그 결과를 선행 증거로
사용한다. 두 버전의 commit과 완료 상태는 분리한다.

## 5. Codex 실행 규칙

### 시작 전

1. 현재 Task ID와 선행 작업·결정 gate를 읽는다.
2. `git status --short --branch`, HEAD, merge-base, `VERSION`을 기록한다.
3. dirty 파일은 사용자 작업으로 간주하고 겹치는 경로와 diff를 먼저 분석한다.
4. `repository_manifest.json`, architecture README, module boundaries, 현재 technical
   architecture, 관련 traceability와 tests를 읽는다.
5. 사용자 선택·실측·권한 gate가 닫혀 있으면 구현하지 않는다.

### 한 Task의 수직 순서

```text
Feature ID / owner / representative entry
→ authority contract·state·command·typed result
→ state hash·checkpoint·save/migration 또는 비저장 판정
→ read-only query DTO
→ UI 표시·입력
→ 정상·경계·잘못된 ID·결정론·관찰 무영향 test
→ performance evidence
→ manifest·traceability·Notion·commit SHA
```

- 구조 이동과 게임 규칙·RNG·save wire 변경을 같은 Task/PR에 섞지 않는다.
- PC와 모바일 변경을 같은 branch/PR에 넣지 않는다.
- 파일 길이보다 owner·생명주기·함께 수정되는 검색 작업집합으로 분리한다.
- 범용 `manager/helper/common/misc`와 UI→World 직접 접근을 만들지 않는다.

### 결정론·RNG

- UI·query·logging·developer observation은 RNG를 소비하거나 권위 상태를 바꾸지 않는다.
- parallel 계산은 immutable input에 한하고 commit·RNG·event는 stable ID 순으로 한다.
- 단일 `AdvanceTo`, 임의 chunk, production pulse, checkpoint 중간 왕복이 같은 최종
  hash·RNG·event·ranking을 내야 한다.
- RNG stream 추가는 purpose ID, call order, fixture와 migration 영향을 기록한다.

### 저장·복구

- authority capture와 encode/compress/SQLite write를 분리한다.
- Candidate World를 검증하기 전 primary를 교체하지 않는다.
- 취소·용량·메모리·파일·권위 실패 뒤 world와 pause·입력 상태를 복구한다.
- 기존 `world.cmip`과 사용자 primary/backup은 테스트 데이터로 수정하지 않는다.
- schema 변경은 fixed fixture, checksum, 전후 hash와 migration을 갖춘다.

### UI·query

- Desktop은 `World`나 SoA 열을 직접 읽지 않는다.
- 닫힌 화면은 관련 query·DTO·행을 만들지 않는다.
- 표시 설정은 권위 save와 분리하고 RNG·공식 통계를 바꾸지 않는다.
- empty, `DataUnavailable`, error, cancelled, permission 상태를 구분한다.

### 성능

- Release, 독립 프로세스, 동일 seed와 harness를 사용한다.
- median/min/max, p95/p99, allocation/GC, live heap, backlog, checkpoint, SQLite를 남긴다.
- 이전 SHA 결과를 현재 SHA의 성과로 소급하지 않는다.
- cache·TTL·page·memory cap·hard threshold는 실측·사용자 승인 전에는 제안이다.
- 정확성·복구 test 실패를 성능 최적화보다 먼저 처리한다.

### 상태와 완료

`제안 → 사용자 확정 → 구현 → 검증 → 릴리스`를 건너뛰지 않는다.

- 코드가 있으면 `구현`, final SHA 요구 test가 있으면 `검증`, 공식 artifact가 있으면
  `릴리스`다.
- 문서·부분 test·in-progress workflow만으로 완료라고 하지 않는다.
- Gap과 임시 규칙을 일괄 승격하지 않는다.
- 중간 커밋·개별 PR에서 Actions를 실행하지 않고 최종 통합 SHA에서만 사용한다.
- 완료 전에 PR merge, GitHub Release, 오래된 모바일 Issue/PR 종료를 자동 수행하지 않는다.

## 6. Task 완료 증거

각 Task는 다음을 남긴다.

- Task ID와 구현 요약
- 시작 base SHA와 완료 commit SHA
- 변경 파일과 owner
- 규칙·RNG·save wire 영향
- 실행 명령과 실제 통과 수
- 결정론·저장·query·UI·성능 결과
- 남은 위험과 사용자 질문
- GitHub PR/diff와 Notion Task URL
- 다음 실행 가능한 Task ID

## 7. 전체 완료 기준

- PC 0.0.23은 final SHA, `VERSION 0.0.23`, Release 전체 검사, 600초 SOAK,
  대용량 저장·복구, 화면 행렬, self-contained package와 직접 실행 증거가 모두 있다.
- 63 Gap은 버전별 행 증거가 생길 때만 감소한다.
- 55 임시 규칙은 승인·반려·대체·삭제 후보 상태를 유지하고 자동 확정하지 않는다.
- 모바일은 private fixed SHA와 physical device 증거가 없으면 `PENDING` 또는
  `권한 미확인`을 유지한다.
- Notion 상태, GitHub SHA·PR과 local evidence가 같은 버전·집계·결정 상태를 말한다.

## 8. 직접 근거

- [PC `main@9b0fda8`](https://github.com/hddy774/Chronicles-of-Myriad-Immortal-Paths/commit/9b0fda8134c01b387bdef28caf281d99a7095190)
- [0.0.22 검증 증거](https://github.com/hddy774/Chronicles-of-Myriad-Immortal-Paths/blob/9b0fda8134c01b387bdef28caf281d99a7095190/docs/evidence/0.0.22/verification.md)
- [238행 이식 원장](traceability/pc-port-ledger.json)
- [PC 임시 규칙 원장](traceability/PC_TEMP_REVIEW_LEDGER.md)
- [Notion Alpha 1.1.3 로드맵](https://app.notion.com/p/3ccacc9e617581768206f7d30d267fd7)
- [Notion Alpha 1.1.3 02-H](https://app.notion.com/p/3ccacc9e617581138cc3c9d119dcf50d)
- [모바일 v1.1.2](https://github.com/junghara/xianxia-chronicle-releases/releases/tag/v1.1.2)
- [오래된 public Issue #57](https://github.com/junghara/xianxia-chronicle-releases/issues/57)
- [오래된 public Draft PR #56](https://github.com/junghara/xianxia-chronicle-releases/pull/56)
