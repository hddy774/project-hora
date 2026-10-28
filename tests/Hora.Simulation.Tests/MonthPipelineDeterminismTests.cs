// 120개월 재현성과 빈 시스템 처리의 월별 관리 힙 할당을 검증한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;
using NUnit.Framework;

namespace Hora.Simulation.Tests
{
    /// <summary>서로 다른 실행의 상태 요약과 재사용 버퍼 성능 계약 테스트.</summary>
    [TestFixture]
    public sealed class MonthPipelineDeterminismTests
    {
        /// <summary>빈 시스템 10개를 120개월 실행한 결과가 시드별로 재현된다.</summary>
        [TestCase(0UL)]
        [TestCase(42UL)]
        [TestCase(ulong.MaxValue)]
        public void Resolve_120Months_SameSeed_SameHash(ulong seed)
        {
            // Arrange
            var first = new WorldState(seed);
            var second = new WorldState(seed);
            MonthPipeline firstPipeline = CreateTenSystems();
            MonthPipeline secondPipeline = CreateTenSystems();

            // Act
            RunMonths(firstPipeline, first, 120);
            RunMonths(secondPipeline, second, 120);

            // Assert
            Assert.That(first.MonthIndex, Is.EqualTo(120));
            Assert.That(first.Phase, Is.EqualTo(TurnPhase.Planning));
            Assert.That(first.ComputeStateHash(), Is.EqualTo(second.ComputeStateHash()));
        }

        /// <summary>생성 및 JIT 예열 이후 진행률 없는 120개월 처리는 관리 힙을 할당하지 않는다.</summary>
        [Test]
        public void Resolve_120MonthsWithoutProgress_AllocatesZeroBytes()
        {
            // Arrange
            MonthPipeline pipeline = CreateTenSystems();
            var world = new WorldState(42);
            RunMonths(pipeline, world, 120);
            GC.GetAllocatedBytesForCurrentThread();

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            RunMonths(pipeline, world, 120);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.That(allocated, Is.Zero);
            Assert.That(world.MonthIndex, Is.EqualTo(240));
        }

        /// <summary>같은 세계는 성능 측정과 진행률 수신 여부가 달라도 해시가 같다.</summary>
        [Test]
        public void Resolve_ProgressObserver_DoesNotChangeWorldHash()
        {
            // Arrange
            MonthPipeline pipeline = CreateTenSystems();
            var first = new WorldState(42);
            var second = new WorldState(42);
            int reports = 0;
            var progress = new PipelineTestProgress(_ => reports++);

            // Act
            pipeline.Resolve(first);
            pipeline.Resolve(second, progress);

            // Assert
            Assert.That(first.ComputeStateHash(), Is.EqualTo(second.ComputeStateHash()));
            Assert.That(reports, Is.EqualTo(10));
        }

        /// <summary>할당 측정 전에 빈 단계 열 개와 고정 실행 버퍼를 생성한다.</summary>
        private static MonthPipeline CreateTenSystems()
        {
            var systems = new ISimulationSystem[10];
            for (int index = 0; index < systems.Length; index++)
            {
                systems[index] = new PipelineTestSystem();
            }

            return new MonthPipeline(systems);
        }

        /// <summary>측정 구간에 테스트 프레임워크 할당을 넣지 않고 지정 개월을 진행한다.</summary>
        private static void RunMonths(MonthPipeline pipeline, WorldState world, int months)
        {
            for (int month = 0; month < months; month++)
            {
                pipeline.Resolve(world);
                world.BeginPlanning();
            }
        }
    }
}
