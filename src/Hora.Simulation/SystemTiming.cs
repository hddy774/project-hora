// 시스템별 처리 시간을 결과에만 보관하여 경제 상태와 분리한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System.Diagnostics.CodeAnalysis;

namespace Hora.Simulation
{
    /// <summary>등록한 시스템 한 개의 벽시계 실행 시간. 결정론적 세계 해시에 포함하지 않는다.</summary>
    public readonly struct SystemTiming
    {
        /// <summary>등록 시 고정한 시스템 이름.</summary>
        [SuppressMessage("Design", "CA1051", Justification = "코드 규칙 §5.2의 불변 readonly 값 필드이다.")]
        public readonly string SystemName;

        /// <summary>실행에 걸린 시간(마이크로초, 소수 부분 내림).</summary>
        [SuppressMessage("Design", "CA1051", Justification = "코드 규칙 §5.2의 불변 readonly 값 필드이다.")]
        public readonly long ElapsedMicroseconds;

        /// <summary>성능 관측용 시간 값을 만든다.</summary>
        /// <param name="systemName">측정한 단계의 이름.</param>
        /// <param name="elapsedMicroseconds">측정 시간(단위: 마이크로초).</param>
        public SystemTiming(string systemName, long elapsedMicroseconds)
        {
            SystemName = systemName;
            ElapsedMicroseconds = elapsedMicroseconds;
        }
    }
}
