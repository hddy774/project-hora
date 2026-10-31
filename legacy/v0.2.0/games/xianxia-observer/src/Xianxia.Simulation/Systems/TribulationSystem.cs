// 천겁(뇌겁)과 비승. 화신경 이상으로 올라설 때 하늘의 벼락을 견뎌야 하고, 반선경 대원만은 비승 천겁에 도전한다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>천겁 규칙.</summary>
    public static class TribulationSystem
    {
        /// <summary>천겁 통과 확률(10~92%).</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="targetRealm">올라설 경지.</param>
        /// <returns>확률.</returns>
        public static double Chance(World world, Cultivator cultivator, int targetRealm)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            double chance = 0.30
                + (0.25 * Math.Min(1, cultivator.InnerPower / (150.0 * targetRealm)))
                + (0.15 * cultivator.Constitution / Traits.StatMax)
                + (0.15 * cultivator.DaoHeart / Balance.DaoHeartMax)
                + (0.04 * cultivator.ArtifactGrade)
                + (sect is null ? 0 : 0.02 * sect.Level(FacilityKind.GuardianArray))
                - (0.04 * Math.Max(0, targetRealm - Realms.FirstTribulationRealm));
            return Math.Clamp(chance, 0.1, 0.92);
        }

        /// <summary>
        /// 천겁을 겪는다. 피뢰부가 있으면 하나 써서 +10%p. 실패하면 큰 부상을 입고 돌파가 무산된다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="targetRealm">올라설 경지.</param>
        /// <returns>통과하면 true.</returns>
        public static bool Endure(World world, Cultivator cultivator, int targetRealm)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            world.Totals.Tribulations++;
            cultivator.LastTribulationYear = world.Year;
            double chance = Chance(world, cultivator, targetRealm);
            int thunder = (int)TalismanKind.Thunder;
            if (cultivator.Talismans[thunder] > 0)
            {
                cultivator.Talismans[thunder]--;
                chance = Math.Min(0.95, chance + 0.1);
            }

            string realmName = Realms.Name(targetRealm);
            if (world.Random.Chance(chance))
            {
                cultivator.TribulationsPassed++;
                cultivator.InnerPower += 20 * targetRealm;
                TitleSystem.CheckTribulation(world, cultivator);
                Narrator.Log(world, LogKind.Tribulation, cultivator.DisplayName + "이(가) " + realmName + "의 천겁을 넘었다!",
                    cultivator, notable: true);
                return true;
            }

            Growth.AddDaoHeart(cultivator, -10);
            Setbacks.Injure(world, cultivator, world.Random.Range(0.4, 0.7), "천겁");
            string text = cultivator.DisplayName + "이(가) " + realmName + "의 천겁에 막혀 돌파가 무산되었다.";
            Narrator.Log(world, LogKind.Tribulation, text, cultivator, notable: true);
            Narrator.Biography(world, cultivator, realmName + "의 천겁에 실패했다.");
            if (targetRealm >= 6)
            {
                Narrator.Chronicle(world, ChronicleKind.FirstRealm, text, cultivator);
            }

            return false;
        }
    }

    /// <summary>비승 규칙. 성공하면 영혼이 선계로 떠나고 같은 자리에 새 영혼이 태어난다.</summary>
    public static class AscensionSystem
    {
        /// <summary>비승 확률(2~60%).</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>확률.</returns>
        public static double Chance(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            double chance = Balance.AscensionBase
                * (0.6 + (0.8 * (cultivator.Comprehension + cultivator.Constitution) / (2.0 * Traits.StatMax)))
                * (1 + (0.5 * cultivator.Insight / Balance.InsightMax))
                * (0.6 + (0.5 * cultivator.DaoHeart / Balance.DaoHeartMax))
                * (1 + (0.04 * cultivator.ArtifactGrade));
            return Math.Clamp(chance, 0.02, 0.6);
        }

        /// <summary>비승을 시도한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">반선경 대원만 수선자.</param>
        /// <param name="requirement">필요 영기.</param>
        public static void Attempt(World world, Cultivator cultivator, long requirement)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            double chance = Chance(cultivator);
            int thunder = (int)TalismanKind.Thunder;
            if (cultivator.Talismans[thunder] > 0)
            {
                cultivator.Talismans[thunder]--;
                chance = Math.Min(0.7, chance * 1.1);
            }

            if (world.Random.Chance(chance))
            {
                LifeCycleSystem.Ascend(world, cultivator);
                return;
            }

            cultivator.Qi = Math.Max(0, cultivator.Qi - requirement);
            Growth.AddDaoHeart(cultivator, -10);
            Setbacks.Injure(world, cultivator, world.Random.Range(0.5, 0.8), "비승 천겁");
            string text = cultivator.DisplayName + "이(가) 비승 천겁에 도전했으나 벼락에 밀려났다.";
            Narrator.Log(world, LogKind.Ascension, text, cultivator, notable: true);
            Narrator.Biography(world, cultivator, "비승 천겁에 실패했다.");
            Narrator.Chronicle(world, ChronicleKind.Ascension, text, cultivator);
        }

        /// <summary>비승 명부를 최신순으로 돌려준다.</summary>
        /// <param name="world">세계.</param>
        /// <returns>명부.</returns>
        public static List<ImmortalRecord> Registry(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            List<ImmortalRecord> list = new(world.Immortals);
            list.Reverse();
            return list;
        }
    }
}
