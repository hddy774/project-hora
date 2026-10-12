using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    int portraitZoom = -1;
    float DrawInvestorPodium(float y)
    {
        var top = FrameRanking(rankingMetric, comparisonPeriod).Take(3).ToArray();
        foreach (var row in top)
        {
            float x = 20 + (row.Rank - 1) * 123; var p = row.Trader;
            Box(x, y, 114, 154, Card2, 15); Portrait(p.PortraitId, x + 8, y + 8, 98, 94);
            Text($"{row.Rank}위 · {Representatives.Name(p.PortraitId)}", x + 57, y + 123, 11, Lime, true, Paint.Align.Center);
            string value = rankingMetric == RankingMetric.Return ? Percent(row.Value) : ShortMoney((long)row.Value);
            Text(value, x + 57, y + 143, 10, Ink, false, Paint.Align.Center);
            int id = p.Id; Hit(x, y, 114, 154, () => selectedTrader = id);
        }
        return y + 170;
    }
    void DrawPortraitZoom()
    {
        int id = portraitZoom; float top = Modal(h - 16, () => portraitZoom = -1);
        Text(Representatives.Name(id) + " · 일러스트 확대", 27, top + 39, 17, Ink, true);
        float available = h - top - 96;
        var region = portraitRegions[id - 1]; float ratio = (float)region.width / region.height;
        float width = Math.Min(360, available * ratio), height = width / ratio;
        Portrait(id, (400 - width) / 2, top + 64, width, height);
    }
}
