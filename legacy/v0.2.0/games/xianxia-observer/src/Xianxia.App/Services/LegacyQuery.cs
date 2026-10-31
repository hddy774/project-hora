// 영혼 업적 순위. 최종 순위는 원본처럼 최고 점수 순위와 5생 합 순위의 합으로 정한다.
using System.Collections.Generic;
using System.Linq;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Services
{
    /// <summary>동점은 영혼 번호로 고정하는 업적 순위 계산.</summary>
    internal static class LegacyQuery
    {
        /// <summary>종료한 생의 업적을 정렬한다. 현생의 미확정 점수는 섞지 않는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="sort">최고 0, 상위 5생 합 1, 두 순위 합 2.</param>
        /// <returns>영혼과 두 개별 순위.</returns>
        internal static List<(SoulRecord Soul, int BestRank, int FiveRank)> Select(World world, int sort)
        {
            SoulRecord[] best = world.Souls.OrderByDescending(soul => soul.BestScore)
                .ThenBy(soul => soul.SoulId).ToArray();
            Dictionary<int, int> bestRanks = new();
            for (int index = 0; index < best.Length; index++)
            {
                bestRanks.Add(best[index].SoulId, index + 1);
            }

            SoulRecord[] five = world.Souls.OrderByDescending(soul => soul.TopFiveSum)
                .ThenBy(soul => soul.SoulId).ToArray();
            List<(SoulRecord Soul, int BestRank, int FiveRank)> rows = new();
            for (int index = 0; index < five.Length; index++)
            {
                rows.Add((five[index], bestRanks[five[index].SoulId], index + 1));
            }

            return sort switch
            {
                0 => rows.OrderBy(row => row.BestRank).ToList(),
                1 => rows,
                _ => rows.OrderBy(row => row.BestRank + row.FiveRank)
                    .ThenByDescending(row => row.Soul.BestScore).ThenBy(row => row.Soul.SoulId).ToList(),
            };
        }
    }
}
