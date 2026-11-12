using Cubiomes_Viewer.Engine.Data.Analysis;

namespace Cubiomes_Viewer.Engine.Tests.Data.Analysis;

public sealed class CsvTableTests
{
    [Fact]
    public void ToCsv_FieldsWithCommasQuotesOrLineBreaks_AreQuoted()
    {
        var table = new CsvTable("Seed", "Biome", "Note");
        table.Add("-12", "Plains", "plain");
        table.Add("7", "Badlands, eroded", "a \"red\" one\nin two lines");

        var csv = table.ToCsv();

        Assert.Equal("Seed,Biome,Note\r\n-12,Plains,plain\r\n7,\"Badlands, eroded\",\"a \"\"red\"\" one\nin two lines\"\r\n", csv);
    }

    [Fact]
    public void ToCsv_WithSemicolons_QuotesFieldsWithSemicolonsButNotThoseWithCommas()
    {
        var table = new CsvTable("Biome", "Note");
        table.Add("Badlands, eroded", "red; dry");

        Assert.Equal("Biome;Note\r\nBadlands, eroded;\"red; dry\"\r\n", table.ToCsv(CsvSeparator.Semicolon));
    }

    [Fact]
    public void ToCsv_WithTabs_SeparatesByTabs()
    {
        var table = new CsvTable("X", "Z");
        table.Add("-16", "32");

        Assert.Equal("X\tZ\r\n-16\t32\r\n", table.ToCsv(CsvSeparator.Tab));
    }

    [Fact]
    public void Add_RowOfAnotherWidth_Throws()
    {
        var table = new CsvTable("A", "B");

        var exception = Assert.Throws<ArgumentException>(() => table.Add("only one"));

        Assert.StartsWith("A row of this table has 2 fields, not 1.", exception.Message, StringComparison.Ordinal);
    }
}
