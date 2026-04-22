using CryPhysics.Native.Handles;
using CryPhysics.Native.Marshal;

namespace CryPhysics.Native;

public sealed class NativePhysicalWorld : IDisposable
{
    private readonly WorldHandle _handle;

    private NativePhysicalWorld(WorldHandle handle)
    {
        _handle = handle;
    }

    public static NativePhysicalWorld Create(in WorldCfg cfg)
    {
        NativeLibraryResolver.Register();
        var raw = NativeMethods.WorldCreate(cfg);
        if (raw == nint.Zero)
        {
            throw new CryPhysicsException(CpResult.ErrNativeException, "world_create returned null");
        }
        return new NativePhysicalWorld(new WorldHandle(raw));
    }

    public void TimeStep(float dt, int flags = 0)
    {
        var rc = NativeMethods.WorldTimeStep(_handle.Raw, dt, flags);
        CryPhysicsException.Throw(rc, "world_timestep");
    }

    /// <summary>
    /// Runs one native step and returns false instead of throwing if the DLL
    /// caught an SEH access violation inside LY code. Use this on the physics
    /// thread so a native fault doesn't tear down the tick loop — the caller
    /// can log + continue, and <see cref="DebugGetSehStats"/> reveals the
    /// fault code/address for post-mortem.
    /// </summary>
    public bool TryTimeStep(float dt, out int resultCode, int flags = 0)
    {
        resultCode = NativeMethods.WorldTimeStep(_handle.Raw, dt, flags);
        return resultCode == 0;
    }

    public static void DebugGetSehStats(out ulong count, out uint lastCode, out nuint lastAddr)
        => NativeMethods.DebugGetSehStats(out count, out lastCode, out lastAddr);

    public void SetupEntityGrid(CpVec3 origin, int nx, int ny, float stepX, float stepY)
    {
        var rc = NativeMethods.WorldSetupEntityGrid(_handle.Raw, origin, nx, ny, stepX, stepY);
        CryPhysicsException.Throw(rc, "world_setup_entity_grid");
    }

    public void RegisterSurfaceType(int id, float friction, float bounciness)
    {
        var rc = NativeMethods.WorldRegisterSurfaceType(_handle.Raw, id, friction, bounciness);
        CryPhysicsException.Throw(rc, "world_register_surface_type");
    }

    public void SetWaterArea(in WaterAreaDesc desc)
    {
        var rc = NativeMethods.WaterAreaSet(_handle.Raw, desc);
        CryPhysicsException.Throw(rc, "water_area_set");
    }

    public void ClearWaterArea(int id)
    {
        var rc = NativeMethods.WaterAreaClear(_handle.Raw, id);
        CryPhysicsException.Throw(rc, "water_area_clear");
    }

    public NativeRigidEntity CreateRigidEntity(in EntityCfg cfg)
    {
        var raw = NativeMethods.EntityCreateRigid(_handle.Raw, cfg);
        if (raw == nint.Zero)
        {
            throw new CryPhysicsException(CpResult.ErrNativeException, "entity_create_rigid returned null");
        }
        return new NativeRigidEntity(new EntityHandle(raw));
    }

    public NativeGeometry CreateBoxGeometry(CpVec3 size, CpVec3 pivot)
    {
        var raw = NativeMethods.GeomCreateBox(_handle.Raw, size, pivot);
        if (raw == nint.Zero)
        {
            throw new CryPhysicsException(CpResult.ErrNativeException, "geom_create_box returned null");
        }
        return new NativeGeometry(this, raw);
    }

    /// <summary>
    /// Builds a trimesh from packed xyz vertices and triangle indices.
    /// <paramref name="verts"/> has length 3·N (float x,y,z per vertex);
    /// <paramref name="indices"/> has length 3·triCount. Index values must fit in
    /// ushort (≤65535) — LY copies both buffers internally so the spans can be
    /// freed on return.
    /// </summary>
    public unsafe NativeGeometry CreateTriMeshGeometry(ReadOnlySpan<float> verts, ReadOnlySpan<int> indices, int triCount)
    {
        if (verts.IsEmpty || indices.IsEmpty || triCount <= 0)
        {
            throw new ArgumentException("verts/indices must be non-empty and triCount > 0");
        }
        fixed (float* pv = verts)
        fixed (int* pi = indices)
        {
            var raw = NativeMethods.GeomCreateTriMesh(_handle.Raw, (nint)pv, (nint)pi, triCount, nint.Zero);
            if (raw == nint.Zero)
            {
                throw new CryPhysicsException(CpResult.ErrNativeException, "geom_create_trimesh returned null");
            }
            return new NativeGeometry(this, raw);
        }
    }

    /// <summary>
    /// Installs the world's heightfield terrain as a live managed callback.
    /// The DLL holds no height data — every query LY issues during intersect
    /// is forwarded to <paramref name="cb"/> on the physics thread.
    ///
    /// The caller MUST keep <paramref name="cb"/> rooted (e.g. in a static
    /// field) until the returned geometry is disposed — otherwise the GC
    /// can reclaim the delegate's thunk and LY will call into freed memory.
    /// </summary>
    public NativeGeometry CreateHeightfieldGeometry(HeightCallback cb, nint user, int w, int h, float step)
    {
        ArgumentNullException.ThrowIfNull(cb);
        var fnPtr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(cb);
        var raw = NativeMethods.GeomCreateHeightfield(_handle.Raw, fnPtr, user, w, h, step);
        if (raw == nint.Zero)
        {
            throw new CryPhysicsException(CpResult.ErrNativeException, "geom_create_heightfield returned null");
        }
        return new NativeGeometry(this, raw);
    }

    internal nint Raw => _handle.Raw;

    public static void DebugGetHfStats(
        out ulong callCount,
        out int lastIx, out int lastIy, out float lastH,
        out int minIx, out int minIy,
        out int maxIx, out int maxIy)
        => NativeMethods.DebugGetHfStats(
            out callCount,
            out lastIx, out lastIy, out lastH,
            out minIx, out minIy,
            out maxIx, out maxIy);

    public void Dispose()
    {
        _handle.Dispose();
    }
}
