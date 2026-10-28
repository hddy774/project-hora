// 금액·수량 계산과 센트 잔여 배분의 공통 규칙을 제공한다. [핫 패스]
// 관련 기획: docs/tasks/M0-02-money-quantity.md. 정상 경로에서 관리 힙을 할당하지 않는다.
using System;
using System.Runtime.InteropServices;
using Hora.Domain.Internal;

namespace Hora.Domain.Values
{
    /// <summary>정수 내림과 최대 나머지 방식으로 금액의 보존을 보장한다.</summary>
    public static class MoneyMath
    {
        /// <summary>단가 × 최소 단위 수량 ÷ 가격 기준 단위 수를 음의 무한대 방향으로 내림한다.</summary>
        /// <param name="unitPrice">가격 기준 단위 수에 대한 USD 금액.</param>
        /// <param name="quantity">상품의 최소 단위 수량. 변화량 계산을 위해 음수도 허용한다.</param>
        /// <param name="unitsPerPrice">가격 기준 최소 단위 수. 1 이상이어야 한다.</param>
        /// <returns>센트 단위로 내림한 금액.</returns>
        /// <exception cref="OverflowException">최종 금액이 signed 64비트 센트 범위를 벗어난다.</exception>
        public static Money Multiply(Money unitPrice, Quantity quantity, long unitsPerPrice)
        {
            if (unitsPerPrice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerPrice));
            }

            WideUnsigned product = WideUnsigned.Multiply(
                WideUnsigned.Magnitude(unitPrice.Cents), WideUnsigned.Magnitude(quantity.Units));
            WideUnsigned divisor = WideUnsigned.FromUInt64(checked((ulong)unitsPerPrice));
            bool isNegative = (unitPrice.Cents < 0) != (quantity.Units < 0);
            long cents = DivideFloor(product, divisor, isNegative, out _);
            return Money.FromCents(cents);
        }

        /// <summary>
        /// 총액을 가중치로 나눈다. 나머지가 큰 순서, 동률이면 앞 인덱스 순서로 1센트씩 배분한다.
        /// [성능] 외부 버퍼만 사용해 할당 0B를 유지한다. 잔여 순위 계산 시간은 수령자 수의 제곱이다.
        /// </summary>
        /// <param name="total">보존할 USD 총액. 음수도 허용한다.</param>
        /// <param name="weights">음수가 없는 가중치. 합계는 0보다 커야 한다.</param>
        /// <param name="output">weights와 같은 길이의 별도 출력 버퍼. 모든 슬롯의 합은 total과 같다.</param>
        /// <exception cref="ArgumentException">가중치·버퍼 길이가 잘못되거나 입력과 출력 메모리가 겹친다.</exception>
        public static void Allocate(Money total, ReadOnlySpan<long> weights, Span<Money> output)
        {
            WideUnsigned weightSum = ValidateAllocation(weights, output);
            WideUnsigned remainderSum = default;
            for (int index = 0; index < weights.Length; index++)
            {
                long cents = GetShare(total, weights[index], weightSum, out WideUnsigned remainder);
                output[index] = Money.FromCents(cents);
                remainderSum = remainderSum.Add(remainder);
            }

            // [보존] 음수 총액의 floor 합은 long.MinValue보다 작을 수 있다.
            // 따라서 floor 합 대신 128비트 나머지의 합에서 남은 센트 수를 얻는다.
            int remainingCents = checked((int)remainderSum.Divide(weightSum, out _).ToUInt64Checked());
            if (remainingCents != 0)
            {
                DistributeRemainders(total, weights, weightSum, remainingCents, output);
            }
        }

        /// <summary>출력을 쓰기 전에 모든 입력과 양수 가중치 합을 검사한다.</summary>
        private static WideUnsigned ValidateAllocation(ReadOnlySpan<long> weights, Span<Money> output)
        {
            if (output.Length != weights.Length)
            {
                throw new ArgumentException("출력 버퍼와 수령자 수가 같아야 합니다.", nameof(output));
            }

            if (weights.Overlaps(MemoryMarshal.Cast<Money, long>(output)))
            {
                throw new ArgumentException("가중치와 출력 버퍼는 겹칠 수 없습니다.", nameof(output));
            }

            WideUnsigned sum = default;
            for (int index = 0; index < weights.Length; index++)
            {
                if (weights[index] < 0)
                {
                    throw new ArgumentException("가중치는 음수일 수 없습니다.", nameof(weights));
                }

                sum = sum.Add(WideUnsigned.FromUInt64(checked((ulong)weights[index])));
            }

            if (sum.IsZero)
            {
                throw new ArgumentException("가중치 합은 0보다 커야 합니다.", nameof(weights));
            }

            return sum;
        }

        private static long GetShare(Money total, long weight, WideUnsigned sum, out WideUnsigned remainder)
        {
            WideUnsigned product = WideUnsigned.Multiply(WideUnsigned.Magnitude(total.Cents), checked((ulong)weight));
            return DivideFloor(product, sum, total.IsNegative, out remainder);
        }

        /// <summary>음수 결과의 몫과 나머지를 floor 규약에 맞춰 변환한다.</summary>
        private static long DivideFloor(
            WideUnsigned product, WideUnsigned divisor, bool isNegative, out WideUnsigned remainder)
        {
            WideUnsigned quotient = product.Divide(divisor, out remainder);
            if (isNegative && !remainder.IsZero)
            {
                quotient = quotient.Add(WideUnsigned.FromUInt64(1));
                remainder = divisor.Subtract(remainder);
            }

            return quotient.ToSignedChecked(isNegative);
        }

        /// <summary>정확한 나머지 순위를 재계산해 가변 크기 임시 배열 없이 배분한다.</summary>
        private static void DistributeRemainders(
            Money total, ReadOnlySpan<long> weights, WideUnsigned sum, int remaining, Span<Money> output)
        {
            for (int index = 0; index < weights.Length; index++)
            {
                GetShare(total, weights[index], sum, out WideUnsigned remainder);
                int rank = CountHigherRemainders(total, weights, sum, index, remainder);
                if (rank < remaining)
                {
                    output[index] += Money.FromCents(1);
                }
            }
        }

        /// <summary>동률을 인덱스로 해소하므로 입력 순서가 같은 한 배분 결과도 같다.</summary>
        private static int CountHigherRemainders(
            Money total, ReadOnlySpan<long> weights, WideUnsigned sum, int index, WideUnsigned remainder)
        {
            int rank = 0;
            for (int other = 0; other < weights.Length; other++)
            {
                GetShare(total, weights[other], sum, out WideUnsigned candidate);
                int comparison = candidate.CompareTo(remainder);
                if (comparison > 0 || (comparison == 0 && other < index))
                {
                    rank++;
                }
            }

            return rank;
        }
    }
}
