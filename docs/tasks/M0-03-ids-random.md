# [M0-03] 엔티티 ID 타입과 결정론적 난수기

## 배경 (왜)
현행 게임은 `"city-0"` 같은 문자열 ID와 해시 조회를 사용해 느리고 오타에 취약했다(피드백 T4, P4). 정수 기반 전용 ID 타입으로 배열 인덱싱과 컴파일 시점 검사를 얻는다. 같은 시드 = 같은 결과를 위해 플랫폼 독립 난수기가 필요하다.
관련: [코드 규칙 §5.1, §7, §8.2](../standards/coding-conventions.md)

## 범위
- 포함: `src/Hora.Domain/Ids/` 의 ID 타입 6종, `src/Hora.Domain/Random/DeterministicRandom.cs`, 테스트
- 제외: ID 발급기·테이블(M1에서)

## 인터페이스 (계약)
```csharp
namespace Hora.Domain.Ids
{
    // 아래 6종을 같은 형태로 만든다:
    // ContinentId, NationId, CityId, CompanyId, ProductId, FacilityId
    public readonly struct CityId : IEquatable<CityId>, IComparable<CityId>
    {
        public readonly int Value;                 // 테이블 행 인덱스 (0부터)
        public static readonly CityId None;        // Value = -1
        public CityId(int value);                  // value < -1 이면 ArgumentOutOfRangeException
        public bool IsValid { get; }               // Value >= 0
        // ==, !=, Equals, GetHashCode, CompareTo, ToString() → "City#3"
    }
}

namespace Hora.Domain.Random
{
    /// PCG32 (XSH RR). 참조: https://www.pcg-random.org/
    public struct DeterministicRandom
    {
        public DeterministicRandom(ulong seed, ulong streamId);
        public uint NextUInt();
        public int NextInt(int minInclusive, int maxExclusive);   // 편향 없는 범위 (Lemire 방식 또는 거부 샘플링)
        public bool NextChance(int numerator, int denominator);    // numerator/denominator 확률
        public DeterministicRandom Fork(ulong streamId);           // 같은 시드에서 독립 스트림 파생
    }
}
```

## 동작 규칙
1. ID 6종은 코드 중복이 있어도 **각각 별도 구조체**로 만든다(타입 혼동 방지가 목적). 소스 생성기·T4 사용 금지(단순함 유지).
   서로 다른 ID 타입 간 암시적 변환이 없어야 한다.
2. `DeterministicRandom` 은 `float`/`double` 을 사용하지 않는다.
3. `NextInt(min, max)`: `min >= max` 면 `ArgumentException`. 결과 분포에 모듈로 편향이 없어야 한다.
4. `Fork(streamId)`: 원본 상태를 변경하지 않는다. 같은 (seed, streamId) 조합은 항상 같은 수열을 낸다.
5. 구조체이므로 복사 시 상태가 복제된다는 점을 타입 주석에 경고로 적는다 (`ref` 로 전달 권장).

## 수용 기준
- [ ] ID 6종 + 난수기 구현, 모든 public 멤버 XML 주석
- [ ] PCG32 공식 참조 구현과 같은 seed/stream에서 첫 10개 출력이 일치하는 테스트 (기대값은 테스트 코드에 상수로 고정, 출처 주석)
- [ ] `NextInt(0, 6)` 600,000회 분포에서 각 값 빈도가 ±1% 이내 (고정 시드)
- [ ] `NextUInt`, `NextInt` 할당 0B
- [ ] 코드 규칙·format·전체 테스트 통과

## 테스트 케이스
- `CityId_None_IsNotValid`
- `CityId_NegativeBelowMinusOne_Throws`
- `Random_SameSeedAndStream_ProducesSameSequence`
- `Random_DifferentStream_ProducesDifferentSequence`
- `Random_KnownSeed_MatchesReferenceOutput`
- `NextInt_MinNotLessThanMax_Throws`
- `NextInt_Distribution_IsUniformWithinOnePercent`
- `Fork_DoesNotChangeParentState`

## 참고
- 선행: M0-01
