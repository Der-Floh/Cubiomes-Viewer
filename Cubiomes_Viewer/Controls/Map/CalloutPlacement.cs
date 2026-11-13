using Avalonia;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Places a callout above the marker it describes, or below it where the map has no room above. Sideways it stays inside
/// the map while it can, but always spans the marker, so it leaves the map together with the marker.
/// </summary>
public static class CalloutPlacement
{
    public const double Gap = 6;
    public const double Margin = 8;

    /// <summary>How far in from its edges a callout always reaches over the marker's centre.</summary>
    public const double MinOverlap = 16;

    /// <summary>Returns the top left corner of a callout of the given size for a marker drawn within <paramref name="marker"/>.</summary>
    public static Point Place(Size callout, Rect marker, Size view)
    {
        var inside = Math.Clamp(marker.Center.X - callout.Width / 2, Margin, Math.Max(Margin, view.Width - Margin - callout.Width));
        var x = Math.Clamp(inside, marker.Center.X - callout.Width + MinOverlap, Math.Max(marker.Center.X - callout.Width + MinOverlap, marker.Center.X - MinOverlap));

        var above = marker.Top - Gap - callout.Height;
        var y = above >= Margin || marker.Bottom + Gap + callout.Height > view.Height - Margin ? above : marker.Bottom + Gap;
        return new Point(x, y);
    }
}
