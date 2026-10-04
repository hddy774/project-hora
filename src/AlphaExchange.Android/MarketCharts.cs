using Android.Graphics;
using AlphaExchange.Core;
using APath = Android.Graphics.Path;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    float TimeGraph(string title,IEnumerable<(long Hour,double Value,bool Gap)> values,float y,string caption)
    {
        var points = values.Where(p=>double.IsFinite(p.Value)).OrderBy(p=>p.Hour).ToArray();
        Box(20,y,360,160,Card,17); Text(title,36,y+27,14,Ink,true);
        if (points.Length > 0)
        {
            double low = points.Min(p=>p.Value), high=points.Max(p=>p.Value), span=Math.Max(1,high-low), padding=span*.1;
            low-=padding; high+=padding; long first=points[0].Hour, last=points[^1].Hour;
            using var path = new APath();
            for (int i=0;i<points.Length;i++)
            {
                var point=points[i]; float x=36+328f*(point.Hour-first)/Math.Max(1,last-first);
                float py=y+118-(float)((point.Value-low)/(high-low))*70;
                if (i==0 || point.Gap) path.MoveTo(x,py); else path.LineTo(x,py);
                if (i==points.Length-1) Circle(x,py,3,Teal);
            }
            paint.SetShader(null); paint.Color=Teal; paint.StrokeWidth=2; paint.SetStyle(Paint.Style.Stroke); c.DrawPath(path,paint); paint.SetStyle(Paint.Style.Fill);
            Text(ShortMoney((long)high),36,y+44,9,Muted);
            Text($"{Moment(first)} → {Moment(last)}",36,y+136,9,Muted);
        }
        TextFit(caption,36,y+151,9,Muted,328); return y+176;
    }
    float TimeGraph(string title,IEnumerable<DailySnapshot> points,Func<DailySnapshot,double> metric,float y,string caption)
        => TimeGraph(title,points.Select(p=>(p.Hour,metric(p),p.GapBefore)),y,caption);
    long corporateBefore=long.MaxValue;
    bool corporateTab;
    readonly Stack<long> corporatePages=[];
    static string EventName(CorporateEventKind kind) => kind switch
    { CorporateEventKind.Dividend=>"배당",CorporateEventKind.Split=>"액면분할",CorporateEventKind.ReverseSplit=>"주식 병합",CorporateEventKind.Merger=>"기업 합병",CorporateEventKind.Ipo=>"신규 상장",CorporateEventKind.Issue=>"유상증자",CorporateEventKind.Buyback=>"자사주 매입",_=>"자사주 소각" };
    float CorporateCards(float y,IEnumerable<CorporateEvent> events)
    {
        foreach (var e in events)
        {
            Box(20,y,360,112,Card,14); Pill(EventName(e.Kind),35,y+14,Teal,80);
            Text(Moment(e.Hour),363,y+31,10,Muted,false,Paint.Align.Right);
            TextFit(S.Stocks.FirstOrDefault(s=>s.SecurityId==e.SecurityId)?.Name ?? e.SecurityId,36,y+60,14,Ink,328,true);
            Wrap(e.Detail,36,y+82,326,10,Muted,17); y+=124;
        }
        return y;
    }
    float DrawCorporateEvents(float y)
    {
        Text("주식 수와 주주의 권리",21,y+15,20,Ink,true); y+=43;
        var records=store.ReadEvents(S,beforeId:corporateBefore);
        Button("← 최근",20,y,100,36,()=>{ corporateBefore=corporatePages.Pop(); scroll=0; },false,corporatePages.Count>0);
        Text("기업행동 기록",200,y+23,12,Muted,false,Paint.Align.Center);
        Button("과거 →",280,y,100,36,()=>{ corporatePages.Push(corporateBefore); corporateBefore=records[^1].Id; scroll=0; },false,records.Count==60); y+=52;
        if(records.Count==0) { Text("배당·분할·상장 등의 사건이 발생하면 기록합니다.",21,y+20,11,Muted); return y+70; }
        return CorporateCards(y,records)+15;
    }
}
