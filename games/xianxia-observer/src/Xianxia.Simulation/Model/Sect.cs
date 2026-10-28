// 종문 상태: 금고·시설·직위 인원·전적. 구성원은 수선자의 SectId로 정하고 여기에는 집계만 둔다.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>종문 하나.</summary>
    public sealed class Sect
    {
        /// <summary>종문 번호(1 청운종, 2 천마문).</summary>
        public int Id { get; set; }

        /// <summary>이름.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>정도(true) 또는 마도(false).</summary>
        public bool Orthodox { get; set; }

        /// <summary>금고(영석). 녹봉·시설·전쟁 배상에 쓴다.</summary>
        public long Treasury { get; set; }

        /// <summary>시설 단계. 인덱스는 <see cref="FacilityKind"/>.</summary>
        public List<int> Facilities { get; set; } = new() { 0, 0, 0, 0, 0 };

        /// <summary>직위별 인원. 인덱스는 <see cref="SectRank"/>. 직위 변경은 SectSystem만 한다.</summary>
        public List<int> RankCounts { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0 };

        /// <summary>현재 종주 영혼 번호(0이면 공석).</summary>
        public int LeaderId { get; set; }

        /// <summary>누적 공헌.</summary>
        public double TotalContribution { get; set; }

        /// <summary>연단방에 쌓인 단약(종문 공용). 인덱스는 <see cref="PillKind"/>.</summary>
        public List<int> PillStock { get; set; } = new() { 0, 0, 0, 0 };

        /// <summary>전쟁 승리 수.</summary>
        public int WarsWon { get; set; }

        /// <summary>전쟁 패배 수.</summary>
        public int WarsLost { get; set; }

        /// <summary>연속 전쟁 패배 수(와신상담 보정).</summary>
        public int LosingStreak { get; set; }

        /// <summary>대비무 우승자 배출 수.</summary>
        public int TournamentWins { get; set; }

        /// <summary>비승자 배출 수.</summary>
        public int Ascensions { get; set; }

        /// <summary>역대 종주 수.</summary>
        public int LeaderCount { get; set; }

        /// <summary>이념 이름.</summary>
        [JsonIgnore]
        public string IdeologyName => Orthodox ? "정도" : "마도";

        /// <summary>이념 설명(원본 IDEO.dc).</summary>
        [JsonIgnore]
        public string IdeologyDescription => Orthodox ? "기초 중시 · 고경지 특화" : "빠른 상승 · 고경지 약화";

        /// <summary>시설 단계.</summary>
        /// <param name="facility">시설.</param>
        /// <returns>단계(0~5).</returns>
        public int Level(FacilityKind facility)
        {
            return Facilities[(int)facility];
        }
    }
}
