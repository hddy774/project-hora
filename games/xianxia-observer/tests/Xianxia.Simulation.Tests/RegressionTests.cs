// 원본 게임에서 확인한 버그가 다시 생기지 않는지 확인하는 회귀 검사(docs/01-feedback.md B1~B8).
using System.Linq;
using NUnit.Framework;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    public sealed class RegressionTests
    {
        [Test]
        public void BorderBattle_AwardsPointsToActualWinner()
        {
            // B1: 원본은 배열 앞쪽(청운종)에게 점수를 줬다. 청운종을 먼저 넘겨도 이긴 천마문이 점수를 받아야 한다.
            World world = TestWorlds.Create();
            world.War.AtWar = true;
            Region region = world.Regions[9];
            Cultivator orthodox = world.Cultivators.First(c => c.SectId == RegionCatalog.OrthodoxSectId);
            Cultivator demonic = world.Cultivators.First(c => c.SectId == RegionCatalog.DemonicSectId);
            orthodox.Realm = 0;
            demonic.Realm = 8;
            demonic.Lifespan = Lifespans.For(demonic);
            int before = region.ControlPoints;
            WarSystem.FightBorderBattle(world, region, orthodox, demonic);
            Assert.That(world.War.DemonicWins, Is.EqualTo(1));
            Assert.That(world.War.OrthodoxWins, Is.EqualTo(0));
            Assert.That(region.ControlPoints, Is.EqualTo(before - 1));
        }

        [Test]
        public void Join_NeverSeatsNewcomerInVacantHighRank()
        {
            // B4: 원본 fillSlot은 아래 직위가 차면 빈 장로·종주 자리에 갓 환생한 열여섯 살을 앉혔다.
            World world = TestWorlds.Create();
            Sect sect = world.Sects[0];
            Cultivator rogue = world.Cultivators.First(c => c.IsRogue);
            sect.RankCounts[(int)SectRank.Outer] = SectRules.Capacity(SectRank.Outer);
            sect.RankCounts[(int)SectRank.Elder] = 0;
            SectSystem.Join(world, rogue, sect);
            Assert.That(rogue.Rank, Is.EqualTo(SectRank.None));
            Assert.That(rogue.SectId, Is.EqualTo(sect.Id));
        }

        [Test]
        public void Rerank_ClearsRanksOutsideCapacityAndUpdatesLeader()
        {
            // B3·B5: 원본 재편은 명부 밖 구성원의 옛 직위를 남겼고, 종주 번호를 갱신하지 않았다.
            World world = TestWorlds.Create(seed: 3, population: 1000);
            Sect sect = world.Sects[0];
            foreach (Cultivator member in world.Cultivators.Where(c => c.SectId == sect.Id))
            {
                member.Rank = SectRank.Elder;
            }

            Cultivator strongest = world.Cultivators.Where(c => c.SectId == sect.Id)
                .OrderByDescending(SectSystem.RankScore)
                .ThenBy(c => c.Id)
                .First();
            SectSystem.Rerank(world, sect, announce: false);
            int ranked = world.Cultivators.Count(c => c.SectId == sect.Id && c.Rank != SectRank.None);
            Assert.That(ranked, Is.LessThanOrEqualTo(363));
            Assert.That(world.Cultivators.Count(c => c.SectId == sect.Id && c.Rank == SectRank.Elder),
                Is.LessThanOrEqualTo(SectRules.Capacity(SectRank.Elder)));
            Assert.That(sect.LeaderId, Is.EqualTo(strongest.Id));
            Assert.That(strongest.Rank, Is.EqualTo(SectRank.Leader));
        }

        [Test]
        public void EventLog_KeepsNewestEntriesInOrder()
        {
            // B2: 원본은 unshift/push를 섞고 앞에서 잘라 최신 돌파·전투 기록을 버렸다.
            EventLog log = new();
            for (int index = 0; index < 700; index++)
            {
                log.Add(index, index % 2 == 0 ? LogKind.Breakthrough : LogKind.Rebirth, "기록 " + index);
            }

            Assert.That(log.Count, Is.EqualTo(Balance.LogCapacity));
            System.Collections.Generic.List<LogEntry> newest = log.CopyNewestFirst(Balance.LogCapacity);
            Assert.That(newest[0].Text, Is.EqualTo("기록 699"));
            Assert.That(newest.Select(entry => entry.Sequence), Is.Ordered.Descending);
            Assert.That(newest.Count(entry => entry.Kind == LogKind.Breakthrough), Is.EqualTo(300));
            Assert.That(log.Counts[(int)LogKind.Breakthrough], Is.EqualTo(350));
        }

        [Test]
        public void Ideology_ChangesGrowthByRealm()
        {
            // B6: 원본은 이념 배율(IDEO.bm)을 계산만 하고 적용하지 않았다.
            Assert.That(SectRules.IdeologyGrowth(orthodox: false, 0),
                Is.GreaterThan(SectRules.IdeologyGrowth(orthodox: true, 0)));
            Assert.That(SectRules.IdeologyGrowth(orthodox: true, 7),
                Is.GreaterThan(SectRules.IdeologyGrowth(orthodox: false, 7)));
            World world = TestWorlds.Create();
            Cultivator member = world.Cultivators.First(c => c.SectId == RegionCatalog.DemonicSectId);
            Assert.That(Growth.Breakdown(world, member).Ideology, Is.Not.EqualTo(1.0));
        }

        [Test]
        public void Power_IsDominatedByRealm()
        {
            // B7: 원본 전투력은 재능 항(평균 45%)이 경지 항(14%)보다 컸다.
            World world = TestWorlds.Create();
            Cultivator weak = world.Cultivators[0];
            Cultivator strong = world.Cultivators[1];
            strong.Comprehension = strong.Constitution = strong.Bone = strong.Root = Traits.StatMin;
            weak.Comprehension = weak.Constitution = weak.Bone = weak.Root = Traits.StatMax;
            weak.Realm = 2;
            strong.Realm = 3;
            weak.Stage = strong.Stage = 0;
            weak.TechniqueGrade = strong.TechniqueGrade = 4;
            weak.ArtifactGrade = strong.ArtifactGrade = 0;
            weak.Injury = strong.Injury = 0;
            Assert.That(CombatSystem.Power(world, strong), Is.GreaterThan(CombatSystem.Power(world, weak)));
        }

        [Test]
        public void RealmCollapse_IsRecordedSeparately()
        {
            // B8: 경지 하락으로 수명이 줄어 죽는 경우를 원본은 수명종료와 구분하지 않았다.
            World world = TestWorlds.Create();
            Cultivator elder = world.Cultivators[5];
            elder.HighestRealm = 5;
            elder.Realm = 3;
            elder.Lifespan = Lifespans.For(elder);
            elder.Age = elder.Lifespan - 1;
            long before = world.Totals.CollapseDeaths;
            Assert.That(LifeCycleSystem.Age(world, elder), Is.True);
            Assert.That(world.Totals.CollapseDeaths, Is.EqualTo(before + 1));
            Assert.That(elder.Age, Is.EqualTo(Balance.StartingAge));
            Assert.That(elder.Reincarnations, Is.EqualTo(1));
        }
    }
}
