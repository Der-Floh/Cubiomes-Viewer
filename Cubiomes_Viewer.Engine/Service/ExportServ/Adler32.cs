namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>The Adler-32 checksum that ends a zlib stream, as RFC 1950 defines it, with the combination of two parts that zlib offers.</summary>
internal static class Adler32
{
    public const uint Initial = 1;

    private const uint Modulus = 65521;

    /// <summary>The most bytes the sums can take in before they have to be reduced, so that they stay within 32 bits.</summary>
    private const int MaxBytesBetweenReductions = 5552;

    public static uint Compute(ReadOnlySpan<byte> data, uint adler = Initial)
    {
        uint low = adler & 0xFFFF, high = adler >> 16;
        while (!data.IsEmpty)
        {
            var part = data[..Math.Min(data.Length, MaxBytesBetweenReductions)];
            foreach (var value in part)
            {
                low += value;
                high += low;
            }

            low %= Modulus;
            high %= Modulus;
            data = data[part.Length..];
        }

        return high << 16 | low;
    }

    /// <summary>The checksum of two parts in a row, from the checksum of each and the length of the second.</summary>
    public static uint Combine(uint first, uint second, long secondLength)
    {
        var remainder = (uint)(secondLength % Modulus);
        var low = first & 0xFFFF;
        var high = remainder * low % Modulus;
        low += (second & 0xFFFF) + Modulus - 1;
        high += (first >> 16) + (second >> 16) + Modulus - remainder;
        if (low >= Modulus)
            low -= Modulus;
        if (low >= Modulus)
            low -= Modulus;
        if (high >= Modulus << 1)
            high -= Modulus << 1;
        if (high >= Modulus)
            high -= Modulus;
        return high << 16 | low;
    }
}
