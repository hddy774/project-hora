// 확장 기능 검사: 비승, 유산, 대비무, 천기 개입, 비경, 수조, 인연, 호칭.
using System.Linq;
using NUnit.Framework;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    public sealed class FeatureTests
    {
        [Test]
        public void Ascend_ReplacesSoulAndKeepsPopulation()
        {
            World world = TestWorlds.Create();
            Cultivator sage = world.Cultivators[10];
            int slot = 10;
            int count = world.Cultivators.Count;
            LifeCycleSystem.Ascend(world, sage);
            Assert.That(world.Cultivators.Count, Is.EqualTo(count));
            Assert.That(world.Cultivators[slot].Id, Is.Not.EqualTo(sage.Id));
            Assert.That(world.Find(sage.Id), Is.Null);
            Assert.That(world.Immortals.Single().SoulId, Is.EqualTo(sage.Id));
            Assert.That(world.FindSoul(sage.Id)!.Ascended, Is.True);
            AssertLedger(world);
        }

        [Test]
        public void Death_PassesEstateToDisciple()
        {
            World world = TestWorlds.Create();
            Cultivator master = world.Cultivators[3];
            Cultivator disciple = world.Cultivators[4];
            master.DiscipleIds.Add(disciple.Id);
            disciple.MasterId = master.Id;
            long estate = master.SpiritStones;
            long discipleBefore = disciple.SpiritStones;
            LifeCycleSystem.Die(world, master, DeathCause.Lifespan);
            Assert.That(disciple.SpiritStones, Is.EqualTo(discipleBefore + estate));
            AssertLedger(world);
            Assert.That(disciple.MasterId, Is.EqualTo(0));
        }

        [Test]
        public void Tournament_ProducesBracketAndChampion()
        {
            World world = TestWorlds.Run(600, seed: 4, population: 400);
            Assert.That(world.Tournaments, Is.Not.Empty);
            TournamentRecord record = world.Tournaments[0];
            Assert.That(record.Matches.Count, Is.EqualTo(record.Entrants - 1));
            Assert.That(record.Matches[^1].WinnerId, Is.EqualTo(record.ChampionId));
            Cultivator? champion = world.Find(record.ChampionId);
            Assert.That(champion is null || champion.TournamentBest == 1 || champion.Reincarnations > 0, Is.True);
        }

        [Test]
        public void Intervention_SpendsFavorAndRefusesWhenEmpty()
        {
            World world = TestWorlds.Create();
            world.HeavenlyFavor = 3;
            Cultivator chosen = world.Cultivators[0];
            Assert.That(InterventionSystem.BestowFortune(world, chosen), Is.True);
            Assert.That(world.HeavenlyFavor, Is.EqualTo(1));
            Assert.That(chosen.HasTitle(TitleId.Chosen), Is.True);
            Assert.That(InterventionSystem.OpenSecretRealm(world, 9), Is.False);
            Assert.That(world.SecretRealms, Is.Empty);
        }

        [Test]
        public void SecretRealm_OpensAndClosesWithRecord()
        {
            World world = TestWorlds.Create();
            world.HeavenlyFavor = 10;
            Assert.That(InterventionSystem.OpenSecretRealm(world, 10), Is.True);
            SecretRealmState realm = world.SecretRealms.Single();
            Assert.That(world.Regions[10].AuraModifier, Is.GreaterThan(1.0));
            WorldSimulator.Run(world, realm.CloseYear - world.Year + 1);
            Assert.That(world.SecretRealms.Any(open => open.Name == realm.Name && open.OpenYear == realm.OpenYear),
                Is.False);
            Assert.That(world.Regions[10].AuraModifier, Is.EqualTo(1.0));
        }

        [Test]
        public void BeastTide_EndsAndRestoresRegion()
        {
            World world = TestWorlds.Create();
            world.HeavenlyFavor = 10;
            Assert.That(InterventionSystem.SummonBeastTide(world, 2), Is.True);
            BeastTideState tide = world.BeastTides.Single();
            Assert.That(BeastTideSystem.IsRaging(world, 2), Is.True);
            WorldSimulator.Run(world, tide.EndYear - world.Year + 1);
            Assert.That(world.BeastTides.Any(active => active.StartYear == tide.StartYear && active.RegionId == 2),
                Is.False);
            Assert.That(world.Regions[2].AuraModifier, Is.EqualTo(1.0));
        }

        [Test]
        public void Rivalry_IsMutualAndCapped()
        {
            World world = TestWorlds.Create();
            Cultivator first = world.Cultivators[0];
            for (int index = 1; index <= 5; index++)
            {
                RelationshipSystem.TryFormRivalry(world, first, world.Cultivators[index]);
            }

            Assert.That(first.RivalIds.Count, Is.EqualTo(Balance.MaxRivals));
            foreach (int rivalId in first.RivalIds)
            {
                Assert.That(world.Find(rivalId)!.RivalIds, Does.Contain(first.Id));
            }
        }

        [Test]
        public void Title_IsAwardedOnce()
        {
            World world = TestWorlds.Create();
            Cultivator hero = world.Cultivators[0];
            Assert.That(TitleSystem.Award(world, hero, TitleId.Fortunate), Is.True);
            Assert.That(TitleSystem.Award(world, hero, TitleId.Fortunate), Is.False);
            Assert.That(hero.Titles.Count(title => title == TitleId.Fortunate), Is.EqualTo(1));
        }

        [Test]
        public void WarCycle_FollowsThousandYearWarAndTenThousandYearPeace()
        {
            Assert.That(WarSystem.IsWarYear(Balance.FirstWarYear - 1), Is.False);
            Assert.That(WarSystem.IsWarYear(Balance.FirstWarYear), Is.True);
            Assert.That(WarSystem.IsWarYear(Balance.FirstWarYear + Balance.WarDuration - 1), Is.True);
            Assert.That(WarSystem.IsWarYear(Balance.FirstWarYear + Balance.WarDuration), Is.False);
            Assert.That(WarSystem.IsWarYear(Balance.FirstWarYear + Balance.WarCycle), Is.True);
            Assert.That(WarSystem.YearsUntilChange(0), Is.EqualTo(Balance.FirstWarYear));
        }

        private static void AssertLedger(World world)
        {
            long actual = world.Cultivators.Sum(c => c.SpiritStones) + world.Sects.Sum(s => s.Treasury);
            Assert.That(actual, Is.EqualTo(world.Ledger.Expected()));
        }
    }
}
