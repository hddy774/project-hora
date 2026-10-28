// 전쟁 진행 상태와 끝난 전쟁의 기록.
using System.Collections.Generic;

namespace Xianxia.Simulation.Model
{
    /// <summary>현재 전쟁(또는 휴전) 상태.</summary>
    public sealed class WarState
    {
        /// <summary>전쟁 중이면 true.</summary>
        public bool AtWar { get; set; }

        /// <summary>지금까지 시작된 전쟁 수(진행 중 포함).</summary>
        public int WarNumber { get; set; }

        /// <summary>이번 전쟁 시작 연도.</summary>
        public int StartYear { get; set; }

        /// <summary>이번 전쟁 청운종 승리 전투 수.</summary>
        public int OrthodoxWins { get; set; }

        /// <summary>이번 전쟁 천마문 승리 전투 수.</summary>
        public int DemonicWins { get; set; }

        /// <summary>이번 전쟁에서 일어난 경지 하락 수.</summary>
        public int Casualties { get; set; }

        /// <summary>마지막 전쟁이 끝난 해(대비무 일정 기준).</summary>
        public int LastWarEndYear { get; set; }

        /// <summary>이번 전쟁의 주요 장면(최대 12줄).</summary>
        public List<string> Highlights { get; set; } = new();
    }

    /// <summary>끝난 전쟁 기록.</summary>
    public sealed class WarRecord
    {
        /// <summary>몇 번째 전쟁.</summary>
        public int Number { get; set; }

        /// <summary>시작 연도.</summary>
        public int StartYear { get; set; }

        /// <summary>끝난 연도.</summary>
        public int EndYear { get; set; }

        /// <summary>승리 종문 번호(0이면 무승부).</summary>
        public int VictorSectId { get; set; }

        /// <summary>청운종 전투 승리 수.</summary>
        public int OrthodoxWins { get; set; }

        /// <summary>천마문 전투 승리 수.</summary>
        public int DemonicWins { get; set; }

        /// <summary>종전 시 청운종 지배 접경지 수.</summary>
        public int OrthodoxRegions { get; set; }

        /// <summary>종전 시 천마문 지배 접경지 수.</summary>
        public int DemonicRegions { get; set; }

        /// <summary>경지 하락 피해.</summary>
        public int Casualties { get; set; }

        /// <summary>배상 영석.</summary>
        public long Tribute { get; set; }

        /// <summary>공훈 1위(전쟁 영웅) 이름.</summary>
        public string HeroName { get; set; } = "없음";

        /// <summary>공훈 1위 영혼 번호.</summary>
        public int HeroId { get; set; }

        /// <summary>대표 결투 요약.</summary>
        public string ChampionDuel { get; set; } = "없음";

        /// <summary>주요 장면(최대 12줄).</summary>
        public List<string> Highlights { get; set; } = new();
    }
}
