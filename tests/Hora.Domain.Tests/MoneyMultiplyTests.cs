// 큰 중간 곱과 음수 floor를 독립 BigInteger 계산과 비교한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md.
using System;
using System.Numerics;
using Hora.Domain.Values;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>단가와 수량 곱셈의 정밀도·부호·최종 범위를 검증한다.</summary>
    [TestFixture]
    public sealed class MoneyMultiplyTests
    {
        /// <summary>음수의 나머지는 0 방향이 아닌 음의 무한대 방향으로 내린다.</summary>
        /// <param name="price">가격 기준 단위 수에 대한 USD 센트 단가.</param>
        /// <param name="quantity">상품 최소 단위 수량.</param>
        /// <param name="unitsPerPrice">가격 기준 최소 단위 수.</param>
        /// <param name="expected">내림한 예상 USD 센트 금액.</param>
        [TestCase(-1, 1, 3, -1)]
        [TestCase(1, -1, 3, -1)]
        [TestCase(-1, -1, 3, 0)]
        [TestCase(-10, 3, 4, -8)]
        [TestCase(10, 3, 4, 7)]
        public void Multiply_NegativeResult_FloorsTowardNegativeInfinity(
            long price, long quantity, long unitsPerPrice, long expected)
        {
            // Arrange / Act
            Money result = MoneyMath.Multiply(Money.FromCents(price), Quantity.FromUnits(quantity), unitsPerPrice);

            // Assert
            Assert.That(result.Cents, Is.EqualTo(expected));
        }

        /// <summary>중간 곱이 64비트를 넘어도 최종 값이 유효하면 정확하게 계산한다.</summary>
        /// <param name="price">가격 기준 단위 수에 대한 USD 센트 단가.</param>
        /// <param name="quantity">상품 최소 단위 수량.</param>
        /// <param name="unitsPerPrice">가격 기준 최소 단위 수.</param>
        /// <param name="expected">최종 예상 USD 센트 금액.</param>
        [TestCase(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue)]
        [TestCase(long.MinValue, long.MaxValue, long.MaxValue, long.MinValue)]
        [TestCase(long.MinValue, 1, 1, long.MinValue)]
        [TestCase(long.MaxValue, 0, 1, 0)]
        public void Multiply_LargeIntermediate_PreservesFinalValue(
            long price, long quantity, long unitsPerPrice, long expected)
        {
            // Arrange / Act
            Money result = MoneyMath.Multiply(Money.FromCents(price), Quantity.FromUnits(quantity), unitsPerPrice);

            // Assert
            Assert.That(result.Cents, Is.EqualTo(expected));
        }

        /// <summary>잘못된 분모와 최종 금액의 범위 초과를 숨기지 않는다.</summary>
        [Test]
        public void Multiply_InvalidDivisorOrFinalOverflow_Throws()
        {
            // Arrange
            Money minimum = Money.FromCents(long.MinValue);

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => MoneyMath.Multiply(Money.Zero, Quantity.Zero, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MoneyMath.Multiply(Money.Zero, Quantity.Zero, -1));
            Assert.Throws<OverflowException>(() => MoneyMath.Multiply(minimum, Quantity.FromUnits(-1), 1));
            Assert.Throws<OverflowException>(() => MoneyMath.Multiply(minimum, Quantity.FromUnits(long.MinValue), 1));
        }

        /// <summary>10,000개 고정 시드 입력을 임의 정밀도 정수 oracle과 비교한다.</summary>
        [Test]
        public void Multiply_RandomInputs_MatchesIndependentIntegerOracle()
        {
            // Arrange
            var random = new System.Random(234);

            // Act / Assert
            for (int sample = 0; sample < 10_000; sample++)
            {
                long price = random.NextInt64(long.MinValue, long.MaxValue);
                long quantity = random.NextInt64(long.MinValue, long.MaxValue);
                long divisor = random.NextInt64(1, long.MaxValue);
                VerifyProduct(price, quantity, divisor);
            }
        }

        /// <summary>부호 있는 BigInteger의 정확한 곱에서 floor와 범위를 독립 계산한다.</summary>
        private static void VerifyProduct(long price, long quantity, long divisor)
        {
            BigInteger product = (BigInteger)price * quantity;
            BigInteger expected = BigInteger.DivRem(product, divisor, out BigInteger remainder);
            if (remainder.Sign < 0)
            {
                expected--;
            }

            if (expected < long.MinValue || expected > long.MaxValue)
            {
                Assert.Throws<OverflowException>(
                    () => MoneyMath.Multiply(Money.FromCents(price), Quantity.FromUnits(quantity), divisor));
            }
            else
            {
                Money result = MoneyMath.Multiply(Money.FromCents(price), Quantity.FromUnits(quantity), divisor);
                Assert.That(result.Cents, Is.EqualTo((long)expected));
            }
        }
    }
}
