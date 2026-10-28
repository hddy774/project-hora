// 결정론적 난수 생성기 PCG32(XSH-RR 64/32). 같은 시드와 같은 호출 순서면 어느 기기에서도 같은 수열을 만든다.
using System;
using System.Collections.Generic;

namespace Xianxia.Simulation.Core
{
    /// <summary>
    /// 세계 시뮬레이션의 유일한 난수원. 상태(<see cref="State"/>, <see cref="Increment"/>)를 저장하면
    /// 불러온 뒤에도 같은 수열을 이어서 만든다.
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const double UnitScale = 1.0 / 4294967296.0;

        /// <summary>시드와 스트림 번호로 생성기를 만든다.</summary>
        /// <param name="seed">시드.</param>
        /// <param name="sequence">스트림 번호(같은 시드라도 다른 수열).</param>
        public Pcg32(ulong seed, ulong sequence)
        {
            Increment = unchecked((sequence << 1) | 1UL);
            State = 0;
            NextUInt();
            State = unchecked(State + seed);
            NextUInt();
        }

        /// <summary>저장된 상태로 생성기를 복원한다.</summary>
        /// <param name="state">내부 상태.</param>
        /// <param name="increment">스트림 증분(홀수).</param>
        /// <returns>복원된 생성기.</returns>
        public static Pcg32 FromState(ulong state, ulong increment)
        {
            Pcg32 random = new(0, 0) { State = state, Increment = increment | 1UL };
            return random;
        }

        /// <summary>내부 64비트 상태. 저장용.</summary>
        public ulong State { get; private set; }

        /// <summary>스트림 증분. 저장용.</summary>
        public ulong Increment { get; private set; }

        /// <summary>균등한 32비트 정수를 만든다.</summary>
        /// <returns>0 이상 2^32 미만의 정수.</returns>
        public uint NextUInt()
        {
            ulong old = State;
            State = unchecked((old * Multiplier) + Increment);
            uint xorShifted = unchecked((uint)(((old >> 18) ^ old) >> 27));
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <summary>0 이상 <paramref name="maxExclusive"/> 미만의 정수를 치우침 없이 만든다.</summary>
        /// <param name="maxExclusive">상한(포함하지 않음). 1 이상.</param>
        /// <returns>균등 정수.</returns>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "상한은 1 이상이어야 한다.");
            }

            // [결정론] 거부 표본추출로 나머지 치우침을 없앤다. 호출 횟수는 시드에 따라 달라도 재현된다.
            uint bound = (uint)maxExclusive;
            uint threshold = unchecked((uint)(-(int)bound)) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }

        /// <summary>양 끝을 포함하는 정수 구간에서 균등하게 고른다.</summary>
        /// <param name="minInclusive">하한.</param>
        /// <param name="maxInclusive">상한.</param>
        /// <returns>균등 정수.</returns>
        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + NextInt(maxInclusive - minInclusive + 1);
        }

        /// <summary>0 이상 1 미만의 실수를 만든다.</summary>
        /// <returns>균등 실수.</returns>
        public double NextDouble()
        {
            return NextUInt() * UnitScale;
        }

        /// <summary>실수 구간에서 균등하게 고른다.</summary>
        /// <param name="min">하한.</param>
        /// <param name="max">상한.</param>
        /// <returns>min 이상 max 미만의 실수.</returns>
        public double Range(double min, double max)
        {
            return min + ((max - min) * NextDouble());
        }

        /// <summary>확률 <paramref name="probability"/>로 참을 돌려준다.</summary>
        /// <param name="probability">0~1 확률.</param>
        /// <returns>성공 여부.</returns>
        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }

        /// <summary>목록에서 하나를 균등하게 고른다.</summary>
        /// <typeparam name="T">원소 형식.</typeparam>
        /// <param name="items">비어 있지 않은 목록.</param>
        /// <returns>고른 원소.</returns>
        public T Pick<T>(IReadOnlyList<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            return items[NextInt(items.Count)];
        }

        /// <summary>가중치 배열에서 하나의 인덱스를 고른다.</summary>
        /// <param name="weights">음수가 아닌 가중치. 합이 0이면 0을 돌려준다.</param>
        /// <returns>고른 인덱스.</returns>
        public int PickWeighted(ReadOnlySpan<double> weights)
        {
            double total = 0;
            for (int index = 0; index < weights.Length; index++)
            {
                total += weights[index];
            }

            if (total <= 0)
            {
                return 0;
            }

            double roll = NextDouble() * total;
            for (int index = 0; index < weights.Length; index++)
            {
                roll -= weights[index];
                if (roll < 0)
                {
                    return index;
                }
            }

            return weights.Length - 1;
        }
    }
}
