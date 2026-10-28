// 빈 시스템 10개로 120개월을 처리해 초기화 이후의 월당 비용을 관측한다.
// 관련 명세: docs/tasks/M0-04-month-pipeline.md
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Hora.Simulation;

namespace Hora.Tools.Benchmarks
{
    /// <summary>클라이언트·경제 데이터가 없는 M0 월 정산 골격의 측정 도구.</summary>
    internal static class PipelineProbe
    {
        private const int SystemCount = 10;
        private const int MonthsPerSample = 120;
        private const int WarmupSamples = 20;
        private const ulong Seed = 42;

        /// <summary>별도 준비 뒤 120개월을 실행하고 월당 시간·관리 힙 할당을 출력한다.</summary>
        /// <returns>측정 루프의 관리 힙 할당이 0이면 0, 아니면 1.</returns>
        internal static int Run()
        {
            Console.WriteLine("0% · 빈 시스템 10개를 준비합니다.");
            MonthPipeline pipeline = CreatePipeline();
            WarmUp(pipeline);
            var world = new WorldState(Seed);
            Console.WriteLine("50% · 준비 완료. 120개월을 측정합니다.");
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            ResolveMonths(pipeline, world);
            long elapsed = Stopwatch.GetTimestamp() - started;
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            WriteResult(world, elapsed, allocatedBytes);
            return allocatedBytes == 0 ? 0 : 1;
        }

        private static MonthPipeline CreatePipeline()
        {
            var systems = new ISimulationSystem[SystemCount];
            for (int index = 0; index < systems.Length; index++)
            {
                systems[index] = new NoOpSystem();
            }

            return new MonthPipeline(systems);
        }

        private static void WarmUp(MonthPipeline pipeline)
        {
            for (int sample = 0; sample < WarmupSamples; sample++)
            {
                ResolveMonths(pipeline, new WorldState(Seed));
            }
        }

        /// <summary>측정 경계 안에서만 월 정산과 보고 종료를 반복한다.</summary>
        private static void ResolveMonths(MonthPipeline pipeline, WorldState world)
        {
            for (int month = 0; month < MonthsPerSample; month++)
            {
                ResolveResult result = pipeline.Resolve(world);
                if (!result.IsSuccess || !world.BeginPlanning())
                {
                    throw new InvalidOperationException("측정 중 월 진행 상태가 일치하지 않습니다.");
                }
            }
        }

        /// <summary>측정 종료 후 문자열을 만들며 호스트·단위·해시를 함께 기록한다.</summary>
        private static void WriteResult(WorldState world, long elapsedTicks, long allocatedBytes)
        {
            // 시간 관측의 소수점은 게임 금액·난수·상태 계산에 들어가지 않는다.
            double microsecondsPerMonth = elapsedTicks * 1000000.0 / Stopwatch.Frequency / MonthsPerSample;
            Console.WriteLine("100% · 빈 월 정산 측정 완료 (실제 경제·Android 성능과 별도)");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"systems={SystemCount} months={MonthsPerSample} mean_us={microsecondsPerMonth:F3}"));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"allocated_bytes={allocatedBytes} bytes_per_month={allocatedBytes / MonthsPerSample}"));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"state_hash={world.ComputeStateHash():x16} runtime={RuntimeInformation.FrameworkDescription}"));
        }
    }
}
