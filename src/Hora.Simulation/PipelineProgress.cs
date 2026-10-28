// 실제 완료한 시스템 개수로 진행바를 표시하는 값을 전달한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md, R24
using System.Diagnostics.CodeAnalysis;

namespace Hora.Simulation
{
    /// <summary>완료 단계 수를 담는 불변 진행률 값. 시간 기반 예상 퍼센트가 아니다.</summary>
    public readonly struct PipelineProgress
    {
        /// <summary>작업을 끝낸 시스템 수.</summary>
        [SuppressMessage("Design", "CA1051", Justification = "M0-04의 불변 readonly 값 필드 계약이다.")]
        public readonly int Completed;

        /// <summary>실행하도록 등록한 전체 시스템 수.</summary>
        [SuppressMessage("Design", "CA1051", Justification = "M0-04의 불변 readonly 값 필드 계약이다.")]
        public readonly int Total;

        /// <summary>방금 완료한 시스템의 등록 시 이름.</summary>
        [SuppressMessage("Design", "CA1051", Justification = "M0-04의 불변 readonly 값 필드 계약이다.")]
        public readonly string SystemName;

        /// <summary>한 단계의 실제 완료 상태를 만든다.</summary>
        /// <param name="completed">완료 수(단위: 시스템).</param>
        /// <param name="total">전체 수(단위: 시스템).</param>
        /// <param name="systemName">방금 끝난 단계 이름.</param>
        public PipelineProgress(int completed, int total, string systemName)
        {
            Completed = completed;
            Total = total;
            SystemName = systemName;
        }
    }
}
