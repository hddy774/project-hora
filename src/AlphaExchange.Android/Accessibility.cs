namespace AlphaExchange.App;

public sealed partial class GameView
{
    void DescribeScreen()
    {
        string screen=loading ? "기록 불러오는 중" : lobby ? "시뮬레이션 시작 화면" :
            portraitZoom>=0 ? "일러스트 확대" : selectedStock>=0 ? "실시간 호가 · 매수와 매도" :
            selectedTrader>=0 ? "투자사 프로필" : help ? "시뮬레이션 안내" : page switch
            {
                0=>"시장 화면",1=>"자산과 투자 계획",2=>"랭킹 화면",3=>"통계 화면",4=>"시즌 기록",5=>"뉴스 화면",
                6=>"기업 재무제표",7=>"지분 구조",8=>"회사 투표",9=>"파산 기록",
                10=>peopleTabs[peopleRole]+" 인물 목록",11=>"인물 프로필",12=>"경제 정부와 선거",13=>"기업 사업 과정",_=>"경제 관찰"
            };
        string description=$"알파 익스체인지 · {screen} · {(auto ? "진행 중" : "일시정지")} · {speed}배속";
        if(ContentDescription!=description) ContentDescription=description;
    }
}
