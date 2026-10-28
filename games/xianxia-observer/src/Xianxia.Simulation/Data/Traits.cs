// 영근 속성 상극, 성격별 행동 가중치, 재능 등급 규칙.
using System;

namespace Xianxia.Simulation.Data
{
    /// <summary>영근·성격·재능 규칙.</summary>
    public static class Traits
    {
        /// <summary>오행 속성 수(금·목·수·화·토).</summary>
        public const int BasicElementCount = 5;

        /// <summary>재능 능력치 최솟값.</summary>
        public const int StatMin = 10;

        /// <summary>재능 능력치 최댓값.</summary>
        public const int StatMax = 500;

        // 행동별 기본 가중치. 열 순서는 CultivatorAction(수련·탐색·거래·기예·비무·임무·이동·지도·요양).
        private static readonly double[,] s_actionWeights =
        {
            { 40, 15, 8, 10, 10, 12, 5, 6, 0 },
            { 34, 15, 5, 6, 22, 10, 8, 4, 0 },
            { 36, 18, 15, 12, 7, 6, 6, 4, 0 },
            { 60, 12, 3, 14, 2, 3, 4, 4, 0 },
            { 32, 12, 5, 5, 30, 8, 8, 4, 0 },
        };

        /// <summary>
        /// 공격 속성이 방어 속성을 이기면(상극) true. 금→목→토→수→화→금. 변이 영근은 상극 관계가 없다.
        /// </summary>
        /// <param name="attacker">공격자 속성.</param>
        /// <param name="defender">방어자 속성.</param>
        /// <returns>상극 여부.</returns>
        public static bool Overcomes(Element attacker, Element defender)
        {
            return (attacker, defender) switch
            {
                (Element.Metal, Element.Wood) => true,
                (Element.Wood, Element.Earth) => true,
                (Element.Earth, Element.Water) => true,
                (Element.Water, Element.Fire) => true,
                (Element.Fire, Element.Metal) => true,
                _ => false,
            };
        }

        /// <summary>변이 영근(뇌·빙·풍) 여부. 수련 +8%, 전투 +5%.</summary>
        /// <param name="element">속성.</param>
        /// <returns>변이 여부.</returns>
        public static bool IsVariant(Element element)
        {
            return (int)element >= BasicElementCount;
        }

        /// <summary>성격의 행동 기본 가중치.</summary>
        /// <param name="personality">성격.</param>
        /// <param name="action">행동.</param>
        /// <returns>가중치.</returns>
        public static double ActionWeight(Personality personality, CultivatorAction action)
        {
            return s_actionWeights[(int)personality, (int)action];
        }

        /// <summary>성격이 종문 이념과 맞는 정도(0~1). 입문·배신 판단에 쓴다.</summary>
        /// <param name="personality">성격.</param>
        /// <param name="orthodox">정도 종문이면 true.</param>
        /// <returns>적합도.</returns>
        public static double SectAffinity(Personality personality, bool orthodox)
        {
            return personality switch
            {
                Personality.Righteous => orthodox ? 0.95 : 0.15,
                Personality.Demonic => orthodox ? 0.1 : 0.95,
                Personality.Domineering => orthodox ? 0.35 : 0.8,
                Personality.Hermit => orthodox ? 0.55 : 0.3,
                _ => 0.6,
            };
        }

        /// <summary>재능 합계(4~2000)를 등급(1 범재 ~ 5 절세)으로 바꾼다. 원본 cg 구간을 유지한다.</summary>
        /// <param name="talentTotal">오성+체질+근골+영근.</param>
        /// <returns>등급.</returns>
        public static int TalentGrade(int talentTotal)
        {
            if (talentTotal < 600)
            {
                return 1;
            }

            if (talentTotal < 1000)
            {
                return 2;
            }

            if (talentTotal < 1400)
            {
                return 3;
            }

            return talentTotal < 1700 ? 4 : 5;
        }

        /// <summary>
        /// 능력치 하나를 뽑는다. 균등 분포 대신 낮은 값이 흔한 분포(u^1.6)를 써서 천재가 드물게 나오게 한다.
        /// 윤회 업보(<paramref name="karmaFloor"/>)가 높을수록 하한이 올라간다.
        /// </summary>
        /// <param name="uniform">0~1 균등 난수.</param>
        /// <param name="karmaFloor">하한 가산(0~200).</param>
        /// <returns>능력치(10~500).</returns>
        public static int RollStat(double uniform, int karmaFloor)
        {
            int floor = Math.Clamp(StatMin + karmaFloor, StatMin, 300);
            double skewed = Math.Pow(Math.Clamp(uniform, 0, 1), 1.6);
            return Math.Clamp(floor + (int)Math.Round((StatMax - floor) * skewed), StatMin, StatMax);
        }
    }
}
