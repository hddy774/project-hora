// 사건을 로그·연대기·생애 기록·관심 인물 알림으로 남기는 공용 도우미와 한국어 숫자 표기.
using System;
using System.Globalization;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>사건 서술 도우미. 모든 시스템이 같은 방식으로 기록을 남기게 한다.</summary>
    public static class Narrator
    {
        /// <summary>
        /// 로그를 남긴다. 주인공이나 상대가 관심 인물이고 <paramref name="notable"/>이면 화면 알림도 낸다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="kind">분류.</param>
        /// <param name="text">내용.</param>
        /// <param name="subject">주인공.</param>
        /// <param name="other">상대.</param>
        /// <param name="notable">관심 인물 알림 대상인가.</param>
        public static void Log(World world, LogKind kind, string text, Cultivator? subject = null,
            Cultivator? other = null, bool notable = false)
        {
            ArgumentNullException.ThrowIfNull(world);
            world.Log.Add(world.Year, kind, text, subject?.Id ?? 0, other?.Id ?? 0);
            if (!notable)
            {
                return;
            }

            if (subject is not null && world.IsFollowed(subject.Id))
            {
                world.Chronicle.Notify(world.Year, text, subject.Id, false);
            }
            else if (other is not null && world.IsFollowed(other.Id))
            {
                world.Chronicle.Notify(world.Year, text, other.Id, false);
            }
        }

        /// <summary>연대기에 남긴다(화면 알림 포함).</summary>
        /// <param name="world">세계.</param>
        /// <param name="kind">분류.</param>
        /// <param name="text">내용.</param>
        /// <param name="subject">주인공.</param>
        public static void Chronicle(World world, ChronicleKind kind, string text, Cultivator? subject = null)
        {
            ArgumentNullException.ThrowIfNull(world);
            world.Chronicle.Add(world.Year, kind, text, subject?.Id ?? 0);
        }

        /// <summary>현생 생애 기록에 한 줄을 더한다. 넘치면 가장 오래된(탄생 제외) 줄을 지운다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="text">내용.</param>
        public static void Biography(World world, Cultivator cultivator, string text)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.Biography.Add(new LifeEvent(world.Year, cultivator.Age, text));
            if (cultivator.Biography.Count > Balance.BiographyCapacity)
            {
                cultivator.Biography.RemoveAt(1);
            }
        }

        /// <summary>종문 이름 또는 "산수".</summary>
        /// <param name="world">세계.</param>
        /// <param name="sectId">종문 번호.</param>
        /// <returns>소속 이름.</returns>
        public static string Affiliation(World world, int sectId)
        {
            ArgumentNullException.ThrowIfNull(world);
            return world.FindSect(sectId)?.Name ?? "산수";
        }
    }

    /// <summary>한국어 숫자 표기(만·억). 시뮬레이션 문장과 화면이 같이 쓴다.</summary>
    public static class KoreanNumber
    {
        /// <summary>큰 수를 "1.2만", "3.4억" 형식으로 줄인다.</summary>
        /// <param name="value">값.</param>
        /// <returns>표기.</returns>
        public static string Compact(double value)
        {
            double magnitude = Math.Abs(value);
            string sign = value < 0 ? "-" : string.Empty;
            if (magnitude >= 1e12)
            {
                return sign + (magnitude / 1e12).ToString("0.#", CultureInfo.InvariantCulture) + "조";
            }

            if (magnitude >= 1e8)
            {
                return sign + (magnitude / 1e8).ToString("0.#", CultureInfo.InvariantCulture) + "억";
            }

            if (magnitude >= 1e4)
            {
                return sign + (magnitude / 1e4).ToString("0.#", CultureInfo.InvariantCulture) + "만";
            }

            return sign + Math.Floor(magnitude).ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>천 단위 쉼표 표기.</summary>
        /// <param name="value">값.</param>
        /// <returns>예: 12,345.</returns>
        public static string Grouped(double value)
        {
            return Math.Round(value).ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>백분율 표기.</summary>
        /// <param name="ratio">0~1 비율.</param>
        /// <returns>예: 42%.</returns>
        public static string Percent(double ratio)
        {
            return Math.Round(ratio * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        }
    }
}
