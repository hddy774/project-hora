// 수련 속도(영기·깨달음) 계산. 원본의 재능·공법·지역·직위·기명제자 배율을 계승하고, 무시되던 이념 배율을 적용한다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>수련 속도 항목별 배율(화면 "수행" 탭 설명용).</summary>
    /// <param name="Base">경지 기본값.</param>
    /// <param name="Root">영근 배율.</param>
    /// <param name="Technique">공법 배율(속성 일치 포함).</param>
    /// <param name="Place">지역·시설·점령 배율.</param>
    /// <param name="Rank">직위 배율.</param>
    /// <param name="Ideology">이념 배율.</param>
    /// <param name="Era">시대 배율.</param>
    /// <param name="Bonds">사제·도려 배율.</param>
    /// <param name="Condition">도심·부상·노쇠 배율.</param>
    public readonly record struct GrowthBreakdown(
        double Base, double Root, double Technique, double Place, double Rank,
        double Ideology, double Era, double Bonds, double Condition)
    {
        /// <summary>한 해 수련 영기(곱).</summary>
        public double Total => Base * Root * Technique * Place * Rank * Ideology * Era * Bonds * Condition;
    }

    /// <summary>수련 속도 규칙.</summary>
    public static class Growth
    {
        /// <summary>한 해 수련으로 얻는 영기의 항목별 배율.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>배율 묶음.</returns>
        public static GrowthBreakdown Breakdown(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            double technique = Techniques.GainMultiplier(cultivator.TechniqueGrade)
                * (cultivator.TechniqueElement == cultivator.Element ? 1.1 : 1.0)
                * (Traits.IsVariant(cultivator.Element) ? 1.05 : 1.0);
            return new GrowthBreakdown(
                Balance.BaseQiGain * Math.Pow(Balance.QiGainPerRealm, cultivator.Realm),
                0.5 + (1.5 * Math.Pow(cultivator.Root / 500.0, 1.1)),
                technique,
                PlaceFactor(world, cultivator, sect),
                SectRules.GrowthMultiplier(cultivator.Rank),
                sect is null ? 1.0 : SectRules.IdeologyGrowth(sect.Orthodox, cultivator.Realm),
                EraSystem.GrowthFactor(world, sect),
                BondFactor(world, cultivator),
                ConditionFactor(cultivator));
        }

        /// <summary>한 해 수련 영기.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>영기.</returns>
        public static double QiPerYear(World world, Cultivator cultivator)
        {
            return Breakdown(world, cultivator).Total;
        }

        /// <summary>한 해 수련으로 얻는 깨달음(오성 비례).</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>깨달음.</returns>
        public static double InsightPerYear(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            return 0.5 + (2.5 * cultivator.Comprehension / 500.0);
        }

        /// <summary>깨달음을 더한다(최대 1000).</summary>
        /// <param name="cultivator">수선자.</param>
        /// <param name="amount">양.</param>
        public static void AddInsight(Cultivator cultivator, double amount)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.Insight = Math.Clamp(cultivator.Insight + amount, 0, Balance.InsightMax);
        }

        /// <summary>도심을 바꾼다(0~100).</summary>
        /// <param name="cultivator">수선자.</param>
        /// <param name="amount">변화량.</param>
        public static void AddDaoHeart(Cultivator cultivator, double amount)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.DaoHeart = Math.Clamp(cultivator.DaoHeart + amount, 0, Balance.DaoHeartMax);
        }

        private static double PlaceFactor(World world, Cultivator cultivator, Sect? sect)
        {
            Region region = world.Regions[cultivator.RegionId];
            double factor = Math.Clamp(region.Aura, 0.3, 2.0);
            if (sect is null)
            {
                return factor;
            }

            if (region.ControllerSectId == sect.Id)
            {
                // 원본의 점령 접경지 ×1.3·영지 ×1.15를 다른 배율과 곱해도 과하지 않게 ×1.15·×1.05로 줄였다.
                // 영맥진 단계마다 +4%.
                bool home = region.Definition.HomeSectId == sect.Id;
                factor *= home ? 1.05 * (1 + (0.04 * sect.Level(FacilityKind.SpiritArray))) : 1.15;
            }

            return factor;
        }

        private static double BondFactor(World world, Cultivator cultivator)
        {
            double factor = 1.0;
            Cultivator? master = world.Find(cultivator.MasterId);
            if (master is not null)
            {
                factor *= 1.15;
            }

            Cultivator? companion = world.Find(cultivator.CompanionId);
            if (companion is not null && companion.RegionId == cultivator.RegionId)
            {
                factor *= 1.1;
            }

            return factor;
        }

        private static double ConditionFactor(Cultivator cultivator)
        {
            double dao = 0.7 + (0.3 * cultivator.DaoHeart / Balance.DaoHeartMax);
            double health = 0.4 + (0.6 * cultivator.HealthRatio);
            double vitality = Lifespans.AgeRatio(cultivator) > 0.9 ? 0.8 : 1.0;
            return dao * health * vitality;
        }
    }
}
