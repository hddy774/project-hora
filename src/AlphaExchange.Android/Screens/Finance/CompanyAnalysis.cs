using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float DrawCompanyAnalysis(Stock stock, CompanyReport report, float y)
    {
        if (companyTab == 5)
        {
            y = MoneyStatement("현금 창출·상환 보고서", y,
            [
                ("EBITDA · 영업이익 + 감가상각", FinancialAnalysis.Ebitda(report)),
                ("영업 현금흐름", report.OperatingCashFlow),
                ("잉여 현금흐름 · 영업 + 투자", FinancialAnalysis.FreeCashFlow(report)),
                ("순차입금 · 차입 - 현금", report.Debt - report.Cash),
                ("배당 후 순이익", report.NetIncome - report.Dividends)
            ]);
            return Statement("지급 능력·수익의 질", y,
            [
                ("이자 보상 배수", FinancialAnalysis.InterestCoverage(report) is { } coverage ? $"{coverage:0.00}배" : "이자 비용 없음"),
                ("영업 현금 / 순이익", FinancialAnalysis.CashConversion(report) is { } quality ? $"{quality:P2}" : "적자·이익 없음"),
                ("총자산 이익률 · 월 결산", $"{FinancialAnalysis.ReturnOnAssets(report):P2}"),
                ("유동자산 / 총부채", report.Liabilities > 0 ? $"{(double)(report.Cash + report.Receivables + report.Inventory) / report.Liabilities:P2}" : "부채 없음")
            ]);
        }
        y = MoneyStatement("금리·매출 충격 보고서", y,
        [
            ("기준 결산 순이익", report.NetIncome),
            ("연 금리 +1%p 시 월 순이익", FinancialAnalysis.IncomeAfterRateRise(report)),
            ("매출 -10% · 비용 동일 시", report.NetIncome - report.Revenue / 10),
            ("두 충격 동시 발생 시", FinancialAnalysis.IncomeAfterRateRise(report) - report.Revenue / 10)
        ]);
        y = Wrap("현재 차입·비용을 고정한 민감도 계산입니다. 법인세 절감, 추가 경영 대응과 실제 주가 변동을 예측하지 않습니다.", 21, y + 8, 354, 11, Muted, 20) + 18;
        var exposure = CompanyEconomics.For(stock.Symbol);
        return Statement("회사별 경제 노출 · 현재 경영 환경", y,
        [
            ("사업 기조", GameEngine.CompanyStrategyNames[(int)stock.CompanyStrategy]),
            ("운영 방식", GameEngine.ManagementNames[(int)stock.Management]),
            ("자본 배분", GameEngine.AllocationNames[(int)stock.Allocation]),
            ("수요 / 정책 민감도", $"{exposure.Demand:0.00} / {exposure.Policy:0.00}"),
            ("금융 완화 / 물가 노출", $"{exposure.Financing:+0.00;-0.00} / {exposure.Inflation:+0.00;-0.00}"),
            ("회사 단독 뉴스", $"{stock.CompanyNewsCount:N0}건 · 다음 {Math.Max(0, stock.NextCompanyNewsMinute - S.CompletedMinutes)}분 후"),
            ("공개 뉴스 누적 반영", $"{stock.SeasonNewsImpact:+0.000;-0.000;0.000}")
        ]);
    }
}
