using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    (string Run, long Next, long Before)? failureViewKey;
    IReadOnlyList<BankruptcyRecord> failureView = [];
    long failureBefore = long.MaxValue;
    readonly Stack<long> failurePages = [];
    static string FailureLabel(BankruptcyKind kind) => kind switch { BankruptcyKind.Company => "기업", BankruptcyKind.Institution => "기관", _ => "개인" };
    float DrawBankruptcies(float y)
    {
        Text("파산과 새로운 세대", 20, y + 23, 24, Ink, true);
        Text("이전 주체의 손실과 사유를 파일에 보존", 21, y + 47, 11, Muted); y += 65;
        var totals = S.BankruptcyTotals;
        y = Statement("시장 전체 누적 파산", y, [("기업",Money(totals.Companies)+"개"),("기관",Money(totals.Institutions)+"개"),("개인",Money(totals.Retail)+"명"),
            ("실제 재출자 · 기업",ShortMoney(totals.CompanyCapital)+"원"),("실제 재출자 · 기관/개인",ShortMoney(totals.InstitutionCapital+totals.RetailCapital)+"원")]);
        y = Wrap("파산 시 보유 자산을 정산하고 미회수 채권을 기록합니다. 새 주체는 실제 경제 자금으로 출자받습니다. 재원이 부족하면 대기하며, 투자 이익에 재출자와 임금을 포함하지 않습니다.", 21, y + 4, 354, 11, Muted, 19) + 20;
        var key = (S.RunId, S.NextBankruptcyId, failureBefore);
        if (failureViewKey != key) { failureView = store.ReadBankruptcies(S, beforeId: failureBefore); failureViewKey = key; }
        Button("← 최근", 20, y, 100, 36, () => { failureBefore = failurePages.Pop(); scroll = 0; }, false, failurePages.Count > 0);
        Text("발생 순 기록", 200, y + 23, 12, Muted, false, Paint.Align.Center);
        Button("과거 →", 280, y, 100, 36, () => { failurePages.Push(failureBefore); failureBefore = failureView[^1].Id; scroll = 0; }, false, failureView.Count == 60); y += 52;
        if (failureView.Count == 0) { Text("파산이 발생하면 기업·기관·개인 기록을 표시합니다.", 21, y + 20, 11, Muted); y += 53; }
        foreach (var failure in failureView)
        {
            if (Visible(y, 149))
            {
                Box(20, y, 360, 149, Card, 15); Pill(FailureLabel(failure.Kind) + " 파산", 35, y + 14, Red, 81);
                Text(Moment(failure.Hour), 363, y + 31, 10, Muted, false, Paint.Align.Right);
                TextFit(failure.Name + $" · {failure.Generation}기", 36, y + 58, 14, Ink, 328, true);
                TextFit(failure.Reason, 36, y + 82, 11, Muted, 328);
                Text($"당시 순자산 {ShortMoney(failure.Equity)}원", 36, y + 105, 11, Red);
                Text($"미회수 {ShortMoney(failure.LoanWriteOff + failure.TradeWriteOff + failure.ShortWriteOff + failure.FineWriteOff + failure.TaxWriteOff)}원 · 재출자 {ShortMoney(failure.ReplacementCapital)}원", 36, y + 129, 10, Teal);
            }
            y += 162;
        }
        Button("시장 통계로 돌아가기", 20, y, 360, 42, () => SetPage(ScreenId.Statistics), false); return y + 64;
    }
}
