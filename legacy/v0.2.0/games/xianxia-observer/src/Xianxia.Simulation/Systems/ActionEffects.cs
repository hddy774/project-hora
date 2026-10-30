// 행동 효과: 수련, 탐색(자원·기연·위험), 비무 상대 찾기, 종문 임무, 지역 이동, 요양.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>행동 효과 규칙.</summary>
    public static class ActionEffects
    {
        private const int OpponentTries = 6;
        private static readonly int[] s_fortuneWeights = { 18, 12, 22, 20, 14, 14 };

        /// <summary>수련: 영기·깨달음·내공을 얻는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="share">한 해 중 수련에 쓴 비율(0~1).</param>
        public static void Cultivate(World world, Cultivator cultivator, double share)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            double previousQi = cultivator.Qi;
            cultivator.Qi += Growth.QiPerYear(world, cultivator) * world.Random.Range(0.9, 1.5) * share;
            world.Diagnostics?.AddQi(cultivator, QiSource.Cultivation, cultivator.Qi - previousQi);
            Growth.AddInsight(cultivator, Growth.InsightPerYear(cultivator) * world.Random.Range(0.5, 1.5) * share);
            cultivator.InnerPower += (0.3 + (0.1 * cultivator.Realm)) * share;
        }

        /// <summary>탐색: 영석·영초·영철을 얻고, 드물게 기연을 만나거나 영수에게 다친다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Explore(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Region region = world.Regions[cultivator.RegionId];
            RegionDefinition place = region.Definition;
            double richness = (place.Herbs + place.Ore) / 2 * (region.AuraModifier < 1 ? 0.8 : 1.0);
            long found = (long)Math.Round(world.Random.Range(4, 16) * (1 + (0.6 * cultivator.Realm)) * richness);
            cultivator.SpiritStones += found;
            world.Ledger.Create(StoneSource.Exploration, found);
            Tithe(world, cultivator, found);
            if (world.Random.Chance(0.5 * place.Herbs))
            {
                cultivator.Herbs += world.Random.Range(1, 3);
            }

            if (world.Random.Chance(0.35 * place.Ore))
            {
                cultivator.Ore += world.Random.Range(1, 2);
            }

            Cultivate(world, cultivator, 0.3);
            double fortune = 0.0015 * (cultivator.IsRogue ? 1.5 : 1.0) * (0.5 + cultivator.Charisma)
                * (world.Era.Kind == EraKind.Resurgence ? 1.5 : 1.0);
            if (world.Random.Chance(fortune))
            {
                Fortune(world, cultivator, "탐색 중");
            }

            if (world.Random.Chance(place.Danger * 0.15 * (1 - (0.5 * cultivator.Caution))))
            {
                bool critical = Setbacks.Injure(world, cultivator, world.Random.Range(0.05, 0.2), "영수의 습격");
                if (!critical && world.Random.Chance(0.3))
                {
                    cultivator.BeastCores++;
                }
            }
        }

        /// <summary>
        /// 기연: 공법·법보·깨달음·영기·파경단·영석 중 하나를 얻는다. 세 번 만나면 '기연아' 호칭.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="where">상황(로그용).</param>
        public static void Fortune(World world, Cultivator cultivator, string where)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Span<double> weights = stackalloc double[s_fortuneWeights.Length];
            for (int index = 0; index < weights.Length; index++)
            {
                weights[index] = s_fortuneWeights[index];
            }

            string gain = world.Random.PickWeighted(weights) switch
            {
                0 => GainTechnique(cultivator, world.Random.Range(1, 2)),
                1 => GainArtifact(cultivator),
                2 => GainInsight(cultivator, world.Random.Range(200, 400)),
                3 => GainQi(world, cultivator, 0.4),
                4 => GainPill(cultivator, PillKind.Breakthrough),
                _ => GainStones(world, cultivator, world.Random.Range(100, 500) * (cultivator.Realm + 1)),
            };
            cultivator.Fortunes++;
            Narrator.Log(world, LogKind.Fortune, cultivator.Name + "이(가) " + where + " 기연을 만나 " + gain + ".",
                cultivator,
                notable: true);
            Narrator.Biography(world, cultivator, where + " 기연: " + gain + ".");
            TitleSystem.CheckFortune(world, cultivator);
        }

        /// <summary>비무·결투: 같은 지역에서 비슷한 경지의 상대(숙적 우선)를 찾아 겨룬다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Spar(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Cultivator? foe = FindOpponent(world, cultivator);
            if (foe is null)
            {
                Cultivate(world, cultivator, 0.5);
                return;
            }

            BattleKind kind = KindOf(world, cultivator, foe);
            if (kind == BattleKind.War)
            {
                WarSystem.FightBorderBattle(world, world.Regions[cultivator.RegionId], cultivator, foe);
                return;
            }

            CombatSystem.Fight(world, cultivator, foe, kind);
        }

        /// <summary>종문 임무: 공헌·보수·깨달음을 얻는다. 보수의 30%는 종문 금고로 간다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">구성원.</param>
        public static void Mission(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is null)
            {
                Cultivate(world, cultivator, 1.0);
                return;
            }

            SectSystem.AddContribution(world, cultivator, world.Random.Range(15, 30) * (cultivator.Realm + 1));
            long reward = world.Random.Range(5, 15) * (cultivator.Realm + 1);
            world.Ledger.Create(StoneSource.Mission, reward);
            long share = reward * 3 / 10;
            sect.Treasury += share;
            cultivator.SpiritStones += reward - share;
            Growth.AddInsight(cultivator, 5);
            if (world.Random.Chance(0.05))
            {
                Setbacks.Injure(world, cultivator, world.Random.Range(0.05, 0.15), "임무 중 사고");
            }

            if (world.Random.Chance(0.002))
            {
                Fortune(world, cultivator, "임무 중");
            }
        }

        /// <summary>이동: 종문 제자는 영지와 접경지를 오가고, 떠돌이는 영기와 자원, 열린 비경을 따라 떠돈다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Travel(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            int destination = cultivator.IsRogue ? RogueDestination(world, cultivator) : MemberDestination(world,
                cultivator);
            if (destination == cultivator.RegionId)
            {
                Cultivate(world, cultivator, 0.5);
                return;
            }

            LifeCycleSystem.MoveTo(world, cultivator, destination);
            if (world.IsFollowed(cultivator.Id))
            {
                Narrator.Log(world, LogKind.Relation, cultivator.Name + "이(가) " + world.Regions[destination].Name
                    + "(으)로 떠났다.",
                    cultivator);
            }
        }

        /// <summary>요양: 부상을 크게 회복하고 필요하면 회복단을 먹는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Recover(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.Injury = Math.Max(0, cultivator.Injury - (cultivator.MaxHealth * 0.1));
            int healing = (int)PillKind.Healing;
            if (cultivator.HealthRatio < 0.6 && cultivator.Pills[healing] > 0)
            {
                cultivator.Pills[healing]--;
                cultivator.Injury = Math.Max(0, cultivator.Injury - (cultivator.MaxHealth * 0.4));
            }

            int clarity = (int)PillKind.Clarity;
            if (cultivator.DaoHeart < 40 && cultivator.Pills[clarity] > 0)
            {
                cultivator.Pills[clarity]--;
                Growth.AddDaoHeart(cultivator, 20);
            }

            Growth.AddDaoHeart(cultivator, 1);
        }

        private static void Tithe(World world, Cultivator cultivator, long found)
        {
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is null)
            {
                return;
            }

            long tithe = (long)(found * (sect.Orthodox ? 0.1 : 0.15));
            cultivator.SpiritStones -= tithe;
            sect.Treasury += tithe;
        }

        private static Cultivator? FindOpponent(World world, Cultivator cultivator)
        {
            List<Cultivator> residents = world.Regions[cultivator.RegionId].Residents;
            foreach (int rivalId in cultivator.RivalIds)
            {
                Cultivator? rival = world.Find(rivalId);
                if (rival is not null && rival.RegionId == cultivator.RegionId && rival.RecoveryYears == 0)
                {
                    return rival;
                }
            }

            for (int attempt = 0; attempt < OpponentTries && residents.Count > 1; attempt++)
            {
                Cultivator candidate = residents[world.Random.NextInt(residents.Count)];
                if (candidate.Id != cultivator.Id && candidate.RecoveryYears == 0
                    && Math.Abs(candidate.Realm - cultivator.Realm) <= 1)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static BattleKind KindOf(World world, Cultivator cultivator, Cultivator foe)
        {
            if (cultivator.RivalIds.Contains(foe.Id))
            {
                return BattleKind.Grudge;
            }

            if (!cultivator.IsRogue && cultivator.SectId == foe.SectId)
            {
                return BattleKind.Spar;
            }

            if (!cultivator.IsRogue && !foe.IsRogue)
            {
                bool border = world.Regions[cultivator.RegionId].Kind == RegionKind.Border;
                return world.War.AtWar && border ? BattleKind.War : BattleKind.CrossSectSpar;
            }

            bool predatory = cultivator.Personality is Personality.Demonic or Personality.Domineering;
            return predatory && cultivator.Ambition > 0.6 ? BattleKind.Grudge : BattleKind.Spar;
        }

        private static int MemberDestination(World world, Cultivator cultivator)
        {
            IReadOnlyList<int> territory = RegionCatalog.TerritoryOf(cultivator.SectId);
            bool atHome = world.Regions[cultivator.RegionId].Definition.HomeSectId == cultivator.SectId;
            if (cultivator.OnWarDuty)
            {
                return cultivator.RegionId;
            }

            if (!atHome || world.Random.Chance(0.75))
            {
                return world.Random.Pick(territory);
            }

            return world.Random.Pick(RegionCatalog.BorderIds);
        }

        private static int RogueDestination(World world, Cultivator cultivator)
        {
            Span<double> weights = stackalloc double[world.Regions.Count];
            for (int index = 0; index < world.Regions.Count; index++)
            {
                Region region = world.Regions[index];
                double weight = region.Aura * (region.Definition.Herbs + region.Definition.Ore);
                weight *= region.Kind == RegionKind.Border ? 2.0 : 0.6;
                weight *= SecretRealmSystem.IsOpenFor(world, index, cultivator) ? 6.0 : 1.0;
                weight *= BeastTideSystem.IsRaging(world, index) ? 0.3 + cultivator.Ambition : 1.0;
                weights[index] = weight;
            }

            return world.Random.PickWeighted(weights);
        }

        /// <summary>기연 공법 보상. 상위 공법은 보존하며 새로운 난수를 쓰지 않는다.</summary>
        /// <param name="cultivator">수선자.</param>
        /// <param name="grades">호출자가 이미 뽑은 획득 등급.</param>
        /// <returns>기연 기록에 넣을 문장.</returns>
        internal static string GainTechnique(Cultivator cultivator, int grades)
        {
            int previous = cultivator.TechniqueGrade;
            cultivator.TechniqueGrade = Techniques.ImproveByReward(previous, grades, 15);
            if (cultivator.TechniqueGrade == previous)
            {
                return "고대 비급을 얻었으나 이미 " + Techniques.Name(previous) + " 공법을 익혀 새로 깨친 것이 없었다";
            }

            return "고대 비급에서 " + Techniques.Name(cultivator.TechniqueGrade) + " 공법을 깨쳤다";
        }

        private static string GainArtifact(Cultivator cultivator)
        {
            cultivator.ArtifactGrade = Math.Min(5, cultivator.ArtifactGrade + 1);
            return Labels.ArtifactGrade(cultivator.ArtifactGrade) + " 법보를 얻었다";
        }

        private static string GainInsight(Cultivator cultivator, int amount)
        {
            Growth.AddInsight(cultivator, amount);
            return "깨달음 " + amount + "을 얻었다";
        }

        private static string GainQi(World world, Cultivator cultivator, double ratio)
        {
            double previousQi = cultivator.Qi;
            cultivator.Qi += cultivator.QiRequirement * ratio;
            world.Diagnostics?.AddQi(cultivator, QiSource.Fortune, cultivator.Qi - previousQi);
            return "천년 영약을 먹고 영기가 차올랐다";
        }

        private static string GainPill(Cultivator cultivator, PillKind pill)
        {
            cultivator.Pills[(int)pill]++;
            return Labels.Of(pill) + "을 얻었다";
        }

        private static string GainStones(World world, Cultivator cultivator, long amount)
        {
            cultivator.SpiritStones += amount;
            world.Ledger.Create(StoneSource.Fortune, amount);
            return "영석 " + KoreanNumber.Compact(amount) + "을 캐냈다";
        }
    }
}
