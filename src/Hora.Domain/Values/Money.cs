// USD 금액을 센트 정수로 표현하고 범위를 넘는 연산을 차단한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md, R21.
using System;
using System.Diagnostics.CodeAnalysis;

namespace Hora.Domain.Values
{
    /// <summary>USD 금액. 1달러는 100센트이며 부채는 음수로 표현한다.</summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        private const long CentsPerDollar = 100;

        /// <summary>0센트.</summary>
        public static readonly Money Zero;

        /// <summary>USD의 최소 화폐 단위인 센트 수.</summary>
        [SuppressMessage("Design", "CA1051", Justification = "M0-02 계약은 불변 값 타입의 readonly 필드를 요구한다.")]
        public readonly long Cents;

        private Money(long cents) => Cents = cents;

        /// <summary>부채나 손실처럼 금액이 0센트보다 작은지 나타낸다.</summary>
        public bool IsNegative => Cents < 0;

        /// <summary>센트 정수로 금액을 만든다.</summary>
        /// <param name="cents">센트 수. 음수 허용.</param>
        /// <returns>입력한 센트 금액.</returns>
        public static Money FromCents(long cents) => new Money(cents);

        /// <summary>정수 달러를 센트로 바꾸며 범위를 넘으면 예외를 발생시킨다.</summary>
        /// <param name="dollars">USD 달러 수.</param>
        /// <returns>100을 곱한 센트 금액.</returns>
        /// <exception cref="OverflowException">센트 수가 정수 범위를 벗어난다.</exception>
        public static Money FromDollars(long dollars) => new Money(checked(dollars * CentsPerDollar));

        /// <summary>두 센트 금액을 더한다. 범위 초과는 허용하지 않는다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>단위를 보존한 연산 결과.</returns>
        public static Money operator +(Money a, Money b) => FromCents(checked(a.Cents + b.Cents));

        /// <summary>두 센트 금액의 차를 계산한다. 범위 초과는 허용하지 않는다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>단위를 보존한 연산 결과.</returns>
        public static Money operator -(Money a, Money b) => FromCents(checked(a.Cents - b.Cents));

        /// <summary>금액의 부호를 바꾼다. 최소 정수의 부호 반전은 범위 초과다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <returns>단위를 보존한 연산 결과.</returns>
        public static Money operator -(Money a) => FromCents(checked(-a.Cents));

        /// <summary>센트 금액에 정수 배수를 곱한다. 범위 초과는 허용하지 않는다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="factor">단위 없는 정수 배수.</param>
        /// <returns>단위를 보존한 연산 결과.</returns>
        public static Money operator *(Money a, long factor) => FromCents(checked(a.Cents * factor));

        /// <summary>두 금액의 센트 수가 같은지 비교한다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator ==(Money a, Money b) => a.Cents == b.Cents;

        /// <summary>두 금액의 센트 수가 다른지 비교한다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator !=(Money a, Money b) => a.Cents != b.Cents;

        /// <summary>왼쪽 금액이 더 작은지 비교한다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator <(Money a, Money b) => a.Cents < b.Cents;

        /// <summary>왼쪽 금액이 더 큰지 비교한다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator >(Money a, Money b) => a.Cents > b.Cents;

        /// <summary>왼쪽 금액이 작거나 같은지 비교한다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;

        /// <summary>왼쪽 금액이 크거나 같은지 비교한다.</summary>
        /// <param name="a">왼쪽 USD 센트 금액.</param>
        /// <param name="b">오른쪽 USD 센트 금액.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;

        /// <summary>통화가 같은 두 센트 금액을 비교한다.</summary>
        /// <param name="other">비교할 USD 금액.</param>
        /// <returns>센트 수가 같으면 참.</returns>
        public bool Equals(Money other) => Cents == other.Cents;

        /// <summary>같은 금액 타입과 센트 수인지 비교한다.</summary>
        /// <param name="obj">비교할 객체.</param>
        /// <returns>같은 USD 금액이면 참.</returns>
        public override bool Equals(object? obj) => obj is Money other && Equals(other);

        /// <summary>센트 수에 기반한 해시를 반환한다.</summary>
        /// <returns>동일 금액에 동일한 해시.</returns>
        public override int GetHashCode() => Cents.GetHashCode();

        /// <summary>센트 정수의 오름차순으로 비교한다.</summary>
        /// <param name="other">비교할 금액.</param>
        /// <returns>작으면 음수, 같으면 0, 크면 양수.</returns>
        public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

        /// <summary>디버깅용 USD 표기를 반환한다. 항상 센트 두 자리를 표시한다.</summary>
        /// <returns>예: $1,234.56.</returns>
        public override string ToString() => MoneyFormatter.FormatExact(this, CurrencyView.Usd);
    }
}
