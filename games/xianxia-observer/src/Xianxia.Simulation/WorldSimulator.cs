// 한 해(1틱) 진행 순서. 순서를 바꾸면 같은 시드의 결과가 달라지므로 시스템을 끝에 추가한다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation
{
    /// <summary>세계를 한 해씩 진행한다. 화면·저장과 무관한 순수 규칙이다.</summary>
    public static class WorldSimulator
    {
        /// <summary>
        /// 한 해를 진행한다.
        /// [결정론] 수선자는 자리 순서대로, 세계 사건은 아래 고정 순서대로 처리한다. 난수는 세계 난수 하나만 쓴다.
        /// </summary>
        /// <param name="world">세계.</param>
        public static void Step(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            world.Year++;
            EraSystem.Update(world);
            WarSystem.UpdatePhase(world);
            List<Cultivator> cultivators = world.Cultivators;
            for (int slot = 0; slot < cultivators.Count; slot++)
            {
                UpdateCultivator(world, cultivators[slot]);
            }

            WarSystem.ResolveBattles(world);
            SecretRealmSystem.Update(world);
            BeastTideSystem.Update(world);
            AuctionSystem.Update(world);
            TournamentSystem.Update(world);
            SectSystem.Update(world);
            StatisticsSystem.Update(world);
            InterventionSystem.Regenerate(world);
        }

        /// <summary>여러 해를 한 번에 진행한다(도구·테스트용).</summary>
        /// <param name="world">세계.</param>
        /// <param name="years">년수.</param>
        public static void Run(World world, int years)
        {
            for (int year = 0; year < years; year++)
            {
                Step(world);
            }
        }

        /// <summary>
        /// 수선자의 한 해: 노화(좌화하면 윤회로 끝) → 전생 각성 → 인연 → 행동 → 연수단 → 돌파.
        /// </summary>
        private static void UpdateCultivator(World world, Cultivator cultivator)
        {
            if (LifeCycleSystem.Age(world, cultivator))
            {
                return;
            }

            LifeCycleSystem.TryAwaken(world, cultivator);
            RelationshipSystem.Update(world, cultivator);
            ActionSystem.Update(world, cultivator);
            TakeLongevityPill(world, cultivator);
            BreakthroughSystem.TryAdvance(world, cultivator);
        }

        /// <summary>수명의 80%를 넘기면 가진 연수단을 먹는다(한 생 최대 3알, 하나당 수명 +5%).</summary>
        private static void TakeLongevityPill(World world, Cultivator cultivator)
        {
            int longevity = (int)PillKind.Longevity;
            if (cultivator.Pills[longevity] == 0 || cultivator.LongevityPillsTaken >= Lifespans.MaxLongevityPills
                || Lifespans.AgeRatio(cultivator) < 0.8)
            {
                return;
            }

            cultivator.Pills[longevity]--;
            cultivator.LongevityPillsTaken++;
            cultivator.Lifespan = Lifespans.For(cultivator);
            Narrator.Log(world, LogKind.Craft, cultivator.Name + "이(가) 연수단을 먹고 수명을 늘렸다. (수명 "
                + KoreanNumber.Grouped(cultivator.Lifespan) + "년)", cultivator, notable: true);
            Narrator.Biography(world, cultivator, "연수단으로 수명을 늘렸다.");
        }
    }
}
