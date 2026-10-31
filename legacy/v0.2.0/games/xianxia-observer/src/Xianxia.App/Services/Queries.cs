// 화면용 집계: 종문 요약, 지역 거주자, 강자 순위. 세계를 바꾸지 않는 읽기 전용 계산만 둔다.
// [성능] 1,000명 기준 한 번 훑는 데 1ms 안팎이라 화면 갱신(초당 4번)마다 다시 계산해도 된다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Services
{
    /// <summary>종문 요약.</summary>
    /// <param name="Members">구성원 수.</param>
    /// <param name="Power">총 전투력.</param>
    /// <param name="AverageRealm">평균 경지(0~8).</param>
    /// <param name="TopRealm">최고 경지.</param>
    /// <param name="Borders">점령한 접경지 수.</param>
    /// <param name="RealmCounts">경지별 인원.</param>
    /// <param name="OnWarDuty">파견 중인 인원.</param>
    internal sealed record SectSummary(int Members, double Power, double AverageRealm, int TopRealm, int Borders,
        int[] RealmCounts, int OnWarDuty);

    /// <summary>집계 도우미.</summary>
    internal static class Queries
    {
        /// <summary>종문(또는 산수, 번호 0) 요약.</summary>
        /// <param name="world">세계.</param>
        /// <param name="sectId">종문 번호(0이면 산수).</param>
        /// <returns>요약.</returns>
        internal static SectSummary Summarize(World world, int sectId)
        {
            int members = 0;
            int onDuty = 0;
            int topRealm = 0;
            double power = 0;
            double realmSum = 0;
            int[] realms = new int[Realms.Count];
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (cultivator.SectId != sectId)
                {
                    continue;
                }

                members++;
                realmSum += cultivator.Realm;
                realms[cultivator.Realm]++;
                topRealm = Math.Max(topRealm, cultivator.Realm);
                power += CombatSystem.Power(world, cultivator);
                if (cultivator.OnWarDuty)
                {
                    onDuty++;
                }
            }

            int borders = 0;
            foreach (int regionId in RegionCatalog.BorderIds)
            {
                if (sectId != 0 && world.Regions[regionId].ControllerSectId == sectId)
                {
                    borders++;
                }
            }

            return new SectSummary(members, power, members == 0 ? 0 : realmSum / members, topRealm, borders, realms,
                onDuty);
        }

        /// <summary>전투력 상위 수선자.</summary>
        /// <param name="world">세계.</param>
        /// <param name="count">인원.</param>
        /// <param name="sectId">종문 제한(-1이면 전체, 0이면 산수).</param>
        /// <returns>전투력 내림차순.</returns>
        internal static List<(Cultivator Cultivator, double Power)> Strongest(World world, int count,
            int sectId = -1)
        {
            List<(Cultivator Cultivator, double Power)> list = new(world.Cultivators.Count);
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (sectId < 0 || cultivator.SectId == sectId)
                {
                    list.Add((cultivator, CombatSystem.Power(world, cultivator)));
                }
            }

            list.Sort((left, right) => right.Power.CompareTo(left.Power) != 0
                ? right.Power.CompareTo(left.Power) : left.Cultivator.Id.CompareTo(right.Cultivator.Id));
            if (list.Count > count)
            {
                list.RemoveRange(count, list.Count - count);
            }

            return list;
        }

        /// <summary>지역별 거주자 수.</summary>
        /// <param name="world">세계.</param>
        /// <returns>지역 번호 순서의 인원.</returns>
        internal static int[] ResidentCounts(World world)
        {
            int[] counts = new int[world.Regions.Count];
            for (int index = 0; index < world.Regions.Count; index++)
            {
                counts[index] = world.Regions[index].Residents.Count;
            }

            return counts;
        }

        /// <summary>지역에 열린 비경(없으면 null).</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역.</param>
        /// <returns>비경.</returns>
        internal static SecretRealmState? SecretRealmAt(World world, int regionId)
        {
            foreach (SecretRealmState realm in world.SecretRealms)
            {
                if (realm.RegionId == regionId)
                {
                    return realm;
                }
            }

            return null;
        }

        /// <summary>지역에 이는 수조(없으면 null).</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역.</param>
        /// <returns>수조.</returns>
        internal static BeastTideState? BeastTideAt(World world, int regionId)
        {
            foreach (BeastTideState tide in world.BeastTides)
            {
                if (tide.RegionId == regionId)
                {
                    return tide;
                }
            }

            return null;
        }

        /// <summary>죽은 영혼 포함 이름 찾기: 살아 있으면 현생 이름, 아니면 영혼 기록 이름.</summary>
        /// <param name="world">세계.</param>
        /// <param name="soulId">영혼 번호.</param>
        /// <returns>이름(모르면 "알 수 없음").</returns>
        internal static string NameOf(World world, int soulId)
        {
            return world.Find(soulId)?.DisplayName ?? world.FindSoul(soulId)?.Name ?? "알 수 없음";
        }
    }
}
