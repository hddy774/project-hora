// 헤드리스 도구: 세계를 화면 없이 돌려 경지 분포·전쟁·대회·장부·성능을 보고한다. 밸런스 조정 근거를 만든다.
// 사용법: dotnet run -c Release --project tools/Xianxia.Tools -- report [--seeds 1,2,3] [--years 30000] [--population
// 1000]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xianxia.Simulation;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.Tools
{
    /// <summary>명령줄 도구.</summary>
    public static class Program
    {
        /// <summary>프로그램 진입점.</summary>
        /// <param name="args">명령줄 인자.</param>
        /// <returns>종료 코드.</returns>
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string command = args.Length > 0 ? args[0] : "report";
            List<ulong> seeds = new() { 1, 2, 3 };
            int years = 30000;
            int population = Balance.DefaultPopulation;
            for (int index = 1; index + 1 < args.Length; index += 2)
            {
                switch (args[index])
                {
                    case "--seeds":
                        seeds = new List<ulong>(Array.ConvertAll(args[index + 1].Split(','), ulong.Parse));
                        break;
                    case "--years":
                        years = int.Parse(args[index + 1], CultureInfo.InvariantCulture);
                        break;
                    case "--population":
                        population = int.Parse(args[index + 1], CultureInfo.InvariantCulture);
                        break;
                }
            }

            if (command != "report")
            {
                Console.Error.WriteLine("알 수 없는 명령: " + command);
                return 2;
            }

            foreach (ulong seed in seeds)
            {
                Report(seed, years, population);
            }

            return 0;
        }

        private static void Report(ulong seed, int years, int population)
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = seed, Population = population });
            Console.WriteLine("== 시드 " + seed + ", 인구 " + population + ", " + years + "년 ==");
            int[] checkpoints = { 1000, 3000, 5000, 10000, 20000, 30000, 50000, 100000 };
            Stopwatch watch = Stopwatch.StartNew();
            int next = 0;
            while (world.Year < years)
            {
                WorldSimulator.Step(world);
                if (next < checkpoints.Length && world.Year == checkpoints[next])
                {
                    PrintCheckpoint(world, watch);
                    next++;
                }
            }

            watch.Stop();
            Console.WriteLine("  평균 " + (watch.Elapsed.TotalMilliseconds / years).ToString("0.000",
                CultureInfo.InvariantCulture)
                + " ms/년");
            PrintSummary(world);
        }

        private static void PrintGrowth(World world)
        {
            for (int realm = 0; realm < Realms.Count; realm++)
            {
                List<double> gains = new();
                double chance = 0;
                double age = 0;
                double lifespan = 0;
                foreach (Cultivator cultivator in world.Cultivators)
                {
                    if (cultivator.Realm != realm)
                    {
                        continue;
                    }

                    gains.Add(Growth.QiPerYear(world, cultivator));
                    chance += realm < Realms.Max ? BreakthroughSystem.MajorChance(world, cultivator) : 0;
                    age += cultivator.Age;
                    lifespan += cultivator.Lifespan;
                }

                if (gains.Count == 0)
                {
                    continue;
                }

                gains.Sort();
                double median = gains[gains.Count / 2];
                double p90 = gains[(int)(gains.Count * 0.9)];
                long total = 0;
                for (int stage = 0; stage < Realms.StageCount; stage++)
                {
                    total += Realms.Requirement(realm, stage);
                }

                Console.WriteLine("    " + Realms.Name(realm) + " n=" + gains.Count + " 영기/년 중앙 "
                    + median.ToString("0.0", CultureInfo.InvariantCulture) + " p90 " + p90.ToString("0.0",
                        CultureInfo.InvariantCulture)
                    + " 채움(중앙,년) " + (total / (median * 0.48)).ToString("0", CultureInfo.InvariantCulture)
                    + " 대경지확률 " + (chance / gains.Count).ToString("0.00", CultureInfo.InvariantCulture)
                    + " 나이/수명 " + (age / gains.Count).ToString("0", CultureInfo.InvariantCulture) + "/"
                    + (lifespan / gains.Count).ToString("0", CultureInfo.InvariantCulture));
            }
        }

        private static void PrintCheckpoint(World world, Stopwatch watch)
        {
            StatSample sample = StatisticsSystem.Snapshot(world);
            Console.WriteLine("  " + world.Year + "년: 경지 [" + string.Join(", ", sample.RealmCounts) + "] 청운 "
                + sample.OrthodoxMembers + "/" + sample.OrthodoxAverageRealm.ToString("0.00",
                    CultureInfo.InvariantCulture)
                + " 천마 " + sample.DemonicMembers + "/" + sample.DemonicAverageRealm.ToString("0.00",
                    CultureInfo.InvariantCulture)
                + " 산수 " + sample.Rogues + " 접경 " + sample.OrthodoxBorders + ":" + sample.DemonicBorders
                + " 금고 " + KoreanNumber.Compact(sample.OrthodoxTreasury) + ":"
                    + KoreanNumber.Compact(sample.DemonicTreasury)
                + " (" + watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "초)");
            if (Environment.GetEnvironmentVariable("XIANXIA_GROWTH") == "1")
            {
                PrintGrowth(world);
            }
        }

        private static void PrintSummary(World world)
        {
            WorldTotals totals = world.Totals;
            Console.WriteLine("  좌화 " + totals.NaturalDeaths + " / 붕괴 " + totals.CollapseDeaths + " / 비승 "
                + totals.Ascensions
                + " / 대경지 돌파 " + totals.MajorBreakthroughs + " / 천겁 " + totals.Tribulations + " / 결투 " + totals.Battles
                + " / 비무 " + totals.Spars + " / 배신 " + totals.Defections);
            for (int realm = 1; realm < Realms.Count; realm++)
            {
                RecordEntry? first = world.Records.FirstReach[realm];
                Console.WriteLine("  최초 " + Realms.Name(realm) + ": " + (first is null ? "-" : first.Year + "년 "
                    + first.Name));
            }

            foreach (WarRecord war in world.Wars)
            {
                Console.WriteLine("  전쟁 " + war.Number + ": 승자 " + Narrator.Affiliation(world, war.VictorSectId)
                    + " 전투 "
                    + war.OrthodoxWins + ":" + war.DemonicWins + " 점령 " + war.OrthodoxRegions + ":" + war.DemonicRegions
                    + " 하락 " + war.Casualties + " 영웅 " + war.HeroName);
            }

            Console.WriteLine("  대비무 " + world.Tournaments.Count + "회, 비경 진행 " + world.SecretRealms.Count + ", 수조 진행 "
                + world.BeastTides.Count + ", 연대기 " + world.Chronicle.Entries.Count + "건, 시대 " + world.Era.Count + "회");
            long actual = 0;
            foreach (Cultivator cultivator in world.Cultivators)
            {
                actual += cultivator.SpiritStones;
            }

            foreach (Sect sect in world.Sects)
            {
                actual += sect.Treasury;
                Console.WriteLine("  " + sect.Name + ": 시설 [" + string.Join(", ", sect.Facilities) + "] 전쟁 "
                    + sect.WarsWon
                    + "승 " + sect.WarsLost + "패, 대회 우승 " + sect.TournamentWins + ", 종주 " + sect.LeaderCount + "대");
            }

            Console.WriteLine("  영석 장부 " + world.Ledger.Expected() + " / 실제 " + actual
                + (world.Ledger.Expected() == actual ? " (일치)" : " (불일치!)"));
            Console.WriteLine("  로그 분류별: " + string.Join(", ", LogCounts(world)));
        }

        private static IEnumerable<string> LogCounts(World world)
        {
            for (int kind = 0; kind < world.Log.Counts.Count; kind++)
            {
                yield return Labels.Of((LogKind)kind) + " " + world.Log.Counts[kind];
            }
        }
    }
}
