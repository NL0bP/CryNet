using CryPhysics.Native.Marshal;

namespace CryPhysics.Native;

/// <summary>
/// Holds a <c>phys_geometry*</c> registered with a <see cref="NativePhysicalWorld"/>'s geom
/// manager. Geometry lifetime is tied to the owning world — destroying the world also
/// releases every geom registered against it — so this type does not wrap a SafeHandle;
/// it just carries the raw pointer and a back-ref to the world for UnregisterGeometry.
/// </summary>
public sealed class NativeGeometry : IDisposable
{
    private readonly NativePhysicalWorld _world;
    private nint _raw;

    internal NativeGeometry(NativePhysicalWorld world, nint raw)
    {
        _world = world;
        _raw = raw;
    }

    internal nint Raw => _raw;

    public void Dispose()
    {
        var raw = _raw;
        _raw = nint.Zero;
        if (raw != nint.Zero)
        {
            NativeMethods.GeomDestroy(_world.Raw, raw);
        }
    }
}
