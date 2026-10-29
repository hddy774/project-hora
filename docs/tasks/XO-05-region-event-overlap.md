# [XO-05] 선협 관찰 — 지역 사건 보정 재계산과 개입 입력 검증

상태: 승인(사용자 전체 개발 지시, 2026-10-29) · 우선순위 P1 · 검토 근거: [13 §2 R-06·R-07](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
비경과 수조는 지역의 `AuraModifier`(1.2/0.8)와 `Status` 문구를 시작·종료 때 **직접 대입**한다.
같은 지역에 두 사건이 겹친 뒤 하나가 끝나면 남은 사건이 있어도 보정이 1.0, 상태가 "평온"이 된다.
또 `InterventionSystem`은 천기를 먼저 차감한 뒤 지역 번호·중복·대상 생존을 검사하지 않는다(UI만 막는다).

## 범위
- 포함
  - 새 파일 `games/xianxia-observer/src/Xianxia.Simulation/Systems/RegionEvents.cs`.
  - `SecretRealmSystem.cs`(Open·Update·Close), `BeastTideSystem.cs`(Start·Update·Finish): 직접 대입 제거, 재계산 호출.
  - `InterventionSystem.cs`: 검사 후 차감.
  - `Persistence/WorldSerializer.cs`: 로드 후 모든 지역 재계산.
  - `Xianxia.App/Sheets/RegionSheet.cs`: 버튼 활성 조건을 코어 검사 함수로 교체(동작 동일).
  - 테스트: `tests/Xianxia.Simulation.Tests/RegionEventTests.cs`(신규).
- 제외
  - 자연 발생 확률·지역 선택 규칙 변경(같은 지역 중첩은 계속 허용한다).
  - 보정 수치(1.2·0.8) 변경. 난수 호출 추가·삭제.

## 인터페이스 (계약)
```csharp
/// <summary>지역의 활성 사건에서 영기 보정과 상태 문구를 다시 계산한다.</summary>
public static class RegionEvents
{
    /// <summary>비경 하나 이상이면 ×1.2, 수조 하나 이상이면 ×0.8을 곱한다. 사건이 없으면 1.0.</summary>
    public static void Refresh(World world, int regionId);

    /// <summary>모든 지역을 Refresh한다(로드 직후).</summary>
    public static void RefreshAll(World world);
}

// InterventionSystem — 실행 가능 여부(천기 잔액 포함). UI 버튼과 실행이 같은 함수를 쓴다.
public static bool CanOpenSecretRealm(World world, int regionId);
public static bool CanSummonBeastTide(World world, int regionId);
public static bool CanBestowFortune(World world, Cultivator cultivator);
```

## 동작 규칙
1. `AuraModifier = (비경≥1 ? 1.2 : 1.0) × (수조≥1 ? 0.8 : 1.0)`. 둘 다면 0.96. 같은 종류가 여러 개여도 한 번만 곱한다.
2. `Status`: 수조가 있으면 `"수조: " + 가장 먼저 시작한 수조의 BeastName`, 아니면 비경이 있으면
   `"비경 개방: " + 가장 먼저 열린 비경의 Name`, 둘 다 없으면 `"평온"`. "가장 먼저"는 목록 앞쪽.
3. `Open`/`Start`는 목록에 추가한 **뒤** `Refresh`. `Update`의 종료 처리는 `RemoveAt` **뒤** `Refresh`. `Close`/`Finish`는 보정·상태를 건드리지 않는다.
4. 탐색 재료 풍부도 규칙 `AuraModifier < 1 ? 0.8 : 1.0`(ActionEffects.cs:37)은 그대로 둔다(0.96도 < 1이므로 수조 중 감소 유지).
5. 로드 시 `Normalize` 다음, `RebuildIndexes` 전에 `RefreshAll`. 과거 잘못 저장된 보정도 이것으로 고쳐진다.
6. `CanOpenSecretRealm(world, id)`: `id ∈ RegionCatalog.EventRegionIds` **그리고** 그 지역에 활성 비경 없음 **그리고** 천기 ≥ 3.
   `CanSummonBeastTide`: 같은 조건에 수조. `CanBestowFortune`: `world.Find(cultivator.Id)`가 **같은 객체**이고 천기 ≥ 2.
7. 실행 함수는 `Can…`이 false면 **천기를 바꾸지 않고** false를 반환한다. true일 때만 차감 후 실행한다.
8. 자연 발생(`Update`)은 6의 중복 검사를 하지 않는다(현재 규칙 유지).
9. `RegionSheet.Intervene`의 기존 검사(지역 범위 + `Queries.BeastTideAt/SecretRealmAt is null`)를 `Can…`으로 바꾸고, 토스트 문구는 그대로 둔다.
10. **의도된 동작 변경**: 지금 UI는 본산(0·4)에서도 개입을 허용한다. 이 작업 후에는 자연 사건과 같은 후보 지역(`EventRegionIds`: 1~3, 5~12)만 허용하고,
    본산 상세에서는 개입 버튼을 비활성화하고 보조 문구 `"본산에는 천기가 닿지 않습니다."`를 표시한다. 오너가 본산 개입 유지를 원하면 승인 시 이 항목을 지운다.

## 수용 기준 (Acceptance Criteria)
- [ ] 지역 8에 수조 → 비경 순으로 연 뒤 비경만 닫으면 보정 0.8, 상태 "수조: …". 반대 순서·반대 종료도 각각 검사한다(4가지 조합).
- [ ] 둘 다 활성일 때 보정 0.96, 둘 다 끝나면 1.0과 "평온".
- [ ] 보정이 1.2로 저장됐지만 활성 사건이 없는 저장을 로드하면 1.0이 된다.
- [ ] regionId −1·0·13, 같은 지역 같은 종류 중복, 비승해 목록에 없는 인물에 대한 개입이 false를 반환하고 천기가 그대로다.
- [ ] 정상 개입은 기존처럼 비용을 쓰고 사건을 만든다(`Intervention_SpendsFavorAndRefusesWhenEmpty` 통과).
- [ ] 같은 시드·사건 겹침이 없는 1,000년 진행에서 변경 전후 직렬화 바이트가 같다(시드 7·인구 300; 겹침이 생기면 다른 시드를 골라 PR에 적는다).
- [ ] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과(App 테스트 포함).

## 테스트 케이스
- `Refresh_KeepsTideModifierWhenSecretRealmCloses`
- `Refresh_KeepsSecretRealmModifierWhenTideEnds`
- `Refresh_CombinesBothEvents`
- `Refresh_ResetsWhenNoEvents`
- `Load_RecomputesStaleModifier`
- `Intervention_RejectsInvalidRegionWithoutSpending`
- `Intervention_RejectsDuplicateKindWithoutSpending`
- `Intervention_RejectsCultivatorNotInWorld`

## 참고
- 코드: `SecretRealmSystem.cs:16~75·205~215`, `BeastTideSystem.cs:18~75·182~190`, `InterventionSystem.cs:62~125`, `RegionSheet.cs:128~146`.
- 난수 호출 순서는 바뀌지 않는다. 겹침이 있었던 시드만 이후 전개가 달라진다.
