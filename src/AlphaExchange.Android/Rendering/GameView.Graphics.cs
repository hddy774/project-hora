using AlphaExchange.Client;
using APath = Android.Graphics.Path;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    readonly ChartSeriesBuffer chartSeries = new();
    readonly APath chartLine = new();
    readonly APath chartArea = new();
    bool graphicsReleased;

    public void ReleaseGraphics()
    {
        if (graphicsReleased) return;
        graphicsReleased = true;
        // Hardware display lists may retain bitmaps after detachment. Release
        // cache ownership; Android decides when their pixel memory is reclaimable.
        portraitAtlases.Clear();
        portraitLru.Clear();
        portraitBytes = 0;
        arena = null;
        chartLine.Dispose();
        chartArea.Dispose();
        portraitOutline.Dispose();
        portraitSource.Dispose();
        portraitTarget.Dispose();
        paint.Dispose();
        normal.Dispose();
        bold.Dispose();
        display.Dispose();
    }
}
