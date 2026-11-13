namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>A table to save as a CSV file: a header row and rows of as many fields.</summary>
public sealed class CsvTable
{
    public IReadOnlyList<string> Header { get; }

    public IReadOnlyList<IReadOnlyList<string>> Rows => _rows;

    private readonly List<IReadOnlyList<string>> _rows = [];

    public CsvTable(params string[] header)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentOutOfRangeException.ThrowIfZero(header.Length);

        Header = header;
    }

    public void Add(params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Length != Header.Count)
            throw new ArgumentException($"A row of this table has {Header.Count} fields, not {fields.Length}.", nameof(fields));

        _rows.Add(fields);
    }
}
