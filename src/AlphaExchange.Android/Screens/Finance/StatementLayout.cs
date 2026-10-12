using Android.Graphics;
using AlphaExchange.Core;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    float Statement(string title, float y, (string label, string value)[] rows)
    {
        if (!Visible(y, 58 + rows.Length * 48)) return y + 74 + rows.Length * 48;
        Box(20, y, 360, 58 + rows.Length * 48, Card, 17);
        TextFit(title, 36, y + 31, 16, Ink, 328, true); Line(36, y + 44, 364, y + 44, Stroke);
        for (int i = 0; i < rows.Length; i++)
        {
            bool last = i == rows.Length - 1; float baseline = y + 65 + i * 48;
            TextFit(rows[i].label, 36, baseline, 11, last ? Ink : Muted, 328, last);
            Text(rows[i].value, 364, baseline + 19, 12, last ? Lime : Ink, last, Paint.Align.Right);
        }
        return y + 74 + rows.Length * 48;
    }
}
