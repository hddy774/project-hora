// 수조: 영수 떼가 지역을 습격한다. 거주자와 종문 증원이 막아 내면 격퇴, 아니면 시간이 지나 물러간다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>수조 규칙.</summary>
    public static class BeastTideSystem
    {
        private const double StartChancePerYear = 0.0008;
        private const int MaxDefendersPerYear = 30;
        private const int Reinforcements = 6;

        /// <summary>새 수조를 일으킬지 정하고 진행 중인 수조를 처리한다.</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.BeastTides.Count < 2 && world.Random.Chance(StartChancePerYear
                * EraSystem.BeastTideFactor(world)))
            {
                Start(world, world.Random.Pick(RegionCatalog.EventRegionIds), summoned: false);
            }

            for (int index = world.BeastTides.Count - 1; index >= 0; index--)
            {
                BeastTideState tide = world.BeastTides[index];
                Defend(world, tide);
                if (tide.Strength <= 0 || world.Year >= tide.EndYear)
                {
                    Finish(world, tide);
                    world.BeastTides.RemoveAt(index);
                }
            }
        }

        /// <summary>수조를 일으킨다. 영수 수장의 경지는 세계 평균보다 한두 경지 높다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">습격 지역.</param>
        /// <param name="summoned">천기 개입으로 불렀는가.</param>
        /// <returns>수조.</returns>
        public static BeastTideState Start(World world, int regionId, bool summoned)
        {
            ArgumentNullException.ThrowIfNull(world);
            int realm = Math.Clamp(AverageRealm(world) + world.Random.Range(1, 2), 1, Realms.Max);
            double strength = 40 * Realms.PowerBase(realm, 2);
            BeastTideState tide = new()
            {
                BeastName = world.Random.Pick(NameData.BeastNames),
                RegionId = regionId,
                StartYear = world.Year,
                EndYear = world.Year + world.Random.Range(20, 50),
                InitialStrength = strength,
                Strength = strength,
                Realm = realm,
                Summoned = summoned,
            };
            world.BeastTides.Add(tide);
            Region region = world.Regions[regionId];
            region.AuraModifier = 0.8;
            region.Status = "수조: " + tide.BeastName;
            SendReinforcements(world, region, realm);
            string text = "[" + region.Name + "] " + tide.BeastName + "(" + Realms.Name(realm)
                + ")이(가) 영수 떼를 이끌고 습격했다!";
            Narrator.Log(world, LogKind.BeastTide, text);
            Narrator.Chronicle(world, ChronicleKind.BeastTide, text);
            return tide;
        }

        /// <summary>지역에서 수조가 진행 중인가.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역.</param>
        /// <returns>진행 중이면 true.</returns>
        public static bool IsRaging(World world, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (BeastTideState tide in world.BeastTides)
            {
                if (tide.RegionId == regionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int AverageRealm(World world)
        {
            double total = 0;
            foreach (Cultivator cultivator in world.Cultivators)
            {
                total += cultivator.Realm;
            }

            return (int)Math.Round(total / Math.Max(1, world.Cultivators.Count));
        }

        /// <summary>지역을 지배하는 종문이 강한 제자 여섯을 보낸다.</summary>
        private static void SendReinforcements(World world, Region region, int realm)
        {
            int sectId = region.ControllerSectId;
            if (sectId == 0)
            {
                return;
            }

            List<Cultivator> strong = new();
            foreach (Cultivator member in world.Cultivators)
            {
                if (member.SectId == sectId && member.Realm >= realm - 1 && member.RecoveryYears == 0
                    && member.Rank < SectRank.GrandElder && !member.OnWarDuty)
                {
                    strong.Add(member);
                }
            }

            strong.Sort((left, right) => right.Realm != left.Realm ? right.Realm.CompareTo(left.Realm)
                : left.Id.CompareTo(right.Id));
            for (int index = 0; index < strong.Count && index < Reinforcements; index++)
            {
                LifeCycleSystem.MoveTo(world, strong[index], region.Id);
            }
        }

        private static void Defend(World world, BeastTideState tide)
        {
            Region region = world.Regions[tide.RegionId];
            double beastPower = Realms.PowerBase(tide.Realm, 2) * 1.2;
            double defense = Crafting.FormationSupport(region, 0);
            List<Cultivator> defenders = new(region.Residents);
            int fights = 0;
            foreach (Cultivator defender in defenders)
            {
                if (fights >= MaxDefendersPerYear || tide.Strength <= 0)
                {
                    break;
                }

                if (defender.Realm < tide.Realm - 2 || defender.RecoveryYears > 0)
                {
                    continue;
                }

                fights++;
                double power = CombatSystem.Power(world, defender) * defense;
                double weight = Math.Pow(power, Balance.DuelExponent);
                bool won = world.Random.NextDouble() * (weight + Math.Pow(beastPower, Balance.DuelExponent)) < weight;
                if (won)
                {
                    double damage = power * world.Random.Range(0.5, 1.0);
                    tide.Strength -= damage;
                    Credit(world, tide, defender, damage / beastPower * 20);
                }
                else
                {
                    Setbacks.Injure(world, defender, world.Random.Range(0.15, 0.35), "수조");
                }
            }
        }

        private static void Credit(World world, BeastTideState tide, Cultivator defender, double merit)
        {
            int index = tide.DefenderIds.IndexOf(defender.Id);
            if (index < 0)
            {
                tide.DefenderIds.Add(defender.Id);
                tide.DefenderMerit.Add(0);
                index = tide.DefenderIds.Count - 1;
            }

            tide.DefenderMerit[index] += merit;
            SectSystem.AddContribution(world, defender, merit * 5);
            if (world.Random.Chance(0.3))
            {
                defender.BeastCores++;
            }
        }

        private static void Finish(World world, BeastTideState tide)
        {
            Region region = world.Regions[tide.RegionId];
            region.AuraModifier = 1.0;
            region.Status = "평온";
            if (tide.Strength > 0)
            {
                Narrator.Log(world, LogKind.BeastTide, "[" + region.Name + "] " + tide.BeastName + "의 영수 떼가 물러갔다.");
                return;
            }

            int best = 0;
            for (int index = 1; index < tide.DefenderMerit.Count; index++)
            {
                best = tide.DefenderMerit[index] > tide.DefenderMerit[best] ? index : best;
            }

            Cultivator? hero = tide.DefenderIds.Count > 0 ? world.Find(tide.DefenderIds[best]) : null;
            string text = "[" + region.Name + "] " + tide.BeastName + "의 수조를 격퇴했다."
                + (hero is null ? string.Empty : " 최대 공훈 " + hero.DisplayName + ".");
            Narrator.Log(world, LogKind.BeastTide, text, hero, notable: true);
            Narrator.Chronicle(world, ChronicleKind.BeastTide, text, hero);
            if (hero is not null)
            {
                TitleSystem.Award(world, hero, TitleId.BeastSlayer);
                Narrator.Biography(world, hero, tide.BeastName + "의 수조를 막아 냈다.");
            }
        }
    }
}
