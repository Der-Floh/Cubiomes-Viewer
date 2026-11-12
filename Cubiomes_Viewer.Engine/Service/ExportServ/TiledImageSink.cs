using System.Globalization;
using System.IO.Compression;

using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Cuts the rows into square tiles, the last column and row cut to the image, and writes each tile as a PNG entry of an
/// archive named after its column and row. The tiles of one row are kept in memory until that row is complete.
/// </summary>
internal sealed class TiledImageSink : IImageRowSink
{
    private readonly ZipArchive _archive;
    private readonly string _namePrefix;
    private readonly MapImageLayout _layout;
    private readonly int _tileSize;
    private readonly PngFormat _format;
    private readonly List<(MemoryStream Content, PngWriter Writer)> _tiles = [];
    private int _row;

    public TiledImageSink(ZipArchive archive, string namePrefix, MapImageLayout layout, int tileSize, PngFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileSize);

        _archive = archive;
        _namePrefix = namePrefix;
        _layout = layout;
        _tileSize = tileSize;
        _format = format;
    }

    /// <summary>The entry name of the tile in a column and row, counted from the top left.</summary>
    public static string GetEntryName(string namePrefix, int column, int row)
        => string.Create(CultureInfo.InvariantCulture, $"{namePrefix}{column}_{row}.png");

    public void WriteRow(ReadOnlySpan<byte> row)
    {
        if (_tiles.Count == 0)
            StartTileRow();

        var bytesPerPixel = _format.BytesPerPixel;
        for (var column = 0; column < _tiles.Count; column++)
        {
            var writer = _tiles[column].Writer;
            writer.WriteRow(row.Slice(column * _tileSize * bytesPerPixel, writer.RowBytes));
        }

        _row++;
        if (_row % _tileSize == 0 || _row == _layout.Height)
            StoreTileRow();
    }

    public void Finish()
    {
        if (_row != _layout.Height)
            throw new InvalidOperationException($"{_row} of the {_layout.Height} rows are written.");
    }

    public void Dispose()
    {
        foreach (var (content, writer) in _tiles)
        {
            writer.Dispose();
            content.Dispose();
        }

        _tiles.Clear();
    }

    private void StartTileRow()
    {
        var height = Math.Min(_tileSize, _layout.Height - _row);
        for (var left = 0; left < _layout.Width; left += _tileSize)
        {
            var content = new MemoryStream();
            _tiles.Add((content, new PngWriter(content, Math.Min(_tileSize, _layout.Width - left), height, _format)));
        }
    }

    private void StoreTileRow()
    {
        var tileRow = (_row - 1) / _tileSize;
        for (var column = 0; column < _tiles.Count; column++)
        {
            var (content, writer) = _tiles[column];
            writer.Finish();
            var entry = _archive.CreateEntry(GetEntryName(_namePrefix, column, tileRow), CompressionLevel.NoCompression);
            using var stream = entry.Open();
            stream.Write(content.GetBuffer().AsSpan(0, (int)content.Length));
        }

        Dispose();
    }
}
