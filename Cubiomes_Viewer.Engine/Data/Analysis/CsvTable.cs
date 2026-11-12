using System.Text;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>
/// A table written as separated values: a header row, then one line per row, with the fields that hold the separator,
/// quotes or line breaks in quotes.
/// </summary>
public sealed class CsvTable
{
    private const string LineBreak = "\r\n";
    private const string Quote = "\"";
    private static readonly char[] _breaksAField = ['"', '\r', '\n'];

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

    public string ToCsv(CsvSeparator separator = CsvSeparator.Comma)
    {
        var character = separator switch
        {
            CsvSeparator.Comma => ',',
            CsvSeparator.Semicolon => ';',
            CsvSeparator.Tab => '\t',
            _ => throw new NotSupportedException($"Unsupported CSV separator: {separator}"),
        };

        var builder = new StringBuilder();
        AppendRow(builder, Header, character);
        foreach (var row in _rows)
            AppendRow(builder, row, character);
        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> fields, char separator)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (index > 0)
                builder.Append(separator);
            builder.Append(Escape(fields[index], separator));
        }

        builder.Append(LineBreak);
    }

    private static string Escape(string field, char separator)
        => field.IndexOfAny(_breaksAField) < 0 && !field.Contains(separator, StringComparison.Ordinal)
            ? field
            : Quote + field.Replace(Quote, Quote + Quote, StringComparison.Ordinal) + Quote;
}
