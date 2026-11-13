using Avalonia;
using Avalonia.Controls;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Extensions;

public static class VisualExtensions
{
    extension(Visual visual)
    {
        /// <summary>
        /// The device pixels the visual is drawn on: the screen's scaling times any scale around the visual, such as the
        /// interface size, and where its origin lies on them.
        /// </summary>
        public PixelGrid GetPixelGrid()
        {
            ArgumentNullException.ThrowIfNull(visual);

            if (TopLevel.GetTopLevel(visual) is not { } topLevel || visual.TransformToVisual(topLevel) is not { } transform)
                return PixelGrid.Unscaled;

            var scaling = topLevel.RenderScaling;
            return new PixelGrid(scaling * transform.M11, transform.M31 * scaling, transform.M32 * scaling);
        }
    }
}
