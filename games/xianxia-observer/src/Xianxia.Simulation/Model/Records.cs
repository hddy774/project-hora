// 세계 기록, 영혼별 업적(원본 HD), 영석 장부.
using System.Collections.Generic;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>기록 하나.</summary>
    /// <param name="Year">달성 연도.</param>
    /// <param name="SoulId">영혼 번호.</param>
    /// <param name="Name">이름.</param>
    /// <param name="Value">기록 값.</param>
    /// <param name="Detail">설명.</param>
    public sealed record RecordEntry(int Year, int SoulId, string Name, double Value, string Detail);

    /// <summary>세계 기록(최초·최연소·최장수 등).</summary>
    public sealed class WorldRecords
    {
        /// <summary>경지별 최초 도달(인덱스 = 경지, 아직 없으면 null).</summary>
        public List<RecordEntry?> FirstReach { get; set; } = new(new RecordEntry?[Realms.Count]);

        /// <summary>경지별 최연소 도달.</summary>
        public List<RecordEntry?> YoungestReach { get; set; } = new(new RecordEntry?[Realms.Count]);

        /// <summary>최장수(한 생).</summary>
        public RecordEntry? LongestLife { get; set; }

        /// <summary>최다 윤회.</summary>
        public RecordEntry? MostLives { get; set; }

        /// <summary>최고 전투력.</summary>
        public RecordEntry? HighestPower { get; set; }

        /// <summary>한 생 최다 승리.</summary>
        public RecordEntry? MostWins { get; set; }

        /// <summary>최다 영석 보유.</summary>
        public RecordEntry? Richest { get; set; }

        /// <summary>대비무 우승한 생이 가장 많은 영혼(생 단위 누적).</summary>
        public RecordEntry? MostTournamentWins { get; set; }
    }

    /// <summary>영혼 하나의 윤회 업적(원본 HD 계승, 경지·단계 표시 버그 수정).</summary>
    public sealed class SoulRecord
    {
        /// <summary>영혼 번호.</summary>
        public int SoulId { get; set; }

        /// <summary>이름.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>끝난 생 수.</summary>
        public int Lives { get; set; }

        /// <summary>최고 생애 점수.</summary>
        public int BestScore { get; set; }

        /// <summary>최고 생애의 경지.</summary>
        public int BestRealm { get; set; }

        /// <summary>최고 생애의 단계.</summary>
        public int BestStage { get; set; }

        /// <summary>최고 생애가 몇 번째 생인가.</summary>
        public int BestLife { get; set; }

        /// <summary>상위 5개 생애 점수(내림차순).</summary>
        public List<int> TopScores { get; set; } = new();

        /// <summary>상위 5개 합.</summary>
        public int TopFiveSum { get; set; }

        /// <summary>누적 대경지 돌파.</summary>
        public int TotalMajorBreakthroughs { get; set; }

        /// <summary>누적 승리.</summary>
        public int TotalWins { get; set; }

        /// <summary>누적 천겁 통과.</summary>
        public int TotalTribulations { get; set; }

        /// <summary>대비무에서 우승한 생의 수. 한 생의 여러 우승은 한 번으로 센다.</summary>
        public int TournamentWins { get; set; }

        /// <summary>누적 전쟁 영웅.</summary>
        public int WarHeroCount { get; set; }

        /// <summary>얻어 본 적 있는 호칭(중복 없음).</summary>
        public List<TitleId> TitlesEver { get; set; } = new();

        /// <summary>마지막 생의 끝.</summary>
        public string LastEnding { get; set; } = "없음";

        /// <summary>비승했는가.</summary>
        public bool Ascended { get; set; }
    }

    /// <summary>영석이 세계에 들어오는 경로.</summary>
    public enum StoneSource
    {
        /// <summary>처음 지급.</summary>
        Initial = 0,

        /// <summary>탐색 수확.</summary>
        Exploration = 1,

        /// <summary>상회에 재료 판매.</summary>
        MerchantSale = 2,

        /// <summary>종문 영지 산출.</summary>
        Territory = 3,

        /// <summary>기연·비경 보물.</summary>
        Fortune = 4,

        /// <summary>임무 보수(의뢰인 지급).</summary>
        Mission = 5,
    }

    /// <summary>영석이 세계에서 사라지는 경로.</summary>
    public enum StoneSink
    {
        /// <summary>상회에서 재료·공법 구입.</summary>
        MerchantPurchase = 0,

        /// <summary>종문 시설 건설.</summary>
        Facility = 1,

        /// <summary>경매 낙찰.</summary>
        Auction = 2,

        /// <summary>법보 제련 소모.</summary>
        Refining = 3,

        /// <summary>비승자가 가져감.</summary>
        Ascension = 4,

        /// <summary>전쟁 군비.</summary>
        War = 5,

        /// <summary>종문 시설 유지비.</summary>
        Upkeep = 6,

        /// <summary>상속자 없는 유품이 흩어짐.</summary>
        Death = 7,
    }

    /// <summary>
    /// [보존] 영석 장부. 모든 영석의 생성·소멸을 기록해 "수선자 보유 + 종문 금고 = 생성 − 소멸"을 검사한다.
    /// 원본은 녹봉·전리품이 장부 없이 생겨났다.
    /// </summary>
    public sealed class StoneLedger
    {
        /// <summary>경로별 생성량. 인덱스는 <see cref="StoneSource"/>.</summary>
        public List<long> Created { get; set; } = new() { 0, 0, 0, 0, 0, 0 };

        /// <summary>경로별 소멸량. 인덱스는 <see cref="StoneSink"/>.</summary>
        public List<long> Destroyed { get; set; } = new() { 0, 0, 0, 0, 0, 0, 0, 0 };

        /// <summary>장부상 현재 총량.</summary>
        /// <returns>생성 합 − 소멸 합.</returns>
        public long Expected()
        {
            long total = 0;
            foreach (long value in Created)
            {
                total += value;
            }

            foreach (long value in Destroyed)
            {
                total -= value;
            }

            return total;
        }

        /// <summary>생성 기록.</summary>
        /// <param name="source">경로.</param>
        /// <param name="amount">양(0 이상).</param>
        public void Create(StoneSource source, long amount)
        {
            Created[(int)source] += amount;
        }

        /// <summary>소멸 기록.</summary>
        /// <param name="sink">경로.</param>
        /// <param name="amount">양(0 이상).</param>
        public void Destroy(StoneSink sink, long amount)
        {
            Destroyed[(int)sink] += amount;
        }
    }
}
