using System.Runtime.InteropServices;

namespace CryPhysics.Native.Marshal;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CpVec3
{
    public float X;
    public float Y;
    public float Z;

    public CpVec3(float x, float y, float z) { X = x; Y = y; Z = z; }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CpQuat
{
    public float W;
    public float X;
    public float Y;
    public float Z;

    public CpQuat(float w, float x, float y, float z) { W = w; X = x; Y = y; Z = z; }

    public static CpQuat Identity => new(1f, 0f, 0f, 0f);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe struct CpMat33
{
    public fixed float M[9];
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct WorldCfg
{
    public float GravityZ;
    public float MaxTimeStep;
    public int   SingleThreaded;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EntityCfg
{
    public int    Id;
    public CpVec3 Pos;
    public CpQuat Rot;
    public float  Mass;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PartParams
{
    public CpVec3 Pivot;
    public CpQuat Rot;
    public float  Mass;
    public int    SurfaceId;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct WaterAreaDesc
{
    public int    Id;
    public float  LevelZ;
    public CpVec3 Flow;
    public float  Density;
    public float  Resistance;
}

// Mirrors CpImpulse in cryphysics_api.h — layout must stay in lock-step.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CpImpulse
{
    public CpVec3 Impulse;
    public CpVec3 AngImpulse;
    public CpVec3 Point;
    public int    HasAngImpulse;
    public int    HasPoint;
    public int    IApplyTime;
    public int    PartId;
}

// Dynamics snapshot — mirrors CpDynamics in cryphysics_api.h.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CpDynamics
{
    public float  Mass;
    public CpVec3 V;
    public CpVec3 W;
    public float  SubmergedFraction;
    public int    NContacts;
}

// Per-entity simulation tunables. Leave a field at 0 (or negative for damping)
// to keep the LY default.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CpSimParams
{
    public float MaxTimeStep;
    public float Damping;
    public float MaxRotVel;
    public float Mass;

    public static CpSimParams Default => new()
    {
        MaxTimeStep = 0f,
        Damping = -1f,
        MaxRotVel = 0f,
        Mass = 0f,
    };
}

// Per-entity buoyancy overrides. Leave at 0 (or negative for damping) to keep
// defaults.
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CpBuoyancyParams
{
    public float WaterDensityScale;
    public float WaterResistanceScale;
    public float WaterDamping;

    public static CpBuoyancyParams Default => new()
    {
        WaterDensityScale = 0f,
        WaterResistanceScale = 0f,
        WaterDamping = -1f,
    };
}

public enum CpResult
{
    Ok                  = 0,
    ErrInvalidHandle    = -1,
    ErrInvalidArg       = -2,
    ErrNativeException  = -3,
    ErrNotImplemented   = -4,
}
