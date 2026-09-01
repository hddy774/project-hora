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
- 좌측 관찰 경로와 전체 작업공간 전환: `MainWindow.Workspace.cs`

지도와 경제 화면은 열린 경로만 `ObservationSections`를 요청한다. 지도 내부의 수선자
행과 시장의 제안·예약 행도 해당 내부 탭이 열릴 때만 materialize한다.

## 0.0.18 수행 진입점

- 수행 Work·준비·사전검증·시도·안착 전이: `World.Cultivation.cs`
- 대경지 수명·기준 기간·공식·임시 검토값: `RealmRules.cs`
- 승인 Effect 0개·Typed 실패·가용성 상태: `CultivationModel.cs`
- 삼도축기요결 5단계 결과와 Source Snapshot: `FoundationFormationAuthorityStore.cs`

인물 상세는 개요·수행·전투·관계·기록 내부 탭 중 선택한 한 구역만 갱신한다. 지도와
시장의 짧은 상세 탭에서는 중첩 `ScrollViewer`를 사용하지 않고, 기록·대형 표처럼
목록 탐색에 효율적인 경우에만 가상화된 스크롤을 유지한다.

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
