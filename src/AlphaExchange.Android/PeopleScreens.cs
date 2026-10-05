using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    int peopleRole,peopleOffset,profilePerson=1,rankingRole;
    int portraitZoom=-1;
    int businessStock;
    static readonly string[] peopleTabs=["종합","투자자","경영자","정치인","금융인"];
    float PeopleRoleTabs(float y,bool league)
    {
        int selected=league ? rankingRole : peopleRole;
        for(int i=0;i<peopleTabs.Length;i++)
        {
            int choice=i; float x=20+i*73;
            Box(x,y,68,34,selected==i ? Lime : Card2,10);
            Text(peopleTabs[i],x+34,y+22,11,selected==i ? Bg : Muted,true,Paint.Align.Center);
            Hit(x,y,68,34,()=> { if(league) rankingRole=choice; else peopleRole=choice; peopleOffset=0; scroll=0; });
        }
        return y+49;
    }
    void OpenPerson(int id) { profilePerson=id; selectedTrader=-1; SetPage(11); }
    float DrawPeople(float y)
    {
        Text("경제를 움직이는 200명",20,y+22,24,Ink,true);
        Text("투자자 100 · 경영자 30 · 정치인 50 · 금융인 20",21,y+46,11,Muted); y+=65;
        y=PeopleRoleTabs(y,false);
        Button("경제 정부 · 선거와 정책 표결  →",20,y,360,40,()=>SetPage(12),false); y+=56;
        var people=game!.World.People.Where(p=>peopleRole==0 || (int)p.Role==peopleRole-1).ToList();
        foreach(var person in people.Skip(peopleOffset).Take(40))
        {
            int id=person.Id;
            if(Visible(y,96))
            {
                Box(20,y,360,96,Card,15); Portrait(person.PortraitId,30,y+10,60,76);
                Text(person.Name,103,y+29,16,Ink,true); TextFit(person.Office,103,y+51,11,Muted,258);
                Text($"개인 자산 {ShortMoney(person.Cash)}원",103,y+74,11,Teal);
                Text($"투표권 {person.VotingPower}",364,y+28,10,Lime,false,Paint.Align.Right);
                Hit(20,y,360,96,()=>OpenPerson(id));
            }
            y+=108;
        }
        if(people.Count>40)
        {
            Button("이전",20,y,110,38,()=> { peopleOffset=Math.Max(0,peopleOffset-40); scroll=0; },false,peopleOffset>0);
            Text($"{peopleOffset+1}–{Math.Min(people.Count,peopleOffset+40)} / {people.Count}",200,y+24,11,Muted,false,Paint.Align.Center);
            Button("다음",270,y,110,38,()=> { peopleOffset+=40; scroll=0; },false,peopleOffset+40<people.Count); y+=53;
        }
        return y+10;
    }
    float DrawPeopleRanking(float y,PersonRole? role)
    {
        var ranking=game!.PeopleRanking(role);
        Text(role is {} r ? game.WorldRules.Roles[(int)r].Name+" 분야 랭킹" : "종합 랭킹",21,y+18,21,Ink,true); y+=38;
        for(int i=0;i<Math.Min(3,ranking.Count);i++)
        {
            var row=ranking[i]; var p=game.Person(row.PersonId); float x=20+i*123;
            Box(x,y,114,168,Card2,15); Portrait(p.PortraitId,x+8,y+9,98,98);
            Text($"{i+1}위 · {p.Name}",x+57,y+129,11,Lime,true,Paint.Align.Center);
            Text($"종합 {row.Score:0.00}",x+57,y+151,11,Ink,false,Paint.Align.Center);
            int id=p.Id; Hit(x,y,114,168,()=>OpenPerson(id));
        }
        y+=184;
        y=Wrap("종합 점수: 능력 40% · 분야 성과 30% · 개인 재무 20% · 평판 10%. 법인 자산은 개인 자산에 합산하지 않습니다.",21,y+8,354,11,Muted,18)+16;
        foreach(var row in ranking)
        {
            var p=game.Person(row.PersonId);
            if(Visible(y,72))
            {
                Box(20,y,360,72,Card,13); Text(row.Rank.ToString("00"),33,y+42,12,row.Rank<=3 ? Lime : Muted,true);
                Portrait(p.PortraitId,60,y+12,46,48); Text(p.Name,118,y+29,13,Ink,true); TextFit(p.Office,118,y+51,10,Muted,173);
                Text(row.Score.ToString("0.00"),364,y+29,15,Lime,true,Paint.Align.Right);
                Text($"투표 {p.VotingPower}",364,y+52,10,Muted,false,Paint.Align.Right);
                int id=p.Id; Hit(20,y,360,72,()=>OpenPerson(id));
            }
            y+=82;
        }
        return y+10;
    }
    float DrawInvestorPodium(float y)
    {
        var top=FrameRanking(rankingMetric,comparisonPeriod).Take(3).ToArray();
        foreach(var row in top)
        {
            float x=20+(row.Rank-1)*123; var p=game!.Person(row.Trader.Id);
            Box(x,y,114,154,Card2,15); Portrait(p.PortraitId,x+8,y+8,98,94);
            Text($"{row.Rank}위 · {p.Name}",x+57,y+123,11,Lime,true,Paint.Align.Center);
            string value=rankingMetric==RankingMetric.Return ? Percent(row.Value) : ShortMoney((long)row.Value);
            Text(value,x+57,y+143,10,Ink,false,Paint.Align.Center);
            int id=p.Id; Hit(x,y,114,154,()=>OpenPerson(id));
        }
        return y+170;
    }
    float DrawPersonProfile(float y)
    {
        var p=game!.Person(profilePerson); var role=game.WorldRules.Roles[(int)p.Role];
        Button("← 인물 목록",20,y,174,36,()=>SetPage(10),false);
        Button("숫자: "+(S.World!.NumberFormat==0 ? "축약 ⇄ 전체" : "전체 ⇄ 축약"),206,y,174,36,()=> { S.World.NumberFormat=1-S.World.NumberFormat; Save(); },false); y+=50;
        Box(20,y,360,206,Card,18,Stroke); Portrait(p.PortraitId,30,y+10,113,186);
        Hit(30,y+10,113,186,()=>portraitZoom=p.PortraitId);
        Text("눌러서 확대",86,y+193,9,Lime,true,Paint.Align.Center);
        Text(p.Name,158,y+40,24,Ink,true); Text(role.Name,158,y+65,12,Teal);
        TextFit(p.Office,158,y+90,11,Muted,203);
        Text("개인 자산",158,y+123,11,Muted); TextFit(ShortMoney(p.Cash)+"원",158,y+149,22,Lime,203,true);
        TextFit(KoreanNumber.Full(p.Cash)+"원",158,y+172,11,Muted,203);
        Text($"평판 {p.Reputation:0.0} · 투표권 {p.VotingPower}",158,y+192,11,Ink); y+=222;
        if(p.Role==PersonRole.Investor)
        {
            var t=game.Owner(p.Id);
            y=Statement("개인과 투자 법인의 재무 구분",y,[("개인 지갑",KoreanNumber.Dual(p.Cash)+"원"),("투자사 순자산",ShortMoney(t.Equity(S.Stocks))+"원"),
                ("투자사 현금",ShortMoney(t.Cash)+"원"),("시즌 수익률",Percent(t.Return(S.Stocks))),("누적 거래량",Money(t.TradedVolume)+"주"),("누적 거래금액",ShortMoney(t.TradedTurnover)+"원")]);
            Button(S.FollowedId==p.Id ? "현재 관찰 중 · 투자 계획 보기 →" : "이 투자사 관찰 · 소유자 기밀 보기 →",20,y,360,42,()=> { S.FollowedId=p.Id; portfolioTab=3; institutionTab=4; SetPage(1); Save(); },false); y+=57;
        }
        else if(p.Role==PersonRole.Executive)
        {
            var company=S.Stocks[p.OrganizationId];
            y=Statement("기업 법인 재무",y,[("회사",company.Name),("법인 자본",ShortMoney(company.Report.Equity)+"원"),("법인 현금",ShortMoney(company.Report.Cash)+"원"),("개인 지갑",ShortMoney(p.Cash)+"원")]);
            Button("회사 재무제표와 사업 →",20,y,360,42,()=> { businessStock=p.OrganizationId; SetPage(13); },false); y+=57;
        }
        else y=Statement("소속 공공기관과 개인 지갑",y,[("소속",p.Role==PersonRole.Politician ? "경제 정부 · "+(p.Party==0 ? "성장당" : "안정당") : "시장안정은행"),
            ("기관 현금",ShortMoney(p.Role==PersonRole.Politician ? S.Government.Cash : S.Bank.Cash)+"원"),("개인 지갑",ShortMoney(p.Cash)+"원")]);
        y=Statement("개인 자금 흐름",y,[("초기 개인 현금",ShortMoney(p.OpeningCash)+"원"),("보수 수입",ShortMoney(p.Earnings)+"원"),("등록 후원 수입",ShortMoney(p.DonationsReceived)+"원"),
            ("정책 후원 지출",ShortMoney(p.DonationsPaid)+"원"),("개인 벌금 납부",ShortMoney(p.FinesPaid)+"원")]);
        var skills=p.Role==PersonRole.Investor ? game.Owner(p.Id).Abilities.Values() : p.Skills;
        for(int i=0;i<skills.Length;i++)
        { Box(20,y,360,48,Card,12); Text(role.Skills[i],35,y+23,12,Ink,true); Text(skills[i].ToString(),364,y+23,12,Lime,true,Paint.Align.Right); Box(35,y+34,329*skills[i]/100f,4,PaletteColors[i],2); y+=57; }
        return y+10;
    }
    float DrawPolitics(float y)
    {
        var w=game!.World;
        Text("경제 의회와 통화 위원회",20,y+22,23,Ink,true); y+=43;
        y=Statement("현직 · 임기·표결",y,[("국가 지도자",game.Person(w.LeaderId).Name),("다음 정부 선거",Moment(w.NextGovernmentElectionHour)),
            ("은행 총재",game.Person(w.GovernorId).Name),("다음 총재 선거",Moment(w.NextGovernorElectionHour)),("양당 구성","성장당 25명 · 안정당 25명"),
            ("정책 후원 법률",w.PoliticalFundingAllowed ? "등록 후원 허용" : "대가성 후원 금지 · 벌금"),("경기 / 물가",$"{w.EconomicActivity:P1} / {w.Inflation:P1}")]);
        y=Wrap("정책과 법률은 200명 인물의 투표로 결정합니다. 시즌 종합 순위에 따라 다음 시즌 1~10표를 받습니다. 정부·은행 수장은 10~30일마다 선거로 교체됩니다.",21,y+7,354,11,Muted,18)+18;
        foreach(var v in w.Votes.AsEnumerable().Reverse())
        {
            int actor=v.Kind is WorldVoteKind.GovernmentElection or WorldVoteKind.GovernorElection ? v.WinnerId : v.ProposerId;
            string outcome=v.Kind==WorldVoteKind.EconomicPolicy ? game.WorldRules.Policies[v.WinnerId].Name : v.Kind==WorldVoteKind.FundingLaw ? (v.WinnerId==1 ? "허용" : "금지") : game.Person(v.WinnerId).Name;
            Box(20,y,360,103,Card,15); TextFit(v.Title,36,y+28,14,Ink,328,true); Text(outcome,36,y+52,12,Lime,true);
            Text($"{Moment(v.Minute/60)} · 총 {v.Ballots.Sum(b=>b.Weight)}표 · 200명 투표",36,y+76,10,Muted);
            int id=actor; Hit(20,y,360,103,()=>OpenPerson(id)); y+=116;
        }
        return y+10;
    }
    float DrawBusiness(float y)
    {
        var stock=S.Stocks[businessStock];
        Text(stock.Name+" 사업 관찰",21,y+22,23,Ink,true); y+=43;
        Button("공개 재무제표 →",20,y,360,40,()=> { companyStock=businessStock; companySeason=companyTab=0; SetPage(6); },false); y+=57;
        if(game!.PrivateBusiness(S.FollowedId,businessStock) is not {} p)
        {
            y=Wrap("사업 내부 진행은 현재 관찰하는 투자사가 발행 지분의 상위 3대 보유자일 때 열립니다. 자사주·은행·개미의 실제 보유량도 순위에 포함합니다. 공개 공시와 재무제표는 계속 볼 수 있습니다.",21,y+10,354,13,Muted,22)+25;
            return y;
        }
        Text("주요 주주 열람 · 사업 내부 기록",21,y+17,12,Lime,true); y+=34;
        y=Statement(p.Detail,y,[("총 기간",$"{p.DurationDays}일"),("현재 단계",game.WorldRules.BusinessStages[p.Stage]),("사업 투자 예산",KoreanNumber.Dual(p.Budget)+"원"),
            ("집행액",ShortMoney(p.Paid)+"원"),("기대 매출 기준",ShortMoney(p.SalesTarget)+"원"),("사업 품질",$"{p.Quality:P1}"),("상태",p.Completed ? "검증 완료 · 월 결산 반영" : "진행 중")]);
        y=Pie("투자 예산 집행",[("집행",(double)p.Paid),("남은 예산",(double)Math.Max(0,p.Budget-p.Paid))],y);
        Button("경영자 프로필 →",20,y,360,40,()=>OpenPerson(101+businessStock),false); return y+57;
    }
    void DrawPortraitZoom()
    {
        int id=portraitZoom; float top=Modal(h-16,()=>portraitZoom=-1);
        var p=game!.World.People.FirstOrDefault(p=>p.PortraitId==id);
        Text((p?.Name ?? "대표")+" · 일러스트 확대",27,top+39,17,Ink,true);
        float available=h-top-96;
        var region=portraitRegions[id-1]; float ratio=(float)region.width/region.height;
        float width=Math.Min(360,available*ratio),height=width/ratio;
        Portrait(id,(400-width)/2,top+64,width,height);
    }
}
