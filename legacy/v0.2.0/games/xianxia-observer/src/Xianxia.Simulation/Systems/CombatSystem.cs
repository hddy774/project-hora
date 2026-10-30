// 전투력 계산과 1:1 대결. 원본은 재능 항이 경지 항보다 3배 컸다(평균 45% 대 14%). 여기서는 경지가 기준값을 정하고
// 재능·공법·법보·내공은 배율로만 작용해 "높은 경지가 대체로 이긴다"는 장르 규칙을 지킨다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>대결 종류.</summary>
    public enum BattleKind
    {
        /// <summary>같은 종문·지역 친선 비무.</summary>
        Spar = 0,

        /// <summary>휴전 중 종문 간 대비무.</summary>
        CrossSectSpar = 1,

        /// <summary>숙적·약탈 결투.</summary>
        Grudge = 2,

        /// <summary>전쟁 전투.</summary>
        War = 3,

        /// <summary>비경 보물 다툼.</summary>
        SecretRealm = 4,

        /// <summary>대비무 대회.</summary>
        Tournament = 5,
    }

    /// <summary>전투력 항목.</summary>
    /// <param name="RealmBase">경지 기준값.</param>
    /// <param name="Bone">근골 배율.</param>
    /// <param name="Technique">공법 배율.</param>
    /// <param name="Artifact">법보 배율.</param>
    /// <param name="InnerPower">내공 배율.</param>
    /// <param name="Experience">전투 경험 배율.</param>
    /// <param name="Condition">부상·도심 배율.</param>
    /// <param name="Sect">연무장·변이 영근 배율.</param>
    public readonly record struct PowerBreakdown(
        double RealmBase, double Bone, double Technique, double Artifact, double InnerPower,
        double Experience, double Condition, double Sect)
    {
        /// <summary>총 전투력.</summary>
        public double Total => RealmBase * Bone * Technique * Artifact * InnerPower * Experience * Condition * Sect;
    }

    /// <summary>대결 결과.</summary>
    /// <param name="Winner">승자.</param>
    /// <param name="Loser">패자.</param>
    /// <param name="Upset">전투력이 낮은 쪽이 이겼는가.</param>
    public readonly record struct DuelResult(Cultivator Winner, Cultivator Loser, bool Upset);

    /// <summary>전투 규칙.</summary>
    public static class CombatSystem
    {
        /// <summary>전투력 항목.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>항목.</returns>
        public static PowerBreakdown Breakdown(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            double training = sect is null ? 1.0 : 1 + (0.03 * sect.Level(FacilityKind.TrainingGround));
            double innerTarget = 200.0 * (cultivator.Realm + 1);
            return new PowerBreakdown(
                Realms.PowerBase(cultivator.Realm, cultivator.Stage),
                1 + (0.5 * cultivator.Bone / Traits.StatMax),
                1 + (0.04 * cultivator.TechniqueGrade),
                1 + (0.08 * cultivator.ArtifactGrade),
                1 + (0.3 * Math.Min(1, cultivator.InnerPower / innerTarget)),
                1 + Math.Min(0.2, cultivator.Wins * 0.004),
                (0.5 + (0.5 * cultivator.HealthRatio)) * (0.85 + (0.15 * cultivator.DaoHeart / Balance.DaoHeartMax)),
                training * (Traits.IsVariant(cultivator.Element) ? 1.05 : 1.0));
        }

        /// <summary>총 전투력.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>전투력.</returns>
        public static double Power(World world, Cultivator cultivator)
        {
            return Breakdown(world, cultivator).Total;
        }

        /// <summary>
        /// 대결 승패만 정한다(결과 적용은 하지 않음). 승률 = Pa^k / (Pa^k + Pb^k), k = 2.5.
        /// 공격부를 가지고 있으면 하나 써서 +15%, 오행 상극이면 +15%.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="first">한쪽.</param>
        /// <param name="second">다른 쪽.</param>
        /// <param name="useTalismans">공격부를 쓸 만한 진지한 싸움인가.</param>
        /// <param name="firstBonus">한쪽의 상황 배율(진법·호산대진 등).</param>
        /// <param name="secondBonus">다른 쪽의 상황 배율.</param>
        /// <returns>결과.</returns>
        public static DuelResult Resolve(World world, Cultivator first, Cultivator second, bool useTalismans,
            double firstBonus = 1.0, double secondBonus = 1.0)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);
            double firstBase = Power(world, first) * firstBonus;
            double secondBase = Power(world, second) * secondBonus;
            double firstPower = Effective(world, first, second, firstBase, useTalismans);
            double secondPower = Effective(world, second, first, secondBase, useTalismans);
            double firstWeight = Math.Pow(firstPower, Balance.DuelExponent);
            double secondWeight = Math.Pow(secondPower, Balance.DuelExponent);
            bool firstWins = world.Random.NextDouble() * (firstWeight + secondWeight) < firstWeight;
            Cultivator winner = firstWins ? first : second;
            Cultivator loser = firstWins ? second : first;
            bool upset = firstWins ? firstBase < secondBase : secondBase < firstBase;
            return new DuelResult(winner, loser, upset);
        }

        /// <summary>대결을 치르고 종류에 맞는 결과(부상·전리품·공헌·호칭)를 적용한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="first">한쪽.</param>
        /// <param name="second">다른 쪽.</param>
        /// <param name="kind">대결 종류.</param>
        /// <param name="firstBonus">한쪽의 상황 배율.</param>
        /// <param name="secondBonus">다른 쪽의 상황 배율.</param>
        /// <returns>결과.</returns>
        public static DuelResult Fight(World world, Cultivator first, Cultivator second, BattleKind kind,
            double firstBonus = 1.0, double secondBonus = 1.0)
        {
            bool serious = kind is BattleKind.Grudge or BattleKind.War or BattleKind.SecretRealm;
            DuelResult result = Resolve(world, first, second, serious, firstBonus, secondBonus);
            RecordOutcome(world, result, serious);
            if (serious)
            {
                BattleAftermath.ApplySerious(world, result, kind);
            }
            else
            {
                BattleAftermath.ApplyFriendly(world, result, kind);
            }

            return result;
        }

        private static double Effective(World world, Cultivator self, Cultivator foe, double basePower,
            bool useTalismans)
        {
            double power = basePower * world.Random.Range(0.85, 1.15);
            if (Traits.Overcomes(self.Element, foe.Element))
            {
                power *= Balance.ElementAdvantage;
            }

            int attack = (int)TalismanKind.Attack;
            if (useTalismans && self.Talismans[attack] > 0)
            {
                self.Talismans[attack]--;
                power *= 1.15;
            }

            return power;
        }

        private static void RecordOutcome(World world, DuelResult result, bool serious)
        {
            Cultivator winner = result.Winner;
            Cultivator loser = result.Loser;
            winner.Battles++;
            loser.Battles++;
            winner.Wins++;
            loser.Losses++;
            winner.WinStreak++;
            loser.WinStreak = 0;
            if (serious)
            {
                world.Totals.Battles++;
            }
            else
            {
                world.Totals.Spars++;
            }

            TitleSystem.CheckCombat(world, winner, loser, result.Upset);
            TitleSystem.CheckVeteran(world, loser);
        }
    }
}
