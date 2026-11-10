using Cubiomes_Viewer.Engine.Data.Seeds;

namespace Cubiomes_Viewer.Engine.Service.SeedServ;

public interface ISeedParser
{
    /// <summary>
    /// Turns seed input into a world seed the way Minecraft does: a number is used as is, other text is hashed
    /// like a Java string, and empty input picks a random seed.
    /// </summary>
    SeedParseResult Parse(string? text);
}
