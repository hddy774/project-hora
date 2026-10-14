using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float HorizontalBars(string title, (string Label, double Value)[] rows, float y, string unit, bool percent = false)
    {
        float height = 64 + rows.Length * 39;
        if (!Visible(y, height)) return y + height + 16;
        Box(20, y, 360, height, Card, 17);
        Text(title, 36, y + 28, 14, Ink, true);
        double low = Math.Min(0, rows.Select(r => r.Value).DefaultIfEmpty().Min());
        double high = Math.Max(0, rows.Select(r => r.Value).DefaultIfEmpty().Max());
        double span = Math.Max(1e-9, high - low);
        float zero = 36 + (float)(-low / span) * 328;
        for (int i = 0; i < rows.Length; i++)
        {
            float top = y + 49 + i * 39;
            TextFit(rows[i].Label, 36, top, 10, Muted, 170);
            Text(percent ? $"{rows[i].Value:P1}" : ShortMoney((long)rows[i].Value) + unit,
                364, top, 10, Ink, true, Paint.Align.Right);
            Box(36, top + 9, 328, 8, Card2, 3);
            float end = 36 + (float)((rows[i].Value - low) / span) * 328;
            if (rows[i].Value != 0) Box(Math.Min(zero, end), top + 9, Math.Max(1, Math.Abs(end - zero)), 8,
                rows[i].Value < 0 ? Red : Teal, 3);
            if (low < 0) Line(zero, top + 6, zero, top + 20, Muted);
        }
        Text("같은 축 기준 · " + (percent ? "%" : unit), 36, y + height - 12, 9, Muted);
        return y + height + 16;
    }

    float StatisticsTable(string title, string[] headings, string[][] rows, float y)
    {
        float height = 77 + rows.Length * 34;
        if (!Visible(y, height)) return y + height + 16;
        Box(20, y, 360, height, Card, 17);
        Text(title, 36, y + 28, 14, Ink, true);
        float width = 328f / headings.Length;
        for (int column = 0; column < headings.Length; column++)
            TextFit(headings[column], 36 + column * width, y + 55, 10, Teal, width - 6, true);
        for (int row = 0; row < rows.Length; row++)
        {
            float top = y + 78 + row * 34;
            Line(36, top - 14, 364, top - 14, Stroke);
            for (int column = 0; column < headings.Length; column++)
                TextFit(rows[row][column], 36 + column * width, top + 7, 10, Ink, width - 6);
        }
        return y + height + 16;
    }

    float DrawInvestmentResults(float y)
    {
        var stats = game!.Statistics();
        Metric("성공한 투자", $"{stats.SuccessfulInvestments:N0}회", 20, y, 174);
        Metric("완료한 투자", $"{stats.CompletedInvestments:N0}회", 206, y, 174); y += 84;
        y = Pie("전량 정리한 투자의 결과", [("순매매이익", stats.SuccessfulInvestments),
            ("손실·손익 없음", stats.CompletedInvestments - stats.SuccessfulInvestments)], y);
        string[] cashLabels = ["현금 0~10%", "10~20%", "20~30%", "30~40%", "40% 이상"];
        y = HorizontalBars("기관 현금 비중 분포", Enumerable.Range(0, 5).Select(i => (cashLabels[i], (double)stats.CashDistribution[i])).ToArray(), y, "곳");
        y = Statement("주식 50% 이상 · 현금 최대 50%", y,
            [("성향별 현금 목표", "5~30%"), ("현재 비중 회복 주문 대상", $"{stats.AllocationRecoveryCount}곳")]);
        y = StatisticsTable("투자 성공 상위 10명 · 현 세대 누적", ["투자자", "성공 / 완료", "성공률"],
            S.Bots.OrderByDescending(t => t.Development!.SuccessfulInvestments).ThenBy(t => t.Id).Take(10).Select(t =>
            {
                var d = t.Development!;
                return new[] { Representatives.Name(t.PortraitId), $"{d.SuccessfulInvestments} / {d.CompletedInvestments}",
                    d.CompletedInvestments == 0 ? "집계 전" : $"{d.SuccessfulInvestments / (double)d.CompletedInvestments:P1}" };
            }).ToArray(), y);
        return Wrap("한 종목의 매수부터 전량 매도(공매도는 전량 상환)까지를 1회로 셉니다. 매수·매도 수수료와 해당 거래의 세금을 뺀 매매손익이 양수면 성공입니다. 부분 체결은 합산합니다. 배당·금융 비용과 기업행동 정산은 별도 재무 항목입니다. 이전 저장의 보유분은 완료 집계에서 제외하며 새로 시작한 투자부터 기록합니다.",
            21, y + 8, 354, 11, Muted, 20) + 20;
    }
}
