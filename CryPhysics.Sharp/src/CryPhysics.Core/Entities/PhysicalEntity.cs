// Port of CryPhysics physicalentity.h/cpp - base physical entity
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Dynamics;
using CryPhysics.Geometry;
using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.Entities;

/// <summary>
/// Geometry flags. Port of geom flags from physinterface.h.
/// </summary>
public static class GeomPartFlags
{
    public const uint GeomCollides = 0x0040;
    public const uint GeomFloats = 0x10000; // buoyancy applied to this part
    public const uint GeomMonitorContacts = 0x400;
}

/// <summary>
/// Geometry part attached to an entity.
/// Port of the geom struct from physicalentity.h.
/// </summary>
public class EntityGeom
{
    public int Id;
    public PhysGeometry? PhysGeom;
    public PhysVector3 Offset;
    public PhysQuaternion Rotation = PhysQuaternion.Identity;
    public float Scale = 1f;
    public float Mass;
    public uint Flags;
    public uint FlagsCollider;
    public int SurfaceIdx;
    public float MinContactDist;
    public PhysVector3 BBoxMin;
    public PhysVector3 BBoxMax;
}

/// <summary>
/// Base physical entity implementation. All entity types derive from this.
/// Port of CPhysicalEntity from CryEngine.
/// </summary>
public class PhysicalEntity : IPhysicalEntity
{
    private int _refCount = 1;
    private int _nextPartId;

    // Core state
    public virtual PhysicsEntityType Type => PhysicsEntityType.Static;
    public int Id { get; set; }
    public bool IsAwake { get; protected set; }

    // Transform
    public PhysVector3 Position { get; set; }
    public PhysQuaternion Orientation { get; set; } = PhysQuaternion.Identity;
    public float Scale { get; set; } = 1f;

    // Bounding box
    public PhysVector3 BBoxMin { get; protected set; }
    public PhysVector3 BBoxMax { get; protected set; }

    // Geometry parts
    public readonly List<EntityGeom> Parts = new();

    // Simulation class
    public SimClass SimulationClass { get; set; } = SimClass.Static;

    // Foreign data
    public object? ForeignData { get; set; }
    public int ForeignDataType { get; set; }

    // World reference
    public IPhysicalWorld? World { get; set; }

    // Flags
    public uint Flags { get; set; }

    // Structural breakability info (port of m_pStructure from CPhysicalEntity)
    public StructureInfo? Structure { get; set; }

    // ============================================================================
    // IPhysicalEntity implementation
    // ============================================================================

    public int AddRef() => Interlocked.Increment(ref _refCount);
    public int Release() => Interlocked.Decrement(ref _refCount);

    public virtual int SetParams(PhysicsParamsBase parameters, bool threadSafe = false)
    {
        switch (parameters)
        {
            case ParamsPos pos:
                if (pos.Position.HasValue) Position = pos.Position.Value;
                if (pos.Orientation.HasValue) Orientation = pos.Orientation.Value;
                if (pos.Scale.HasValue) Scale = pos.Scale.Value;
                if (pos.SimClass.HasValue) SimulationClass = (SimClass)pos.SimClass.Value;
                if (pos.RecalcBounds) ComputeBBox();
                return 1;

            case ParamsFlags flags:
                if (flags.FlagsAnd.HasValue) Flags &= flags.FlagsAnd.Value;
                if (flags.FlagsOr.HasValue) Flags |= flags.FlagsOr.Value;
                return 1;

            case ParamsForeignData fd:
                if (fd.ForeignData != null) ForeignData = fd.ForeignData;
                if (fd.IForeignData.HasValue) ForeignDataType = fd.IForeignData.Value;
                return 1;

            default:
                return 0;
        }
    }

    public virtual int GetParams(PhysicsParamsBase parameters)
    {
        switch (parameters)
        {
            case ParamsPos pos:
                pos.Position = Position;
                pos.Orientation = Orientation;
                pos.Scale = Scale;
                pos.SimClass = (int)SimulationClass;
                return 1;

            case ParamsFlags flags:
                flags.FlagsOr = Flags;
                return 1;

            default:
                return 0;
        }
    }

    public virtual int DoAction(PhysicsActionBase action, bool threadSafe = false)
    {
        return 0; // Base entity has no actions
    }

    public virtual int GetStatus(PhysicsStatusBase status)
    {
        switch (status)
        {
            case StatusPos pos:
                pos.Position = Position;
                pos.Orientation = Orientation;
                pos.Scale = Scale;
                pos.BBoxMin = BBoxMin;
                pos.BBoxMax = BBoxMax;
                pos.SimClass = (int)SimulationClass;
                return 1;

            default:
                return 0;
        }
    }

    public virtual int AddGeometry(PhysGeometry geometry, ParamsPart parameters, int id = -1, bool threadSafe = false)
    {
        if (id < 0) id = _nextPartId++;

        var part = new EntityGeom
        {
            Id = id,
            PhysGeom = geometry,
            Offset = parameters.Position ?? PhysVector3.Zero,
            Rotation = parameters.Orientation ?? PhysQuaternion.Identity,
            Scale = parameters.Scale ?? 1f,
            Mass = parameters.Mass ?? 0f,
            SurfaceIdx = parameters.IdMaterial ?? 0
        };

        Parts.Add(part);
        ComputeBBox();
        return id;
    }

    public virtual void RemoveGeometry(int id, bool threadSafe = false)
    {
        Parts.RemoveAll(p => p.Id == id);
        ComputeBBox();
    }

    public object? GetForeignData(int type = 0)
    {
        return ForeignDataType == type ? ForeignData : null;
    }

    public int GetForeignDataType() => ForeignDataType;

    public virtual int DoStep(float timeInterval, int callerIndex = 0)
    {
        return 1; // Base: static entities don't move
    }

    public IPhysicalWorld? GetWorld() => World;

    public virtual void Awake(bool awake = true, float minTime = 0f)
    {
        IsAwake = awake;
    }

    // ============================================================================
    // Internal methods
    // ============================================================================

    /// <summary>Recompute bounding box from all parts.</summary>
    protected virtual void ComputeBBox()
    {
        if (Parts.Count == 0)
        {
            BBoxMin = BBoxMax = Position;
            return;
        }

        var min = new PhysVector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new PhysVector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var part in Parts)
        {
            if (part.PhysGeom?.Geometry == null) continue;

            var bbox = new Primitives.Box();
            part.PhysGeom.Geometry.GetBBox(ref bbox);

            // Transform to world space with proper OBB rotation (matching C++ physicalentity.cpp)
            // Combined rotation: entity orientation * part rotation
            var combinedQ = Orientation * part.Rotation;
            var combinedR = new PhysMatrix33(combinedQ);

            // Transform bbox center through part rotation, scale, and entity transform
            var localCenter = part.Offset + part.Rotation.Rotate(bbox.Center) * part.Scale;
            var partCenter = Position + Orientation.Rotate(localCenter);

            // Transform OBB to AABB: rotate basis, take abs values, multiply by half-extents
            // C++: boxMat = (abox.Basis * RT).Fabs(); sz = abox.size * boxMat * scale;
            var rotatedBasis = bbox.Basis * combinedR.Transposed();
            // Take absolute values of each element for AABB computation
            var partSize = new PhysVector3(
                (MathF.Abs(rotatedBasis.M00) * bbox.Size.X + MathF.Abs(rotatedBasis.M01) * bbox.Size.Y + MathF.Abs(rotatedBasis.M02) * bbox.Size.Z) * part.Scale * Scale,
                (MathF.Abs(rotatedBasis.M10) * bbox.Size.X + MathF.Abs(rotatedBasis.M11) * bbox.Size.Y + MathF.Abs(rotatedBasis.M12) * bbox.Size.Z) * part.Scale * Scale,
                (MathF.Abs(rotatedBasis.M20) * bbox.Size.X + MathF.Abs(rotatedBasis.M21) * bbox.Size.Y + MathF.Abs(rotatedBasis.M22) * bbox.Size.Z) * part.Scale * Scale
            );

            var partMin = partCenter - partSize;
            var partMax = partCenter + partSize;

            min = PhysVector3.Min(min, partMin);
            max = PhysVector3.Max(max, partMax);
        }

        BBoxMin = min;
        BBoxMax = max;
    }
}
