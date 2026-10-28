// 파이프라인 계약 테스트에서 실행 단계의 효과를 주입한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;

namespace Hora.Simulation.Tests
{
    /// <summary>측정 경계 밖에서 만든 콜백을 실행하는 테스트 전용 단계.</summary>
    internal sealed class PipelineTestSystem : ISimulationSystem
    {
        private readonly Action<WorldState, MonthContext>? _execute;

        /// <summary>빈 단계 또는 관찰 콜백이 있는 단계를 만든다.</summary>
        /// <param name="name">등록 이름.</param>
        /// <param name="execute">실행 시 호출할 테스트 동작. null이면 빈 단계.</param>
        internal PipelineTestSystem(string name = "test", Action<WorldState, MonthContext>? execute = null)
        {
            Name = name;
            _execute = execute;
        }

        /// <summary>진행률과 측정 결과에서 확인할 이름.</summary>
        public string Name { get; }

        /// <summary>선택한 테스트 동작만 실행한다.</summary>
        /// <param name="world">정산 중인 상태.</param>
        /// <param name="context">현재 월의 컨텍스트.</param>
        public void Execute(WorldState world, MonthContext context)
        {
            _execute?.Invoke(world, context);
        }
    }
}
