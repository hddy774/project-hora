// 월 전환, 고정 실행 순서와 실제 단계 진행률을 검증한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Hora.Simulation.Tests
{
    /// <summary>사용자가 한 달을 중복 처리하지 못하는 정산 계약 테스트.</summary>
    [TestFixture]
    public sealed class MonthPipelineTests
    {
        /// <summary>계획 단계가 아닌 상태에서는 시스템 호출과 월 변경을 거절한다.</summary>
        [TestCase(TurnPhase.Resolving)]
        [TestCase(TurnPhase.Report)]
        public void Resolve_WhenNotPlanning_ReturnsFailureWithoutRunning(TurnPhase phase)
        {
            // Arrange
            int callCount = 0;
            var system = new PipelineTestSystem(execute: (_, _) => callCount++);
            var pipeline = new MonthPipeline(new[] { system });
            var world = new WorldState(42) { Phase = phase };
            ulong before = world.ComputeStateHash();

            // Act
            ResolveResult result = pipeline.Resolve(world);

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Timings, Is.Empty);
            Assert.That(callCount, Is.Zero);
            Assert.That(world.ComputeStateHash(), Is.EqualTo(before));
        }

        /// <summary>등록 배열의 순서를 정확히 한 번씩 실행하고 정산 월을 전달한다.</summary>
        [Test]
        public void Resolve_RunsSystemsInRegistrationOrder()
        {
            // Arrange
            var calls = new List<int>();
            var systems = new ISimulationSystem[3];
            for (int index = 0; index < systems.Length; index++)
            {
                int capturedIndex = index;
                systems[index] = new PipelineTestSystem(execute: (world, context) =>
                {
                    Assert.That(world.Phase, Is.EqualTo(TurnPhase.Resolving));
                    Assert.That(context.MonthIndex, Is.Zero);
                    calls.Add(capturedIndex);
                });
            }

            // Act
            var pipeline = new MonthPipeline(systems);
            pipeline.Resolve(new WorldState(42));

            // Assert
            Assert.That(calls.Count, Is.EqualTo(3));
            for (int index = 0; index < calls.Count; index++)
            {
                Assert.That(calls[index], Is.EqualTo(index));
            }
        }

        /// <summary>한 달을 처리한 후 보고 단계를 열고 재호출은 거절한다.</summary>
        [Test]
        public void Resolve_IncrementsMonthAndEntersReport()
        {
            // Arrange
            var pipeline = new MonthPipeline(new[] { new PipelineTestSystem() });
            var world = new WorldState(42);

            // Act
            ResolveResult first = pipeline.Resolve(world);
            ResolveResult duplicate = pipeline.Resolve(world);

            // Assert
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(world.MonthIndex, Is.EqualTo(1));
            Assert.That(world.Phase, Is.EqualTo(TurnPhase.Report));
            Assert.That(duplicate.IsSuccess, Is.False);
        }

        /// <summary>빈 파이프라인도 한 달을 끝내며 없는 시스템의 진행률을 만들지 않는다.</summary>
        [Test]
        public void Resolve_NoSystems_CompletesWithoutProgress()
        {
            // Arrange
            var pipeline = new MonthPipeline(Array.Empty<ISimulationSystem>());
            var world = new WorldState(42);
            int reports = 0;
            var progress = new PipelineTestProgress(_ => reports++);

            // Act
            ResolveResult result = pipeline.Resolve(world, progress);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Timings, Is.Empty);
            Assert.That(world.MonthIndex, Is.EqualTo(1));
            Assert.That(world.Phase, Is.EqualTo(TurnPhase.Report));
            Assert.That(reports, Is.Zero);
        }

        /// <summary>단계를 마친 직후에만 1/2, 2/2 진행률을 같은 순서로 알린다.</summary>
        [Test]
        public void Resolve_ReportsProgressOncePerSystem()
        {
            // Arrange
            var pipeline = new MonthPipeline(new[]
            {
                new PipelineTestSystem("production"),
                new PipelineTestSystem("market"),
            });
            var reports = new List<PipelineProgress>();
            var world = new WorldState(42);
            var progress = new PipelineTestProgress(value =>
            {
                Assert.That(world.Phase, Is.EqualTo(TurnPhase.Resolving));
                reports.Add(value);
            });

            // Act
            ResolveResult result = pipeline.Resolve(world, progress);

            // Assert
            Assert.That(reports.Count, Is.EqualTo(2));
            Assert.That(reports[0].Completed, Is.EqualTo(1));
            Assert.That(reports[1].Completed, Is.EqualTo(2));
            Assert.That(reports[0].Total, Is.EqualTo(2));
            Assert.That(reports[1].Total, Is.EqualTo(2));
            Assert.That(reports[0].SystemName, Is.EqualTo("production"));
            Assert.That(reports[1].SystemName, Is.EqualTo("market"));
            Assert.That(result.Timings[0].SystemName, Is.EqualTo("production"));
            Assert.That(result.Timings[1].ElapsedMicroseconds, Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>생성자에 넘긴 외부 목록을 변경해도 이미 등록한 단계를 바꾸지 못한다.</summary>
        [Test]
        public void Constructor_InputArrayChanges_KeepsRegisteredSystem()
        {
            // Arrange
            var systems = new ISimulationSystem[] { new PipelineTestSystem("original") };
            var pipeline = new MonthPipeline(systems);
            systems[0] = new PipelineTestSystem("replacement");

            // Act
            ResolveResult result = pipeline.Resolve(new WorldState(42));

            // Assert
            Assert.That(result.Timings[0].SystemName, Is.EqualTo("original"));
        }
    }
}
