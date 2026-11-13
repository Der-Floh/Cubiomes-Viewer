using System.Globalization;

using CsvHelper;

using Cubiomes_Viewer.Engine.Data.Analysis;

namespace Cubiomes_Viewer.Service.CsvServ;

public sealed class CsvService : ICsvService
{
    /// <summary>The line break RFC 4180 ends records with.</summary>
    private const string LineBreak = "\r\n";

    public string Write(CsvTable table, CsvSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(table);

        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = CsvWriter.Create(text, new CsvWriterOptions { Separator = ToCharacter(separator), NewLine = LineBreak }, leaveOpen: true))
        {
            WriteRecord(csv, table.Header);
            foreach (var row in table.Rows)
                WriteRecord(csv, row);
        }

        return text.ToString();
    }

    private static char ToCharacter(CsvSeparator separator) => separator switch
    {
        CsvSeparator.Comma => ',',
        CsvSeparator.Semicolon => ';',
        CsvSeparator.Tab => '\t',
        _ => throw new NotSupportedException($"Unsupported CSV separator: {separator}"),
    };

    private static void WriteRecord(CsvWriter csv, IReadOnlyList<string> fields)
    {
        foreach (var field in fields)
            csv.WriteField(field);
        csv.EndRecord();
    }
}
