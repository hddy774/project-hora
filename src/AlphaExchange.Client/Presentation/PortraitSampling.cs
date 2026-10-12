namespace AlphaExchange.Client;

internal static class PortraitSampling
{
    // Power-of-two decoding keeps enough pixels for the selected crop at the
    // device's actual density, including the full-size zoom screen.
    public static int SampleSize(int cropWidth, int cropHeight, float pixelWidth, float pixelHeight)
    {
        if (cropWidth <= 0 || cropHeight <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(cropWidth));
        int sample = 1;
        while (sample < 8 && cropWidth / (sample * 2) >= pixelWidth && cropHeight / (sample * 2) >= pixelHeight)
            sample *= 2;
        return sample;
    }
}
