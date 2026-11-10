using Microsoft.Win32.SafeHandles;

namespace Cubiomes_Viewer.Engine.Interop;

internal sealed class WorldHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public WorldHandle(nint world) : base(ownsHandle: true)
    {
        SetHandle(world);
    }

    protected override bool ReleaseHandle()
    {
        CubiomesNative.WorldDestroy(handle);
        return true;
    }
}
