// [핫 패스] 월 번호와 등록 순서별 난수를 할당 없이 다시 초기화한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;
using Hora.Domain.Random;

namespace Hora.Simulation
{
    /// <summary>
    /// 한 파이프라인이 재사용하는 월 컨텍스트. Execute 밖에서 참조나 난수 ref를 보관하지 않는다.
    /// 기존 시스템의 인덱스를 유지하며 끝에 등록해야 기존 난수 스트림이 유지된다.
    /// </summary>
    public sealed class MonthContext
    {
        /// <summary>월별 스트림 구간 크기이자 등록 가능한 최대 시스템 수.</summary>
        internal const int StreamStride = 1000;

        private readonly DeterministicRandom[] _randomStreams;

        /// <summary>월마다 다시 쓰는 난수 배열을 한 번 확보한다.</summary>
        /// <param name="systemCount">등록된 시스템 수(0~1000).</param>
        internal MonthContext(int systemCount)
        {
            _randomStreams = new DeterministicRandom[systemCount];
        }

        /// <summary>이번 실행이 처리하는 계획 월 번호(단위: 개월, 0부터).</summary>
        public int MonthIndex { get; private set; }

        /// <summary>해당 등록 인덱스의 난수 상태를 직접 반환한다. 구조체 복사를 피한다.</summary>
        /// <param name="systemIndex">등록 시 정한 시스템 인덱스(0 이상, 시스템 수 미만).</param>
        /// <returns>이 월 동안만 유효한 가변 난수 참조.</returns>
        /// <exception cref="ArgumentOutOfRangeException">등록하지 않은 인덱스.</exception>
        public ref DeterministicRandom RandomFor(int systemIndex)
        {
            if (systemIndex < 0 || systemIndex >= _randomStreams.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(systemIndex));
            }

            return ref _randomStreams[systemIndex];
        }

        /// <summary>정산 전 원래 시드와 월 번호로 각 독립 스트림을 재설정한다.</summary>
        /// <param name="seed">세계 개시 시드.</param>
        /// <param name="monthIndex">처리할 월 번호(0 이상, 단위: 개월).</param>
        internal void Reset(ulong seed, int monthIndex)
        {
            if (monthIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(monthIndex));
            }

            MonthIndex = monthIndex;
            // [결정론] int 곱셈 오버플로를 피하고 월별로 겹치지 않는 1000개 구간을 쓴다.
            ulong firstStream = checked((ulong)monthIndex * StreamStride);
            for (int index = 0; index < _randomStreams.Length; index++)
            {
                _randomStreams[index] = new DeterministicRandom(seed, firstStream + (ulong)index);
            }
        }
    }
}
