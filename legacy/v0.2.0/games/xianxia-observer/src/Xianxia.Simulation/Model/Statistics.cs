// 시간에 따른 통계 표본. 그래프와 균형 점검에 쓴다.
using System.Collections.Generic;

namespace Xianxia.Simulation.Model
{
    /// <summary>한 시점의 세계 통계.</summary>
    public sealed class StatSample
    {
        /// <summary>연도.</summary>
        public int Year { get; set; }

        /// <summary>경지별 인원(9칸).</summary>
        public List<int> RealmCounts { get; set; } = new();

        /// <summary>청운종 인원.</summary>
        public int OrthodoxMembers { get; set; }

        /// <summary>천마문 인원.</summary>
        public int DemonicMembers { get; set; }

        /// <summary>떠돌이 인원.</summary>
        public int Rogues { get; set; }

        /// <summary>청운종 평균 경지(단계 포함, 0~8.75).</summary>
        public double OrthodoxAverageRealm { get; set; }

        /// <summary>천마문 평균 경지.</summary>
        public double DemonicAverageRealm { get; set; }

        /// <summary>청운종 총 전투력.</summary>
        public double OrthodoxPower { get; set; }

        /// <summary>천마문 총 전투력.</summary>
        public double DemonicPower { get; set; }

        /// <summary>청운종 금고.</summary>
        public long OrthodoxTreasury { get; set; }

        /// <summary>천마문 금고.</summary>
        public long DemonicTreasury { get; set; }

        /// <summary>청운종 지배 접경지 수.</summary>
        public int OrthodoxBorders { get; set; }

        /// <summary>천마문 지배 접경지 수.</summary>
        public int DemonicBorders { get; set; }
    }

    /// <summary>
    /// 통계 이력. [성능] 표본이 상한을 넘으면 하나 걸러 지우고 간격을 두 배로 늘려,
    /// 아무리 오래 돌려도 메모리와 그래프 그리기 비용이 일정하다.
    /// </summary>
    public sealed class StatHistory
    {
        /// <summary>표본 간격(년).</summary>
        public int Interval { get; set; } = Data.Balance.StatisticsInterval;

        /// <summary>표본(오래된 것부터).</summary>
        public List<StatSample> Samples { get; set; } = new();

        /// <summary>표본을 추가하고 필요하면 압축한다.</summary>
        /// <param name="sample">새 표본.</param>
        public void Add(StatSample sample)
        {
            Samples.Add(sample);
            if (Samples.Count <= Data.Balance.StatisticsCapacity)
            {
                return;
            }

            List<StatSample> kept = new(Samples.Count / 2 + 1);
            for (int index = 0; index < Samples.Count; index += 2)
            {
                kept.Add(Samples[index]);
            }

            if (kept[^1] != sample)
            {
                kept.Add(sample);
            }

            Samples = kept;
            Interval *= 2;
        }
    }
}
