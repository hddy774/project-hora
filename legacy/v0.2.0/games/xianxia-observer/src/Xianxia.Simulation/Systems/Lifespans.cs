// 수명 계산. 경지 기본 수명에 체질과 연수단 보정을 더한다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>수명 규칙.</summary>
    public static class Lifespans
    {
        /// <summary>연수단 최대 복용 수(한 생).</summary>
        public const int MaxLongevityPills = 3;

        /// <summary>
        /// 현재 경지 기준 수명. 체질 500이면 +10%, 연수단 하나당 +5%.
        /// 경지가 떨어지면 이 값도 떨어지므로 나이가 많으면 바로 좌화할 수 있다(원본의 숨은 규칙을 명시).
        /// </summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>수명(년).</returns>
        public static int For(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            double bonus = 1.0 + (cultivator.Constitution / 5000.0) + (0.05 * cultivator.LongevityPillsTaken);
            return (int)Math.Round(Realms.BaseLifespan(cultivator.Realm) * bonus);
        }

        /// <summary>수명 대비 나이 비율(0~1+).</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>비율.</returns>
        public static double AgeRatio(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            return cultivator.Lifespan <= 0 ? 1 : (double)cultivator.Age / cultivator.Lifespan;
        }
    }
}
