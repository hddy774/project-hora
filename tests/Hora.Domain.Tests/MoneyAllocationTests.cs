// 최대 나머지 배분의 센트 보존·동률 순서·음수 경계를 검증한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md.
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Hora.Domain.Values;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>분배된 총액을 독립 임의 정밀도 정수 계산과 대조한다.</summary>
    [TestFixture]
    public sealed class MoneyAllocationTests
    {
        /// <summary>나머지가 같으면 앞 인덱스부터 1센트를 배분한다.</summary>
        [Test]
        public void Allocate_ThreeEqualWeights_RemainderGoesToLowestIndex()
        {
            // Arrange
            long[] weights = { 1, 1, 1 };
            var output = new Money[weights.Length];

            // Act
            MoneyMath.Allocate(Money.FromCents(100), weights, output);

            // Assert
            Assert.That(output, Is.EqualTo(new[]
            {
                Money.FromCents(34), Money.FromCents(33), Money.FromCents(33),
            }));
        }

        /// <summary>음수 최소 금액과 signed 범위를 넘는 가중치 합을 함께 처리한다.</summary>
        /// <param name="total">보존할 총액, USD 센트.</param>
        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        [TestCase(-100)]
        [TestCase(0)]
        [TestCase(1)]
        public void Allocate_ExtremeTotalAndWeights_MatchesOracle(long total)
        {
            // Arrange
            long[][] weightSets =
            {
                new long[] { 1, 1, 1 },
                new long[] { long.MaxValue, long.MaxValue, long.MaxValue, 0 },
                new long[] { 0, 1, 0 },
                new long[] { long.MaxValue, 1, long.MaxValue - 1, 19 },
            };

            // Act / Assert
            foreach (long[] weights in weightSets)
            {
                VerifyAllocation(total, weights);
            }
        }

        /// <summary>10,000회 고정 시드 무작위 배분에서 개별 몫과 합계 모두 정확하다.</summary>
        [Test]
        public void Allocate_RandomInputs_SumEqualsTotal()
        {
            // Arrange
            var random = new System.Random(711);

            // Act / Assert
            for (int sample = 0; sample < 10_000; sample++)
            {
                long[] weights = new long[random.Next(1, 13)];
                for (int index = 0; index < weights.Length; index++)
                {
                    weights[index] = random.NextInt64(1, long.MaxValue);
                }

                long total = random.NextInt64(long.MinValue, long.MaxValue);
                VerifyAllocation(total, weights);
            }
        }

        /// <summary>0 가중치 합·음수 가중치·길이 불일치는 출력 변경 전에 거부한다.</summary>
        [Test]
        public void Allocate_ZeroWeightSumOrInvalidBuffer_ThrowsWithoutWriting()
        {
            // Arrange
            Money total = Money.FromCents(12);
            Money[] output = { total, total };

            // Act / Assert
            Assert.Throws<ArgumentException>(() => MoneyMath.Allocate(total, new long[] { 0, 0 }, output));
            Assert.Throws<ArgumentException>(() => MoneyMath.Allocate(total, new long[] { 1, -1 }, output));
            Assert.Throws<ArgumentException>(() => MoneyMath.Allocate(total, new long[] { 1, 1, 1 }, output));
            Assert.Throws<ArgumentException>(() => MoneyMath.Allocate(total, new long[] { 1 }, output));
            Assert.Throws<ArgumentException>(
                () => MoneyMath.Allocate(total, Array.Empty<long>(), Array.Empty<Money>()));
            Assert.That(output, Is.EqualTo(new[] { total, total }));
        }

        /// <summary>같은 메모리를 재사용한 입출력은 가중치 파괴 전에 거부한다.</summary>
        [Test]
        public void Allocate_AliasedInputOutput_ThrowsWithoutWriting()
        {
            // Arrange
            long[] weights = { 1, 2, 3 };

            // Act / Assert
            Assert.Throws<ArgumentException>(() => AllocateAliased(weights));
            Assert.That(weights, Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        /// <summary>워밍업 후 곱셈과 배분은 매번 관리 힙 객체를 만들지 않는다.</summary>
        [Test]
        public void MultiplyAndAllocate_ReusedBuffers_AllocateZeroBytes()
        {
            // Arrange
            long[] weights = { 1, 2, 3, 4 };
            var output = new Money[weights.Length];
            RunArithmetic(weights, output);
            long before = GC.GetAllocatedBytesForCurrentThread();

            // Act
            RunArithmetic(weights, output);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.That(bytes, Is.Zero);
            Assert.That(output[0].Cents, Is.LessThan(0));
        }

        private static void AllocateAliased(long[] weights)
        {
            Span<Money> output = MemoryMarshal.Cast<long, Money>(weights);
            MoneyMath.Allocate(Money.FromCents(8), weights, output);
        }

        private static void RunArithmetic(long[] weights, Money[] output)
        {
            for (int iteration = 0; iteration < 1000; iteration++)
            {
                Money product = MoneyMath.Multiply(Money.FromCents(-1234567), Quantity.FromUnits(321), 1000);
                MoneyMath.Allocate(product, weights, output);
            }
        }

        /// <summary>개별 몫과 합계를 각각 검사해 보존만 맞는 잘못된 배분도 찾는다.</summary>
        private static void VerifyAllocation(long total, long[] weights)
        {
            var output = new Money[weights.Length];
            long[] expected = MoneyAllocationOracle.Calculate(total, weights);
            MoneyMath.Allocate(Money.FromCents(total), weights, output);
            BigInteger sum = BigInteger.Zero;
            for (int index = 0; index < output.Length; index++)
            {
                Assert.That(output[index].Cents, Is.EqualTo(expected[index]));
                sum += output[index].Cents;
            }

            Assert.That(sum, Is.EqualTo(new BigInteger(total)));
        }
    }
}
