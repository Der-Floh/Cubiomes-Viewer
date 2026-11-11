namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>Brings out the shape of the terrain on a height tile that is already painted in its colours.</summary>
internal static class HeightRelief
{
    private const int BytesPerPixel = TileColorizer.BytesPerPixel;
    private const int ColorChannels = 3;

    // The light comes from the north-west, 35 degrees above the horizon: the unit vector towards it.
    private const double LightEast = -0.5773502691896258;
    private const double LightSouth = -0.5773502691896258;
    private const double LightUp = 0.5773502691896258;
    private const double MinBrightness = 0.45;
    private const double MaxBrightness = 1.45;
    private const double ContourBrightness = 0.55;

    /// <summary>
    /// Brightens slopes that face the light and darkens those that face away; flat ground keeps its colour.
    /// Coarser samples flatten the terrain, so the slopes are exaggerated with the square root of the scale.
    /// </summary>
    public static void Shade(ReadOnlySpan<short> heights, int samples, int scale, int unit, Span<byte> bgra)
    {
        var exaggeration = Math.Sqrt(scale) / 2;
        var stepsPerSample = (double)scale * unit;

        for (var z = 0; z < samples; z++)
        {
            var (north, south) = (Math.Max(z - 1, 0), Math.Min(z + 1, samples - 1));
            for (var x = 0; x < samples; x++)
            {
                var (west, east) = (Math.Max(x - 1, 0), Math.Min(x + 1, samples - 1));
                var riseEast = GetSlope(heights[z * samples + east] - heights[z * samples + west], east - west, stepsPerSample) * exaggeration;
                var riseSouth = GetSlope(heights[south * samples + x] - heights[north * samples + x], south - north, stepsPerSample) * exaggeration;

                Scale(bgra.Slice((z * samples + x) * BytesPerPixel, ColorChannels), GetBrightness(riseEast, riseSouth));
            }
        }
    }

    /// <summary>Darkens every sample whose neighbour to the east or south lies in another band of heights.</summary>
    public static void DrawContours(ReadOnlySpan<short> heights, int samples, int scale, int unit, Span<byte> bgra)
    {
        var interval = GetContourBlocks(scale) * unit;

        for (var z = 0; z < samples; z++)
        {
            for (var x = 0; x < samples; x++)
            {
                var band = GetBand(heights[z * samples + x], interval);
                var east = GetBand(heights[z * samples + Math.Min(x + 1, samples - 1)], interval);
                var south = GetBand(heights[Math.Min(z + 1, samples - 1) * samples + x], interval);
                if (band != east || band != south)
                    Scale(bgra.Slice((z * samples + x) * BytesPerPixel, ColorChannels), ContourBrightness);
            }
        }
    }

    /// <summary>The blocks between two contour lines: wider apart where a sample covers more ground.</summary>
    public static int GetContourBlocks(int scale) => scale switch
    {
        <= 16 => 16,
        <= 64 => 32,
        _ => 64,
    };

    private static double GetSlope(int rise, int sampleDistance, double stepsPerSample)
        => sampleDistance == 0 ? 0 : rise / (sampleDistance * stepsPerSample);

    /// <summary>How bright a slope is compared with flat ground, from the angle between its normal and the light.</summary>
    private static double GetBrightness(double riseEast, double riseSouth)
    {
        var facing = -riseEast * LightEast - riseSouth * LightSouth + LightUp;
        var brightness = facing / (Math.Sqrt(riseEast * riseEast + riseSouth * riseSouth + 1) * LightUp);
        return Math.Clamp(brightness, MinBrightness, MaxBrightness);
    }

    private static int GetBand(short height, int interval) => (int)Math.Floor((double)height / interval);

    private static void Scale(Span<byte> channels, double brightness)
    {
        for (var index = 0; index < channels.Length; index++)
            channels[index] = (byte)Math.Min(byte.MaxValue, Math.Round(channels[index] * brightness));
    }
}
