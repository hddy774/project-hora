// 장기 실행의 작은 요약 DTO. 세계 객체를 여러 시드 동안 계속 보유하지 않는다.
using System.Collections.Generic;

namespace Xianxia.Tools
{
    /// <summary>시드 하나의 비교 가능한 결과.</summary>
    public sealed class BalanceResult
    {
        /// <summary>시드.</summary>
        public ulong Seed { get; set; }
        /// <summary>연수.</summary>
        public int Years { get; set; }
        /// <summary>인구.</summary>
        public int Population { get; set; }
        /// <summary>0년 이후 경지별 첫 돌파 해.</summary>
        public int?[] FirstReach { get; set; } = System.Array.Empty<int?>();
        /// <summary>최종 경지 분포.</summary>
        public int[] RealmCounts { get; set; } = System.Array.Empty<int>();
        /// <summary>5000년 간격의 분포·영석.</summary>
        public List<BalanceSnapshot> Snapshots { get; set; } = new();
        /// <summary>자연 비승 연도.</summary>
        public int[] AscensionYears { get; set; } = System.Array.Empty<int>();
        /// <summary>청운종 전쟁 승수.</summary>
        public int OrthodoxWins { get; set; }
        /// <summary>천마문 전쟁 승수.</summary>
        public int DemonicWins { get; set; }
        /// <summary>청운종 최장 연패.</summary>
        public int OrthodoxLosingStreak { get; set; }
        /// <summary>천마문 최장 연패.</summary>
        public int DemonicLosingStreak { get; set; }
        /// <summary>종료 시 실제 영석 총량.</summary>
        public long Stones { get; set; }
        /// <summary>gzip 저장 바이트.</summary>
        public int SaveBytes { get; set; }
        /// <summary>5회 평균 저장 밀리초.</summary>
        public double SaveMs { get; set; }
        /// <summary>5회 평균 로드 밀리초.</summary>
        public double LoadMs { get; set; }
        /// <summary>영혼 기록 수.</summary>
        public int Souls { get; set; }
        /// <summary>전쟁 기록 수.</summary>
        public int Wars { get; set; }
        /// <summary>대비무 기록 수.</summary>
        public int Tournaments { get; set; }
        /// <summary>종료 표본의 평균 경지 체류.</summary>
        public double[] DwellMean { get; set; } = System.Array.Empty<double>();
        /// <summary>경지별 체류 표본 수.</summary>
        public long[] DwellSamples { get; set; } = System.Array.Empty<long>();
        /// <summary>경지별·출처별 영기. 각 행은 다섯 출처 순이다.</summary>
        public double[][] QiSources { get; set; } = System.Array.Empty<double[]>();
    }

    /// <summary>한 시점의 분포와 총량.</summary>
    public sealed class BalanceSnapshot
    {
        /// <summary>실제 해.</summary>
        public int Year { get; set; }
        /// <summary>경지 분포.</summary>
        public int[] Realms { get; set; } = System.Array.Empty<int>();
        /// <summary>실제 영석 총량.</summary>
        public long Stones { get; set; }
    }
}
