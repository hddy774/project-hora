using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawFinancials(FinancialStatement f, float y, int divisor)
    {
        long Amount(double value) => (long)Math.Round(value / Math.Max(1, divisor));
        y = Wrap("금액 항목을 누르면 만·억·조와 정확한 숫자를 함께 볼 수 있습니다." + (divisor > 1 ? " 평균 금액은 원 단위로 반올림합니다." : ""), 21, y + 8, 354, 11, Muted, 20) + 14;
        y = MoneyStatement("재무상태표", y,
        [
            ("현금 및 예금", Amount(f.Cash)), ("주식 평가액", Amount(f.Holdings)), ("자산 총계", Amount(f.Assets)),
            ("공매도 부채", Amount(f.ShortDebt)), ("대출 잔액", Amount(f.LoanDebt)), ("미납 세금", Amount(f.TaxDebt)),
            ("미납 벌금", Amount(f.FineDebt)), ("공매도 미납 배당", Amount(f.ShortDividendDebt)),
            ("부채 총계", Amount(f.Liabilities)), ("자본 총계", Amount(f.Equity))
        ]);
        y = MoneyStatement("손익계산서", y,
        [
            ("실현 매매손익", Amount(f.RealizedProfit)), ("평가손익 변동", Amount(f.ValuationChange)),
            ("거래 수수료", -Amount(f.Fees)), ("세금", -Amount(f.Taxes)), ("이자", -Amount(f.InterestExpense)),
            ("공매도 대여료", -Amount(f.BorrowFees)), ("벌금", -Amount(f.Fines)), ("주식 보조금", Amount(f.Subsidies)),
            ("배당 수입", Amount(f.DividendIncome)), ("배당세", -Amount(f.DividendTax)),
            ("공매도 배당 비용", -Amount(f.ShortDividendExpense)), ("임금", Amount(f.WageIncome)),
            ("소비", -Amount(f.Consumption)), ("채무 면제", Amount(f.DebtRelief)), ("누적 순손익", Amount(f.NetIncome))
        ]);
        y = MoneyStatement("현금흐름표", y,
        [
            ("기초 현금", Amount(f.OpeningCash)), ("주식 매도 유입", Amount(f.Sales)), ("주식 매수 유출", -Amount(f.Purchases)),
            ("수수료 유출", -Amount(f.Fees)), ("차입", Amount(f.Borrowed)), ("상환", -Amount(f.Repaid)),
            ("세금 납부 · 환급 차감", -Amount(f.TaxesPaid)), ("이자·대여료·벌금 납부", -Amount(f.InterestPaid + f.BorrowFees + f.FinesPaid)),
            ("보조금", Amount(f.Subsidies)), ("배당 유입 · 세후", Amount(f.DividendIncome - f.DividendTax)),
            ("공매도 배당 납부", -Amount(f.ShortDividendPaid)), ("임금·소비 순유입", Amount(f.WageIncome - f.Consumption)),
            ("순현금흐름", Amount(f.NetCashFlow)), ("기말 현금", Amount(f.Cash))
        ]);
        y = MoneyStatement("자본변동표", y,
        [
            ("기초 순자산", Amount(f.OpeningEquity)),
            ("투자 손익 · 생활 흐름 제외", Amount(f.NetIncome - f.WageIncome + f.Consumption)),
            ("임금·소비 순유입", Amount(f.WageIncome - f.Consumption)),
            ("기말 순자산", Amount(f.Equity))
        ]);
        y = MoneyStatement("수익 기여 분석", y,
        [
            ("매매·평가 기여", Amount(f.RealizedProfit + f.ValuationChange)),
            ("배당 순기여", Amount(f.DividendIncome - f.DividendTax - f.ShortDividendExpense)),
            ("금융·거래 비용", -Amount(f.Fees + f.Taxes + f.InterestExpense + f.BorrowFees + f.Fines)),
            ("보조금·채무조정", Amount(f.Subsidies + f.DebtRelief)),
            ("생활 자금 기여", Amount(f.WageIncome - f.Consumption)),
            ("총 순손익", Amount(f.NetIncome))
        ]);
        y = Statement("재무비율 보고서", y,
        [
            ("현금 / 자산", $"{(double)f.Cash / Math.Max(1, f.Assets):P2}"),
            ("부채 / 자산", $"{(double)f.Liabilities / Math.Max(1, f.Assets):P2}"),
            ("주문 예약 / 현금", $"{(double)f.ReservedCash / Math.Max(1, f.Cash):P2}"),
            ("누적 거래비용 / 기초 자본", $"{(double)(f.Fees + f.Taxes + f.InterestExpense + f.BorrowFees) / Math.Max(1, f.OpeningEquity):P2}")
        ]);
        return Wrap("자산 = 부채 + 자본. 기초 현금 + 순현금흐름 = 기말 현금. 투자 수익률은 임금·소비 유출입을 제외합니다.", 21, y + 8, 354, 11, Muted, 20) + 16;
    }
}
