// 공법 20등급의 이름·경지 한계·수련 배율·장경각 공헌 비용 표.
using System;

namespace Xianxia.Simulation.Data
{
    /// <summary>공법 등급표. 등급이 높을수록 영기 흡수가 빠르고 더 높은 경지까지 무리 없이 돌파한다.</summary>
    public static class Techniques
    {
        /// <summary>공법 등급 수.</summary>
        public const int GradeCount = 20;

        /// <summary>최고 등급(신품).</summary>
        public const int MaxGrade = GradeCount - 1;

        /// <summary>장경각에서 구할 수 있는 최고 등급. 신품(19)은 정상 획득 경로가 없는 예약 등급이다(비경 최대18·경매 최대16).</summary>
        public const int MaxPavilionGrade = 18;

        private static readonly string[] s_names =
        {
            "무급", "1성", "2성", "3성", "4성", "5성", "6성", "7성", "8성", "9성",
            "현품", "지품", "천품", "선품", "성품", "제품", "황품", "왕품", "극품", "신품",
        };

        // 원본 CVMR: 각 등급이 무리 없이 감당하는 최고 경지.
        private static readonly int[] s_realmCap =
        {
            0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 7, 8, 8, 8,
        };

        /// <summary>등급 이름.</summary>
        /// <param name="grade">등급.</param>
        /// <returns>예: 현품.</returns>
        public static string Name(int grade)
        {
            return s_names[Math.Clamp(grade, 0, MaxGrade)];
        }

        /// <summary>등급이 감당하는 최고 경지.</summary>
        /// <param name="grade">등급.</param>
        /// <returns>경지 인덱스.</returns>
        public static int RealmCap(int grade)
        {
            return s_realmCap[Math.Clamp(grade, 0, MaxGrade)];
        }

        /// <summary>수련 영기 배율(0.75~1.7). 다른 배율과 곱해지므로 폭을 좁게 둔다.</summary>
        /// <param name="grade">등급.</param>
        /// <returns>배율.</returns>
        public static double GainMultiplier(int grade)
        {
            return 0.75 + (0.05 * Math.Clamp(grade, 0, MaxGrade));
        }

        /// <summary>
        /// 대경지 돌파 시 공법 한계 보정. 목표 경지가 한계를 넘으면 한 경지당 확률이 0.4배로 줄어든다(원본의 공법 패널티를 계승).
        /// </summary>
        /// <param name="grade">공법 등급.</param>
        /// <param name="targetRealm">돌파해 들어갈 경지.</param>
        /// <returns>확률 배율.</returns>
        public static double BreakthroughModifier(int grade, int targetRealm)
        {
            int gap = targetRealm - RealmCap(grade);
            if (gap <= 0)
            {
                return 1.0 + (0.03 * Math.Min(3, -gap));
            }

            return Math.Pow(0.4, gap);
        }

        /// <summary>장경각에서 이 등급을 익히는 데 드는 공헌점.</summary>
        /// <param name="grade">익힐 등급.</param>
        /// <returns>공헌점.</returns>
        public static int PavilionCost(int grade)
        {
            int next = Math.Clamp(grade, 0, MaxGrade) + 1;
            return 60 * next * next;
        }

        /// <summary>장경각 단계에서 구할 수 있는 최고 등급.</summary>
        /// <param name="pavilionLevel">장경각 단계(0~5).</param>
        /// <returns>최고 등급.</returns>
        public static int PavilionGradeLimit(int pavilionLevel)
        {
            return Math.Min(MaxPavilionGrade, 3 + (3 * Math.Max(0, pavilionLevel)));
        }

        /// <summary>떠돌이가 상회에서 공법을 살 때의 영석 가격.</summary>
        /// <param name="grade">등급.</param>
        /// <returns>영석.</returns>
        public static long MarketPrice(int grade)
        {
            int next = Math.Clamp(grade, 0, MaxGrade) + 1;
            return 40L * next * next * next;
        }
    }
}
