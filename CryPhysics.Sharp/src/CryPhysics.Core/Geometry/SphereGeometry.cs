// Port of CryPhysics spheregeom.h/cpp - sphere collision geometry
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Sphere collision geometry. Port of CSphereGeom from CryEngine.
/// </summary>
public class SphereGeometry : GeometryBase
{
    public override int GeomType => GeomTypes.Sphere;

    public Sphere Sphere { get; } = new();

    public SphereGeometry() { Tree = new SingleBoxTree(); }

    public SphereGeometry(in PhysVector3 center, float radius) : this()
    {
        Sphere.Center = center;
        Sphere.Radius = radius;
        UpdateTree();
    }

    private void UpdateTree()
    {
        var sbt = (SingleBoxTree)Tree!;
        float r = Sphere.Radius;
        sbt.SetBox(Sphere.Center, new PhysVector3(r, r, r), PhysMatrix33.Identity);
    }

    public override void GetBBox(ref Box bbox)
    {
        float r = Sphere.Radius;
        bbox.Center = Sphere.Center;
        bbox.Size = new PhysVector3(r, r, r);
        bbox.Basis = PhysMatrix33.Identity;
        bbox.IsOriented = false;
    }

    public override float GetVolume() => (4f / 3f) * MathF.PI * MathUtils.Cube(Sphere.Radius);

    public override PhysVector3 GetCenter() => Sphere.Center;

    public override PhysicalProperties CalcPhysicalProperties()
    {
        float r = Sphere.Radius;
        float v = GetVolume();
        float I = 0.4f * r * r * v; // C++ stores V-weighted: 2/5 * r^2 * V
        return new PhysicalProperties
        {
            Volume = v,
            CenterOfMass = Sphere.Center,
            InertiaTensor = PhysMatrix33.Diagonal(I, I, I)
        };
    }

    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        var diff = pt - Sphere.Center;
        float dist = diff.Length();
        if (dist > 1e-10f)
        {
            normal = diff / dist;
            closestPt = Sphere.Center + normal * Sphere.Radius;
        }
        else
        {
            normal = PhysVector3.UnitZ;
            closestPt = Sphere.Center + PhysVector3.UnitZ * Sphere.Radius;
        }
        return MathF.Abs(dist - Sphere.Radius);
    }

    public override int PointInsideStatus(in PhysVector3 pt)
    {
        return (pt - Sphere.Center).LengthSq() <= Sphere.Radius * Sphere.Radius ? 1 : 0;
    }

    public override Primitives.Primitive? GetPrimitive() => Sphere;

    // ========================================================================
    // Buoyancy & Medium Resistance (port of CSphereGeom)
    // ========================================================================

    /// <summary>
    /// Calculate submerged volume for a sphere intersecting a water plane.
    /// Port of CSphereGeom::CalculateBuoyancy from spheregeom.cpp lines 162-175.
    /// Analytical spherical cap formula.
    /// </summary>
    public override float CalculateBuoyancy(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale, out PhysVector3 massCenter)
    {
        float r = Sphere.Radius * scale;
        var n = planeNormal;
        var center = R * Sphere.Center * scale + offset;
        massCenter = center;

        // x = signed distance from sphere center to plane (positive = center is below plane)
        float x = (planeOrigin - center).Dot(n);

        if (x < -r)
            return 0; // entirely above water

        if (x > r)
            return (4.0f / 3f) * MathF.PI * MathUtils.Cube(r); // entirely submerged

        // Spherical cap mass center offset
        massCenter = center + n * (MathF.PI * 0.5f * (x * x * (r * r - x * x * 0.5f) - r * r * r * r * 0.5f) - x);
        // Spherical cap volume
        return MathF.PI * ((2.0f / 3f) * MathUtils.Cube(r) + x * (r * r - x * x * (1.0f / 3f)));
    }

    /// <summary>
    /// Calculate medium resistance for a sphere.
    /// Port of CSphereGeom::CalculateMediumResistance from spheregeom.cpp lines 178-216.
    /// </summary>
    public override void CalculateMediumResistance(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale,
        in PhysVector3 vIn, in PhysVector3 wIn, in PhysVector3 com,
        out PhysVector3 dPres, out PhysVector3 dLres)
    {
        var center = R * Sphere.Center * scale + offset;
        float r = Sphere.Radius * scale;
        var n = planeNormal;
        var vel = vIn + (wIn ^ (center - com));
        float x = (planeOrigin - center).Dot(n);

        if (MathF.Abs(x) > r)
        {
            dLres = PhysVector3.Zero;
            dPres = PhysVector3.Zero;
            if (x > r)
                dPres = vel * (-MathF.PI * r * r);
            return;
        }

        var vn = vel.LengthSq() > 1e-20f ? vel.Normalized() : PhysVector3.UnitZ;
        var axisy = vn ^ n;
        float vxn = axisy.Length();
        float l;

        if (vxn < 0.01f)
        {
            axisy = vn.GetOrthogonal().Normalized();
            l = MathUtils.SgnNZ(x) * r;
        }
        else
        {
            axisy = axisy * (1f / vxn);
            l = x / vxn;
        }

        var axisx = axisy ^ vn;
        float nv = n.Dot(vel);
        float cx = x * vxn;
        float ry = MathF.Max(0.001f, MathF.Sqrt(MathF.Max(0f, r * r - x * x)));

        float lx = MathF.Max(-r * 0.999f, MathF.Min(r * 0.999f, l));
        float ly = MathF.Sqrt(MathF.Max(0f, r * r - lx * lx));
        float circS = (lx * ly + r * r * (MathF.PI * 0.5f + MathF.Asin(lx / r))) * 0.5f;
        float circX = MathUtils.Cube(ly) * (-1.0f / 3f);

        lx = MathF.Max(-ry * 0.999f, MathF.Min(ry * 0.999f, (l - cx) / MathF.Max(0.001f, MathF.Abs(nv))));
        ly = MathF.Sqrt(MathF.Max(0f, ry * ry - lx * lx));
        float ellS = (lx * ly + ry * ry * (MathF.PI * 0.5f + MathF.Asin(lx / ry) * MathUtils.SgnNZ(nv))) * MathF.Abs(nv) * 0.5f;
        float ellX = MathUtils.Cube(ly) * (-1.0f / 3f) * nv + cx;

        float S = circS + ellS;
        float xCenter = (circX * circS + ellX * ellS) / S;

        dPres = -vel * S;
        center = center + axisx * xCenter + vn * MathF.Sqrt(MathF.Max(0f, r * r - xCenter * xCenter));
        dLres = (center - com) ^ dPres;
    }
}
