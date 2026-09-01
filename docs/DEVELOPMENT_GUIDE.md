# 선로만상록 PC 개발 가이드

이 문서는 `pc-csharp`에서 새 기능, 버그 수정, 구조 리팩터링, 저장 변경, 성능 최적화를
일관된 방식으로 진행하기 위한 실무 규칙이다. 숫자·공식·세계관을 새로 승인하는 문서가
아니며, 기존 `PC_TEMP_REVIEW`를 자동으로 확정하지 않는다.

현재 상태와 근거는 [2026-09-02 기술 감사 보고서](TECHNICAL_AUDIT_2026-09-02.md),
우선순위는 [리팩터링·성능 로드맵](REFACTORING_PERFORMANCE_ROADMAP.md), 기획·승인
상태는 [Notion 통합 보고서](https://app.notion.com/p/3ceacc9e6175816a9ba7e07d18626f24)를
함께 확인한다.

## 1. 개발을 시작하기 전 읽을 순서

1. [`AGENTS.md`](../AGENTS.md)
2. [`docs/architecture/repository_manifest.json`](architecture/repository_manifest.json)
3. [`docs/architecture/README.md`](architecture/README.md)
4. 변경 기능의 [`docs/traceability`](traceability/README.md) 문서와
   [`pc-port-ledger.json`](traceability/pc-port-ledger.json)
5. 임시 규칙이 관련되면
   [`PC_TEMP_REVIEW_LEDGER.md`](traceability/PC_TEMP_REVIEW_LEDGER.md)
6. 실제 `*.csproj`, owner 코드와 관련 테스트
7. 현재 버전의 `docs/evidence/<VERSION>/verification.md`
8. README·GAME_DESIGN·CHANGELOG의 설명과 과거 기록

충돌이 있으면 manifest·실제 project reference·코드·테스트를 함께 확인한다. 과거
README 문장이나 예전 benchmark만으로 현재 구조와 완료 상태를 판정하지 않는다.

## 2. PC primary / 모바일 reference 원칙

이 저장소에서는 PC C# 구현이 실행·저장·UI·성능의 primary다. 모바일 C++/Android는
요구 의미와 누락 범위를 확인하는 reference다.

- 모바일 원문·Notion URL·source SHA는 이식 원장 생성 입력에서 보존한다.
- 모바일 파일이 존재하는 것은 PC 완료 증거가 아니다.
- PC 구현 파일과 PC 자동 검사가 요구 문장 전체를 증명해야 `Port`다.
- JNI·Activity·Android lifecycle처럼 전달 표면만 다른 경우 `PC Adapt` 근거를 적는다.
- 모바일과 PC의 정책·공식·수치가 다르면 임의로 합치지 말고 결정 ID를 요청한다.
- PC의 임시값을 모바일 정본이나 공통 기획에 역전파하지 않는다.
- 일부 절만 구현했다면 기존 코드가 많아도 `Gap/Deferred`를 유지한다.

## 3. 두 개의 상태 축을 분리한다

### 3.1 요구 이식 disposition

| 상태 | 사용 조건 |
|---|---|
| `Port` | 요구 문장 전체의 PC 권위 상태·전이·저장/hash·조회·검증이 연결됨 |
| `PC Adapt` | 플랫폼 전용 전달 경계를 PC로 교체했고 나머지 요구 전체를 증명함 |
| `PC_TEMP_REVIEW` | 구현과 테스트는 있으나 승인되지 않은 공식·수치·분포가 안정 review ID로 남음 |
| `Platform Excluded` | 요구 행 전체가 Android 전용임. 혼합 행의 Android 절만 제외할 때는 행 전체를 제외하지 않음 |
| `Gap/Deferred` | 요구 문장의 일부 또는 전부를 증명하지 못함 |

### 3.2 개발 생명주기

```text
Proposed
  -> User Confirmed
  -> Implemented
  -> Validated
  -> Packaged
  -> Released
```

각 단계는 이전 단계의 자동 승격이 아니다.

- `Proposed`: 아이디어, 분석, 검토안
- `User Confirmed`: 결정 ID/URL, 확정 문장, 범위와 revision 존재
- `Implemented`: 실제 owner 코드와 Git SHA 존재
- `Validated`: 같은 SHA의 자동 검사와 필요한 직접 검증 존재
- `Packaged`: VERSION과 실행 파일 hash·제품 버전 존재
- `Released`: tag·공개 위치·asset hash와 source SHA가 연결됨

“코드가 있다”, “테스트가 있다”, “실행 파일이 있다”, “사용자에게 배포됐다”를 한 단어
`완료`로 합치지 않는다.

## 4. 기획·공식·임시값 승인 규칙

### 4.1 절대 임의 추가하지 않는 것

- 기능, 대형 시스템, 데이터 필드, 이름, 수치, 확률, 분포
- 미래 사용을 예상한 빈 권위 필드나 범용 map
- 승인되지 않은 Law effect, 장착·복용·수련 계수
- 0과 `DataUnavailable`을 구분하지 않는 기본값
- 편의를 위한 임시 호환 계층이나 범용 manager/event bus
- 아직 Production이 아닌 지도·조직·콘텐츠를 실제 세계에 존재하는 것처럼 보이는 UI

### 4.2 `PC_TEMP_REVIEW`를 추가할 때 필요한 기록

임시값이 없으면 세계 진행 자체가 막히고, 사용자가 임시 구현 정책을 허용한 경우에만
다음 정보를 모두 적는다.

1. 충돌 없는 안정 ID: `PC-TEMP-<DOMAIN>-<MEANING>-<VERSION>`
2. review revision과 owner 모듈
3. 임시로 정한 정확한 값·공식·분포
4. 왜 승인값이 아닌지와 원래 결정 출처
5. 영향을 받는 state/hash/RNG/checkpoint/query/UI
6. 교체 시 save migration이 필요한지
7. 동작·경계·결정론·저장·관찰 무영향 테스트
8. 관련 이식 원장 행 또는 “PC 내부 세부값이라 행 없음” 판정
9. 교체 조건, 담당자, 목표 버전

문서 문자열만 만들고 실제 코드의 formula/revision과 연결하지 않거나, 여러 의미에 같은
review ID를 재사용하지 않는다.

## 5. 모듈 배치 규칙

```text
Core -> Simulation -> Queries -> Runtime
                  \-> Persistence
Queries -> Presentation
Application = Core + Simulation + Queries + Runtime + Persistence 조정
Desktop     = Application + Presentation
```

| 질문 | 배치할 모듈 |
|---|---|
| 안정 ID, 정수 시간, RNG stream, 플랫폼 독립 값 계약인가? | Core |
| 세계의 권위 상태나 결정론적 상태 전이인가? | Simulation |
| 세계를 바꾸지 않는 화면·통계·ranking DTO인가? | Queries |
| worker, pulse, 배속, observation 요청·publication인가? | Runtime |
| checkpoint encoding, SQLite, checksum, 후보 복구, 원자 파일 lifecycle인가? | Persistence |
| 플랫폼 독립 표시 모델·한국어 포맷인가? | Presentation |
| 세션, 명령, 저장·불러오기 조정인가? | Application |
| WPF control·window·사용자 입력 전달인가? | Desktop |

### 금지 의존

- Core가 상위 모듈을 참조
- Simulation이 Queries·Runtime·Persistence·Presentation·Application·Desktop을 참조
- Queries가 Runtime·Persistence·Presentation·Application·Desktop을 참조
- Persistence가 Queries·Runtime·Presentation·Application·Desktop을 참조
- Desktop이 `World`, store, SoA 배열을 직접 생성·변경·순회
- UI가 RNG를 소비하거나 공식 통계를 갱신

새 project reference가 필요하다면 코드를 먼저 붙이지 말고 manifest의 DAG에서 허용되는지
판정한다.

## 6. 수직 기능 개발 순서

### 1단계 — 기능 ID와 결정 범위 고정

- 요구 ID, Notion 결정 ID/URL, 적용 revision을 기록한다.
- PC primary 구현인지, 모바일 reference 이식인지, PC Adapt인지 정한다.
- 확정·임시·Gap을 문장별로 구분한다.
- “이번 변경에서 하지 않는 것”을 명시한다.

### 2단계 — owner와 작업집합 지정

- manifest의 feature entrypoint를 먼저 확인한다.
- 대표 검색 진입 파일, 관련 companion 파일, 테스트를 목록화한다.
- 새 파일을 만들기 전에 같은 owner·기능·생명주기인지 확인한다.
- 구조 이동, 게임 규칙/RNG, save wire는 각각 별도 변경으로 계획한다.

### 3단계 — 최소 권위 계약 구현

- typed ID, typed command/result, lifecycle과 실패 상태를 정한다.
- 실패 전이는 state와 RNG를 바꾸지 않게 한다.
- 순서·표시 이름·Dictionary iteration에 의미가 의존하지 않게 한다.
- 미등록 ID는 `Undefined`/`Blocked`로 처리하고 READY나 기본값으로 보충하지 않는다.

### 4단계 — hash·checkpoint·migration 판정

- 새 권위 열은 canonical state hash에 포함한다.
- checkpoint와 복원 검증을 연결한다.
- 저장하지 않는 파생 cache·화면 행이면 그 판정을 추적 문서에 적는다.
- schema 변경이 필요하면 구 fixture와 명시적 migration을 추가한다.

### 5단계 — query DTO와 UI 연결

- Queries에서 불변 DTO와 section/page 요청을 정의한다.
- 닫힌 작업공간·접힌 상세는 DTO를 만들지 않는다.
- Presentation에서 문구·숫자 포맷을 끝낸다.
- Desktop은 표시와 입력 전달만 맡는다.
- 모바일 세로 화면 요구를 이식할 때는 WPF 정보 구조에 맞춘 `PC Adapt`를 기록한다.

### 6단계 — 검증

- 정상·경계·잘못된 ID·중복·손상 입력
- 동일 seed 결정론과 state hash
- 저장 왕복과 구 schema migration
- 관찰 전후 state hash·RNG 무영향
- 닫힌 section 0회와 page materialization 제한
- 10,000명 회귀·SOAK·성능 예산
- 필요한 직접 화면 시나리오

### 7단계 — 정본과 증거 동기화

- manifest owner·entrypoint·실제 csproj
- 이식 원장 disposition과 test literal
- 기능 추적 문서와 review ID
- CHANGELOG와 VERSION
- 구현 SHA, 검증 SHA, package hash, release 상태

## 7. 결정론·대규모 상태 규칙

### 시간과 스케줄

- 세계 시간은 `SimulationTick` 정수만 권위로 저장한다.
- `DateTime.Now`, frame delta, thread scheduling을 세계 공식에 넣지 않는다.
- AI는 매 렌더 frame이 아니라 행동 완료·중단·생명주기 경계에서만 다시 판단한다.
- 같은 달 행동 최대 1회 계약을 깨는 재예약을 금지한다.
- 월 환경 시스템은 인물 수만큼 반복하지 않고 월 경계에서 한 번 처리한다.

### RNG

- seed와 stream/domain을 명시한다.
- 새 domain은 기존 인물 RNG 순서를 재배열하지 않아야 한다.
- query·정렬·표시 이름 생성은 gameplay RNG를 소비하지 않는다.
- `Dictionary` 열거 순서나 런타임 hash code를 권위 결과에 사용하지 않는다.
- domain 수량 변경 전후 다른 domain의 fixture를 비교한다.

### 데이터 배치

- 10,000명 hot state는 연속 배열·작은 값 타입·안정 ID 참조를 우선한다.
- 문자열, WPF 타입, UI 문구를 권위 SoA 열에 저장하지 않는다.
- 관계·조직 연결은 무제한 객체 그래프 대신 bounded/sparse 구조를 우선한다.
- 로그·history·queue에는 명시적 용량과 drop/compaction 정책을 둔다.
- 성능 이유 없이 SoA를 per-agent 객체 그래프로 바꾸지 않는다.

### 상태 hash

- 동일 의미는 저장·복원 전후 같은 canonical 순서로 hash한다.
- query cache, 포맷 문자열, 화면 선택 상태는 권위 hash에서 제외한다.
- schema별 과거 hash 의미를 변경하지 않는다.
- 새 owner는 hash 포함 여부를 코드와 추적 문서에 동시에 적는다.

## 8. Query·UI 개발 규칙

- `ObservationSections`는 화면 정보 구조와 같은 단위로 설계한다.
- 선택하지 않은 외부 탭, 내부 탭, Expander는 상세 DTO를 요청하지 않는다.
- 10,000명 목록은 page/virtualization을 사용하고 한 번에 전체 문자열 행을 만들지 않는다.
- revision이 같으면 immutable projection을 재사용하고, 변경 영역만 invalidation한다.
- 검색·정렬·관찰은 state, RNG, ranking history와 공식 통계를 바꾸지 않는다.
- 문자열·한국어 수치 포맷은 Presentation 책임으로 모은다.
- WPF code-behind에 공식과 상태 전이를 복제하지 않는다.
- UI 버튼은 typed Application 명령을 전달하고 결과 상태를 표시한다.
- 직접 화면 검증을 중단했다면 자동 검사 성공과 별개로 pending을 남긴다.

### UI 검증 최소 세트

1. 기본 10,000명 세계 생성과 시간 진행
2. 일시정지, 1×/10×/25×/50×/100×
3. 검색·페이지·TOP 1,000 virtualization
4. 외부 탭과 내부 탭별 lazy query
5. 선택 인물 상세와 접기/펼치기
6. 저장·불러오기·손상 후보 표시
7. UI 배율·복원·최대화
8. 정상 종료 뒤 프로세스 0개

## 9. 저장·migration 규칙

### owner 경계

- Simulation은 canonical authority checkpoint의 의미와 검증을 소유한다.
- Persistence는 SQLite world schema, codec 호출, checksum, 파일 후보 lifecycle을
  소유한다.
- Application은 안전 경계 capture와 백그라운드 save/load, 진행률을 조정한다.
- Queries와 UI DTO는 저장 정본이 아니다.

### 저장 순서

```text
Safe Boundary
  -> Encode
  -> temporary SQLite
  -> integrity + payload hash + decode + candidate World restore + state hash
  -> backup
  -> atomic primary replacement
  -> manifest
```

- 임시 후보 검증 전에 primary를 덮어쓰지 않는다.
- 복원할 때 primary/temp/backup을 generation 순서와 candidate priority로 검사한다.
- Candidate World 검증이 끝난 뒤에만 live Runtime World를 교체한다.
- 기존 `.cmip`은 byte·timestamp를 바꾸지 않고 typed 비호환으로 거부한다.
- fixed schema와 width, endian, enum 범위를 명시한다.
- save wire 변경과 파일 구조 리팩터링을 같은 커밋에 섞지 않는다.
- `A110-M02-V13`의 53-table 물리 형태는 PC 필수 저장 계약에서 제외한다. PC는 같은
  논리 권위 도메인을 canonical `authority_snapshot`과 typed checkpoint로 보존하는
  `PC Adapt`를 사용하며, migration·손상 복구·성능은 계속 별도 증거로 검증한다.

## 10. 성능 개발 규칙

### 무엇을 따로 측정할까

- world 초기화
- 10,000명·20,000 tick 진행
- observation/query
- checkpoint 생성
- checkpoint 복원
- SQLite save/load
- UI section·page materialization
- managed allocation과 peak
- `_worldGate` 보유·대기 시간과 backlog

한 개의 총시간만 보고 원인을 추측하지 않는다.

### 재현 가능한 측정 프로토콜

1. 비교할 source SHA와 `VERSION`을 기록한다.
2. `Release`로 빌드한다.
3. 동일 population, seed, tick 수, observation section을 사용한다.
4. warm in-process 반복만 사용하지 말고 최소 5개의 독립 프로세스를 실행한다.
5. 중앙값을 기본으로, UI·catalog에는 p95와 row 수를 함께 기록한다.
6. 이전 고정 SHA·장기 baseline·직전 버전을 모두 비교한다.
7. absolute budget과 상대 회귀 budget을 동시에 확인한다.
8. 코드가 느려졌다는 이유로 budget 숫자를 완화해 통과시키지 않는다.
9. 중앙값 예산 통과와 모든 회차 예산 통과를 따로 기록하고 실패 회차를 버리지 않는다.

0.0.21의 보존 package evidence 비교점은 초기화 275ms, 진행 1,189ms, 관찰 63.0ms,
합계 1,535ms, checkpoint 117ms, 복원 565ms, SQLite 저장 973ms·복원 869ms다.
2026-09-02 감사 중 c559 Release 바이너리를 독립 5-process로 다시 실행한 값은 진행
중앙값 294ms, runner 총 329ms, SQLite 저장/복원 852/851ms였지만, 한 프로세스가
1,108/1,027ms로 권고 1초를 넘었다. 이 예산은 현재 hard test gate가 아니다. 두
캠페인은 동일 코드 계열이어도 절대값 차이가 크므로 어느 하나로 다른 기록을 덮어쓰지
않는다. 자세한 조건과 범위는
[`TECHNICAL_AUDIT_2026-09-02.md`](TECHNICAL_AUDIT_2026-09-02.md)를 따른다. 이 수치는
해당 장비·SHA·harness 기준이며 다른 PC의 보편 수치가 아니다.

### 최적화 우선순위

1. 프로파일과 단계별 계측으로 병목을 확인한다.
2. 전수 계산을 event/month boundary로 바꿀 수 있는지 본다.
3. revision cache와 section/page lazy projection을 확인한다.
4. hot loop의 문자열·LINQ·임시 배열·boxing을 제거한다.
5. 크기가 예측되는 `List`·`Dictionary`·배열은 용량을 미리 확보한다.
6. lock 안에는 권위 capture만 두고 포맷·I/O는 밖으로 옮길 수 있는지 측정한다.
7. 마지막으로 알고리즘·자료구조 변경을 검토하고 결정론 fixture를 비교한다.

## 11. 리팩터링 규칙

### 작업집합이 기준이다

1,000줄은 정보성 경고다. 다음 질문으로 분리 여부를 판정한다.

- 같은 기능을 바꿀 때 항상 함께 검색하는가
- 같은 owner와 생명주기를 갖는가
- 같이 hash·checkpoint·migration되는가
- 별도 테스트 fixture가 있는가
- 분리하면 public API나 순환 의존이 늘어나는가

작은 companion 파일을 무조건 늘리지 않는다. 반대로 authority, query, persistence,
WPF, test, generated/manual 경계를 파일 수를 줄이기 위해 합치지 않는다.

### 현재 우선 후보

- `World.cs`: scheduler를 보존하고 hash·ranking section의 구조-only 분리 검토
- `World.Persistence.cs`: domain codec section 분리, canonical 순서·schema 의미 보존
- `SlotSaveStore.cs`: catalog header, candidate 검증, atomic lifecycle 작업집합 분리
- `World.Queries.cs`: 기능별 query-owned DTO로 점진 이동
- `KoreanNumberFormatter.cs`: Core에서 Presentation으로 owner 교정
- `MainWindow.AgentObservation.cs`·`MainWindow.xaml`: 내부 탭 단위 view/presenter 분리
- `tests/Chronicles.Tests/Program.cs`: domain suite 등록기로 축소

### 리팩터링 금지 패턴

- `Manager`, `Helper`, `Common`, `Misc`로 책임을 숨김
- 범용 `Dictionary<string, object>`나 event bus로 typed 계약 제거
- 구조 이동과 공식·RNG·save wire 동시 변경
- anonymous/static symbol, namespace, initialization 순서를 확인하지 않은 기계 병합
- 줄 수를 줄이려고 authority와 query DTO를 같은 파일에 합침
- 테스트 이름·fixture·benchmark를 삭제해 변경을 통과시킴

## 12. 변경 종류별 테스트 매트릭스

| 변경 종류 | 필수 검사 |
|---|---|
| Core ID·시간·RNG | 단위 경계, 잘못된 값, deterministic fixture, 기존 stable ID 보존 |
| Simulation 공식·상태 | 정상·실패 원자성, 동일 seed hash, SOAK, ranking·chronicle 영향 |
| 새 authority store | owner/revision, typed command/result, index 검증, hash/checkpoint/migration |
| Query | section off 시 null/0회, page 제한, DTO 불변, state/RNG 무영향 |
| Runtime | pause/speed, worker fault, lock 안전성, invalidation, backlog·drop policy |
| Persistence | schema, checksum, round-trip hash, corruption, primary/temp/backup, old fixture |
| Presentation/Desktop | 포맷 경계, 선택 view만 갱신, virtualization, 직접 화면 시나리오 |
| 성능 | 5개 Release 프로세스 중앙값, p95, allocation, 이전 SHA 비교 |
| 구조-only | architecture validator, 전체 test 동일, save/hash/RNG 결과 동일 |

## 13. 표준 로컬 검증 명령

```powershell
./scripts/validate-architecture.ps1
./scripts/validate-port-ledger.ps1
dotnet build Chronicles.slnx -c Release
dotnet run --project tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release --no-build
```

로컬 self-contained 패키지와 직접 실행까지 검증할 때:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-local.ps1 -CreateDesktopShortcut -Launch
```

패키징 명령은 `artifacts`와 바탕화면 바로가기를 변경하므로, 분석·문서 작업에서는
실행하지 않는다.

## 14. VERSION·Git·릴리스 규칙

- `VERSION` 하나만 제품 버전 원본으로 사용한다.
- 사용자 기능 업데이트 시작 시 patch를 한 번만 올린다. major/minor는 사용자 결정 없이
  바꾸지 않는다.
- 같은 버전 개발 중 작은 커밋마다 다시 version bump하지 않는다.
- 기능 변경, 구조 리팩터링, wire migration, evidence 문서를 구분 가능한 커밋으로 남긴다.
- 생성물과 `bin/obj/artifacts`를 커밋하지 않는다.
- 구현 SHA를 고정한 뒤 Release build·전체 tests·성능·패키지·직접 실행을 순서대로
  증명한다.
- 중간 커밋마다 GitHub Actions를 실행하지 않는다. 최종 통합 고정 SHA에서 필요한
  무거운 검증만 실행한다.
- 문서만 갱신됐다고 제품 source SHA의 검증 결과가 바뀐 것처럼 쓰지 않는다.
- push, tag, GitHub Release가 실제로 끝나기 전에는 `Released`로 표시하지 않는다.

## 15. evidence 작성 템플릿

```text
VERSION:
기능/요구 ID:
기획 결정 ID·revision:
source SHA:
validation SHA:
package SHA:
release tag/SHA:

Release build: warnings / errors
architecture validator:
port-ledger validator:
tests: passed / total
determinism fixture:
save schema / migration:
population / ticks / seed:
independent processes:
init / advance / query / checkpoint / restore:
SQLite save / load:
UI direct scenarios:
artifact path / bytes / SHA-256 / product version:
PC_TEMP_REVIEW:
Gap/Deferred:
not run / interrupted / unavailable:
```

실행하지 않은 검사는 빈 성공으로 두지 말고 `Not run`과 이유를 적는다. 사용자가 중단한
검사는 `Interrupted`; 이전 버전 증거를 재사용하면 그 SHA를 명시한다.

## 16. Definition of Done

기능을 완료라고 부르기 전에 모두 확인한다.

- [ ] 요구 문장과 기획 결정 ID가 고정됐다.
- [ ] 확정·임시·Gap이 구분됐다.
- [ ] owner와 대표 진입점이 manifest에 맞는다.
- [ ] 역방향 project reference와 순환 의존이 없다.
- [ ] 권위 상태·전이·실패 원자성이 구현됐다.
- [ ] hash·checkpoint·migration 또는 명시적 비저장 판정이 있다.
- [ ] query DTO와 section/page lazy 경계가 있다.
- [ ] UI가 World·SoA를 직접 읽거나 변경하지 않는다.
- [ ] 정상·경계·잘못된 ID·결정론·저장·관찰 무영향 테스트가 있다.
- [ ] 관련 성능 budget과 회귀 비교가 통과했다.
- [ ] architecture와 port-ledger validator가 통과했다.
- [ ] 기능 추적·manifest·CHANGELOG·evidence가 같은 사실을 말한다.
- [ ] 필요한 직접 UI 시나리오가 완료되거나 pending으로 명시됐다.
- [ ] package hash와 제품 버전이 기록됐다.
- [ ] 실제 공개한 경우에만 release tag·asset hash가 기록됐다.

## 17. 현재 우선 개발 백로그

1. 원장 `Gap/Deferred` 63개를 사용자 기획 우선순위로 재정렬한다.
2. `PC_TEMP_REVIEW` 31개 요구 행과 repo-wide 55개 review ID를
   `PC_TEMP_REVIEW_LEDGER.md`에서 분리 추적하고 결정 URL·교체 조건을 채운다.
3. 53-table SQLite 요구의 PC Adapt 결정을 원장·저장 계약·migration 증거에 계속
   일관되게 반영한다. 물리 table 수를 완료 기준으로 다시 사용하지 않는다.
4. 최신 0.0.21 Windows 직접 UI·저장·종료 시나리오를 재검증한다.
5. Query DTO와 한국어 formatter의 owner 부채를 구조-only 변경으로 해결한다.
6. 대형 Simulation·WPF·test 파일을 작업집합 기준으로 점진 분리한다.
7. `_worldGate`, checkpoint, SQLite, lazy query의 단계별 계측을 계속 유지한다.
8. GitHub Release가 필요하면 최종 통합 SHA에서만 원격 검증·tag·asset을 연결한다.

이 순서는 구현 편의보다 권위 명확성, 저장 호환성, 결정론, 사용자에게 보이는 검증을
우선한다.
