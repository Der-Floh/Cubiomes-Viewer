using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>One line of an analysis: what it is, a few details, and the world and place it opens on the map.</summary>
public sealed record AnalysisRow(string Title, string Detail, WorldKey World, BlockPos? Position);

/// <summary>What an analysis found: the lines to show, the table to save, and a sentence about it.</summary>
public sealed record AnalysisResult(IReadOnlyList<AnalysisRow> Rows, CsvTable Table, string Status);
