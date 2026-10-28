// 상품 테이블의 행 인덱스를 다른 엔티티 종류와 구분한다.
// 관련 기획: docs/tasks/M0-03-ids-random.md, docs/redesign/02-game-design.md §7.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Hora.Domain.Ids
{
    /// <summary>
    /// 상품 식별자. 기본값은 행 0이며, 미지정 상태에는 <see cref="None"/>을 사용한다.
    /// </summary>
    public readonly struct ProductId : IEquatable<ProductId>, IComparable<ProductId>
    {
        /// <summary>참조할 상품가 없음을 나타내는 값(-1).</summary>
        public static readonly ProductId None = new ProductId(-1);

        /// <summary>상품 테이블의 행 인덱스(0 이상), 또는 미지정 표시(-1).</summary>
        [SuppressMessage("Design", "CA1051:Do not declare visible instance fields",
            Justification = "M0-03의 공개 readonly 값 필드 계약을 유지한다.")]
        public readonly int Value;

        /// <summary>테이블 행 인덱스로 상품 식별자를 만든다.</summary>
        /// <param name="value">행 인덱스(0 이상) 또는 -1.</param>
        /// <exception cref="ArgumentOutOfRangeException">값이 -1보다 작음.</exception>
        public ProductId(int value)
        {
            if (value < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        /// <summary>행 인덱스로 사용할 수 있는지 여부. 실제 테이블 길이는 별도로 검사한다.</summary>
        public bool IsValid => Value >= 0;

        /// <summary>두 상품 식별자의 행 인덱스가 같은지 비교한다.</summary>
        /// <param name="left">왼쪽 식별자.</param>
        /// <param name="right">오른쪽 식별자.</param>
        /// <returns>행 인덱스가 같으면 true.</returns>
        public static bool operator ==(ProductId left, ProductId right) => left.Equals(right);

        /// <summary>두 상품 식별자의 행 인덱스가 다른지 비교한다.</summary>
        /// <param name="left">왼쪽 식별자.</param>
        /// <param name="right">오른쪽 식별자.</param>
        /// <returns>행 인덱스가 다르면 true.</returns>
        public static bool operator !=(ProductId left, ProductId right) => !left.Equals(right);

        /// <summary>왼쪽 식별자의 행 인덱스가 더 작은지 비교한다.</summary>
        /// <param name="left">왼쪽 식별자.</param>
        /// <param name="right">오른쪽 식별자.</param>
        /// <returns>왼쪽 행 인덱스가 작으면 true.</returns>
        public static bool operator <(ProductId left, ProductId right) => left.Value < right.Value;

        /// <summary>왼쪽 식별자의 행 인덱스가 더 큰지 비교한다.</summary>
        /// <param name="left">왼쪽 식별자.</param>
        /// <param name="right">오른쪽 식별자.</param>
        /// <returns>왼쪽 행 인덱스가 크면 true.</returns>
        public static bool operator >(ProductId left, ProductId right) => left.Value > right.Value;

        /// <summary>왼쪽 식별자의 행 인덱스가 작거나 같은지 비교한다.</summary>
        /// <param name="left">왼쪽 식별자.</param>
        /// <param name="right">오른쪽 식별자.</param>
        /// <returns>왼쪽 행 인덱스가 작거나 같으면 true.</returns>
        public static bool operator <=(ProductId left, ProductId right) => left.Value <= right.Value;

        /// <summary>왼쪽 식별자의 행 인덱스가 크거나 같은지 비교한다.</summary>
        /// <param name="left">왼쪽 식별자.</param>
        /// <param name="right">오른쪽 식별자.</param>
        /// <returns>왼쪽 행 인덱스가 크거나 같으면 true.</returns>
        public static bool operator >=(ProductId left, ProductId right) => left.Value >= right.Value;

        /// <summary>같은 종류의 식별자와 행 인덱스를 비교한다.</summary>
        /// <param name="other">비교할 상품 식별자.</param>
        /// <returns>동일한 행 인덱스이면 true.</returns>
        public bool Equals(ProductId other) => Value == other.Value;

        /// <summary>같은 종류·행 인덱스인 객체에만 동등성을 인정한다.</summary>
        /// <param name="obj">비교할 객체 또는 null.</param>
        /// <returns>같은 종류·행 인덱스이면 true.</returns>
        public override bool Equals(object? obj) => obj is ProductId other && Equals(other);

        /// <summary>행 인덱스로부터 결정론적 해시를 얻는다.</summary>
        /// <returns>행 인덱스와 같은 해시 값.</returns>
        public override int GetHashCode() => Value;

        /// <summary>다른 상품 식별자와 행 순서를 비교한다.</summary>
        /// <param name="other">비교할 식별자.</param>
        /// <returns>작으면 음수, 같으면 0, 크면 양수.</returns>
        public int CompareTo(ProductId other) => Value.CompareTo(other.Value);

        /// <summary>종류와 행 인덱스를 문화권에 무관한 디버그 문자열로 표시한다.</summary>
        /// <returns>Product#3 형태의 문자열.</returns>
        public override string ToString() => "Product#" + Value.ToString(CultureInfo.InvariantCulture);
    }
}
