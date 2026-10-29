// 사건이 겹치거나 순서가 바뀌어도 활성 목록에서 지역 효과를 복원한다. 난수를 쓰지 않는다.
using System;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>지역의 활성 사건에서 영기 보정과 상태를 계산한다.</summary>
    public static class RegionEvents
    {
        /// <summary>동일 종류는 한 번만 적용하고 수조 상태를 먼저 표시한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역 번호.</param>
        public static void Refresh(World world, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            SecretRealmState? secret = null;
            BeastTideState? tide = null;
            foreach (SecretRealmState entry in world.SecretRealms)
            {
                if (entry.RegionId == regionId)
                {
                    secret = entry;
                    break;
                }
            }

            foreach (BeastTideState entry in world.BeastTides)
            {
                if (entry.RegionId == regionId)
                {
                    tide = entry;
                    break;
                }
            }

            Region region = world.Regions[regionId];
            region.AuraModifier = (secret is null ? 1 : 1.2) * (tide is null ? 1 : 0.8);
            region.Status = tide is not null ? "수조: " + tide.BeastName
                : secret is not null ? "비경 개방: " + secret.Name : "평온";
        }

        /// <summary>저장된 오래된 보정 대신 모든 지역의 현재 목록을 반영한다.</summary>
        /// <param name="world">세계.</param>
        public static void RefreshAll(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            for (int index = 0; index < world.Regions.Count; index++)
            {
                Refresh(world, index);
            }
        }
    }
}
