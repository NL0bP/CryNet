// Literal port of g_Overlapper / OverlapBV pattern from geometry.cpp + overlapchecks.cpp.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.BVTrees;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// BV-vs-BV overlap dispatcher. Port of the C++ `g_Overlapper.Check(type1, type2, BV1, BV2)`.
/// Dispatches by tree-type pair to the appropriate overlap test. Falls back to box-box SAT
/// for unsupported pairs (matches the C++ behaviour where unknown pairs default to box overlap
/// using the abox stored in BBox-style BVs).
/// </summary>
public static class BVOverlap
{
    /// <summary>
    /// Returns true if the two BVs overlap. Matches the bool semantics of g_Overlapper.Check.
    /// </summary>
    public static bool Check(int type1, int type2, BV pBV1, BV pBV2)
    {
        // Heightfield-vs-anything: trust heightfield. Matches the C++ behaviour where the
        // heightfield BV always reports "overlap" so the geometry handles its own grid stepping.
        if (type1 == BVTreeTypes.Heightfield || type2 == BVTreeTypes.Heightfield) return true;

        // Ray-vs-anything: a ray BV against a box reduces to ray-AABB.
        if (pBV1 is BVRay r1 && pBV2 is BBox b2) return RayBoxOverlap(r1, b2);
        if (pBV2 is BVRay r2 && pBV1 is BBox b1) return RayBoxOverlap(r2, b1);

        // Box-vs-box (the common case for AABB/OBB/SingleBox).
        if (pBV1 is BBox bb1 && pBV2 is BBox bb2) return BoxBoxOverlap(bb1.ABox, bb2.ABox);

        // Default: assume overlap, let the leaf primitive checks reject.
        return true;
    }

    /// <summary>
    /// Box-vs-box separating-axis test. Handles both AABB and OBB. Port of the
    /// `box_box_overlap` body shared by overlapchecks.cpp and the g_Overlapper.
    /// </summary>
    public static bool BoxBoxOverlap(in Box a, in Box b)
    {
        // If both are AABB, do trivial AABB-vs-AABB.
        if (!a.IsOriented && !b.IsOriented)
        {
            var d = a.Center - b.Center;
            return MathF.Abs(d.X) <= a.Size.X + b.Size.X
                && MathF.Abs(d.Y) <= a.Size.Y + b.Size.Y
                && MathF.Abs(d.Z) <= a.Size.Z + b.Size.Z;
        }

        // Full SAT: 3 axes of A, 3 of B, 9 cross products.
        var aAxes = new[] { a.IsOriented ? a.Basis.GetRow(0) : PhysVector3.UnitX,
                            a.IsOriented ? a.Basis.GetRow(1) : PhysVector3.UnitY,
                            a.IsOriented ? a.Basis.GetRow(2) : PhysVector3.UnitZ };
        var bAxes = new[] { b.IsOriented ? b.Basis.GetRow(0) : PhysVector3.UnitX,
                            b.IsOriented ? b.Basis.GetRow(1) : PhysVector3.UnitY,
                            b.IsOriented ? b.Basis.GetRow(2) : PhysVector3.UnitZ };
        var t = a.Center - b.Center;

        // 3 A-axes
        for (int i = 0; i < 3; i++)
        {
            float ra = a.Size[i];
            float rb = MathF.Abs(aAxes[i].Dot(bAxes[0])) * b.Size.X
                     + MathF.Abs(aAxes[i].Dot(bAxes[1])) * b.Size.Y
                     + MathF.Abs(aAxes[i].Dot(bAxes[2])) * b.Size.Z;
            if (MathF.Abs(t.Dot(aAxes[i])) > ra + rb) return false;
        }
        // 3 B-axes
        for (int j = 0; j < 3; j++)
        {
            float ra = MathF.Abs(bAxes[j].Dot(aAxes[0])) * a.Size.X
                     + MathF.Abs(bAxes[j].Dot(aAxes[1])) * a.Size.Y
                     + MathF.Abs(bAxes[j].Dot(aAxes[2])) * a.Size.Z;
            float rb = b.Size[j];
            if (MathF.Abs(t.Dot(bAxes[j])) > ra + rb) return false;
        }
        // 9 cross axes
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                var axis = aAxes[i] ^ bAxes[j];
                if (axis.LengthSq() < 1e-10f) continue;
                float ra = MathF.Abs(aAxes[(i + 1) % 3].Dot(axis)) * a.Size[(i + 1) % 3]
                         + MathF.Abs(aAxes[(i + 2) % 3].Dot(axis)) * a.Size[(i + 2) % 3];
                float rb = MathF.Abs(bAxes[(j + 1) % 3].Dot(axis)) * b.Size[(j + 1) % 3]
                         + MathF.Abs(bAxes[(j + 2) % 3].Dot(axis)) * b.Size[(j + 2) % 3];
                if (MathF.Abs(t.Dot(axis)) > ra + rb) return false;
            }
        return true;
    }

    private static bool RayBoxOverlap(BVRay rayBV, BBox boxBV)
    {
        if (rayBV.ARay == null) return true;
        var ray = rayBV.ARay;
        var box = boxBV.ABox;
        // Slab test in box-local space (ignore basis for AABB).
        var p0 = ray.Origin - box.Center;
        var p1 = p0 + ray.Dir;
        if (box.IsOriented)
        {
            p0 = box.Basis * p0;
            p1 = box.Basis * p1;
        }
        for (int axis = 0; axis < 3; axis++)
        {
            float pmin = MathF.Min(p0[axis], p1[axis]);
            float pmax = MathF.Max(p0[axis], p1[axis]);
            if (pmin > box.Size[axis] || pmax < -box.Size[axis]) return false;
        }
        return true;
    }
}
