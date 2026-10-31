// 수선자의 한 해: 자연 회복 → 행동 선택(성격·상황 가중치) → 행동 실행.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>행동 선택 규칙.</summary>
    public static class ActionSystem
    {
        private const int ActionCount = 9;

        /// <summary>한 해의 행동을 고르고 실행한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Update(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Passive(world, cultivator);
            CultivatorAction action = Choose(world, cultivator);
            cultivator.LastAction = action;
            Perform(world, cultivator, action);
        }

        /// <summary>
        /// 행동 가중치. 성격 기본값에 부상·떠돌이·기예·자원·전쟁 상황을 곱한다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="weights">결과(길이 9).</param>
        public static void Weights(World world, Cultivator cultivator, Span<double> weights)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            for (int index = 0; index < ActionCount; index++)
            {
                weights[index] = Traits.ActionWeight(cultivator.Personality, (CultivatorAction)index);
            }

            if (cultivator.HealthRatio < 0.7)
            {
                weights[(int)CultivatorAction.Spar] *= 0.2;
                weights[(int)CultivatorAction.Recover] = 15;
            }

            if (cultivator.IsRogue)
            {
                weights[(int)CultivatorAction.Mission] = 0;
                weights[(int)CultivatorAction.Travel] *= 1.6;
                weights[(int)CultivatorAction.Explore] *= 1.3;
                weights[(int)CultivatorAction.Trade] *= 1.2;
            }

            weights[(int)CultivatorAction.Craft] *= cultivator.Art == ArtKind.None ? 0 : 1.3;
            if (cultivator.DiscipleIds.Count == 0)
            {
                weights[(int)CultivatorAction.Teach] = 0;
            }

            if (cultivator.SpiritStones < 30)
            {
                weights[(int)CultivatorAction.Explore] *= 1.5;
            }

            if (cultivator.Realm == 0)
            {
                weights[(int)CultivatorAction.Spar] *= 0.5;
            }

            ApplySituation(world, cultivator, weights);
        }

        private static void ApplySituation(World world, Cultivator cultivator, Span<double> weights)
        {
            if (world.War.AtWar && cultivator.OnWarDuty)
            {
                weights[(int)CultivatorAction.Spar] *= 2.0;
                weights[(int)CultivatorAction.Travel] = 0;
                weights[(int)CultivatorAction.Mission] *= 0.3;
            }

            if (world.Era.Kind == EraKind.BloodMoon && cultivator.Personality == Personality.Demonic)
            {
                weights[(int)CultivatorAction.Spar] *= 1.3;
            }
        }

        private static void Passive(World world, Cultivator cultivator)
        {
            double recovery = cultivator.MaxHealth * 0.015;
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is not null && world.Regions[cultivator.RegionId].Definition.HomeSectId == sect.Id)
            {
                recovery += cultivator.MaxHealth * 0.005 * sect.Level(FacilityKind.AlchemyHall);
            }

            cultivator.Injury = Math.Max(0, cultivator.Injury - recovery);
            if (cultivator.RecoveryYears > 0)
            {
                cultivator.RecoveryYears--;
            }

            if (cultivator.DaoHeart < 70)
            {
                Growth.AddDaoHeart(cultivator, 0.3);
            }
        }

        private static CultivatorAction Choose(World world, Cultivator cultivator)
        {
            if (cultivator.RecoveryYears > 0 || cultivator.HealthRatio < 0.35)
            {
                return CultivatorAction.Recover;
            }

            Span<double> weights = stackalloc double[ActionCount];
            Weights(world, cultivator, weights);
            return (CultivatorAction)world.Random.PickWeighted(weights);
        }

        private static void Perform(World world, Cultivator cultivator, CultivatorAction action)
        {
            switch (action)
            {
                case CultivatorAction.Explore:
                    ActionEffects.Explore(world, cultivator);
                    break;
                case CultivatorAction.Trade:
                    Commerce.Trade(world, cultivator);
                    break;
                case CultivatorAction.Craft:
                    Commerce.Craft(world, cultivator);
                    break;
                case CultivatorAction.Spar:
                    ActionEffects.Spar(world, cultivator);
                    break;
                case CultivatorAction.Mission:
                    ActionEffects.Mission(world, cultivator);
                    break;
                case CultivatorAction.Travel:
                    ActionEffects.Travel(world, cultivator);
                    break;
                case CultivatorAction.Teach:
                    RelationshipSystem.Teach(world, cultivator);
                    ActionEffects.Cultivate(world, cultivator, 0.5);
                    break;
                case CultivatorAction.Recover:
                    ActionEffects.Recover(world, cultivator);
                    break;
                default:
                    ActionEffects.Cultivate(world, cultivator, 1.0);
                    break;
            }
        }
    }
}
