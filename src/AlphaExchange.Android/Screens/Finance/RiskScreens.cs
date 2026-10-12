using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawPortfolioRisk(Trader trader, float y)
    {
        var risk = FinancialAnalysis.Portfolio(trader, S.Stocks);
        y = Statement("포트폴리오 위험 보고서", y,
        [
            ("현금 비율", $"{risk.CashRatio:P2}"), ("총 노출 / 순자산", $"{risk.GrossExposure:P2}"),
            ("순 노출 / 순자산", $"{risk.NetExposure:P2}"), ("최대 종목 비중", $"{risk.LargestStockWeight:P2}"),
            ("최대 업종 비중", $"{risk.LargestSectorWeight:P2}"), ("종목 집중도 HHI", $"{risk.Concentration:0.000}"),
            ("관측 최대 낙폭", $"{risk.MaximumDrawdown:P2}")
        ]);
        y = MoneyStatement("가격 충격 보고서 · 금액을 눌러 상세 확인", y,
        [
            ("현재 순자산", trader.Equity(S.Stocks)),
            ("전 종목 10% 하락 시", risk.EquityAfterTenPercentFall),
            ("전 종목 10% 상승 시", risk.EquityAfterTenPercentRise)
        ]);
        return Wrap("현재 보유 주식과 공매도의 단순 가격 충격입니다. 추가 거래·세금·유동성 변화는 포함하지 않습니다.", 21, y + 6, 354, 11, Muted, 20) + 18;
    }

    float DrawMicrostructure(float y)
    {
        var flow = S.Activity;
        int twoSided = 0; double spread = 0; long bids = 0, asks = 0;
        for (int i = 0; i < S.Stocks.Count; i++)
        {
            var buy = game!.Orders(i, true); var sell = game.Orders(i, false);
            bids += buy.Sum(o => (long)o.Remaining); asks += sell.Sum(o => (long)o.Remaining);
            if (buy.Count > 0 && sell.Count > 0) { twoSided++; spread += (sell[0].Price - buy[0].Price) / (double)S.Stocks[i].Price; }
        }
        y = Statement("호가·체결 통계", y,
        [
            ("양방향 호가 종목", $"{twoSided} / 15"),
            ("평균 스프레드", twoSided == 0 ? "양방향 호가 없음" : $"{spread / twoSided:P2}"),
            ("매수 / 매도 대기 수량", $"{KoreanNumber.Compact(bids)} / {KoreanNumber.Compact(asks)}주"),
            ("호가 수량 불균형", $"{(bids - asks) / (double)Math.Max(1, bids + asks):P1}"),
            ("누적 접수 / 취소", $"{flow.SubmittedOrders:N0} / {flow.CancelledOrders:N0}"),
            ("상대 호가 진입 주문", $"{flow.AggressiveOrders:N0}"),
            ("기관 간 / 개미 간 체결", $"{flow.InstitutionMatches:N0} / {flow.RetailMatches:N0}"),
            ("기관·개미 간 체결", $"{flow.CrossGroupMatches:N0}")
        ]);
        y = MoneyStatement("기관·개미 간 실제 매매 자금", y,
        [
            ("누적 거래대금", flow.CrossGroupTurnover),
            ("개미 → 기관", flow.CashToInstitutions), ("기관 → 개미", flow.CashToRetail),
            ("기관 순현금 유입", flow.CashToInstitutions - flow.CashToRetail)
        ]);
        return Wrap("자금 이동은 매매 대금이며 투자 이익과 다릅니다. 손익은 보유 주식 평가와 세금·비용까지 반영한 재무제표에서 확인합니다.", 21, y + 8, 354, 11, Muted, 20) + 18;
    }

    float DrawMarketRisk(float y)
    {
        var institutions = S.Bots;
        y = Statement("시장 위험·전략 통계", y,
        [
            ("수익 중인 기관", $"{institutions.Count(t => t.ReturnIndex > 1)} / 100"),
            ("총 계획 재평가", $"{institutions.Sum(t => t.Development!.PortfolioReviews):N0}회"),
            ("현재 공매도 기관", $"{institutions.Count(t => t.ShortShares.Any(q => q > 0))}곳"),
            ("누적 담보 부족", $"{institutions.Sum(t => t.MarginCalls):N0}회"),
            ("평균 최대 낙폭", $"{institutions.Average(t => t.Development!.MaximumDrawdown):P2}"),
            ("공개 랜덤 이벤트", $"{S.Environment.EventsPublished:N0}건")
        ]);
        y = Statement("기관 전략 단계 분포", y, Enum.GetValues<CampaignPhase>()
            .Select(phase => (GameEngine.CampaignNames[(int)phase], $"{institutions.Count(t => t.Development!.CampaignPhase == phase)}곳")).ToArray());
        return DrawPortfolioRisk(Focus, y);
    }
}
