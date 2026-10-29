// 압축된 통계의 실제 시간 간격을 균일한 샘플 순서로 왜곡하지 않는다.
using NUnit.Framework;
using Xianxia.App.Controls;

namespace Xianxia.App.Tests
{
    /// <summary>꺾은선과 누적 띠가 함께 사용하는 실제 연도 축의 회귀.</summary>
    public sealed class ChartTimelineTests
    {
        private static readonly int[] s_offset = { 1200, 1210, 1300 };
        private static readonly int[] s_sameYear = { 100, 100 };
        /// <summary>1년 표본은0~100년 축의1% 위치에 있어야 한다.</summary>
        [Test]
        public void Chart_UsesSampleYears()
        {
            int[] years = { 0, 1, 100 };
            Assert.That(ChartBase.YearFraction(years, 0), Is.Zero);
            Assert.That(ChartBase.YearFraction(years, 1), Is.EqualTo(.01).Within(1e-9));
            Assert.That(ChartBase.YearFraction(years, 2), Is.EqualTo(1));
            Assert.That(ChartBase.YearFraction(s_offset, 1), Is.EqualTo(.1).Within(1e-9));
            Assert.That(ChartBase.YearFraction(s_sameYear, 1), Is.Zero);
        }
    }
}
