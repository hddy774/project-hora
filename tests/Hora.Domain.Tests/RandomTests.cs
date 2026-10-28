// PCG32 공식 참조 수열, 독립 스트림과 고정 시드 분포를 검증한다.
// 관련 기획: docs/tasks/M0-03-ids-random.md.
using Hora.Domain.Random;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>난수 알고리즘이 실행 환경과 무관하게 재현되는지 검증한다.</summary>
    public sealed class RandomTests
    {
        /// <summary>공식 C 구현을 별도로 컴파일한 첫 열 개 출력을 고정 기준으로 사용한다.</summary>
        [Test]
        public void Random_KnownSeed_MatchesReferenceOutput()
        {
            // Arrange
            // 출처: https://www.pcg-random.org/downloads/pcg-c-basic-0.9.zip
            // pcg_basic.c의 pcg32_srandom_r(&state, 42u, 54u) 후 pcg32_random_r 10회.
            // 공식 ZIP SHA-256: 143f9cc0edc7e81064bde5ed6c5f16cbf7ca2f373d35543286f5fe4475a2eff1
            uint[] expected =
            {
                0xa15c02b7U, 0x7b47f409U, 0xba1d3330U, 0x83d2f293U, 0xbfa4784bU,
                0xcbed606eU, 0xbfc6a3adU, 0x812fff6dU, 0xe61f305aU, 0xf9384b90U,
            };
            var random = new DeterministicRandom(42, 54);

            // Act / Assert
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(expected[index]), "참조 수열 인덱스: " + index);
            }
        }

        /// <summary>같은 시드와 스트림은 오버플로 순환을 포함하여 같은 출력을 낸다.</summary>
        [Test]
        public void Random_SameSeedAndStream_ProducesSameSequence()
        {
            // Arrange
            var first = new DeterministicRandom(ulong.MaxValue, ulong.MaxValue);
            var second = new DeterministicRandom(ulong.MaxValue, ulong.MaxValue);

            // Act / Assert
            for (int index = 0; index < 1000; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        /// <summary>같은 시드라도 PCG 스트림 번호가 다르면 다른 수열로 전진한다.</summary>
        [Test]
        public void Random_DifferentStream_ProducesDifferentSequence()
        {
            // Arrange
            var first = new DeterministicRandom(42, 0);
            var second = new DeterministicRandom(42, 1);
            bool differs = false;

            // Act
            for (int index = 0; index < 20; index++)
            {
                differs |= first.NextUInt() != second.NextUInt();
            }

            // Assert
            Assert.That(differs, Is.True);
        }

        /// <summary>PCG의 스트림 선택자는 공식 규칙대로 하위 63비트만 사용한다.</summary>
        [Test]
        public void Random_StreamHighBit_IsIgnoredByReferenceSemantics()
        {
            // Arrange
            var first = new DeterministicRandom(42, 54);
            var second = new DeterministicRandom(42, (1UL << 63) | 54);

            // Act / Assert
            for (int index = 0; index < 20; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        /// <summary>주사위 구간의 고정 시드 표본 60만 개가 허용 오차 1%를 만족한다.</summary>
        [Test]
        public void NextInt_Distribution_IsUniformWithinOnePercent()
        {
            // Arrange
            const int sampleCount = 600000;
            const int expectedCount = sampleCount / 6;
            const int tolerance = expectedCount / 100;
            int[] counts = new int[6];
            var random = new DeterministicRandom(42, 54);

            // Act
            for (int index = 0; index < sampleCount; index++)
            {
                counts[random.NextInt(0, 6)]++;
            }

            // Assert
            Assert.That(counts, Is.All.InRange(expectedCount - tolerance, expectedCount + tolerance));
            TestContext.WriteLine("0..5 빈도: " + string.Join(", ", counts));
        }

        /// <summary>파생 스트림 생성은 부모의 다음 난수를 바꾸지 않는다.</summary>
        [Test]
        public void Fork_DoesNotChangeParentState()
        {
            // Arrange
            var parent = new DeterministicRandom(42, 54);
            parent.NextUInt();
            DeterministicRandom copy = parent;

            // Act
            DeterministicRandom fork = parent.Fork(13);
            fork.NextUInt();

            // Assert
            Assert.That(parent.NextUInt(), Is.EqualTo(copy.NextUInt()));
        }

        /// <summary>부모 수열을 소비한 횟수와 무관하게 원래 시드에서 파생한다.</summary>
        [Test]
        public void Fork_AfterParentAdvances_MatchesOriginalSeedAndStream()
        {
            // Arrange
            var parent = new DeterministicRandom(42, 54);
            DeterministicRandom early = parent.Fork(13);
            parent.NextUInt();
            parent.NextUInt();
            var expected = new DeterministicRandom(42, 13);

            // Act
            DeterministicRandom late = parent.Fork(13);

            // Assert
            for (int index = 0; index < 20; index++)
            {
                uint value = expected.NextUInt();
                Assert.That(early.NextUInt(), Is.EqualTo(value));
                Assert.That(late.NextUInt(), Is.EqualTo(value));
            }
        }
    }
}
