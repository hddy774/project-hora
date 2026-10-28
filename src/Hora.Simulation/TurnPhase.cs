// 월 정산과 사용자 입력을 구분하는 상태를 정의한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
namespace Hora.Simulation
{
    /// <summary>계획 입력, 정산 실행, 결과 열람을 구분하는 월 상태.</summary>
    public enum TurnPhase
    {
        /// <summary>시간이 멈추고 다음 정산을 준비하는 상태.</summary>
        Planning,

        /// <summary>정산 시스템이 세계를 변경하며 외부 입력을 차단하는 상태.</summary>
        Resolving,

        /// <summary>완료한 월의 보고서를 열람하는 상태.</summary>
        Report,
    }
}
