// 화면 표기 규칙: 연도·수·배율·비율. 시뮬레이션 문장과 같은 한국어 숫자 표기(KoreanNumber)를 쓴다.
using System.Globalization;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Services
{
    /// <summary>표기 도우미.</summary>
    internal static class Fmt
    {
        /// <summary>쉼표 숫자.</summary>
        /// <param name="value">값.</param>
        /// <returns>예: 12,345.</returns>
        internal static string Number(double value)
        {
            return KoreanNumber.Grouped(value);
        }

        /// <summary>줄인 숫자.</summary>
        /// <param name="value">값.</param>
        /// <returns>예: 1.2만.</returns>
        internal static string Short(double value)
        {
            return KoreanNumber.Compact(value);
        }

        /// <summary>햇수.</summary>
        /// <param name="years">햇수.</param>
        /// <returns>예: 1,234년.</returns>
        internal static string Years(int years)
        {
            return Number(years) + "년";
        }

        /// <summary>비율.</summary>
        /// <param name="ratio">0~1.</param>
        /// <returns>예: 42%.</returns>
        internal static string Percent(double ratio)
        {
            return KoreanNumber.Percent(ratio);
        }

        /// <summary>소수 한 자리 백분율(확률 표시).</summary>
        /// <param name="ratio">0~1.</param>
        /// <returns>예: 4.2%.</returns>
        internal static string Chance(double ratio)
        {
            return (ratio * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>배율.</summary>
        /// <param name="factor">배율.</param>
        /// <returns>예: ×1.25.</returns>
        internal static string Factor(double factor)
        {
            return "×" + factor.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>소수 표기.</summary>
        /// <param name="value">값.</param>
        /// <param name="format">형식(예: 0.0).</param>
        /// <returns>표기.</returns>
        internal static string Decimal(double value, string format = "0.0")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>나이/수명.</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>예: 312 / 1,600세.</returns>
        internal static string Age(Cultivator cultivator)
        {
            return Number(cultivator.Age) + " / " + Number(cultivator.Lifespan) + "세";
        }

        /// <summary>소속과 직위.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>예: 청운종 장로, 산수.</returns>
        internal static string Affiliation(World world, Cultivator cultivator)
        {
            return cultivator.IsRogue ? "산수(떠돌이)"
                : Narrator.Affiliation(world, cultivator.SectId) + " " + Labels.Of(cultivator.Rank);
        }

        /// <summary>남은 해(음수면 "없음").</summary>
        /// <param name="years">남은 햇수.</param>
        /// <returns>예: 312년 뒤.</returns>
        internal static string After(int years)
        {
            return years < 0 ? "예정 없음" : years == 0 ? "올해" : Years(years) + " 뒤";
        }
    }
}
