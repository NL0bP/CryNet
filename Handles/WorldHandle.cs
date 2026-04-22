using System.Runtime.InteropServices;

namespace CryPhysics.Native.Handles;

public sealed class WorldHandle : SafeHandle
{
    public WorldHandle() : base(nint.Zero, ownsHandle: true) { }

    internal WorldHandle(nint raw) : base(nint.Zero, ownsHandle: true)
    {
        SetHandle(raw);
    }

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle()
    {
        if (handle != nint.Zero)
        {
            NativeMethods.WorldDestroy(handle);
        }
        return true;
    }

    internal nint Raw => handle;
}
