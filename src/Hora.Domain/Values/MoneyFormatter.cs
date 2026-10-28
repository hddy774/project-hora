// USD의 정확한 금액·M/B 표기와 고정 환율 원화 보기를 제공한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md, R25~R26.
using System;
using System.Globalization;
using Hora.Domain.Internal;

namespace Hora.Domain.Values
{
    /// <summary>금액을 정수만으로 표시한다. 표시 변경은 USD 장부를 수정하지 않는다.</summary>
    public static class MoneyFormatter
    {
        private const ulong CentsPerDollar = 100;
        private const ulong CentsPerMillionDollars = 100_000_000;
        private const ulong CentsPerBillionDollars = 100_000_000_000;
        private const ulong CentsPerHundredMillionWon = 10_000_000;
        private const ulong CentsPerTrillionWon = 100_000_000_000;
        private const ulong WonPerCent = 10;
        private const int GroupSize = 3;
        private const int GroupedBufferLength = 32;

        /// <summary>표시용 고정 환율. USD 1달러는 1,000원이다.</summary>
        public const long KrwPerUsd = 1000;

        /// <summary>USD는 M/B, 원화는 억/조 단위로 소수 한 자리까지 내림해 표시한다.</summary>
        /// <param name="value">USD 센트 금액.</param>
        /// <param name="view">달러 또는 고정 환율 원화 보기.</param>
        /// <returns>예: $1M, 10억원, 1조원. 소수부가 0이면 생략한다.</returns>
        public static string FormatShort(Money value, CurrencyView view)
        {
            ulong magnitude = WideUnsigned.Magnitude(value.Cents);
            string sign = value.IsNegative ? "-" : string.Empty;
            switch (view)
            {
                case CurrencyView.Usd:
                    return FormatShortUsd(value, magnitude, sign);
                case CurrencyView.Krw:
                    return FormatShortKrw(value, magnitude, sign);
                default:
                    throw new ArgumentOutOfRangeException(nameof(view));
            }
        }

        /// <summary>센트 단위 금액을 문화권에 관계없이 천 단위 쉼표와 함께 정확하게 표시한다.</summary>
        /// <param name="value">USD 센트 금액.</param>
        /// <param name="view">달러 또는 고정 환율 원화 보기.</param>
        /// <returns>예: $1,234.56 또는 1,234,560원. USD 소수 두 자리는 항상 표시한다.</returns>
        public static string FormatExact(Money value, CurrencyView view)
        {
            ulong magnitude = WideUnsigned.Magnitude(value.Cents);
            string sign = value.IsNegative ? "-" : string.Empty;
            switch (view)
            {
                case CurrencyView.Usd:
                    string dollars = (magnitude / CentsPerDollar).ToString("N0", CultureInfo.InvariantCulture);
                    string cents = (magnitude % CentsPerDollar).ToString("D2", CultureInfo.InvariantCulture);
                    return sign + "$" + dollars + "." + cents;
                case CurrencyView.Krw:
                    // 원화는 long 센트 범위보다 커질 수 있어 표시 중간값도 128비트를 사용한다.
                    return sign + FormatGrouped(WideUnsigned.Multiply(magnitude, WonPerCent)) + "원";
                default:
                    throw new ArgumentOutOfRangeException(nameof(view));
            }
        }

        /// <summary>센트 임계값으로 축약 단위를 골라 반올림 때문에 M/B 경계가 바뀌지 않게 한다.</summary>
        private static string FormatShortUsd(Money value, ulong magnitude, string sign)
        {
            if (magnitude < CentsPerMillionDollars)
            {
                return FormatExact(value, CurrencyView.Usd);
            }

            return magnitude < CentsPerBillionDollars
                ? sign + "$" + FormatScaled(magnitude, CentsPerMillionDollars) + "M"
                : sign + "$" + FormatScaled(magnitude, CentsPerBillionDollars) + "B";
        }

        /// <summary>원화 환산액을 크게 곱하기 전에 센트 기준 억/조 임계값을 적용한다.</summary>
        private static string FormatShortKrw(Money value, ulong magnitude, string sign)
        {
            if (magnitude < CentsPerHundredMillionWon)
            {
                return FormatExact(value, CurrencyView.Krw);
            }

            return magnitude < CentsPerTrillionWon
                ? sign + FormatScaled(magnitude, CentsPerHundredMillionWon) + "억원"
                : sign + FormatScaled(magnitude, CentsPerTrillionWon) + "조원";
        }

        private static string FormatScaled(ulong cents, ulong centsPerScale)
        {
            string whole = (cents / centsPerScale).ToString(CultureInfo.InvariantCulture);
            ulong tenths = (cents % centsPerScale) / (centsPerScale / 10);
            return tenths == 0 ? whole : whole + "." + tenths.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>64비트를 넘는 원화 정수도 문자열에서 반올림하거나 자릿수를 버리지 않는다.</summary>
        private static string FormatGrouped(WideUnsigned magnitude)
        {
            Span<char> buffer = stackalloc char[GroupedBufferLength];
            int position = buffer.Length;
            int digits = 0;
            do
            {
                if (digits > 0 && digits % GroupSize == 0)
                {
                    buffer[--position] = ',';
                }

                magnitude = magnitude.Divide(WideUnsigned.FromUInt64(10), out WideUnsigned remainder);
                buffer[--position] = checked((char)('0' + remainder.ToUInt64Checked()));
                digits++;
            }
            while (!magnitude.IsZero);

            return new string(buffer.Slice(position));
        }
    }
}
