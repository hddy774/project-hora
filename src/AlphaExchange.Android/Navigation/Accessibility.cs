namespace AlphaExchange.App;

public sealed partial class GameView
{
    void DescribeScreen()
    {
        string screen = loading ? "기록 불러오는 중" : fileBusy ? "기록 파일 처리 중" :
            confirmNew ? "새 시장 시작 확인" : lobby ? "시뮬레이션 시작 화면" :
            portraitZoom >= 0 ? "일러스트 확대" : selectedStock >= 0 ? "실시간 호가 · 매수와 매도" :
            selectedTrader >= 0 ? "투자사 프로필" : help ? "시뮬레이션 안내" : page switch
            {
                ScreenId.Market => "시장 화면",
                ScreenId.Portfolio => "자산과 투자 계획",
                ScreenId.Ranking => "랭킹 화면",
                ScreenId.Statistics => "통계 화면 · " + (statsTab switch { 1 => "업종별", 2 => "개미 재무", 3 => "기관 재무", 4 => "정책·금융", 5 => "호가·수급", 6 => "위험·전략", _ => "전체 시장" }),
                ScreenId.Seasons => "시즌 기록",
                ScreenId.News => "뉴스 화면",
                ScreenId.CompanyFinancials => "기업 재무제표 · " + (companyTab switch { 1 => "손익", 2 => "현금흐름", 3 => "자본변동", 4 => "재무비율", 5 => "현금·상환", 6 => "충격 분석", _ => "재무상태" }),
                ScreenId.Ownership => "지분 구조",
                ScreenId.Bankruptcies => "파산 기록",
                ScreenId.Investors => "투자자 목록",
                _ => "경제 관찰"
            };
        string description = $"알파 익스체인지 · {screen} · {(auto ? "진행 중" : "일시정지")} · {speed}배속";
        if (game is not null && !lobby && page is ScreenId.Market or ScreenId.CompanyFinancials or ScreenId.Ownership)
            description += $" · 업종 {marketSector} · 표시 종목 {FilteredCompanies().Length}";
        if (game is not null && !lobby && page == ScreenId.CompanyFinancials)
            description += " · " + S.Stocks[companyStock].Symbol;
        if (ContentDescription != description) ContentDescription = description;
    }
}
