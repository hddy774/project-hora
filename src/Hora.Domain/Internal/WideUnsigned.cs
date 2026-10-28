// .NET Standard 2.1에서 금액의 곱·나머지를 할당 없이 계산하는 두 워드 정수다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md. 일반적인 공개 숫자 타입이 아니다.
using System;

namespace Hora.Domain.Internal
{
    /// <summary>64비트 금액의 중간 계산에만 사용하는 부호 없는 128비트 값.</summary>
    internal readonly struct WideUnsigned
    {
        private const int WordBits = 64;
        private const int HalfWordBits = 32;
        private const ulong HalfWordMask = uint.MaxValue;
        private readonly ulong _high;
        private readonly ulong _low;

        private WideUnsigned(ulong high, ulong low)
        {
            _high = high;
            _low = low;
        }

        /// <summary>두 워드가 모두 0인지 나타낸다.</summary>
        internal bool IsZero => _high == 0 && _low == 0;

        /// <summary>64비트 값을 손실 없이 확장한다.</summary>
        /// <param name="value">확장할 비음수 정수.</param>
        /// <returns>같은 정수의 128비트 표현.</returns>
        internal static WideUnsigned FromUInt64(ulong value) => new WideUnsigned(0, value);

        /// <summary>최소 signed 정수도 표현할 수 있는 절댓값을 계산한다.</summary>
        /// <param name="value">부호 있는 정수.</param>
        /// <returns>0부터 2의 63제곱까지의 크기.</returns>
        internal static ulong Magnitude(long value)
        {
            return value < 0 ? checked((ulong)(-(value + 1)) + 1) : checked((ulong)value);
        }

        /// <summary>64비트 두 수의 정확한 곱을 만든다.</summary>
        /// <param name="left">첫 번째 비음수 정수.</param>
        /// <param name="right">두 번째 비음수 정수.</param>
        /// <returns>최대 128비트의 곱.</returns>
        internal static WideUnsigned Multiply(ulong left, ulong right)
        {
            // [보존] 32비트 부분 곱 네 개와 올림을 합쳐 중간값의 유실을 막는다.
            ulong lowProduct = checked((left & HalfWordMask) * (right & HalfWordMask));
            ulong crossLeft = checked((left >> HalfWordBits) * (right & HalfWordMask));
            ulong crossRight = checked((left & HalfWordMask) * (right >> HalfWordBits));
            ulong middle = checked((lowProduct >> HalfWordBits) + (crossLeft & HalfWordMask)
                + (crossRight & HalfWordMask));
            ulong high = checked((left >> HalfWordBits) * (right >> HalfWordBits)
                + (crossLeft >> HalfWordBits) + (crossRight >> HalfWordBits) + (middle >> HalfWordBits));
            ulong low = (middle << HalfWordBits) | (lowProduct & HalfWordMask);
            return new WideUnsigned(high, low);
        }

        /// <summary>128비트 두 수를 더하고 최종 범위 초과를 검사한다.</summary>
        /// <param name="other">더할 비음수 정수.</param>
        /// <returns>정확한 합.</returns>
        internal WideUnsigned Add(WideUnsigned other)
        {
            // 하위 워드의 순환은 버림이 아닌 상위 워드로의 올림으로 보존한다.
            ulong low = unchecked(_low + other._low);
            ulong carry = low < _low ? 1UL : 0UL;
            return new WideUnsigned(checked(_high + other._high + carry), low);
        }

        /// <summary>작거나 같은 값을 빼며 음수 결과를 허용하지 않는다.</summary>
        /// <param name="other">뺄 비음수 정수.</param>
        /// <returns>정확한 차.</returns>
        internal WideUnsigned Subtract(WideUnsigned other)
        {
            if (CompareTo(other) < 0)
            {
                throw new OverflowException("음수는 부호 없는 중간값으로 표현할 수 없습니다.");
            }

            ulong borrow = _low < other._low ? 1UL : 0UL;
            return new WideUnsigned(checked(_high - other._high - borrow), unchecked(_low - other._low));
        }

        /// <summary>두 워드 전체의 크기를 비교한다.</summary>
        /// <param name="other">비교할 값.</param>
        /// <returns>작으면 음수, 같으면 0, 크면 양수.</returns>
        internal int CompareTo(WideUnsigned other)
        {
            int highComparison = _high.CompareTo(other._high);
            return highComparison != 0 ? highComparison : _low.CompareTo(other._low);
        }

        /// <summary>나눗셈의 정수 몫과 나머지를 정확하게 구한다.</summary>
        /// <param name="divisor">0보다 큰 나누는 수.</param>
        /// <param name="remainder">0 이상 divisor 미만인 나머지.</param>
        /// <returns>비음수 정수 몫.</returns>
        internal WideUnsigned Divide(WideUnsigned divisor, out WideUnsigned remainder)
        {
            if (divisor.IsZero)
            {
                throw new DivideByZeroException();
            }

            if (_high == 0 && divisor._high == 0)
            {
                remainder = FromUInt64(_low % divisor._low);
                return FromUInt64(_low / divisor._low);
            }

            remainder = this;
            WideUnsigned quotient = default;
            int shift = GetBitLength() - divisor.GetBitLength();
            if (shift < 0)
            {
                return quotient;
            }

            WideUnsigned shiftedDivisor = divisor.ShiftLeft(shift);
            for (int bit = shift; bit >= 0; bit--)
            {
                if (remainder.CompareTo(shiftedDivisor) >= 0)
                {
                    remainder = remainder.Subtract(shiftedDivisor);
                    quotient = quotient.WithBit(bit);
                }

                shiftedDivisor = shiftedDivisor.ShiftRightOne();
            }

            return quotient;
        }

        /// <summary>64비트로 좁히며 상위 값이 사라지는 경우 실패한다.</summary>
        /// <returns>64비트 비음수 정수.</returns>
        internal ulong ToUInt64Checked()
        {
            if (_high != 0)
            {
                throw new OverflowException();
            }

            return _low;
        }

        /// <summary>지정 부호의 signed 정수로 바꾸며 최종 금액 범위를 검사한다.</summary>
        /// <param name="isNegative">음수 부호를 붙일지 여부.</param>
        /// <returns>부호 있는 64비트 정수.</returns>
        internal long ToSignedChecked(bool isNegative)
        {
            ulong magnitude = ToUInt64Checked();
            if (isNegative && magnitude == (1UL << (WordBits - 1)))
            {
                return long.MinValue;
            }

            long value = checked((long)magnitude);
            return isNegative ? checked(-value) : value;
        }

        /// <summary>최상위 유효 비트까지의 길이로 나누는 수의 첫 위치를 정한다.</summary>
        private int GetBitLength()
        {
            int bits = _high == 0 ? 0 : WordBits;
            ulong remaining = _high == 0 ? _low : _high;
            while (remaining != 0)
            {
                bits++;
                remaining >>= 1;
            }

            return bits;
        }

        /// <summary>비트 길이로 확인한 범위 안에서만 나누는 수를 왼쪽으로 정렬한다.</summary>
        private WideUnsigned ShiftLeft(int bits)
        {
            if (bits == 0)
            {
                return this;
            }

            if (bits >= WordBits)
            {
                return new WideUnsigned(_low << (bits - WordBits), 0);
            }

            return new WideUnsigned((_high << bits) | (_low >> (WordBits - bits)), _low << bits);
        }

        private WideUnsigned ShiftRightOne() => new WideUnsigned(_high >> 1, (_low >> 1) | (_high << 63));

        private WideUnsigned WithBit(int bit)
        {
            return bit < WordBits
                ? new WideUnsigned(_high, _low | (1UL << bit))
                : new WideUnsigned(_high | (1UL << (bit - WordBits)), _low);
        }
    }
}
