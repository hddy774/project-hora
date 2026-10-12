using Android.App;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    float NumberFormatPicker(float y)
    {
        Button(S.Environment.NumberFormat == 0 ? "금액: 만·억·조  ⇄  정확한 숫자" : "금액: 정확한 숫자  ⇄  만·억·조", 20, y, 360, 36,
            () => { S.Environment.NumberFormat = 1 - S.Environment.NumberFormat; RequestSave(); }, false);
        return y + 49;
    }

    void ShowAmount(string label, long amount)
    {
        if (Context is not Activity activity) return;
        new AlertDialog.Builder(activity)!.SetTitle(label)!
            .SetMessage($"{KoreanNumber.Compact(amount)}원\n\n정확한 금액: {KoreanNumber.Full(amount)}원")!
            .SetPositiveButton("확인", (_, _) => { })!.Show();
    }

    float MoneyStatement(string title, float y, (string Label, long Amount)[] rows)
    {
        float end = Statement(title, y, rows.Select(r => (r.Label, ShortMoney(r.Amount) + "원")).ToArray());
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            Hit(20, y + 48 + i * 48, 360, 48, () => ShowAmount(row.Label, row.Amount));
        }
        return end;
    }
}
