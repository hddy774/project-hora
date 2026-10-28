// 정수 전체 범위, 거부 샘플링, 잘못된 확률과 할당 없는 반복 호출을 검증한다.
// 관련 기획: docs/tasks/M0-03-ids-random.md.
using System;
using Hora.Domain.Random;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>편향 없는 난수 구간과 상태 전이의 경계 조건을 검증한다.</summary>
    public sealed class RandomBoundaryTests
    {
        /// <summary>비어 있거나 역전된 구간은 상태를 소비하기 전에 차단한다.</summary>
        /// <param name="minimum">구간 하한.</param>
        /// <param name="maximum">구간 상한.</param>
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(int.MaxValue, int.MinValue)]
        public void NextInt_MinNotLessThanMax_Throws(int minimum, int maximum)
        {
            // Arrange
            var random = new DeterministicRandom(42, 54);
            DeterministicRandom copy = random;

            // Act / Assert
            Assert.Throws<ArgumentException>(() => random.NextInt(minimum, maximum));
            Assert.That(random.NextUInt(), Is.EqualTo(copy.NextUInt()));
        }

        /// <summary>폭 계산이 int 범위를 넘는 경우에도 양·음수를 포함한 유효 구간만 반환한다.</summary>
        [Test]
        public void NextInt_FullSignedRange_DoesNotOverflow()
        {
            // Arrange
            var random = new DeterministicRandom(42, 54);
            bool hasNegative = false;
            bool hasPositive = false;

            // Act / Assert
            for (int index = 0; index < 1000; index++)
            {
                int value = random.NextInt(int.MinValue, int.MaxValue);
                Assert.That(value, Is.LessThan(int.MaxValue));
                hasNegative |= value < 0;
                hasPositive |= value > 0;
            }

            Assert.That(hasNegative && hasPositive, Is.True);
        }

        /// <summary>경계의 원소 한 개짜리 구간은 해당 원소만 반환한다.</summary>
        /// <param name="minimum">구간 하한.</param>
        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(int.MaxValue - 1)]
        public void NextInt_WidthOne_ReturnsMinimum(int minimum)
        {
            // Arrange
            var random = new DeterministicRandom(42, 54);

            // Act
            int value = random.NextInt(minimum, minimum + 1);

            // Assert
            Assert.That(value, Is.EqualTo(minimum));
        }

        /// <summary>거부율이 큰 구간도 공식 C의 boundedrand와 같은 표본을 선택한다.</summary>
        [Test]
        public void NextInt_RejectionRange_MatchesReferenceOutput()
        {
            // Arrange
            // pcg-c-basic-0.9의 seed=42, stream=54, bound=2147483649; 각 값에 int.MinValue를 더함.
            // 출처: https://www.pcg-random.org/downloads/pcg-c-basic-0.9.zip
            int[] expected =
            {
                -1587805514, -1172491473, -2083327342, -1079740342, -873635731,
                -1077501012, -2127560852, -434163623, -113751153, -310876123,
            };
            var random = new DeterministicRandom(42, 54);

            // Act / Assert
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(random.NextInt(int.MinValue, 1), Is.EqualTo(expected[index]));
            }
        }

        /// <summary>분모와 분자 범위를 벗어난 확률은 난수 상태를 바꾸지 않는다.</summary>
        /// <param name="numerator">성공 분자.</param>
        /// <param name="denominator">양수여야 하는 분모.</param>
        [TestCase(0, 0)]
        [TestCase(0, -1)]
        [TestCase(-1, 3)]
        [TestCase(4, 3)]
        public void NextChance_InvalidFraction_Throws(int numerator, int denominator)
        {
            // Arrange
            var random = new DeterministicRandom(42, 54);
            DeterministicRandom copy = random;

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextChance(numerator, denominator));
            Assert.That(random.NextUInt(), Is.EqualTo(copy.NextUInt()));
        }

        /// <summary>확률의 끝점은 고정 결과를 내고 다음 난수를 그대로 보존한다.</summary>
        [Test]
        public void NextChance_CertainOutcomes_DoesNotAdvanceState()
        {
            // Arrange
            var random = new DeterministicRandom(42, 54);
            DeterministicRandom copy = random;

            // Act / Assert
            Assert.That(random.NextChance(0, 3), Is.False);
            Assert.That(random.NextChance(3, 3), Is.True);
            Assert.That(random.NextChance(int.MaxValue, int.MaxValue), Is.True);
            Assert.That(random.NextUInt(), Is.EqualTo(copy.NextUInt()));
        }

        /// <summary>분수 확률은 같은 스트림의 편향 없는 정수 표본과 대응한다.</summary>
        [Test]
        public void NextChance_InteriorFraction_UsesBoundedSample()
        {
            // Arrange
            var chance = new DeterministicRandom(42, 54);
            DeterministicRandom bounded = chance;

            // Act / Assert
            for (int index = 0; index < 100; index++)
            {
                Assert.That(chance.NextChance(2, 7), Is.EqualTo(bounded.NextInt(0, 7) < 2));
            }
        }

        /// <summary>생성자를 거치지 않은 상태가 조용히 영의 수열을 생성하지 않게 한다.</summary>
        [Test]
        public void Random_DefaultValue_RequiresInitialization()
        {
            // Arrange
            var random = default(DeterministicRandom);

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => random.NextUInt());
        }

        /// <summary>정상적인 난수 반복 호출은 관리 힙을 할당하지 않는다.</summary>
        [Test]
        public void Random_RepeatedCalls_AllocateZeroBytes()
        {
            // Arrange
            var random = new DeterministicRandom(42, 54);
            ConsumeSamples(ref random, 100);
            long before = GC.GetAllocatedBytesForCurrentThread();

            // Act
            uint checksum = ConsumeSamples(ref random, 100000);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.That(allocated, Is.Zero);
            TestContext.WriteLine("난수 할당: " + allocated + " B; checksum: " + checksum);
        }

        /// <summary>측정 구간 안에서 테스트 프레임워크 호출이나 문자열 생성을 피한다.</summary>
        private static uint ConsumeSamples(ref DeterministicRandom random, int count)
        {
            uint checksum = 0;
            for (int index = 0; index < count; index++)
            {
                checksum ^= random.NextUInt();
                checksum ^= unchecked((uint)random.NextInt(int.MinValue, int.MaxValue));
            }

            return checksum;
        }
    }
}
