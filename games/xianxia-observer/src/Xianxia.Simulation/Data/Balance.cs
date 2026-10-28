// 세계 전체에 걸친 밸런스 상수. 숫자를 바꿀 때는 tools/Xianxia.Tools 보고서로 분포를 다시 확인한다.
namespace Xianxia.Simulation.Data
{
    /// <summary>세계 단위 밸런스 상수.</summary>
    public static class Balance
    {
        /// <summary>기본 인구(영혼 수).</summary>
        public const int DefaultPopulation = 1000;

        /// <summary>처음 떠돌이(산수) 비율.</summary>
        public const double InitialRogueShare = 0.2;

        /// <summary>환생·탄생 나이.</summary>
        public const int StartingAge = 16;

        /// <summary>첫 전쟁이 시작되는 해. 종문이 자리 잡을 시간을 준다.</summary>
        public const int FirstWarYear = 3000;

        /// <summary>전쟁 기간(원본 천년 전쟁).</summary>
        public const int WarDuration = 1000;

        /// <summary>전쟁 주기(원본 천년 전쟁 + 만년 휴전).</summary>
        public const int WarCycle = 11000;

        /// <summary>대비무 대회 간격(휴전 중).</summary>
        public const int TournamentInterval = 1000;

        /// <summary>대비무 대회가 휴전 시작 뒤 몇 년째에 처음 열리는가.</summary>
        public const int TournamentOffset = 500;

        /// <summary>경매 간격.</summary>
        public const int AuctionInterval = 300;

        /// <summary>녹봉·시설 결정 간격.</summary>
        public const int StipendInterval = 100;

        /// <summary>입문 시험 간격.</summary>
        public const int RecruitmentInterval = 50;

        /// <summary>직위 재편 간격(원본 500년 → 300년).</summary>
        public const int RerankInterval = 300;

        /// <summary>통계 표본 간격.</summary>
        public const int StatisticsInterval = 100;

        /// <summary>기본 수련 영기(경지별 1.55배씩 증가).</summary>
        public const double BaseQiGain = 10.0;

        /// <summary>경지당 수련 영기 증가 배율.</summary>
        public const double QiGainPerRealm = 1.55;

        /// <summary>대경지 돌파 실패 시 잃는 대원만 영기 비율.</summary>
        public const double MajorFailureQiLoss = 1.0;

        /// <summary>소경지 돌파 실패 시 잃는 영기 비율.</summary>
        public const double MinorFailureQiLoss = 0.3;

        /// <summary>비승(반선경 대원만 → 선계) 기본 확률.</summary>
        public const double AscensionBase = 0.2;

        /// <summary>전투 승률 곡선의 지수. 전투력 2배면 약 85% 승리.</summary>
        public const double DuelExponent = 2.5;

        /// <summary>오행 상극 전투력 보너스.</summary>
        public const double ElementAdvantage = 1.15;

        /// <summary>깨달음 최댓값.</summary>
        public const double InsightMax = 1000;

        /// <summary>도심 최댓값.</summary>
        public const double DaoHeartMax = 100;

        /// <summary>기명 제자 최대 수(원본 3명).</summary>
        public const int MaxDisciples = 3;

        /// <summary>숙적 최대 수.</summary>
        public const int MaxRivals = 3;

        /// <summary>천기(개입 점수) 최댓값.</summary>
        public const int MaxHeavenlyFavor = 10;

        /// <summary>천기 1점이 회복되는 년수.</summary>
        public const int FavorRegenYears = 200;

        /// <summary>로그 보관 수(최신순 링 버퍼).</summary>
        public const int LogCapacity = 600;

        /// <summary>연대기 보관 수.</summary>
        public const int ChronicleCapacity = 1500;

        /// <summary>통계 표본 최대 수. 넘치면 오래된 구간을 절반으로 합친다.</summary>
        public const int StatisticsCapacity = 400;

        /// <summary>현생 생애 기록 최대 수.</summary>
        public const int BiographyCapacity = 40;

        /// <summary>보관하는 전생 요약 수.</summary>
        public const int PastLifeCapacity = 20;

        /// <summary>영초 상회 판매가(영석).</summary>
        public const int HerbSellPrice = 6;

        /// <summary>영철 상회 판매가(영석).</summary>
        public const int OreSellPrice = 9;

        /// <summary>요단(영수 내단) 상회 판매가(영석).</summary>
        public const int BeastCoreSellPrice = 40;

        /// <summary>상회에서 재료를 살 때 판매가 대비 배율.</summary>
        public const double MerchantMarkup = 2.0;
    }
}
