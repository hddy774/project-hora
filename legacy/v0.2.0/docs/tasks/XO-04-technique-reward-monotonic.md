# [XO-04] 선협 관찰 — 공법 보상 단조성

상태: 승인(사용자 전체 개발 지시, 2026-10-29) · 우선순위 P1 · 검토 근거: [13 §2 R-02](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
기연의 공법 보상 `ActionEffects.GainTechnique`는 `Math.Min(15, 기존 + 획득)`이다. 장경각(최대 18)·경매(최대 16)로
16~18등급을 가진 인물이 기연을 만나면 **15등급으로 떨어진다.** 로그는 "공법을 깨쳤다"고 표시한다.
비경 보상 `Math.Min(18, 기존 + 1)`도 같은 형태라 19등급이 생기면 하락한다.

## 범위
- 포함
  - `games/xianxia-observer/src/Xianxia.Simulation/Data/Techniques.cs`: 보상 계산 함수 추가.
  - `.../Systems/ActionEffects.cs`(GainTechnique), `.../Systems/SecretRealmSystem.cs`(공법 보물): 새 함수 사용.
  - 테스트: `tests/Xianxia.Simulation.Tests/RewardTests.cs`(신규).
- 제외
  - 보상 상한 값(15·18) 변경, 19등급 획득 경로 추가(오너 결정 D-04 대기).
  - 경매(`AuctionSystem`)와 장경각·상회: 이미 상위 등급만 주므로 변경하지 않는다.
  - 상위 등급이라 보상을 못 받는 경우 다른 보상으로 바꾸는 규칙(난수 호출이 늘어나므로 제외).

## 인터페이스 (계약)
```csharp
/// <summary>
/// 공법 보상 후 등급. 기존 등급이 보상 상한 이상이면 그대로 두고, 아니면 상한까지만 올린다. 보상으로 등급이 내려가지 않는다.
/// </summary>
/// <param name="current">현재 등급(0~19).</param>
/// <param name="gain">올릴 등급 수(1 이상).</param>
/// <param name="rewardCap">이 보상이 줄 수 있는 최고 등급.</param>
/// <returns>max(current, min(rewardCap, current + gain)).</returns>
public static int ImproveByReward(int current, int gain, int rewardCap);
```

## 동작 규칙
1. 결과 = `Math.Max(current, Math.Min(rewardCap, current + gain))`.
2. `gain ≤ 0`이면 `ArgumentOutOfRangeException`. `rewardCap`이 0~19 밖이면 `ArgumentOutOfRangeException`.
3. 기연: `ImproveByReward(기존, Range(1,2), 15)`. **`Range(1,2)` 호출은 지금처럼 항상 한 번** 한다(난수 호출 수 보존).
4. `GainTechnique`가 돌려주는 문구(로그의 "기연을 만나 {문구}." 자리): 등급이 오르지 않으면
   `"고대 비급을 얻었으나 이미 " + 공법명 + " 공법을 익혀 새로 깨친 것이 없었다"`. 오른 경우 기존 문구 유지.
   생애 기록·기연 횟수·호칭 처리는 기존과 같다.
5. 비경: `ImproveByReward(기존, 1, 18)`. 오르지 않으면 보물 이름을 `"(이미 익힌) " + 공법명 + " 공법"`으로 기록한다.
   수확 점수(Gains) 계산은 바꾸지 않는다.
6. 두 경로 모두 공법 속성(TechniqueElement)을 바꾸지 않는다(현재 코드와 같음). 수확 점수 `value=3`도 그대로 둔다.

## 수용 기준 (Acceptance Criteria)
- [x] 등급 0~19 × gain 1~2 × 상한 15·18의 모든 조합에서 결과 ≥ 기존 등급(테스트로 전수 확인).
- [x] 기존 16·17·18등급 인물에게 기연 공법 보상을 적용하면 등급이 그대로다.
- [x] 기존 14등급 + gain 2 → 15(상한 적용)는 기존과 같다.
- [x] 기연 1회의 난수 호출 수가 변경 전과 같다(같은 시드에서 보상 직후 `Random.State` 비교).
- [x] 장기 불변식 테스트(`InvariantTests`) 전부 통과.
- [x] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과.

## 테스트 케이스
- `ImproveByReward_NeverLowersGrade` (전수 조합)
- `ImproveByReward_RejectsNonPositiveGain`
- `Fortune_KeepsGradeAboveCap`
- `Fortune_ConsumesSameRandomCalls`
- `SecretRealm_TechniqueTreasureKeepsHigherGrade`

기연은 public `ActionEffects.Fortune(world, cultivator, where)`를 쓰되, 보상 종류가 난수로 정해지므로
`GainTechnique`를 `internal`로 바꿔 직접 검사한다(`InternalsVisibleTo Xianxia.Simulation.Tests`가 이미 있다).
비경 `Treasure`도 `internal`로 바꿔 직접 검사한다. reflection은 쓰지 않는다.

## 참고
- 코드: `ActionEffects.cs:301~305`, `SecretRealmSystem.cs:145~155`(Treasure), `Techniques.cs`.
- 같은 시드의 미래가 바뀔 수 있다(하락 사례가 있던 시드만). 버전 노트에 적는다(D-09).

완료 근거(2026-10-29): 대표 PR #32의 상태 원본과 연결된 서브 PR/CI, 현재 구현 문서20을 따른다. Android 실제 입력·출시는 별도 XO-09/UI-QA/RELEASE-02에서 완료 전까지 추적한다.
