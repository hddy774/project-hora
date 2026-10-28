# [M0-02] `Money`, `Quantity` 값 타입

## 배경 (왜)
현행 게임은 금액을 정수 센트로 다뤘지만 타입이 없어 단위 혼동 위험이 있었다. 사용자 확정 요구 R21(USD, 내부 1달러=100센트 정수), R25(M/B 축약), R26(원화 보기 1$=1,000원)을 타입으로 보장한다.
관련: [코드 규칙 §5.1, §7](../standards/coding-conventions.md)

## 범위
- 포함: `src/Hora.Domain/Values/Money.cs`, `Quantity.cs`, `MoneyMath.cs`, `MoneyFormatter.cs`, 각 테스트
- 제외: 다중 통화(EUR 등), 로컬라이즈 문자열 리소스

## 인터페이스 (계약)
```csharp
namespace Hora.Domain.Values
{
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        public readonly long Cents;                     // 1달러 = 100
        public static readonly Money Zero;
        public static Money FromCents(long cents);
        public static Money FromDollars(long dollars);  // 오버플로 시 OverflowException
        public bool IsNegative { get; }
        public static Money operator +(Money a, Money b);   // checked 연산
        public static Money operator -(Money a, Money b);   // checked 연산
        public static Money operator -(Money a);
        public static Money operator *(Money a, long factor); // checked 연산
        // ==, !=, <, >, <=, >= 및 Equals/GetHashCode/CompareTo
        public override string ToString();              // 디버그용: "$1,234.56"
    }

    public readonly struct Quantity : IEquatable<Quantity>, IComparable<Quantity>
    {
        public readonly long Units;                     // 상품별 최소 단위 (예: g, mL, 개)
        public static readonly Quantity Zero;
        public static Quantity FromUnits(long units);
        // +, -, 비교 연산자 (checked)
    }

    public static class MoneyMath
    {
        // price × quantity / unitsPerPrice, 소수점 이하 내림
        public static Money Multiply(Money unitPrice, Quantity quantity, long unitsPerPrice);
        // total을 weights 비율로 나눠 output에 기록. 합계는 반드시 total과 같다(보존).
        public static void Allocate(Money total, ReadOnlySpan<long> weights, Span<Money> output);
    }

    public enum CurrencyView { Usd, Krw }

    public static class MoneyFormatter
    {
        public const long KrwPerUsd = 1000;             // R26 표시용 고정 환율
        public static string FormatShort(Money value, CurrencyView view);  // "$1.2M", "12억원"
        public static string FormatExact(Money value, CurrencyView view);  // "$1,234,567.89", "1,234,567,890원"
    }
}
```

## 동작 규칙
1. 모든 산술은 `checked`. 오버플로는 버그이므로 `OverflowException` 을 그대로 던진다.
2. `MoneyMath.Multiply`: 중간 계산 오버플로를 피하기 위해 `Math.BigMul` 또는 128비트 계산 사용. 음수 결과는 0 방향이 아닌 **음의 무한대 방향 내림**(floor).
3. `MoneyMath.Allocate` (보존 법칙):
   - 먼저 `total × weight / sum(weights)` 를 내림으로 배분.
   - 남은 센트는 **내림으로 잘린 나머지가 큰 순서**, 같으면 **인덱스가 작은 순서**로 1센트씩 배분 (결정론).
   - `sum(output) == total` 을 항상 만족. weights 합이 0이거나 음수 weight가 있으면 `ArgumentException`.
   - 할당 0B (Span만 사용).
4. `FormatShort` (USD): |값| < $1,000,000 → `$12,345.67` / < $1B → `$1.2M` (소수 1자리, 내림) / 그 이상 → `$3.4B`. 음수는 앞에 `-`.
5. `FormatShort` (KRW): 원 = 센트 × 1000 / 100. |원| < 1억 → `1,234,567원` / < 1조 → `12.3억원` / 그 이상 → `1.2조원`.
   예: $1M → `10억원`, $1B → `1조원` (소수점 아래가 0이면 생략).
6. `FormatExact`: 천 단위 쉼표, USD는 센트 2자리 항상 표시, KRW는 정수 원. 문화권 설정에 영향받지 않도록 `CultureInfo.InvariantCulture` 기반으로 직접 포맷.

## 수용 기준
- [ ] 위 시그니처 모두 구현, 모든 public 멤버 XML 주석(단위 명시)
- [ ] `Allocate` 무작위 테스트 10,000회(고정 시드)에서 합계 보존 100%
- [ ] `Multiply`/`Allocate` 벤치마크(`tools/Hora.Tools`)에서 할당 0B
- [ ] `float`/`double`/`decimal` 미사용 (포맷 계산 포함)
- [ ] 코드 규칙·format·전체 테스트 통과

## 테스트 케이스
- `FromDollars_Overflow_Throws`
- `Add_TwoValues_SumsCents`
- `Multiply_NegativeResult_FloorsTowardNegativeInfinity`
- `Allocate_ThreeEqualWeights_RemainderGoesToLowestIndex` (100센트 → 34, 33, 33)
- `Allocate_RandomInputs_SumEqualsTotal`
- `Allocate_ZeroWeightSum_Throws`
- `FormatShort_Usd_OneMillion_ReturnsOneM` (`$1.0M` 아닌 `$1M`)
- `FormatShort_Krw_OneBillionDollars_ReturnsOneJo` (`1조원`)
- `FormatExact_Usd_Negative_ShowsMinusAndCents`

## 참고
- 선행: M0-01
- 구버전 참고(읽기 전용): `games/monthly-economy/ui/presentation.gd`
