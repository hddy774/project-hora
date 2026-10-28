// PCG32 XSH RR로 플랫폼과 실행 순서에 독립적인 정수 난수 스트림을 제공한다.
// 관련 기획: docs/tasks/M0-03-ids-random.md. [핫 패스] 호출 중 관리 힙 할당 없음.
using System;

namespace Hora.Domain.Random
{
    /// <summary>
    /// PCG32 XSH RR 난수 상태. 생성자로 초기화한 뒤 사용하며 복사하면 진행 상태도 복제된다.
    /// <para>상태 공유가 필요하면 값 복사 대신 ref로 전달한다. 여러 스레드에서 공유하지 않는다.</para>
    /// <para>참조 알고리즘: https://www.pcg-random.org/using-pcg-c-basic.html.</para>
    /// </summary>
    public struct DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private readonly ulong _seed;
        private readonly ulong _increment;
        private ulong _state;

        /// <summary>초기 시드와 PCG 스트림 번호로 독립 수열을 초기화한다.</summary>
        /// <param name="seed">초기 64비트 시드.</param>
        /// <param name="streamId">스트림 번호. PCG32 규칙에 따라 하위 63비트가 사용된다.</param>
        public DeterministicRandom(ulong seed, ulong streamId)
        {
            _seed = seed;
            _increment = unchecked((streamId << 1) | 1UL);

            // [결정론] PCG 참조의 상태 0 전진 → seed 추가 → 전진을 그대로 수행한다.
            // 모듈로 2^64 연산은 PCG 정의의 일부이므로 의도적으로 unchecked를 사용한다.
            _state = unchecked((_increment + seed) * Multiplier + _increment);
        }

        /// <summary>수열을 한 번 전진시키고 32비트 부호 없는 난수를 반환한다.</summary>
        /// <returns>0부터 uint.MaxValue까지의 난수.</returns>
        /// <exception cref="InvalidOperationException">생성자로 초기화하지 않은 기본 구조체 사용.</exception>
        public uint NextUInt()
        {
            if (_increment == 0)
            {
                throw new InvalidOperationException("난수기는 시드와 스트림 생성자로 초기화해야 합니다.");
            }

            ulong previous = _state;
            _state = unchecked(previous * Multiplier + _increment);
            uint mixed = unchecked((uint)(((previous >> 18) ^ previous) >> 27));
            int rotation = (int)(previous >> 59);
            return (mixed >> rotation) | (mixed << ((-rotation) & 31));
        }

        /// <summary>하한 이상·상한 미만의 편향 없는 정수 난수를 반환한다.</summary>
        /// <param name="minInclusive">포함하는 하한.</param>
        /// <param name="maxExclusive">포함하지 않는 상한. 하한보다 커야 한다.</param>
        /// <returns>지정한 반열린 구간 안의 정수.</returns>
        /// <exception cref="ArgumentException">상한이 하한보다 크지 않음.</exception>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentException("상한은 하한보다 커야 합니다.", nameof(maxExclusive));
            }

            // signed int 전체 범위에 가까운 구간도 넘치지 않도록 차이를 long으로 계산한다.
            uint width = checked((uint)((long)maxExclusive - minInclusive));
            uint offset = NextBounded(width);
            return checked((int)(minInclusive + (long)offset));
        }

        /// <summary>정수 분수 확률을 평가한다. 확률 0과 1은 난수 상태를 소비하지 않는다.</summary>
        /// <param name="numerator">성공 비율의 분자(0 이상, 분모 이하).</param>
        /// <param name="denominator">성공 비율의 분모(1 이상).</param>
        /// <returns>해당 확률에 따라 결정된 성공 여부.</returns>
        /// <exception cref="ArgumentOutOfRangeException">양수가 아닌 분모 또는 범위 밖 분자.</exception>
        public bool NextChance(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(denominator));
            }

            if (numerator < 0 || numerator > denominator)
            {
                throw new ArgumentOutOfRangeException(nameof(numerator));
            }

            if (numerator == 0 || numerator == denominator)
            {
                return numerator != 0;
            }

            return NextBounded((uint)denominator) < (uint)numerator;
        }

        /// <summary>원래 생성 시드로 새 스트림을 만든다. 부모의 현재 상태와 진행 횟수에 영향받지 않는다.</summary>
        /// <param name="streamId">새 스트림 번호. PCG32에서 하위 63비트 사용.</param>
        /// <returns>원래 시드와 지정 스트림으로 초기화한 난수기 값.</returns>
        public readonly DeterministicRandom Fork(ulong streamId) => new DeterministicRandom(_seed, streamId);

        /// <summary>표본 공간 크기가 구간 너비의 배수가 되도록 나머지 영역을 거부한다.</summary>
        private uint NextBounded(uint width)
        {
            // PCG 참조 boundedrand: 2^32 % width 미만을 버려 나머지 연산의 편향을 제거한다.
            uint threshold = unchecked(0U - width) % width;
            uint sample;
            do
            {
                sample = NextUInt();
            }
            while (sample < threshold);

            return sample % width;
        }
    }
}
