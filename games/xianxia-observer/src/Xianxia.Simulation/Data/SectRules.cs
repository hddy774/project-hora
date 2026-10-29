// 종문 직위 정원·녹봉·수련 배율, 시설 효과와 비용, 이념별 보정 표.
using System;

namespace Xianxia.Simulation.Data
{
    /// <summary>종문 시설 종류.</summary>
    public enum FacilityKind
    {
        /// <summary>영맥진: 본산·영지에서 수련 속도 증가.</summary>
        SpiritArray = 0,

        /// <summary>장경각: 익힐 수 있는 공법 등급 상한.</summary>
        Pavilion = 1,

        /// <summary>연단방: 단약 공급량.</summary>
        AlchemyHall = 2,

        /// <summary>연무장: 소속 제자의 전투력 증가.</summary>
        TrainingGround = 3,

        /// <summary>호산대진: 전쟁·수조 방어.</summary>
        GuardianArray = 4,
    }

    /// <summary>종문 규칙표.</summary>
    public static class SectRules
    {
        /// <summary>시설 종류 수.</summary>
        public const int FacilityCount = 5;

        /// <summary>시설 최고 단계.</summary>
        public const int MaxFacilityLevel = 5;

        /// <summary>정원이 있는 직위 수(외문제자~종주).</summary>
        public const int RankedTierCount = 6;

        // 원본 정원(종주1·대장로2·장로10·직전50·내문100·외문200)을 유지한다. 인덱스는 SectRank 값.
        private static readonly int[] s_capacity = { int.MaxValue, 200, 100, 50, 10, 2, 1 };

        // 100년마다 지급하는 녹봉(영석). 종문 금고에서 나가며, 영지 산출(100년에 지역당 약 1,000~2,400)과 맞춘 값이다.
        private static readonly int[] s_stipend = { 0, 15, 30, 60, 120, 200, 300 };

        // 직위별 수련 배율. 원본 RRES.gw(1.06~1.6)의 순서를 지키되 다른 배율과 곱해지므로 폭을 줄였다.
        private static readonly double[] s_growth = { 1.0, 1.05, 1.1, 1.15, 1.25, 1.35, 1.45 };

        private static readonly string[] s_facilityNames = { "영맥진", "장경각", "연단방", "연무장", "호산대진" };

        private static readonly string[] s_facilityEffects =
        {
            "본산·영지 수련 +4%/단계",
            "공법 상한 +3등급/단계",
            "100년마다 단약 6개/단계",
            "제자 전투력 +3%/단계",
            "전쟁·수조 방어 +5%/단계",
        };

        /// <summary>직위 정원.</summary>
        /// <param name="rank">직위.</param>
        /// <returns>정원.</returns>
        public static int Capacity(SectRank rank)
        {
            return s_capacity[(int)rank];
        }

        /// <summary>100년 녹봉.</summary>
        /// <param name="rank">직위.</param>
        /// <returns>영석.</returns>
        public static int Stipend(SectRank rank)
        {
            return s_stipend[(int)rank];
        }

        /// <summary>직위 수련 배율.</summary>
        /// <param name="rank">직위.</param>
        /// <returns>배율.</returns>
        public static double GrowthMultiplier(SectRank rank)
        {
            return s_growth[(int)rank];
        }

        /// <summary>시설 이름.</summary>
        /// <param name="facility">시설.</param>
        /// <returns>이름.</returns>
        public static string FacilityName(FacilityKind facility)
        {
            return s_facilityNames[(int)facility];
        }

        /// <summary>시설 효과 설명.</summary>
        /// <param name="facility">시설.</param>
        /// <returns>설명.</returns>
        public static string FacilityEffect(FacilityKind facility)
        {
            return s_facilityEffects[(int)facility];
        }

        /// <summary>시설을 다음 단계로 올리는 비용.</summary>
        /// <param name="currentLevel">현재 단계.</param>
        /// <returns>영석.</returns>
        public static long UpgradeCost(int currentLevel)
        {
            return (long)Math.Round(20000 * Math.Pow(2.5, currentLevel));
        }

        /// <summary>
        /// 이념별 경지 수련 배율. 원본 IDEO.bm의 구간(정도 4·6, 마도 3·5·7)을 따르되 폭을 줄여 실제로 적용한다
        /// (원본은 계산만 하고 적용하지 않았다). 정도는 고경지에서, 마도는 저경지에서 빠르다.
        /// </summary>
        /// <param name="orthodox">정도 종문이면 true, 마도면 false.</param>
        /// <param name="realm">경지.</param>
        /// <returns>배율.</returns>
        public static double IdeologyGrowth(bool orthodox, int realm)
        {
            if (orthodox)
            {
                return realm < 3 ? 1.05 : realm < 5 ? 1.15 : realm < 7 ? 1.2 : 1.25;
            }

            return realm < 3 ? 1.35 : realm < 5 ? 1.2 : realm < 7 ? 0.95 : 0.8;
        }

        /// <summary>이념별 대경지 돌파 보정. 정도는 기초가 단단해 모든 경지에서 돌파가 안정적이다.</summary>
        /// <param name="orthodox">정도 종문 여부.</param>
        /// <param name="realm">현재 경지.</param>
        /// <returns>확률 배율.</returns>
        public static double IdeologyBreakthrough(bool orthodox, int realm)
        {
            if (orthodox)
            {
                return realm >= 4 ? 1.15 : 1.1;
            }

            return realm < 3 ? 1.15 : realm >= 5 ? 0.9 : 1.0;
        }

        /// <summary>이념별 주화입마(돌파 반동) 확률 가산.</summary>
        /// <param name="orthodox">정도 종문 여부.</param>
        /// <returns>가산 확률.</returns>
        public static double IdeologyBacklash(bool orthodox)
        {
            return orthodox ? -0.03 : 0.05;
        }

        /// <summary>이념별 전쟁 전투력 배율. 마도는 혈전(血戰)에 강하다.</summary>
        /// <param name="orthodox">정도 종문 여부.</param>
        /// <returns>배율.</returns>
        public static double IdeologyWarPower(bool orthodox)
        {
            return orthodox ? 1.0 : 1.1;
        }

        /// <summary>이념별 전리품 배율.</summary>
        /// <param name="orthodox">정도 종문 여부.</param>
        /// <returns>배율.</returns>
        public static double IdeologyLoot(bool orthodox)
        {
            return orthodox ? 1.0 : 1.5;
        }

        /// <summary>이념별 시설 투자 우선순위.</summary>
        /// <param name="orthodox">정도 종문 여부.</param>
        /// <returns>시설 순서.</returns>
        public static FacilityKind[] FacilityPriority(bool orthodox)
        {
            return orthodox
                ? new[]
                {
                    FacilityKind.Pavilion, FacilityKind.SpiritArray, FacilityKind.AlchemyHall,
                    FacilityKind.GuardianArray, FacilityKind.TrainingGround,
                }
                : new[]
                {
                    FacilityKind.TrainingGround, FacilityKind.AlchemyHall, FacilityKind.SpiritArray,
                    FacilityKind.Pavilion, FacilityKind.GuardianArray,
                };
        }
    }
}
