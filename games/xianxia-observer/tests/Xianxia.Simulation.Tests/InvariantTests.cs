// 오래 돌린 세계가 지켜야 할 불변식: 영석 보존, 직위 정원·집계, 지역 색인, 인연 대칭, 값 범위.
using System.Collections.Generic;
using NUnit.Framework;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Tests
{
    public sealed class InvariantTests
    {
        private World _world = null!;

        [OneTimeSetUp]
        public void RunWorld()
        {
            // 첫 전쟁(3000년)과 대비무·비경·수조가 모두 일어나도록 4500년을 돌린다.
            _world = TestWorlds.Run(4500, seed: 21, population: 400);
        }

        [Test]
        public void StoneLedger_MatchesActualHoldings()
        {
            long actual = 0;
            foreach (Cultivator cultivator in _world.Cultivators)
            {
                actual += cultivator.SpiritStones;
            }

            foreach (Sect sect in _world.Sects)
            {
                actual += sect.Treasury;
            }

            Assert.That(actual, Is.EqualTo(_world.Ledger.Expected()));
        }

        [Test]
        public void RankCounts_MatchMembersAndCapacity()
        {
            foreach (Sect sect in _world.Sects)
            {
                int[] counted = new int[(int)SectRank.Leader + 1];
                foreach (Cultivator cultivator in _world.Cultivators)
                {
                    if (cultivator.SectId == sect.Id)
                    {
                        counted[(int)cultivator.Rank]++;
                    }
                    else
                    {
                        Assert.That(cultivator.SectId == 0 || cultivator.SectId != sect.Id, Is.True);
                    }
                }

                for (int rank = 0; rank < counted.Length; rank++)
                {
                    Assert.That(sect.RankCounts[rank], Is.EqualTo(counted[rank]), sect.Name + " " + (SectRank)rank);
                    Assert.That(counted[rank], Is.LessThanOrEqualTo(SectRules.Capacity((SectRank)rank)));
                }
            }
        }

        [Test]
        public void Leader_IsMemberWithLeaderRank()
        {
            foreach (Sect sect in _world.Sects)
            {
                Cultivator? leader = _world.Find(sect.LeaderId);
                Assert.That(leader, Is.Not.Null, sect.Name);
                Assert.That(leader!.SectId, Is.EqualTo(sect.Id));
                Assert.That(leader.Rank, Is.EqualTo(SectRank.Leader));
            }
        }

        [Test]
        public void RogueCultivators_HaveNoRank()
        {
            foreach (Cultivator cultivator in _world.Cultivators)
            {
                if (cultivator.IsRogue)
                {
                    Assert.That(cultivator.Rank, Is.EqualTo(SectRank.None));
                }
            }
        }

        [Test]
        public void RegionResidents_ContainEachCultivatorExactlyOnce()
        {
            HashSet<int> seen = new();
            foreach (Region region in _world.Regions)
            {
                foreach (Cultivator resident in region.Residents)
                {
                    Assert.That(resident.RegionId, Is.EqualTo(region.Id));
                    Assert.That(seen.Add(resident.Id), Is.True, "중복 거주자 " + resident.Name);
                }
            }

            Assert.That(seen.Count, Is.EqualTo(_world.Cultivators.Count));
        }

        [Test]
        public void Relationships_AreSymmetric()
        {
            foreach (Cultivator cultivator in _world.Cultivators)
            {
                Cultivator? master = _world.Find(cultivator.MasterId);
                if (master is not null)
                {
                    Assert.That(master.DiscipleIds, Does.Contain(cultivator.Id));
                }

                foreach (int discipleId in cultivator.DiscipleIds)
                {
                    Assert.That(_world.Find(discipleId)?.MasterId, Is.EqualTo(cultivator.Id));
                }

                Cultivator? companion = _world.Find(cultivator.CompanionId);
                if (companion is not null)
                {
                    Assert.That(companion.CompanionId, Is.EqualTo(cultivator.Id));
                }

                foreach (int rivalId in cultivator.RivalIds)
                {
                    Assert.That(_world.Find(rivalId)?.RivalIds, Does.Contain(cultivator.Id));
                }

                Assert.That(cultivator.DiscipleIds.Count, Is.LessThanOrEqualTo(Balance.MaxDisciples));
                Assert.That(cultivator.RivalIds.Count, Is.LessThanOrEqualTo(Balance.MaxRivals));
            }
        }

        [Test]
        public void CultivatorValues_StayInRange()
        {
            foreach (Cultivator cultivator in _world.Cultivators)
            {
                Assert.That(cultivator.Realm, Is.InRange(0, Realms.Max));
                Assert.That(cultivator.Stage, Is.InRange(0, Realms.PeakStage));
                Assert.That(cultivator.Age, Is.LessThan(cultivator.Lifespan));
                Assert.That(cultivator.Qi, Is.GreaterThanOrEqualTo(0));
                Assert.That(cultivator.SpiritStones, Is.GreaterThanOrEqualTo(0));
                Assert.That(cultivator.Herbs, Is.GreaterThanOrEqualTo(0));
                Assert.That(cultivator.Ore, Is.GreaterThanOrEqualTo(0));
                Assert.That(cultivator.DaoHeart, Is.InRange(0, Balance.DaoHeartMax));
                Assert.That(cultivator.Insight, Is.InRange(0, Balance.InsightMax));
                Assert.That(cultivator.Injury, Is.LessThanOrEqualTo(cultivator.MaxHealth));
                Assert.That(cultivator.TechniqueGrade, Is.InRange(0, Techniques.MaxGrade));
                Assert.That(cultivator.ArtifactGrade, Is.InRange(0, 5));
                Assert.That(cultivator.Pills, Has.All.GreaterThanOrEqualTo(0));
                Assert.That(cultivator.Talismans, Has.All.GreaterThanOrEqualTo(0));
                Assert.That(cultivator.Biography.Count, Is.LessThanOrEqualTo(Balance.BiographyCapacity));
            }
        }

        [Test]
        public void SoulRecords_KeepStageInRange()
        {
            // 원본은 최고 단계 칸에 경지 값을 넣어 "반선경 undefined"를 표시했다.
            Assert.That(_world.Souls, Is.Not.Empty);
            foreach (SoulRecord soul in _world.Souls)
            {
                Assert.That(soul.BestStage, Is.InRange(0, Realms.PeakStage));
                Assert.That(soul.BestRealm, Is.InRange(0, Realms.Max));
                Assert.That(soul.TopScores.Count, Is.LessThanOrEqualTo(5));
            }
        }

        [Test]
        public void WorldEvents_HaveHappened()
        {
            Assert.That(_world.Wars, Is.Not.Empty, "전쟁");
            Assert.That(_world.Tournaments, Is.Not.Empty, "대비무");
            Assert.That(_world.Chronicle.Entries, Is.Not.Empty, "연대기");
            Assert.That(_world.Statistics.Samples.Count, Is.GreaterThan(10), "통계");
            Assert.That(_world.Totals.MajorBreakthroughs, Is.GreaterThan(0), "돌파");
        }

        [Test]
        public void Population_StaysConstant()
        {
            Assert.That(_world.Cultivators.Count, Is.EqualTo(400));
        }
    }
}
