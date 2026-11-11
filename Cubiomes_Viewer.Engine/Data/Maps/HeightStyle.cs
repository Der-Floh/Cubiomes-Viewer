namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>How the surface height is drawn. The numbers are stored in the settings.</summary>
public enum HeightStyle
{
    /// <summary>Colours by height: blues below the sea level, land colours above.</summary>
    Tinted = 0,

    /// <summary>The tinted colours, lit from the north-west so that slopes stand out.</summary>
    Shaded = 1,

    /// <summary>The tinted colours with a line wherever the height crosses a fixed interval.</summary>
    Contours = 2,

    /// <summary>From black for the lowest to white for the highest places.</summary>
    Grayscale = 3,
}
