using Cubiomes_Viewer.Engine.Data.Analysis;

namespace Cubiomes_Viewer.Service.CsvServ;

public interface ICsvService
{
    /// <summary>
    /// Writes the table as the content of a CSV file: UTF-8 with a byte-order mark, values split by
    /// <paramref name="separator"/>, the header, then one line per row, with fields in quotes where they would otherwise
    /// be read wrongly.
    /// </summary>
    byte[] Write(CsvTable table, CsvSeparator separator);
}
