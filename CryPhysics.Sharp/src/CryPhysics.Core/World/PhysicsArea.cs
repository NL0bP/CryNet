// Port of CryPhysics physarea.cpp - CPhysArea
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Params;

namespace CryPhysics.World;

/// <summary>
/// Area shape type. Port of area_type from physinterface.h.
/// Spline areas are simplified to box for this port.
/// </summary>
public enum AreaType
{
    Box = 0,
    Sphere = 1
}

/// <summary>
/// Defines a volume with custom gravity, buoyancy, and damping overrides.
/// Port of CPhysArea from CryEngine physarea.cpp.
///
/// Areas are axis-aligned boxes or spheres that override environment parameters
/// for entities inside them. The C++ original supports spline-based areas and
/// mesh-based areas as well, but this port covers box and sphere shapes.
/// </summary>
public class PhysicsArea
{
    // ============================================================================
    // Shape definition
    // ============================================================================

    /// <summary>Shape type (box or sphere).</summary>
    public AreaType ShapeType { get; set; } = AreaType.Box;

    /// <summary>Center of the area volume.</summary>
    public PhysVector3 Center { get; set; }

    /// <summary>Half-extents for box areas.</summary>
    public PhysVector3 HalfExtents { get; set; } = new(5f, 5f, 5f);

    /// <summary>Radius for sphere areas.</summary>
    public float Radius { get; set; } = 5f;

    // ============================================================================
    // Physics overrides (port of CPhysArea member variables)
    // ============================================================================

    /// <summary>
    /// Custom gravity vector inside this area. Null means use world default.
    /// Port of m_gravity in CPhysArea.
    /// </summary>
    public PhysVector3? Gravity { get; set; }

    /// <summary>
    /// Whether gravity is uniform throughout the volume.
    /// Port of m_bUniformGravity flag.
    /// </summary>
    public bool UniformGravity { get; set; } = true;

    /// <summary>
    /// Damping override inside this area. Null means no override.
    /// Port of m_damping in CPhysArea.
    /// </summary>
    public float? Damping { get; set; }

    /// <summary>
    /// Falloff distance from the area boundary (for smooth transitions).
    /// Port of m_falloff in CPhysArea. 0 = hard boundary.
    /// </summary>
    public float Falloff { get; set; }

    /// <summary>
    /// Priority for overlapping areas. Higher priority overrides lower.
    /// Port of m_iPriority in CPhysArea.
    /// </summary>
    public int Priority { get; set; }

    // ============================================================================
    // Buoyancy parameters (port of CPhysArea water-related fields)
    // ============================================================================

    /// <summary>
    /// Whether this area acts as a water volume.
    /// Port of m_bWaterVolume flag from CPhysArea.
    /// </summary>
    public bool IsWaterVolume { get; set; }

    /// <summary>
    /// Water density (kg/m^3). Typical value: 1000 for water.
    /// Port of m_waterDensity.
    /// </summary>
    public float WaterDensity { get; set; } = 1000f;

    /// <summary>
    /// Water damping factor applied to entities in the volume.
    /// Port of m_waterDamping.
    /// </summary>
    public float WaterDamping { get; set; }

    /// <summary>
    /// Water resistance applied to moving entities.
    /// Port of m_waterResistance.
    /// </summary>
    public float WaterResistance { get; set; }

    /// <summary>
    /// Water flow velocity vector (current).
    /// Port of m_waterFlow.
    /// </summary>
    public PhysVector3 WaterFlow { get; set; }

    /// <summary>
    /// Water plane normal (direction of "up" relative to water surface).
    /// Port of m_waterPlane.n.
    /// </summary>
    public PhysVector3 WaterPlaneNormal { get; set; } = PhysVector3.UnitZ;

    /// <summary>
    /// Water plane origin (a point on the water surface).
    /// Port of m_waterPlane.origin.
    /// </summary>
    public PhysVector3 WaterPlaneOrigin { get; set; }

    /// <summary>
    /// Minimum energy threshold for water (entities below this energy sleep in water).
    /// Port of m_waterEmin.
    /// </summary>
    public float WaterEmin { get; set; }

    // ============================================================================
    // State
    // ============================================================================

    /// <summary>Whether this area is currently active.</summary>
    public bool Enabled { get; set; } = true;

    // ============================================================================
    // Containment tests (port of CPhysArea::CheckPoint / CheckBBox)
    // ============================================================================

    /// <summary>
    /// Test whether a point is inside this area.
    /// Port of CPhysArea::CheckPoint from physarea.cpp.
    /// Returns the blend factor [0..1] where 1 = fully inside, 0 = outside.
    /// Values between 0 and 1 occur in the falloff region.
    /// </summary>
    public float CheckPoint(in PhysVector3 pt)
    {
        if (!Enabled) return 0f;

        float dist;

        switch (ShapeType)
        {
            case AreaType.Box:
            {
                // Signed distance from box boundary (negative = inside)
                var d = pt - Center;
                float dx = MathF.Abs(d.X) - HalfExtents.X;
                float dy = MathF.Abs(d.Y) - HalfExtents.Y;
                float dz = MathF.Abs(d.Z) - HalfExtents.Z;
                dist = MathF.Max(dx, MathF.Max(dy, dz));
                break;
            }
            case AreaType.Sphere:
            {
                dist = (pt - Center).Length() - Radius;
                break;
            }
            default:
                return 0f;
        }

        if (dist > 0f) return 0f;       // outside
        if (Falloff <= 0f) return 1f;    // hard boundary, fully inside

        // Smooth blend in falloff region
        float penetration = -dist;
        if (penetration >= Falloff) return 1f;
        return penetration / Falloff;
    }

    /// <summary>
    /// Test whether an AABB overlaps this area.
    /// Port of CPhysArea::CheckBBox from physarea.cpp (simplified).
    /// Returns true if there is any overlap.
    /// </summary>
    public bool CheckBBox(in PhysVector3 bboxMin, in PhysVector3 bboxMax)
    {
        if (!Enabled) return false;

        switch (ShapeType)
        {
            case AreaType.Box:
            {
                var areaMin = Center - HalfExtents;
                var areaMax = Center + HalfExtents;
                return bboxMax.X >= areaMin.X && bboxMin.X <= areaMax.X &&
                       bboxMax.Y >= areaMin.Y && bboxMin.Y <= areaMax.Y &&
                       bboxMax.Z >= areaMin.Z && bboxMin.Z <= areaMax.Z;
            }
            case AreaType.Sphere:
            {
                // Closest point on AABB to sphere center
                float cx = MathF.Max(bboxMin.X, MathF.Min(Center.X, bboxMax.X));
                float cy = MathF.Max(bboxMin.Y, MathF.Min(Center.Y, bboxMax.Y));
                float cz = MathF.Max(bboxMin.Z, MathF.Min(Center.Z, bboxMax.Z));
                float dist2 = (cx - Center.X) * (cx - Center.X) +
                              (cy - Center.Y) * (cy - Center.Y) +
                              (cz - Center.Z) * (cz - Center.Z);
                float r = Radius + Falloff;
                return dist2 <= r * r;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Build a ParamsBuoyancy from this area's water parameters.
    /// Used when an entity is found inside a water area.
    /// </summary>
    public ParamsBuoyancy ToBuoyancyParams()
    {
        return new ParamsBuoyancy
        {
            WaterPlaneNormal = WaterPlaneNormal,
            WaterPlaneOrigin = WaterPlaneOrigin,
            WaterDensity = WaterDensity,
            WaterDamping = WaterDamping,
            WaterResistance = WaterResistance,
            WaterFlow = WaterFlow,
            WaterEmin = WaterEmin
        };
    }
}
