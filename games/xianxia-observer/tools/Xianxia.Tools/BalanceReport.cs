// 고정 시드의 장기 실행과 원문 보고. 수치 조정이나 결과 해석은 수행하지 않는다.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xianxia.Simulation;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.Tools
{
    /// <summary>다중 시드·경지 체류·영기·저장 측정 보고.</summary>
    internal static class BalanceReport
    {
        private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };

        /// <summary>각 시드와 최소·중앙·최대를 출력하고 선택적으로 JSON을 쓴다.</summary>
        /// <param name="options">고정 실행 조건.</param>
        internal static void Run(ReportOptions options)
        {
            List<BalanceResult> results = new();
            foreach (ulong seed in options.Seeds)
            {
                results.Add(RunSeed(seed, options));
            }

            BalanceSummary.Print(results);
            if (options.JsonPath is { } path)
            {
                string absolute = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                File.WriteAllText(absolute, JsonSerializer.Serialize(results, s_json));
            }
        }

        private static BalanceResult RunSeed(ulong seed, ReportOptions options)
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = seed, Population = options.Population });
            if (Environment.GetEnvironmentVariable("XIANXIA_DIAGNOSTICS") == "1")
            {
                world.Diagnostics = new WorldDiagnostics(world);
            }

            Console.WriteLine("== balance seed=" + seed + " population=" + options.Population
                + " years=" + options.Years + " diagnostics=" + (world.Diagnostics is not null) + " ==");
            List<BalanceSnapshot> snapshots = new() { Snapshot(world) };
            Stopwatch watch = Stopwatch.StartNew();
            while (world.Year < options.Years)
            {
                WorldSimulator.Step(world);
                if (world.Year % 5000 == 0)
                {
                    snapshots.Add(Snapshot(world));
                    Console.WriteLine("  snapshot " + world.Year + " realms=["
                        + string.Join(",", snapshots[^1].Realms) + "] stones=" + snapshots[^1].Stones);
                }
            }

            watch.Stop();
            Program.PrintSummary(world);
            BalanceResult result = Capture(world, snapshots);
            Print(result, world.Diagnostics);
            PrintTerritoryChanges(world);
            Console.WriteLine("  simulation ms/year=" + F(watch.Elapsed.TotalMilliseconds / options.Years));
            return result;
        }

        private static BalanceResult Capture(World world, List<BalanceSnapshot> snapshots)
        {
            if (TotalStones(world) != world.Ledger.Expected())
            {
                throw new InvalidDataException("영석 장부가 실제 총량과 다릅니다.");
            }

            BalanceResult result = new()
            {
                Seed = world.Settings.Seed,
                Years = world.Year,
                Population = world.Cultivators.Count,
                FirstReach = world.Records.FirstReach.Select(entry => entry?.Year).ToArray(),
                RealmCounts = StatisticsSystem.Snapshot(world).RealmCounts.ToArray(),
                Snapshots = snapshots,
                AscensionYears = world.Immortals.Select(entry => entry.Year).ToArray(),
                OrthodoxWins = world.Wars.Count(war => war.VictorSectId == 1),
                DemonicWins = world.Wars.Count(war => war.VictorSectId == 2),
                OrthodoxLosingStreak = LosingStreak(world, 1),
                DemonicLosingStreak = LosingStreak(world, 2),
                Stones = TotalStones(world),
                Souls = world.Souls.Count,
                Wars = world.Wars.Count,
                Tournaments = world.Tournaments.Count,
            };
            MeasureStorage(world, result);
            CaptureDiagnostics(world.Diagnostics, result);
            return result;
        }

        private static void CaptureDiagnostics(WorldDiagnostics? diagnostics, BalanceResult result)
        {
            if (diagnostics is null)
            {
                return;
            }

            result.DwellMean = new double[Realms.Count];
            result.DwellSamples = new long[Realms.Count];
            result.QiSources = new double[Realms.Count][];
            for (int realm = 0; realm < Realms.Count; realm++)
            {
                long count = diagnostics.DwellSamples(realm);
                result.DwellSamples[realm] = count;
                result.DwellMean[realm] = count == 0 ? 0 : (double)diagnostics.DwellYears(realm) / count;
                result.QiSources[realm] = Enum.GetValues<QiSource>()
                    .Select(source => diagnostics.QiGained(realm, source)).ToArray();
            }
        }

        private static void MeasureStorage(World world, BalanceResult result)
        {
            WorldSerializer.FromBytes(WorldSerializer.ToBytes(world));
            double saves = 0;
            double loads = 0;
            for (int run = 0; run < 5; run++)
            {
                long start = Stopwatch.GetTimestamp();
                byte[] bytes = WorldSerializer.ToBytes(world);
                saves += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                start = Stopwatch.GetTimestamp();
                World loaded = WorldSerializer.FromBytes(bytes);
                loads += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                if (loaded.Year != world.Year || loaded.Cultivators.Count != world.Cultivators.Count)
                {
                    throw new InvalidDataException("측정 저장 왕복이 원본과 다릅니다.");
                }

                result.SaveBytes = bytes.Length;
            }

            result.SaveMs = saves / 5;
            result.LoadMs = loads / 5;
        }

        private static void Print(BalanceResult result, WorldDiagnostics? diagnostics)
        {
            Console.WriteLine("  first-reach=" + string.Join(",", result.FirstReach.Select(year => year?.ToString(
                CultureInfo.InvariantCulture) ?? "-")));
            Console.WriteLine("  ascension-years=[" + string.Join(",", result.AscensionYears) + "] losing-streak="
                + result.OrthodoxLosingStreak + ":" + result.DemonicLosingStreak);
            for (int realm = 0; realm < result.DwellMean.Length; realm++)
            {
                Console.WriteLine("  dwell " + Realms.Name(realm) + " mean="
                    + (result.DwellSamples[realm] == 0 ? "-" : F(result.DwellMean[realm])) + " n="
                    + result.DwellSamples[realm] + " downward=" + diagnostics!.DownwardExits(realm));
                if (realm >= 6)
                {
                    double total = result.QiSources[realm].Sum();
                    Console.WriteLine("  qi-source " + Realms.Name(realm)
                        + " [수련,기연,비경,천기,지도]=" + string.Join(",", result.QiSources[realm]
                            .Select(value => total == 0 ? "-" : F(value * 100 / total) + "%")));
                }
            }

            Console.WriteLine("  storage bytes=" + result.SaveBytes + " save-ms=" + F(result.SaveMs)
                + " load-ms=" + F(result.LoadMs) + " (5회 평균) records souls/wars/tournaments="
                + result.Souls + "/" + result.Wars + "/" + result.Tournaments);
        }

        private static void PrintTerritoryChanges(World world)
        {
            int orthodox = 0;
            int demonic = 0;
            foreach (WarRecord war in world.Wars)
            {
                Console.WriteLine("  territory war=" + war.Number + " orthodox=" + orthodox + "→"
                    + war.OrthodoxRegions + " demonic=" + demonic + "→" + war.DemonicRegions);
                orthodox = war.OrthodoxRegions;
                demonic = war.DemonicRegions;
            }
        }

        private static BalanceSnapshot Snapshot(World world) => new()
        {
            Year = world.Year,
            Realms = StatisticsSystem.Snapshot(world).RealmCounts.ToArray(),
            Stones = TotalStones(world),
        };

        private static long TotalStones(World world) => world.Cultivators.Sum(person => person.SpiritStones)
            + world.Sects.Sum(sect => sect.Treasury);

        private static int LosingStreak(World world, int sectId)
        {
            int current = 0;
            int maximum = 0;
            foreach (WarRecord war in world.Wars)
            {
                current = war.VictorSectId != 0 && war.VictorSectId != sectId ? current + 1 : 0;
                maximum = Math.Max(maximum, current);
            }

            return maximum;
        }

        private static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
