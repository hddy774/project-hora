# PC 0.0.27 성능 기반·전체 코드 구조 감사

## 범위와 불변 조건

- 기준선은 `0.0.26`의 `c3100c42ef86a3ff28a8aca0d8bebed0ca23e770`이다.
- 감사 범위는 8개 production module, 수동 C#/XAML 145개 파일, 57,074줄이다.
- 권위 상태는 10,000개 객체 그래프로 바꾸지 않고 연속 SoA 배열과 안정 ID를 유지한다.
- 게임 규칙, RNG 소비 순서, 월 경계, state hash, 외부 `WorldSchema 25`, 내부 checkpoint
  schema 32는 변경하지 않는다.
- 최대 배속은 `1/10/25/50/100x`로 유지한다. 250x 이상은 추가하지 않는다.

## 프로파일 기반 결론

Release EventPipe 표본에서 `World.AdvanceTo`가 simulation 관련 최대 포괄 구간이었다.
직접 benchmark trace의 주요 포괄 비중은 `AdvanceTo 8.69%`, `ResolveActivity 3.21%`,
`ResolveCultivationMonth 1.43%`, `SetActivity 1.16%`, `RecordDecision 0.99%`였다.
indexed 4진 일정표의 개별 연산은 약 0.1~0.4% 범위여서 자료구조 전면 교체 대상이
아니었다.

따라서 다음 순서로 수정했다.

1. dirty revision이 없는 랭킹 정렬을 제거하고, 공유 scratch와 유효 bit 수만 처리하는
   non-negative 11-bit radix 정렬을 사용한다.
2. 행동별로 실제 변경 가능한 전투력 입력만 두 개의 `ulong`에 packing해 비교한다.
3. 입문 Gate가 고정된 수행 구간에서는 통과 mask를 재사용하고 실패 Gate 단련을
   연속 span에서 일괄 갱신한다.
4. RNG·시장·관계·로그를 읽거나 쓰지 않는 수행 Work만 최대 4 worker로 계산한다.
   결과 commit과 이벤트·RNG·일정 갱신은 AgentId 순서로 직렬 유지한다.
5. 인과 판단에서 큰 현재 경로 구조체를 복사하지 않고 readonly ref와 필요한 ID만
   사용한다.
6. 최근 로그 링, 압축 연대기, 저장 section streaming과 열린 화면 query만 유지해
   장기 메모리와 UI 경합을 제한한다.

## 논문·공식 문서 검토와 적용 판정

| 기법 | 자료 | 판정 | 근거 |
|---|---|---|---|
| Calendar Queue | ACM, *Calendar Queues: A Fast O(1) Priority Queue Implementation for the Simulation Event Set Problem* | 보류 | 기대 O(1)이지만 현재 일정표 비중이 매우 낮고, 분포 tuning과 결정론 회귀 비용이 더 크다. |
| Hashed/Hierarchical Timing Wheel | Varghese·Lauck, SOSP 1987 | 보류 | 제한 범위 timer에는 적합하지만 현재 10,000명당 다음 일정 하나인 indexed heap은 변경·제거까지 O(log n)으로 안정적이다. |
| Parallel discrete-event simulation | WSC 2017 retrospective | 제한 적용 | 세계 전이를 병렬 commit하지 않고 충돌 없는 수행 계산만 2단계로 분리한다. |
| TPL partitioning | Microsoft .NET Parallel Programming / Custom Partitioners | 실측 채택 | 기존 `Parallel.For`가 독립 5회에서 직렬 중앙값 633ms 대비 475ms였다. 고정 연속 구간 실험은 동적 분할 392ms 대비 397ms로 느려 폐기했다. |
| GC·allocation 진단 | Microsoft .NET GC performance guidance | 채택 | tick당 할당, live heap, generation별 GC, fragmentation을 진단하고 hot/cold 기록 저장소를 분리한다. 강제 GC는 benchmark에만 둔다. |

검토 링크:

- https://dl.acm.org/doi/10.1145/63039.63045
- https://www.cs.columbia.edu/~nahum/w6998/papers/sosp87-timing-wheels.pdf
- https://www.informs-sim.org/wsc17papers/includes/files/019.pdf
- https://learn.microsoft.com/dotnet/standard/parallel-programming/
- https://learn.microsoft.com/dotnet/standard/parallel-programming/custom-partitioners-for-plinq-and-tpl
- https://learn.microsoft.com/dotnet/standard/garbage-collection/performance
- https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/

## 작업집합 리팩터링

파일 수를 늘리는 것이 목적이 아니라 서로 다른 변경 원인·검증 경계를 분리하는 것이
목적이다.

| 기존 작업집합 | 기존 줄 | 분리 결과 | 책임 |
|---|---:|---|---|
| `World.cs` | 2,065 | `World.cs` 866 + Scheduling 227 + Snapshots 275 + StateHash 718 + Rankings 317 | 조정, 일정, 투영, hash, ranking |
| `World.Persistence.cs` | 2,539 | checkpoint/restore 873 + 일반 검증 971 + 인과·역사 검증 709 | capture, restore, corruption validation |
| `OrganizationModel.cs` | 1,692 | live store 998 + Contracts 362 + Persistence 351 | 공용 계약, 실시간 권위, 저장 경계 |
| `SlotSaveStore.cs` | 1,371 | write/manifest 877 + load/recovery 507 | 원자 쓰기, 후보 검사·순차 복원 |
| `AgentStore.cs` | 1,129 | 본체 840 + ProgressionAssets 300 | 핵심 SoA, 수행·재료·법보 접근 |
| `RelationModel.cs` | 1,103 | live sparse store 909 + Persistence 203 | 상호작용, checkpoint·검증 |
| `AgentHistoryStores.cs` | 1,132 | 최근 로그 546 + 압축 연대기 593 | 100행 ring, 생별 cold history |
| `World.Cultivation.cs` | 1,266 | 입문·Work·투영 668 + Breakthroughs 609 | 빈번한 Work, 저빈도 돌파 경계 |
| `CityMarket.cs` | 1,083 | 계약·snapshot 727 + Industry 367 | 거래 상태, 월별 생산·가격·수급 |
| `MainWindow.AgentObservation.cs` | 1,532 | 현재 인물 화면 1,015 + RecordsView 534 | 활성 인물 탭, 기록·윤회 탭 |

권장 1,000줄을 넘는 남은 세 파일은 manifest에 책임과 유지 이유를 기록했다.

- `MainWindow.xaml` 1,574줄: root shell과 선택 인물 visual tree의 이름·이벤트 연결이 한
  생명주기다. 다른 작업공간은 이미 UserControl로 분리됐으며 단순 추출은 hidden-view
  materialization을 줄이지 않는다.
- `MainWindow.AgentObservation.cs` 1,015줄: 기록·윤회는 분리했고 남은 15줄 초과분은
  선택 인물/활성 탭 수명주기를 공유한다.
- `ContentAuthorityStore.cs` 1,004줄: command, provenance, owner/location index가 하나의
  원자 권위 불변식을 이루며 단 네 줄 초과다.

## 폐기한 실험

- 네 랭킹 범주 동시 병렬 정렬은 10,000명에서 직렬 260.5ms, 병렬 283.8ms로 8.9%
  느려 제거했다.
- 수행 계산의 고정 연속 구간 partition은 동적 `Parallel.For`보다 중앙값 1.3% 느려
  제거했다.
- Calendar Queue/Timing Wheel은 현재 scheduler 병목 증거가 없어 구현하지 않았다.
- 일반 event bus, 범용 dictionary 상태, 인물별 객체 그래프는 locality·할당·권위 추적을
  악화시키므로 도입하지 않았다.

## 검증 기준

- 전체 Release 검사와 구조/XAML/238행 이식 원장을 통과한다.
- 동일 seed, `AdvanceTo` 분할, 직렬·병렬 5 seed, checkpoint 왕복 hash가 일치한다.
- 10,000명·20,000 tick은 독립 프로세스 5회와 동일 머신 0.0.26 A/B를 기록한다.
- 100배속은 60초, warm-up 30초 뒤 p95 80ms, p99 100ms, 정상 95%, 진행률 95%,
  최종 backlog 1초 이하를 검사한다.
- 닫힌 화면은 query·DTO·행 생성 0을 유지하며 화면 query는 권위와 RNG를 바꾸지 않는다.

