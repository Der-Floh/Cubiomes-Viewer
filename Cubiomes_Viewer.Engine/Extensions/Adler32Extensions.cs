using System.IO.Hashing;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class Adler32Extensions
{
    private const uint Modulus = 65521;

    extension(Adler32)
    {
        /// <summary>The checksum of two parts in a row, from the checksum of each and the length of the second, as zlib combines them.</summary>
        public static uint Combine(uint first, uint second, long secondLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(secondLength);

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
}
