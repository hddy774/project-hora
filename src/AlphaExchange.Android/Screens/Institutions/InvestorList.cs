using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawInvestors(float y)
    {
        Text("100명의 투자자", 20, y + 23, 24, Ink, true);
        Text("기관별 포트폴리오와 시장 전략을 관찰합니다", 21, y + 47, 11, Muted);
        y += 65;
        y = NumberFormatPicker(y);
        foreach (var trader in S.Bots)
        {
            if (Visible(y, 96))
            {
                Box(20, y, 360, 96, Card, 15);
                Portrait(trader.PortraitId, 30, y + 10, 60, 76);
                Text(Representatives.Name(trader.PortraitId), 103, y + 29, 16, Ink, true);
                Text($"투자 성공 {trader.Development!.SuccessfulInvestments:N0}회 / 완료 {trader.Development.CompletedInvestments:N0}회", 103, y + 51, 11, Muted);
                Text($"순자산 {ShortMoney(trader.Equity(S.Stocks))}원", 103, y + 75, 11, Teal);
                Text(Percent(trader.Return(S.Stocks)), 364, y + 28, 10, Direction(trader.Return(S.Stocks)), false, Paint.Align.Right);
                int id = trader.Id;
                Hit(20, y, 360, 96, () => selectedTrader = id);
            }
            y += 108;
        }
        return y + 10;
    }

    float DrawInvestorStrategy(float y, Trader trader)
    {
        var d = trader.Development!;
        string symbol = d.CampaignIndex < 0 ? "선정 중" : S.Stocks[d.CampaignIndex].Name;
        y = Statement("기관의 시장 전략", y,
        [
            ("관찰 대상", symbol), ("현재 단계", GameEngine.CampaignNames[(int)d.CampaignPhase]),
            ("개인 투자 성향", GameEngine.StrategyNames[(int)trader.Strategy]),
            ("포트폴리오 재평가", $"{d.PortfolioReviews:N0}회"), ("적극적 주문", $"{d.AggressiveOrders:N0}회"),
            ("최대 낙폭", $"{d.MaximumDrawdown:P2}"), ("다음 판단", $"{Math.Max(0, d.NextExecutionMinute - S.CompletedMinutes)}분 후")
        ]);
        y = Wrap("기관은 개미의 추세 추종과 공개 뉴스 반응을 분석해 매집하고, 호가 경쟁으로 추격 매수를 유도한 뒤 보유분을 나눠 매도합니다. 다른 기관과의 경쟁, 거래비용과 손실 위험에 따라 계획이 실패할 수 있습니다.", 21, y + 8, 354, 12, Muted, 21) + 20;
        y = Wrap(trader.Decision, 21, y, 354, 12, Teal, 21) + 15;
        y = DrawPortfolioRisk(trader, y);
        Button("종목별 투자 계획 →", 20, y, 360, 42, () => { institutionTab = 4; scroll = 0; }, false);
        return y + 57;
    }

    float DrawOperations(float y)
    {
        Text("기관들의 시장 전략", 21, y + 18, 22, Ink, true);
        y = Wrap("투자자별 매집·추격 유도·분산 매도 단계입니다. 실제 자금과 주식으로 주문하며, 전략만으로 가격이나 이익이 확정되지 않습니다.", 21, y + 46, 354, 11, Muted, 20) + 18;
        foreach (var trader in S.Bots)
        {
            var d = trader.Development!;
            if (d.CampaignIndex < 0) continue;
            if (Visible(y, 98))
            {
                Box(20, y, 360, 98, Card, 15);
                Text(trader.Name, 36, y + 27, 15, Ink, true);
                Text(GameEngine.CampaignNames[(int)d.CampaignPhase], 36, y + 52, 12, Lime);
                Text($"{S.Stocks[d.CampaignIndex].Symbol} · 재평가 {d.PortfolioReviews:N0}회 · 현금 {game!.CashRatio(trader):P0}", 36, y + 78, 10, Muted);
                int id = trader.Id;
                Hit(20, y, 360, 98, () => selectedTrader = id);
            }
            y += 110;
        }
        return y + 10;
    }
}
