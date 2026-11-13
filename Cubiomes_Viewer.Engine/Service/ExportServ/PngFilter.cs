namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>The filter types of PNG rows: what each byte is predicted from before it is compressed.</summary>
internal enum PngFilter : byte
{
    None = 0,
    Sub = 1,
    Up = 2,
    Average = 3,
    Paeth = 4,
}

/// <summary>Applies the PNG filters to rows, as the PNG specification defines them.</summary>
internal static class PngFilters
{
    /// <summary>Writes the filtered bytes of <paramref name="row"/>, without the filter type, into <paramref name="output"/>.</summary>
    public static void Apply(PngFilter filter, ReadOnlySpan<byte> row, ReadOnlySpan<byte> above, int bytesPerPixel, Span<byte> output)
    {
        switch (filter)
        {
            case PngFilter.None:
                row.CopyTo(output);
                break;
            case PngFilter.Sub:
                row[..bytesPerPixel].CopyTo(output);
                for (var index = bytesPerPixel; index < row.Length; index++)
                    output[index] = unchecked((byte)(row[index] - row[index - bytesPerPixel]));
                break;
            case PngFilter.Up:
                for (var index = 0; index < row.Length; index++)
                    output[index] = unchecked((byte)(row[index] - above[index]));
                break;
            case PngFilter.Average:
                for (var index = 0; index < bytesPerPixel; index++)
                    output[index] = unchecked((byte)(row[index] - (above[index] >> 1)));
                for (var index = bytesPerPixel; index < row.Length; index++)
                    output[index] = unchecked((byte)(row[index] - ((row[index - bytesPerPixel] + above[index]) >> 1)));
                break;
            case PngFilter.Paeth:
                for (var index = 0; index < bytesPerPixel; index++)
                    output[index] = unchecked((byte)(row[index] - above[index]));
                for (var index = bytesPerPixel; index < row.Length; index++)
                    output[index] = unchecked((byte)(row[index] - Paeth(row[index - bytesPerPixel], above[index], above[index - bytesPerPixel])));
                break;
            default:
                throw new NotSupportedException($"Unsupported PNG filter: {filter}");
        }
    }

    /// <summary>The neighbour closest to left + up - up-left, preferring left, then up.</summary>
    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}
