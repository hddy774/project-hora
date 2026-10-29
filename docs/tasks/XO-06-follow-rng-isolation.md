# [XO-06] 선협 관찰 — 관심 표시와 세계 난수 분리

상태: 승인(사용자 전체 개발 지시, 2026-10-29) · 우선순위 P1 · 선행 결정: D-01(관심은 순수 관찰) — **2026-10-29 승인됨**
검토 근거: [13 §2 R-03·R-17](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
로그 표본 추출이 `notable || world.Random.Chance(p)` 형태라, 관심 인물이면 세계 난수를 덜 소비한다.
그래서 관심 표시만으로 이후 역사가 달라진다(시드 7·100명·600년 실험). 관찰자 게임에서 "보기만 해도 바뀐다"는
같은 시드 비교·공유를 막는다. 로그 표본은 세계 결과에 영향이 없어야 한다.

## 범위
- 포함
  - `Model/World.cs`: 로그 전용 난수 `LogRandom`과 저장 필드, 관심 색인.
  - `Systems/WorldFactory.cs`: 생성 시 `LogRandom` 초기화.
  - `Persistence/WorldSerializer.cs`: 저장 시 상태 캡처, 로드 시 복원·기본값.
  - 표본 추출을 `LogRandom`으로 바꿀 곳(실제 대조 결과 5곳): `BattleAftermath.cs:45~47`, `BattleAftermath.cs:150~152`, `SecretRealmSystem.cs:178`, `LifeCycleSystem.cs:75`, `BreakthroughSystem.cs:165`.
  - 테스트: `tests/Xianxia.Simulation.Tests/FollowIsolationTests.cs`(신규).
- 제외
  - 다른 시스템의 난수 스트림 분리. 로그 표본 확률(0.1·0.015·0.08·0.3) 변경.
  - 관심 인물 알림 규칙 변경.

## 인터페이스 (계약)
```csharp
// World
/// <summary>로그 표본 추출 전용 난수. 세계 결과에 쓰지 않는다. 저장 시 아래 두 필드로 캡처한다.</summary>
[JsonIgnore] public Pcg32 LogRandom { get; set; }
/// <summary>LogRandom 상태(저장용). 0이면 구 저장으로 보고 시드에서 다시 만든다.</summary>
public ulong LogRandomState { get; set; }
public ulong LogRandomIncrement { get; set; }
/// <summary>관심 목록이 바뀐 뒤 색인을 다시 만든다. Followed를 직접 바꾸는 코드는 이 함수를 호출해야 한다.</summary>
public void RebuildFollowIndex();
```
- 스트림 번호: `WorldFactory`의 세계 스트림 상수와 다른 상수 `LogRandomStream = 0x4C4F47`("LOG")을 쓴다. `new Pcg32(settings.Seed, LogRandomStream)`.

## 동작 규칙
1. 다섯 표본 지점은 `notable || world.LogRandom.Chance(p)`로 바꾼다. 세계 난수 `world.Random`은 이 지점에서 **호출하지 않는다.**
2. `CaptureRandomState`가 `LogRandomState/Increment`도 캡처한다. 로드 시 `LogRandomIncrement == 0`이면 `new Pcg32(Settings.Seed, LogRandomStream)`, 아니면 `Pcg32.FromState`.
3. `IsFollowed`는 `HashSet<int>` 색인(`[JsonIgnore]`)으로 조회한다. 저장 형식은 `List<int> Followed` 그대로.
   `RebuildIndexes`와 관심을 바꾸는 모든 앱 코드(`CultivatorSheet` 등, `Followed.Add/Remove` 검색)가 `RebuildFollowIndex`를 호출한다.
4. 관심을 바꾸는 경로는 `World.SetFollowed(int soulId, bool followed)`(신규 public)로 한 곳에 모으고 내부에서 색인을 갱신한다.
   현재 직접 바꾸는 곳은 두 군데다: `CultivatorSheet.cs:174~176`(토글), `LifeCycleSystem.cs:165`(비승 시 제거). 둘 다 이 함수로 교체한다.
5. 그 밖의 `IsFollowed` 사용(로그·알림 문구 분기)은 세계 난수를 쓰지 않으므로 그대로 둔다. PR에 `grep -n IsFollowed` 결과와 각 지점의 판단을 표로 첨부한다.

## 수용 기준 (Acceptance Criteria)
- [x] 시드 7·인구 100·600년, 관심 0명 vs 100명(0년에 전원 관심): `world.Random.State`가 같고,
      `Log`·`Chronicle`(알림 포함)·`Followed`·`LogRandom*`를 뺀 직렬화 JSON이 같다.
- [x] 같은 실험을 관심 1명(0년, Id 최소 인물)과 300년째 관심 추가로도 반복해 같은 결과.
- [x] 저장 → 로드 → 계속 진행이 저장 없이 진행한 것과 직렬화 바이트까지 같다(`SaveAndLoad_ContinuesExactlyTheSameHistory` 통과).
- [x] `LogRandom*` 필드가 없는 구 저장(JSON에서 두 필드 삭제)을 로드해 진행할 수 있다.
- [x] 관심 1,000명 세계에서 1,000년 진행 시간이 관심 0명 대비 5% 이내(PR에 수치).
- [x] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과.

## 테스트 케이스
- `Following_DoesNotChangeWorldRandomState`
- `Following_DoesNotChangeCoreState`
- `FollowingMidRun_DoesNotChangeWorld`
- `Load_WithoutLogRandom_CreatesFromSeed`
- `SetFollowed_UpdatesIndex`

비교용 "핵심 상태"는 직렬화 JSON을 `System.Text.Json.Nodes`로 읽어 `Log`, `Chronicle`, `Followed`, `LogRandomState`, `LogRandomIncrement` 키를 지운 뒤 문자열 비교한다.

## 참고
- 이 변경 후 **모든 시드의 미래가 한 번 바뀐다**(세계 난수 호출 수가 줄기 때문). 버전 노트에 적는다(D-09). XO-10 측정은 이 작업 뒤에 한다.
- 관련: `World.cs:128·189~192`, `Narrator.cs`, `WorldFactory.cs:23`.

실행 근거: 시뮬레이션45+UI11 성공. 관심 0/1/100명·300년 중도 등록의 핵심 상태가 동일하다.
전원 관심의 로그 생성 비용은 이름 스냅샷·지연 문장/알림으로 줄였으며 보관 순서·본문·저장 형식을 유지했다.
1,000명·1,000년 10회 교대 실행 중앙값은0명614.278ms/전원634.686ms(+3.32%)다.
원문: `games/xianxia-observer/docs/measurements/follow-isolation-20261029.json`. 호스트 변동이 있어 Android 성능으로 일반화하지 않는다.
