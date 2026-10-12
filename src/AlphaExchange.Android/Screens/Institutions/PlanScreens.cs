using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawConfidentialPlans(float y, Trader trader)
    {
        Text("개인별 포트폴리오 계획", 21, y + 20, 21, Ink, true);
        y = Wrap("총 투자 예산 안에서 종목·업종 비중을 함께 배분합니다. 손절·익절·보유 기한·예상 거래비용을 확인하며, 다른 기관의 계획은 AI 판단에 공개되지 않습니다.", 21, y + 48, 352, 11, Muted, 20) + 18;
        var plans = game!.ConfidentialPlans(S.FollowedId, trader.Id);
        if (plans.Count == 0) { Text("첫 투자 판단 이후 계획을 표시합니다.", 21, y + 20, 12, Muted); return y + 58; }
        y = Statement("전체 계획", y,
        [
            ("종목 목표 합계", $"{plans.Sum(p => p.TargetWeight):P1}"),
            ("현금 목표", $"{trader.Development!.TargetCashRatio:P1}"),
            ("계획 변경 횟수", $"{trader.Development.PlanRevision:N0}")
        ]);
        foreach (var plan in plans)
        {
            int index = game.SecurityIndex(plan.SecurityId);
            if (index < 0) continue;
            var stock = S.Stocks[index];
            if (Visible(y, 222))
            {
                Box(20, y, 360, 222, Card2, 15);
                Text(stock.Name, 36, y + 28, 15, Ink, true);
                Text($"목표 {plan.TargetWeight:P1} · 상한 {plan.MaximumWeight:P0}", 36, y + 54, 12, Lime, true);
                double actual = stock.Value(trader.Shares[index]) / (double)Math.Max(1, trader.GrossAssets(S.Stocks));
                Text($"현재 {actual:P1} · {game.Rules.Horizons[(int)plan.Horizon].Name} · {Math.Max(0, plan.TargetMinute - S.CompletedMinutes) / 60.0:0.#}시간 남음", 36, y + 78, 11, Muted);
                Text($"예상 신호 {plan.ExpectedReturn:P1} · 확신 {plan.Confidence:P0} · 비용 {plan.EstimatedCost:P2}", 36, y + 102, 10, Teal);
                Text($"손절 {Money(plan.StopPrice)} · 익절 {Money(plan.TakeProfitPrice)}원", 36, y + 128, 12, Ink);
                Text($"손실 예산 {ShortMoney(plan.LossBudget)}원", 36, y + 152, 11, Muted);
                TextFit(plan.Thesis, 36, y + 177, 10, Muted, 328);
                Text($"다음 재검토 {Math.Max(0, plan.ReviewMinute - S.CompletedMinutes)}분 후 · 눌러서 호가", 36, y + 204, 10, Lime);
                Hit(20, y, 360, 222, () => selectedStock = index);
            }
            y += 234;
        }
        foreach (var report in game.ConfidentialReports(S.FollowedId, trader.Id))
        {
            y = Statement(report.Sector + " 분석", y,
                [("예상 방향", Percent(report.ExpectedReturn)), ("확신", $"{report.Confidence:P0}"), ("작성", Moment(report.WrittenMinute / 60))]);
        }
        return y + 10;
    }
}
