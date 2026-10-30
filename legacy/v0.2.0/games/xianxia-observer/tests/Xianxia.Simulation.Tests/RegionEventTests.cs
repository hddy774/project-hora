// 비경·수조의 시작·종료 순서와 잘못된 개입에 따른 천기 유실을 재현한다.
using NUnit.Framework;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>활성 사건 재계산과 코어 입력 검증 회귀.</summary>
    public sealed class RegionEventTests
    {
        /// <summary>두 시작 순서와 두 종료 종류에서 잔존 사건이 유지돼야 한다.</summary>
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EventExpiry_KeepsRemainingModifier(bool secretFirst, bool finishSecret)
        {
            World world = TestWorlds.Create(population: 100);
            if (secretFirst)
            {
                SecretRealmSystem.Open(world, 8, false);
                BeastTideSystem.Start(world, 8, false);
            }
            else
            {
                BeastTideSystem.Start(world, 8, false);
                SecretRealmSystem.Open(world, 8, false);
            }

            Assert.That(world.Regions[8].AuraModifier, Is.EqualTo(0.96).Within(1e-12));
            if (finishSecret)
            {
                world.SecretRealms[0].CloseYear = world.Year;
                SecretRealmSystem.Update(world);
            }
            else
            {
                world.BeastTides[0].EndYear = world.Year;
                BeastTideSystem.Update(world);
            }

            Assert.That(world.Regions[8].AuraModifier, Is.EqualTo(finishSecret ? 0.8 : 1.2));
            Assert.That(world.Regions[8].Status, Does.StartWith(finishSecret ? "수조: " : "비경 개방: "));
        }

        /// <summary>같은 종류 두 개는 한 번만 곱하며 앞 사건의 이름을 사용한다.</summary>
        [Test]
        public void Refresh_UsesOldestAndResetsWhenAllEventsEnd()
        {
            World world = TestWorlds.Create(population: 100);
            BeastTideState first = BeastTideSystem.Start(world, 8, false);
            BeastTideSystem.Start(world, 8, false);
            SecretRealmSystem.Open(world, 8, false);
            Assert.That(world.Regions[8].AuraModifier, Is.EqualTo(0.96).Within(1e-12));
            Assert.That(world.Regions[8].Status, Is.EqualTo("수조: " + first.BeastName));
            world.SecretRealms.Clear();
            world.BeastTides.Clear();
            ulong state = world.Random.State;
            RegionEvents.Refresh(world, 8);
            Assert.That(world.Regions[8].AuraModifier, Is.EqualTo(1));
            Assert.That(world.Regions[8].Status, Is.EqualTo("평온"));
            Assert.That(world.Random.State, Is.EqualTo(state));
        }

        /// <summary>구 저장의 남아 있는 잘못된 보정도 색인 재구축 전에 고친다.</summary>
        [Test]
        public void Load_RecomputesStaleModifier()
        {
            World world = TestWorlds.Create(population: 100);
            world.Regions[8].AuraModifier = 1.2;
            world.Regions[8].Status = "이미 끝난 비경";
            World loaded = WorldSerializer.FromBytes(WorldSerializer.ToBytes(world));
            Assert.That(loaded.Regions[8].AuraModifier, Is.EqualTo(1));
            Assert.That(loaded.Regions[8].Status, Is.EqualTo("평온"));
        }

        /// <summary>범위 밖과 본산은 세계 난수·천기를 소비하지 않는다.</summary>
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(4)]
        [TestCase(13)]
        public void Intervention_RejectsInvalidRegionWithoutSpending(int regionId)
        {
            World world = TestWorlds.Create(population: 100);
            world.HeavenlyFavor = 10;
            ulong state = world.Random.State;
            Assert.That(InterventionSystem.OpenSecretRealm(world, regionId), Is.False);
            Assert.That(InterventionSystem.SummonBeastTide(world, regionId), Is.False);
            Assert.That(world.HeavenlyFavor, Is.EqualTo(10));
            Assert.That(world.Random.State, Is.EqualTo(state));
        }

        /// <summary>중복 사건과 번호만 같은 다른 인물 객체도 비용 없이 거부한다.</summary>
        [Test]
        public void Intervention_RejectsDuplicateAndStalePersonWithoutSpending()
        {
            World world = TestWorlds.Create(population: 100);
            world.HeavenlyFavor = 10;
            Assert.That(InterventionSystem.OpenSecretRealm(world, 8), Is.True);
            Assert.That(InterventionSystem.SummonBeastTide(world, 8), Is.True);
            Assert.That(world.HeavenlyFavor, Is.EqualTo(4));
            ulong state = world.Random.State;
            Assert.That(InterventionSystem.OpenSecretRealm(world, 8), Is.False);
            Assert.That(InterventionSystem.SummonBeastTide(world, 8), Is.False);
            Assert.That(InterventionSystem.BestowFortune(world,
                new Cultivator { Id = world.Cultivators[0].Id }), Is.False);
            Assert.That(InterventionSystem.BestowFortune(world, new Cultivator { Id = 999999 }), Is.False);
            Assert.That(world.HeavenlyFavor, Is.EqualTo(4));
            Assert.That(world.Random.State, Is.EqualTo(state));
        }
    }
}
