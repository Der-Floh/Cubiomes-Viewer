using System.Buffers;

using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>Brings out the shape of the terrain on a tile that is already painted in its colours.</summary>
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

    /// <summary>The share of the light and shade of the height layer that biome colours take, so that they stay recognisable.</summary>
    private const double BiomeReliefStrength = 0.6;

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
            for (var x = 0; x < samples; x++)
                Scale(bgra.Slice((z * samples + x) * BytesPerPixel, ColorChannels), GetSampleBrightness(heights, samples, x, z, stepsPerSample, exaggeration));
        }
    }

    /// <summary>
    /// Shades a tile of biome colours, <paramref name="pixels"/> samples wide, with the heights of the samples of a height
    /// tile that cover it. The light falls as on the height layer, only softer, and where one height sample spans several
    /// pixels, its brightness blends smoothly into that of its neighbours, which the samples around the covered ones give.
    /// </summary>
    public static void ShadeCovered(ReadOnlySpan<short> heights, int heightSamples, int heightScale, int unit, CoveredSamples covered, int pixels, Span<byte> bgra)
    {
        var grid = covered.Count + 2;
        var brightness = ArrayPool<double>.Shared.Rent(grid * grid);
        try
        {
            FillCoveredBrightness(heights, heightSamples, heightScale, unit, covered, brightness);
            var (cells, weights) = GetBlend(pixels, pixels / covered.Count);
            for (var z = 0; z < pixels; z++)
            {
                var (above, below) = (cells[z] * grid, (cells[z] + 1) * grid);
                for (var x = 0; x < pixels; x++)
                {
                    var (left, right) = (cells[x], cells[x] + 1);
                    var upper = Lerp(brightness[above + left], brightness[above + right], weights[x]);
                    var lower = Lerp(brightness[below + left], brightness[below + right], weights[x]);
                    Scale(bgra.Slice((z * pixels + x) * BytesPerPixel, ColorChannels), Lerp(upper, lower, weights[z]));
                }
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(brightness);
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

    /// <summary>
    /// The brightness of the covered samples and of the ring of samples around them, row by row; the ring is taken from
    /// the height tile as far as it reaches, so that biome tiles under the same height tile meet without a seam.
    /// </summary>
    private static void FillCoveredBrightness(ReadOnlySpan<short> heights, int samples, int scale, int unit, CoveredSamples covered, Span<double> brightness)
    {
        var exaggeration = Math.Sqrt(scale) / 2;
        var stepsPerSample = (double)scale * unit;
        var grid = covered.Count + 2;

        for (var row = 0; row < grid; row++)
        {
            var z = Math.Clamp(covered.Z + row - 1, 0, samples - 1);
            for (var column = 0; column < grid; column++)
            {
                var x = Math.Clamp(covered.X + column - 1, 0, samples - 1);
                brightness[row * grid + column] = 1 + BiomeReliefStrength * (GetSampleBrightness(heights, samples, x, z, stepsPerSample, exaggeration) - 1);
            }
        }
    }

    /// <summary>
    /// For each pixel along one side, the cell of the brightness grid whose samples surround its centre and how far
    /// towards the next sample it lies.
    /// </summary>
    private static (int[] Cells, double[] Weights) GetBlend(int pixels, int pixelsPerSample)
    {
        var (cells, weights) = (new int[pixels], new double[pixels]);
        for (var pixel = 0; pixel < pixels; pixel++)
        {
            var position = (pixel + 0.5) / pixelsPerSample + 0.5;
            cells[pixel] = (int)position;
            weights[pixel] = position - cells[pixel];
        }

        return (cells, weights);
    }

    private static double GetSampleBrightness(ReadOnlySpan<short> heights, int samples, int x, int z, double stepsPerSample, double exaggeration)
    {
        var (north, south) = (Math.Max(z - 1, 0), Math.Min(z + 1, samples - 1));
        var (west, east) = (Math.Max(x - 1, 0), Math.Min(x + 1, samples - 1));
        var riseEast = GetSlope(heights[z * samples + east] - heights[z * samples + west], east - west, stepsPerSample) * exaggeration;
        var riseSouth = GetSlope(heights[south * samples + x] - heights[north * samples + x], south - north, stepsPerSample) * exaggeration;
        return GetBrightness(riseEast, riseSouth);
    }

    private static double GetSlope(int rise, int sampleDistance, double stepsPerSample)
        => sampleDistance == 0 ? 0 : rise / (sampleDistance * stepsPerSample);

    private static double Lerp(double from, double to, double weight) => from + (to - from) * weight;

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
