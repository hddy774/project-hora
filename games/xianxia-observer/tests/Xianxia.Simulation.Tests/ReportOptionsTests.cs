// 실제 장기 측정 명령의 시드 범위와 인자 누락을 검사한다.
using System;
using NUnit.Framework;
using Xianxia.Tools;

namespace Xianxia.Simulation.Tests
{
    /// <summary>시드 범위와 값 없는 balance 옵션의 실행 계약.</summary>
    public sealed class ReportOptionsTests
    {
        /// <summary>범위의 양끝을 포함하며 balance 뒤의 다른 옵션도 빠뜨리지 않는다.</summary>
        [Test]
        public void BalanceReport_ParsesSeedRangeAndFlag()
        {
            ReportOptions options = ReportOptions.Parse(
                "report --seeds 1-10 --balance --years 1000 --population 100".Split(' '));
            Assert.That(options.Seeds, Has.Count.EqualTo(10));
            for (int index = 0; index < 10; index++)
            {
                Assert.That(options.Seeds[index], Is.EqualTo((ulong)index + 1));
            }

            Assert.That(options.Balance, Is.True);
            Assert.That(options.Years, Is.EqualTo(1000));
            Assert.That(options.Population, Is.EqualTo(100));
        }

        /// <summary>중복은 제거하고 ulong 마지막 단일 범위에서도 무한 반복하지 않는다.</summary>
        [Test]
        public void Seeds_RemoveDuplicatesAndAcceptMaximumValue()
        {
            Assert.That(ReportOptions.ParseSeeds("1-3,2,4"), Has.Count.EqualTo(4));
            Assert.That(ReportOptions.ParseSeeds("18446744073709551615-18446744073709551615"),
                Is.EqualTo(new[] { ulong.MaxValue }));
        }

        /// <summary>역순·과대·숫자가 아닌 범위를 조용히 무시하지 않는다.</summary>
        /// <param name="value">잘못된 범위.</param>
        [TestCase("10-1")]
        [TestCase("1-1001")]
        [TestCase("1-")]
        [TestCase("x")]
        public void Seeds_RejectInvalidRange(string value)
        {
            Assert.Throws<FormatException>(() => ReportOptions.ParseSeeds(value));
        }

        /// <summary>누락 값과 알 수 없는 옵션은 실행 전에 거부한다.</summary>
        /// <param name="command">잘못된 명령.</param>
        [TestCase("report --years")]
        [TestCase("report --unknown 3")]
        [TestCase("report --years 0")]
        [TestCase("report --population 3001")]
        public void Options_RejectIncompleteOrInvalidCommand(string command)
        {
            Assert.Catch<ArgumentException>(() => ReportOptions.Parse(command.Split(' ')));
        }
    }
}
