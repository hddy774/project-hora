// 정마대전: 3000년부터 11000년 주기로 천 년씩 벌어진다. 파병·접경지 전투·점령·종전(배상·대표 결투·전쟁 영웅).
// [원본 버그 수정] 원본은 전투 승자가 아니라 배열 순서상 앞의 수선자에게 점수를 줘 청운종이 모든 접경지를 차지했다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>전쟁 규칙.</summary>
    public static class WarSystem
    {
        /// <summary>점령에 필요한 세력 점수(원본 ±4).</summary>
        public const int CaptureThreshold = 4;

        /// <summary>세력 점수 한계(원본 ±10).</summary>
        public const int PointLimit = 10;

        /// <summary>해당 연도가 전쟁 기간인가.</summary>
        /// <param name="year">연도.</param>
        /// <returns>전쟁 기간이면 true.</returns>
        public static bool IsWarYear(int year)
        {
            return year >= Balance.FirstWarYear && (year
                - Balance.FirstWarYear) % Balance.WarCycle < Balance.WarDuration;
        }

        /// <summary>다음 전환(전쟁 시작 또는 종전)까지 남은 년수.</summary>
        /// <param name="year">연도.</param>
        /// <returns>남은 년수.</returns>
        public static int YearsUntilChange(int year)
        {
            if (year < Balance.FirstWarYear)
            {
                return Balance.FirstWarYear - year;
            }

            int phase = (year - Balance.FirstWarYear) % Balance.WarCycle;
            return phase < Balance.WarDuration ? Balance.WarDuration - phase : Balance.WarCycle - phase;
        }

        /// <summary>전쟁 시작·종전과 100년마다의 증원을 처리한다.</summary>
        /// <param name="world">세계.</param>
        public static void UpdatePhase(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            bool warYear = IsWarYear(world.Year);
            if (warYear && !world.War.AtWar)
            {
                StartWar(world);
            }
            else if (!warYear && world.War.AtWar)
            {
                WarSettlement.EndWar(world);
            }
            else if (world.War.AtWar && (world.Year - world.War.StartYear) % 100 == 50)
            {
                Dispatch(world);
            }
        }

        /// <summary>전쟁 중 각 접경지에서 파견된 제자끼리 싸운다.</summary>
        /// <param name="world">세계.</param>
        public static void ResolveBattles(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (!world.War.AtWar)
            {
                return;
            }

            foreach (int regionId in RegionCatalog.BorderIds)
            {
                Region region = world.Regions[regionId];
                for (int round = 0; round < 2; round++)
                {
                    Cultivator? orthodox = PickFighter(world, region, RegionCatalog.OrthodoxSectId);
                    Cultivator? demonic = PickFighter(world, region, RegionCatalog.DemonicSectId);
                    if (orthodox is null || demonic is null || !world.Random.Chance(0.6))
                    {
                        break;
                    }

                    FightBorderBattle(world, region, orthodox, demonic);
                }
            }
        }

        /// <summary>
        /// 접경지 전투 한 번. 지역을 지배 중인 쪽은 진법사와 호산대진의 도움을 받는다.
        /// 실제 승자의 종문에 세력 점수를 준다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="region">접경지.</param>
        /// <param name="first">한쪽.</param>
        /// <param name="second">다른 쪽.</param>
        public static void FightBorderBattle(World world, Region region, Cultivator first, Cultivator second)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(region);
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);
            DuelResult result = CombatSystem.Fight(world, first, second, BattleKind.War,
                DefenseBonus(world, region, first), DefenseBonus(world, region, second));
            region.Battles++;
            int winnerSect = result.Winner.SectId;
            if (winnerSect == RegionCatalog.OrthodoxSectId)
            {
                world.War.OrthodoxWins++;
            }
            else if (winnerSect == RegionCatalog.DemonicSectId)
            {
                world.War.DemonicWins++;
            }

            AddControl(world, region, winnerSect);
        }

        /// <summary>세력 점수를 더하고 임계값을 넘으면 지배 종문을 바꾼다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="region">지역.</param>
        /// <param name="sectId">점수를 얻은 종문.</param>
        public static void AddControl(World world, Region region, int sectId)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(region);
            int delta = sectId == RegionCatalog.OrthodoxSectId ? 1 : sectId == RegionCatalog.DemonicSectId ? -1 : 0;
            region.ControlPoints = Math.Clamp(region.ControlPoints + delta, -PointLimit, PointLimit);
            int controller = region.ControlPoints >= CaptureThreshold ? RegionCatalog.OrthodoxSectId
                : region.ControlPoints <= -CaptureThreshold ? RegionCatalog.DemonicSectId : region.ControllerSectId;
            if (controller == region.ControllerSectId)
            {
                return;
            }

            region.ControllerSectId = controller;
            string text = "[" + region.Name + "] " + Narrator.Affiliation(world, controller) + "이(가) 점령했다!";
            Narrator.Log(world, LogKind.War, text);
            Narrator.Chronicle(world, ChronicleKind.War, text);
            if (world.War.AtWar)
            {
                WarSettlement.Highlight(world, text);
            }
        }

        /// <summary>
        /// 각 종문이 경지 1 이상·장로 이하 제자 약 40%를 접경지에 파견한다(성격이 호전적일수록 잘 뽑힌다).
        /// </summary>
        /// <param name="world">세계.</param>
        public static void Dispatch(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (cultivator.IsRogue || cultivator.OnWarDuty || cultivator.Realm < 1
                    || cultivator.Rank >= SectRank.GrandElder
                    || cultivator.RecoveryYears > 0)
                {
                    continue;
                }

                double chance = cultivator.Personality switch
                {
                    Personality.Domineering or Personality.Demonic => 0.55,
                    Personality.Hermit => 0.15,
                    _ => 0.35,
                };
                if (world.Random.Chance(chance))
                {
                    cultivator.OnWarDuty = true;
                    LifeCycleSystem.MoveTo(world, cultivator, ChooseFront(world, cultivator.SectId));
                }
            }
        }

        private static void StartWar(World world)
        {
            world.War.AtWar = true;
            world.War.WarNumber++;
            world.War.StartYear = world.Year;
            world.War.OrthodoxWins = 0;
            world.War.DemonicWins = 0;
            world.War.Casualties = 0;
            world.War.Highlights.Clear();
            foreach (Cultivator cultivator in world.Cultivators)
            {
                cultivator.WarMerit = 0;
            }

            Dispatch(world);
            string text = "제" + world.War.WarNumber + "차 정마대전이 발발했다. 천 년의 전쟁이 시작된다.";
            Narrator.Log(world, LogKind.War, text);
            Narrator.Chronicle(world, ChronicleKind.War, text);
        }

        private static int ChooseFront(World world, int sectId)
        {
            Span<double> weights = stackalloc double[RegionCatalog.BorderIds.Count];
            for (int index = 0; index < weights.Length; index++)
            {
                Region region = world.Regions[RegionCatalog.BorderIds[index]];
                weights[index] = region.ControllerSectId == sectId ? 1.0 : region.ControllerSectId == 0 ? 1.5 : 2.0;
            }

            return RegionCatalog.BorderIds[world.Random.PickWeighted(weights)];
        }

        private static Cultivator? PickFighter(World world, Region region, int sectId)
        {
            List<Cultivator> residents = region.Residents;
            for (int attempt = 0; attempt < 8 && residents.Count > 0; attempt++)
            {
                Cultivator candidate = residents[world.Random.NextInt(residents.Count)];
                if (candidate.SectId == sectId && candidate.Realm >= 1 && candidate.RecoveryYears == 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// 지형·방어 보정: 마도 혈전 +10%, 연패 와신상담 최대 +90%, 보급선이 가까운 쪽 +10%,
        /// 지역을 지배 중인 쪽은 진법사와 호산대진의 도움을 받는다.
        /// 접경지마다 유리한 쪽이 달라 한 종문이 모든 접경지를 계속 쥐기 어렵다.
        /// </summary>
        private static double DefenseBonus(World world, Region region, Cultivator cultivator)
        {
            if (cultivator.IsRogue)
            {
                return 1.0;
            }

            Sect own = world.FindSect(cultivator.SectId)!;
            // 이전최대15%는10시드 중6시드의3연패를 막지 못했다. 손실 뒤 회복 여지를 강화한다.
            double resolve = 1 + (0.30 * Math.Min(3, own.LosingStreak));
            double bonus = SectRules.IdeologyWarPower(own.Orthodox) * resolve
                * (region.Definition.NearSectId == cultivator.SectId ? 1.1 : 1.0);
            if (region.ControllerSectId != cultivator.SectId)
            {
                return bonus;
            }

            return bonus * Crafting.FormationSupport(region, own.Id) * (1 + (0.05
                * own.Level(FacilityKind.GuardianArray)));
        }
    }
}
