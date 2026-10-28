// 엔진 없이 금액 계산과 월 처리 골격을 측정하는 CLI 진입점이다.
// 관련 명세: docs/tasks/M0-02-money-quantity.md, M0-04-month-pipeline.md
using System;
using System.Collections.Generic;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Hora.Tools.Benchmarks;

namespace Hora.Tools
{
    /// <summary>코어 개발 도구의 명령 진입점.</summary>
    internal static class Program
    {
        /// <summary>측정 명령을 선택하고 실패·잘못된 명령을 0이 아닌 종료 코드로 알린다.</summary>
        private static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                PrintHelp();
                return 0;
            }

            switch (args[0])
            {
                case "bench-pipeline":
                    return PipelineProbe.Run();
                case "bench-money":
                    string[] options = args.Length == 1 ? new[] { "--filter", "*" } : args[1..];
                    IEnumerable<Summary> summaries =
                        BenchmarkSwitcher.FromTypes(new[] { typeof(MoneyBenchmarks) }).Run(options);
                    return AreBenchmarksSuccessful(summaries) ? 0 : 1;
                default:
                    PrintHelp();
                    return 2;
            }
        }

        /// <summary>검증 오류와 실행 실패를 모두 확인하여 측정 없는 성공 표시를 막는다.</summary>
        private static bool AreBenchmarksSuccessful(IEnumerable<Summary> summaries)
        {
            bool hasReports = false;
            foreach (Summary summary in summaries)
            {
                if (summary.HasCriticalValidationErrors || summary.Reports.Length == 0)
                {
                    return false;
                }

                foreach (BenchmarkReport report in summary.Reports)
                {
                    if (!report.Success || report.ResultStatistics is null)
                    {
                        return false;
                    }

                    hasReports = true;
                }
            }

            return hasReports;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Hora Economic Lab · 호라경제연구실 — C# 코어 개발 도구");
            Console.WriteLine("bench-pipeline : 빈 시스템 10개 × 120개월의 월당 시간·할당 측정");
            Console.WriteLine("bench-money [BenchmarkDotNet 옵션] : 금액 곱셈·보존 배분 측정");
        }
    }
}
