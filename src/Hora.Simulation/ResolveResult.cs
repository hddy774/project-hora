// 정산 성패와 시스템별 시간에 대한 읽기 전용 결과를 제공한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System.Collections.Generic;

namespace Hora.Simulation
{
    /// <summary>
    /// 파이프라인이 소유하고 재사용하는 결과. 성공 결과와 시간 목록은 다음 허용된 Resolve가
    /// 시작되기 전까지만 유효하다. 보관하려면 호출자가 정산 밖에서 복사해야 한다.
    /// 실패 결과는 시간 목록이 비어 있으며 이전 성공 결과를 변경하지 않는다.
    /// </summary>
    public sealed class ResolveResult
    {
        /// <summary>파이프라인 초기화 시 결과 컨테이너를 한 번 만든다.</summary>
        /// <param name="isSuccess">정산 성공 여부.</param>
        /// <param name="timings">파이프라인 소유의 읽기 전용 측정 목록.</param>
        internal ResolveResult(bool isSuccess, IReadOnlyList<SystemTiming> timings)
        {
            IsSuccess = isSuccess;
            Timings = timings;
        }

        /// <summary>한 달 전체가 정상 완료되었으면 true.</summary>
        public bool IsSuccess { get; }

        /// <summary>등록 순서대로 기록한 처리 시간(마이크로초). 실패 시 비어 있다.</summary>
        public IReadOnlyList<SystemTiming> Timings { get; }
    }
}
