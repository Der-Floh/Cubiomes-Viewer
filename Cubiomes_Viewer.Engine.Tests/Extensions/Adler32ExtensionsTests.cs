using System.IO.Hashing;

using Cubiomes_Viewer.Engine.Extensions;

namespace Cubiomes_Viewer.Engine.Tests.Extensions;

public sealed class Adler32ExtensionsTests
{
    [Theory]
    [InlineData(12_345, 7_000)]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(3, 200_000)]
    public void Combine_OfTwoParts_IsTheChecksumOfBothInARow(int firstLength, int secondLength)
    {
        var random = new Random(3);
        var first = new byte[firstLength];
        var second = new byte[secondLength];
        random.NextBytes(first);
        random.NextBytes(second);

        var combined = Adler32.Combine(Adler32.HashToUInt32(first), Adler32.HashToUInt32(second), second.Length);

        Assert.Equal(Adler32.HashToUInt32([.. first, .. second]), combined);
    }
}
