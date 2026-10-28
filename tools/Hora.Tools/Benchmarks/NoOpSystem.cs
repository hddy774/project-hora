// 실제 경제 로직 없이 파이프라인 자체의 비용만 재기 위한 빈 시스템이다.
// 관련 명세: docs/tasks/M0-04-month-pipeline.md
using Hora.Simulation;

namespace Hora.Tools.Benchmarks
{
    /// <summary>월 처리의 호출·단계 전환 비용을 분리해 측정하는 시스템.</summary>
    internal sealed class NoOpSystem : ISimulationSystem
    {
        /// <summary>경제 결과를 생성하지 않는 측정 단계의 이름.</summary>
        public string Name => "Empty";

        /// <summary>세계와 컨텍스트를 수정하지 않고 호출 비용만 남긴다.</summary>
        /// <param name="world">측정용 최소 세계.</param>
        /// <param name="context">파이프라인이 재사용하는 월 컨텍스트.</param>
        public void Execute(WorldState world, MonthContext context)
        {
        }
    }
}
