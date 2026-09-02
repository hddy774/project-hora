# PC 저장소 구조 계약

이 문서는 `docs/architecture/repository_manifest.json`의 사람이 읽는 설명이다. 실제 프로젝트
참조와 owner 판정은 manifest와 `scripts/validate-architecture.ps1`을 함께 사용한다.

```text
Core -> Simulation -> Queries -> Runtime
                  \-> Persistence
Queries -> Presentation
Application = Runtime + Persistence + Queries 조정
Desktop = Application + Presentation
```

## 변경 순서

1. 권위 상태와 결정론적 전이는 `Simulation`에서만 변경한다.
2. 화면에 필요한 읽기 계약은 `Queries`의 `ObservationRequest`와 읽기 전용 투영 경계를
   통해 요청한다.
3. worker와 배속은 `Runtime`, 저장 조정은 `Application`, 문자열 표시 규칙은
   `Presentation`에 둔다.
4. WPF는 `Application`과 `Presentation` 프로젝트만 직접 참조하며 `World`를 생성하거나
   SoA 열을 직접 읽지 않는다.
5. 구조 이동, 게임 규칙/RNG 변경, 저장 wire 변경은 서로 다른 커밋으로 남긴다.

## 0.0.14 전환 메모

기존 snapshot 값 형식 중 일부의 CLR namespace는 결정론과 authority schema를 보존하기 위해
아직 `Chronicles.Simulation`에 남아 있다. Desktop은 이 형식을 transitively 표시할 수
있지만 가변 `World`나 store에는 접근하지 않는다. DTO 물리 이동은 저장/RNG 변경과
섞지 않고 후속 수직 기능에서 query-owned projection으로 교체한다.

## 0.0.16 수직 기능 진입점

- 인물 생명 권위: `PersonLifeAuthority.cs`
- Goal→Plan→Task→Action·인지·현생 기억: `AgentCausalRuntime.cs`
- 윤회·부모·가계·영혼 손상·사건 계보: `ReincarnationModel.cs`와
  `ReincarnationStore*.cs`
- 희소 관계·일반 대화·Teaching·외부 맥락 availability: `RelationModel.cs`

이 파일들은 모두 Simulation 권위 owner다. Desktop은 `WorldObservationQueries`가 만든
불변 DTO만 받아 펼친 인물 상세 구역을 그린다.

## 0.0.17 Atlas·경제 진입점

- 5층천 Definition·Production 1·2층천·계층·900 영맥: `WorldAtlasModel.cs`
- 개인 공간 지식·이동·지역 index·유한 영맥 공급: `SpatialAuthorityStore.cs`
- 경제 Owner/Holder·Offer/Reservation/Channel: `MarketAuthorityModel.cs`
- 제안→예약→만료/취소/체결: `CityMarket.Contracts.cs`
- 수요 기반 생산·가격·12개월 수급 창: `CityMarket.Industry.cs`
- 좌측 관찰 경로와 전체 작업공간 전환: `MainWindow.Workspace.cs`

지도와 경제 화면은 열린 경로만 `ObservationSections`를 요청한다. 지도 내부의 수선자
행과 시장의 제안·예약 행도 해당 내부 탭이 열릴 때만 materialize한다.

## 0.0.18 수행 진입점

- 입문·수행 Work·관찰 투영: `World.Cultivation.cs`
- 병목 사전검증·돌파·안착 전이: `World.Cultivation.Breakthroughs.cs`
- 대경지 수명·기준 기간·공식·임시 검토값: `RealmRules.cs`
- 승인 Effect 0개·Typed 실패·가용성 상태: `CultivationModel.cs`
- 삼도축기요결 5단계 결과와 Source Snapshot: `FoundationFormationAuthorityStore.cs`

인물 상세는 개요·수행·전투·관계·기록 내부 탭 중 선택한 한 구역만 갱신한다. 지도와
시장의 짧은 상세 탭에서는 중첩 `ScrollViewer`를 사용하지 않고, 기록·대형 표처럼
목록 탐색에 효율적인 경우에만 가상화된 스크롤을 유지한다.

## 0.0.19 장기 행동·수행 계획 진입점

- 1개월~1만년 수행 계획과 조건 계약: `CultivationActionPlan.cs`
- 기간·장소·강도·자원·진법·공동 수행 결정과 pulse 예약: `World.CultivationPlans.cs`
- Work 일괄 축적과 경계 전이: `World.Cultivation.cs`
- 권위 저장·손상 검증: `World.Persistence.cs`
- 열린 수행 상세에서만 만드는 세 내부 탭: `MainWindow.AgentObservation.cs`

수행 계획은 `Goal→Plan→Task→Action` 판단 결과를 여러 달 유지하는 권위 상태다. 계획
경계 전에는 위치·목표·행동을 다시 선택하지 않으며, 여유/일반/폐관에 따라 6·12·24개월
단위 pulse만 예약한다. pulse는 그 기간의 월 Work와 자원 소비를 동일한 정수 공식으로
반영하되, 다음 병목·돌파·안착 경계를 넘지 않고 즉시 월 단위 전이로 돌아온다.
UI는 수행 Expander와 현재 내부 탭이 열렸을 때만 계획 DTO 문구와 상대 이름을 갱신한다.

## 0.0.21 세부 수행 진척 진입점

- 대경지별 단계 정의·가중치·파생 투영: `CultivationDetailProgress.cs`
- 선택 인물의 지연 DTO 생성: `World.Cultivation.cs`, `WorldObservationQueries.cs`
- 토글·다섯 진행 막대 UI: `MainWindow.xaml`, `MainWindow.AgentObservation.cs`

세부 수행 진척은 기존 권위 Work에서 계산하는 관찰 투영이다. 저장 wire·RNG·수행 공식은
바꾸지 않으며 `CultivationDetail` section과 수행 토글이 열렸을 때만 단계 배열과 행을
만든다. 토글 내부에는 중첩 스크롤을 두지 않는다.

## 0.0.21 콘텐츠·지식·법칙 진입점

- Definition/Instance/Owner/Holder/Provenance: `ContentAuthorityModel.cs`,
  `ContentAuthorityStore.cs`
- 판본·물리 기록·접근·Claim·복원 계보: `KnowledgeAuthorityModel.cs`,
  `KnowledgeAuthorityStore.cs`
- 객관 후보·개인 이해·세계 AI·천도 분리: `LawAuthorityModel.cs`,
  `LawAuthorityStore.cs`
- schema 27~29 codec와 migration: `World.Persistence.cs`
- 내부 탭별 지연 projection: `WorldObservationQueries.cs`, `ContentWorkspaceView.xaml.cs`

Content, Knowledge, Law는 각각 Simulation 권위 owner가 state hash와 checkpoint를
소유한다. 콘텐츠 화면은 선택한 내부 탭의 page만 투영한다. 법칙은 실행 증거가 없는
후보 ref만 보존하며 일반 UI와 세계 설정 진단 정보를 분리한다. 실제 Adapter와 Stable
LawId는 증거가 생기기 전까지 생성하지 않는다.

## 0.0.21 절차 생성 세계 진입점

- 생성 설정·도메인 seed·고정 유니크 계약: `WorldGenerationModel.cs`
- 아이템·서적·요수 카탈로그와 지도 파생: `WorldGenerationCatalog.cs`
- 시간 경계의 신규 실물·판본 연결: `World.GeneratedContent.cs`
- schema 30 검증·이전: `World.Persistence.cs`
- 새 세계 설정과 지연 상세: `SettingsWorkspaceView.xaml.cs`,
  `ContentWorkspaceView.xaml.cs`, `MapObservationView.xaml.cs`

지도·요수·아이템·서적은 서로 분리된 결정론적 domain seed를 사용하며 게임플레이 RNG를
소비하지 않는다. 생성 수량을 바꿔도 다른 생성 영역과 인물 RNG 순서는 변하지 않는다.
카탈로그와 지속 생성 cursor만 권위 상태이며 이름 문자열·화면 행은 필요할 때 파생한다.
콘텐츠 화면과 지도 화면이 닫혀 있으면 생성 상세 query·행 materialization은 0회다.

## 0.0.22 실행·월 경계·지도 통합 진입점

- Desktop XAML 리소스 계약과 시작 진단: `validate-xaml-resources.ps1`,
  `App.xaml.cs`
- 월별 환경·조직·콘텐츠 전이와 의사결정 랭킹 경계: `World.cs`
- schema 31 월 경계·랭킹 checkpoint와 검증: `WorldCheckpoint.cs`,
  `World.Persistence.cs`
- 하천대륙→주→지점 및 기존 3×3 activity 대응: `WorldGeographyModel.cs`
- 통합 지도 작업공간: `MapObservationView.xaml`, `MapObservationView.xaml.cs`

월별 권위 전이는 `AdvanceTo` 호출 횟수가 아니라 저장되는 마지막 처리 월을 기준으로
각 달에 정확히 한 번 실행한다. AI가 읽는 네 랭킹도 새 달 경계에서만 확정하고 같은 달
안에서는 고정한다. schema 31은 이 월 cursor, agent별 순위와 dirty 상태를 보존한다.

지도 UI의 단일 경로는 `하천대륙 10×10 주 → 오천주 → 층천별 10×10 지점`이다.
기존 Production 3×3 지역 활동은 오천주 중앙 3×3 지점의 내부 탭으로 대응한다. 그 밖의
지점과 99개 주는 없는 권위를 생성하지 않고 `DataUnavailable`로 표시한다. 수선자 page는
`수선자` 내부 탭과 대응 Production 지점이 모두 선택됐을 때만 요청한다.

## 0.0.23 저장·메모리·병렬 수행 진입점

- 장기 JSON/Brotli streaming·SQLite incremental BLOB: `WorldSaveCodec.cs`,
  `SlotSaveStore.cs`, `SlotSaveStore.Load.cs`, `WindowsSqlite.cs`
- typed 저장 오류·작업 복구: `SaveOperationException.cs`,
  `GameApplicationSession.cs`, `MainWindow.SaveSlots.cs`
- 최근 로그·연대기·윤회 희소 저장: `AgentHistoryStores.cs`,
  `ReincarnationStore.cs`, `ReincarnationStore.Persistence.cs`
- 고정 행동 일정·dirty ranking: `AgentDecisionSchedule.cs`,
  `RankingRevisionCache.cs`
- 결정론적 수행 입력 계산·직렬 commit: `World.CultivationParallel.cs`,
  `World.Cultivation.cs`
- runtime 계측과 100배속 SOAK: `SimulationRunner.cs`,
  `PerformanceDiagnosticsModel.cs`, `RuntimeHundredSpeedSoakTests.cs`
- 직렬·병렬 처리량과 장기 저장 probe: `CultivationParallelThroughputTests.cs`,
  `Program.cs`

Desktop과 Application은 저장 결과의 작은 값과 query DTO만 유지한다. 저장 scratch와
checkpoint는 작업 수명 안에서만 보유하고, 수행 worker는 권위 상태를 쓰지 않는다.
모든 권위 commit은 안정된 AgentId 순서와 기존 RNG 소비 순서를 유지한다.

## 0.0.24 개인 연대기 보존·저장 경계 진입점

- 최근 개인 로그 링: `AgentHistoryStores.cs`
- 압축 세부 연대기·생별 이름 참조: `CompactChronicleStore.cs`
- 100년 누계·완결 종합본·관심 보존 정책: `PersonalChronicleArchiveStore.cs`
- schema 32 checkpoint·이전·검증: `WorldCheckpoint.cs`, `World.Persistence.cs`
- compact JSON wire: `ValueTypeJsonConverters.cs`, `WorldSaveCodec.cs`
- 관심 목록 runtime 결합: `SimulationRunner.cs`, `GameApplicationSession.cs`,
  `MainWindow.ObserverPreferences.cs`
- 저장 경계 조건부 메모리 회수: `SaveOperationProgress.cs`

세부 기록과 100년 종합본은 별도 권위 저장소다. 일반 인물의 세부 기록은 100건을 넘지
않으며 완결 종합본과 중복되는 행을 제거한다. 관심 인물만 세부 기록을 계속 보존하고,
종합본은 관심 여부와 무관하게 최근 100권으로 제한한다. UI 관심 목록 변경은 RNG·게임
공식·schema 32 상태 hash에 영향을 주지 않지만 저장 보존 정책 자체는 checkpoint에
명시한다. 외부 SQLite world schema는 25를 유지한다.

## 0.0.25 분할 권위 저장·조직 역사 상한 진입점

- 권위 header와 배열 section 분리: `WorldCheckpointSectionStore.cs`
- section streaming codec·checksum: `WorldSaveCodec.cs`
- SQLite incremental BLOB 쓰기: `SlotSaveStore.cs`
- 후보 metadata·순차 decode·복구 선택: `SlotSaveStore.Load.cs`
- 조직별 최근 100건 역사: `OrganizationModel.cs`, `OrganizationTransitions.cs`
- 조직 종류·역할·명령·checkpoint DTO·창설 규칙: `OrganizationContracts.cs`
- 조직 checkpoint·hash·복원·손상 검증: `OrganizationStore.Persistence.cs`
- 검증 완료 Candidate World 교체: `SimulationRunner.cs`, `GameApplicationSession.cs`

한 슬롯은 계속 하나의 원자적 `world.db`지만, 권위 header와 인물·세계 이벤트·세계
연대기 배열은 별도의 압축·checksum BLOB으로 저장한다. 인물은 512명, 이벤트와 세계
연대기는 4,096건 단위로 나누며 불러오기는 metadata 검증 뒤 한 section씩 순서대로
해제한다. 기존 단일 payload SQLite 슬롯도 그대로 읽는다. 외부 WorldSchema 25와 내부
checkpoint schema 32는 유지한다.

조직 역사는 창설·가입·탈퇴·직책·재정·외교·분열·합병·승계를 포함해 조직마다 최신
100건만 권위 상태에 보존한다. 기존 세이브는 저장된 hash를 먼저 검증한 뒤 runtime에
게시하기 전에 초과분을 정리한다. 전역 history sequence는 되감지 않으며 새 이벤트의
인과 순서를 보존한다.

## 0.0.26 100배속 runtime·관찰 경합 진입점

- lock-free 셸·busy-frame 생략·실제 query 계측: `SimulationRunner.cs`
- 화면별 관찰 요청과 닫힌 화면 0-query: `MainWindow.Observation.cs`
- 성능 표본 기반 선택 탭 갱신: `SettingsWorkspaceView.xaml.cs`
- 월·수명·장기 수행 scheduled hot path: `World.cs`, `World.CultivationPlans.cs`
- compact 반복 로그·관계 인접 index·radix ranking: `AgentHistoryStores.cs`,
  `RelationModel.cs`, `RankingRevisionCache.cs`

설정·저장 화면은 `RuntimeShellSnapshot`만 읽고, 상세 관찰 화면은 world lock을 즉시
얻을 수 있을 때만 불변 DTO를 새로 만든다. 관찰 실패는 이전 DTO 유지일 뿐 권위 전이나
RNG 소비가 아니다. 저장과 명시적 사용자 요청은 계속 blocking 안전 경계를 사용한다.
100배속에서는 3 pulse마다 관찰을 무효화해 UI 작업과 월 위상 고정을 함께 줄인다.

## 0.0.27 프로파일 기반 성능 기반·작업집합 진입점

- due 일정·월 경계 조정: `World.Scheduling.cs`
- 범주별 dirty·adaptive radix ranking: `World.Rankings.cs`,
  `RankingRevisionCache.cs`
- 입문·수행 Work와 돌파 경계: `World.Cultivation.cs`,
  `World.Cultivation.Breakthroughs.cs`
- 수행 순수 계산·직렬 commit: `World.CultivationParallel.cs`
- 인물 SoA와 진행·자산 접근: `AgentStore.cs`, `AgentStore.ProgressionAssets.cs`
- 최근 로그와 cold 연대기: `AgentHistoryStores.cs`, `CompactChronicleStore.cs`
- 조직·관계 checkpoint 경계: `OrganizationStore.Persistence.cs`,
  `RelationStore.Persistence.cs`
- 슬롯 쓰기와 순차 복구: `SlotSaveStore.cs`, `SlotSaveStore.Load.cs`
- 선택 인물 표시와 기록 표시: `MainWindow.AgentObservation.cs`,
  `MainWindow.AgentRecordsView.cs`

성능 변경은 EventPipe 프로파일과 동일 머신 A/B 뒤 채택한다. 일정표의 calendar queue·
timing wheel 전환, 랭킹 범주 병렬화, 고정 구간 수행 partition은 실제 이득이 없어
적용하지 않았다. 전체 판정과 남은 대형 작업집합 사유는
`PC_PERFORMANCE_FOUNDATION_0.0.27.md`에 기록한다.

## 검증

```powershell
& .\scripts\validate-architecture.ps1
& .\scripts\validate-port-ledger.ps1
dotnet build Chronicles.slnx -c Release
dotnet run --project tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release --no-build
```

줄 수는 정보성 지표이며 파일 분리의 단독 근거로 사용하지 않는다. 동일 owner·기능·
생명주기의 파일은 한 작업집합으로 유지하고 authority, query, persistence, WPF 경계는
파일 수를 줄이기 위해 합치지 않는다.
