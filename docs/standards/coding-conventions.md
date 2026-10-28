# C# 코드 규칙

> 적용 대상: `src/`, `tests/`, `tools/` 의 모든 C# 코드.
> 우선순위: **① 정확성 → ② 성능 → ③ 가독성·유지보수성 → ④ 간결함**.
> 간결함은 가독성을 해치지 않는 범위에서만 추구한다. 한 줄에 여러 일을 압축하지 않는다.
> 기계적으로 검사 가능한 규칙은 `.editorconfig` 와 Roslyn 분석기로 강제한다.

---

## 0. 핵심 10계명 (요약)

1. **모든 파일·타입·public 멤버에 주석**을 단다. 복잡한 로직에는 "왜"를 쓴다.
2. 이름만 읽어도 무엇을 하는지 알 수 있게 짓는다. 약어 금지 (`cnt` ✗ → `count` ✓).
3. 파일 하나에 타입 하나. **파일 400줄, 메서드 40줄, 중첩 3단계, 한 줄 120자** 이하.
4. 돈은 `Money`(센트 `long`), 수량은 `Quantity`, ID는 전용 구조체. **`float`/`double`로 돈 계산 금지.**
5. 시뮬레이션 코어(`Hora.Domain`, `Hora.Simulation`)는 **엔진 API를 참조하지 않는다.**
6. 예상 가능한 실패는 `Result`로 반환, 예외는 버그·환경 오류에만.
7. 핫 패스(월 정산 반복문)에서 **LINQ·박싱·클로저·문자열 생성·new 할당 금지**.
8. 같은 시드 + 같은 입력 = 같은 결과. **`System.Random`, `DateTime.Now`, `Dictionary` 순회 순서에 의존 금지.**
9. 매직 넘버 금지. 밸런스 수치는 데이터(JSON), 고정 상수는 이름 있는 `const`.
10. 테스트 없는 시뮬레이션 로직은 병합하지 않는다.

---

## 1. 언어 버전과 프로젝트 설정

| 프로젝트 | 대상 | C# 버전 | 이유 |
|---|---|---|---|
| `Hora.Domain`, `Hora.Simulation` | .NET Standard 2.1 | **C# 9** | Unity·Godot 양쪽에서 그대로 사용 |
| `Hora.Client` | 엔진 기본값 | 엔진 기본값 | — |
| `tests/`, `tools/` | .NET 8 이상 | 최신 | 코어를 참조만 함 |

- 코어에서 **C# 10 이상 문법 사용 금지**: file-scoped namespace, global using, `record struct`, `required` 등.
- `<Nullable>enable</Nullable>` 필수. null 가능한 값은 `?`로 명시한다.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (코어 프로젝트).

---

## 2. 이름 규칙

| 대상 | 규칙 | 예시 |
|---|---|---|
| 네임스페이스 | `Hora.<모듈>[.<하위>]` — 폴더 구조와 일치 | `Hora.Simulation.Market` |
| 클래스·구조체·레코드·열거형 | PascalCase, 명사 | `OrderBook`, `CompanyTable` |
| 인터페이스 | `I` + PascalCase | `ISimulationSystem` |
| 메서드 | PascalCase, **동사로 시작** | `MatchOrders()`, `TryReserveCash()` |
| bool 반환 메서드·속성 | `Is/Has/Can/Should` 접두어 | `IsBankrupt`, `CanAfford()` |
| 실패 가능 메서드 | `Try` 접두어 + `out` 또는 `Result` | `TryGetCompany(id, out company)` |
| public 속성·필드 | PascalCase | `MonthIndex` |
| private 필드 | `_camelCase` | `_orderBuffer` |
| static private 필드 | `s_camelCase` | `s_sharedPool` |
| 상수 (`const`) | PascalCase | `MaxCitiesPerNation` |
| 지역 변수·매개변수 | camelCase | `totalDemand` |
| 제네릭 타입 매개변수 | `T` + 설명 | `TCommand` |
| 비동기 메서드 | `Async` 접미어 | `SaveAsync()` |
| 이벤트 | 과거형 또는 `-ing` | `MonthResolved`, `Saving` |
| 테스트 메서드 | `대상_조건_기대결과` | `MatchOrders_WhenBuyPriceBelowAsk_DoesNotTrade` |

추가 규칙:
- 식별자는 **영어**. 한국어는 주석·문서·UI 문자열(리소스 파일)에만.
- 단위가 있는 값은 이름에 단위를 붙이거나 전용 타입을 쓴다: `durationMonths`, `weightGrams`, `Money price`.
- 약어는 널리 알려진 것만 (`Id`, `Ui`, `Ai`, `Json`). 두 글자 약어도 PascalCase (`Ui` ○, `UI` ✗).
- 부정형 bool 금지: `IsNotEmpty` ✗ → `HasItems` ✓.

---

## 3. 파일·코드 구조

### 3.1 크기 제한
| 항목 | 권장 | 최대 (초과 시 PR 거절) |
|---|---|---|
| 파일 길이 | 250줄 | 400줄 |
| 메서드 길이 | 20줄 | 40줄 |
| 중첩 깊이 (if/for) | 2 | 3 |
| 한 줄 길이 | 100자 | 120자 |
| 메서드 매개변수 | 3개 | 5개 (초과 시 매개변수 객체) |
| 클래스의 public 메서드 | 7개 | 15개 |

### 3.2 파일 규칙
- **파일 하나에 최상위 타입 하나**, 파일 이름 = 타입 이름.
- 폴더 = 네임스페이스. `src/Hora.Simulation/Market/OrderBook.cs` → `namespace Hora.Simulation.Market`.
- 멤버 순서: 상수 → static 필드 → 필드 → 생성자 → public 속성 → public 메서드 → internal → private 메서드.
- `using` 은 네임스페이스 바깥, `System` 먼저, 알파벳 순.

### 3.3 서식
- 중괄호는 **항상 새 줄** (Allman). 한 줄짜리 `if` 에도 중괄호 생략 금지.
- 들여쓰기 4칸 공백. 탭 금지.
- 연산자·대입·쉼표 뒤에 공백 (`var total = a + b;`). 공백 없이 붙여 쓰기 금지.
- 한 줄에 하나의 문장. 삼항 연산자 중첩 금지.
- `var` 는 오른쪽에서 타입이 명확할 때만 (`var orders = new List<Order>();` ○, `var x = Calc();` ✗).

---

## 4. 주석 규칙 ⭐ (사람과 AI가 함께 읽는 코드)

주석은 선택이 아니라 **필수**다. 원칙: **코드는 "무엇을/어떻게", 주석은 "왜/의도/제약"을 말한다.**

### 4.1 반드시 주석을 달아야 하는 곳
| 위치 | 형식 | 내용 |
|---|---|---|
| **모든 파일 상단** | `//` 블록 | 이 파일의 책임 1~2줄, 관련 기획서 절 |
| **모든 타입** (public/internal/private 모두) | `/// <summary>` | 무엇을 표현하는가, 누가 사용하는가 |
| **모든 public/internal 멤버** | `/// <summary>`, `<param>`, `<returns>` | 계약: 입력 조건, 반환 의미, 단위 |
| **복잡한 private 메서드** (10줄 이상 또는 분기 3개 이상) | `/// <summary>` | 무엇을 하는지 |
| **알고리즘·수식** | `//` | 수식의 출처·의미, 왜 이 방식인지 |
| **성능 때문에 덜 읽기 쉬운 코드** | `// [성능]` | 왜 이렇게 했는지, 측정 근거 |
| **결정론·보존 법칙 관련 코드** | `// [결정론]`, `// [보존]` | 어떤 불변식을 지키는지 |
| **임시 코드·미완성** | `// TODO(#이슈번호): ...` | 이슈 번호 없는 TODO 금지 |
| **우회 코드(엔진 버그 등)** | `// HACK(#이슈번호): ...` | 원인과 제거 조건 |

### 4.2 작성 규칙
- 주석 언어: **한국어** (식별자·코드 용어는 영어 그대로).
- 코드를 그대로 반복하는 주석 금지:
  ```csharp
  // ✗ 나쁜 예: i를 1 증가시킨다
  i++;

  // ○ 좋은 예: 첫 번째 슬롯은 시스템 예약 계정이므로 건너뛴다
  i++;
  ```
- 값에는 **단위**를 적는다: `/// <param name="duration">공사 기간 (개월, 1 이상)</param>`
- 코드를 바꾸면 주석도 같은 커밋에서 바꾼다. **틀린 주석은 주석이 없는 것보다 나쁘다.**
- 주석 처리된 죽은 코드를 남기지 않는다 (Git 이력에 남아 있다).
- AI 협업을 위해 **모듈 경계·불변식·호출 순서 제약**은 반드시 타입 주석에 적는다. AI는 파일 하나만 읽고 작업하는 경우가 많다.

### 4.3 예시 (표준 형식)
```csharp
// ---------------------------------------------------------------------------
// OrderBook.cs
// 도시 하나·상품 하나의 매수/매도 호가를 모아 가격 순으로 체결한다.
// 관련 기획: docs/redesign/02-game-design.md §5.3 시장
// ---------------------------------------------------------------------------
using System;
using Hora.Domain;

namespace Hora.Simulation.Market
{
    /// <summary>
    /// 한 도시의 한 상품에 대한 월간 호가 장부.
    /// <para>
    /// 사용 순서: <see cref="Clear"/> → <see cref="AddBid"/>/<see cref="AddAsk"/> 반복
    /// → <see cref="Match"/> 1회. <see cref="Match"/> 이후에는 다시 Clear 해야 한다.
    /// </para>
    /// <para>[성능] 월마다 새로 만들지 않고 재사용한다(할당 0 목표).</para>
    /// </summary>
    public sealed class OrderBook
    {
        /// <summary>매수 호가 버퍼. 용량은 생성 시 고정되며 초과 시 확장한다.</summary>
        private Order[] _bids;

        /// <summary>현재 유효한 매수 호가 개수.</summary>
        private int _bidCount;

        // ... (중략)

        /// <summary>
        /// 매수·매도 호가를 가격 순으로 체결하고 체결 결과를 <paramref name="output"/>에 기록한다.
        /// </summary>
        /// <param name="output">체결 결과를 받을 버퍼. 호출자가 소유하며 이 메서드는 비우지 않는다.</param>
        /// <returns>이번 달 대표 체결 가격. 체결이 없으면 <see cref="Money.Zero"/>.</returns>
        public Money Match(TradeBuffer output)
        {
            // [결정론] 가격이 같으면 주문 ID 오름차순으로 정렬해 항상 같은 체결 순서를 보장한다.
            SortBids();
            SortAsks();

            // ...
        }
    }
}
```

---

## 5. 타입 설계

### 5.1 도메인 기본 타입 (원시 타입 집착 금지)
| 개념 | 타입 | 이유 |
|---|---|---|
| 금액 | `readonly struct Money` (내부 `long` 센트) | 부동소수점 오차 방지, 통화 단위 혼동 방지 (R21) |
| 수량 | `readonly struct Quantity` (내부 `long`, 최소 단위) | 단위 혼동 방지 (R22) |
| 엔티티 ID | `readonly struct CompanyId`, `CityId`, `ProductId` … (내부 `int`) | ID 종류 혼동을 컴파일러가 차단, 배열 인덱스로 직접 사용 |
| 기간 | `readonly struct MonthIndex` | 월 계산 실수 방지 |
| 비율 | `readonly struct Ratio` (내부 `int`, 1/10,000 단위) | 결정론적 정수 연산 |

```csharp
/// <summary>회사 식별자. 값은 <see cref="CompanyTable"/>의 행 인덱스와 같다.</summary>
public readonly struct CompanyId : IEquatable<CompanyId>
{
    /// <summary>테이블 행 인덱스 (0부터).</summary>
    public readonly int Value;

    // ...
}
```

### 5.2 class / struct / record 선택
| 상황 | 선택 |
|---|---|
| 16바이트 이하, 불변 값 | `readonly struct` |
| 많은 개수의 시뮬레이션 데이터 (회사·시설·화물) | 구조체 배열 또는 SoA 테이블 (§8.2) |
| 불변 정의 데이터 (상품 정의, 레시피) | `sealed class` (생성 후 읽기 전용) |
| 명령, 이벤트, DTO | `sealed record` (C# 9 class record) |
| 서비스·시스템 | `sealed class` + 인터페이스 |

- 상속은 2단계까지. 기본은 `sealed`. 확장이 필요하면 상속보다 인터페이스·합성을 쓴다.
- `public` 필드는 `readonly struct` 에서만 허용. 그 외는 속성.
- 컬렉션 반환은 `IReadOnlyList<T>` 또는 `ReadOnlySpan<T>`.

### 5.3 열거형
- 상태·종류는 문자열이 아니라 `enum` (`"Planning"` ✗ → `TurnPhase.Planning` ✓).
- 오류 코드도 `enum` (`ErrorCode.InsufficientCash`). 표시 문구는 UI 리소스에서 매핑.
- `switch` 는 모든 값을 처리하고 `default` 에서 예외(버그)를 던진다.

---

## 6. 오류 처리

| 상황 | 처리 |
|---|---|
| 플레이어·AI 명령이 규칙상 불가 (잔액 부족 등) | `Result` / `CommandResult` 반환. **예외 금지** |
| 정의 데이터 오류 (JSON 누락 필드) | 로드 시 `DataValidationException`, 게임 시작 차단 |
| 불변식 위반 (재고 음수 등) = 버그 | `InvariantViolationException` — Debug/테스트에서 즉시 중단 |
| 파일 입출력 실패 | 예외를 잡아 사용자 메시지 + 백업 복구 경로 |

```csharp
/// <summary>명령 검증 결과. 실패 시 <see cref="Error"/>에 이유가 담긴다.</summary>
public readonly struct CommandResult
{
    public readonly bool IsSuccess;
    public readonly ErrorCode Error;
    // ...
}
```

- `catch (Exception)` 로 삼키기 금지. 잡으면 기록하거나 다시 던진다.
- `null` 반환 대신 `TryXxx(out T)` 패턴을 쓴다.

---

## 7. 결정론 규칙 (리플레이·테스트의 전제)

- 난수는 `Hora.Domain.DeterministicRandom`(PCG32)만 사용. 시스템별로 시드에서 파생한 별도 스트림.
- 시뮬레이션 코어에서 금지: `System.Random`, `DateTime.Now`, `Guid.NewGuid()`, `Environment.TickCount`, 멀티스레드 결과 합산 순서 의존.
- `Dictionary`/`HashSet` 순회 결과에 의존하는 로직 금지 → 정렬된 배열 또는 ID 순서로 순회.
- 금액·수량·비율 계산은 정수. 나눗셈 반올림 규칙은 `MoneyMath` 한 곳에 정의(은행가 반올림 금지, 명시적 내림 + 잔여 배분).
- 테스트: 같은 시드로 2회 실행한 상태 해시가 같아야 한다 (CI 필수 테스트).

---

## 8. 성능 규칙 ⭐

성능 목표는 기획서 §7.3을 따른다 (기본 세계 월 정산 ≤ 300ms, 정산 중 할당 ≤ 1MB).

### 8.1 핫 패스 정의
월 정산 시스템의 `Execute` 와 그 안에서 엔티티 수에 비례해 반복 호출되는 모든 코드. 파일 상단 주석에 `// [핫 패스]` 로 표시한다.

### 8.2 데이터 배치
- 엔티티는 **정수 ID + 배열**로 저장. 대량 데이터는 SoA(Struct of Arrays) 테이블을 우선 고려한다.
  ```csharp
  /// <summary>
  /// 모든 회사의 상태를 열(column) 단위 배열로 보관한다.
  /// [성능] 시스템은 필요한 열만 순차 접근하므로 캐시 효율이 높다.
  /// </summary>
  public sealed class CompanyTable
  {
      public int Count;
      public Money[] Cash;          // 회사별 현금 (센트)
      public CityId[] HomeCity;     // 본사 도시
      public CompanyStatus[] Status;
  }
  ```
- 문자열 ID는 데이터 로드 시점에만 사용하고 즉시 정수 인덱스로 변환한다.
- 세계 상태 **깊은 복사 금지**. 저장이 필요하면 직렬화, 되돌리기가 필요하면 명령 로그 재실행.

### 8.3 핫 패스 금지 목록
| 금지 | 대안 |
|---|---|
| LINQ (`Where`, `Select`, `OrderBy`…) | `for` 루프 |
| `foreach` over `IEnumerable<T>` 인터페이스 | 배열/`List<T>`/`Span<T>` 에 `for` |
| 람다·클로저 캡처 | static 메서드, 구조체 비교자 |
| 루프 안 `new` (참조 타입) | 재사용 버퍼, `ArrayPool<T>`, 객체 풀 |
| 문자열 연결·`string.Format`·보간 | 원인 기록은 enum + ID로 저장, 문자열화는 UI에서 |
| 박싱 (구조체 → `object`/인터페이스) | 제네릭 제약 `where T : struct` |
| `Dictionary` 조회 반복 | ID 배열 인덱싱 |
| 예외를 흐름 제어에 사용 | `Try` 패턴 |

### 8.4 측정 원칙
- **추측하지 말고 측정한다.** 최적화 PR에는 전/후 벤치마크 수치를 첨부한다.
- `Hora.Tools` 의 BenchmarkDotNet으로 시스템별 시간·할당을 측정한다.
- CI의 `perf-guard` 는 기준 대비 **20% 이상 느려지면 실패**한다.
- 가독성을 크게 해치는 최적화는 측정으로 효과가 입증될 때만, `// [성능]` 주석과 함께 허용한다.

### 8.5 클라이언트(엔진) 성능
- 시뮬레이션은 UI 스레드를 막지 않는다: 정산은 백그라운드 스레드 1개에서 실행하고 결과만 메인 스레드로 넘긴다 (정산 중 상태 접근 금지).
- `Update()` 에서 매 프레임 계산 금지 → 상태 변경 이벤트가 올 때만 화면 갱신.
- 리스트 UI는 가상화(보이는 항목만 생성). 아이콘·스프라이트는 아틀라스 사용.
- 컴포넌트·노드 참조는 초기화 시 캐시 (`GetComponent`/`GetNode` 반복 호출 금지).

---

## 9. 아키텍처 규칙

- 의존 방향: `Client → Simulation → Domain`. 역방향 참조는 컴파일 오류가 나도록 어셈블리를 분리한다.
- 시스템은 `ISimulationSystem` 만 구현하고 서로를 직접 호출하지 않는다. 데이터는 `WorldState` 를 통해 주고받는다.
- 상태 변경 경로는 둘뿐이다: **명령(`ICommand`)** 과 **시스템**. UI가 상태를 직접 수정하지 않는다.
- static 가변 상태 금지 (캐시도 인스턴스로 소유). 순수 함수형 유틸리티의 static 메서드는 허용.
- 클라이언트는 MVP: `View`(엔진 노드) ↔ `Presenter`(순수 C#, 테스트 가능) ↔ `Simulation`. View에 로직 금지.

```csharp
/// <summary>
/// 월 정산 파이프라인의 한 단계.
/// <para>
/// 구현 규칙: 다른 시스템을 참조하지 않는다. <see cref="Execute"/> 안에서 할당하지 않는다.
/// 같은 입력에 항상 같은 결과를 낸다(결정론).
/// </para>
/// </summary>
public interface ISimulationSystem
{
    /// <summary>시스템 실행 순서 확인과 로그에 쓰이는 이름.</summary>
    string Name { get; }

    /// <summary>한 달치 처리를 수행하고 <paramref name="world"/>를 갱신한다.</summary>
    /// <param name="world">갱신 대상 세계 상태.</param>
    /// <param name="context">이번 달 번호, 난수 스트림, 원인 기록기.</param>
    void Execute(WorldState world, MonthContext context);
}
```

---

## 10. 테스트 규칙

- 프레임워크: NUnit. 테스트 프로젝트는 대상 프로젝트와 1:1 (`Hora.Simulation.Tests`).
- 이름: `대상_조건_기대결과`. 본문은 **Arrange / Act / Assert** 주석으로 구분.
- 필수 테스트 종류:
  | 종류 | 대상 | 예 |
  |---|---|---|
  | 단위 | 각 시스템·타입 | `Money` 반올림, 호가 매칭 |
  | 불변식 | 매 정산 | 현금 총합 = 발행 − 소멸, 재고 ≥ 0 |
  | 결정론 | 전체 정산 | 같은 시드 2회 → 같은 해시 |
  | 회귀(골든) | 고정 시드 N개월 | 주요 지표가 기준 파일과 일치 |
  | 성능 | 벤치마크 | 기준 대비 20% 이내 |
- 시뮬레이션 로직의 라인 커버리지 목표 **80% 이상**.
- 테스트도 주석 규칙을 따른다 (무엇을 검증하는지 `/// <summary>`).

---

## 11. 엔진(클라이언트) 코드 규칙

- 엔진 스크립트(`MonoBehaviour` / `Node`)는 **얇게**: 입력 전달, 화면 표시, 애니메이션만.
- 게임 규칙·계산은 Presenter 또는 코어에. 엔진 스크립트에서 `Money` 계산 금지.
- UI 문자열은 코드에 하드코딩하지 않고 로컬라이즈 테이블에서 가져온다.
- 씬·프리팹 파일 이름은 PascalCase (`MarketScreen`).

---

## 12. 도구로 강제하는 항목

| 도구 | 검사 |
|---|---|
| `.editorconfig` | 들여쓰기, 중괄호, 이름 규칙, `var` 사용, using 정렬 |
| `dotnet format --verify-no-changes` | 서식 위반 시 CI 실패 |
| Roslyn 분석기 (`AnalysisLevel=latest-recommended`) | 사용하지 않는 코드, null 경고 |
| `CS1591` 경고를 오류로 | public 멤버 XML 주석 누락 시 빌드 실패 |
| 커스텀 검사 스크립트 (M0에서 작성) | 파일 400줄·한 줄 120자 초과, 이슈 번호 없는 TODO |

도구가 잡지 못하는 항목(주석의 질, "왜" 설명, 이름의 의미)은 리뷰에서 확인한다.
