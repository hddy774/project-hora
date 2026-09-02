namespace Chronicles.Application;

/// <summary>
/// Keeps observer UI zoom inside the space that can safely display the responsive shell.
/// The requested preference is preserved separately from this effective presentation scale.
/// </summary>
public static class AdaptiveUiScalePolicy
{
    public const double ReferenceWidth = 1_280d;
    public const double ReferenceHeight = 720d;
    public const double ViewportPadding = 32d;
    public const double MinimumEffectiveScale = 0.75d;

    public static double CalculateEffectiveScale(
        double requestedScale,
        double viewportWidth,
        double viewportHeight)
    {
        if (!double.IsFinite(requestedScale) || requestedScale <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedScale));
        }

        if (!double.IsFinite(viewportWidth)
            || !double.IsFinite(viewportHeight)
            || viewportWidth <= ViewportPadding
            || viewportHeight <= ViewportPadding)
        {
            return requestedScale;
        }

        var widthFit = (viewportWidth - ViewportPadding) / ReferenceWidth;
        var heightFit = (viewportHeight - ViewportPadding) / ReferenceHeight;
        var fitScale = Math.Max(
            MinimumEffectiveScale,
            Math.Min(widthFit, heightFit));
        return Math.Min(requestedScale, fitScale);
    }
}
