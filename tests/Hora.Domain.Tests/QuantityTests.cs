// 상품의 최소 단위 수량이 정수로 보존되는지 검사한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md.
using System;
using Hora.Domain.Values;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>수량 값 타입의 산술·정렬·범위 초과 계약을 검증한다.</summary>
    [TestFixture]
    public sealed class QuantityTests
    {
        /// <summary>최소 단위 더하기·빼기가 원래 수량을 복원한다.</summary>
        [Test]
        public void Arithmetic_MinimalUnits_PreservesQuantity()
        {
            // Arrange
            Quantity first = Quantity.FromUnits(1234);
            Quantity second = Quantity.FromUnits(-34);

            // Act
            Quantity sum = first + second;

            // Assert
            Assert.That(sum.Units, Is.EqualTo(1200));
            Assert.That(sum - second, Is.EqualTo(first));
            Assert.That(Quantity.Zero.Units, Is.Zero);
            Assert.That(sum.ToString(), Is.EqualTo("1200"));
        }

        /// <summary>더하기와 빼기는 범위를 넘을 때 순환하지 않는다.</summary>
        [Test]
        public void Arithmetic_Overflow_Throws()
        {
            // Arrange
            Quantity one = Quantity.FromUnits(1);

            // Act / Assert
            Assert.Throws<OverflowException>(() => _ = Quantity.FromUnits(long.MaxValue) + one);
            Assert.Throws<OverflowException>(() => _ = Quantity.FromUnits(long.MinValue) - one);
        }

        /// <summary>정렬과 동등성은 최소 단위의 signed 정수 순서를 따른다.</summary>
        [Test]
        public void Compare_MinimalUnits_OrdersCorrectly()
        {
            // Arrange
            Quantity first = Quantity.FromUnits(-3);
            Quantity same = Quantity.FromUnits(-3);
            Quantity second = Quantity.FromUnits(5);

            // Act / Assert
            Assert.That(first == same && first != second, Is.True);
            Assert.That(first < second && second > first, Is.True);
            Assert.That(first <= same && same >= first, Is.True);
            Assert.That(first.Equals((object)same), Is.True);
            Assert.That(first.Equals((object)Money.FromCents(-3)), Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first.CompareTo(second), Is.LessThan(0));
        }
    }
}
