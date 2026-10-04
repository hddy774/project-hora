using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    static string Moment(long hour) => $"S{hour / 720 + 1} D{hour % 720 / 24 + 1} {hour % 24:00}시";
    float PeriodPicker(float y)
    {
        string[] labels = ["현재 시즌", "전체", "10일 전", "5일 전", "전일"];
        for (int i = 0; i < labels.Length; i++)
        {
            var period = (ComparisonPeriod)i; bool active = comparisonPeriod == period;
            float x = 20 + i * 73;
            Box(x, y, 68, 32, active ? Lime : Card2, 9);
            Text(labels[i], x + 34, y + 21, 10, active ? Bg : Muted, active, Paint.Align.Center);
            Hit(x, y, 68, 32, () => { comparisonPeriod = period; scroll = 0; });
        }
        var result = game!.Period(comparisonPeriod);
        Text($"{Moment(result.Start.Hour)} → {Moment(result.End.Hour)}", 21, y + 51, 10, Muted);
        if (result.Partial) { Text("기록 부족: 가능한 기간 표시 · 전체 차트는 최근 120일", 21, y + 70, 10, Red); return y + 89; }
        return y + 74;
    }
    float Pie(string title, (string Label, double Value)[] rows, float y)
    {
        rows = rows.Where(r => double.IsFinite(r.Value) && r.Value > 0).ToArray();
        float height = Math.Max(156, 57 + rows.Length * 23);
        Box(20, y, 360, height, Card, 17); Text(title, 36, y + 28, 14, Ink, true);
        double total = rows.Sum(r => r.Value); float angle = -90;
        using var bounds = new RectF(38, y + 47, 134, y + 143);
        for (int i = 0; i < rows.Length; i++)
        {
            var color = Hex(Palette[i % Palette.Length]); float sweep = (float)(rows[i].Value / total * 360);
            paint.SetShader(null); paint.Color = color; paint.SetStyle(Paint.Style.Fill); c.DrawArc(bounds, angle, sweep, true, paint); angle += sweep;
            Circle(155, y + 59 + i * 23, 3, color);
            TextFit(rows[i].Label, 166, y + 63 + i * 23, 10, Muted, 132);
            Text($"{rows[i].Value / total:P1}", 364, y + 63 + i * 23, 10, Ink, true, Paint.Align.Right);
        }
        Circle(86, y + 95, 28, Card); Text(total > 0 ? "구성" : "자료 없음", 86, y + 99, 10, Muted, false, Paint.Align.Center);
        return y + Math.Max(160, height + 15);
    }
    float Graph(string title, IEnumerable<double> values, float y, string caption, bool bar = false)
    {
        var data = values.Where(double.IsFinite).ToArray();
        Box(20, y, 360, 150, Card, 17); Text(title, 36, y + 27, 14, Ink, true);
        if (!bar) Chart(data, 36, y + 48, 328, 69, Teal, true);
        else
        {
            double low = Math.Min(0, data.DefaultIfEmpty(0).Min()), high = Math.Max(1, data.DefaultIfEmpty(0).Max());
            double span = Math.Max(1, high - low); float zero = y + 117 - (float)((0 - low) / span * 69);
            Line(36, zero, 364, zero, Stroke);
            for (int i = 0; i < data.Length; i++)
            {
                float top = y + 117 - (float)((data[i] - low) / span * 69), width = 328f / Math.Max(1, data.Length);
                Box(36 + i * width, Math.Min(top, zero), Math.Max(1, width - 3), Math.Max(1, Math.Abs(zero - top)), Direction(data[i]), 2);
            }
        }
        TextFit(caption, 36, y + 138, 10, Muted, 328); return y + 166;
    }
    float DrawRepresentative(float y, Trader t)
    {
        Text("대표의 판단 능력", 21, y + 15, 20, Ink, true);
        Text(GameEngine.DispositionNames[(int)t.Disposition] + " · 능력 1~100 · 모든 기법을 상황에 맞춰 혼합", 21, y + 39, 10, Muted); y += 56;
        var values = t.Abilities.Values();
        for (int i = 0; i < values.Length; i++)
        {
            Box(20, y, 360, 51, Card, 12);
            Text(GameEngine.AbilityNames[i], 35, y + 21, 12, Ink, true);
            Text(values[i].ToString(), 364, y + 21, 12, Lime, true, Paint.Align.Right);
            Box(35, y + 32, 329, 5, Card2, 2); Box(35, y + 32, 329 * values[i] / 100f, 5, Hex(Palette[i]), 2); y += 60;
        }
        y = Wrap(t.Decision, 22, y + 14, 356, 12, Teal) + 12;
        var loan = game!.LoanTerms(t);
        y = Statement("은행 · 신용 " + loan.Rating, y, [("신용 점수", $"{t.CreditScore} / 99"), ("자기자산 대비 한도", $"{loan.AssetRatio:P0}"),
            ("연 이자율", $"{loan.AnnualRate:P2}"), ("대출 한도", Money(loan.Limit)), ("추가 대출 가능", Money(loan.Available)),
            ("대출 잔액", Money(t.LoanDebt)), ("누적 이자 비용", Money(t.InterestExpense)), ("벌금 / 미납", $"{ShortMoney(t.Fines)} / {ShortMoney(t.FineDebt)}")]);
        Text("한도는 부채를 뺀 자기자산 기준 · 이자는 매일 정산", 21, y + 5, 10, Muted); y += 28;
        return y;
    }
    float DrawEconomy(float y)
    {
        var p = S.Government.Policy;
        y = Statement($"정부 · 시즌 {p.Season} · {p.Name}", y, [("매매 이익 세율", $"{p.TaxRate:P1}"), ("양쪽 거래 수수료", $"{p.FeeBasisPoints / 100.0:0.00}%"),
            ("기준금리 (연)", $"{p.BaseRate:P2}"), ("공매도 규제", p.ShortSellingAllowed ? $"허용 / 자기자산 {p.ShortExposureLimit:P0}" : "신규 공매도 금지"),
            ("대출 한도 조정", $"기본 한도의 {p.LoanLimitMultiplier:P0}"), ("주식 보조금 (일)", $"보유액 {p.SubsidyRate:P3}"), ("감독 적발 강도", $"{p.Enforcement:P0}")]);
        y = Pie("은행 · 정부 · 거래소 현금 구성", [("은행", S.Bank.Cash), ("정부", S.Government.Cash), ("거래소", S.FeePool)], y);
        y = Statement("은행 대출 현황", y, [("은행 현금", ShortMoney(S.Bank.Cash) + "원"), ("참가자 대출 잔액", ShortMoney(game!.Participants.Sum(t => t.LoanDebt)) + "원"),
            ("누적 이자 수익", ShortMoney(S.Bank.InterestIncome) + "원"), ("공매도 대여료 수익", ShortMoney(S.Bank.BorrowFeeIncome) + "원"), ("기관 공매도 부채", ShortMoney(S.Bots.Sum(t => t.ShortLiability(S.Stocks))) + "원")]);
        var rates = Enum.GetValues<CreditRating>().Select(r => (r.ToString(), $"{GameEngine.CreditLimits[(int)r]:P0} / {p.BaseRate + GameEngine.CreditSpreads[(int)r]:P2}")).ToArray();
        y = Statement("신용 등급 · 기본 한도 / 연 금리", y, rates);
        Text("AAA 100% → C 30% · 정책의 한도 조정이 추가 적용", 21, y + 4, 10, Muted); y += 28;
        y = Statement("정부 누적 재정", y, [("세금 수입", ShortMoney(S.Government.Taxes) + "원"), ("부과 벌금", ShortMoney(S.Government.Fines) + "원"),
            ("보조금 지출", ShortMoney(S.Government.Subsidies) + "원"), ("정부 현금", ShortMoney(S.Government.Cash) + "원")]);
        Button("대표 상호작용 · 작전 보기  →", 20, y, 360, 42, () => { operationsTab = true; SetPage(5); }, false); y += 60;
        Text("최근 시즌 정책", 21, y + 14, 18, Ink, true); y += 32;
        foreach (var policy in S.Government.History.AsEnumerable().Reverse())
        { Box(20, y, 360, 69, Card, 13); Text($"S{policy.Season} · {policy.Name}", 36, y + 26, 13, Ink, true); Text($"세율 {policy.TaxRate:P0} · 금리 {policy.BaseRate:P2} · 공매도 {(policy.ShortSellingAllowed ? "허용" : "제한")}", 36, y + 50, 11, Muted); y += 79; }
        return y + 10;
    }
    float DrawOperations(float y)
    {
        Text("대표들의 합의와 작전", 21, y + 15, 20, Ink, true);
        y = Wrap("대표의 협상 능력·성향·준법 성향에 따라 공동 매집을 제안합니다. 목표 미달이나 감독 적발 시 두 기관 모두 벌금을 냅니다.", 21, y + 42, 352, 11, Muted, 19) + 18;
        if (S.Operations.Count == 0) { Text("아직 대표 간 공동 작전이 없습니다.", 22, y + 19, 12, Muted); return y + 65; }
        foreach (var o in S.Operations.AsEnumerable().Reverse())
        {
            var color = o.Status == OperationStatus.Active ? Lime : o.Status == OperationStatus.Failed ? Red : Teal;
            Box(20, y, 360, 171, Card, 17);
            Pill(o.Status == OperationStatus.Active ? "진행 중" : o.Status == OperationStatus.Failed ? "실패·벌금" : "완료", 35, y + 14, color, 79);
            Text(S.Stocks[o.StockIndex].Symbol + " · 공동 매집", 363, y + 31, 12, Ink, true, Paint.Align.Right);
            Text(Representatives.Name(o.LeaderId) + " ↔ " + Representatives.Name(o.PartnerId), 36, y + 66, 17, Ink, true);
            Text($"{Moment(o.StartedHour)} → {Moment(o.EndsHour)}", 36, y + 90, 10, Muted);
            TextFit(o.Detail, 36, y + 115, 11, Muted, 328);
            Text($"벌금 {ShortMoney(o.Fine)}원 · 참여 {S.Bots[o.LeaderId - 1].Name} / {S.Bots[o.PartnerId - 1].Name}", 36, y + 144, 10, color);
            int id = o.LeaderId; Hit(20, y, 360, 171, () => selectedTrader = id); y += 183;
        }
        return y + 10;
    }
}
