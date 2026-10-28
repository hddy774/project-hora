// 세계 사건 상태: 시대, 대비무 대회 기록, 비경, 수조, 경매 기록.
using System.Collections.Generic;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>현재 천지 대세(시대).</summary>
    public sealed class EraState
    {
        /// <summary>시대 종류.</summary>
        public EraKind Kind { get; set; }

        /// <summary>시작 연도.</summary>
        public int StartYear { get; set; }

        /// <summary>끝나는 연도.</summary>
        public int EndYear { get; set; }

        /// <summary>지금까지의 시대 수.</summary>
        public int Count { get; set; }
    }

    /// <summary>대비무 대회 한 경기.</summary>
    /// <param name="Round">라운드(1부터, 마지막이 결승).</param>
    /// <param name="WinnerId">승자 영혼 번호.</param>
    /// <param name="WinnerName">승자 이름.</param>
    /// <param name="LoserId">패자 영혼 번호.</param>
    /// <param name="LoserName">패자 이름.</param>
    /// <param name="Upset">전투력이 낮은 쪽이 이겼는가.</param>
    public sealed record TournamentMatch(int Round, int WinnerId, string WinnerName, int LoserId, string LoserName,
        bool Upset);

    /// <summary>대비무 대회 기록.</summary>
    public sealed class TournamentRecord
    {
        /// <summary>몇 번째 대회.</summary>
        public int Number { get; set; }

        /// <summary>연도.</summary>
        public int Year { get; set; }

        /// <summary>참가자 수.</summary>
        public int Entrants { get; set; }

        /// <summary>우승자 영혼 번호.</summary>
        public int ChampionId { get; set; }

        /// <summary>우승자 이름.</summary>
        public string ChampionName { get; set; } = string.Empty;

        /// <summary>우승자 소속(종문 이름 또는 산수).</summary>
        public string ChampionAffiliation { get; set; } = string.Empty;

        /// <summary>준우승자 이름.</summary>
        public string RunnerUpName { get; set; } = string.Empty;

        /// <summary>우승 상금(영석).</summary>
        public long Prize { get; set; }

        /// <summary>모든 경기.</summary>
        public List<TournamentMatch> Matches { get; set; } = new();
    }

    /// <summary>열려 있는 비경.</summary>
    public sealed class SecretRealmState
    {
        /// <summary>이름.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>입구 지역.</summary>
        public int RegionId { get; set; }

        /// <summary>열린 해.</summary>
        public int OpenYear { get; set; }

        /// <summary>닫히는 해.</summary>
        public int CloseYear { get; set; }

        /// <summary>들어갈 수 있는 최저 경지.</summary>
        public int MinRealm { get; set; }

        /// <summary>들어갈 수 있는 최고 경지.</summary>
        public int MaxRealm { get; set; }

        /// <summary>남은 보물 수.</summary>
        public int Treasures { get; set; }

        /// <summary>진입자 영혼 번호.</summary>
        public List<int> Entrants { get; set; } = new();

        /// <summary>진입자별 수확 점수(Entrants와 같은 순서).</summary>
        public List<int> Gains { get; set; } = new();

        /// <summary>천기 개입으로 열렸는가.</summary>
        public bool Summoned { get; set; }
    }

    /// <summary>진행 중인 수조(영수 떼 습격).</summary>
    public sealed class BeastTideState
    {
        /// <summary>이끄는 영수 이름.</summary>
        public string BeastName { get; set; } = string.Empty;

        /// <summary>습격 지역.</summary>
        public int RegionId { get; set; }

        /// <summary>시작 연도.</summary>
        public int StartYear { get; set; }

        /// <summary>스스로 물러가는 연도.</summary>
        public int EndYear { get; set; }

        /// <summary>처음 세력.</summary>
        public double InitialStrength { get; set; }

        /// <summary>남은 세력.</summary>
        public double Strength { get; set; }

        /// <summary>영수 수장의 경지(전투력 기준).</summary>
        public int Realm { get; set; }

        /// <summary>방어자별 공훈 (영혼 번호, 공훈) — 같은 인덱스.</summary>
        public List<int> DefenderIds { get; set; } = new();

        /// <summary>방어자 공훈.</summary>
        public List<double> DefenderMerit { get; set; } = new();

        /// <summary>천기 개입으로 불러왔는가.</summary>
        public bool Summoned { get; set; }
    }

    /// <summary>경매 기록.</summary>
    /// <param name="Year">연도.</param>
    /// <param name="Lots">낙찰 요약.</param>
    /// <param name="Total">총 낙찰가.</param>
    public sealed record AuctionRecord(int Year, IReadOnlyList<string> Lots, long Total);

    /// <summary>비승한 선인 명부 항목.</summary>
    /// <param name="Year">비승 연도.</param>
    /// <param name="SoulId">영혼 번호.</param>
    /// <param name="Name">이름(도호 포함).</param>
    /// <param name="Lives">비승까지 걸린 생 수.</param>
    /// <param name="Age">비승 나이.</param>
    /// <param name="Affiliation">소속.</param>
    public sealed record ImmortalRecord(int Year, int SoulId, string Name, int Lives, int Age, string Affiliation);
}
