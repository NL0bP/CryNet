// Port of CryPhysics boxgeom.h/cpp - box collision geometry
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Box collision geometry. Port of CBoxGeom from CryEngine.
/// </summary>
public class BoxGeometry : GeometryBase
{
    public override int GeomType => GeomTypes.Box;

    public Box Box { get; } = new();

    public BoxGeometry() { Tree = new SingleBoxTree(); }

    public BoxGeometry(in PhysVector3 center, in PhysVector3 size, in PhysMatrix33 basis) : this()
    {
        Box.Center = center;
        Box.Size = size;
        Box.Basis = basis;
        Box.IsOriented = true;
        UpdateTree();
    }

    private void UpdateTree()
    {
        ((SingleBoxTree)Tree!).SetBox(Box);
    }

    public override void GetBBox(ref Box bbox)
    {
        bbox.Center = Box.Center;
        bbox.Size = Box.Size;
        bbox.Basis = Box.Basis;
        bbox.IsOriented = Box.IsOriented;
    }

    public override float GetVolume() => 8f * Box.Size.X * Box.Size.Y * Box.Size.Z;

    public override PhysVector3 GetCenter() => Box.Center;

    public override PhysicalProperties CalcPhysicalProperties()
    {
        float v = GetVolume();
        // C++ stores Ibody = V * (halfsize^2) / 3 = V * fullsize^2 / 12 (volume-weighted inertia)
        float sx2 = MathUtils.Sqr(Box.Size.X * 2f);
        float sy2 = MathUtils.Sqr(Box.Size.Y * 2f);
        float sz2 = MathUtils.Sqr(Box.Size.Z * 2f);
        return new PhysicalProperties
        {
            Volume = v,
            CenterOfMass = Box.Center,
            InertiaTensor = PhysMatrix33.Diagonal(
                (sy2 + sz2) / 12f * v,
                (sx2 + sz2) / 12f * v,
                (sx2 + sy2) / 12f * v
            )
        };
    }

    public override float FindClosestPoint(in PhysVector3 pt, out PhysVector3 closestPt, out PhysVector3 normal)
    {
        var localPt = Box.Basis * (pt - Box.Center);
        var clamped = new PhysVector3(
            System.Math.Clamp(localPt.X, -Box.Size.X, Box.Size.X),
            System.Math.Clamp(localPt.Y, -Box.Size.Y, Box.Size.Y),
            System.Math.Clamp(localPt.Z, -Box.Size.Z, Box.Size.Z)
        );

        closestPt = Box.Center + Box.Basis.Transposed() * clamped;
        var diff = pt - closestPt;
        float dist = diff.Length();
        normal = dist > 1e-10f ? diff / dist : PhysVector3.UnitZ;
        return dist;
    }

    public override int PointInsideStatus(in PhysVector3 pt)
    {
        var localPt = Box.Basis * (pt - Box.Center);
        return (MathF.Abs(localPt.X) <= Box.Size.X &&
                MathF.Abs(localPt.Y) <= Box.Size.Y &&
                MathF.Abs(localPt.Z) <= Box.Size.Z) ? 1 : 0;
    }

    public override Primitives.Primitive? GetPrimitive() => Box;

    // ========================================================================
    // Buoyancy & Medium Resistance (port of CBoxGeom)
    // ========================================================================

    /// <summary>
    /// Calculate buoyancy by building a tri-mesh from box faces and delegating.
    /// Port of CBoxGeom::CalculateBuoyancy from boxgeom.cpp lines 469-473.
    /// </summary>
    public override float CalculateBuoyancy(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale, out PhysVector3 massCenter)
    {
        var mesh = BuildTriMeshFromBox();
        return mesh.CalculateBuoyancy(planeNormal, planeOrigin, R, offset, scale, out massCenter);
    }

    /// <summary>
    /// Calculate medium resistance by iterating 6 faces.
    /// Port of CBoxGeom::CalculateMediumResistance from boxgeom.cpp lines 475-496.
    /// </summary>
    public override void CalculateMediumResistance(in PhysVector3 planeNormal, in PhysVector3 planeOrigin,
        in PhysMatrix33 R, in PhysVector3 offset, float scale,
        in PhysVector3 v, in PhysVector3 w, in PhysVector3 com,
        out PhysVector3 dPres, out PhysVector3 dLres)
    {
        dPres = PhysVector3.Zero;
        dLres = PhysVector3.Zero;

        var size = Box.Size * scale;
        var boxOffset = R * (Box.IsOriented ? Box.Basis.Transposed() * Box.Center : Box.Center) * scale + offset;
        PhysMatrix33 boxR;
        if (!Box.IsOriented)
            boxR = R;
        else
            boxR = R * Box.Basis.Transposed();

        float planeD = planeOrigin.Dot(planeNormal);
        Span<PhysVector3> pt = stackalloc PhysVector3[4];

        for (int i = 0; i < 6; i++)
        {
            int iz = i >> 1;
            int ix = MathUtils.IncMod3[iz];
            int iy = MathUtils.DecMod3[iz];
            float nSign = (i & 1) * 2 - 1; // -1 for even faces, +1 for odd

            var localN = PhysVector3.Zero;
            localN[iz] = nSign;
            var n = boxR * localN;

            for (int j = 0; j < 4; j++)
            {
                var localPt = PhysVector3.Zero;
                localPt[ix] = size[ix] * (1 - ((j ^ (j << 1)) & 2));
                localPt[iy] = size[iy] * (((j & 2) ^ ((i << 1) & 2)) - 1);
                localPt[iz] = size[iz] * nSign;
                pt[j] = boxR * localPt + boxOffset;
            }

            MathUtils.CalcMediumResistance(pt, 4, n, planeNormal, planeD,
                v, w, com, ref dPres, ref dLres);
        }
    }

    /// <summary>
    /// Build a TriMeshGeometry from the box's 6 faces (12 triangles).
    /// Used by CalculateBuoyancy to delegate to the tri-mesh implementation.
    /// </summary>
    private TriMeshGeometry BuildTriMeshFromBox()
    {
        // 8 vertices of the box in local space
        var s = Box.Size;
        var c = Box.Center;
        PhysMatrix33 basisT = Box.IsOriented ? Box.Basis.Transposed() : PhysMatrix33.Identity;

        var verts = new PhysVector3[8];
        int vi = 0;
        for (int sz = -1; sz <= 1; sz += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var local = new PhysVector3(s.X * sx, s.Y * sy, s.Z * sz);
            verts[vi++] = basisT * local + c;
        }

        // 12 triangles (2 per face), vertices indexed as:
        // sx=-1: 0,2,6,4  sx=+1: 1,5,7,3
        // sy=-1: 0,4,5,1  sy=+1: 2,3,7,6
        // sz=-1: 0,1,3,2  sz=+1: 4,6,7,5
        var indices = new int[]
        {
            // -X face (0,2,6), (0,6,4)
            0, 2, 6,  0, 6, 4,
            // +X face (1,5,7), (1,7,3)
            1, 5, 7,  1, 7, 3,
            // -Y face (0,4,5), (0,5,1)
            0, 4, 5,  0, 5, 1,
            // +Y face (2,3,7), (2,7,6)
            2, 3, 7,  2, 7, 6,
            // -Z face (0,1,3), (0,3,2)
            0, 1, 3,  0, 3, 2,
            // +Z face (4,6,7), (4,7,5)
            4, 6, 7,  4, 7, 5,
        };

        var mesh = new TriMeshGeometry();
        mesh.SetData(verts, indices, 12);
        return mesh;
    }
}
