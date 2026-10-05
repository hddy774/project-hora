using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    float DrawConfidentialPlans(float y,Trader t)
    {
        Text("관찰 중인 투자사의 소유자 기록",21,y+17,18,Ink,true);
        y=Wrap("대표가 수석 애널리스트로 분석을 이끌고 직원이 보조합니다. 업종 보고서와 종목 계획은 다른 투자사 AI가 읽을 수 없으며 공개 뉴스에도 노출되지 않습니다.",21,y+42,352,11,Muted,19)+18;
        var reports=game!.ConfidentialReports(S.FollowedId,t.Id);
        if(reports.Count==0) { Text("첫 분 단위 투자 판단 후 기밀 보고서가 작성됩니다.",21,y+17,11,Muted); return y+50; }
        foreach(var r in reports)
        {
            Box(20,y,360,112,Card,15); Text(r.Sector+" 업종 보고서",36,y+27,15,Ink,true);
            Text($"{(r.ExpectedReturn>=0 ? "상승" : "하락")} {Percent(r.ExpectedReturn)} · 신뢰 {r.Confidence:P0}",36,y+53,12,Direction(r.ExpectedReturn),true);
            Text($"업종 직원 {r.SupportingAnalysts}명 · {Moment(r.WrittenMinute/60)}",36,y+77,10,Muted);
            TextFit(r.Thesis,36,y+99,10,Muted,328); y+=124;
        }
        Text("종목별 포트폴리오 계획",21,y+17,19,Ink,true); y+=40;
        string[] styles=["방어·안전","균형","성장·장기","기회·위험"];
        foreach(var p in game.ConfidentialPlans(S.FollowedId,t.Id))
        {
            var stock=S.Stocks.FirstOrDefault(s=>s.SecurityId==p.SecurityId); if(stock is null) continue;
            if(Visible(y,143))
            {
                Box(20,y,360,143,Card2,15); Text(stock.Name,36,y+27,15,Ink,true);
                Text(styles[(int)p.Style]+" · "+game.Rules.Horizons[(int)p.Horizon].Name,36,y+51,11,Teal);
                Text($"기간 {p.DurationMinutes/1440.0:0.##}일 · 종목 목표 상한 {p.TargetWeight:P0}",36,y+75,11,Muted);
                Text($"가격 범위 {Money(p.LowerPrice)} ~ {Money(p.UpperPrice)}원",36,y+99,12,Lime,true);
                Text("범위 이탈·위험·공시 변화 시 계획 재평가",36,y+124,10,Muted);
                int index=S.Stocks.IndexOf(stock); Hit(20,y,360,143,()=>selectedStock=index);
            }
            y+=155;
        }
        return y+10;
    }
}
