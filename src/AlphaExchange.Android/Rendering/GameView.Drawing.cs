using AlphaExchange.Client;
using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;
using AColor = Android.Graphics.Color;
using APath = Android.Graphics.Path;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    bool Visible(float y, float height) => y + height >= clipTop && y <= clipBottom;
    void Box(float x, float y, float w, float height, AColor color, float radius = 18, AColor? border = null)
    {
        if (!Visible(y, height)) return;
        paint.SetShader(null); paint.Color = color; paint.SetStyle(Paint.Style.Fill);
        c.DrawRoundRect(x, y, x + w, y + height, radius, radius, paint);
        if (border.HasValue)
        { paint.Color = border.Value; paint.StrokeWidth = 1; paint.SetStyle(Paint.Style.Stroke); c.DrawRoundRect(x, y, x + w, y + height, radius, radius, paint); paint.SetStyle(Paint.Style.Fill); }
    }

    void Text(string value, float x, float y, float size, AColor color, bool heavy = false, Paint.Align? align = null, bool headline = false)
    {
        if (!Visible(y - size * 1.5f, size * 2)) return;
        paint.SetShader(null); paint.Color = color; paint.TextSize = size; paint.SetStyle(Paint.Style.Fill);
        paint.SetTypeface(headline ? display : heavy ? bold : normal); paint.TextAlign = align ?? Paint.Align.Left;
        c.DrawText(value, x, y, paint);
    }

    void TextFit(string value, float x, float y, float size, AColor color, float width, bool heavy = false)
    {
        if (!Visible(y - size * 1.5f, size * 2)) return;
        paint.TextSize = size; paint.SetTypeface(heavy ? bold : normal);
        if (paint.MeasureText(value) > width)
        {
            int length = paint.BreakText(value, true, Math.Max(0, width - paint.MeasureText("…")), null);
            if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            value = value[..length] + "…";
        }
        Text(value, x, y, size, color, heavy);
    }

    float Wrap(string value, float x, float y, float width, float size, AColor color, float spacing = 21)
    {
        paint.TextSize = size; paint.SetTypeface(normal); int offset = 0;
        while (offset < value.Length)
        {
            int newline = value.IndexOf('\n', offset); if (newline < 0) newline = value.Length;
            if (newline == offset) { offset++; y += spacing; continue; }
            string remaining = value[offset..newline];
            int length = Math.Max(1, paint.BreakText(remaining, true, width, null));
            if (length < remaining.Length && char.IsHighSurrogate(remaining[length - 1])) length = length == 1 ? Math.Min(2, remaining.Length) : length - 1;
            Text(remaining[..Math.Min(length, remaining.Length)], x, y, size, color); y += spacing; offset += length;
            if (offset == newline && offset < value.Length) offset++;
        }
        return y;
    }

    void Line(float x1, float y1, float x2, float y2, AColor color, float width = 1)
    { paint.SetShader(null); paint.Color = color; paint.StrokeWidth = width; paint.StrokeCap = Paint.Cap.Round; c.DrawLine(x1, y1, x2, y2, paint); }

    void Circle(float x, float y, float radius, AColor color)
    { paint.SetShader(null); paint.SetStyle(Paint.Style.Fill); paint.Color = color; c.DrawCircle(x, y, radius, paint); }

    void Hit(float x, float y, float w, float height, Action action)
    {
        var clipped = ModalViewport.ClipTarget(y, y + height, clipTop, clipBottom);
        if (clipped is { } bounds) targets.Add((x, bounds.Top, x + w, bounds.Bottom, () =>
        {
            lock (simulationGate)
            {
                action();
                // Settings can affect a paused market without advancing time.
                frameData.Clear();
            }
        }
            ));
    }

    void Button(string label, float x, float y, float w, float height, Action action, bool primary = true, bool enabled = true)
    {
        Box(x, y, w, height, !enabled ? Card2 : primary ? Lime : Card2, 15, primary ? null : Stroke);
        Text(label, x + w / 2, y + height / 2 + 5, 14, !enabled ? Muted : primary ? Bg : Ink, true, Paint.Align.Center);
        if (enabled) Hit(x, y, w, height, action);
    }

    void Pill(string label, float x, float y, AColor color, float w)
    { Box(x, y, w, 24, new AColor((int)color.R, color.G, color.B, 24), 8); Text(label, x + w / 2, y + 16, 10, color, true, Paint.Align.Center); }

    void Chart(IEnumerable<double> values, float x, float y, float width, float height, AColor color, bool fill = false)
    {
        if (!Visible(y, height)) return;
        chartSeries.Capture(values);
        var data = chartSeries.Values;
        double low = chartSeries.Low, high = chartSeries.High;
        var path = chartLine;
        path.Rewind();
        float lastX = x, lastY = y;
        for (int i = 0; i < data.Length; i++)
        {
            float px = x + i * width / (data.Length - 1), py = y + height - (float)((data[i] - low) / (high - low)) * height;
            if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
            lastX = px; lastY = py;
        }
        if (fill)
        {
            var area = chartArea;
            area.Set(path); area.LineTo(x + width, y + height); area.LineTo(x, y + height); area.Close();
            using var gradient = new LinearGradient(x, y, x, y + height, new AColor((int)color.R, color.G, color.B, 65), new AColor((int)color.R, color.G, color.B, 0), Shader.TileMode.Clamp!);
            paint.SetShader(gradient); paint.SetStyle(Paint.Style.Fill); c.DrawPath(area, paint); paint.SetShader(null);
        }
        paint.Color = color; paint.StrokeWidth = fill ? 2.5f : 1.8f; paint.SetStyle(Paint.Style.Stroke); paint.StrokeJoin = Paint.Join.Round;
        c.DrawPath(path, paint); paint.SetStyle(Paint.Style.Fill);
        if (fill) { Circle(lastX, lastY, 4, Bg); Circle(lastX, lastY, 3, color); }
    }

    void Robot(int id, float x, float y, float size) => Portrait(S.Bots[id - 1].PortraitId, x, y, size, size);

    void Mark(float x, float y, float size, AColor color)
    {
        using var p = new APath(); p.MoveTo(x, y + size); p.LineTo(x + size / 2, y); p.LineTo(x + size, y + size);
        paint.Color = color; paint.StrokeWidth = size * .18f; paint.SetStyle(Paint.Style.Stroke); paint.StrokeJoin = Paint.Join.Round; c.DrawPath(p, paint); paint.SetStyle(Paint.Style.Fill);
        Line(x + size * .38f, y + size * .72f, x + size * .62f, y + size * .72f, color, size * .12f);
    }

    void NavIcon(int type, float x, float y, AColor color)
    {
        if (type == 0) { Line(x, y + 17, x + 5, y + 10, color, 2); Line(x + 5, y + 10, x + 11, y + 13, color, 2); Line(x + 11, y + 13, x + 20, y + 2, color, 2); Line(x + 14, y + 2, x + 20, y + 2, color, 2); }
        else if (type == 1) { Box(x, y + 4, 22, 15, Bg, 4, color); Box(x + 13, y + 8, 11, 7, Bg, 2, color); Circle(x + 17, y + 11.5f, 1, color); }
        else if (type == 2) { Line(x + 3, y + 3, x + 19, y + 3, color, 2); Line(x + 3, y + 3, x + 6, y + 13, color, 2); Line(x + 19, y + 3, x + 16, y + 13, color, 2); Line(x + 6, y + 13, x + 16, y + 13, color, 2); Line(x + 11, y + 13, x + 11, y + 19, color, 2); Line(x + 6, y + 21, x + 16, y + 21, color, 2); }
        else if (type == 3) { Circle(x + 11, y + 6, 5, color); Line(x + 3, y + 22, x + 3, y + 16, color, 2); Line(x + 3, y + 16, x + 11, y + 12, color, 2); Line(x + 11, y + 12, x + 19, y + 16, color, 2); Line(x + 19, y + 16, x + 19, y + 22, color, 2); }
        else if (type == 4) { Box(x, y + 12, 5, 10, color, 1); Box(x + 8, y + 5, 5, 17, color, 1); Box(x + 16, y, 5, 22, color, 1); }
        else if (type == 5) { Box(x, y + 3, 22, 20, Bg, 3, color); Line(x, y + 10, x + 22, y + 10, color); Line(x + 6, y, x + 6, y + 6, color, 2); Line(x + 16, y, x + 16, y + 6, color, 2); Circle(x + 7, y + 16, 1.5f, color); Circle(x + 15, y + 16, 1.5f, color); }
        else { Box(x, y + 1, 22, 21, Bg, 4, color); Line(x + 5, y + 7, x + 17, y + 7, color, 1.5f); Line(x + 5, y + 12, x + 17, y + 12, color, 1.5f); Line(x + 5, y + 17, x + 12, y + 17, color, 1.5f); }
    }
}
