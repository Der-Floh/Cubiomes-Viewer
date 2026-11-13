namespace Cubiomes_Viewer.Engine.Data;

public static class Constants
{
    public const string AppDataFolderName = "Cubiomes Viewer";

    public const string NativeLibraryName = "cvnative";
    public const int NativeAbiVersion = 10;

    /// <summary>The most samples the native generator makes along one side of an area (CV_MAX_AREA_SIDE).</summary>
    public const int MaxAreaSideSamples = 4096;

    public const int NoBiomeId = -1;
    public const byte NoBiomeTileValue = 255;
    public const int BiomeColorCount = 256;
    public const int BiomeColorChannels = 3;

    /// <summary>Climate parameters are stored as whole numbers of this many steps per unit.</summary>
    public const int ClimateUnit = 10000;

    /// <summary>Surface heights are stored as whole numbers of this many steps per block.</summary>
    public const int HeightUnit = 16;
}
