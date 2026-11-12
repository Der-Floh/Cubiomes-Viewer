namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>The CRC-32 a PNG chunk ends with, over its type and its data.</summary>
public static class PngCrc
{
    private const uint Polynomial = 0xEDB88320;

    private static readonly uint[] _table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data) => ~Update(Update(uint.MaxValue, type), data);

    private static uint Update(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
            crc = _table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            table[index] = value;
        }

        return table;
    }
}
