using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Tests.Data;

public sealed class BlockPosTextTests
{
    private static readonly BlockPos _position = new(120, -340);

    [Fact]
    public void ToLocation_NoHeight_NamesTheBlockAndItsChunk()
        => Assert.Equal("X 120, Z -340  ·  Chunk 7, -22", BlockPosText.ToLocation(_position));

    [Fact]
    public void ToLocation_ExactHeight_PutsItBetweenTheOtherAxes()
        => Assert.Equal("X 120, Y -51, Z -340  ·  Chunk 7, -22", BlockPosText.ToLocation(_position, StructureHeight.At(-51)));

    [Fact]
    public void ToLocation_EstimatedHeight_MarksItAsAnEstimate()
        => Assert.Equal("X 120, Y ≈ 72, Z -340  ·  Chunk 7, -22", BlockPosText.ToLocation(_position, StructureHeight.Around(72)));

    [Fact]
    public void ToLocation_HeightRange_NamesBothEnds()
        => Assert.Equal("X 120, Y 33 to 87, Z -340  ·  Chunk 7, -22", BlockPosText.ToLocation(_position, StructureHeight.Between(33, 87)));

    [Fact]
    public void ToHeight_RangeOfOneLevel_IsThatLevel()
        => Assert.Equal("Y 48", BlockPosText.ToHeight(StructureHeight.Between(48, 48)));
}
