// Money의 단위·경계·비교 계약을 검사한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md.
using System;
using Hora.Domain.Values;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>센트 정수 금액이 조용히 넘치거나 다른 단위로 바뀌지 않는지 확인한다.</summary>
    [TestFixture]
    public sealed class MoneyTests
    {
        /// <summary>달러에서 센트로 변환하는 과정의 범위 초과를 검출한다.</summary>
        [Test]
        public void FromDollars_Overflow_Throws()
        {
            // Arrange / Act / Assert
            Assert.Throws<OverflowException>(() => Money.FromDollars(long.MaxValue));
            Assert.Throws<OverflowException>(() => Money.FromDollars(long.MinValue));
        }

        /// <summary>달러 값은 센트 단위로 저장되고 합계 역시 센트 단위다.</summary>
        [Test]
        public void Add_TwoValues_SumsCents()
        {
            // Arrange
            Money first = Money.FromDollars(12);
            Money second = Money.FromCents(34);

            // Act
            Money sum = first + second;

            // Assert
            Assert.That(sum.Cents, Is.EqualTo(1234));
            Assert.That(sum.ToString(), Is.EqualTo("$12.34"));
        }

        /// <summary>모든 금액 산술 연산에서 범위 초과가 예외가 된다.</summary>
        [Test]
        public void Arithmetic_Overflow_Throws()
        {
            // Arrange
            Money minimum = Money.FromCents(long.MinValue);
            Money maximum = Money.FromCents(long.MaxValue);
            Money cent = Money.FromCents(1);

            // Act / Assert
            Assert.Throws<OverflowException>(() => _ = maximum + cent);
            Assert.Throws<OverflowException>(() => _ = minimum - cent);
            Assert.Throws<OverflowException>(() => _ = -minimum);
            Assert.Throws<OverflowException>(() => _ = maximum * 2);
            Assert.Throws<OverflowException>(() => _ = minimum * -1);
        }

        /// <summary>음수·0·양수의 정렬과 동등성은 센트 정수와 같다.</summary>
        [Test]
        public void Compare_NegativeZeroPositive_UsesCents()
        {
            // Arrange
            Money negative = Money.FromCents(-1);
            Money positive = Money.FromCents(1);

            // Act / Assert
            Assert.That(negative.IsNegative, Is.True);
            Assert.That(Money.Zero.IsNegative, Is.False);
            Assert.That(negative < Money.Zero && positive > Money.Zero, Is.True);
            Assert.That(negative <= -positive && positive >= -negative, Is.True);
            Assert.That(positive == Money.FromCents(1) && positive != negative, Is.True);
            Assert.That(positive.Equals((object)Money.FromCents(1)), Is.True);
            Assert.That(positive.Equals((object)1), Is.False);
            Assert.That(positive.GetHashCode(), Is.EqualTo(Money.FromCents(1).GetHashCode()));
            Assert.That(negative.CompareTo(positive), Is.LessThan(0));
        }
    }
}
