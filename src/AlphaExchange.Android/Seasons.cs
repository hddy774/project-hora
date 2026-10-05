using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    readonly Dictionary<long, SeasonResult?> seasonCache = [];
    SeasonResult? Season(long number)
    {
        if (seasonCache.TryGetValue(number, out var cached)) return cached;
        try
        {
            var result = store.ReadSeason(S, number);
            if (seasonCache.Count > 20) seasonCache.Clear();
            seasonCache[number] = result; return result;
        }
        catch { Notify("시즌 기록을 읽을 수 없습니다. 저장 공간을 확인하세요."); return null; }
    }
    float DrawPersonalHistory(float y, Trader t)
    {
        Text("시즌마다 쌓이는 성적", 21, y + 18, 19, Ink, true);
        Text("각 시즌 시작 자산 대비 수익률 순위", 21, y + 41, 11, Muted); y += 59;
        long completed = S.Season - 1;
        if (completed == 0)
        { Text("첫 30일 시즌이 끝나면 성적이 기록됩니다.", 22, y + 22, 12, Muted); return y + 65; }
        long maxPage = (completed - 1) / 10;
        historyPage = Math.Clamp(historyPage, 0, maxPage);
        Button("← 최근", 20, y, 108, 37, () => { historyPage--; scroll = 0; }, false, historyPage > 0);
        Text($"{historyPage + 1} / {maxPage + 1}", 200, y + 24, 12, Ink, true, Paint.Align.Center);
        Button("이전 →", 272, y, 108, 37, () => { historyPage++; scroll = 0; }, false, historyPage < maxPage); y += 55;
        for (long number = completed - historyPage * 10; number > Math.Max(0, completed - (historyPage + 1) * 10); number--)
        {
            var r = Season(number)?.Standings.FirstOrDefault(x => x.TraderId == t.Id);
            Box(20, y, 360, 82, Card, 14);
            Text($"시즌 {number}"+(r is null ? "" : $" · {r.Generation}기"), 36, y + 28, 14, Ink, true);
            Text(r is null ? "기록을 읽을 수 없음" : $"최종 자산 ₩{Money(r.Equity)}", 36, y + 58, 11, Muted);
            if (r is not null)
            {
                Text($"{r.Rank}위 / 100", 364, y + 29, 17, r.Rank <= 3 ? Lime : Ink, true, Paint.Align.Right);
                Text(Percent(r.Return), 364, y + 58, 12, Direction(r.Return), true, Paint.Align.Right);
                long target = number; Hit(20, y, 360, 82, () => { selectedSeason = target; SetPage(4); });
            }
            y += 93;
        }
        return y + 10;
    }
    float DrawSeasons(float y)
    {
        Text("끝없이 이어지는 리그", 20, y + 23, 24, Ink, true);
        Text($"시즌 {S.Season} 진행 중 · 30일마다 자동 갱신", 21, y + 47, 12, Muted); y += 66;
        Box(20, y, 360, 76, Card2, 15);
        Text("시장과 자산은 다음 시즌으로 이어집니다.", 36, y + 28, 13, Ink, true);
        Text("성적은 배당을 포함하고 임금·소비를 제외합니다.", 36, y + 51, 11, Muted); y += 94;
        if (S.Season <= 1)
        {
            Box(20, y, 360, 130, Card, 18);
            Text("첫 번째 역사를 쓰는 중", 200, y + 49, 19, Ink, true, Paint.Align.Center);
            Text($"DAY {S.Day} / 30 · 종료 후 100개 순위를 보관합니다.", 200, y + 82, 11, Muted, false, Paint.Align.Center);
            Button("현재 순위 보기", 20, y + 148, 360, 46, () => SetPage(2), false); return y + 215;
        }
        selectedSeason = Math.Clamp(selectedSeason == 0 ? S.Season - 1 : selectedSeason, 1, S.Season - 1);
        Button("← 이전", 20, y, 92, 39, () => { selectedSeason--; scroll = 0; }, false, selectedSeason > 1);
        Text($"시즌 {selectedSeason}", 200, y + 26, 17, Lime, true, Paint.Align.Center);
        Button("다음 →", 288, y, 92, 39, () => { selectedSeason++; scroll = 0; }, false, selectedSeason < S.Season - 1); y += 54;
        var result = Season(selectedSeason);
        if (result is null) { Text("기록을 읽을 수 없습니다.", 22, y + 24, 13, Muted); return y + 70; }
        if (result.Start is not null && result.End is not null)
        {
            y = Statement("시즌 시작 → 종료", y, [("시가총액", ShortMoney(result.Start.Capitalization) + " → " + ShortMoney(result.End.Capitalization)),
                ("거래량", Money(result.End.Volume - result.Start.Volume) + "주"), ("거래금액", ShortMoney(result.End.Turnover - result.Start.Turnover) + "원"),
                ("기관 순자산", ShortMoney(result.Start.InstitutionEquity) + " → " + ShortMoney(result.End.InstitutionEquity))]);
            var fullSeason = game!.HistorySource?.Range(result.Start.Hour,result.End.Hour) ?? result.Days;
            y = TimeGraph("시즌 중 시장 추이", fullSeason,d=>d.Capitalization,y,$"시즌 {selectedSeason} · 시가총액 · 원");
            y = Pie("시즌 종료 순자산 구성", [("기관", result.End.InstitutionEquity), ("개인", result.End.RetailEquity)], y);
            Button("이 시즌의 기업 결산  →", 20, y, 360, 40, () => { companySeason = selectedSeason; companyStock = companyTab = 0; SetPage(6); }, false); y += 56;
        }
        var winner = result.Standings[0];
        int PastPortrait(int id,int generation)=>(id-1+(generation-1)*17)%GameEngine.AiCount+1;
        Box(20, y, 360, 150, Card, 18, Lime); Portrait(PastPortrait(winner.TraderId,winner.Generation), 30, y + 10, 82, 130);
        Text("SEASON CHAMPION", 132, y + 27, 10, Lime, true);
        Text(Representatives.Name(PastPortrait(winner.TraderId,winner.Generation)), 132, y + 59, 23, Ink, true);
        TextFit(winner.InstitutionName.Length>0 ? winner.InstitutionName : $"기관 {winner.TraderId:000} · {winner.Generation}기",132,y+82,12,Muted,228);
        Text(Percent(winner.Return), 132, y + 112, 23, Direction(winner.Return), true);
        Text($"시즌 매칭 {Money(result.Matches)}건", 363, y + 136, 10, Muted, false, Paint.Align.Right); y += 168;
        Text("최종 순위 · 기관 100개", 22, y + 12, 14, Ink, true); y += 28;
        foreach (var row in result.Standings)
        {
            if (y + 70 >= clipTop && y <= clipBottom)
            {
                Box(20, y, 360, 68, Card, 13);
                Text($"{row.Rank:00}", 34, y + 40, 12, row.Rank <= 3 ? Lime : Muted, true);
                Portrait(PastPortrait(row.TraderId,row.Generation), 62, y + 11, 45, 45);
                Text(Representatives.Name(PastPortrait(row.TraderId,row.Generation)), 121, y + 27, 13, Ink, true);
                TextFit(row.InstitutionName.Length>0 ? row.InstitutionName : $"기관 {row.TraderId:000} · {row.Generation}기",121,y+47,10,Muted,149);
                Text(Percent(row.Return), 364, y + 28, 13, Direction(row.Return), true, Paint.Align.Right);
                Text($"₩{ShortMoney(row.Equity)}", 364, y + 48, 11, Muted, false, Paint.Align.Right);
                int id = row.TraderId; Hit(20, y, 360, 68, () => { S.FollowedId = id; portfolioTab = 2; historyPage = 0; SetPage(1); });
            }
            y += 77;
        }
        return y + 15;
    }
}
