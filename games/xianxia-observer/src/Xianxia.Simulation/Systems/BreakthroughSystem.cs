// 돌파 규칙: 소경지(단계) 돌파, 대경지 돌파, 화신경 이상의 천겁, 반선경 대원만의 비승.
// 원본 확률식은 재능 항이 커서 시도의 19%가 99.5% 상한에 붙었다. 여기서는 경지별 기본 확률에 배율을 곱하는 구조로 바꿨다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>돌파 규칙.</summary>
    public static class BreakthroughSystem
    {
        /// <summary>파경단 하나의 대경지 돌파 배율.</summary>
        public const double PillBoost = 1.35;

        /// <summary>영기가 찼으면 한 해에 한 번 돌파를 시도한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void TryAdvance(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            long requirement = cultivator.QiRequirement;
            if (cultivator.RecoveryYears > 0 || cultivator.Qi < requirement)
            {
                return;
            }

            if (cultivator.Stage < Realms.PeakStage)
            {
                AttemptMinor(world, cultivator, requirement);
            }
            else if (cultivator.Realm < Realms.Max)
            {
                AttemptMajor(world, cultivator, requirement);
            }
            else
            {
                AscensionSystem.Attempt(world, cultivator, requirement);
            }
        }

        /// <summary>소경지 돌파 확률(50~97%).</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>확률.</returns>
        public static double MinorChance(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            double chance = 0.82 + (0.12 * cultivator.Comprehension / Traits.StatMax) - (0.2 * (1
                - cultivator.HealthRatio));
            return Math.Clamp(chance, 0.5, 0.97);
        }

        /// <summary>대경지 돌파 확률(파경단 제외, 1~90%).</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>확률.</returns>
        public static double MajorChance(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            int target = cultivator.Realm + 1;
            Sect? sect = world.FindSect(cultivator.SectId);
            double chance = Realms.MajorBreakthroughBase(cultivator.Realm)
                * (0.6 + (0.8 * (cultivator.Comprehension + cultivator.Root) / (2.0 * Traits.StatMax)))
                * Techniques.BreakthroughModifier(cultivator.TechniqueGrade, target)
                * (1 + (0.5 * cultivator.Insight / Balance.InsightMax))
                * (0.6 + (0.5 * cultivator.DaoHeart / Balance.DaoHeartMax))
                * (1 - (0.3 * (1 - cultivator.HealthRatio)))
                * EraSystem.BreakthroughFactor(world);
            if (sect is not null)
            {
                chance *= 1.1 * (1 + (0.02 * sect.Level(FacilityKind.SpiritArray)))
                    * SectRules.IdeologyBreakthrough(sect.Orthodox, cultivator.Realm);
            }

            if (world.Find(cultivator.MasterId) is not null)
            {
                chance *= 1.1;
            }

            if (Lifespans.AgeRatio(cultivator) > 0.85)
            {
                chance *= 0.8;
            }

            return Math.Clamp(chance, 0.01, 0.8);
        }

        private static void AttemptMinor(World world, Cultivator cultivator, long requirement)
        {
            if (!world.Random.Chance(MinorChance(cultivator)))
            {
                cultivator.Qi -= requirement * Balance.MinorFailureQiLoss;
                Growth.AddDaoHeart(cultivator, -2);
                return;
            }

            cultivator.Qi -= requirement;
            cultivator.Stage++;
            cultivator.MinorBreakthroughs++;
            cultivator.InnerPower += 2 * (cultivator.Realm + 1);
            SectSystem.AddContribution(world, cultivator, 20 * (cultivator.Realm + 1));
            RecordSystem.NoteProgress(world, cultivator);
            if (world.IsFollowed(cultivator.Id))
            {
                Narrator.Log(world, LogKind.Breakthrough, cultivator.Name + " " + cultivator.RealmText + " 돌파.",
                    cultivator);
            }
            else
            {
                world.Log.CountOnly(LogKind.Breakthrough);
            }
        }

        private static void AttemptMajor(World world, Cultivator cultivator, long requirement)
        {
            double chance = MajorChance(world, cultivator);
            int pill = (int)PillKind.Breakthrough;
            bool usedPill = cultivator.Pills[pill] > 0;
            if (usedPill)
            {
                cultivator.Pills[pill]--;
                chance = Math.Min(0.95, chance * PillBoost);
            }

            int target = cultivator.Realm + 1;
            if (!world.Random.Chance(chance))
            {
                FailMajor(world, cultivator, requirement, usedPill);
                return;
            }

            if (target >= Realms.FirstTribulationRealm && !TribulationSystem.Endure(world, cultivator, target))
            {
                cultivator.Qi = 0;
                return;
            }

            AdvanceRealm(world, cultivator, requirement, usedPill);
        }

        private static void FailMajor(World world, Cultivator cultivator, long requirement, bool usedPill)
        {
            cultivator.Qi -= requirement * Balance.MajorFailureQiLoss;
            cultivator.Insight *= 0.5;
            Growth.AddDaoHeart(cultivator, -6);
            Setbacks.Injure(world, cultivator, world.Random.Range(0.1, 0.2), "돌파 실패");
            Sect? sect = world.FindSect(cultivator.SectId);
            double backlash = 0.06 + (sect is null ? 0 : SectRules.IdeologyBacklash(sect.Orthodox))
                + (cultivator.DaoHeart < 40 ? 0.06 : 0);
            string target = Realms.Name(cultivator.Realm + 1);
            if (world.Random.Chance(backlash))
            {
                string before = cultivator.RealmText;
                Setbacks.DropStage(world, cultivator);
                Setbacks.Injure(world, cultivator, 0.3, "주화입마");
                string text = cultivator.Name + "이(가) " + target + " 돌파 중 주화입마에 빠졌다. " + before + " → "
                    + cultivator.RealmText;
                Narrator.Log(world, LogKind.Breakthrough, text, cultivator, notable: true);
                Narrator.Biography(world, cultivator, target + " 돌파 중 주화입마에 빠졌다.");
                return;
            }

            bool notable = cultivator.Realm >= 4 || world.IsFollowed(cultivator.Id);
            if (notable || world.LogRandom.Chance(0.25))
            {
                string pillText = usedPill ? "(파경단 사용) " : string.Empty;
                Narrator.Log(world, LogKind.Breakthrough, cultivator.Name + "이(가) " + pillText + target + " 돌파에 실패했다.",
                    cultivator, notable: notable);
            }
        }

        /// <summary>대경지를 올린다. 수명·내공·공헌·기록·호칭·도호를 함께 처리한다.</summary>
        private static void AdvanceRealm(World world, Cultivator cultivator, long requirement, bool usedPill)
        {
            cultivator.Qi = Math.Max(0, cultivator.Qi - requirement);
            cultivator.Realm++;
            cultivator.Stage = 0;
            cultivator.Lifespan = Math.Max(cultivator.Lifespan, Lifespans.For(cultivator));
            cultivator.Injury *= 0.5;
            cultivator.Insight = 0;
            cultivator.InnerPower += 10 * (cultivator.Realm + 1);
            cultivator.MajorBreakthroughs++;
            Growth.AddDaoHeart(cultivator, 5);
            world.Totals.MajorBreakthroughs++;
            double merit = (100 * (cultivator.Realm + 1)) + (cultivator.MajorBreakthroughs * 10);
            SectSystem.AddContribution(world, cultivator, merit);
            Cultivator? master = world.Find(cultivator.MasterId);
            if (master is not null)
            {
                SectSystem.AddContribution(world, master, merit * 0.5);
            }

            if (cultivator.Realm >= 3 && cultivator.DaoName is null)
            {
                cultivator.DaoName = CultivatorFactory.DaoName(world.Random, cultivator.Realm);
                Narrator.Biography(world, cultivator, "도호 '" + cultivator.DaoName + "'를 얻었다.");
            }

            bool firstInWorld = RecordSystem.NoteProgress(world, cultivator);
            TitleSystem.CheckBreakthrough(world, cultivator);
            string pillText = usedPill ? "파경단의 힘으로 " : string.Empty;
            string text = cultivator.DisplayName + "이(가) " + pillText + Realms.Name(cultivator.Realm) + "에 올랐다! ("
                + cultivator.Age + "세)";
            Narrator.Log(world, LogKind.Breakthrough, text, cultivator, notable: true);
            Narrator.Biography(world, cultivator, pillText + Realms.Name(cultivator.Realm) + "에 올랐다.");
            if (cultivator.Realm >= 6 && !firstInWorld)
            {
                Narrator.Chronicle(world, ChronicleKind.FirstRealm, text, cultivator);
            }
        }
    }
}
