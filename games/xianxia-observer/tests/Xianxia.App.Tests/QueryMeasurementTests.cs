// 실제 UI 질의의 3000명 비용을 원시 표본으로 남긴다. 공유 CI의 벽시계 한계로 시간 임계는 강제하지 않는다.
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.Simulation;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>복제한 정렬 대신 앱이 사용하는 실제 질의를 측정한다.</summary>
    public sealed class QueryMeasurementTests
    {
        /// <summary>8개 정렬의50회 표본과 p95를 기록하고 원본·RNG 불변도 확인한다.</summary>
        [Test]
        public void PeopleQuery_3000People_RecordsCostAndPreservesWorld()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 3000 });
            WorldSimulator.Run(world, 1000);
            byte[] before = WorldSerializer.ToBytes(world);
            List<SortMeasurement> measurements = new();
            for (int sort = 0; sort < 8; sort++)
            {
                PeopleQuery.Select(world, sort, 0, "");
                List<double> times = new();
                for (int run = 0; run < 50; run++)
                {
                    long start = Stopwatch.GetTimestamp();
                    Cultivator[] people = PeopleQuery.Select(world, sort, 0, "").ToArray();
                    times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    Assert.That(people, Has.Length.EqualTo(3000));
                }

                double[] ordered = times.Order().ToArray();
                measurements.Add(new SortMeasurement(sort, times.ToArray(), times.Average(), ordered[47]));
            }

            Assert.That(WorldSerializer.ToBytes(world), Is.EqualTo(before));
            string json = JsonSerializer.Serialize(measurements);
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "query-3000-measurements.json");
            File.WriteAllText(path, json);
            TestContext.Out.WriteLine(json);
        }

        /// <summary>정렬 하나의 원시 비용.</summary>
        /// <param name="Sort">앱의 정렬 위치.</param>
        /// <param name="SamplesMs">50회 원시 표본.</param>
        /// <param name="MeanMs">평균.</param>
        /// <param name="P95Ms">최근접 순위 p95.</param>
        private sealed record SortMeasurement(int Sort, double[] SamplesMs, double MeanMs, double P95Ms);
    }
}
