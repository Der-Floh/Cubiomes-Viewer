using Cubiomes_Viewer.Engine.Data.Analysis;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>How exported files are written.</summary>
public sealed class ExportSettings
{
    public CsvSeparator CsvSeparator { get; set; } = CsvSeparator.Comma;
}
