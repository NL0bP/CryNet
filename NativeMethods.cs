using System.Runtime.InteropServices;
using CryPhysics.Native.Marshal;

namespace CryPhysics.Native;

/// <summary>
/// Signature LY invokes once per heightmap cell during narrowphase intersect.
/// The DLL stores no height buffer — each query fans out to managed code on
/// the physics thread. <paramref name="user"/> is the opaque pointer passed
/// to <c>geom_create_heightfield</c>, echoed back unchanged.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate float HeightCallback(int ix, int iy, nint user);

internal static partial class NativeMethods
{
    internal const string Lib = "CryPhysics";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void EventCallback(int eventType, nint payload, nuint len, nint user);

    [LibraryImport(Lib, EntryPoint = "cryphysics_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial uint AbiVersion();

    [LibraryImport(Lib, EntryPoint = "cryphysics_abi_sizeof_vec3")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nuint AbiSizeofVec3();

    [LibraryImport(Lib, EntryPoint = "cryphysics_abi_sizeof_quat")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nuint AbiSizeofQuat();

    [LibraryImport(Lib, EntryPoint = "cryphysics_debug_get_hf_stats")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void DebugGetHfStats(
        out ulong callCount,
        out int lastIx, out int lastIy, out float lastH,
        out int minIx, out int minIy,
        out int maxIx, out int maxIy);

    [LibraryImport(Lib, EntryPoint = "cryphysics_debug_get_seh_stats")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void DebugGetSehStats(
        out ulong count, out uint lastCode, out nuint lastAddr);

    [LibraryImport(Lib, EntryPoint = "world_create")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nint WorldCreate(in WorldCfg cfg);

    [LibraryImport(Lib, EntryPoint = "world_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void WorldDestroy(nint world);

    [LibraryImport(Lib, EntryPoint = "world_timestep")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int WorldTimeStep(nint world, float dt, int flags);

    [LibraryImport(Lib, EntryPoint = "world_setup_entity_grid")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int WorldSetupEntityGrid(nint world, CpVec3 origin, int nx, int ny, float stepX, float stepY);

    [LibraryImport(Lib, EntryPoint = "world_register_surface_type")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int WorldRegisterSurfaceType(nint world, int id, float friction, float bounciness);

    [LibraryImport(Lib, EntryPoint = "world_set_event_sink")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int WorldSetEventSink(nint world, nint callback, nint user);

    [LibraryImport(Lib, EntryPoint = "entity_create_rigid")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nint EntityCreateRigid(nint world, in EntityCfg cfg);

    [LibraryImport(Lib, EntryPoint = "entity_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void EntityDestroy(nint entity);

    [LibraryImport(Lib, EntryPoint = "entity_set_params")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntitySetParams(nint entity, int paramsType, nint blob, nuint len);

    [LibraryImport(Lib, EntryPoint = "entity_do_action")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityDoAction(nint entity, int actionType, nint blob, nuint len);

    [LibraryImport(Lib, EntryPoint = "entity_get_status")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityGetStatus(nint entity, int statusType, nint output, nuint len);

    [LibraryImport(Lib, EntryPoint = "entity_add_geometry")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityAddGeometry(nint entity, nint geom, in PartParams part);

    [LibraryImport(Lib, EntryPoint = "entity_apply_impulse")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityApplyImpulse(nint entity, in CpImpulse imp);

    [LibraryImport(Lib, EntryPoint = "entity_set_pose")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static unsafe partial int EntitySetPose(nint entity, CpVec3* pos, CpQuat* rot);

    [LibraryImport(Lib, EntryPoint = "entity_set_velocity")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static unsafe partial int EntitySetVelocity(nint entity, CpVec3* linVel, CpVec3* angVel);

    [LibraryImport(Lib, EntryPoint = "entity_get_pos")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityGetPos(nint entity, out CpVec3 outPos);

    [LibraryImport(Lib, EntryPoint = "entity_get_quat")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityGetQuat(nint entity, out CpQuat outQuat);

    [LibraryImport(Lib, EntryPoint = "entity_get_velocity")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityGetVelocity(nint entity, out CpVec3 outLinVel, out CpVec3 outAngVel);

    [LibraryImport(Lib, EntryPoint = "entity_get_dynamics")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntityGetDynamics(nint entity, out CpDynamics outDyn);

    [LibraryImport(Lib, EntryPoint = "entity_set_simulation_params")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntitySetSimulationParams(nint entity, in CpSimParams p);

    [LibraryImport(Lib, EntryPoint = "entity_set_buoyancy_params")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntitySetBuoyancyParams(nint entity, in CpBuoyancyParams p);

    [LibraryImport(Lib, EntryPoint = "entity_set_flags")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int EntitySetFlags(nint entity, uint flagsOr, uint flagsAnd);

    [LibraryImport(Lib, EntryPoint = "geom_create_box")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nint GeomCreateBox(nint world, CpVec3 size, CpVec3 pivot);

    [LibraryImport(Lib, EntryPoint = "geom_create_trimesh")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nint GeomCreateTriMesh(nint world, nint verts, nint indices, int triCount, nint surfaceIds);

    [LibraryImport(Lib, EntryPoint = "geom_create_heightfield")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial nint GeomCreateHeightfield(nint world, nint cb, nint user, int w, int h, float step);

    [LibraryImport(Lib, EntryPoint = "geom_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void GeomDestroy(nint world, nint geom);

    [LibraryImport(Lib, EntryPoint = "water_area_set")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int WaterAreaSet(nint world, in WaterAreaDesc desc);

    [LibraryImport(Lib, EntryPoint = "water_area_clear")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial int WaterAreaClear(nint world, int id);
}
