// 보상 상한보다 좋은 공법을 가진 인물의 하락 회귀와 기존 난수 계약을 검사한다.
using System;
using NUnit.Framework;
using Xianxia.Simulation.Core;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>공법 보상 단조성과 사건 기록 검사.</summary>
    public sealed class RewardTests
    {
        /// <summary>모든 등급·증가량·두 보상 상한의 조합을 검사한다.</summary>
        [Test]
        public void ImproveByReward_NeverLowersGrade()
        {
            for (int current = 0; current <= 19; current++)
            {
                for (int gain = 1; gain <= 2; gain++)
                {
                    foreach (int cap in new[] { 15, 18 })
                    {
                        int result = Techniques.ImproveByReward(current, gain, cap);
                        Assert.That(result, Is.GreaterThanOrEqualTo(current));
                        Assert.That(result, Is.EqualTo(current >= cap ? current : Math.Min(cap, current + gain)));
                    }
                }
            }

            Assert.That(Techniques.ImproveByReward(14, 2, 15), Is.EqualTo(15));
            Assert.That(Techniques.ImproveByReward(1, int.MaxValue, 18), Is.EqualTo(18));
        }

        /// <summary>비정상 보상 입력은 명시적으로 거부한다.</summary>
        [TestCase(0, 15)]
        [TestCase(-1, 15)]
        [TestCase(1, -1)]
        [TestCase(1, 20)]
        public void ImproveByReward_RejectsInvalidInput(int gain, int cap)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Techniques.ImproveByReward(1, gain, cap));
        }

        /// <summary>기연 공법 분기를 실제로 통과시켜 세계 난수 두 번 소비와 기존 속성을 확인한다.</summary>
        [TestCase(16)]
        [TestCase(17)]
        [TestCase(18)]
        public void Fortune_KeepsHigherGradeAndRandomCalls(int grade)
        {
            World world = TestWorlds.Create(population: 100);
            Cultivator person = world.Cultivators[0];
            person.TechniqueGrade = grade;
            Element element = person.TechniqueElement;
            Pcg32 expected = Pcg32.FromState(world.Random.State, world.Random.Increment);
            while (expected.NextDouble() >= 0.18)
            {
                world.Random.NextDouble();
                expected = Pcg32.FromState(world.Random.State, world.Random.Increment);
            }

            expected.Range(1, 2);
            ActionEffects.Fortune(world, person, "검사 중");
            Assert.That(world.Random.State, Is.EqualTo(expected.State));
            Assert.That(person.TechniqueGrade, Is.EqualTo(grade));
            Assert.That(person.TechniqueElement, Is.EqualTo(element));
            Assert.That(world.Log.Entries[^1].Text, Does.Contain("새로 깨친 것이 없었다"));
        }

        /// <summary>비경 공법 보물 분기의 점수·난수·기록을 실제 호출로 확인한다.</summary>
        [Test]
        public void SecretRealm_TechniqueTreasureKeepsHigherGrade()
        {
            World world = TestWorlds.Create(population: 100);
            Cultivator person = world.Cultivators[0];
            person.TechniqueGrade = 19;
            world.SetFollowed(person.Id, true);
            Pcg32 expected = Pcg32.FromState(world.Random.State, world.Random.Increment);
            while (expected.NextInt(5) != 0)
            {
                world.Random.NextUInt();
                expected = Pcg32.FromState(world.Random.State, world.Random.Increment);
            }

            Assert.That(SecretRealmSystem.Treasure(world, person), Is.EqualTo(3));
            Assert.That(person.TechniqueGrade, Is.EqualTo(19));
            Assert.That(world.Random.State, Is.EqualTo(expected.State));
            Assert.That(world.Log.Entries[^1].Text, Does.Contain("(이미 익힌) 신품 공법"));
        }
    }
}
