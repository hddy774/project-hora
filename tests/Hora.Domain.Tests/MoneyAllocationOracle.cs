// 구현의 두 워드 산술과 독립적인 BigInteger 배분 기준을 제공한다.
// 관련 기획: docs/tasks/M0-02-money-quantity.md.
using System;
using System.Numerics;

namespace Hora.Domain.Tests
{
    /// <summary>테스트에서만 할당과 정렬을 사용해 최대 나머지의 정확한 답을 계산한다.</summary>
    internal static class MoneyAllocationOracle
    {
        /// <summary>임의 정밀도 산술·정렬로 인덱스별 센트 몫을 구한다.</summary>
        /// <param name="total">USD 센트 총액.</param>
        /// <param name="weights">비음수 가중치.</param>
        /// <returns>정확한 배분 결과.</returns>
        internal static long[] Calculate(long total, long[] weights)
        {
            BigInteger sum = SumWeights(weights);
            long[] output = new long[weights.Length];
            var remainders = new BigInteger[weights.Length];
            int[] indexes = new int[weights.Length];
            BigInteger floorSum = BigInteger.Zero;
            for (int index = 0; index < weights.Length; index++)
            {
                BigInteger share = BigInteger.DivRem((BigInteger)total * weights[index], sum, out remainders[index]);
                if (remainders[index].Sign < 0)
                {
                    share--;
                    remainders[index] += sum;
                }

                output[index] = (long)share;
                floorSum += share;
                indexes[index] = index;
            }

            Array.Sort(indexes, (left, right) => CompareRemainders(remainders, left, right));
            int remaining = (int)(total - floorSum);
            for (int index = 0; index < remaining; index++)
            {
                output[indexes[index]]++;
            }

            return output;
        }

        private static BigInteger SumWeights(long[] weights)
        {
            BigInteger sum = BigInteger.Zero;
            for (int index = 0; index < weights.Length; index++)
            {
                sum += weights[index];
            }

            return sum;
        }

        private static int CompareRemainders(BigInteger[] remainders, int left, int right)
        {
            int comparison = remainders[right].CompareTo(remainders[left]);
            return comparison != 0 ? comparison : left.CompareTo(right);
        }
    }
}
