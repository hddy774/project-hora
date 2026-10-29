// 천기1회가 다음 소경지까지 걸리는 시간을 얼마나 줄이는지 같은 시드의 수련 대조군으로 측정한다.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>깨달음·부상 차이를 통제한 다음 단계 도달 시간 대조.</summary>
    public sealed class FortuneTimingTests
    {
        private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };
        /// <summary>영기0·건강/도심/깨달음 최대에서 경지0/4/6/8×10시드를 측정한다.</summary>
        [Test]
        public void Fortune_OneUseReducesNextStageTime()
        {
            List<Timing> samples = new();
            foreach (int realm in new[] { 0, 4, 6, 8 })
            {
                for (ulong seed = 1; seed <= 10; seed++)
                {
                    World original = WorldFactory.Create(new WorldSettings { Seed = seed, Population = 100 });
                    Cultivator person = original.Cultivators[0];
                    person.Realm = realm;
                    person.HighestRealm = realm;
                    person.Stage = 0;
                    person.Qi = 0;
                    person.Insight = 1000;
                    person.DaoHeart = 100;
                    person.Injury = 0;
                    person.RecoveryYears = 0;
                    World favored = WorldSerializer.FromBytes(WorldSerializer.ToBytes(original));
                    Assert.That(InterventionSystem.BestowFortune(favored, favored.Cultivators[0]), Is.True);
                    int baseline = YearsToNextStage(original);
                    int withFortune = YearsToNextStage(favored);
                    samples.Add(new Timing(seed, realm, baseline, withFortune, 1 - ((double)withFortune / baseline)));
                }
            }

            foreach (IGrouping<int, Timing> group in samples.GroupBy(sample => sample.Realm))
            {
                double[] ordered = group.Select(sample => sample.Reduction).Order().ToArray();
                Assert.That((ordered[4] + ordered[5]) / 2, Is.InRange(.30, .70), "경지 " + group.Key);
            }

            File.WriteAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, "fortune-timing.json"),
                JsonSerializer.Serialize(samples, s_json));
        }

        private static int YearsToNextStage(World world)
        {
            Cultivator person = world.Cultivators[0];
            for (int year = 1; year <= 1000000; year++)
            {
                world.Year++;
                ActionEffects.Cultivate(world, person, 1);
                BreakthroughSystem.TryAdvance(world, person);
                if (person.Stage > 0)
                {
                    return year;
                }
            }

            Assert.Fail("대조 실행에서 다음 단계에 도달하지 못했습니다.");
            return 0;
        }

        /// <summary>한 쌍의 원시 연수와 단축 비율.</summary>
        /// <param name="Seed">시드.</param>
        /// <param name="Realm">경지.</param>
        /// <param name="BaselineYears">개입 없는 연수.</param>
        /// <param name="FortuneYears">기연1회 뒤 연수.</param>
        /// <param name="Reduction">단축 비율.</param>
        private sealed record Timing(ulong Seed, int Realm, int BaselineYears, int FortuneYears, double Reduction);
    }
}
