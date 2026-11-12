using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>cv_rules</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeRules
{
    public const int StructureTypes = 25;

    private const uint NoTerrainEstimate = 1;

    public uint Flags;
    public uint Salted;
    public fixed int Salts[StructureTypes];

    public static NativeRules From(StructureRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var native = new NativeRules { Flags = rules.EstimateTerrain ? 0 : NoTerrainEstimate };
        foreach (var salt in rules.Salts)
        {
            var type = (int)salt.Type;
            if (type is < 0 or >= StructureTypes)
                throw new ArgumentOutOfRangeException(nameof(rules), salt.Type, "The native generator has no such structure type.");

            native.Salted |= 1u << type;
            native.Salts[type] = salt.Salt;
        }

        return native;
    }
}
