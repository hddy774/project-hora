// 보고서 재열람과 고정 바이트 순서의 상태 해시를 검증한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using NUnit.Framework;

namespace Hora.Simulation.Tests
{
    /// <summary>최소 세계의 단계 전환과 결정론적 요약 값 테스트.</summary>
    [TestFixture]
    public sealed class WorldStateTests
    {
        /// <summary>새 세계는 월 0의 계획 단계에서 시작한다.</summary>
        [Test]
        public void Constructor_Seed_StartsAtMonthZeroPlanning()
        {
            // Arrange / Act
            var world = new WorldState(42);

            // Assert
            Assert.That(world.Seed, Is.EqualTo(42));
            Assert.That(world.MonthIndex, Is.Zero);
            Assert.That(world.Phase, Is.EqualTo(TurnPhase.Planning));
        }

        /// <summary>보고서 닫기는 이미 증가한 월을 다시 진행하지 않는다.</summary>
        [Test]
        public void BeginPlanning_FromReport_EntersPlanning()
        {
            // Arrange
            var world = new WorldState(42) { MonthIndex = 120, Phase = TurnPhase.Report };

            // Act
            bool transitioned = world.BeginPlanning();
            bool repeated = world.BeginPlanning();

            // Assert
            Assert.That(transitioned, Is.True);
            Assert.That(repeated, Is.False);
            Assert.That(world.MonthIndex, Is.EqualTo(120));
            Assert.That(world.Phase, Is.EqualTo(TurnPhase.Planning));
        }

        /// <summary>정산 중의 보고서 닫기 입력으로 계획을 다시 열 수 없다.</summary>
        [Test]
        public void BeginPlanning_FromResolving_LeavesStateUntouched()
        {
            // Arrange
            var world = new WorldState(42) { Phase = TurnPhase.Resolving };
            ulong before = world.ComputeStateHash();

            // Act
            bool changed = world.BeginPlanning();

            // Assert
            Assert.That(changed, Is.False);
            Assert.That(world.ComputeStateHash(), Is.EqualTo(before));
        }

        /// <summary>명시한 리틀 엔디언 16바이트 FNV-1a 값과 대조한다.</summary>
        [TestCase(0, TurnPhase.Planning, 0x9D44C7352C418DCFUL)]
        [TestCase(120, TurnPhase.Report, 0x63C9058533C982D5UL)]
        public void ComputeStateHash_ExplicitBytes_MatchesGolden(int month, TurnPhase phase, ulong expected)
        {
            // Arrange: 기대값은 Python의 독립 바이트 루프에서 구한 FNV-1a 64이다.
            var world = new WorldState(42) { MonthIndex = month, Phase = phase };

            // Act
            ulong actual = world.ComputeStateHash();

            // Assert
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
