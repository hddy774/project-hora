// 통계 표본 수집과 100년 주기 정리(기록 갱신·숙적 정리).
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>통계 규칙.</summary>
    public static class StatisticsSystem
    {
        /// <summary>정기 처리.</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.Year % world.Statistics.Interval == 0)
            {
                Sample(world);
            }

            if (world.Year % Balance.StatisticsInterval == 0)
            {
                RecordSystem.UpdatePeriodic(world);
                RelationshipSystem.Prune(world);
            }
        }

        /// <summary>현재 세계를 표본으로 남긴다.</summary>
        /// <param name="world">세계.</param>
        public static void Sample(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            world.Statistics.Add(Snapshot(world));
        }

        /// <summary>현재 세계 통계를 계산한다(저장하지 않음, 화면용).</summary>
        /// <param name="world">세계.</param>
        /// <returns>표본.</returns>
        public static StatSample Snapshot(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            StatSample sample = new() { Year = world.Year, RealmCounts = new List<int>(new int[Realms.Count]) };
            double orthodoxRealm = 0;
            double demonicRealm = 0;
            foreach (Cultivator cultivator in world.Cultivators)
            {
                sample.RealmCounts[cultivator.Realm]++;
                double level = cultivator.Realm + (cultivator.Stage / (double)Realms.StageCount);
                double power = CombatSystem.Power(world, cultivator);
                if (cultivator.SectId == RegionCatalog.OrthodoxSectId)
                {
                    sample.OrthodoxMembers++;
                    orthodoxRealm += level;
                    sample.OrthodoxPower += power;
                }
                else if (cultivator.SectId == RegionCatalog.DemonicSectId)
                {
                    sample.DemonicMembers++;
                    demonicRealm += level;
                    sample.DemonicPower += power;
                }
                else
                {
                    sample.Rogues++;
                }
            }

            sample.OrthodoxAverageRealm = orthodoxRealm / Math.Max(1, sample.OrthodoxMembers);
            sample.DemonicAverageRealm = demonicRealm / Math.Max(1, sample.DemonicMembers);
            sample.OrthodoxTreasury = world.Sects[0].Treasury;
            sample.DemonicTreasury = world.Sects[1].Treasury;
            foreach (int regionId in RegionCatalog.BorderIds)
            {
                int controller = world.Regions[regionId].ControllerSectId;
                sample.OrthodoxBorders += controller == RegionCatalog.OrthodoxSectId ? 1 : 0;
                sample.DemonicBorders += controller == RegionCatalog.DemonicSectId ? 1 : 0;
            }

            return sample;
        }
    }
}
