// 진단이 세계 미래·저장 바이트를 바꾸지 않으며 실제 경로를 빠짐없이 집계하는지 검증한다.
using System;
using NUnit.Framework;
using Xianxia.Simulation.Core;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>선택적 경지·영기 진단의 관찰 무영향 회귀.</summary>
    public sealed class DiagnosticsTests
    {
        /// <summary>시드1·1000명의1000년 미래와 두 난수 상태가 켬/끔에서 완전히 같다.</summary>
        [Test]
        public void Diagnostics_DoNotChangeSimulation()
        {
            World control = TestWorlds.Create(seed: 1, population: 1000);
            World observed = TestWorlds.Create(seed: 1, population: 1000);
            observed.Diagnostics = new WorldDiagnostics(observed);
            WorldSimulator.Run(control, 1000);
            WorldSimulator.Run(observed, 1000);
            Assert.That(WorldSerializer.ToBytes(observed), Is.EqualTo(WorldSerializer.ToBytes(control)));
            Assert.That(observed.Random.State, Is.EqualTo(control.Random.State));
            Assert.That(observed.LogRandom.State, Is.EqualTo(control.LogRandom.State));
            Assert.That(WorldSerializer.FromBytes(WorldSerializer.ToBytes(observed)).Diagnostics, Is.Null);
        }

        /// <summary>초기 구간·상승·하락·생 종료를 중복 없이 닫는다.</summary>
        [Test]
        public void Dwell_ClosesVisitsOnRiseFallAndDeath()
        {
            World world = TestWorlds.Create(population: 100);
            Cultivator person = world.Cultivators[0];
            person.Realm = 0;
            person.Stage = 0;
            WorldDiagnostics diagnostics = new(world);
            world.Diagnostics = diagnostics;
            world.Year = 10;
            person.Realm = 1;
            RecordSystem.NoteProgress(world, person);
            world.Year = 30;
            Setbacks.DropRealm(world, person);
            world.Year = 45;
            RecordSystem.EndLife(world, person, "검사");
            Assert.That(diagnostics.DwellYears(0), Is.EqualTo(25));
            Assert.That(diagnostics.DwellSamples(0), Is.EqualTo(2));
            Assert.That(diagnostics.DwellYears(1), Is.EqualTo(20));
            Assert.That(diagnostics.DwellSamples(1), Is.EqualTo(1));
            Assert.That(diagnostics.DownwardExits(1), Is.EqualTo(1));
        }

        /// <summary>다섯 영기 증가 경로를 실제 사건에서 각각 집계한다.</summary>
        [Test]
        public void QiSources_RecordAllFiveActualPaths()
        {
            World world = TestWorlds.Create(population: 100);
            Cultivator person = world.Cultivators[0];
            person.Realm = 6;
            person.Stage = 0;
            person.Qi = 0;
            WorldDiagnostics diagnostics = new(world);
            world.Diagnostics = diagnostics;
            ActionEffects.Cultivate(world, person, 0.5);
            SeekRoll(world, random =>
            {
                double roll = random.NextDouble();
                return roll >= 0.52 && roll < 0.72;
            });
            ActionEffects.Fortune(world, person, "검사");
            SeekRoll(world, random => random.NextInt(5) == 3);
            SecretRealmSystem.Treasure(world, person);
            world.HeavenlyFavor = 10;
            Assert.That(InterventionSystem.BestowFortune(world, person), Is.True);
            Cultivator master = world.Cultivators[1];
            master.RegionId = person.RegionId;
            master.DiscipleIds.Clear();
            master.DiscipleIds.Add(person.Id);
            RelationshipSystem.Teach(world, master);
            foreach (QiSource source in Enum.GetValues<QiSource>())
            {
                Assert.That(diagnostics.QiGained(6, source), Is.GreaterThan(0), source.ToString());
            }
        }

        private static void SeekRoll(World world, Func<Pcg32, bool> accepts)
        {
            while (!accepts(Pcg32.FromState(world.Random.State, world.Random.Increment)))
            {
                world.Random.NextUInt();
            }
        }
    }
}
