# [XO-03] 선협 관찰 — 저장 의미 검증과 손상 저장 보존

상태: 승인(사용자 전체 개발 지시, 2026-10-29) · 우선순위 P1 · 검토 근거: [13 §2 R-01·R-15](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
유효한 gzip JSON이지만 의미가 틀린 저장(예: `RegionId=999`)을 불러오면 `World.RebuildIndexes`가
`ArgumentOutOfRangeException`을 던진다. `SaveStore.TryLoad`는 `SaveFormatException`만 격리하므로
앱 시작 자동 로드가 예외로 끝난다. 손상 저장 격리 파일(`.broken`)도 1개만 남아 덮어써진다.

## 범위
- 포함
  - `games/xianxia-observer/src/Xianxia.Simulation/Persistence/WorldSerializer.cs`: 역직렬화 직후 의미 검증 추가.
  - 새 파일 `games/xianxia-observer/src/Xianxia.Simulation/Persistence/WorldValidator.cs`.
  - `games/xianxia-observer/src/Xianxia.App/Services/SaveStore.cs`: 격리 파일 이름과 보존 개수.
  - 테스트: `tests/Xianxia.Simulation.Tests/SaveValidationTests.cs`(신규), `tests/Xianxia.App.Tests`에 SaveStore 테스트.
- 제외
  - 저장 형식(FormatVersion) 변경, 마이그레이션 표, 다중 슬롯, 내보내기.
  - 직전 정상본 `.bak` 유지와 flush는 XO-07에서 한다.
  - 시뮬레이션 규칙·난수 변경 없음.

## 인터페이스 (계약)
```csharp
namespace Xianxia.Simulation.Persistence
{
    /// <summary>불러온 세계의 의미 검증. 색인 재구축 전에 호출한다.</summary>
    public static class WorldValidator
    {
        /// <summary>문제가 있으면 첫 문제를 한국어 문장으로 돌려준다. 정상이면 null.</summary>
        public static string? FindProblem(World world);
    }
}
```
`WorldSerializer.Load` 순서: 역직렬화 → 기존 버전·빈 인구 검사 → **`FindProblem`이 null이 아니면
`SaveFormatException(문제 문장)`** → `Normalize` → `RebuildIndexes`.
`FindProblem`은 `Normalize`가 채우는 길이 부족 목록(Log.Counts, Ledger, 시설 배열)은 문제로 보지 않는다.

## 동작 규칙
`FindProblem`은 아래를 순서대로 검사하고 첫 위반의 문장을 반환한다.
1. 파일 크기: `SaveStore`는 압축 파일이 **32 MiB**를 넘으면 읽지 않고 SaveFormatException 경로로 격리한다.
   압축 해제 스트림은 **256 MiB**를 넘으면 중단하고 SaveFormatException을 던진다(WorldSerializer에서 길이 제한 스트림 사용).
2. `FormatVersion`이 1 미만이면 문제.
3. `Settings`, `Cultivators`, `Sects`, `Regions`, `Souls`, `Wars`, `Tournaments`, `Immortals`, `Followed`, 각 인물의
   리스트·배열 속성 중 하나라도 null이면 문제. (데이터 사전 `reference/data-dictionary.md`의 컬렉션 속성 전체)
4. `Regions.Count`가 `RegionCatalog`의 지역 수(13)와 다르면 문제. `Sects.Count`가 2가 아니면 문제.
5. 각 인물: `Id > 0`, Id 중복 없음, `0 ≤ RegionId < Regions.Count`, `SectId ∈ {0,1,2}`,
   `0 ≤ Realm ≤ 8`, `0 ≤ Stage ≤ 3`, `0 ≤ TechniqueGrade ≤ 19`, `0 ≤ ArtifactGrade ≤ 5`,
   `Qi`·`Insight`·`DaoHeart`·`Injury`가 유한수(NaN·무한대 아님)이고 0 이상, `Age ≥ 0`, `Lifespan > 0`.
6. `NextSoulId`가 모든 인물 Id·Souls의 SoulId보다 커야 한다.
7. 활성 비경·수조의 `RegionId`가 범위 안이어야 한다.
8. `RandomIncrement`가 짝수면 문제(PCG increment는 홀수).
9. 관계 ID(MasterId, CompanionId, 제자·숙적 목록)가 존재하지 않는 영혼을 가리키는 것은 **문제로 보지 않는다**
   (기존 정리 규칙이 처리한다). 단 자기 자신을 가리키면 문제.

`SaveStore`:
10. 격리 파일 이름은 `world.sav.broken-yyyyMMddHHmmss`(UTC). 같은 초에 겹치면 `-1`, `-2` 접미사.
11. 격리 후 `world.sav.broken-*`가 3개를 넘으면 이름순으로 가장 오래된 것부터 지운다. 기존 `world.sav.broken`(접미사 없음)은 건드리지 않는다.
12. `TryLoad`의 사용자 문구는 기존 형식을 유지하고 괄호 안에 `FindProblem` 문장을 넣는다.

## 수용 기준 (Acceptance Criteria)
- [ ] 규칙 2~8의 위반을 각각 담은 저장 바이트가 `SaveFormatException`을 던진다(예외 종류가 다른 것은 실패).
- [ ] 정상 세계(시드 7, 인구 300, 1,000년 진행)의 저장 → 로드가 기존처럼 성공하고 `SaveAndLoad_ContinuesExactlyTheSameHistory`가 통과한다.
- [ ] `RegionId=999` 저장이 있을 때 `GameSession.LoadOrCreate`가 예외 없이 새 세계와 문제 문구를 돌려주고 격리 파일이 생긴다.
- [ ] 격리를 5번 하면 `broken-*` 파일이 3개만 남는다.
- [ ] 32 MiB 초과 파일은 읽지 않고 격리된다(테스트는 임시 폴더에 희소 파일 또는 큰 바이트로 확인).
- [ ] 1,000명 세계 로드 시간 증가가 검증 전 대비 10% 이하(Release, 같은 기기에서 10회 평균. PR에 수치 첨부).
- [ ] `dotnet format --verify-no-changes`, `bash ../../tools/check-code-rules.sh .`, Release 빌드 경고 0, 전체 테스트 통과.

## 테스트 케이스
- `Load_RejectsRegionOutOfRange`
- `Load_RejectsDuplicateCultivatorId`
- `Load_RejectsUnknownSectId`
- `Load_RejectsNullCollection`
- `Load_RejectsNaNQi`
- `Load_RejectsEvenRandomIncrement`
- `Load_RejectsNextSoulIdNotGreaterThanIds`
- `Load_AcceptsMissingRelationshipTarget`
- `Load_AcceptsShortCountArraysAndPadsThem`
- `TryLoad_QuarantinesSemanticErrorWithTimestamp`
- `TryLoad_KeepsAtMostThreeBrokenFiles`
- `TryLoad_SkipsOversizedFile`

테스트 저장 만드는 법: 정상 세계를 `WorldSerializer.ToBytes`로 만든 뒤 gzip을 풀어 `System.Text.Json.Nodes`로 한 필드를 바꾸고 다시 압축한다.

## 참고
- 선행 작업 없음. XO-07과 같은 파일(SaveStore)을 고치므로 XO-03을 먼저 병합한다.
- 코드: `World.RebuildIndexes`(World.cs:197~240), `SaveStore.TryLoad`(SaveStore.cs:59~100).
- 게임 폴더에서 검사: `dotnet restore XianxiaObserver.slnx --locked-mode && dotnet build XianxiaObserver.slnx -c Release --no-restore -warnaserror && dotnet test XianxiaObserver.slnx -c Release --no-build`.
