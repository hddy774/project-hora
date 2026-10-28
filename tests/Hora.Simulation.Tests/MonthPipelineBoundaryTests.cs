// 정산 예외, 재진입과 결과 버퍼 수명에 대한 경계 조건을 검증한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Hora.Simulation.Tests
{
    /// <summary>잘못된 호출과 실패가 다른 정산을 오염시키지 않는지 확인한다.</summary>
    [TestFixture]
    public sealed class MonthPipelineBoundaryTests
    {
        /// <summary>시스템의 버그 예외는 전파하고 완료된 월로 표시하지 않는다.</summary>
        [Test]
        public void Resolve_SystemThrows_LeavesResolvingWithoutAdvancing()
        {
            // Arrange
            var expected = new InvalidOperationException("test bug");
            PipelineTestSystem[] systems = new[] { new PipelineTestSystem(execute: (_, _) => throw expected) };
            var pipeline = new MonthPipeline(systems);
            var world = new WorldState(42);

            // Act
            Exception? actual = Assert.Throws<InvalidOperationException>(() => pipeline.Resolve(world));

            // Assert
            Assert.That(actual, Is.SameAs(expected));
            Assert.That(world.Phase, Is.EqualTo(TurnPhase.Resolving));
            Assert.That(world.MonthIndex, Is.Zero);
            Assert.That(pipeline.Resolve(world).IsSuccess, Is.False);
        }

        /// <summary>진행률 콜백 실패도 완료 커밋으로 취급하지 않는다.</summary>
        [Test]
        public void Resolve_ProgressThrows_LeavesResolvingWithoutAdvancing()
        {
            // Arrange
            var pipeline = new MonthPipeline(new[] { new PipelineTestSystem() });
            var world = new WorldState(42);
            var progress = new PipelineTestProgress(_ => throw new InvalidOperationException("test callback"));

            // Act / Assert
            Assert.Throws<InvalidOperationException>(() => pipeline.Resolve(world, progress));
            Assert.That(world.Phase, Is.EqualTo(TurnPhase.Resolving));
            Assert.That(world.MonthIndex, Is.Zero);
        }

        /// <summary>별도 계획 세계를 전달한 재진입도 공유 컨텍스트 변경 전에 거절한다.</summary>
        [Test]
        public void Resolve_ReentersWithDifferentWorld_RejectsWithoutChangingEitherRun()
        {
            // Arrange
            var pipeline = new MonthPipeline(new[] { new PipelineTestSystem() });
            var outer = new WorldState(42);
            var nested = new WorldState(99);
            ResolveResult? nestedResult = null;
            var progress = new PipelineTestProgress(_ => nestedResult = pipeline.Resolve(nested));

            // Act
            ResolveResult result = pipeline.Resolve(outer, progress);

            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(nestedResult, Is.Not.Null);
            Assert.That(nestedResult!.IsSuccess, Is.False);
            Assert.That(outer.MonthIndex, Is.EqualTo(1));
            Assert.That(nested.MonthIndex, Is.Zero);
            Assert.That(nested.Phase, Is.EqualTo(TurnPhase.Planning));
            Assert.That(result.Timings.Count, Is.EqualTo(1));
        }

        /// <summary>월 번호가 넘치면 어떤 시스템도 실행하지 않고 계획 상태를 보존한다.</summary>
        [Test]
        public void Resolve_MonthOverflows_FailsBeforeExecuting()
        {
            // Arrange
            int calls = 0;
            var system = new PipelineTestSystem(execute: (_, _) => calls++);
            var pipeline = new MonthPipeline(new[] { system });
            var world = new WorldState(42) { MonthIndex = int.MaxValue };
            ulong before = world.ComputeStateHash();

            // Act / Assert
            Assert.Throws<OverflowException>(() => pipeline.Resolve(world));
            Assert.That(calls, Is.Zero);
            Assert.That(world.ComputeStateHash(), Is.EqualTo(before));
        }

        /// <summary>성공 결과 재사용과 외부 수정 불가능한 시간 목록을 보장한다.</summary>
        [Test]
        public void Resolve_TwoMonths_ReusesBorrowedReadOnlyResult()
        {
            // Arrange
            var pipeline = new MonthPipeline(new[] { new PipelineTestSystem() });
            var world = new WorldState(42);
            ResolveResult first = pipeline.Resolve(world);

            // Act
            world.BeginPlanning();
            ResolveResult second = pipeline.Resolve(world);

            // Assert
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Timings, Is.SameAs(first.Timings));
            Assert.That(second.Timings, Is.Not.InstanceOf<SystemTiming[]>());
            var mutableView = (IList<SystemTiming>)second.Timings;
            Assert.Throws<NotSupportedException>(() => mutableView[0] = default);
        }

        /// <summary>null 입력은 호출 코드의 버그이며 등록 또는 실행 시 즉시 알린다.</summary>
        [Test]
        public void ConstructorAndResolve_NullArguments_Throw()
        {
            // Arrange
            var pipeline = new MonthPipeline(Array.Empty<ISimulationSystem>());

            // Act / Assert
            Assert.Throws<ArgumentNullException>(() => new MonthPipeline(null!));
            Assert.Throws<ArgumentException>(() => new MonthPipeline(new ISimulationSystem[] { null! }));
            Assert.Throws<ArgumentException>(() => new MonthPipeline(new[] { new PipelineTestSystem(null!) }));
            Assert.Throws<ArgumentNullException>(() => pipeline.Resolve(null!));
        }

        /// <summary>월별 1000개 난수 구간을 넘는 시스템 등록은 월간 충돌을 만들므로 거절한다.</summary>
        [Test]
        public void Constructor_OverOneThousandSystems_RejectsStreamOverlap()
        {
            // Arrange
            var systems = new ISimulationSystem[1001];

            // Act / Assert
            Assert.Throws<ArgumentException>(() => new MonthPipeline(systems));
        }
    }
}
