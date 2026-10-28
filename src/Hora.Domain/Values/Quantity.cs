// 상품 정의가 정한 최소 단위 수량을 정수로 보관한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md, R22.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Hora.Domain.Values
{
    /// <summary>
    /// 상품별 최소 단위 수량(g, mL, 개 등). 서로 다른 상품·단위인지는 호출자가 검증한다.
    /// 음수는 재고 변화량에 사용할 수 있으며 실제 보유량 제약은 재고 시스템이 검사한다.
    /// </summary>
    public readonly struct Quantity : IEquatable<Quantity>, IComparable<Quantity>
    {
        /// <summary>최소 단위 0개.</summary>
        public static readonly Quantity Zero;

        /// <summary>해당 상품 정의에 명시된 최소 단위 수.</summary>
        [SuppressMessage("Design", "CA1051", Justification = "M0-02 계약은 불변 값 타입의 readonly 필드를 요구한다.")]
        public readonly long Units;

        private Quantity(long units) => Units = units;

        /// <summary>최소 단위 수로 수량을 만든다.</summary>
        /// <param name="units">g, mL, 개 등 상품별 최소 단위 수.</param>
        /// <returns>입력 수량.</returns>
        public static Quantity FromUnits(long units) => new Quantity(units);

        /// <summary>같은 단위 수량을 더하며 범위를 넘으면 예외를 발생시킨다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>단위를 보존한 연산 결과.</returns>
        public static Quantity operator +(Quantity a, Quantity b) => FromUnits(checked(a.Units + b.Units));

        /// <summary>같은 단위 수량을 빼며 범위를 넘으면 예외를 발생시킨다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>단위를 보존한 연산 결과.</returns>
        public static Quantity operator -(Quantity a, Quantity b) => FromUnits(checked(a.Units - b.Units));

        /// <summary>최소 단위 수가 같은지 비교한다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator ==(Quantity a, Quantity b) => a.Units == b.Units;

        /// <summary>최소 단위 수가 다른지 비교한다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator !=(Quantity a, Quantity b) => a.Units != b.Units;

        /// <summary>왼쪽 수량이 작은지 비교한다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator <(Quantity a, Quantity b) => a.Units < b.Units;

        /// <summary>왼쪽 수량이 큰지 비교한다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator >(Quantity a, Quantity b) => a.Units > b.Units;

        /// <summary>왼쪽 수량이 작거나 같은지 비교한다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator <=(Quantity a, Quantity b) => a.Units <= b.Units;

        /// <summary>왼쪽 수량이 크거나 같은지 비교한다.</summary>
        /// <param name="a">왼쪽 상품별 최소 단위 수량.</param>
        /// <param name="b">오른쪽 상품별 최소 단위 수량.</param>
        /// <returns>비교 결과.</returns>
        public static bool operator >=(Quantity a, Quantity b) => a.Units >= b.Units;

        /// <summary>같은 최소 단위 수인지 비교한다.</summary>
        /// <param name="other">비교할 수량.</param>
        /// <returns>최소 단위 수가 같으면 참.</returns>
        public bool Equals(Quantity other) => Units == other.Units;

        /// <summary>수량 타입과 최소 단위 수가 같은지 비교한다.</summary>
        /// <param name="obj">비교할 객체.</param>
        /// <returns>같은 수량이면 참.</returns>
        public override bool Equals(object? obj) => obj is Quantity other && Equals(other);

        /// <summary>최소 단위 수를 이용한 해시를 반환한다.</summary>
        /// <returns>같은 수량에 같은 해시.</returns>
        public override int GetHashCode() => Units.GetHashCode();

        /// <summary>최소 단위 수의 오름차순을 비교한다.</summary>
        /// <param name="other">비교할 수량.</param>
        /// <returns>작으면 음수, 같으면 0, 크면 양수.</returns>
        public int CompareTo(Quantity other) => Units.CompareTo(other.Units);

        /// <summary>상품 단위를 추측하지 않고 최소 단위 수만 표시한다.</summary>
        /// <returns>문화권과 관계없는 정수 표기.</returns>
        public override string ToString() => Units.ToString(CultureInfo.InvariantCulture);
    }
}
