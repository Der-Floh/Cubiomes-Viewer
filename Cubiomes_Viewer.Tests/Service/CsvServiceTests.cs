using System.Text;

using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Service.CsvServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class CsvServiceTests
{
    private static readonly byte[] _byteOrderMark = [0xEF, 0xBB, 0xBF];

    private readonly CsvService _csv = new();

    [Fact]
    public void Write_FieldsWithCommasQuotesOrLineBreaks_AreQuoted()
    {
        var table = new CsvTable("Seed", "Biome", "Note");
        table.Add("-12", "Plains", "plain");
        table.Add("7", "Badlands, eroded", "a \"red\" one\nin two lines");

        Assert.Equal("Seed,Biome,Note\r\n-12,Plains,plain\r\n7,\"Badlands, eroded\",\"a \"\"red\"\" one\nin two lines\"\r\n", Write(table, CsvSeparator.Comma));
    }

    [Fact]
    public void Write_WithSemicolons_QuotesFieldsWithSemicolonsButNotThoseWithCommas()
    {
        var table = new CsvTable("Biome", "Note");
        table.Add("Badlands, eroded", "red; dry");

        Assert.Equal("Biome;Note\r\nBadlands, eroded;\"red; dry\"\r\n", Write(table, CsvSeparator.Semicolon));
    }

    [Fact]
    public void Write_WithTabs_SeparatesByTabs()
    {
        var table = new CsvTable("X", "Z");
        table.Add("-16", "32");

        Assert.Equal("X\tZ\r\n-16\t32\r\n", Write(table, CsvSeparator.Tab));
    }

    [Fact]
    public void Write_FieldWithALineBreakAlone_IsQuoted()
    {
        var table = new CsvTable("Note");
        table.Add("two\nlines");

        Assert.Equal("Note\r\n\"two\nlines\"\r\n", Write(table, CsvSeparator.Comma));
    }

    [Fact]
    public void Write_TextBeyondAscii_IsUtf8AfterAByteOrderMarkSoExcelReadsIt()
    {
        var table = new CsvTable("Details");
        table.Add("Size: 9 × 9 · cracked");

        var content = _csv.Write(table, CsvSeparator.Comma);

        Assert.Equal(_byteOrderMark, content[.._byteOrderMark.Length]);
        Assert.Equal("Details\r\nSize: 9 × 9 · cracked\r\n", Encoding.UTF8.GetString(content.AsSpan(_byteOrderMark.Length)));
    }

    private string Write(CsvTable table, CsvSeparator separator)
    {
        var content = _csv.Write(table, separator);
        Assert.Equal(_byteOrderMark, content[.._byteOrderMark.Length]);
        return Encoding.UTF8.GetString(content.AsSpan(_byteOrderMark.Length));
    }
}
