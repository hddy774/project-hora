using Android.Graphics;
using AlphaExchange.Client;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    Bitmap? DecodeArena()
    {
        try
        {
            using var options = new BitmapFactory.Options { InJustDecodeBounds = true };
            using (var bounds = Context!.Assets!.Open("arena.png")) BitmapFactory.DecodeStream(bounds, null, options);
            if (options.OutWidth <= 0 || options.OutHeight <= 0) return null;
            float pixels = (Context!.Resources!.DisplayMetrics!.WidthPixels) * 1.1f;
            options.InSampleSize = PortraitSampling.SampleSize(options.OutWidth, options.OutHeight, pixels, pixels);
            options.InJustDecodeBounds = false;
            options.InPreferredConfig = Bitmap.Config.Rgb565;
            using var stream = Context.Assets!.Open("arena.png");
            return BitmapFactory.DecodeStream(stream, null, options);
        }
        catch (Java.IO.IOException) { return null; }
    }
}
