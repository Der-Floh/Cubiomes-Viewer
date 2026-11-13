using Cubiomes_Viewer.Engine.Data.Analysis;

namespace Cubiomes_Viewer.Engine.Tests.Data.Analysis;

public sealed class CsvTableTests
{
    [Fact]
    public void Add_RowOfAnotherWidth_Throws()
    {
        var table = new CsvTable("A", "B");

        var exception = Assert.Throws<ArgumentException>(() => table.Add("only one"));

        Assert.StartsWith("A row of this table has 2 fields, not 1.", exception.Message, StringComparison.Ordinal);
    }
}
