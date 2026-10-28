// 동기 진행률 통지를 테스트하여 SynchronizationContext 영향을 제거한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;

namespace Hora.Simulation.Tests
{
    /// <summary>보고 순간의 단계와 재진입을 관찰하는 동기식 테스트 진행률 수신기.</summary>
    internal sealed class PipelineTestProgress : IProgress<PipelineProgress>
    {
        private readonly Action<PipelineProgress> _report;

        /// <summary>통지마다 실행할 테스트 콜백을 저장한다.</summary>
        /// <param name="report">값을 동기적으로 검사하는 동작.</param>
        internal PipelineTestProgress(Action<PipelineProgress> report)
        {
            _report = report;
        }

        /// <summary>즉시 호출하여 완료한 단계의 관측 순서를 유지한다.</summary>
        /// <param name="value">실제 완료한 시스템 수.</param>
        public void Report(PipelineProgress value)
        {
            _report(value);
        }
    }
}
