using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    int governanceStock;
    (string Run,string Security,long Hour,long Next)? voteViewKey;
    IReadOnlyList<CompanyVote> voteView=[];
    (string Run,long Next,long Before)? failureViewKey;
    IReadOnlyList<BankruptcyRecord> failureView=[];
    long failureBefore=long.MaxValue;
    readonly Stack<long> failurePages=[];
    static string VoteLabel(VoteStatus status)=>status switch { VoteStatus.Open=>"투표 대기",VoteStatus.Passed=>"가결",VoteStatus.Rejected=>"부결",_=>"취소" };
    static string VoteKindLabel(VoteKind kind)=>kind switch { VoteKind.Strategy=>"기업 전략",VoteKind.Management=>"경영 방침",_=>"자본 정책" };
    static string FailureLabel(BankruptcyKind kind)=>kind switch { BankruptcyKind.Company=>"기업",BankruptcyKind.Institution=>"기관",_=>"개인" };
    void OpenGovernance(int index) { governanceStock=index; selectedStock=-1; SetPage(8); }
    float DrawGovernance(float y)
    {
        var stock=S.Stocks[governanceStock];
        Text("주주총회와 기업 경영",20,y+23,24,Ink,true);
        Text("실제 보유 주식으로 AI 주주가 자동 의결",21,y+47,11,Muted); y+=65;
        y=CompanySelector(y,governanceStock,i=>governanceStock=i);
        y=Statement(stock.Name+$" · {stock.Generation}기",y,[("기업 전략",GameEngine.CompanyStrategyNames[(int)stock.CompanyStrategy]),
            ("경영 방침",GameEngine.ManagementNames[(int)stock.Management]),("자본 정책",GameEngine.AllocationNames[(int)stock.Allocation]),
            ("자사주 비율 / 목표",$"{(double)stock.TreasuryShares/Math.Max(1,stock.TotalShares):P1} / {stock.TreasuryTarget:P0}"),
            ("의결권 · 자사주 제외",Money(stock.OutstandingShares)+"주")]);
        string strategy=stock.CompanyStrategy switch { CompanyStrategy.Growth=>"성장: 매출 +10%, 운영비 +6%, 이익의 35% 투자",CompanyStrategy.Defensive=>"방어: 매출 −7%, 운영비 −15%, 이익의 10% 투자",_=>"가치: 매출 +2%, 운영비 −3%, 이익의 20% 투자" };
        string management=stock.Management switch { ManagementPolicy.Expansion=>"설비 확장: 매출 +3%, 운영비 +2%, 투자 비중 +10%p",ManagementPolicy.Liquidity=>"유동성: 투자 절반, 현금 완충 확대·대출 상환",_=>"비용 효율: 운영비 추가 −6%" };
        string capital=stock.Allocation switch { CapitalAllocation.Dividend=>"배당: 순이익의 최대 45%를 실제 현금으로 지급",CapitalAllocation.Reinvestment=>"재투자: 투자 비중 +15%p, 배당 최대 8%",_=>"자사주: 배당 최대 10%, 여유 현금의 8%로 매입" };
        y=Wrap(strategy+"\n"+management+"\n"+capital,21,y+8,354,11,Teal,20)+21;
        y=Wrap("매월 72시간 동안 안건을 공개하고 마감 시점의 주주가 투표합니다. 유효 표가 의결권의 1/3 이상이고, 찬성이 반대보다 많으며 전체 의결권의 25% 이상이면 가결됩니다. 자사주와 공매도 반환 청구권에는 의결권이 없습니다.",21,y+4,354,10,Muted,18)+22;
        var key=(S.RunId,stock.SecurityId,S.CompletedHours,S.NextVoteId);
        if(voteViewKey!=key)
        { voteView=store.ReadVotes(S,stock.SecurityId).Concat(S.CompanyVotes.Where(v=>v.SecurityId==stock.SecurityId && v.Status==VoteStatus.Open)).OrderByDescending(v=>v.Id).ToArray(); voteViewKey=key; }
        foreach(var vote in voteView.Take(12))
        {
            if(Visible(y,139))
            {
                Box(20,y,360,139,Card,15);
                Pill(VoteLabel(vote.Status),35,y+14,vote.Status==VoteStatus.Passed ? Teal : vote.Status==VoteStatus.Open ? Lime : Muted,75);
                Text(VoteKindLabel(vote.Kind),364,y+31,11,Muted,false,Paint.Align.Right);
                Text(GameEngine.VoteOptionName(vote.Kind,vote.Option),36,y+60,17,Ink,true);
                Text(vote.Status==VoteStatus.Open ? $"마감 {Moment(vote.CloseHour)} · 남은 {Math.Max(0,vote.CloseHour-S.CompletedHours)}시간" : $"의결 {Moment(vote.CloseHour)} · 전체 {Money(vote.EligibleShares)}주",36,y+83,10,Muted);
                Text($"찬성 {Money(vote.ForShares)} · 반대 {Money(vote.AgainstShares)} · 기권 {Money(vote.AbstainShares)}",36,y+107,11,Lime);
                Text(vote.Status==VoteStatus.Open ? "마감 시 현재 보유 지분으로 자동 투표" : "자사주 제외 · 개인은 합산 · 기관/은행의 실제 표",36,y+126,9,Muted);
            }
            y+=151;
        }
        var last=voteView.FirstOrDefault(v=>v.Status is VoteStatus.Passed or VoteStatus.Rejected);
        if(last is not null)
        {
            y=Pie("최근 안건의 의결권 구성",[("찬성",last.ForShares),("반대",last.AgainstShares),("기권",last.AbstainShares)],y);
            y=Statement("최근 의결 · 주요 주주",y,last.Ballots.OrderByDescending(b=>b.Shares).Take(8)
                .Select(b=>(b.Name,$"{Money(b.Shares)}주 · {(b.Choice==VoteChoice.For ? "찬성" : b.Choice==VoteChoice.Against ? "반대" : "기권")}")).ToArray());
        }
        Button("지분 구조 →",20,y,174,42,()=>OpenOwnership(governanceStock),false);
        Button("기업 재무제표 →",206,y,174,42,()=>{ companyStock=governanceStock; companySeason=companyTab=0; SetPage(6); },false);
        return y+62;
    }
    float DrawBankruptcies(float y)
    {
        Text("파산과 새로운 세대",20,y+23,24,Ink,true);
        Text("이전 주체의 손실과 사유를 파일에 보존",21,y+47,11,Muted); y+=65;
        var totals=S.BankruptcyTotals;
        y=Statement("시장 전체 누적 파산",y,[("기업",Money(totals.Companies)+"개"),("기관",Money(totals.Institutions)+"개"),("개인",Money(totals.Retail)+"명"),
            ("실제 재출자 · 기업",ShortMoney(totals.CompanyCapital)+"원"),("실제 재출자 · 기관/개인",ShortMoney(totals.InstitutionCapital+totals.RetailCapital)+"원")]);
        y=Wrap("파산 시 보유 자산을 정산하고 미회수 채권을 기록합니다. 새 주체는 실제 경제 자금으로 출자받습니다. 재원이 부족하면 대기하며, 투자 이익에 재출자와 임금을 포함하지 않습니다.",21,y+4,354,11,Muted,19)+20;
        var key=(S.RunId,S.NextBankruptcyId,failureBefore);
        if(failureViewKey!=key) { failureView=store.ReadBankruptcies(S,beforeId:failureBefore); failureViewKey=key; }
        Button("← 최근",20,y,100,36,()=>{ failureBefore=failurePages.Pop(); scroll=0; },false,failurePages.Count>0);
        Text("발생 순 기록",200,y+23,12,Muted,false,Paint.Align.Center);
        Button("과거 →",280,y,100,36,()=>{ failurePages.Push(failureBefore); failureBefore=failureView[^1].Id; scroll=0; },false,failureView.Count==60); y+=52;
        if(failureView.Count==0) { Text("파산이 발생하면 기업·기관·개인 기록을 표시합니다.",21,y+20,11,Muted); y+=53; }
        foreach(var failure in failureView)
        {
            if(Visible(y,149))
            {
                Box(20,y,360,149,Card,15); Pill(FailureLabel(failure.Kind)+" 파산",35,y+14,Red,81);
                Text(Moment(failure.Hour),363,y+31,10,Muted,false,Paint.Align.Right);
                TextFit(failure.Name+$" · {failure.Generation}기",36,y+58,14,Ink,328,true);
                TextFit(failure.Reason,36,y+82,11,Muted,328);
                Text($"당시 순자산 {ShortMoney(failure.Equity)}원",36,y+105,11,Red);
                Text($"미회수 {ShortMoney(failure.LoanWriteOff+failure.TradeWriteOff+failure.ShortWriteOff+failure.FineWriteOff+failure.TaxWriteOff)}원 · 재출자 {ShortMoney(failure.ReplacementCapital)}원",36,y+129,10,Teal);
            }
            y+=162;
        }
        Button("시장 통계로 돌아가기",20,y,360,42,()=>SetPage(3),false); return y+64;
    }
}
