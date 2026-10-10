namespace AlphaExchange.App;

// Logical 400-point canvas coordinates, independent of Android for regression checks.
internal readonly record struct ModalViewport(float Top, float BodyTop, float BodyBottom, float FooterTop, float FooterBottom)
{
    public float BodyHeight => Math.Max(0, BodyBottom - BodyTop);

    public static ModalViewport Create(float height, float desiredHeight, float footerHeight)
    {
        float top = Math.Max(12, height - desiredHeight);
        float footerBottom = height - 22;
        float footerTop = footerBottom - footerHeight;
        return new(top, top + 58, Math.Max(top + 58, footerTop - 12), footerTop, footerBottom);
    }

    public bool ContainsBody(float x, float y) => x >= 8 && x < 392 && y >= BodyTop && y < BodyBottom;

    public (float Top, float Bottom)? ClipTarget(float top, float bottom) => ClipTarget(top, bottom, BodyTop, BodyBottom);

    public static (float Top, float Bottom)? ClipTarget(float top, float bottom, float clipTop, float clipBottom)
    {
        top = Math.Max(top, clipTop);
        bottom = Math.Min(bottom, clipBottom);
        return bottom > top ? (top, bottom) : null;
    }
}

internal sealed class ModalScrollState
{
    public string Key { get; private set; } = "";
    public ModalViewport Viewport { get; private set; }
    public float Offset { get; private set; }
    public float Maximum { get; private set; }

    public void Show(string key, ModalViewport viewport)
    {
        if (Key != key) Reset();
        Key = key;
        Viewport = viewport;
    }

    public bool SetContentHeight(float height)
    {
        Maximum = Math.Max(0, height - Viewport.BodyHeight);
        float previous = Offset;
        Offset = Math.Clamp(Offset, 0, Maximum);
        return previous != Offset;
    }

    public void Drag(float delta) => Offset = Math.Clamp(Offset + delta, 0, Maximum);

    public void Reset()
    {
        Key = "";
        Offset = Maximum = 0;
        Viewport = default;
    }
}
