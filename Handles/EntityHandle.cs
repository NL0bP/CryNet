using System.Runtime.InteropServices;

namespace CryPhysics.Native.Handles;

public sealed class EntityHandle : SafeHandle
{
    public EntityHandle() : base(nint.Zero, ownsHandle: true) { }

    internal EntityHandle(nint raw) : base(nint.Zero, ownsHandle: true)
    {
        SetHandle(raw);
    }

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle()
    {
        if (handle != nint.Zero)
        {
            NativeMethods.EntityDestroy(handle);
        }
        return true;
    }

    internal nint Raw => handle;
}
