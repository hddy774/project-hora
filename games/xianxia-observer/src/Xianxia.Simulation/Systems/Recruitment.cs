// 입문 시험(떠돌이 영입)과 배신·귀순. 원본에는 떠돌이가 사실상 없어 종문 가입 규칙이 작동하지 않았다.
using System;
using System.Collections.Generic;
using System.Linq;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>종문 출입 규칙.</summary>
    public static class Recruitment
    {
        private const int MaxRecruitsPerExam = 3;

        /// <summary>
        /// 50년마다 각 종문이 영지와 접경지의 떠돌이를 시험해 들인다(점령하지 않은 접경지는 절반 확률).
        /// 성격이 이념과 맞고 재능이 좋을수록 잘 받아들여지며, 은둔형은 거절하기도 한다.
        /// 인원이 적은 종문은 정원을 늘려 한쪽이 눈덩이처럼 커지는 것을 막는다.
        /// </summary>
        /// <param name="world">세계.</param>
        public static void Recruit(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            int orthodoxMembers = world.Sects[0].RankCounts.Sum();
            int demonicMembers = world.Sects[1].RankCounts.Sum();
            foreach (Sect sect in world.Sects)
            {
                int mine = sect.Id == RegionCatalog.OrthodoxSectId ? orthodoxMembers : demonicMembers;
                int theirs = sect.Id == RegionCatalog.OrthodoxSectId ? demonicMembers : orthodoxMembers;
                double imbalance = (theirs - mine) / (double)Math.Max(1, mine + theirs);
                int limit = Math.Clamp((int)Math.Round(MaxRecruitsPerExam * (1 + (2 * imbalance))), 1,
                    MaxRecruitsPerExam * 2);
                int accepted = 0;
                foreach (Region region in world.Regions)
                {
                    bool ours = region.ControllerSectId == sect.Id;
                    if (!ours && region.Kind != RegionKind.Border)
                    {
                        continue;
                    }

                    accepted += RecruitIn(world, sect, region, limit - accepted, ours ? 1.0 : 0.5);
                    if (accepted >= limit)
                    {
                        break;
                    }
                }

                if (accepted > 0)
                {
                    Narrator.Log(world, LogKind.Sect, "[" + sect.Name + "] 입문 시험에서 " + accepted + "명을 받아들였다.");
                }
            }
        }

        /// <summary>
        /// 100년마다 도심이 무너지고 성격이 이념과 어긋난 구성원이 떠나거나 적 종문으로 넘어간다.
        /// </summary>
        /// <param name="world">세계.</param>
        public static void CheckDefections(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (Cultivator cultivator in world.Cultivators)
            {
                Sect? sect = world.FindSect(cultivator.SectId);
                if (sect is null || cultivator.Rank == SectRank.Leader || cultivator.DaoHeart >= 55)
                {
                    continue;
                }

                double affinity = Traits.SectAffinity(cultivator.Personality, sect.Orthodox);
                double chance = affinity < 0.3 ? 0.15 : affinity < 0.6 ? 0.02 : 0.0;
                if (world.Era.Kind == EraKind.BloodMoon && sect.Orthodox)
                {
                    chance *= 2;
                }

                if (world.Random.Chance(chance))
                {
                    Defect(world, cultivator, sect);
                }
            }
        }

        private static int RecruitIn(World world, Sect sect, Region region, int limit, double reach)
        {
            if (limit <= 0)
            {
                return 0;
            }

            List<Cultivator> candidates = new();
            foreach (Cultivator resident in region.Residents)
            {
                if (resident.IsRogue && !resident.HasTitle(TitleId.Traitor))
                {
                    candidates.Add(resident);
                }
            }

            int accepted = 0;
            foreach (Cultivator candidate in candidates)
            {
                double affinity = Traits.SectAffinity(candidate.Personality, sect.Orthodox);
                double talent = 0.5 + (candidate.TalentTotal / 2000.0);
                double chance = 0.15 * reach * affinity * talent * (candidate.Personality == Personality.Hermit
                    ? 0.5 : 1.0);
                if (accepted >= limit || !world.Random.Chance(chance))
                {
                    continue;
                }

                SectSystem.Join(world, candidate, sect);
                accepted++;
                Narrator.Biography(world, candidate, sect.Name + " 입문 시험에 합격했다.");
                if (world.IsFollowed(candidate.Id))
                {
                    Narrator.Log(world, LogKind.Sect, candidate.Name + "이(가) " + sect.Name + "에 입문했다.", candidate,
                        notable: true);
                }
            }

            return accepted;
        }

        private static void Defect(World world, Cultivator cultivator, Sect from)
        {
            Sect? to = null;
            foreach (Sect other in world.Sects)
            {
                if (other.Id != from.Id && Traits.SectAffinity(cultivator.Personality, other.Orthodox) >= 0.5)
                {
                    to = other;
                }
            }

            bool notable = cultivator.Rank >= SectRank.Elder || cultivator.Realm >= 4;
            string rank = Labels.Of(cultivator.Rank);
            world.Totals.Defections++;
            if (to is null)
            {
                SectSystem.Leave(world, cultivator);
                string leaveText = "[" + from.Name + "] " + rank + " " + cultivator.Name + "이(가) 도심을 잃고 산문을 떠났다.";
                Narrator.Log(world, LogKind.Sect, leaveText, cultivator, notable: true);
                Narrator.Biography(world, cultivator, from.Name + "을(를) 떠나 떠돌이가 되었다.");
                return;
            }

            SectSystem.Join(world, cultivator, to);
            TitleSystem.Award(world, cultivator, TitleId.Traitor);
            LifeCycleSystem.MoveTo(world, cultivator, RegionCatalog.HomeOf(to.Id));
            string text = "[" + from.Name + "] " + rank + " " + cultivator.DisplayName + "이(가) " + to.Name
                + "(으)로 넘어갔다!";
            Narrator.Log(world, LogKind.Sect, text, cultivator, notable: true);
            Narrator.Biography(world, cultivator, from.Name + "을(를) 배신하고 " + to.Name + "에 몸을 맡겼다.");
            if (notable)
            {
                Narrator.Chronicle(world, ChronicleKind.Defection, text, cultivator);
            }
        }
    }
}
