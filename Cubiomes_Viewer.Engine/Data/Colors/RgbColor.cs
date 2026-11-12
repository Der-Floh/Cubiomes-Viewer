using System.Globalization;

namespace Cubiomes_Viewer.Engine.Data.Colors;

public readonly record struct RgbColor(byte Red, byte Green, byte Blue)
{
    private const double RedLuminance = 0.299;
    private const double GreenLuminance = 0.587;
    private const double BlueLuminance = 0.114;
    private const double DimmedBrightness = 0.3;
    private const int HexDigits = 6;

    /// <summary>A dark gray of the same brightness, for colours that should step back.</summary>
    public RgbColor ToDimmed()
    {
        var gray = (byte)Math.Round((Red * RedLuminance + Green * GreenLuminance + Blue * BlueLuminance) * DimmedBrightness);
        return new RgbColor(gray, gray, gray);
    }

    /// <summary>The colour as <c>#rrggbb</c>.</summary>
    public string ToHex() => string.Create(CultureInfo.InvariantCulture, $"#{Red:x2}{Green:x2}{Blue:x2}");

    /// <summary>Reads <c>#rrggbb</c> or <c>rrggbb</c>, in either case.</summary>
    public static bool TryParseHex(string? text, out RgbColor color)
    {
        var digits = text?.Trim().TrimStart('#') ?? string.Empty;
        if (digits.Length == HexDigits && uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            color = new RgbColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
            return true;
        }

        color = default;
        return false;
    }

    public override string ToString() => ToHex();
}
