using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Seeds;

namespace Cubiomes_Viewer.Engine.Service.SeedServ;

/// <summary>Reads seed text as Minecraft does: Java's trim, then a whole number if Java's Long.parseLong reads one, else the text's Java hash.</summary>
public sealed class SeedParser : ISeedParser
{
    private const int JavaHashMultiplier = 31;
    private const char LastJavaSpace = ' ';
    private const int Radix = 10;

    public SeedParseResult Parse(string? text)
    {
        var trimmed = TrimLikeJava(text ?? string.Empty);
        if (trimmed.Length == 0)
            return new SeedParseResult(Random.Shared.NextInt64(long.MinValue, long.MaxValue), SeedKind.Random);

        return TryParseLikeJava(trimmed, out var seed)
            ? new SeedParseResult(seed, SeedKind.Numeric)
            : new SeedParseResult(GetJavaHashCode(trimmed), SeedKind.Text);
    }

    /// <summary>Java's String.trim, which takes away only the characters up to U+0020.</summary>
    private static string TrimLikeJava(string text)
    {
        var (start, end) = (0, text.Length);
        while (start < end && text[start] <= LastJavaSpace)
            start++;
        while (end > start && text[end - 1] <= LastJavaSpace)
            end--;
        return text[start..end];
    }

    /// <summary>Java's Long.parseLong: an optional sign followed by decimal digits of any script, within the range of a long.</summary>
    private static bool TryParseLikeJava(string text, out long value)
    {
        value = 0;
        var isNegative = text[0] == '-';
        var start = text[0] is '-' or '+' ? 1 : 0;
        if (start == text.Length)
            return false;

        var limit = isNegative ? -(Int128)long.MinValue : long.MaxValue;
        Int128 magnitude = 0;
        for (var index = start; index < text.Length; index++)
        {
            if (!TryGetDigit(text[index], out var digit))
                return false;

            magnitude = magnitude * Radix + digit;
            if (magnitude > limit)
                return false;
        }

        value = (long)(isNegative ? -magnitude : magnitude);
        return true;
    }

    private static bool TryGetDigit(char character, out int digit)
    {
        digit = char.GetUnicodeCategory(character) == UnicodeCategory.DecimalDigitNumber ? CharUnicodeInfo.GetDecimalDigitValue(character) : -1;
        return digit >= 0;
    }

    private static int GetJavaHashCode(string text)
    {
        var hash = 0;
        foreach (var character in text)
            hash = unchecked(JavaHashMultiplier * hash + character);
        return hash;
    }
}
