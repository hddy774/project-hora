// 경지(9개)와 단계(4개)의 이름·필요 영기·수명·전투력 기준 표. 밸런스 근거는 docs/02-design.md §3.
using System;

namespace Xianxia.Simulation.Data
{
    /// <summary>경지·단계 기준표. 모든 값은 시뮬레이션 규칙의 일부이며 저장 파일에는 쓰지 않는다.</summary>
    public static class Realms
    {
        /// <summary>경지 수.</summary>
        public const int Count = 9;

        /// <summary>최고 경지 인덱스(반선경).</summary>
        public const int Max = Count - 1;

        /// <summary>한 경지의 단계 수(초기·중기·후기·대원만).</summary>
        public const int StageCount = 4;

        /// <summary>대원만 단계 인덱스. 여기서 다음 경지로의 대경지 돌파를 시도한다.</summary>
        public const int PeakStage = StageCount - 1;

        /// <summary>천겁(뇌겁)이 내리는 첫 경지(화신경).</summary>
        public const int FirstTribulationRealm = 4;

        private static readonly string[] s_names =
        {
            "연기경", "축기경", "금단경", "원영경", "화신경", "합체경", "도겁경", "대승경", "반선경",
        };

        private static readonly string[] s_stageNames = { "초기", "중기", "후기", "대원만" };

        // 원본의 수명(경지마다 2배)을 유지한다. 세계의 시간 척도(천년 전쟁·만년 휴전)가 이 값에 맞춰져 있다.
        private static readonly int[] s_baseLifespan =
        {
            400, 800, 1600, 3200, 6400, 12800, 25600, 51200, 102400,
        };

        // 0.2.0: 원영의 입구 병목과 상위 경지의 장기 요구량을 분리한다.
        // 이전400백만 영기에서10시드·3만년의 자연 비승이 전부0이었다. 확률·공법은 먼저 바꾸지 않는다.
        private static readonly long[] s_realmQiTotal =
        {
            800, 5_000, 25_000, 170_000, 600_000, 2_800_000, 7_000_000, 8_000_000, 9_000_000,
        };

        // 단계별 비중. 대원만(병목)이 가장 크다.
        private static readonly double[] s_stageShare = { 0.15, 0.22, 0.28, 0.35 };

        // 대경지 돌파(r→r+1) 기본 확률. 높은 경지일수록 병목이 좁아져 피라미드형 분포를 만든다.
        private static readonly double[] s_majorBreakthroughBase =
        {
            0.60, 0.42, 0.32, 0.25, 0.20, 0.16, 0.12, 0.09,
        };

        /// <summary>경지 이름.</summary>
        /// <param name="realm">경지 인덱스.</param>
        /// <returns>예: 연기경.</returns>
        public static string Name(int realm)
        {
            return s_names[Math.Clamp(realm, 0, Max)];
        }

        /// <summary>단계 이름.</summary>
        /// <param name="stage">단계 인덱스.</param>
        /// <returns>예: 대원만.</returns>
        public static string StageName(int stage)
        {
            return s_stageNames[Math.Clamp(stage, 0, PeakStage)];
        }

        /// <summary>"연기경 초기" 형식의 전체 이름.</summary>
        /// <param name="realm">경지.</param>
        /// <param name="stage">단계.</param>
        /// <returns>경지와 단계 이름.</returns>
        public static string FullName(int realm, int stage)
        {
            return Name(realm) + " " + StageName(stage);
        }

        /// <summary>경지의 기본 수명(년).</summary>
        /// <param name="realm">경지.</param>
        /// <returns>수명.</returns>
        public static int BaseLifespan(int realm)
        {
            return s_baseLifespan[Math.Clamp(realm, 0, Max)];
        }

        /// <summary>해당 단계에서 다음 단계로 넘어가는 데 필요한 영기.</summary>
        /// <param name="realm">경지.</param>
        /// <param name="stage">단계.</param>
        /// <returns>필요 영기.</returns>
        public static long Requirement(int realm, int stage)
        {
            long total = s_realmQiTotal[Math.Clamp(realm, 0, Max)];
            double share = s_stageShare[Math.Clamp(stage, 0, PeakStage)];
            return (long)Math.Round(total * share / 10.0) * 10;
        }

        /// <summary>대경지 돌파 기본 확률. 반선경에서는 비승 확률을 따로 쓴다.</summary>
        /// <param name="realm">현재 경지.</param>
        /// <returns>0~1 확률.</returns>
        public static double MajorBreakthroughBase(int realm)
        {
            return realm >= Max ? 0 : s_majorBreakthroughBase[Math.Max(0, realm)];
        }

        /// <summary>
        /// 경지 전투력 기준값. 경지마다 2.8배라서 한 단계 위 경지를 이기려면 재능·공법·법보가 모두 뛰어나야 한다.
        /// </summary>
        /// <param name="realm">경지.</param>
        /// <param name="stage">단계.</param>
        /// <returns>기준 전투력.</returns>
        public static double PowerBase(int realm, int stage)
        {
            return 100.0 * Math.Pow(2.8, realm) * (1.0 + (0.18 * stage));
        }

        /// <summary>경지·단계를 하나의 순위 값으로 바꾼다(정렬용).</summary>
        /// <param name="realm">경지.</param>
        /// <param name="stage">단계.</param>
        /// <returns>0~35 사이의 값.</returns>
        public static int Rank(int realm, int stage)
        {
            return (realm * StageCount) + stage;
        }
    }
}
