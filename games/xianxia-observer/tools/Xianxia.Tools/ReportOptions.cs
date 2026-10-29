// 보고 명령의 범위·값을 검증한다. 시드 범위는 순서를 유지하고 중복을 제거한다.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Xianxia.Tools
{
    /// <summary>재현 가능한 보고 실행 인자.</summary>
    public sealed class ReportOptions
    {
        /// <summary>실행할 고유 시드.</summary>
        public IReadOnlyList<ulong> Seeds { get; private set; } = new ulong[] { 1, 2, 3 };
        /// <summary>진행 연수.</summary>
        public int Years { get; private set; } = 30000;
        /// <summary>인구.</summary>
        public int Population { get; private set; } = 1000;
        /// <summary>확장 진단 보고 여부.</summary>
        public bool Balance { get; private set; }
        /// <summary>선택적 기계 판독 결과 경로.</summary>
        public string? JsonPath { get; private set; }

        /// <summary>report 명령의 인자를 읽고 알 수 없는 옵션도 거부한다.</summary>
        /// <param name="args">명령 포함 인자.</param>
        /// <returns>검증한 옵션.</returns>
        public static ReportOptions Parse(IReadOnlyList<string> args)
        {
            if (args.Count > 0 && args[0] != "report")
            {
                throw new ArgumentException("지원 명령은 report입니다.", nameof(args));
            }

            ReportOptions result = new();
            for (int index = 1; index < args.Count; index++)
            {
                if (args[index] == "--balance")
                {
                    result.Balance = true;
                    continue;
                }

                string option = args[index];
                if (++index >= args.Count)
                {
                    throw new ArgumentException("옵션 값이 없습니다: " + option, nameof(args));
                }

                result.Set(option, args[index]);
            }

            ArgumentOutOfRangeException.ThrowIfLessThan(result.Population, 100);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(result.Population, 3000);
            ArgumentOutOfRangeException.ThrowIfLessThan(result.Years, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(result.Years, 1_000_000);
            return result;
        }

        /// <summary>쉼표 목록과 포함 범위 a-b를 읽는다. 한 범위는1000개 이하다.</summary>
        /// <param name="value">예: 1-10,20.</param>
        /// <returns>중복 없는 시드.</returns>
        public static IReadOnlyList<ulong> ParseSeeds(string value)
        {
            List<ulong> result = new();
            foreach (string item in value.Split(','))
            {
                string[] parts = item.Trim().Split('-');
                if (parts.Length is < 1 or > 2)
                {
                    throw new FormatException("시드는 숫자 또는 a-b 범위입니다.");
                }

                ulong first = ulong.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture);
                ulong last = parts.Length == 1 ? first
                    : ulong.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture);
                if (last < first || last - first >= 1000)
                {
                    throw new FormatException("시드 범위는 오름차순1000개 이하입니다.");
                }

                AddRange(result, first, last);
            }

            return result;
        }

        private static void AddRange(List<ulong> result, ulong first, ulong last)
        {
            for (ulong seed = first; ; seed++)
            {
                if (!result.Contains(seed))
                {
                    result.Add(seed);
                }

                if (seed == last)
                {
                    return;
                }
            }
        }

        private void Set(string option, string value)
        {
            switch (option)
            {
                case "--seeds":
                    Seeds = ParseSeeds(value);
                    break;
                case "--years":
                    Years = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "--population":
                    Population = int.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case "--json":
                    JsonPath = value;
                    break;
                default:
                    throw new ArgumentException("알 수 없는 옵션: " + option, nameof(option));
            }
        }
    }
}
