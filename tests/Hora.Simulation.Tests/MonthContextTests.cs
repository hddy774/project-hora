// 시스템별 월 난수 구간과 ref 반환의 상태 보존을 검증한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;
using Hora.Domain.Random;
using NUnit.Framework;

namespace Hora.Simulation.Tests
{
    /// <summary>월과 등록 인덱스가 난수에 미치는 영향을 독립적으로 확인한다.</summary>
    [TestFixture]
    public sealed class MonthContextTests
    {
        /// <summary>같은 월과 시스템이면 명세의 PCG 스트림과 정확히 같은 수열이다.</summary>
        [TestCase(0, 0)]
        [TestCase(120, 9)]
        [TestCase(int.MaxValue - 1, 999)]
        public void RandomFor_SameMonthAndSystem_IsDeterministic(int month, int system)
        {
            // Arrange
            var context = new MonthContext(1000);
            context.Reset(42, month);
            var expected = new DeterministicRandom(42, ((ulong)month * 1000) + (ulong)system);

            // Act / Assert
            for (int sample = 0; sample < 10; sample++)
            {
                Assert.That(context.RandomFor(system).NextUInt(), Is.EqualTo(expected.NextUInt()));
            }

            Assert.That(context.MonthIndex, Is.EqualTo(month));
        }

        /// <summary>시스템 하나의 추가 난수 호출이 다른 시스템의 상태를 바꾸지 않는다.</summary>
        [Test]
        public void RandomFor_OtherStreamAdvances_DoesNotChangeSelectedStream()
        {
            // Arrange
            var context = new MonthContext(2);
            context.Reset(42, 5);
            var expected = new DeterministicRandom(42, 5001);

            // Act
            for (int sample = 0; sample < 100; sample++)
            {
                context.RandomFor(0).NextUInt();
            }

            // Assert
            Assert.That(context.RandomFor(1).NextUInt(), Is.EqualTo(expected.NextUInt()));
        }

        /// <summary>끝에 시스템을 추가해도 기존 등록 인덱스의 수열은 유지된다.</summary>
        [Test]
        public void RandomFor_AppendedSystem_KeepsPreviousStreams()
        {
            // Arrange
            var original = new MonthContext(2);
            var expanded = new MonthContext(3);
            original.Reset(42, 120);
            expanded.Reset(42, 120);

            // Act / Assert
            for (int sample = 0; sample < 10; sample++)
            {
                Assert.That(expanded.RandomFor(1).NextUInt(), Is.EqualTo(original.RandomFor(1).NextUInt()));
            }
        }

        /// <summary>잘못된 인덱스는 checked 캐스트 오류 대신 지정한 인수 오류를 낸다.</summary>
        [TestCase(-1)]
        [TestCase(2)]
        public void RandomFor_InvalidIndex_ThrowsArgumentError(int index)
        {
            // Arrange
            var context = new MonthContext(2);

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => context.RandomFor(index).NextUInt());
        }
    }
}
