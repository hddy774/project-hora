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
                ScreenId.Statistics => "통계 화면",
                ScreenId.Seasons => "시즌 기록",
                ScreenId.News => "뉴스 화면",
                ScreenId.CompanyFinancials => "기업 재무제표",
                ScreenId.Ownership => "지분 구조",
                ScreenId.Governance => "회사 투표",
                ScreenId.Bankruptcies => "파산 기록",
                ScreenId.People => peopleTabs[peopleRole] + " 인물 목록",
                ScreenId.PersonProfile => "인물 프로필",
                ScreenId.Politics => "경제 정부와 선거",
                ScreenId.Business => "기업 사업 과정",
                _ => "경제 관찰"
            };
        string description = $"알파 익스체인지 · {screen} · {(auto ? "진행 중" : "일시정지")} · {speed}배속";
        if (ContentDescription != description) ContentDescription = description;
    }
}
