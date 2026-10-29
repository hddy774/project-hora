// 수선자의 인연·현생 전적·호칭·생애 기록·전생 요약.
using System.Collections.Generic;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>수선자의 관계와 생애 기록 부분.</summary>
    public sealed partial class Cultivator
    {
        /// <summary>스승의 영혼 번호(0이면 없음).</summary>
        public int MasterId { get; set; }

        /// <summary>기명 제자 영혼 번호(최대 3).</summary>
        public List<int> DiscipleIds { get; set; } = new();

        /// <summary>숙적 영혼 번호(최대 3).</summary>
        public List<int> RivalIds { get; set; } = new();

        /// <summary>도려(도반)의 영혼 번호(0이면 없음).</summary>
        public int CompanionId { get; set; }

        /// <summary>도려를 맺은 해.</summary>
        public int CompanionSinceYear { get; set; }

        /// <summary>현생 전투 수.</summary>
        public int Battles { get; set; }

        /// <summary>현생 승리 수.</summary>
        public int Wins { get; set; }

        /// <summary>현생 패배 수.</summary>
        public int Losses { get; set; }

        /// <summary>현재 연승.</summary>
        public int WinStreak { get; set; }

        /// <summary>현생 대경지 돌파 수.</summary>
        public int MajorBreakthroughs { get; set; }

        /// <summary>현생 소경지 돌파 수.</summary>
        public int MinorBreakthroughs { get; set; }

        /// <summary>현생 천겁 통과 수.</summary>
        public int TribulationsPassed { get; set; }

        /// <summary>현생 기연 수.</summary>
        public int Fortunes { get; set; }

        /// <summary>이번 전쟁 공훈.</summary>
        public double WarMerit { get; set; }

        /// <summary>현생 누적 전쟁 공훈.</summary>
        public double LifetimeWarMerit { get; set; }

        /// <summary>현생 대비무 최고 성적(0 없음, 1 우승, 2 준우승, 4 4강, 8 8강).</summary>
        public int TournamentBest { get; set; }

        /// <summary>현생 최고 경지.</summary>
        public int HighestRealm { get; set; }

        /// <summary>현생 최고 단계(최고 경지 기준).</summary>
        public int HighestStage { get; set; }

        /// <summary>현생 호칭.</summary>
        public List<TitleId> Titles { get; set; } = new();

        /// <summary>현생 주요 사건(최대 40, 오래된 것부터 지운다).</summary>
        public List<LifeEvent> Biography { get; set; } = new();

        /// <summary>최근 전생 요약(최대 20).</summary>
        public List<PastLife> PastLives { get; set; } = new();

        /// <summary>호칭 보유 여부.</summary>
        /// <param name="title">호칭.</param>
        /// <returns>보유하면 true.</returns>
        public bool HasTitle(TitleId title)
        {
            return Titles.Contains(title);
        }
    }

    /// <summary>생애의 한 사건.</summary>
    /// <param name="Year">세계 연도.</param>
    /// <param name="Age">당시 나이.</param>
    /// <param name="Text">내용.</param>
    public sealed record LifeEvent(int Year, int Age, string Text);

    /// <summary>끝난 한 생의 요약.</summary>
    /// <param name="LifeNumber">몇 번째 생.</param>
    /// <param name="BornYear">태어난 해.</param>
    /// <param name="DeathYear">끝난 해.</param>
    /// <param name="Age">끝난 나이.</param>
    /// <param name="Realm">최고 경지.</param>
    /// <param name="Stage">최고 단계.</param>
    /// <param name="SectId">마지막 소속 종문.</param>
    /// <param name="Ending">끝(사망 원인 또는 비승).</param>
    /// <param name="Score">생애 업적 점수.</param>
    /// <param name="Titles">얻은 호칭.</param>
    public sealed record PastLife(
        int LifeNumber,
        int BornYear,
        int DeathYear,
        int Age,
        int Realm,
        int Stage,
        int SectId,
        string Ending,
        int Score,
        IReadOnlyList<TitleId> Titles);
}
