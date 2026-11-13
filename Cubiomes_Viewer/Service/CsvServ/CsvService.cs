using System.Text;

using CsvHelper;

using Cubiomes_Viewer.Engine.Data.Analysis;

namespace Cubiomes_Viewer.Service.CsvServ;

public sealed class CsvService : ICsvService
{
    /// <summary>The line break RFC 4180 ends records with.</summary>
    private const string LineBreak = "\r\n";

    /// <summary>UTF-8 with a byte-order mark, without which Excel reads the file in the code page of the machine.</summary>
    private static readonly UTF8Encoding _encoding = new(encoderShouldEmitUTF8Identifier: true);

    public byte[] Write(CsvTable table, CsvSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(table);

        using var content = new MemoryStream();
        using (var text = new StreamWriter(content, _encoding, leaveOpen: true))
        using (var csv = CsvWriter.Create(text, new CsvWriterOptions { Separator = ToCharacter(separator), NewLine = LineBreak }, leaveOpen: true))
        {
            WriteRecord(csv, table.Header);
            foreach (var row in table.Rows)
                WriteRecord(csv, row);
        }

        return content.ToArray();
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
