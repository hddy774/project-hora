using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    int institutionTab;
    static readonly string[] institutionTabs=["기관·능력","직원","시즌 보상","투자 기간"];
    float DrawInstitutionDevelopment(float y,Trader t)
    {
        Text("대표와 기관의 성장",21,y+16,20,Ink,true);
        Text("대표 50% + 직원·조직 50% · AI가 보상 포인트를 배분",21,y+40,10,Muted); y+=57;
        for(int i=0;i<institutionTabs.Length;i++)
        {
            int tab=i; float x=20+i*92;
            Box(x,y,84,34,institutionTab==i ? Lime : Card2,10);
            Text(institutionTabs[i],x+42,y+22,10,institutionTab==i ? Bg : Muted,true,Paint.Align.Center);
            Hit(x,y,84,34,()=>{ institutionTab=tab; scroll=0; });
        }
        y+=51;
        var d=t.Development!;
        return institutionTab switch { 1=>DrawEmployees(y,t),2=>DrawGrowthRewards(y,t),3=>DrawInvestmentPeriods(y,t),_=>DrawInstitutionAbilities(y,t,d) };
    }
    float DrawInstitutionAbilities(float y,Trader t,InstitutionDevelopment d)
    {
        var skills=game!.Capabilities(t);
        y=Statement("대표 프로필 · 실제 투자 성과",y,[("시즌 수익률",Percent(t.Return(S.Stocks))),
            ("전체 수익률",Percent(t.ReturnIndex/Math.Max(1e-12,t.OpeningSnapshot.ReturnIndex)-1)),
            ("누적 거래량",Money(t.TradedVolume)+"주"),("누적 거래금액",ShortMoney(t.TradedTurnover)+"원"),
            ("대표 성장 / 미배분",$"{d.PointsSpent}P / {d.AvailablePoints}P"),("직원 / 월 급여",$"{d.EmployeeCount}명 / {ShortMoney(game.MonthlyPayroll(t))}원")]);
        Text("10개 기관 능력",21,y+15,19,Ink,true);
        Text("대표 / 조직 → 기관의 실제 판단·집행 능력",21,y+39,10,Muted); y+=55;
        for(int i=0;i<10;i++)
        {
            if(Visible(y,55))
            {
                var kind=(AbilityKind)i; int rep=t.Abilities.Get(kind),team=skills.Team.Get(kind),effective=skills.Effective.Get(kind);
                Box(20,y,360,55,Card,12); Text(GameEngine.AbilityNames[i],35,y+22,12,Ink,true);
                Text($"{rep} / {team} → {effective}",364,y+22,12,Lime,true,Paint.Align.Right);
                Box(35,y+37,329,5,Card2,2); Box(35,y+37,329*effective/100f,5,PaletteColors[i],2);
            }
            y+=64;
        }
        y=Wrap(t.Decision,22,y+12,356,12,Teal)+16;
        var loan=game.LoanTerms(t);
        y=Statement("은행 · 신용 "+loan.Rating,y,[("신용 점수 / 시즌 보너스",$"{t.CreditScore} / 99 · +{d.CreditBonus}"),
            ("자기자산 대비 한도",$"{loan.AssetRatio:P0}"),("연 이자율",$"{loan.AnnualRate:P2}"),
            ("대출 한도",Money(loan.Limit)+"원"),("추가 대출 가능",Money(loan.Available)+"원"),
            ("대출 잔액",Money(t.LoanDebt)+"원"),("누적 이자 비용",Money(t.InterestExpense)+"원"),
            ("벌금 / 미납",$"{ShortMoney(t.Fines)} / {ShortMoney(t.FineDebt)}원")]);
        if(d.LegacyAbilities.Length>0)
            y=Wrap("이전 대표의 능력 기록은 보존하고, 현재 대표는 모두 같은 50점에서 시즌 보상으로 성장합니다.",21,y+8,352,10,Muted,18)+14;
        return y;
    }
    float DrawEmployees(float y,Trader t)
    {
        var d=t.Development!; var rules=game!.Rules.Employees;
        y=Statement("실제 고용·운영 비용",y,[("고용 인원",$"{d.EmployeeCount} / {rules.MaximumCount}명"),
            ("월 급여 합계",ShortMoney(game.MonthlyPayroll(t))+"원"),("누적 급여·채용 비용",ShortMoney(t.StaffCosts)+"원"),
            ("월 급여 예산",$"평가 총자산의 {rules.PayrollAssetRatio:P1}"),
            ("다음 채용 판단",d.LastHireHour<0 ? "첫 거래일 이후" : Moment(d.LastHireHour+rules.HireIntervalHours))]);
        y=Wrap("기관 AI가 현금 완충·급여 예산과 부족한 역할을 보고 고용합니다. 같은 역할도 인원이 늘수록 기관 능력에 기여하며 효과는 점차 완만해집니다.",21,y+7,352,11,Muted,19)+20;
        Text("등급 · 기본 능력 · 인원 · 월 급여",21,y+15,16,Ink,true); y+=36;
        for(int i=0;i<rules.Grades.Length;i++)
        {
            var grade=rules.Grades[i]; int count=d.Employees.Where(c=>(int)c.Grade==i).Sum(c=>c.Count);
            Box(20,y,360,62,Card,13);
            Text($"{grade.Name}급 · 능력 {grade.Skill}",36,y+26,13,Ink,true);
            Text($"1인 월 {ShortMoney(grade.MonthlySalary)}원 · 채용 {ShortMoney(grade.HiringCost)}원",36,y+47,10,Muted);
            Text($"{count}명",364,y+36,17,count>0 ? Lime : Muted,true,Paint.Align.Right); y+=73;
        }
        Text("역할별 팀",21,y+18,18,Ink,true); y+=40;
        if(d.Employees.Count==0) { Text("현금과 급여 예산을 확보한 뒤 직원이 합류합니다.",22,y+15,11,Muted); return y+54; }
        foreach(var cohort in d.Employees.OrderBy(c=>c.Role).ThenBy(c=>c.Grade))
        {
            if(Visible(y,54))
            {
                Box(20,y,360,54,Card2,12); Text(GameEngine.AbilityNames[(int)cohort.Role],36,y+23,12,Ink,true);
                Text($"{cohort.Grade}급 {cohort.Count}명",364,y+23,12,Lime,true,Paint.Align.Right);
                Text($"월 {ShortMoney(rules.Grades[(int)cohort.Grade].MonthlySalary*cohort.Count)}원",36,y+43,10,Muted);
            }
            y+=64;
        }
        return y+10;
    }
    float DrawGrowthRewards(float y,Trader t)
    {
        var d=t.Development!;
        y=Statement("대표 능력 포인트",y,[("최초 능력",$"10개 각각 {game!.Rules.Representative.InitialAbility} / 100"),
            ("획득 / 배분",$"{d.PointsEarned}P / {d.PointsSpent}P"),("남은 포인트",$"{d.AvailablePoints}P"),
            ("최근 시즌 / 순위",d.LastRewardSeason==0 ? "첫 시즌 진행 중" : $"S{d.LastRewardSeason} · {d.LastRewardRank}위"),
            ("최근 획득",$"{d.LastRewardPoints}P"),("현재 신용 보너스",$"+{d.CreditBonus}")]);
        y=Wrap("시즌 수익률의 상대 순위에서 해당되는 보상 하나를 받습니다. AI는 현재 능력의 부족한 부분과 위험 관리·집행 필요에 따라 포인트를 배분합니다. 거래량만 늘려 보상을 얻지 않습니다.",21,y+7,352,11,Muted,19)+18;
        Text("순위 구간 · 능력 포인트 · 신용 보너스",21,y+17,16,Ink,true); y+=39;
        int lower=1;
        foreach(var reward in game.Rules.Rewards)
        {
            string range=lower==reward.MaximumRank ? $"{lower}위" : $"{lower}~{reward.MaximumRank}위";
            Box(20,y,360,46,Card,11); Text(range,36,y+29,12,Ink,true);
            Text($"+{reward.AbilityPoints}P · 신용 +{reward.CreditBonus}",364,y+29,12,Lime,true,Paint.Align.Right);
            lower=reward.MaximumRank+1; y+=55;
        }
        y=Wrap("상한은 능력별 100점입니다. 최대 능력에 도달하면 남은 포인트를 보관합니다. 신용 보너스는 상한과 시즌 유지율을 적용하고 채무·손실·연체 평가도 계속 반영합니다. 시즌 도중 새로 생긴 기관은 그 시즌 성장 보상을 받지 않습니다.",21,y+8,352,11,Muted,19)+19;
        y=Statement("누적 능력 배분",y,Enumerable.Range(0,10).Select(i=>(GameEngine.AbilityNames[i],$"+{d.AllocatedPoints[i]}P")).ToArray());
        Button("과거 시즌의 실제 지급 기록 →",20,y,360,43,()=>{ portfolioTab=2; historyPage=0; scroll=0; },false);
        return y+61;
    }
    float DrawInvestmentPeriods(float y,Trader t)
    {
        var d=t.Development!;
        y=Statement("현금·주문·부채를 함께 관리",y,[("실제 현금 비중",$"{game!.CashRatio(t):P1}"),
            ("현재 목표",$"{d.TargetCashRatio:P1} · 범위 5~30%"),("주문 가능 현금",ShortMoney(game.AvailableCash(t))+"원"),
            ("매수/공매도 주문 예약",ShortMoney(t.ReservedCash)+"원"),("공매도 필요 담보",ShortMoney(t.ShortCollateral(S.Stocks))+"원"),
            ("다음 정기 재평가",Moment(d.NextDecisionHour))]);
        y=Wrap("초단기: 1시간~1일 미만 · 단기: 1일~10일 미만 · 장기: 10일 이상. 계획과 실제 보유 기간을 구분합니다. 장기는 20일·30일 이상 이어질 수 있고 중요한 뉴스·위험 변화 때 일찍 다시 판단합니다.",21,y+8,352,11,Muted,19)+15;
        y=Wrap("새 시장의 전액 현금, 주가 급변, 주식 공급 고갈, 거래 중단에서는 실제 비중이 목표 밖일 수 있습니다. AI는 실제 주문으로 조정하며 주문을 꾸며내지 않습니다.",21,y,352,10,Muted,18)+22;
        Text(t.ActiveMethods,21,y+14,15,Ink,true); y+=35;
        bool any=false;
        for(int i=0;i<S.Stocks.Count;i++)
        {
            if(t.Shares[i]==0 && t.ShortShares[i]==0) continue;
            any=true; var timing=game.PositionTiming(t,i); var stock=S.Stocks[i];
            if(Visible(y,92))
            {
                Box(20,y,360,92,Card,14); Text(stock.Name,36,y+24,13,Ink,true);
                Text($"계획 {game.Rules.Horizons[(int)timing.Planned].Name}",364,y+24,12,Lime,true,Paint.Align.Right);
                string age=timing.AgeHours is {} hours ? $"{hours/24}일 {hours%24}시간 · 실제 {game.Rules.Horizons[(int)timing.Actual!].Name}" : "이전 보유 · 매수 시각 미기록";
                Text(age,36,y+48,11,Muted);
                Text($"보유 {t.Shares[i]}주 / 공매도 {t.ShortShares[i]}주",36,y+72,10,Muted);
                int stockIndex=i; Hit(20,y,360,92,()=>selectedStock=stockIndex);
            }
            y+=103;
        }
        if(!any) { Text("보유 종목을 배치하며 투자 기간을 판단합니다.",22,y+20,11,Muted); y+=60; }
        return y+10;
    }
}
