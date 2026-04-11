// Port of CryPhysics overlapchecks.h/.cpp - dispatch table for primitive overlap tests
// Original: Copyright Crytek GMBH, used under license

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Collision;

/// <summary>
/// Delegate for primitive overlap test functions.
/// Returns non-zero if primitives overlap.
/// </summary>
public delegate int OverlapCheckFunc(Primitive prim1, Primitive prim2, OverlapChecker? checker);

/// <summary>
/// Dispatch table and state for overlap tests.
/// Port of COverlapChecker from CryEngine.
/// </summary>
public class OverlapChecker
{
    public static readonly OverlapChecker Instance = new();

    private readonly OverlapCheckFunc?[,] _table;

    // Cached state for consecutive checks on same pair type
    public int PrevCode = -1;
    public PhysMatrix33 Basis21;
    public PhysMatrix33 Basis21Abs;

    public OverlapChecker()
    {
        int n = PrimitiveConstants.NPrims;
        _table = new OverlapCheckFunc?[n, n];

        Register(Box.Type, Box.Type, OverlapTests.BoxBox);
        Register(Box.Type, Triangle.Type, OverlapTests.BoxTri);
        Register(Triangle.Type, Box.Type, OverlapTests.TriBox);
        Register(Box.Type, Ray.Type, OverlapTests.BoxRay);
        Register(Ray.Type, Box.Type, OverlapTests.RayBox);
        Register(Box.Type, Sphere.Type, OverlapTests.BoxSphere);
        Register(Sphere.Type, Box.Type, OverlapTests.SphereBox);
        Register(Triangle.Type, Sphere.Type, OverlapTests.TriSphere);
        Register(Sphere.Type, Triangle.Type, OverlapTests.SphereTri);
        Register(Sphere.Type, Sphere.Type, OverlapTests.SphereSphere);
        Register(Box.Type, Heightfield.GridType, OverlapTests.BoxHeightfield);
        Register(Heightfield.GridType, Box.Type, OverlapTests.HeightfieldBox);
        Register(Box.Type, VoxelGrid.Type, OverlapTests.BoxVoxelGrid);
        Register(VoxelGrid.Type, Box.Type, OverlapTests.VoxelGridBox);
        Register(Heightfield.GridType, Sphere.Type, OverlapTests.HeightfieldSphere);
        Register(Sphere.Type, Heightfield.GridType, OverlapTests.SphereHeightfield);
    }

    private void Register(int type1, int type2, OverlapCheckFunc func)
    {
        _table[type1, type2] = func;
    }

    public void Init() { PrevCode = -1; }

    public int Check(int type1, int type2, Primitive prim1, Primitive prim2)
    {
        var func = _table[type1, type2];
        return func?.Invoke(prim1, prim2, this) ?? 0;
    }

    public bool CheckExists(int type1, int type2)
    {
        return _table[type1, type2] != null;
    }
}

/// <summary>
/// Overlap test implementations.
/// Faithful port of overlapchecks.cpp.
/// </summary>
public static class OverlapTests
{
    /// <summary>Sphere-Sphere overlap. Port of sphere_sphere_overlap_check.</summary>
    public static int SphereSphere(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var s1 = (Sphere)p1;
        var s2 = (Sphere)p2;
        float rsum = s1.Radius + s2.Radius;
        return (s2.Center - s1.Center).LengthSq() < rsum * rsum ? 1 : 0;
    }

    /// <summary>Box-Box overlap: full SAT test on 15 axes. Port of box_box_overlap_check.</summary>
    public static int BoxBox(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var box1 = (Box)p1;
        var box2 = (Box)p2;
        var a = box1.Size;
        var b = box2.Size;

        PhysMatrix33 basis21, basis21Abs;
        int code = (box1.IsOriented ? 1 : 0) | (box2.IsOriented ? 1 : 0) << 16;

        if (checker != null && code != checker.PrevCode)
        {
            if (!box1.IsOriented)
                basis21 = box2.Basis.Transposed();
            else if (box2.IsOriented)
                basis21 = box1.Basis * box2.Basis.Transposed();
            else
                basis21 = box1.Basis;
            checker.PrevCode = code;
            checker.Basis21 = basis21;
            checker.Basis21Abs = basis21Abs = Fabs(basis21);
        }
        else if (checker != null)
        {
            basis21 = checker.Basis21;
            basis21Abs = checker.Basis21Abs;
        }
        else
        {
            if (!box1.IsOriented)
                basis21 = box2.Basis.Transposed();
            else if (box2.IsOriented)
                basis21 = box1.Basis * box2.Basis.Transposed();
            else
                basis21 = box1.Basis;
            basis21Abs = Fabs(basis21);
        }

        float e = (a.X + a.Y + a.Z) * 1e-4f;
        var center21 = box2.Center - box1.Center;
        if (box1.IsOriented)
            center21 = box1.Basis * center21;

        // Box1 basis axes
        if (MathF.Abs(center21.X) > a.X + b.Dot(basis21Abs.GetRow(0))) return 0;
        if (MathF.Abs(center21.Y) > a.Y + b.Dot(basis21Abs.GetRow(1))) return 0;
        if (MathF.Abs(center21.Z) > a.Z + b.Dot(basis21Abs.GetRow(2))) return 0;

        // Box2 basis axes
        if (MathF.Abs(center21.Dot(basis21.GetColumn(0))) > a.Dot(basis21Abs.GetColumn(0)) + b.X) return 0;
        if (MathF.Abs(center21.Dot(basis21.GetColumn(1))) > a.Dot(basis21Abs.GetColumn(1)) + b.Y) return 0;
        if (MathF.Abs(center21.Dot(basis21.GetColumn(2))) > a.Dot(basis21Abs.GetColumn(2)) + b.Z) return 0;

        // 9 cross product axes (edge-edge)
        float t1, t2, t3;

        // axes[0] x axes[0]
        t1 = a.Y * basis21Abs[2, 0] + a.Z * basis21Abs[1, 0];
        t2 = b.Y * basis21Abs[0, 2] + b.Z * basis21Abs[0, 1];
        t3 = center21.Z * basis21[1, 0] - center21.Y * basis21[2, 0];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[0] x axes[1]
        t1 = a.Y * basis21Abs[2, 1] + a.Z * basis21Abs[1, 1];
        t2 = b.X * basis21Abs[0, 2] + b.Z * basis21Abs[0, 0];
        t3 = center21.Z * basis21[1, 1] - center21.Y * basis21[2, 1];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[0] x axes[2]
        t1 = a.Y * basis21Abs[2, 2] + a.Z * basis21Abs[1, 2];
        t2 = b.X * basis21Abs[0, 1] + b.Y * basis21Abs[0, 0];
        t3 = center21.Z * basis21[1, 2] - center21.Y * basis21[2, 2];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[1] x axes[0]
        t1 = a.X * basis21Abs[2, 0] + a.Z * basis21Abs[0, 0];
        t2 = b.Y * basis21Abs[1, 2] + b.Z * basis21Abs[1, 1];
        t3 = center21.X * basis21[2, 0] - center21.Z * basis21[0, 0];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[1] x axes[1]
        t1 = a.X * basis21Abs[2, 1] + a.Z * basis21Abs[0, 1];
        t2 = b.X * basis21Abs[1, 2] + b.Z * basis21Abs[1, 0];
        t3 = center21.X * basis21[2, 1] - center21.Z * basis21[0, 1];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[1] x axes[2]
        t1 = a.X * basis21Abs[2, 2] + a.Z * basis21Abs[0, 2];
        t2 = b.X * basis21Abs[1, 1] + b.Y * basis21Abs[1, 0];
        t3 = center21.X * basis21[2, 2] - center21.Z * basis21[0, 2];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[2] x axes[0]
        t1 = a.X * basis21Abs[1, 0] + a.Y * basis21Abs[0, 0];
        t2 = b.Y * basis21Abs[2, 2] + b.Z * basis21Abs[2, 1];
        t3 = center21.Y * basis21[0, 0] - center21.X * basis21[1, 0];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[2] x axes[1]
        t1 = a.X * basis21Abs[1, 1] + a.Y * basis21Abs[0, 1];
        t2 = b.X * basis21Abs[2, 2] + b.Z * basis21Abs[2, 0];
        t3 = center21.Y * basis21[0, 1] - center21.X * basis21[1, 1];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        // axes[2] x axes[2]
        t1 = a.X * basis21Abs[1, 2] + a.Y * basis21Abs[0, 2];
        t2 = b.X * basis21Abs[2, 1] + b.Y * basis21Abs[2, 0];
        t3 = center21.Y * basis21[0, 2] - center21.X * basis21[1, 2];
        if (MathF.Abs(t3) > t1 + t2 + e) return 0;

        return 1;
    }

    /// <summary>Box-Triangle overlap: SAT test. Port of box_tri_overlap_check.</summary>
    public static int BoxTri(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var pbox = (Box)p1;
        var ptri = (Triangle)p2;

        PhysVector3 pt0, pt1, pt2, n;
        if (pbox.IsOriented)
        {
            pt0 = pbox.Basis * (ptri.P0 - pbox.Center);
            pt1 = pbox.Basis * (ptri.P1 - pbox.Center);
            pt2 = pbox.Basis * (ptri.P2 - pbox.Center);
            n = pbox.Basis * ptri.Normal;
        }
        else
        {
            pt0 = ptri.P0 - pbox.Center;
            pt1 = ptri.P1 - pbox.Center;
            pt2 = ptri.P2 - pbox.Center;
            n = ptri.Normal;
        }

        // Check box normals (3 axes)
        float l1, l2, l3, l, c;

        l1 = MathF.Abs(pt0.X - pt1.X); l2 = MathF.Abs(pt1.X - pt2.X); l3 = MathF.Abs(pt2.X - pt0.X); l = l1 + l2 + l3;
        c = (l1 * (pt0.X + pt1.X) + l2 * (pt1.X + pt2.X) + l3 * (pt2.X + pt0.X)) * 0.5f;
        if (MathF.Abs(c) > (pbox.Size.X + l * 0.25f) * l) return 0;

        l1 = MathF.Abs(pt0.Y - pt1.Y); l2 = MathF.Abs(pt1.Y - pt2.Y); l3 = MathF.Abs(pt2.Y - pt0.Y); l = l1 + l2 + l3;
        c = (l1 * (pt0.Y + pt1.Y) + l2 * (pt1.Y + pt2.Y) + l3 * (pt2.Y + pt0.Y)) * 0.5f;
        if (MathF.Abs(c) > (pbox.Size.Y + l * 0.25f) * l) return 0;

        l1 = MathF.Abs(pt0.Z - pt1.Z); l2 = MathF.Abs(pt1.Z - pt2.Z); l3 = MathF.Abs(pt2.Z - pt0.Z); l = l1 + l2 + l3;
        c = (l1 * (pt0.Z + pt1.Z) + l2 * (pt1.Z + pt2.Z) + l3 * (pt2.Z + pt0.Z)) * 0.5f;
        if (MathF.Abs(c) > (pbox.Size.Z + l * 0.25f) * l) return 0;

        // Check triangle normal
        if (MathF.Abs(n.X) * pbox.Size.X + MathF.Abs(n.Y) * pbox.Size.Y + MathF.Abs(n.Z) * pbox.Size.Z < MathF.Abs(n.Dot(pt0)))
            return 0;

        // Check triangle edges x box edges cross products
        Span<PhysVector3> pts = stackalloc PhysVector3[3];
        pts[0] = pt0; pts[1] = pt1; pts[2] = pt2;

        for (int i = 0; i < 3; i++)
        {
            int iNext = MathUtils.IncMod3[i];
            int iPrev = MathUtils.DecMod3[i];
            var edge = pts[iNext] - pts[i];
            var triProj1 = edge.Cross(pts[i]);
            var triProj2 = edge.Cross(pts[iPrev]);

            float boxProj = MathF.Abs(pbox.Size.Y * edge.Z) + MathF.Abs(pbox.Size.Z * edge.Y);
            if (MathF.Abs((triProj1.X + triProj2.X) * 0.5f) > boxProj + MathF.Abs(triProj1.X - triProj2.X) * 0.5f) return 0;

            boxProj = MathF.Abs(pbox.Size.X * edge.Z) + MathF.Abs(pbox.Size.Z * edge.X);
            if (MathF.Abs((triProj1.Y + triProj2.Y) * 0.5f) > boxProj + MathF.Abs(triProj1.Y - triProj2.Y) * 0.5f) return 0;

            boxProj = MathF.Abs(pbox.Size.X * edge.Y) + MathF.Abs(pbox.Size.Y * edge.X);
            if (MathF.Abs((triProj1.Z + triProj2.Z) * 0.5f) > boxProj + MathF.Abs(triProj1.Z - triProj2.Z) * 0.5f) return 0;
        }

        return 1;
    }

    /// <summary>Triangle-Box overlap. Port of tri_box_overlap_check.</summary>
    public static int TriBox(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return BoxTri(p2, p1, checker);
    }

    /// <summary>Box-Ray overlap: slab test. Port of box_ray_overlap_check.</summary>
    public static int BoxRay(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var pbox = (Box)p1;
        var pray = (Ray)p2;

        PhysVector3 halfDir, m, al;
        if (pbox.IsOriented)
        {
            halfDir = pbox.Basis * pray.Dir * 0.5f;
            m = pbox.Basis * (pray.Origin - pbox.Center) + halfDir;
        }
        else
        {
            halfDir = pray.Dir * 0.5f;
            m = pray.Origin + halfDir - pbox.Center;
        }
        al = new PhysVector3(MathF.Abs(halfDir.X), MathF.Abs(halfDir.Y), MathF.Abs(halfDir.Z));

        // Separating axis check for line and box
        if (MathF.Abs(m.X) > pbox.Size.X + al.X) return 0;
        if (MathF.Abs(m.Y) > pbox.Size.Y + al.Y) return 0;
        if (MathF.Abs(m.Z) > pbox.Size.Z + al.Z) return 0;

        if (MathF.Abs(m.Z * halfDir.Y - m.Y * halfDir.Z) > pbox.Size.Y * al.Z + pbox.Size.Z * al.Y) return 0;
        if (MathF.Abs(m.X * halfDir.Z - m.Z * halfDir.X) > pbox.Size.X * al.Z + pbox.Size.Z * al.X) return 0;
        if (MathF.Abs(m.X * halfDir.Y - m.Y * halfDir.X) > pbox.Size.X * al.Y + pbox.Size.Y * al.X) return 0;

        return 1;
    }

    /// <summary>Ray-Box overlap. Port of ray_box_overlap_check.</summary>
    public static int RayBox(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return BoxRay(p2, p1, checker);
    }

    /// <summary>Box-Sphere overlap: closest point on box to sphere center. Port of box_sphere_overlap_check.</summary>
    public static int BoxSphere(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var pbox = (Box)p1;
        var psph = (Sphere)p2;

        PhysVector3 center;
        if (pbox.IsOriented)
            center = pbox.Basis * (psph.Center - pbox.Center);
        else
            center = psph.Center - pbox.Center;

        var dist = new PhysVector3(
            MathF.Max(0f, MathF.Abs(center.X) - pbox.Size.X),
            MathF.Max(0f, MathF.Abs(center.Y) - pbox.Size.Y),
            MathF.Max(0f, MathF.Abs(center.Z) - pbox.Size.Z));

        return dist.LengthSq() < psph.Radius * psph.Radius ? 1 : 0;
    }

    /// <summary>Sphere-Box overlap. Port of sphere_box_overlap_check.</summary>
    public static int SphereBox(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return BoxSphere(p2, p1, checker);
    }

    /// <summary>
    /// Triangle-Sphere squared distance helper. Returns quotient of squared distance.
    /// Port of tri_sphere_dist2 from overlapchecks.cpp.
    /// </summary>
    public static QuotientF TriSphereDist2(Triangle ptri, Sphere psph, out int bFace)
    {
        bFace = 0;
        Span<float> rvtx = stackalloc float[3];
        rvtx[0] = (ptri.P0 - psph.Center).LengthSq();
        rvtx[1] = (ptri.P1 - psph.Center).LengthSq();
        rvtx[2] = (ptri.P2 - psph.Center).LengthSq();

        // Find closest vertex
        int i = 0;
        if (rvtx[1] < rvtx[0]) i = 1;
        if (rvtx[2] < rvtx[i]) i = 2;

        var pi = ptri[i];
        var dp = psph.Center - pi;

        var e0 = ptri[MathUtils.IncMod3[i]] - pi;
        var e1 = ptri[MathUtils.DecMod3[i]] - pi;
        float elen2_0 = e0.LengthSq();
        float elen2_1 = e1.LengthSq();

        int bInside0 = MathUtils.IsNeg(dp.Cross(e0).Dot(ptri.Normal));
        int bInside1 = MathUtils.IsNeg(e1.Cross(dp).Dot(ptri.Normal));

        Span<PhysVector3> edges = stackalloc PhysVector3[2];
        edges[0] = e0; edges[1] = e1;
        Span<float> elen2 = stackalloc float[2];
        elen2[0] = elen2_0; elen2[1] = elen2_1;

        float dpEdge = MathF.Max(0f, dp.Dot(edges[bInside0]));
        rvtx[i] = rvtx[i] * elen2[bInside0] - dpEdge * dpEdge * (bInside0 | bInside1);
        float denom = elen2[bInside0];

        if ((bInside0 & bInside1) != 0)
        {
            if (e0.Dot(e1) < 0)
            {
                var e2 = ptri[MathUtils.DecMod3[i]] - ptri[MathUtils.IncMod3[i]];
                var dp2 = psph.Center - ptri[MathUtils.IncMod3[i]];
                if (dp2.Cross(e2).Dot(ptri.Normal) > 0)
                {
                    float e2len2 = e2.LengthSq();
                    float dp2e2 = dp2.Dot(e2);
                    int iInc = MathUtils.IncMod3[i];
                    return new QuotientF(rvtx[iInc] * e2len2 - dp2e2 * dp2e2, e2len2);
                }
            }
            bFace = 1;
            float faceDist = (psph.Center - ptri.P0).Dot(ptri.Normal);
            return new QuotientF(faceDist * faceDist, 1f);
        }
        return new QuotientF(rvtx[i], denom);
    }

    /// <summary>Triangle-Sphere overlap. Port of tri_sphere_overlap_check.</summary>
    public static int TriSphere(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var ptri = (Triangle)p1;
        var psph = (Sphere)p2;
        var dist2 = TriSphereDist2(ptri, psph, out _);
        var r2 = new QuotientF(psph.Radius * psph.Radius, 1f);
        // dist2 < r2  =>  dist2.X * r2.Y < r2.X * dist2.Y
        return dist2.X * r2.Y < r2.X * dist2.Y ? 1 : 0;
    }

    /// <summary>Sphere-Triangle overlap. Port of sphere_tri_overlap_check.</summary>
    public static int SphereTri(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return TriSphere(p2, p1, checker);
    }

    /// <summary>
    /// Box-Heightfield overlap. Port of box_heightfield_overlap_check.
    /// Transforms the box into heightfield space, finds the underlying grid rectangle,
    /// and tests overlap against heightfield triangles via SAT.
    /// </summary>
    public static int BoxHeightfield(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var pbox = (Box)p1;
        var phf = (Heightfield)p2;

        // Transform box into heightfield local space
        PhysMatrix33 boxBasis;
        PhysVector3 boxCenter;
        if (phf.IsOriented)
        {
            if (pbox.IsOriented)
                boxBasis = pbox.Basis * phf.Basis.Transposed();
            else
                boxBasis = phf.Basis.Transposed();
            boxCenter = phf.Basis * (pbox.Center - phf.Origin);
        }
        else
        {
            boxBasis = pbox.Basis;
            boxCenter = pbox.Center - phf.Origin;
        }

        // Flip rows so z-component of each basis row is negative (find lowest vertices)
        // sgnnz: returns -1 if value < 0, else +1
        float sgn0 = boxBasis.M02 >= 0 ? -1f : 1f;
        boxBasis.SetRow(0, boxBasis.GetRow(0) * sgn0);
        float sgn1 = boxBasis.M12 >= 0 ? -1f : 1f;
        boxBasis.SetRow(1, boxBasis.GetRow(1) * sgn1);
        float sgn2 = boxBasis.M22 >= 0 ? -1f : 1f;
        boxBasis.SetRow(2, boxBasis.GetRow(2) * sgn2);

        var size = pbox.Size;

        // vtx[0] = size * basis (the lowest vertex)
        PhysVector3 MultiplyRowMajor(in PhysVector3 s, in PhysMatrix33 b) => new(
            s.X * b.M00 + s.Y * b.M10 + s.Z * b.M20,
            s.X * b.M01 + s.Y * b.M11 + s.Z * b.M21,
            s.X * b.M02 + s.Y * b.M12 + s.Z * b.M22);

        var vtx0 = MultiplyRowMajor(size, boxBasis);

        // vtx[1]: flip row0, compute, flip back
        boxBasis.SetRow(0, -boxBasis.GetRow(0));
        var vtx1 = MultiplyRowMajor(size, boxBasis);
        boxBasis.SetRow(0, -boxBasis.GetRow(0));

        // vtx[2]: flip row1, compute, flip back
        boxBasis.SetRow(1, -boxBasis.GetRow(1));
        var vtx2 = MultiplyRowMajor(size, boxBasis);
        boxBasis.SetRow(1, -boxBasis.GetRow(1));

        // vtx[3]: flip row2, compute, flip back
        boxBasis.SetRow(2, -boxBasis.GetRow(2));
        var vtx3 = MultiplyRowMajor(size, boxBasis);
        boxBasis.SetRow(2, -boxBasis.GetRow(2));

        // Find underlying grid rectangle
        float szx = 0, szy = 0;
        szx = MathF.Max(szx, MathF.Abs(vtx1.X)); szy = MathF.Max(szy, MathF.Abs(vtx1.Y));
        szx = MathF.Max(szx, MathF.Abs(vtx2.X)); szy = MathF.Max(szy, MathF.Abs(vtx2.Y));
        szx = MathF.Max(szx, MathF.Abs(vtx3.X)); szy = MathF.Max(szy, MathF.Abs(vtx3.Y));

        float ptminx = (boxCenter.X - szx) * phf.StepR.X;
        float ptminy = (boxCenter.Y - szy) * phf.StepR.Y;
        float ptmaxx = (boxCenter.X + szx) * phf.StepR.X;
        float ptmaxy = (boxCenter.Y + szy) * phf.StepR.Y;

        int ix0 = (int)MathF.Floor(ptminx); ix0 = ix0 < 0 ? 0 : ix0;
        int iy0 = (int)MathF.Floor(ptminy); iy0 = iy0 < 0 ? 0 : iy0;
        int ix1 = System.Math.Min((int)MathF.Ceiling(ptmaxx), phf.Size.X);
        int iy1 = System.Math.Min((int)MathF.Ceiling(ptmaxy), phf.Size.Y);

        vtx0 += boxCenter; vtx1 += boxCenter; vtx2 += boxCenter; vtx3 += boxCenter;

        // Build a transformed box for box_tri overlap tests
        var boxtr = new Box
        {
            Center = PhysVector3.Zero, // will be set per-use
            Basis = boxBasis,
            Size = pbox.Size,
            IsOriented = true
        };
        boxtr.Center = boxCenter;

        if ((ix1 - ix0) * (iy1 - iy0) <= 6)
        {
            // Check if all heightfield points are below the lowest box point
            float hmax = 0;
            for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                    hmax = MathF.Max(hmax, phf.GetHeightAt(ix, iy));
            if (hmax < vtx0.Z)
                return 0;

            // Check for intersection with each underlying triangle pair
            for (int ix = ix0; ix < ix1; ix++)
            {
                for (int iy = iy0; iy < iy1; iy++)
                {
                    var hftri = new Triangle();
                    hftri.P0 = new PhysVector3(ix * phf.Step.X, iy * phf.Step.Y, phf.GetHeightAt(ix, iy));
                    hftri.P1 = new PhysVector3(ix * phf.Step.X + phf.Step.X, iy * phf.Step.Y, phf.GetHeightAt(ix + 1, iy));
                    hftri.P2 = new PhysVector3(ix * phf.Step.X, iy * phf.Step.Y + phf.Step.Y, phf.GetHeightAt(ix, iy + 1));
                    hftri.Normal = (hftri.P1 - hftri.P0).Cross(hftri.P2 - hftri.P0);
                    if (BoxTri(boxtr, hftri, null) != 0)
                        return 1;

                    // Second triangle of the quad
                    hftri.P0 = hftri.P2;
                    hftri.P2 = new PhysVector3(ix * phf.Step.X + phf.Step.X, iy * phf.Step.Y + phf.Step.Y, phf.GetHeightAt(ix + 1, iy + 1));
                    hftri.Normal = (hftri.P1 - hftri.P0).Cross(hftri.P2 - hftri.P0);
                    if (BoxTri(boxtr, hftri, null) != 0)
                        return 1;
                }
            }
        }
        else
        {
            // Large area: just check if any heightfield point is above the lowest box vertex
            for (int ix = ix0; ix <= ix1; ix++)
                for (int iy = iy0; iy <= iy1; iy++)
                    if (phf.GetHeightAt(ix, iy) > vtx0.Z)
                        return 1;
        }

        return 0;
    }

    /// <summary>Heightfield-Box overlap. Port of heightfield_box_overlap_check.</summary>
    public static int HeightfieldBox(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return BoxHeightfield(p2, p1, checker);
    }

    /// <summary>
    /// Box-VoxelGrid overlap. Port of box_voxgrid_overlap_check.
    /// Transforms the box into voxel grid space, finds overlapping cells,
    /// collects triangles, and tests each via box-tri SAT.
    /// </summary>
    public static int BoxVoxelGrid(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var pbox = (Box)p1;
        var pgrid = (VoxelGrid)p2;

        // Transform box into grid local space
        PhysMatrix33 boxBasis;
        PhysVector3 boxCenter;
        if (pgrid.IsOriented)
        {
            if (pbox.IsOriented)
                boxBasis = pbox.Basis * pgrid.Basis.Transposed();
            else
                boxBasis = pgrid.Basis.Transposed();
            boxCenter = pgrid.Basis * (pbox.Center - pgrid.Origin);
        }
        else
        {
            boxBasis = pbox.Basis;
            boxCenter = pbox.Center - pgrid.Origin;
        }

        // Compute AABB of box in grid space
        PhysMatrix33 basisAbs = Fabs(boxBasis);
        var dim = new PhysVector3(
            pbox.Size.X * basisAbs[0, 0] + pbox.Size.Y * basisAbs[1, 0] + pbox.Size.Z * basisAbs[2, 0],
            pbox.Size.X * basisAbs[0, 1] + pbox.Size.Y * basisAbs[1, 1] + pbox.Size.Z * basisAbs[2, 1],
            pbox.Size.X * basisAbs[0, 2] + pbox.Size.Y * basisAbs[1, 2] + pbox.Size.Z * basisAbs[2, 2]);

        int bx0 = System.Math.Max(0, (int)MathF.Floor((boxCenter.X - dim.X) * pgrid.StepR.X));
        int by0 = System.Math.Max(0, (int)MathF.Floor((boxCenter.Y - dim.Y) * pgrid.StepR.Y));
        int bz0 = System.Math.Max(0, (int)MathF.Floor((boxCenter.Z - dim.Z) * pgrid.StepR.Z));
        int bx1 = System.Math.Min(pgrid.Size.X, (int)MathF.Ceiling((boxCenter.X + dim.X) * pgrid.StepR.X));
        int by1 = System.Math.Min(pgrid.Size.Y, (int)MathF.Ceiling((boxCenter.Y + dim.Y) * pgrid.StepR.Y));
        int bz1 = System.Math.Min(pgrid.Size.Z, (int)MathF.Ceiling((boxCenter.Z + dim.Z) * pgrid.StepR.Z));

        if ((long)(bx1 - bx0) * (by1 - by0) * (bz1 - bz0) > 18)
            return 1;

        if (pgrid.CellTris == null || pgrid.TriBuf == null ||
            pgrid.Vertices == null || pgrid.Indices == null || pgrid.Normals == null)
            return 0;

        // Transform box into mesh space (using R, offset, scale)
        var boxtrBasis = pbox.Basis * pgrid.Rotation;
        var boxtrCenter = (pgrid.Rotation.Transposed() * (pbox.Center - pgrid.Offset)) * pgrid.RScale;
        var boxtrSize = pbox.Size * pgrid.RScale;

        var boxtr = new Box
        {
            Center = boxtrCenter,
            Basis = boxtrBasis,
            Size = boxtrSize,
            IsOriented = true
        };

        // Collect unique triangles from overlapping cells
        const int MaxTestTris = 8;
        var triSet = new System.Collections.Generic.HashSet<int>();
        for (int iz = bz0; iz < bz1; iz++)
            for (int iy = by0; iy < by1; iy++)
                for (int ix = bx0; ix < bx1; ix++)
                {
                    int icell = ix * pgrid.Stride.X + iy * pgrid.Stride.Y + iz * pgrid.Stride.Z;
                    if (icell + 1 >= pgrid.CellTris.Length) continue;
                    int start = pgrid.CellTris[icell];
                    int end = pgrid.CellTris[icell + 1];
                    for (int t = start; t < end; t++)
                    {
                        if (t < pgrid.TriBuf.Length)
                            triSet.Add(pgrid.TriBuf[t]);
                    }
                    if (triSet.Count > MaxTestTris)
                        return 1;
                }

        // Test each triangle
        foreach (int ti in triSet)
        {
            int i0 = ti * 3, i1 = ti * 3 + 1, i2 = ti * 3 + 2;
            if (i2 >= pgrid.Indices.Length) continue;
            var atri = new Triangle
            {
                P0 = pgrid.Vertices[pgrid.Indices[i0]],
                P1 = pgrid.Vertices[pgrid.Indices[i1]],
                P2 = pgrid.Vertices[pgrid.Indices[i2]],
                Normal = pgrid.Normals[ti]
            };
            if (BoxTri(boxtr, atri, null) != 0)
                return 1;
        }

        return 0;
    }

    /// <summary>VoxelGrid-Box overlap. Port of voxgrid_box_overlap_check.</summary>
    public static int VoxelGridBox(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return BoxVoxelGrid(p2, p1, checker);
    }

    /// <summary>
    /// Heightfield-Sphere overlap. Port of heightfield_sphere_overlap_check.
    /// Transforms sphere center into heightfield space, finds the affected grid cells,
    /// and checks if the sphere bottom is below any heightfield sample.
    /// </summary>
    public static int HeightfieldSphere(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        var phf = (Heightfield)p1;
        var psph = (Sphere)p2;

        var center = phf.Basis * (psph.Center - phf.Origin);

        int ix0 = System.Math.Min(phf.Size.X, System.Math.Max(0, (int)MathF.Floor((center.X - psph.Radius) * phf.StepR.X)));
        int iy0 = System.Math.Min(phf.Size.Y, System.Math.Max(0, (int)MathF.Floor((center.Y - psph.Radius) * phf.StepR.Y)));
        int ix1 = System.Math.Min(phf.Size.X, System.Math.Max(0, (int)MathF.Ceiling((center.X + psph.Radius) * phf.StepR.X)));
        int iy1 = System.Math.Min(phf.Size.Y, System.Math.Max(0, (int)MathF.Ceiling((center.Y + psph.Radius) * phf.StepR.Y)));

        int bContact = 0;
        for (int ix = ix0; ix < ix1; ix++)
            for (int iy = iy0; iy < iy1; iy++)
                bContact |= (center.Z - psph.Radius < phf.GetHeightAt(ix, iy)) ? 1 : 0;

        return bContact;
    }

    /// <summary>Sphere-Heightfield overlap. Port of sphere_heightfield_overlap_check.</summary>
    public static int SphereHeightfield(Primitive p1, Primitive p2, OverlapChecker? checker)
    {
        return HeightfieldSphere(p2, p1, checker);
    }

    /// <summary>Compute element-wise absolute value of a 3x3 matrix.</summary>
    private static PhysMatrix33 Fabs(in PhysMatrix33 m) => new(
        MathF.Abs(m.M00), MathF.Abs(m.M01), MathF.Abs(m.M02),
        MathF.Abs(m.M10), MathF.Abs(m.M11), MathF.Abs(m.M12),
        MathF.Abs(m.M20), MathF.Abs(m.M21), MathF.Abs(m.M22)
    );
}
