// Port of CryPhysics cylindergeom.h/cpp and capsulegeom.h/cpp
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>Cylinder collision geometry. Port of CCylinderGeom.</summary>
public class CylinderGeometry : GeometryBase
{
    public override int GeomType => GeomTypes.Cylinder;

    public Cylinder Cylinder { get; } = new();

    public CylinderGeometry() { Tree = new SingleBoxTree(); }

    public CylinderGeometry(in PhysVector3 center, in PhysVector3 axis, float radius, float halfHeight) : this()
    {
        Cylinder.Center = center;
        Cylinder.Axis = axis;
        Cylinder.Radius = radius;
        Cylinder.HalfHeight = halfHeight;
        UpdateTree();
    }

    protected void UpdateTree()
    {
        var sbt = (SingleBoxTree)Tree!;
        float r = Cylinder.Radius;
        float hh = Cylinder.HalfHeight;
        // Compute AABB from axis
        var absAxis = new PhysVector3(MathF.Abs(Cylinder.Axis.X), MathF.Abs(Cylinder.Axis.Y), MathF.Abs(Cylinder.Axis.Z));
        var halfExtent = new PhysVector3(
            MathF.Sqrt(MathF.Max(0, r * r - Cylinder.Axis.X * Cylinder.Axis.X * r * r / (absAxis.LengthSq() + 1e-20f))) + absAxis.X * hh,
            MathF.Sqrt(MathF.Max(0, r * r - Cylinder.Axis.Y * Cylinder.Axis.Y * r * r / (absAxis.LengthSq() + 1e-20f))) + absAxis.Y * hh,
            MathF.Sqrt(MathF.Max(0, r * r - Cylinder.Axis.Z * Cylinder.Axis.Z * r * r / (absAxis.LengthSq() + 1e-20f))) + absAxis.Z * hh
        );
        sbt.SetBox(Cylinder.Center, halfExtent, PhysMatrix33.Identity);
    }

    public override void GetBBox(ref Box bbox)
    {
        ((SingleBoxTree)Tree!).GetNodeBV(ref _tempBV, 0);
        bbox.Center = _tempBV.BBox!.Center;
        bbox.Size = _tempBV.BBox.Size;
        bbox.Basis = PhysMatrix33.Identity;
        bbox.IsOriented = false;
    }

    private BoundingVolume _tempBV = new();

    public override float GetVolume() => MathF.PI * Cylinder.Radius * Cylinder.Radius * Cylinder.HalfHeight * 2f;

    public override PhysVector3 GetCenter() => Cylinder.Center;

    public override PhysicalProperties CalcPhysicalProperties()
    {
        float r = Cylinder.Radius, h = Cylinder.HalfHeight * 2f;
        float v = GetVolume();
        // C++ stores V-weighted inertia
        float Ix = (3f * r * r + h * h) / 12f * v;
        float Iz = r * r * 0.5f * v;
        return new PhysicalProperties
        {
            Volume = v,
            CenterOfMass = Cylinder.Center,
            InertiaTensor = PhysMatrix33.Diagonal(Ix, Ix, Iz)
        };
    }

    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        // Project onto axis
        var d = pt - Cylinder.Center;
        float axisProj = d.Dot(Cylinder.Axis);
        axisProj = System.Math.Clamp(axisProj, -Cylinder.HalfHeight, Cylinder.HalfHeight);
        var axisPoint = Cylinder.Center + Cylinder.Axis * axisProj;
        var radial = pt - axisPoint;
        float radialDist = radial.Length();

        if (radialDist > 1e-10f)
        {
            closestPt = axisPoint + radial * (Cylinder.Radius / radialDist);
            normal = radial / radialDist;
        }
        else
        {
            closestPt = axisPoint + PhysVector3.UnitX * Cylinder.Radius; // arbitrary
            normal = PhysVector3.UnitX;
        }
        return (pt - closestPt).Length();
    }

    public override int PointInsideStatus(in PhysVector3 pt)
    {
        var d = pt - Cylinder.Center;
        float axisProj = d.Dot(Cylinder.Axis);
        if (MathF.Abs(axisProj) > Cylinder.HalfHeight) return 0;
        var radial = d - Cylinder.Axis * axisProj;
        return radial.LengthSq() <= Cylinder.Radius * Cylinder.Radius ? 1 : 0;
    }

    public override Primitives.Primitive? GetPrimitive() => Cylinder;

    // ========================================================================
    // Buoyancy & Medium Resistance (port of CCylinderGeom)
    // ========================================================================

    /// <summary>
    /// Calculate submerged volume for a cylinder intersecting a water plane.
    /// Port of CCylinderGeom::CalculateBuoyancy from cylindergeom.cpp lines 437-501.
    /// </summary>
    public override float CalculateBuoyancy(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale, out PhysVector3 massCenter)
    {
        float r = Cylinder.Radius * scale;
        float h = Cylinder.HalfHeight * scale * 2f;
        float r2 = r * r, rinv = 1.0f / r;
        var n = planeNormal;
        var center = R * Cylinder.Center * scale + offset;
        var axisZ = R * Cylinder.Axis;
        axisZ = axisZ * MathUtils.SgnNZ(axisZ.Dot(n));
        center = center - axisZ * (h * 0.5f); // move to bottom cap
        var axisY = n ^ axisZ;
        massCenter = center + axisZ * (h * 0.5f);

        if (axisY.LengthSq() < 0.0001f)
        {
            // Water plane perpendicular to cylinder axis
            float z0 = MathF.Max(0f, MathF.Min(h, (planeOrigin - center).Dot(n)));
            massCenter = center + axisZ * (z0 * 0.5f);
            return MathF.PI * r2 * z0;
        }
        axisY.Normalize();
        var axisX = axisY ^ axisZ;

        float denom = 1.0f / axisX.Dot(n);
        float a = ((planeOrigin - center).Dot(n)) * denom;

        Span<float> V = stackalloc float[4];
        Span<PhysVector3> com = stackalloc PhysVector3[4];
        int nPieces = 0;
        float z0Val = 0, z1Val;

        if (a >= r)
        {
            massCenter = PhysVector3.Zero;
            return 0;
        }
        else if (a < -r)
        {
            if (MathUtils.Sqr(axisZ.Dot(n)) < 0.0001f)
                return MathF.PI * r2 * h;
            a = r * -0.9999f;
            z0Val = ((planeOrigin - (center - axisX * r)).Dot(n)) / (axisZ.Dot(n));
            V[nPieces] = MathF.PI * r2 * z0Val;
            com[nPieces] = axisZ * (z0Val * 0.5f * V[nPieces]);
            nPieces++;
        }

        float b = ((planeOrigin - (center + axisZ * h)).Dot(n)) * denom;
        if (b < -r)
        {
            massCenter = center + axisZ * (h * 0.5f);
            return MathF.PI * r2 * h;
        }
        else if (b > r)
        {
            b = r * 0.9999f;
            z1Val = ((planeOrigin - (center + axisX * r)).Dot(n)) / (axisZ.Dot(n));
        }
        else
        {
            z1Val = h;
            float yb = MathF.Sqrt(r2 - b * b);
            V[nPieces] = (r2 * (MathF.PI * 0.5f - MathF.Asin(b * rinv)) - b * yb) * (h - z0Val);
            com[nPieces] = axisX * ((2.0f / 3f) * MathUtils.Cube(yb) * (h - z0Val))
                         + axisZ * ((h + z0Val) * 0.5f * V[nPieces]);
            nPieces++;
        }

        if (b > a + r * 0.01f)
        {
            denom = 1.0f / (b - a);
            float k = (z1Val - z0Val) * denom;
            float ya = MathF.Sqrt(r2 - a * a);
            float anglea = MathF.Asin(a * rinv);
            float yb = MathF.Sqrt(r2 - b * b);
            float angleb = MathF.Asin(b * rinv);

            V[nPieces] = k * ((b * yb + r2 * angleb) * (b - a) + (1.0f / 3f) * (MathUtils.Cube(yb) - MathUtils.Cube(ya))
                - r2 * (b * angleb - a * anglea + yb - ya));

            com[nPieces] = axisZ * (k * k * 0.5f * ((b * yb + r2 * angleb) * (b * b - a * a)
                + 0.5f * (b * MathUtils.Cube(yb) - a * MathUtils.Cube(ya))
                - r2 * (0.75f * (b * yb - a * ya) - 0.25f * r2 * (angleb - anglea) + b * b * angleb - a * a * anglea))
                + V[nPieces] * (z0Val - a * k));

            com[nPieces] = com[nPieces] + axisX * ((2.0f / 3f) * k * (0.25f * (b * MathUtils.Cube(yb) - a * MathUtils.Cube(ya))
                + (3.0f / 8f) * r2 * (b * yb - a * ya + r2 * (angleb - anglea))
                - MathUtils.Cube(yb) * (b - a)));

            nPieces++;
        }

        float Vaccum = 0;
        var comAccum = PhysVector3.Zero;
        for (int i = 0; i < nPieces; i++)
        {
            Vaccum += V[i];
            comAccum = comAccum + com[i];
        }

        if (Vaccum > 0)
            massCenter = center + comAccum / Vaccum;
        else
            Vaccum = 0;

        return Vaccum;
    }

    /// <summary>
    /// Calculate medium resistance for a cylinder by approximating with 12 side quads.
    /// Port of CCylinderGeom::CalculateMediumResistance from cylindergeom.cpp lines 548-641.
    /// Simplified: only handles the side panels (not the arc-based cap integration).
    /// </summary>
    public override void CalculateMediumResistance(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale,
        in PhysVector3 v, in PhysVector3 w, in PhysVector3 com,
        out PhysVector3 dPres, out PhysVector3 dLres)
    {
        dPres = PhysVector3.Zero;
        dLres = PhysVector3.Zero;

        float r = Cylinder.Radius * scale;
        float hh = Cylinder.HalfHeight * scale;
        var n = R * (-Cylinder.Axis);
        var rotax = n ^ PhysVector3.UnitZ;
        float sina = rotax.Length();
        if (sina > 0.001f)
            rotax = rotax * (1f / sina);
        else
            rotax = PhysVector3.UnitX;
        float cosa = n.Z;
        var center = R * Cylinder.Center * scale + offset + n * hh;

        float planeD = planeOrigin.Dot(planeNormal);
        Span<PhysVector3> ptside = stackalloc PhysVector3[4];

        // 12 side panels at 30-degree increments
        // sqrt(3)/2 rotation
        float sqrt3 = MathUtils.Sqrt3;
        float x0 = r, y0 = 0f;
        float x1 = 0.965925826f, y1 = 0.258819045f; // cos/sin 15 degrees (normal midpoint)

        for (int i = 0; i < 12; i++)
        {
            ptside[1] = new PhysVector3(x0, y0, 0).GetRotated(rotax, cosa, -sina) + center;
            float dx = x0;
            x0 = (x0 * sqrt3 - y0) * 0.5f;
            y0 = (y0 * sqrt3 + dx) * 0.5f;
            ptside[0] = new PhysVector3(x0, y0, 0).GetRotated(rotax, cosa, -sina) + center;
            ptside[2] = ptside[1] - n * (hh * 2f);
            ptside[3] = ptside[0] - n * (hh * 2f);

            var faceN = new PhysVector3(x1, y1, 0).GetRotated(rotax, cosa, -sina);
            MathUtils.CalcMediumResistance(ptside, 4, faceN, planeNormal, planeD,
                v, w, com, ref dPres, ref dLres);

            dx = x1;
            x1 = (x1 * sqrt3 - y1) * 0.5f;
            y1 = (y1 * sqrt3 + dx) * 0.5f;
        }
    }
}

/// <summary>Capsule collision geometry (cylinder + hemispherical caps). Port of CCapsuleGeom.</summary>
public class CapsuleGeometry : CylinderGeometry
{
    public override int GeomType => GeomTypes.Capsule;

    public CapsuleGeometry() { }

    public CapsuleGeometry(in PhysVector3 center, in PhysVector3 axis, float radius, float halfHeight)
        : base(center, axis, radius, halfHeight) { }

    public override float GetVolume()
    {
        float r = Cylinder.Radius, hh = Cylinder.HalfHeight;
        return MathF.PI * r * r * (hh * 2f + (4f / 3f) * r);
    }

    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        var d = pt - Cylinder.Center;
        float axisProj = d.Dot(Cylinder.Axis);
        axisProj = System.Math.Clamp(axisProj, -Cylinder.HalfHeight, Cylinder.HalfHeight);
        var sphereCenter = Cylinder.Center + Cylinder.Axis * axisProj;
        var diff = pt - sphereCenter;
        float dist = diff.Length();

        if (dist > 1e-10f)
        {
            normal = diff / dist;
            closestPt = sphereCenter + normal * Cylinder.Radius;
        }
        else
        {
            normal = PhysVector3.UnitZ;
            closestPt = sphereCenter + PhysVector3.UnitZ * Cylinder.Radius;
        }
        return MathF.Abs(dist - Cylinder.Radius);
    }

    /// <summary>
    /// Capsule mass properties (cylinder body + 2 hemispherical caps).
    /// Literal port of CCapsuleGeom::CalcPhysicalProperties from capsulegeom.cpp:51-61.
    /// </summary>
    public override PhysicalProperties CalcPhysicalProperties()
    {
        // Vcap = (4/3)*PI*r^3 (full sphere from joining the two hemispheres)
        float Vcap = (4.0f / 3f) * MathF.PI * MathUtils.Cube(Cylinder.Radius);
        float V = MathUtils.Sqr(Cylinder.Radius) * Cylinder.HalfHeight * (MathF.PI * 2f) + Vcap;
        float r2 = MathUtils.Sqr(Cylinder.Radius);
        float x2 = MathF.PI * Cylinder.HalfHeight * MathUtils.Sqr(r2) * 0.5f;
        float z2 = MathF.PI * r2 * MathUtils.Cube(Cylinder.HalfHeight) * (2.0f / 3f);
        float ix = x2 + z2 + Vcap * (r2 * 0.4f + MathUtils.Sqr(Cylinder.HalfHeight));
        float iz = x2 * 2f + Vcap * r2 * 0.4f;
        return new PhysicalProperties
        {
            Volume = V,
            CenterOfMass = Cylinder.Center,
            InertiaTensor = PhysMatrix33.Diagonal(ix, ix, iz)
        };
    }

    /// <summary>
    /// Capsule inside test: hemispherical caps for the ends, cylindrical for the middle.
    /// Literal port of CCapsuleGeom::PointInsideStatus from capsulegeom.cpp:64-70.
    /// </summary>
    public override int PointInsideStatus(in PhysVector3 pt)
    {
        var ptr = pt - Cylinder.Center;
        var ptc = ptr;
        float h = Cylinder.Axis.Dot(ptr);
        ptr = ptr - Cylinder.Axis * h;
        ptc = ptc - Cylinder.Axis * (Cylinder.HalfHeight * MathUtils.SgnNZ(h));
        // isneg(min(ptc.len2-r^2, max(ptr.len2-r^2, |h|-hh)))
        float capTerm = ptc.LengthSq() - MathUtils.Sqr(Cylinder.Radius);
        float cylTerm = MathF.Max(ptr.LengthSq() - MathUtils.Sqr(Cylinder.Radius),
                                  MathF.Abs(h) - Cylinder.HalfHeight);
        return MathF.Min(capTerm, cylTerm) < 0 ? 1 : 0;
    }

    /// <summary>
    /// Mark intersection-test scratch buffers as carrying capsule primitives.
    /// Literal port of CCapsuleGeom::PrepareForIntersectionTest from capsulegeom.cpp:73-79.
    /// </summary>
    public override void PrepareForIntersectionTest(GeometryUnderTest pGTest, GeometryBase pCollider,
        GeometryUnderTest pGTestColl, bool bKeepPrevContacts)
    {
        base.PrepareForIntersectionTest(pGTest, pCollider, pGTestColl, bKeepPrevContacts);
        pGTest.TypePrim = Capsule.Type;
    }
}
