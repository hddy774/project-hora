// 확정된 M/B 배율·1달러당 1,000원 환율·표시 경계를 검사한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md, R25~R26.
using System;
using System.Globalization;
using Hora.Domain.Values;
using NUnit.Framework;

namespace Hora.Domain.Tests
{
    /// <summary>통화 표시가 금액 범위·문화권에 따라 오표시되지 않는지 검증한다.</summary>
    [TestFixture]
    public sealed class MoneyFormatterTests
    {
        /// <summary>USD M/B 경계를 달러 기준으로 계산하고 한 자리까지만 내림한다.</summary>
        /// <param name="cents">USD 센트 금액.</param>
        /// <param name="expected">축약한 USD 표시.</param>
        [TestCase(99_999_999L, "$999,999.99")]
        [TestCase(100_000_000L, "$1M")]
        [TestCase(109_999_999L, "$1M")]
        [TestCase(110_000_000L, "$1.1M")]
        [TestCase(99_999_999_999L, "$999.9M")]
        [TestCase(100_000_000_000L, "$1B")]
        [TestCase(340_000_000_000L, "$3.4B")]
        [TestCase(-110_999_999L, "-$1.1M")]
        public void FormatShort_Usd_UsesMillionAndBillionDollars(long cents, string expected)
        {
            // Arrange / Act
            string result = MoneyFormatter.FormatShort(Money.FromCents(cents), CurrencyView.Usd);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>확정 원화 환산에서 100만 달러는 10억원, 10억 달러는 1조원이다.</summary>
        /// <param name="cents">USD 센트 금액.</param>
        /// <param name="expected">고정 환율로 축약한 원화 표시.</param>
        [TestCase(9_999_999L, "99,999,990원")]
        [TestCase(10_000_000L, "1억원")]
        [TestCase(100_000_000L, "10억원")]
        [TestCase(123_456_789L, "12.3억원")]
        [TestCase(99_999_999_999L, "9999.9억원")]
        [TestCase(100_000_000_000L, "1조원")]
        [TestCase(120_999_999_999L, "1.2조원")]
        [TestCase(-100_000_000L, "-10억원")]
        public void FormatShort_Krw_UsesFixedRateAndKoreanScales(long cents, string expected)
        {
            // Arrange / Act
            string result = MoneyFormatter.FormatShort(Money.FromCents(cents), CurrencyView.Krw);

            // Assert
            Assert.That(MoneyFormatter.KrwPerUsd, Is.EqualTo(1000));
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>정확한 USD 표기는 음수·0·최소 정수에도 항상 센트 두 자리를 보존한다.</summary>
        /// <param name="cents">USD 센트 금액.</param>
        /// <param name="expected">센트를 생략하지 않는 정확한 USD 표시.</param>
        [TestCase(-123456789L, "-$1,234,567.89")]
        [TestCase(0L, "$0.00")]
        [TestCase(1L, "$0.01")]
        [TestCase(long.MinValue, "-$92,233,720,368,547,758.08")]
        [TestCase(long.MaxValue, "$92,233,720,368,547,758.07")]
        public void FormatExact_Usd_NegativeShowsMinusAndCents(long cents, string expected)
        {
            // Arrange / Act
            string result = MoneyFormatter.FormatExact(Money.FromCents(cents), CurrencyView.Usd);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>원화 중간값이 signed 64비트를 넘어도 모든 원 단위를 표시한다.</summary>
        /// <param name="cents">USD 센트 금액.</param>
        /// <param name="expected">원을 생략하지 않는 정확한 원화 표시.</param>
        [TestCase(0L, "0원")]
        [TestCase(1L, "10원")]
        [TestCase(-1L, "-10원")]
        [TestCase(123456789L, "1,234,567,890원")]
        [TestCase(long.MinValue, "-92,233,720,368,547,758,080원")]
        [TestCase(long.MaxValue, "92,233,720,368,547,758,070원")]
        public void FormatExact_Krw_FullRangePreservesEveryWon(long cents, string expected)
        {
            // Arrange / Act
            string result = MoneyFormatter.FormatExact(Money.FromCents(cents), CurrencyView.Krw);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        /// <summary>문화권 변경이나 원화 보기 선택이 원래 USD 값을 바꾸지 않는다.</summary>
        [Test]
        public void Format_AlternateCulture_PreservesInvariantTextAndValue()
        {
            // Arrange
            Money value = Money.FromCents(123456789);
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

                // Act / Assert
                Assert.That(MoneyFormatter.FormatExact(value, CurrencyView.Usd), Is.EqualTo("$1,234,567.89"));
                Assert.That(MoneyFormatter.FormatShort(value, CurrencyView.Krw), Is.EqualTo("12.3억원"));
                Assert.That(value.Cents, Is.EqualTo(123456789));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        /// <summary>지원하지 않는 통화 보기 값을 달러나 원화로 오인하지 않는다.</summary>
        [Test]
        public void Format_UnknownCurrencyView_Throws()
        {
            // Arrange
            var invalid = (CurrencyView)2;

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => MoneyFormatter.FormatShort(Money.Zero, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => MoneyFormatter.FormatExact(Money.Zero, invalid));
        }
    }
}
