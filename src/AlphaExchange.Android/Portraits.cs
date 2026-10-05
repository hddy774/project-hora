using Android.Graphics;
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
    readonly Dictionary<string, Bitmap> portraitAtlases = [];
    readonly List<string> portraitLru=[];
    readonly Rect portraitSource=new();
    readonly RectF portraitTarget=new();
    readonly APath portraitOutline=new();
    const int PortraitAtlasLimit=4;
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
        if (!portraitAtlases.TryGetValue(region.file, out var bitmap))
        {
            using var stream = Context!.Assets!.Open("portraits/" + region.file);
            using var options = new BitmapFactory.Options { InPreferredConfig = Bitmap.Config.Rgb565 };
            bitmap = BitmapFactory.DecodeStream(stream, null, options)!; portraitAtlases[region.file] = bitmap;
            while(portraitLru.Count>=PortraitAtlasLimit)
            {
                string oldest=portraitLru[0]; portraitLru.RemoveAt(0);
                // Android may still use the bitmap in a hardware display list.
                // Drop our reference; do not recycle memory beneath that list.
                portraitAtlases.Remove(oldest);
            }
        }
        portraitLru.Remove(region.file); portraitLru.Add(region.file);
        int cropHeight = Math.Min(region.height, (int)(region.width * height / width));
        int cropWidth = Math.Min(region.width, (int)(cropHeight * width / height));
        int top = region.y + Math.Min(12, region.height - cropHeight);
        portraitSource.Set(region.x + (region.width - cropWidth) / 2, top, region.x + (region.width + cropWidth) / 2, top + cropHeight);
        portraitTarget.Set(x, y, x + width, y + height);
        portraitOutline.Rewind(); portraitOutline.AddRoundRect(portraitTarget, Math.Min(15, width / 6), Math.Min(15, width / 6), APath.Direction.Cw!);
        c.Save(); c.ClipPath(portraitOutline); paint.SetShader(null); paint.Color = AColor.White; paint.FilterBitmap = true;
        c.DrawBitmap(bitmap, portraitSource, portraitTarget, paint); c.Restore();
    }
}
