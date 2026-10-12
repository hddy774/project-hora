using Android.Graphics;
using AlphaExchange.Client;
using System.Text.Json;
using AColor = Android.Graphics.Color;
using APath = Android.Graphics.Path;

namespace AlphaExchange.App;
public sealed partial class GameView
{
    sealed class PortraitRegion
    {
        public int id { get; set; }
        public string file { get; set; } = "";
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }
    readonly record struct AtlasKey(string File, int Sample);
    readonly Dictionary<AtlasKey, Bitmap> portraitAtlases = [];
    readonly List<AtlasKey> portraitLru = [];
    long portraitBytes;
    const long PortraitCacheBudget = 6 * 1024 * 1024;
    readonly Rect portraitSource = new();
    readonly RectF portraitTarget = new();
    readonly APath portraitOutline = new();
    const int PortraitAtlasLimit = 8;
    PortraitRegion[] portraitRegions = [];
    void LoadPortraitManifest()
    {
        using var stream = Context!.Assets!.Open("portraits/manifest.json");
        portraitRegions = JsonSerializer.Deserialize<PortraitRegion[]>(stream) ?? [];
    }
    void Portrait(int id, float x, float y, float width, float height)
    {
        if (id < 1 || id > portraitRegions.Length || y + height < clipTop || y > clipBottom) return;
        var region = portraitRegions[id - 1];
        int cropHeight = Math.Min(region.height, (int)(region.width * height / width));
        int cropWidth = Math.Min(region.width, (int)(cropHeight * width / height));
        int top = region.y + Math.Min(12, region.height - cropHeight);
        int sample = PortraitSampling.SampleSize(cropWidth, cropHeight, width * scale, height * scale);
        var key = new AtlasKey(region.file, sample);
        if (!portraitAtlases.TryGetValue(key, out var bitmap))
        {
            using var stream = Context!.Assets!.Open("portraits/" + region.file);
            using var options = new BitmapFactory.Options
            {
                InPreferredConfig = Bitmap.Config.Rgb565,
                InSampleSize = sample
            };
            bitmap = BitmapFactory.DecodeStream(stream, null, options);
            if (bitmap is null) return;
            while (portraitLru.Count > 0 && (portraitLru.Count >= PortraitAtlasLimit ||
                portraitBytes + bitmap.AllocationByteCount > PortraitCacheBudget))
            {
                var oldest = portraitLru[0];
                portraitLru.RemoveAt(0);
                portraitBytes -= portraitAtlases[oldest].AllocationByteCount;
                // Hardware display lists may still use evicted pixels.
                // Release our reference without recycling that memory.
                portraitAtlases.Remove(oldest);
            }
            portraitAtlases[key] = bitmap;
            portraitBytes += bitmap.AllocationByteCount;
        }
        portraitLru.Remove(key);
        portraitLru.Add(key);
        portraitSource.Set((region.x + (region.width - cropWidth) / 2) / sample, top / sample,
            (region.x + (region.width + cropWidth) / 2) / sample, (top + cropHeight) / sample);
        portraitTarget.Set(x, y, x + width, y + height);
        portraitOutline.Rewind(); portraitOutline.AddRoundRect(portraitTarget, Math.Min(15, width / 6), Math.Min(15, width / 6), APath.Direction.Cw!);
        c.Save(); c.ClipPath(portraitOutline); paint.SetShader(null); paint.Color = AColor.White; paint.FilterBitmap = true;
        c.DrawBitmap(bitmap, portraitSource, portraitTarget, paint); c.Restore();
    }
}
