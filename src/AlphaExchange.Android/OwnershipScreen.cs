using Android.App;
using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    int ownershipStock,ownershipPage;
    const int OwnersPerPage=12;
    void OpenOwnership(int index)
    { ownershipStock=index; ownershipPage=0; selectedStock=-1; SetPage(7); }
    void PickOwnershipCompany()
    {
        var indices=FilteredCompanies();
        string[] names=indices.Select(i=>$"{S.Stocks[i].Symbol} · {S.Stocks[i].Name} · {S.Stocks[i].Sector}").ToArray();
        new AlertDialog.Builder(Context)!.SetTitle("지분을 볼 회사 · "+marketSector)!.SetItems(names,(_,e)=>OpenOwnership(indices[e.Which]))!.Show();
    }
    float DrawOwnership(float y)
    {
        var stock=S.Stocks[ownershipStock]; var structure=game!.Ownership(ownershipStock);
        Text("회사의 지분 구조",20,y+23,24,Ink,true);
        Text($"현재 보유 기준 · {Moment(structure.Hour)}",21,y+47,11,Muted); y+=65;
        Button("←",20,y,45,48,()=>OpenOwnership((ownershipStock+S.Stocks.Count-1)%S.Stocks.Count),false);
        Box(74,y,252,48,Card2,12,Stroke); TextFit(stock.Name,90,y+21,14,Ink,215,true);
        Text(stock.Symbol+" · "+stock.Sector+" · 회사 선택 ▾",90,y+39,10,Muted);
        Hit(74,y,252,48,PickOwnershipCompany);
        Button("→",335,y,45,48,()=>OpenOwnership((ownershipStock+1)%S.Stocks.Count),false); y+=63;
        y=SectorTabs(y);
        Button("주주총회 · 기업 전략과 의결권 →",20,y,360,42,()=>OpenGovernance(ownershipStock),false); y+=57;
        if(!stock.Active)
        {
            y=Statement("합병 완료 회사",y,[("현재 발행 주식",Money(structure.IssuedShares)+"주"),("현재 시가총액","상장 종료")]);
            Text("주식 교환 이후 지분은 합병 대상 회사에서 확인하세요.",21,y+14,10,Muted); y+=35;
        }
        y=Pie("발행 주식 기준 지분 구성",structure.Slices.Select(s=>(s.Name,(double)s.Shares)).ToArray(),y);
        Text("원형표: 자사주 포함 · 표의 상장 비율: 자사주 제외",21,y+10,10,Muted); y+=31;
        y=Statement("주식 수와 기업 가치",y,[("발행 주식",Money(structure.IssuedShares)+"주"),
            ("상장 주식 (발행 − 자사주)",Money(structure.OutstandingShares)+"주"),
            ("유동 주식 (상장 − 창업자)",Money(structure.FloatShares)+"주"),("시가총액",ShortMoney(stock.MarketCap)+"원")]);
        Text("보유 유형별 지분",21,y+18,18,Ink,true); y+=36;
        for(int i=0;i<structure.Slices.Count;i++)
        {
            var slice=structure.Slices[i];
            if(Visible(y,75))
            {
                Box(20,y,360,75,Card,12); Circle(37,y+23,4,PaletteColors[i]);
                Text(slice.Name,49,y+28,13,Ink,true); Text(Money(slice.Shares)+"주",363,y+28,14,Ink,true,Paint.Align.Right);
                string holders=slice.Category==HolderCategory.Treasury ? "회사가 보유 · 상장/배당 대상 제외" : $"보유 {Money(slice.Holders)}"+(slice.Category is HolderCategory.Institutions or HolderCategory.Retail ? "명" : "주체");
                Text(holders,35,y+54,10,Muted);
                string ratio=slice.Category==HolderCategory.Treasury ? "상장 비율 —" : structure.OutstandingShares>0 ? $"상장 {structure.OutstandingRatio(slice.Shares):P2}" : "상장 비율 —";
                Text(ratio,363,y+54,11,Lime,true,Paint.Align.Right);
            }
            y+=86;
        }
        y=Statement("은행 대여와 공매도 반환 의무",y,[("대여 중인 주식",Money(structure.BorrowedShares)+"주"),
            ("매도용 대여 예약",Money(structure.ReservedLending)+"주")]);
        Text("대여 주식은 현재 소유자의 지분에 포함합니다.",21,y+10,10,Muted);
        Text("은행 반환 청구권은 지분 합계에 다시 더하지 않습니다.",21,y+27,10,Muted); y+=50;
        int pages=Math.Max(1,(structure.Institutions.Count+OwnersPerPage-1)/OwnersPerPage);
        ownershipPage=Math.Clamp(ownershipPage,0,pages-1);
        Text("기관 주요 주주",21,y+20,18,Ink,true); y+=39;
        Button("← 이전",20,y,100,36,()=>ownershipPage--,false,ownershipPage>0);
        Text($"{ownershipPage+1}/{pages} · {structure.Institutions.Count}개 기관",200,y+23,11,Muted,false,Paint.Align.Center);
        Button("다음 →",280,y,100,36,()=>ownershipPage++,false,ownershipPage+1<pages); y+=51;
        foreach(var holder in structure.Institutions.Skip(ownershipPage*OwnersPerPage).Take(OwnersPerPage))
        {
            if(Visible(y,77))
            {
                Box(20,y,360,77,Card,13); Robot(holder.TraderId,32,y+13,48);
                TextFit(holder.Name,92,y+28,12,Ink,173,true); Text(Representatives.Name(S.Bots[holder.TraderId-1].PortraitId),92,y+50,10,Muted);
                Text(Money(holder.Shares)+"주",363,y+28,13,Ink,true,Paint.Align.Right);
                Text(structure.OutstandingRatio(holder.Shares).ToString("P2"),363,y+52,12,Lime,true,Paint.Align.Right);
                Hit(20,y,360,77,()=>selectedTrader=holder.TraderId);
            }
            y+=88;
        }
        if(structure.Institutions.Count==0) { Text("현재 이 회사의 주식을 보유한 기관이 없습니다.",21,y+15,11,Muted); y+=42; }
        Button("이 회사의 재무제표 →",20,y,360,42,()=>{ companyStock=ownershipStock; companySeason=companyTab=0; SetPage(6); },false);
        Button("시장으로 돌아가기",20,y+54,360,42,()=>SetPage(0),false); return y+114;
    }
}
