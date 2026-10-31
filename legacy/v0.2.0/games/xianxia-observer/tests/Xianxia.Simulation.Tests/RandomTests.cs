// PCG32 결정론 난수 검사.
using System;
using NUnit.Framework;
using Xianxia.Simulation.Core;

namespace Xianxia.Simulation.Tests
{
    public sealed class RandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            Pcg32 first = new(42, 54);
            Pcg32 second = new(42, 54);
            for (int index = 0; index < 1000; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        [Test]
        public void KnownSeed_MatchesReferenceOutput()
        {
            // PCG 공식 예제(pcg32-demo, 시드 42·스트림 54)의 첫 출력값.
            Pcg32 random = new(42, 54);
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            foreach (uint value in expected)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(value));
            }
        }

        [Test]
        public void FromState_ContinuesSequence()
        {
            Pcg32 original = new(9, 3);
            original.NextUInt();
            Pcg32 restored = Pcg32.FromState(original.State, original.Increment);
            for (int index = 0; index < 100; index++)
            {
                Assert.That(restored.NextUInt(), Is.EqualTo(original.NextUInt()));
            }
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            Pcg32 random = new(1, 1);
            for (int index = 0; index < 10000; index++)
            {
                int value = random.NextInt(7);
                Assert.That(value, Is.InRange(0, 6));
            }
        }

        [Test]
        public void Range_IncludesBothEnds()
        {
            Pcg32 random = new(3, 3);
            bool sawMin = false;
            bool sawMax = false;
            for (int index = 0; index < 2000; index++)
            {
                int value = random.Range(2, 4);
                sawMin |= value == 2;
                sawMax |= value == 4;
                Assert.That(value, Is.InRange(2, 4));
            }

            Assert.That(sawMin && sawMax, Is.True);
        }

        [Test]
        public void NextInt_WithZeroBound_Throws()
        {
            Pcg32 random = new(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
        }

        [Test]
        public void PickWeighted_NeverPicksZeroWeight()
        {
            Pcg32 random = new(5, 5);
            ReadOnlySpan<double> weights = stackalloc double[] { 0, 1, 0, 3 };
            for (int index = 0; index < 1000; index++)
            {
                int picked = random.PickWeighted(weights);
                Assert.That(picked == 1 || picked == 3, Is.True);
            }
        }
    }
}
