using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Seeds;

namespace Cubiomes_Viewer.Engine.Service.SeedServ;

public sealed class SeedParser : ISeedParser
{
    private const int JavaHashMultiplier = 31;

    public SeedParseResult Parse(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return new SeedParseResult(Random.Shared.NextInt64(long.MinValue, long.MaxValue), SeedKind.Random);

        return long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed)
            ? new SeedParseResult(seed, SeedKind.Numeric)
            : new SeedParseResult(GetJavaHashCode(trimmed), SeedKind.Text);
    }

    private static int GetJavaHashCode(string text)
    {
        var hash = 0;
        foreach (var character in text)
            hash = unchecked(JavaHashMultiplier * hash + character);
        return hash;
    }
}
