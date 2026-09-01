# 선로만상록 PC 기술 감사 보고서

> 감사일: 2026-09-02 KST
>
> 제품 감사 기준: `VERSION 0.0.21`, GitHub/local `main` 고정 SHA
> `7eb4d540b328d4664778b3e494148db0cd36bee2`
>
> VERSION 완료·패키지 원본 SHA:
> `f837e4cb6b40ddeba8029139dd710f4c457869ad`
>
> 최신 재검증 바이너리 기준:
> `c559063d35ebd5e8f0c6763ad009d9b74d8fb40d` Release build
> `c559063`에서 `7eb4d54`까지 `src` tree는
> `de39974c462c8ade192a4a06f9d4cf46248c8e82`, `tests` tree는
> `c9cb7e78cb95e584e58bcc733f0e56ee240c6a80`로 동일하다. 그 사이 커밋은 제품 코드·
> 테스트·`VERSION`을 바꾸지 않은 evidence 문서 변경이다.

이 문서는 현재 코드를 이해하기 위한 설명, 실제 모듈·알고리즘·파일 구조, 검증된
기술 수준, 문서와 코드의 차이, 기획 검토가 필요한 항목, 리팩터링 우선순위를 한 곳에
정리한다. 감사 과정과 이 문서 작성에서는 제품 소스, `VERSION`, manifest, 기존
추적 문서를 수정하지 않았다.

> **Notion 통합 보고서:**
> [2026-09-02 선로만상록 전체 기술 감사·개발 가이드·리팩터링·성능 계획](https://app.notion.com/p/3ceacc9e6175816a9ba7e07d18626f24)

## 1. 결론부터 보기

선로만상록 PC는 단순 화면 시제품이 아니라 다음 기반을 갖춘 결정론적 관찰
시뮬레이션이다.

- 10,000명 규모의 인물 상태를 연속 배열 중심으로 보관한다.
- 매 프레임 10,000명을 모두 갱신하지 않고, 다음 행동 시각이 된 인물만 우선순위
  큐에서 꺼내 처리한다.
- 세계 상태 변경은 `Simulation`, 읽기 조합은 `Queries`, 작업 스레드와 배속은
  `Runtime`, 저장 파일은 `Persistence`, WPF 화면은 `Desktop`이 맡는다.
- 동일 seed·동일 설정·동일 명령이면 동일 상태 hash가 나와야 한다는 계약이 코드와
  테스트에 연결되어 있다.
- 저장은 임시 SQLite 후보를 완전히 복원·검증한 뒤 primary를 교체한다.
- 닫힌 화면의 상세 자료는 만들지 않고, 열린 section과 page만 조회한다.

현재 기술 기반은 **로컬 개발·회귀 검증 단계에서는 강한 편**이다. 0.0.21 증거에는
Release 빌드 경고 0·오류 0, 자동 검사 65/65, 아키텍처·이식 원장 PASS,
self-contained Windows 산출물 hash가 남아 있다. 보존된 패키지 측정은 성능 예산을
통과했다. 감사 중 새 5-process 재실측에서도 전체 검사는 모두 통과했지만, SQLite
저장·복원 한 회차가 권고 1초를 넘었다. 이 예산은 현재 hard test gate가 아니므로
빌드는 성공했지만 저장 지연의 꼬리 변동은 계속 관찰해야 한다. 다음도 함께 기억해야 한다.

- 고정 SHA의 이식 원장 238개 중 완전 이식 계열은 `Port 128 + PC Adapt 15`, 임시
  검토는 31, 미완료·유예는 64다. 238개 전부가 완료된 것이 아니다.
- 0.0.21의 최신 직접 화면 조작은 사용자의 Esc 중단 뒤 재개하지 않았다. 자동 검사와
  산출물 존재는 최신 사용자 동작 검증을 대신하지 않는다.
- GitHub Actions와 GitHub Release는 0.0.21 검증에 사용하지 않았다. 로컬 패키징과
  공개 릴리스는 서로 다른 상태다.
- 모바일 정본의 53-table 물리 분리를 PC에서 그대로 이식할지, 논리 권위 도메인을
  보존하는 PC 저장 Adapt로 판정할지는 사용자 결정과 별도 고정 SHA 검증 전까지
  `Gap/Deferred`로 유지한다.
- 1,000줄이 넘는 파일 자체가 실패 조건은 아니지만, Simulation·WPF·테스트의 일부
  작업집합은 한 변경에서 함께 읽어야 하는 범위가 크다.

## 2. 무엇이 정본인가

이 감사에서는 다음 순서로 사실을 판정했다.

1. [`AGENTS.md`](../AGENTS.md): 제품 경계, 의존 방향, 결정론, 버전·검증 규칙
2. [`repository_manifest.json`](architecture/repository_manifest.json): 8개 모듈의
   owner, 허용 의존, 대표 진입점
3. 실제 `*.csproj`, C#·XAML 코드와 테스트
4. [`pc-port-ledger.json`](traceability/pc-port-ledger.json)과 기능별 추적 문서
5. [`0.0.21 로컬 검증 증거`](evidence/0.0.21/verification.md)
6. README·변경 이력·과거 버전 문서는 설명과 역사 자료로 사용

서로 다른 종류의 정본을 한 문장으로 섞지 않는다.

- **기획 결정 정본**은 무엇을 만들지 정한다.
- **코드·manifest 정본**은 지금 어느 모듈이 무엇을 실제로 소유하는지 정한다.
- **테스트와 evidence**는 어떤 SHA에서 무엇을 검증했는지 증명한다.
- **GitHub Release**는 무엇을 사용자에게 공개했는지 증명한다.

### PC primary / 모바일 reference 경계

이 PC 저장소의 실행·저장·성능·UI 완료 판정에서는 **PC C# 코드가 primary 구현
정본**이다. 모바일 C++/Android의 Alpha 요구사항은 기능 의미와 누락을 찾는 reference
입력이다.

[`traceability/README.md`](traceability/README.md)는 모바일
`ALPHA_1_1_0_REQUIREMENT_COVERAGE_V1`의 238개 문장을 원문·Notion URL·SHA와 함께
복사하고, PC 증거를 별도 연결하도록 규정한다. 따라서 다음은 금지된다.

- 모바일 C++ 파일이나 Android 테스트가 존재한다는 이유만으로 PC 행을 `Port`로
  승격하는 것
- JNI·Activity·Android lifecycle을 WPF에서 그대로 모방하는 것
- PC에서 임시로 정한 공식이나 수치를 모바일 정본에 역으로 덮어쓰는 것
- 모바일 요구 문장의 일부만 구현하고 전체 문장을 완료로 표시하는 것

플랫폼 전달 표면만 다르면 `PC Adapt`, 공식·수치 검토가 남으면
`PC_TEMP_REVIEW`, 일부라도 증명하지 못하면 `Gap/Deferred`를 유지한다.

## 3. 5분 만에 이해하는 구조

### 3.1 쉬운 비유

세계 전체를 하나의 방송국으로 보면 다음과 같다.

- `Core`는 모든 부서가 공유하는 시계, 주민등록번호, 난수 규칙과 계약 사전이다.
- `Simulation`은 실제 세계다. 인물이 수행하고 싸우며 이동하고 거래하는 권위 상태는
  여기서만 변한다.
- `Queries`는 카메라다. 세계를 바꾸지 않고, 현재 화면에 필요한 장면만 불변 DTO로
  찍는다.
- `Runtime`은 방송 송출실이다. worker가 시간을 진행시키고 배속·일시정지·조회 요청을
  조정한다.
- `Persistence`는 금고다. 세계 checkpoint를 SQLite에 안전하게 넣고, 손상된 후보를
  실제 세계와 바꾸지 않는다.
- `Presentation`은 자막 편집실이다. 플랫폼에 독립적인 표시 모델과 한국어 표현을 맡는다.
- `Application`은 제작 책임자다. Runtime·Queries·Persistence를 한 세션으로 묶고
  저장·불러오기 명령을 조정한다.
- `Desktop`은 WPF 화면과 입력 장치다. 세계를 직접 소유하지 않고 Application 명령과
  Presentation 결과만 사용해야 한다.

### 3.2 허용 의존 DAG

화살표는 아래 계층의 계약을 위 계층이 사용할 수 있는 방향이다.

```text
Core -> Simulation -> Queries -> Runtime
                  \-> Persistence
Queries -> Presentation

Application = Core + Simulation + Queries + Runtime + Persistence 조정
Desktop     = Application + Presentation
```

실제 `ProjectReference`도 manifest의 허용 집합과 일치하며, 2026-09-02 재검사에서
production module 8개와 Desktop authority pattern 0개로 PASS했다.

### 3.3 한 번의 화면 갱신이 지나가는 길

```text
WPF 입력
  -> Application 세션 명령
  -> Runtime의 _worldGate 안전 경계
  -> Simulation World의 결정론적 상태 전이
  -> Queries가 요청 section/page만 불변 DTO로 투영
  -> Presentation이 표시 문구와 모델을 조합
  -> Desktop이 화면에 렌더링
```

관찰은 이 경로를 반대로 이용해 세계를 읽을 뿐이며, 세계 상태·RNG·공식 통계를
바꾸면 안 된다.

저장은 별도 흐름을 가진다.

```text
Runtime 안전 경계에서 authority checkpoint 캡처
  -> Application 백그라운드 작업
  -> Persistence가 temporary SQLite 작성
  -> integrity + payload hash + checkpoint decode + World restore + state hash 검증
  -> backup 보존
  -> primary 원자 교체
  -> manifest 갱신
```

## 4. 8개 모듈과 실제 책임

| 모듈 | 실제 직접 의존 | 현재 owner 책임 | 금지해야 할 책임 | 대표 근거 |
|---|---|---|---|---|
| Core | 없음 | 안정 ID, 정수 시간, 결정론 RNG, 권위 registry, 값 계약 | 세계 상태, 파일 I/O, WPF | [`Identifiers.cs`](../src/Chronicles.Core/Identifiers.cs), [`DeterministicRandom.cs`](../src/Chronicles.Core/DeterministicRandom.cs), [`AuthorityRegistry.cs`](../src/Chronicles.Core/AuthorityRegistry.cs) |
| Simulation | Core | SoA 세계 상태, 수행·전투·윤회·경제·조직·콘텐츠와 결정론 전이 | WPF, SQLite 파일 lifecycle, 화면 갱신 | [`World.cs`](../src/Chronicles.Simulation/World.cs), [`AgentStore.cs`](../src/Chronicles.Simulation/AgentStore.cs) |
| Queries | Core, Simulation | 요청 section에 따른 읽기 전용 관찰 경계와 page 투영 | 상태 변경, RNG 소비, UI control | [`WorldObservationQueries.cs`](../src/Chronicles.Queries/WorldObservationQueries.cs) |
| Runtime | Core, Simulation, Queries | worker, 80ms pulse, 배속, world lock, 관찰 무효화, 성능 계측 | 저장 파일 형식, WPF 렌더링 | [`SimulationRunner.cs`](../src/Chronicles.Runtime/SimulationRunner.cs) |
| Persistence | Core, Simulation | SQLite world schema 25, checkpoint codec 연결, 후보 검증, 원자 파일 교체 | UI DTO, AI 규칙 | [`SlotSaveStore.cs`](../src/Chronicles.Persistence/SlotSaveStore.cs) |
| Presentation | Core, Queries | 플랫폼 중립 표시 모델과 한국어 포맷 | World 변경, WPF control | [`PresentationText.cs`](../src/Chronicles.Presentation/PresentationText.cs) |
| Application | Core, Simulation, Queries, Runtime, Persistence | 세션 구성, 명령, 비동기 저장·불러오기, 진행률·진단 조정 | 구체 WPF control, 게임 공식 재정의 | [`GameApplicationSession.cs`](../src/Chronicles.Application/GameApplicationSession.cs) |
| Desktop | Application, Presentation | WPF shell, 열린 view만 렌더링, 입력 전달 | `World` 직접 생성·변경, SoA 열 직접 접근 | [`MainWindow.xaml.cs`](../src/Chronicles.Desktop/MainWindow.xaml.cs), [`MainWindow.Workspace.cs`](../src/Chronicles.Desktop/MainWindow.Workspace.cs) |

## 5. 핵심 알고리즘, owner와 증거

| 알고리즘·계약 | 쉬운 설명 | 권위 owner | 구현·검증 근거 |
|---|---|---|---|
| 안정 ID·정수 시간·결정론 RNG | 표시 이름이나 실행 순서가 달라도 같은 개체와 난수 흐름을 다시 찾는다. 벽시계 시간이 세계 상태에 들어가지 않는다. | Core | [`Identifiers.cs`](../src/Chronicles.Core/Identifiers.cs), [`GameCalendar.cs`](../src/Chronicles.Core/GameCalendar.cs), [`DeterministicRandom.cs`](../src/Chronicles.Core/DeterministicRandom.cs), [`AuthorityFoundationTests.cs`](../tests/Chronicles.Tests/AuthorityFoundationTests.cs) |
| 이벤트 기반 인물 스케줄러 | `PriorityQueue<int,(Tick,Agent)>`에서 행동 시각이 된 인물만 꺼낸다. 한 인물은 같은 달에 최대 한 번 행동하며 다음 완료 시점을 다시 예약한다. | Simulation | [`World.AdvanceTo`, L187-L319](../src/Chronicles.Simulation/World.cs#L187-L319), [`World.ScheduleAgent`, L2038](../src/Chronicles.Simulation/World.cs#L2038) |
| 월 환경 일괄 진행 | 같은 월에 많은 인물 행동이 몰려도 도시 산업·요수·조직·지속 생성은 그 월에 한 번만 확인한다. | Simulation | [`World.cs`, L194-L216](../src/Chronicles.Simulation/World.cs#L194-L216) |
| SoA 인물 저장 | 인물마다 큰 객체 하나를 두는 대신 경지·체력·위치·다음 행동 등을 타입별 연속 배열에 둔다. 10,000명 순회와 hash에 유리하다. | Simulation | [`AgentStore.cs`, L5-L219](../src/Chronicles.Simulation/AgentStore.cs#L5-L219) |
| 수행 상태기계와 장기 계획 | Work 축적, 준비, Preflight, 시도, 병목/안착을 나누고 6·12·24개월 pulse가 경계를 넘지 않게 처리한다. | Simulation | [`World.Cultivation.cs`](../src/Chronicles.Simulation/World.Cultivation.cs), [`World.CultivationPlans.cs`](../src/Chronicles.Simulation/World.CultivationPlans.cs), [`RealmRules.cs`](../src/Chronicles.Simulation/RealmRules.cs) |
| 전투력·랭킹 revision cache | 관련 입력이 바뀐 랭킹만 dirty로 표시한 뒤 정렬하고 역순위 배열을 갱신한다. | Simulation | [`CombatPowerFormula.cs`](../src/Chronicles.Simulation/CombatPowerFormula.cs), [`RankingRevisionCache.cs`](../src/Chronicles.Simulation/RankingRevisionCache.cs), [`World.RefreshRankings`, L1562](../src/Chronicles.Simulation/World.cs#L1562) |
| 인과·윤회·희소 관계 | Goal→Plan→Task→Action과 사건 provenance를 남기고, Soul/Body/Person 생명주기와 제한된 adjacency 관계를 분리한다. | Simulation | [`AgentCausalRuntime.cs`](../src/Chronicles.Simulation/AgentCausalRuntime.cs), [`ReincarnationModel.cs`](../src/Chronicles.Simulation/ReincarnationModel.cs), [`RelationModel.cs`](../src/Chronicles.Simulation/RelationModel.cs) |
| 도시 시장 원자 계약 | Offer→Reservation→만료/취소/체결을 typed result로 처리하고 최근 수급·재고·생산을 월별로 갱신한다. | Simulation | [`CityMarket.Contracts.cs`](../src/Chronicles.Simulation/CityMarket.Contracts.cs), [`CityMarket.Settlement.cs`](../src/Chronicles.Simulation/CityMarket.Settlement.cs), [`CityMarket.cs`](../src/Chronicles.Simulation/CityMarket.cs) |
| Content·Knowledge·Law 권위 | Definition/Instance, 판본/실물/접근, 법칙 후보/개인 이해/세계 판단을 서로 다른 owner와 hash로 보존한다. 승인되지 않은 Law 효과는 실행하지 않는다. | Simulation | [`ContentAuthorityStore.cs`](../src/Chronicles.Simulation/ContentAuthorityStore.cs), [`KnowledgeAuthorityStore.cs`](../src/Chronicles.Simulation/KnowledgeAuthorityStore.cs), [`LawAuthorityStore.cs`](../src/Chronicles.Simulation/LawAuthorityStore.cs) |
| 절차 생성 domain 분리 | Map·Monsters·Items·Books·OngoingContent가 별도 domain seed를 써서 한 영역의 수량 변경이 다른 영역과 인물 RNG 순서를 바꾸지 않는다. 다음 생성 월을 캐시하고 실제 생성 회차에만 용량을 미리 확보한다. | Simulation | [`WorldGenerationModel.cs`, L144](../src/Chronicles.Simulation/WorldGenerationModel.cs#L144), [`WorldGenerationCatalog.cs`, L214-L281](../src/Chronicles.Simulation/WorldGenerationCatalog.cs#L214-L281), [`WorldGenerationTests.cs`](../tests/Chronicles.Tests/WorldGenerationTests.cs) |
| 조직 희소 권위 | 조직 정의·구성원·지도부·재정·계획을 typed transition과 제한된 월별 AI 평가로 유지한다. | Simulation | [`OrganizationModel.cs`](../src/Chronicles.Simulation/OrganizationModel.cs), [`OrganizationTransitions.cs`](../src/Chronicles.Simulation/OrganizationTransitions.cs), [`World.Organizations.cs`](../src/Chronicles.Simulation/World.Organizations.cs) |
| 선택적 관찰 projection | 화면이 요청한 `ObservationSections`와 page 범위만 DTO로 만든다. 접힌 상세나 닫힌 작업공간은 계산하지 않는다. | Queries, 일부 전환 DTO는 Simulation | [`WorldObservationQueries.cs`, L10-L138](../src/Chronicles.Queries/WorldObservationQueries.cs#L10-L138), [`World.Queries.cs`](../src/Chronicles.Simulation/World.Queries.cs) |
| worker·배속·동시성 경계 | 80ms마다 `16 × speed` tick을 진행하고, 진행·조회·checkpoint·복원을 `_worldGate`로 직렬화한다. | Runtime | [`SimulationRunner.cs`, L333-L362](../src/Chronicles.Runtime/SimulationRunner.cs#L333-L362), [`SimulationPacing`, L480-L493](../src/Chronicles.Runtime/SimulationRunner.cs#L480-L493) |
| 검증 후 원자 저장 | temporary DB를 integrity·SHA-256·decode·복원·상태 hash까지 검증하고 backup 뒤 primary로 교체한다. 실패 후보는 live world를 바꾸지 않는다. | Persistence + Application 조정 | [`SlotSaveStore.cs`, L384-L452](../src/Chronicles.Persistence/SlotSaveStore.cs#L384-L452), [`GameApplicationSession.cs`, L163-L219](../src/Chronicles.Application/GameApplicationSession.cs#L163-L219), [`SqliteSlotPersistenceTests.cs`](../tests/Chronicles.Tests/SqliteSlotPersistenceTests.cs) |

## 6. 파일·폴더·작업집합

### 6.1 폴더 역할

| 경로 | 역할 |
|---|---|
| `src/Chronicles.*` | 8개 production 모듈 |
| `tests/Chronicles.Tests` | 공식·결정론·저장·성능·수직 기능 회귀 |
| `docs/architecture` | owner와 DAG 기계 정본 |
| `docs/traceability` | 모바일 요구 문장과 PC 증거의 일대일 추적 |
| `docs/evidence/<version>` | 고정 SHA의 빌드·테스트·성능·산출물 증거 |
| `docs/baselines` | 장기 성능 비교 기준 |
| `scripts` | 버전 증가, 구조·원장 검증, 로컬 빌드·패키징 |
| `artifacts` | Git에 넣지 않는 로컬 self-contained 산출물 |

### 6.2 production 모듈 크기

2026-09-02 작업트리에서 `bin/obj`를 제외한 수동 `*.cs`·`*.xaml` 파일을 세었고,
줄 수는 빈 줄을 제외했다. 이 수치는 분리 명령이 아니라 작업집합 위험을 찾는 정보다.

| 모듈 | 파일 수 | 빈 줄 제외 줄 수 | 해석 |
|---|---:|---:|---|
| Core | 8 | 994 | 작은 기반 모듈이지만 표시 포맷 owner 한 건을 재검토해야 한다. |
| Simulation | 61 | 29,111 | 제품의 대부분을 소유한다. 기능별 partial·store 경계가 핵심이다. |
| Queries | 2 | 268 | 물리적으로 매우 얇고 일부 DTO·투영이 Simulation에 남아 있다. |
| Runtime | 1 | 447 | 단일 runner 작업집합이며 lock·worker·진단이 함께 있다. |
| Persistence | 5 | 1,630 | 외부 SQLite lifecycle은 분리됐지만 주 store가 크다. |
| Presentation | 1 | 115 | manifest 책임에 비해 얇고 일부 한국어 포맷이 Core에 있다. |
| Application | 7 | 992 | 세션·진단·로그·저장 조정이 비교적 작게 분리돼 있다. |
| Desktop | 28 | 11,316 | 화면별 view가 있으나 MainWindow 중심 작업집합은 여전히 크다. |

### 6.3 큰 수동 파일과 의미 있는 분리 후보

| 파일 | 물리 줄 수 | 판정 |
|---|---:|---|
| `tests/Chronicles.Tests/Program.cs` | 4,632 | 테스트 등록·공통 회귀·성능 harness가 한 진입점에 몰려 검색 범위가 크다. domain suite 등록기로 축소할 후보다. |
| `src/Chronicles.Simulation/World.Persistence.cs` | 2,373 | canonical checkpoint owner는 Simulation에 남기되 schema/domain codec section을 의미 단위로 분리할 후보다. |
| `src/Chronicles.Simulation/World.cs` | 2,231 | scheduler·hash·ranking이 함께 있다. 구조-only 커밋으로 `World.Hash`와 `World.Rankings`를 먼저 검토할 수 있다. |
| `src/Chronicles.Simulation/OrganizationModel.cs` | 1,654 | 모델·store·검증의 생명주기를 확인한 뒤 transition 계약과 조회 형식을 분리할 후보다. |
| `src/Chronicles.Desktop/MainWindow.AgentObservation.cs` | 1,495 | 인물 상세 내부 탭별 presenter/view 조정으로 검색 범위를 줄일 후보다. |
| `src/Chronicles.Desktop/MainWindow.xaml` | 1,471 | 화면 경계가 이미 정해진 영역부터 UserControl로 옮기되 binding 계약을 바꾸지 않아야 한다. |
| `src/Chronicles.Simulation/World.Cultivation.cs` | 1,261 | 수행 공식·상태 전이가 강하게 결합돼 있어 줄 수만으로 분리하면 안 된다. |
| `src/Chronicles.Persistence/SlotSaveStore.cs` | 1,144 | catalog header, candidate 검증, atomic lifecycle을 내부 역할별로 나눌 후보다. |
| `src/Chronicles.Simulation/AgentStore.cs` | 1,112 | SoA 열은 함께 순회·hash·codec하는 작업집합이므로 임의 객체화나 작은 파일 분리는 금지한다. |

## 7. 현재 기술 성숙도

평가는 숫자 점수 대신 증거 수준과 남은 한계를 함께 적는다.

| 영역 | 판정 | 근거 | 남은 한계 |
|---|---|---|---|
| 모듈 경계 | 강함 | 8개 project DAG, manifest validator PASS, Desktop authority pattern 0 | Query/Presentation의 물리 owner 부채가 일부 남음 |
| 결정론·권위 | 강함 | seed/RNG stream, 정수 tick, 상태 hash, 관찰 무영향, checkpoint 왕복 검사 | 새 기능마다 hash/save/RNG 연결을 계속 강제해야 함 |
| 자동 회귀 | 강함(로컬) | Release 전체 runner 1회와 독립 5개 프로세스가 모두 65/65, SOAK, 손상 저장 거부, domain RNG 분리 | 단일 대형 test harness 작업집합, 원격 CI 미실행 |
| 저장·복구 | 양호 | SQLite schema 25, checkpoint 30, 후보 검증과 원자 교체, backup/temp 복구 | 53-table 물리 형태의 PC 이식/Adapt 판정과 논리 도메인·migration·성능 증거가 후속 |
| 시뮬레이션 성능 | 양호 | 보존 evidence의 총 1,535ms는 장기 기준 대비 30.6% 감소. 새 c559 바이너리 5-process 총시간 중앙값은 329ms | 캠페인 간 절대값 차이가 커 환경·명령 metadata를 더 고정해야 함 |
| 저장 성능 | 주의 | 새 5-process SQLite 중앙값은 저장 852ms·복원 851ms | 5개 중 1개가 1,108/1,027ms로 권고 1초를 넘음. 현재 hard test gate는 아님 |
| 관찰·UI 성능 | 양호 | 보존 evidence의 선택 UI 94.1% 감소. 새 query p95 중앙값 0.137ms, 1,000행 0.069ms | 최신 0.0.21 직접 화면 상호작용은 미검증 |
| 요구사항 완성도 | 혼합 | 238행 전수 추적, 완료·임시·Gap을 숨기지 않음 | 고정 SHA 기준 PC_TEMP 31, Gap/Deferred 64 |
| 공개 릴리스 | 미완료 | 로컬 산출물 hash·버전·바로가기 존재 | GitHub Actions/Release 미실행, 최신 직접 실행 확인 없음 |
| 유지보수성 | 주의 | owner·feature entrypoint가 manifest에 있음 | Simulation·MainWindow·test의 큰 작업집합, 18개 추가 review ID 정리 필요 |

## 8. 0.0.21 성능 스냅샷

### 8.1 패키지 evidence에 보존된 측정

[`verification.md`](evidence/0.0.21/verification.md)에 보존된 동일 조건 5회 중앙값은
다음과 같다. VERSION 완료·패키지와 연결된 공식 보존 기록이므로 새 재실측으로
덮어쓰지 않는다.

| 구간 | 중앙값 |
|---|---:|
| 10,000명 초기화 | 275ms |
| 20,000 tick 진행 | 1,189ms |
| 관찰 | 63.0ms |
| 합계 | 1,535ms |
| checkpoint 생성 | 117ms |
| checkpoint 복원 | 565ms |
| SQLite 저장 | 973ms |
| SQLite 복원 | 869ms |

- schema 30 최적화 전 1,812ms보다 15.3% 감소했다.
- 0.0.20의 1,484ms보다 3.4% 느리지만 허용 회귀 예산 15% 안이다.
- 장기 기준 0.0.13의 2,212ms보다 30.6% 감소해 30% 목표를 통과했다.
- 선택 UI 80회는 기존 이중 전체 조회 136.3ms에서 8.1ms로 94.1% 감소했다.
- live managed peak는 이전 container 대비 85.8% 줄었다.

### 8.2 감사 중 c559 Release 바이너리 재실측

감사 중 `c559063`에서 만든 Release 바이너리를 별도 프로세스 5개로 다시 실행했다.
현재 감사 기준 `7eb4d54`와 해당 바이너리의 `src`·`tests` tree hash가 각각
`de39974c462c8ade192a4a06f9d4cf46248c8e82`와
`c9cb7e78cb95e584e58bcc733f0e56ee240c6a80`로 같으므로 코드 동등 비교다. 전체 runner
사전 1회와 아래 5개 프로세스는 모두 65/65를 통과했다.

| 지표 | 5개 프로세스 값 | 중앙값 | 범위 |
|---|---|---:|---:|
| 20,000 tick 진행 ms | 423 / 321 / 272 / 294 / 271 | **294** | 271~423 |
| runner 총 ms | 490 / 362 / 306 / 329 / 308 | **329** | 306~490 |
| SQLite 저장 ms | 1,108 / 945 / 852 / 820 / 820 | **852** | 820~1,108 |
| SQLite 복원 ms | 1,027 / 851 / 853 / 804 / 793 | **851** | 793~1,027 |
| managed peak MiB | process별 범위 56.31~56.53 | **56.31** | 56.31~56.53 |
| summary query p95 ms | process별 p95 중앙값 | **0.137** | 0.104~0.205 |
| 1,000행 query p95 ms | process별 p95 중앙값 | **0.069** | 0.055~0.087 |

SQLite는 교차 프로세스 중앙값으로는 권고 1초 안이지만 첫 프로세스의 저장 1,108ms와
복원 1,027ms가 모두 이를 넘었다. 이 예산은 현재 65개 테스트의 hard gate가 아니므로
전체 검사는 성공했다. 그래도 “중앙값 예산 안”과 “5회 모두 예산 안”을 구분해 보고하고,
cold start, 파일 시스템 cache, 백신·동기화 I/O, DB flush 중 무엇이 원인인지는 단계별
계측 없이 단정하지 않는다.

패키지 evidence의 총 1,535ms와 새 runner 총 329ms는 동일 코드 계열이어도 측정
캠페인의 환경·harness 상태 차이가 크다는 신호다. 두 값을 임의로 평균 내거나 새 값으로
과거 evidence를 교체하지 않는다. 앞으로는 OS·CPU·전원 정책·dotnet 정보, 정확한
명령, artifact hash, cold/warm 조건까지 함께 남긴다.

두 캠페인 모두 **고정 코드·Release 구성·해당 PC의 로컬 증거**다. 다른 장비의 보편적
FPS나 최신 UI 체감 성능을 뜻하지 않는다. 성능 결론에는 항상 SHA, population, tick 수,
Release/Debug, 프로세스 반복 횟수와 중앙값·범위·실패 회차를 함께 적어야 한다.

## 9. 권위와 상태 분류

### 9.1 요구 이식 disposition

아래 수는 `main@7eb4d54`의 고정 감사 기준과 코드가 동일한 `c559063` Release 재검증에
사용한 이식 원장 집계다.

| 상태 | 0.0.21 고정 감사 수 | 의미 |
|---|---:|---|
| `Port` | 128 | 요구 문장 전체의 PC 권위·조회·검증 증거가 연결됨 |
| `PC Adapt` | 15 | 플랫폼 전달 표면을 PC 방식으로 바꾸고 PC 범위의 논리 계약을 유지함 |
| `PC_TEMP_REVIEW` | 31 | 동작·검증은 있지만 임시 공식·수치가 안정 review ID로 남음 |
| `Platform Excluded` | 0 | 행 전체가 Android 전용인 경우. 현재는 혼합 행의 PC 부분을 계속 추적하므로 0이 정상임 |
| `Gap/Deferred` | 64 | 요구 문장의 일부 또는 전부를 아직 증명하지 못함 |

### 9.2 개발 생명주기 상태

아래 상태는 위 disposition과 별개다. 예를 들어 `Port` 코드가 로컬 검증됐어도 공개
Release가 없으면 `Released`가 아니다.

| 상태 | 필요한 증거 |
|---|---|
| Proposed | 아이디어·초안. 게임 규칙과 수치를 바꿀 권한이 없음 |
| User Confirmed | 결정 ID/URL, 확정 문장, 범위, 결정일·revision |
| Implemented | owner 모듈의 실제 코드와 변경 SHA |
| Validated | 고정 SHA의 단위·결정론·저장·관찰 무영향·성능 또는 UI 결과 |
| Packaged | VERSION, 실행 파일 경로·크기·hash·제품 버전 |
| Released | 공개/배포 위치, release tag, release asset hash, release와 source SHA 연결 |

0.0.21은 자동 검증과 로컬 패키징 증거가 있으나, 최신 직접 UI 검증과 GitHub Release는
별도로 미완료라고 표시해야 한다.

## 10. 문서 드리프트와 검토가 필요한 항목

### 10.1 확인된 문서·owner 드리프트

| 우선순위 | 발견 | 근거 | 권장 조치 |
|---|---|---|---|
| P0 | 0.0.19 검증 절이 상세 증거를 `0.0.16/verification.md`로 연결한다. | [`LOCAL_TESTING.md`, L248-L260](LOCAL_TESTING.md#L248-L260) | 실제 0.0.19 evidence가 있으면 링크를 교정하고, 없으면 “보존되지 않음”으로 명시한다. |
| P0 | `traceability/README`의 “현재 기준선”은 0.0.13 SHA를 적지만 바로 아래 집계 설명은 0.0.21이다. | [`traceability/README.md`, L26-L38](traceability/README.md#L26-L38) | “원장 생성 기준/장기 성능 기준/현재 제품 버전”을 서로 다른 필드로 나눈다. |
| P1 | manifest는 한국어 포맷을 Presentation 책임으로 두지만 `KoreanNumberFormatter`는 Core에 있고 Desktop이 직접 사용한다. | [`repository_manifest.json`, Presentation](architecture/repository_manifest.json), [`KoreanNumberFormatter.cs`](../src/Chronicles.Core/KoreanNumberFormatter.cs) | 게임 규칙·wire 변경 없는 구조-only 커밋으로 Presentation에 이동한다. |
| P1 | Queries는 2파일·268줄인 반면 상당한 snapshot/DTO 생성이 Simulation의 `World.Queries.cs`에 남아 있다. | [`architecture/README.md` 전환 메모](architecture/README.md), [`World.Queries.cs`](../src/Chronicles.Simulation/World.Queries.cs) | 저장/RNG 변경과 섞지 말고 기능별 query-owned projection으로 점진 이동한다. |
| P1 | README의 수행 기획은 Alpha 1.1.3, 이식 원장 source contract는 Alpha 1.1.0이다. 서로 다른 범위일 수 있으나 문서만으로 적용 우선순위가 즉시 보이지 않는다. | [`README.md`, L71-L77](../README.md#L71-L77), [`repository_manifest.json`](architecture/repository_manifest.json) | 요구 baseline, 수행 설계 revision, PC override를 authority matrix로 명시한다. |

`c559063` 뒤 `c680284`와 `7eb4d54`는 원격/evidence 문구만 동기화했으며 제품 code,
tests, `VERSION`을 바꾸지 않았다. 최종 문서 병합·push 뒤에는 로컬 HEAD와
`origin/main`을 다시 비교해 evidence 문구의 SHA가 실제 원격과 같은지 확인해야 한다.

### 10.2 임의 추가로 오해하기 쉬운 검토 항목

감사 시점의 repo-wide 검색에는 서로 다른 `PC-TEMP-*` ID가 55개 있었다. 이식 원장
JSON이 직접 참조하는 ID는 37개이고, 18개는 게임 디자인·기능 추적·코드에서만 보인다.
이는 곧바로 “누락 요구 18개”라는 뜻은 아니다. 하나의 review ID가 여러 요구 행을
지원할 수 있고 PC 내부 세부 공식일 수도 있다. 이번 감사 workset에서 추가한
[`PC_TEMP_REVIEW_LEDGER.md`](traceability/PC_TEMP_REVIEW_LEDGER.md)는 55개를 한 행씩
모아 중앙 연결 여부와 현재 효과를 구분한다. 앞으로 이 대장을 유지하지 않으면 다음을
판단하기 어렵다.

- 누가 언제 어떤 값을 임시로 정했는가
- 사용자 확정과 구현 편의를 위한 기본값이 구분되는가
- 어떤 save/hash/RNG/UI에 영향을 주는가
- 교체할 때 migration이 필요한가
- 이미 확정·폐기됐는데 식별자가 남은 것은 없는가

특히 다음 0.0.21 항목은 evidence에도 명시된 열린 검토다.

- `PC-TEMP-CULT-DETAIL-STAGES-021`: 대경지별 5단계 이름·가중치
- `PC-TEMP-WORLD-GENERATION-021`: 초기 생성 수량·상한
- `PC-TEMP-PROCEDURAL-NAME-EFFECT-021`: 이름 token·효과 분포
- `PC-TEMP-CONTENT-GROWTH-021`: 10년 주기·회차당 아이템 4종·서적 2종

생성 효과는 profile과 UI까지만 연결되어 있고, 승인되지 않은 장착·복용·수련 계수를
실제 게임플레이에 적용하지 않은 상태가 올바르다.

### 10.3 명시적으로 남은 기능 범위

- 다중 대륙, 나머지 99개 주의 Production화, 지점 내부 장소 생성
- 실제 Law adapter·상태 I/O·metric·효과와 Stable LawId
- 조직 전승·Teaching·법칙 연구의 전체 인과 연결, 실제 지식 복원 공식
- 원장 `Gap/Deferred` 64개
- 0.0.21 Windows 직접 UI 시나리오 재검증
- GitHub Actions/Release가 필요한 경우 최종 고정 SHA 검증과 공개 산출물 연결

이 항목은 이미 존재하는 데이터처럼 UI에 표시하거나, 빈 필드·범용 map·기본값으로
미리 넣으면 안 된다.

`A110-M02-V13`의 53-table 물리 형태는 현재 고정 원장에서 미결 범위다. PC가 동일한
논리 권위·복구 의미를 다른 물리 구조로 제공하는 Adapt로 판정할지, 별도 물리 schema를
구현할지는 사용자 결정과 migration·성능·복구 증거가 함께 고정되기 전까지 확정하지
않는다.

이 감사 뒤 사용자는 전자의 `PC Adapt`를 확정했다. 현재 생성기는 해당 행을
`PC Adapt`로 분류하며 53개 물리 테이블을 다시 완료 조건으로 사용하지 않는다.

### 10.4 과거 `A110-ARCH-001~018`의 현재 재판정

이 표는 과거 Notion 계획을 완료로 소급 표시하는 표가 아니다. 계획 원문과 현재 접근 가능한
로컬 모바일 `VERSION 1.1.2`, `codex/alpha-1.1.2-implementation@2b035c4b`의
`AppState → alpha110::LiveRuntimeFacade110` 활성 경로를 대조한 재감사다. 공개 릴리스
source `761aff4…`와 로컬 checkout이 동일하다고 단정하지 않으며, private 저장소 최신
`main`·PR·Issue는 권한으로 인해 라이브 미확인이다.

| ID | 재판정 | 현재 근거 | 남은 조치 |
|---|---|---|---|
| `A110-ARCH-001` | 해결 | `architecture_contract.py`가 manifest·owner·DAG·CMake 등록을 직접 검사하고 1,000줄 초과는 정보성으로만 보고한다. | 없음 |
| `A110-ARCH-002` | 부분 해결 | CI job과 same-SHA 결과는 분리됐지만 코드 실패와 runner·권한·무로그 인프라 실패의 기계 판독 분류 계약은 없다. | `code/build/test/infrastructure` 원인과 재실행 근거를 evidence로 남긴다. |
| `A110-ARCH-003` | 현재 적용 안 됨 | 현재 활성 `alpha110`·`AppState` 경로는 legacy `AgentStore`를 사용하지 않는다. | 그대로 이전하지 않고 현행 DTO/schema 위험은 004에서 다룬다. |
| `A110-ARCH-004` | 부분 해결 | command ID는 단일 schema에서 생성되지만 dashboard JSON 키는 C++ serializer와 Java `JSONObject.opt*`에 수동 중복된다. | versioned DTO/schema와 필수 필드 실패 계약을 둔다. |
| `A110-ARCH-005` | 해결 | manifest·CMake source 목록과 architecture contract가 실제 engine/app/test/benchmark `.cpp`의 누락·중복·죽은 경로를 대조한다. | 없음 |
| `A110-ARCH-006` | 부분 해결 | Alpha SQLite 구성요소는 나뉘었지만 legacy `StorageWorkflow`가 슬롯·checkpoint·복구·무결성 상태를 함께 소유한다. | Alpha/legacy 생명주기와 repository·catalog·policy owner를 분리한다. |
| `A110-ARCH-007` | 부분 해결 | controller·health governor·저장 구성요소는 분리됐지만 `android_main`·`AppState`가 진행·렌더·관찰·저장을 중앙 조정한다. | lifecycle·advance·observation·storage owner를 고정하고 coordinator를 단계 분리한다. |
| `A110-ARCH-008` | 미해결 | `LiveRuntimeFacade110`이 session·population·관계·세계·수식·사건·저장·hash·query forwarding을 함께 소유한다. | facade는 transaction·조립·원자 commit만 남기고 domain command/query port를 분리한다. |
| `A110-ARCH-009` | 부분 해결 | fixed-SHA 문서와 manifest는 있으나 1.0.9 또는 `PENDING` 현재 상태 문구가 남아 Notion·코드·릴리스 드리프트가 실제 발생했다. | 활성 문서에 authority/status/replaced-by/verified-SHA/date를 넣고 릴리스에서 검사한다. |
| `A110-ARCH-010` | 미해결 | ASan·UBSan은 있으나 production C++ `clang-tidy`와 핵심 상태·저장·query coverage 추세 gate는 확인되지 않는다. | 비차단 관측부터 시작해 기준선 안정 뒤 신규 경고·coverage 하락만 차단한다. |
| `A110-ARCH-011` | 부분 해결 | reason ID·구조화 값은 있으나 한국어 사용자 문구 대부분이 Java renderer에 직접 있고 resource 경계가 완성되지 않았다. | 의미 DTO 검사와 표시 테스트를 분리하고 문구를 resource/formatter catalog로 이동한다. |
| `A110-ARCH-012` | 부분 해결 | 10,000명·1,000년 자동 성능 검증은 있으나 반복 전체 순회와 실기기 발열·메모리 미측정이 남아 있다. | fixed release SHA를 실측한 뒤 hot domain에만 dirty revision·저빈도 갱신·사건 예약을 적용한다. |
| `A110-ARCH-013` | 해결 | legacy simulation 검색 단위는 120→41로 재편됐고 `alpha110`도 manifest에 owner와 대표 검색 작업집합을 가진다. | 없음 |
| `A110-ARCH-014` | 해결 | `dashboard_bridge_contract.json`에서 Java/C++ command를 생성하고 generator check가 이름·숫자·router case drift를 차단한다. | 없음 |
| `A110-ARCH-015` | 부분 해결 | bridge schema·fixture는 있으나 mutex 안에서 전체 dashboard JSON을 만들고 Java가 `JSONObject`로 다시 파싱한다. | section별 payload·직렬화·JNI·파싱·할당을 측정한 뒤 필요한 부분 frame만 도입한다. |
| `A110-ARCH-016` | 현재 적용 안 됨 | 활성 `AppState`는 legacy `SimulationRunner`를 소유하지 않고 단일 `alphaRuntimeMutex`로 전이를 직렬화한다. | 그대로 이전하지 않고 현행 중앙 조정·mutex 위험은 007·015에서 다룬다. |
| `A110-ARCH-017` | 부분 해결 | manifest는 `alpha110`을 반영하지만 사람이 읽는 architecture 문서는 1.0.9 legacy DAG를 현재처럼 제시한다. | `AppState → LiveRuntimeFacade110 → Alpha 저장/UI` 기준으로 현재 구조 문서와 gate를 갱신한다. |
| `A110-ARCH-018` | 해결 | 버전별 fixture 이름과 current architecture/release gate가 분리되고 legacy batch는 현재 버전에서 명시적으로 건너뛴다. | 없음 |

집계는 `해결 5 / 부분 해결 9 / 미해결 2 / 현재 적용 안 됨 2`다. 새 모바일
리팩터링 backlog에는 부분 해결 9개와 미해결 2개, 합계 11개만 넣는다. 해결된
`001·005·013·014·018`과 현재 활성 경로에 직접 적용되지 않는 `003·016`은 다시
구현 항목으로 복사하지 않는다.

## 11. 리팩터링 우선순위

### P0 — 증거와 기획 권위 정리

1. requirement baseline, Notion design revision, PC override, review ID, 구현 SHA,
   검증 SHA, package SHA, release SHA를 한 행에서 구분한다.
2. `PC_TEMP_REVIEW_LEDGER.md`를 단일 review registry로 유지하고 55개 ID의 결정
   URL·owner·영향·대체 조건·목표 버전을 계속 보완한다.
3. `LOCAL_TESTING`의 잘못된 evidence 링크와 “현재 기준선” 문구를 교정한다.
4. 화면 검증 중단은 `Validated`로 승격하지 않고 명시적 pending으로 유지한다.

### P1 — 경계 부채를 구조-only 변경으로 줄이기

1. `KoreanNumberFormatter`를 Core에서 Presentation으로 이동한다.
2. `World.Queries.cs`의 기능별 projection을 Queries owner DTO로 점진 이동한다.
3. `Program.cs`를 domain test suite 등록기로 축소하고 기존 테스트 이름·순서·성능
   조건을 보존한다.
4. `World.Persistence.cs`와 `SlotSaveStore.cs`는 canonical authority와 외부 파일
   lifecycle 경계를 유지하면서 schema/domain codec 작업집합을 줄인다.
5. `MainWindow.AgentObservation.cs`와 `MainWindow.xaml`은 이미 존재하는 내부 탭 경계로
   UserControl/presenter를 분리한다.

구조 이동에는 공식·RNG·save wire·새 기능을 함께 넣지 않는다.

### P2 — 계측 후 성능 구조 개선

1. `_worldGate`의 보유 시간과 대기 시간을 advance/query/checkpoint별로 측정한다.
2. 병목이 확인되면 lock 안에서는 immutable capture만 만들고, 문자열·행 조합과 파일
   I/O는 밖에서 수행한다.
3. query cache hit, 요청 section, materialized row 수와 allocation을 진단에 남긴다.
4. 보존 evidence의 checkpoint 생성 117ms·복원 565ms와 SQLite 저장 973ms·복원
   869ms를 역사 기준으로 유지하고, 새 재실측의 852/851ms 중앙값과 1,108/1,027ms
   권고 초과 회차도 함께 추적한다.
5. SoA를 객체 그래프로 바꾸거나 범용 event bus를 넣지 않는다. 10,000명 hot path의
   연속 배열과 명시적 stable ID를 보존한다.

## 12. 이후 변경의 최소 검증

```powershell
./scripts/validate-architecture.ps1
./scripts/validate-port-ledger.ps1
dotnet build Chronicles.slnx -c Release
dotnet run --project tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release --no-build
```

변경 종류에 따라 다음을 추가한다.

| 변경 | 추가 검증 |
|---|---|
| 공식·RNG·스케줄 | 동일 seed 재현, 상태 hash fixture, 관찰 무영향, 장기 SOAK |
| save/hash/schema | 구 fixture migration, round-trip hash, 손상 후보 거부, primary/temp/backup 복구 |
| query/UI | 닫힌 section 0회, page 제한, state/RNG 무영향, 직접 화면 시나리오 |
| 성능 | 5개 독립 Release 프로세스, 중앙값·p95·allocation, 이전 고정 SHA 비교 |
| 구조 이동 | manifest·csproj DAG, namespace/공개 API, 테스트 결과 동일, wire/hash 동일 |
| 패키징 | VERSION, 실행 파일 hash·크기·제품 버전, 실제 실행·종료, 바로가기 target |
| 공개 릴리스 | source SHA, 검증 SHA, tag, release asset hash, 원격 workflow 결과 |

## 13. 이번 감사에서 직접 재확인한 사실

- GitHub/local `main` 감사 기준:
  `7eb4d540b328d4664778b3e494148db0cd36bee2`
- `VERSION`: `0.0.21`
- 구현·패키지 완료 SHA: `f837e4cb6b40ddeba8029139dd710f4c457869ad`
- 재검증 Release 바이너리 기준: `c559063d35ebd5e8f0c6763ad009d9b74d8fb40d`
- c559와 7eb의 동일 tree:
  `src de39974c462c8ade192a4a06f9d4cf46248c8e82`,
  `tests c9cb7e78cb95e584e58bcc733f0e56ee240c6a80`
- `validate-architecture.ps1`: PASS, production module 8, Desktop authority pattern 0
- `validate-port-ledger.ps1`: PASS, 238행·238 unique ID
- 0.0.21 고정 SHA 당시 검증 원장 집계: Port 128 / PC Adapt 15 /
  PC_TEMP_REVIEW 31 / Platform Excluded 0 / Gap/Deferred 64
- 실제 `ProjectReference`: manifest 허용 DAG와 일치
- 0.0.21 패키지·과거 성능 결과: 기존
  [`verification.md`](evidence/0.0.21/verification.md)의 고정 증거를 인용했다.
- 감사 중 최신 검증: c559 Release build 경고 0·오류 0, 전체 runner 1회와 독립
  5-process 각각 65/65, 최신 성능 표를 재확인했다.
- 이번 문서 작성에서 self-contained 패키징과 화면 조작을 새로 실행했다고 주장하지
  않는다.
