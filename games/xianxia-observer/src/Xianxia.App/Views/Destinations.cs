// 화면 순서가 바뀌어도 Android 입력 기록과 이동 연결은 같은 문자열을 사용한다.
using System;

namespace Xianxia.App.Views
{
    /// <summary>주 탐색과 기존 일곱 화면의 안정된 목적지.</summary>
    internal static class Destinations
    {
        /// <summary>천하.</summary>
        internal const string World = "world";
        /// <summary>종문.</summary>
        internal const string Sects = "sects";
        /// <summary>인물.</summary>
        internal const string People = "people";
        /// <summary>사기.</summary>
        internal const string History = "history";
        /// <summary>더보기.</summary>
        internal const string More = "more";
        /// <summary>업적.</summary>
        internal const string Legacy = "legacy";
        /// <summary>현황.</summary>
        internal const string Stats = "stats";
        /// <summary>기록.</summary>
        internal const string Log = "log";

        private static readonly int[] s_primary = { 0, 1, 2, 3, 7 };

        /// <summary>주 탐색 다섯 항목에 대응하는 화면.</summary>
        internal static ReadOnlySpan<int> Primary => s_primary;

        /// <summary>기존 화면 순서를 저장 ID로 사용하지 않고 이동 경계에서만 변환한다.</summary>
        /// <param name="page">화면 위치.</param>
        /// <returns>목적지.</returns>
        internal static string FromPage(int page) => page switch
        {
            0 => World,
            1 => Sects,
            2 => People,
            3 => History,
            4 => Legacy,
            5 => Stats,
            6 => Log,
            _ => More,
        };

        /// <summary>문자열 목적지를 화면에 대응시킨다.</summary>
        /// <param name="destination">목적지.</param>
        /// <returns>화면 위치.</returns>
        internal static int ToPage(string destination) => destination switch
        {
            World => 0,
            Sects => 1,
            People => 2,
            History => 3,
            Legacy => 4,
            Stats => 5,
            Log => 6,
            More => 7,
            _ => throw new ArgumentException("알 수 없는 화면입니다.", nameof(destination)),
        };
    }
}
