// Port of CryPhysics raygeom.h/cpp
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>Ray collision geometry. Port of CRayGeom.</summary>
public class RayGeometry : GeometryBase
{
    public override int GeomType => GeomTypes.Ray;

    public Ray Ray { get; } = new();

    public RayGeometry() { Tree = new SingleBoxTree(); }

    public RayGeometry(in PhysVector3 origin, in PhysVector3 dir) : this()
    {
        Ray.Origin = origin;
        Ray.Dir = dir;
        UpdateTree();
    }

    private void UpdateTree()
    {
        var sbt = (SingleBoxTree)Tree!;
        var end = Ray.Origin + Ray.Dir;
        var min = PhysVector3.Min(Ray.Origin, end);
        var max = PhysVector3.Max(Ray.Origin, end);
        sbt.SetBox((min + max) * 0.5f, (max - min) * 0.5f, PhysMatrix33.Identity);
    }

    public override void GetBBox(ref Box bbox)
    {
        var end = Ray.Origin + Ray.Dir;
        var min = PhysVector3.Min(Ray.Origin, end);
        var max = PhysVector3.Max(Ray.Origin, end);
        bbox.Center = (min + max) * 0.5f;
        bbox.Size = (max - min) * 0.5f;
        bbox.Basis = PhysMatrix33.Identity;
        bbox.IsOriented = false;
    }

    public override float GetVolume() => 0f;
    public override PhysVector3 GetCenter() => Ray.Origin + Ray.Dir * 0.5f;

    public override PhysicalProperties CalcPhysicalProperties() => new()
    {
        Volume = 0,
        CenterOfMass = GetCenter(),
        InertiaTensor = PhysMatrix33.Zero
    };

    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        float dirLen2 = Ray.Dir.LengthSq();
        float t = dirLen2 > 1e-20f ? (pt - Ray.Origin).Dot(Ray.Dir) / dirLen2 : 0f;
        t = System.Math.Clamp(t, 0f, 1f);
        closestPt = Ray.Origin + Ray.Dir * t;
        var diff = pt - closestPt;
        float dist = diff.Length();
        normal = dist > 1e-10f ? diff / dist : PhysVector3.UnitZ;
        return dist;
    }

    public override int PointInsideStatus(in PhysVector3 pt) => 0; // Rays have no volume

    public override Primitives.Primitive? GetPrimitive() => Ray;
}
