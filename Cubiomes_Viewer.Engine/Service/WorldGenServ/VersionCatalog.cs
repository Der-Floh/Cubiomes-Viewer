using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public sealed class VersionCatalog : IVersionCatalog
{
    public IReadOnlyList<McVersion> Versions => field ??= LoadVersions();
    public McVersion Newest => Versions[^1];

    public string GetName(McVersion version)
        => Marshal.PtrToStringUTF8(CubiomesNative.VersionName((int)version))
            ?? throw new ArgumentOutOfRangeException(nameof(version), version, "The native generator does not know this Minecraft version.");

    private static McVersion[] LoadVersions()
    {
        var oldest = CubiomesNative.OldestVersion();
        var newest = CubiomesNative.NewestVersion();
        return [.. Enumerable.Range(oldest, newest - oldest + 1).Select(version => (McVersion)version)];
    }
}
