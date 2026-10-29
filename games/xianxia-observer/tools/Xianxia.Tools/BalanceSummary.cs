// 표본이 없는 첫 도달·체류를0으로 오해하지 않도록 n과 함께 범위를 출력한다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xianxia.Simulation.Data;

namespace Xianxia.Tools
{
    /// <summary>모든 시드의 원시 지표 최소·중앙·최대.</summary>
    internal static class BalanceSummary
    {
        /// <summary>빈 표본도 누락하지 않고 원문 표로 출력한다.</summary>
        /// <param name="results">시드별 결과.</param>
        internal static void Print(IReadOnlyList<BalanceResult> results)
        {
            SortedDictionary<string, List<double>> metrics = new(StringComparer.Ordinal);
            foreach (BalanceResult result in results)
            {
                Add(metrics, "ascensions", result.AscensionYears.Length);
                Add(metrics, "first-ascension", result.AscensionYears.Length == 0 ? null : result.AscensionYears[0]);
                Add(metrics, "wins.orthodox", result.OrthodoxWins);
                Add(metrics, "wins.demonic", result.DemonicWins);
                Add(metrics, "losing-streak.orthodox", result.OrthodoxLosingStreak);
                Add(metrics, "losing-streak.demonic", result.DemonicLosingStreak);
                Add(metrics, "stones", result.Stones);
                Add(metrics, "storage.bytes", result.SaveBytes);
                Add(metrics, "storage.save-ms", result.SaveMs);
                Add(metrics, "storage.load-ms", result.LoadMs);
                Add(metrics, "records.souls", result.Souls);
                Add(metrics, "records.wars", result.Wars);
                Add(metrics, "records.tournaments", result.Tournaments);
                RealmMetrics(metrics, result);
                SnapshotMetrics(metrics, result);
            }

            Console.WriteLine("== 최소 | 중앙 | 최대 (표본/전체=" + results.Count + ") ==");
            foreach ((string key, List<double> values) in metrics)
            {
                values.Sort();
                string range = values.Count == 0 ? "- | - | -"
                    : F(values[0]) + " | " + F(Median(values)) + " | " + F(values[^1]);
                Console.WriteLine(key + " | " + range + " | n=" + values.Count + "/" + results.Count);
            }
        }

        private static void RealmMetrics(SortedDictionary<string, List<double>> metrics, BalanceResult result)
        {
            for (int realm = 0; realm < Realms.Count; realm++)
            {
                string name = Realms.Name(realm);
                Add(metrics, "final." + name, result.RealmCounts[realm]);
                Add(metrics, "first." + name, result.FirstReach[realm]);
                if (result.DwellSamples.Length == 0)
                {
                    continue;
                }

                Add(metrics, "dwell." + name, result.DwellSamples[realm] == 0 ? null : result.DwellMean[realm]);
                Add(metrics, "dwell-n." + name, result.DwellSamples[realm]);
                if (realm >= 6)
                {
                    double total = result.QiSources[realm].Sum();
                    for (int source = 0; source < result.QiSources[realm].Length; source++)
                    {
                        Add(metrics, "qi-percent." + name + "." + source,
                            total == 0 ? null : result.QiSources[realm][source] * 100 / total);
                    }
                }
            }
        }

        private static void SnapshotMetrics(SortedDictionary<string, List<double>> metrics, BalanceResult result)
        {
            foreach (BalanceSnapshot snapshot in result.Snapshots)
            {
                Add(metrics, "snapshot." + snapshot.Year + ".stones", snapshot.Stones);
                for (int realm = 0; realm < Realms.Count; realm++)
                {
                    Add(metrics, "snapshot." + snapshot.Year + "." + Realms.Name(realm), snapshot.Realms[realm]);
                }
            }
        }

        private static void Add(SortedDictionary<string, List<double>> metrics, string key, double? value)
        {
            if (!metrics.TryGetValue(key, out List<double>? values))
            {
                values = new List<double>();
                metrics.Add(key, values);
            }

            if (value is { } number)
            {
                values.Add(number);
            }
        }

        private static double Median(List<double> sorted) => sorted.Count % 2 == 0
            ? (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2 : sorted[sorted.Count / 2];

        private static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
