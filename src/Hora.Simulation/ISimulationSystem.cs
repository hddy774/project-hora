// 월 정산 단계가 공유하는 엔진 독립 실행 계약이다.
// 관련 기획: docs/redesign/02-game-design.md §7.2
namespace Hora.Simulation
{
    /// <summary>
    /// 한 달의 한 처리 단계. 다른 시스템을 직접 호출하지 않고 세계로만 데이터를 공유한다.
    /// Execute에서는 관리 힙 할당을 피하고 같은 상태와 난수에 같은 결과를 만들어야 한다.
    /// </summary>
    public interface ISimulationSystem
    {
        /// <summary>등록 시 고정하여 진행률과 실행 시간 보고에 쓰는 시스템 이름.</summary>
        string Name { get; }

        /// <summary>한 달의 담당 작업만 수행한다. 입력 명령이나 UI에 접근하지 않는다.</summary>
        /// <param name="world">현재 정산 전용 세계. 다른 스레드에서 함께 읽거나 쓰면 안 된다.</param>
        /// <param name="context">이번 달과 시스템별 난수. 호출 이후 참조를 보관하지 않는다.</param>
        void Execute(WorldState world, MonthContext context);
    }
}
