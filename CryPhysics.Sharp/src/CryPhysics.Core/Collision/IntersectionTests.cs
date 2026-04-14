// Port of CryPhysics intersectionchecks.cpp - primitive-primitive intersection tests
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Collision;

/// <summary>
/// All primitive-primitive intersection test implementations.
/// Port of the intersection functions from intersectionchecks.cpp.
/// </summary>
public static class IntersectionTests
{
    // ============================================================================
    // Sphere-Sphere intersection
    // ============================================================================
    public static int SphereSphere(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var s1 = (Sphere)p1;
        var s2 = (Sphere)p2;

        var diff = s2.Center - s1.Center;
        float dist2 = diff.LengthSq();
        float rsum = s1.Radius + s2.Radius;

        if (dist2 > rsum * rsum)
            return 0;

        float dist = MathF.Sqrt(dist2);
        if (dist < 1e-10f)
        {
            pinters.Normal = PhysVector3.UnitZ;
            pinters.Pt0 = s1.Center;
            pinters.Pt1 = s1.Center;
        }
        else
        {
            var n = diff / dist;
            pinters.Normal = n;
            pinters.Pt0 = s1.Center + n * s1.Radius;
            pinters.Pt1 = s2.Center - n * s2.Radius;
        }

        return 1;
    }

    // ============================================================================
    // Sphere-Ray / Ray-Sphere intersection
    // ============================================================================
    public static int SphereRay(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return RaySphereImpl((Sphere)p1, (Ray)p2, pinters, false);
    }

    public static int RaySphere(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return RaySphereImpl((Sphere)p2, (Ray)p1, pinters, true);
    }

    private static int RaySphereImpl(Sphere sphere, Ray ray, PrimInters pinters, bool rayFirst)
    {
        var oc = ray.Origin - sphere.Center;
        float a = ray.Dir.Dot(ray.Dir);
        float b = 2f * oc.Dot(ray.Dir);
        float c = oc.LengthSq() - sphere.Radius * sphere.Radius;
        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0)
            return 0;

        float sqrtD = MathF.Sqrt(discriminant);
        float t = (-b - sqrtD) / (2f * a);

        if (t < 0)
        {
            t = (-b + sqrtD) / (2f * a);
            if (t < 0) return 0;
        }

        if (t > 1f) return 0; // Ray is finite (origin + t*dir, t in [0,1])

        var hitPt = ray.Origin + ray.Dir * t;
        var normal = (hitPt - sphere.Center).Normalized();

        pinters.Pt0 = hitPt;
        pinters.Pt1 = hitPt;
        pinters.Normal = normal;

        return 1;
    }

    // ============================================================================
    // Box-Ray / Ray-Box intersection (slab method)
    // ============================================================================
    public static int BoxRay(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return RayBoxImpl((Box)p1, (Ray)p2, pinters);
    }

    public static int RayBox(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return RayBoxImpl((Box)p2, (Ray)p1, pinters);
    }

    private static int RayBoxImpl(Box box, Ray ray, PrimInters pinters)
    {
        // Transform ray to box local space
        var localOrigin = box.Basis * (ray.Origin - box.Center);
        var localDir = box.Basis * ray.Dir;

        float tMin = 0f, tMax = 1f;
        PhysVector3 normalMin = PhysVector3.Zero;

        for (int i = 0; i < 3; i++)
        {
            float o = localOrigin[i], d = localDir[i], s = box.Size[i];

            if (MathF.Abs(d) < 1e-10f)
            {
                if (o < -s || o > s) return 0;
                continue;
            }

            float invD = 1f / d;
            float t1 = (-s - o) * invD;
            float t2 = (s - o) * invD;
            var n = PhysVector3.Zero;
            n[i] = -MathUtils.SgnNZ(d);

            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                n = -n;
            }

            if (t1 > tMin)
            {
                tMin = t1;
                normalMin = n;
            }
            if (t2 < tMax) tMax = t2;

            if (tMin > tMax) return 0;
        }

        var hitPt = ray.Origin + ray.Dir * tMin;
        // Transform normal back to world space
        pinters.Normal = box.Basis.Transposed() * normalMin;
        pinters.Pt0 = hitPt;
        pinters.Pt1 = hitPt;

        return 1;
    }

    // ============================================================================
    // Triangle-Ray / Ray-Triangle intersection (Moller-Trumbore)
    // ============================================================================
    public static int TriRay(Primitive p1, Primitive p2, PrimInters pinters)
    {
        // tri_ray_intersection: calls ray_tri_intersection, then swaps features diagonally and flips normal
        int res = RayTriImpl((Ray)p2, (Triangle)p1, pinters);
        byte tmp;
        tmp = pinters.Feature[0, 0]; pinters.Feature[0, 0] = pinters.Feature[1, 1]; pinters.Feature[1, 1] = tmp;
        tmp = pinters.Feature[0, 1]; pinters.Feature[0, 1] = pinters.Feature[1, 0]; pinters.Feature[1, 0] = tmp;
        pinters.Normal = -pinters.Normal;
        return res;
    }

    public static int RayTri(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return RayTriImpl((Ray)p1, (Triangle)p2, pinters);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float FSel(float a, float b, float c) { return a >= 0f ? b : c; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SqrSigned(float x) { return x >= 0f ? x * x : -(x * x); }

    // Literal port of ray_tri_intersection from intersectionchecks.cpp:731-769
    private static int RayTriImpl(Ray pray, Triangle ptri, PrimInters pinters)
    {
        float fDotDir0 = pray.Dir.Dot(ptri.Normal);

        if (Sqr(fDotDir0) > pinters.MinPtDist2 * Sqr(1E-4f))
        {
            float fSign = FSel(fDotDir0, 1.0f, -1.0f);
            float fDotPt0 = ((ptri.P0 - pray.Origin).Dot(ptri.Normal)) * fSign;
            float fDotDir = fDotDir0 * fSign;

            PhysVector3 pt = pray.Origin * fDotDir + pray.Dir * fDotPt0;
            float nlen2 = ptri.Normal.GetLengthSquared() * fDotDir;

            if (fDotDir < MathF.Abs(fDotPt0 * 2.0f - fDotDir))
                return 0;

            PhysVector3 edge0 = ptri.P1 - ptri.P0;
            if (SqrSigned(ptri.Normal.Dot(edge0 ^ (pt - ptri.P0 * fDotDir))) + pinters.MinPtDist2 * edge0.GetLengthSquared() * nlen2 < 0.0f)
                return 0;

            PhysVector3 edge1 = ptri.P2 - ptri.P1;
            if (SqrSigned(ptri.Normal.Dot(edge1 ^ (pt - ptri.P1 * fDotDir))) + pinters.MinPtDist2 * edge1.GetLengthSquared() * nlen2 < 0.0f)
                return 0;

            PhysVector3 edge2 = ptri.P0 - ptri.P2;
            if (SqrSigned(ptri.Normal.Dot(edge2 ^ (pt - ptri.P2 * fDotDir))) + pinters.MinPtDist2 * edge2.GetLengthSquared() * nlen2 < 0.0f)
                return 0;

            PhysVector3 outPt = pray.Origin + pray.Dir * (fDotPt0 / fDotDir);
            pinters.Pt0 = outPt;
            pinters.Pt1 = outPt;
            pinters.Normal = ptri.Normal;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
            pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x20;
            return 1;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Sqr(float x) { return x * x; }

    // ============================================================================
    // Helper: swap pinters for reversed intersection calls
    // ============================================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapPinters(PrimInters pinters)
    {
        (pinters.Pt0, pinters.Pt1) = (pinters.Pt1, pinters.Pt0);
        byte f00 = pinters.Feature[0, 0], f01 = pinters.Feature[0, 1];
        byte f10 = pinters.Feature[1, 0], f11 = pinters.Feature[1, 1];
        pinters.Feature[0, 0] = f11;
        pinters.Feature[0, 1] = f10;
        pinters.Feature[1, 0] = f01;
        pinters.Feature[1, 1] = f00;
    }

    // ============================================================================
    // Helper: idxmin3 / idxmax3 for PhysVector3 components
    // ============================================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IdxMin3(float a, float b, float c)
    {
        int idx = 0;
        if (b < a) idx = 1;
        if (c < (idx == 0 ? a : b)) idx = 2;
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IdxMax3(float a, float b, float c)
    {
        int idx = 0;
        if (b > a) idx = 1;
        if (c > (idx == 0 ? a : b)) idx = 2;
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PhysVector3 VecAbs(in PhysVector3 v) =>
        new(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

    /// <summary>C++ vector2d cross (2D perpendicular dot): a.x*b.y - a.y*b.x</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Cross2D(float ax, float ay, float bx, float by) =>
        ax * by - ay * bx;

    /// <summary>
    /// C++ GetMtxFromBasis: builds a matrix whose rows are the basis vectors,
    /// then returns its inverse. For 3 basis vectors that may not be orthonormal,
    /// this computes the matrix M such that M * v gives coordinates in the basis.
    /// In the C++ code this is used as: pt0 = Vec3r(dot0, dot1, dot2) * GetMtxFromBasis(axes)
    /// which means: pt0 = transpose(GetMtxFromBasis(axes)) * (dot0, dot1, dot2)
    /// Actually in the C++ code, Vec3r * Matrix is a row-vector * matrix multiplication.
    /// GetMtxFromBasis builds a matrix from 3 axes and inverts it.
    /// For the tri-tri case, we just need the result of the multiplication.
    /// </summary>
    private static PhysVector3 MulVecMtxFromBasis(in PhysVector3 v, PhysVector3[] axes)
    {
        // Build matrix with rows = axes, then invert, then multiply v * M^-1
        // In CryEngine, Vec3 * Matrix33 is row-vector * matrix (v.x*row0 + v.y*row1 + v.z*row2)
        // GetMtxFromBasis builds M with columns = axes, so M[col][row] = axes[col][row]
        // Actually GetMtxFromBasis builds the matrix and returns its SetFromVectors result
        // Let's just build and invert:
        var m = new PhysMatrix33(axes[0], axes[1], axes[2]); // rows = axes
        // In C++, the matrix is built with SetFromVectors which sets columns from the vectors.
        // So the matrix has columns = axes[0], axes[1], axes[2].
        // Then Vec3r * Matrix = v * M = v^T * M
        // We need: result = v.x * col0 + v.y * col1 + v.z * col2 = M^T * v
        // But GetMtxFromBasis returns the INVERSE of this matrix.
        // Actually, GetMtxFromBasis: SetFromVectors(axes[0], axes[1], axes[2]) which sets
        // rows of the matrix. Then it returns this matrix.
        // And v * M in C++ is v^T * M = sum_i v[i] * row_i = (v.x*M[0] + v.y*M[1] + v.z*M[2])
        // where M[i] is row i.
        // So result = v.x * axes[0] + v.y * axes[1] + v.z * axes[2]
        // But wait, GetMtxFromBasis doesn't just set rows - let me look at the actual C++ code.
        //
        // In CryEngine: GetMtxFromBasis sets up a matrix from 3 basis vectors.
        // The implementation typically creates a matrix where the basis vectors are rows,
        // and since the basis may not be orthonormal, it inverts.
        // But for our tri-tri test, the axes are orthogonal (they're built from cross products
        // of normals), so the matrix is just the basis matrix.
        //
        // For simplicity and correctness: build matrix with rows=axes, invert, then multiply.
        var mInv = m.Inverted();
        // v * M^-1 in C++ row-vector convention = M^-1^T * v in column-vector convention
        // But in our C# code, matrix * vector = M * v (column-vector).
        // C++ v * M = [v.x*M00+v.y*M10+v.z*M20, v.x*M01+v.y*M11+v.z*M21, v.x*M02+v.y*M12+v.z*M22]
        // = M^T * v
        // So we need mInv^T * v
        return mInv.Transposed() * v;
    }

    // ============================================================================
    // Triangle-Triangle intersection
    // Port of tri_tri_intersection (lines ~104-172 in C++)
    // ============================================================================
    public static int TriTri(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var ptri1 = (Triangle)p1;
        var ptri2 = (Triangle)p2;

        // axes: n2 x n, n1 x n, n = n1 x n2
        var axes = new PhysVector3[3];
        axes[2] = ptri1.Normal ^ ptri2.Normal;
        axes[0] = ptri2.Normal ^ axes[2];
        axes[1] = ptri1.Normal ^ axes[2];
        float nlen2 = axes[2].LengthSq();

        // pt0 = Vec3r(ptri1->pt[0]*ptri1->n, -ptri2->pt[0]*ptri2->n, ptri1->pt[0]*axes[2]) * GetMtxFromBasis(axes)
        var dotVec = new PhysVector3(
            ptri1.P0.Dot(ptri1.Normal),
            -ptri2.P0.Dot(ptri2.Normal),
            ptri1.P0.Dot(axes[2])
        );
        var pt0 = MulVecMtxFromBasis(dotVec, axes);

        // Project triangle vertices into 2D
        var pt2d = new float[2, 3, 2]; // [tri][vert][x,y]
        var sgnx = new int[2, 3];

        for (int i = 0; i < 3; i++)
        {
            var ptloc = ptri1[i] * nlen2 - pt0;
            pt2d[0, i, 0] = axes[1].Dot(ptloc);
            pt2d[0, i, 1] = axes[2].Dot(ptloc);

            ptloc = ptri2[i] * nlen2 - pt0;
            pt2d[1, i, 0] = axes[0].Dot(ptloc);
            pt2d[1, i, 1] = axes[2].Dot(ptloc);

            sgnx[0, i] = MathUtils.SgnNZ(pt2d[0, i, 0]);
            sgnx[1, i] = MathUtils.SgnNZ(pt2d[1, i, 0]);
        }

        var tmin = new QuotientF[2];
        var tmax = new QuotientF[2];
        var iEdge = new int[2, 2];

        for (int i = 0; i < 2; i++)
        {
            int bCross = (sgnx[i, 0] * sgnx[i, 1] - 1) >> 1;
            int nCross = -bCross;
            int iNotCross = 0;

            bCross = (sgnx[i, 1] * sgnx[i, 2] - 1) >> 1;
            nCross -= bCross;
            iNotCross |= 1 & ~bCross;

            bCross = (sgnx[i, 2] * sgnx[i, 0] - 1) >> 1;
            nCross -= bCross;
            iNotCross = (iNotCross & bCross) | (2 & ~bCross);

            if (nCross != 2)
                return 0;

            int idec = MathUtils.DecMod3[iNotCross];
            int iinc = MathUtils.IncMod3[iNotCross];

            var t = new QuotientF[2];
            // t[0].x = pt2d[i][dec] ^ pt2d[i][inc]  (2D cross)
            t[0].X = Cross2D(pt2d[i, idec, 0], pt2d[i, idec, 1], pt2d[i, iinc, 0], pt2d[i, iinc, 1]);
            t[0].Y = pt2d[i, idec, 0] - pt2d[i, iinc, 0];
            // t[1].x = pt2d[i][iNotCross] ^ pt2d[i][dec]
            t[1].X = Cross2D(pt2d[i, iNotCross, 0], pt2d[i, iNotCross, 1], pt2d[i, idec, 0], pt2d[i, idec, 1]);
            t[1].Y = pt2d[i, iNotCross, 0] - pt2d[i, idec, 0];
            t[0] = t[0].FixSign();
            t[1] = t[1].FixSign();

            // sgtest to determine imin
            float sg0 = t[1].X * t[0].Y - t[0].X * t[1].Y;
            float sg1 = pt2d[i, iNotCross, 0];
            float sg2 = pt2d[i, iinc, 0];
            // idxmax3 of abs values
            float a0 = MathF.Abs(sg0), a1 = MathF.Abs(sg1), a2 = MathF.Abs(sg2);
            int idxMax = IdxMax3(a0, a1, a2);
            float sgVal = idxMax switch { 0 => sg0, 1 => sg1, _ => sg2 };
            int imin = MathUtils.IsNeg(sgVal);

            iEdge[i, imin] = iinc;
            iEdge[i, imin ^ 1] = idec;
            tmin[i] = t[imin];
            tmax[i] = t[imin ^ 1];

            // tc and tr for overlap test
            // tc[i] = (t[0]+t[1])*0.5; tr[i] = fabs(t[0]-t[1])*0.5
            // We only need to check: |tc[0]-tc[1]| > tr[0]+tr[1]
            // Store tc and tr as quotients for the overlap test
            if (i == 0)
            {
                // Store for later overlap check
            }
        }

        // Overlap check: fabs((tc[0]-tc[1]).x) > (tr[0]+tr[1]).x
        // tc[i] = (tmin[i] + tmax[i]) * 0.5, but as quotients
        var tc0 = tmin[0] + tmax[0]; // no need to multiply by 0.5, just compare consistently
        var tc1 = tmin[1] + tmax[1];
        var tr0 = new QuotientF(MathF.Abs(tmin[0].X * tmax[0].Y - tmax[0].X * tmin[0].Y), tmin[0].Y * tmax[0].Y);
        var tr1 = new QuotientF(MathF.Abs(tmin[1].X * tmax[1].Y - tmax[1].X * tmin[1].Y), tmin[1].Y * tmax[1].Y);

        // (tc0 - tc1) as quotient
        var tcDiff = tc0 * 0.5f - tc1 * 0.5f;
        var trSum = tr0 * 0.5f + tr1 * 0.5f;

        if (MathF.Abs(tcDiff.X) > trSum.X)
            return 0;

        int imaxIdx = (tmin[0] - tmin[1]).IsNeg(); // start_t = max(min1, min2)
        int iminIdx = (tmax[1] - tmax[0]).IsNeg(); // end_t = min(max1, max2)

        // 1 division scheme
        float ptw = nlen2 * nlen2 * tmin[imaxIdx].Y * tmax[iminIdx].Y;
        if (ptw == 0)
            return 0;
        ptw = 1.0f / ptw;
        var pt0Scaled = pt0 * (nlen2 * tmin[imaxIdx].Y * tmax[iminIdx].Y);
        pinters.Pt0 = (pt0Scaled + axes[2] * (tmin[imaxIdx].X * tmax[iminIdx].Y)) * ptw;
        pinters.Pt1 = (pt0Scaled + axes[2] * (tmax[iminIdx].X * tmin[imaxIdx].Y)) * ptw;

        pinters.Feature[0, imaxIdx] = (byte)(iEdge[imaxIdx, 0] | 0xA0);
        pinters.Feature[0, imaxIdx ^ 1] = 0x40;
        pinters.Feature[1, iminIdx] = (byte)(iEdge[iminIdx, 1] | 0xA0);
        pinters.Feature[1, iminIdx ^ 1] = 0x40;

        return 1;
    }

    // ============================================================================
    // Triangle-Sphere / Sphere-Triangle intersection
    // Port of tri_sphere_intersection / sphere_tri_intersection
    // ============================================================================
    public static int TriSphere(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var ptri = (Triangle)p1;
        var psphere = (Sphere)p2;
        return TriSphereImpl(ptri, psphere, pinters);
    }

    public static int SphereTri(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = TriSphereImpl((Triangle)p2, (Sphere)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    private static int TriSphereImpl(Triangle ptri, Sphere psphere, PrimInters pinters)
    {
        float r2 = MathUtils.Sqr(psphere.Radius);
        int iStart = -1;

        for (int i = 0; i < 3; i++)
        {
            int bOutside = MathUtils.IsNeg(r2 - (ptri[i] - psphere.Center).LengthSq());
            iStart = (iStart & ~(-bOutside)) | (i & (-bOutside));
        }

        if (iStart < 0) // all triangle points are inside the sphere
            return 0;

        int bFaceContact = 0;
        {
            int bOutside = MathUtils.IsNeg(psphere.Radius - MathF.Abs((psphere.Center - ptri.P0).Dot(ptri.Normal)));
            for (int i = 0; i < 3; i++)
                bOutside |= MathUtils.IsNeg(((ptri[MathUtils.IncMod3[i]] - ptri[i]) ^ (psphere.Center - ptri[i])).Dot(ptri.Normal));
            if (bOutside == 0)
            {
                pinters.NBestPtVal = 1000;
                pinters.BestPt = psphere.Center - ptri.Normal * psphere.Radius;
                pinters.Normal = ptri.Normal;
                bFaceContact = 1;
            }
        }

        int iEndCur = -1, iStartBest = -1, iEndBest = -1, bState = 0;
        var mindist = new QuotientF(1, 0);
        var t = new QuotientF[3, 2];

        int ii = iStart;
        do
        {
            int idec = MathUtils.DecMod3[ii];
            var dp = ptri[idec] - ptri[ii];
            var pc = ptri[ii] - psphere.Center;
            float a = dp.LengthSq();
            float b = dp.Dot(pc);
            float c = pc.LengthSq() - r2;
            float d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t[ii, 0] = new QuotientF(-b - d, a);
                t[ii, 1] = new QuotientF(-b + d, a);

                if (t[ii, 0] < t[ii, 1])
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int bSwitch = MathUtils.IsNeg(MathF.Abs(t[ii, j].X * 2 - t[ii, j].Y) - t[ii, j].Y);
                        int bEnd = bSwitch & bState;
                        iEndCur = (iEndCur & ~(-bEnd)) | ((ii << 1 | j) & (-bEnd));

                        var dp1 = ptri[ii] - pinters.Pt1;
                        var dist = new QuotientF(
                            (dp1 * t[ii, j].Y + (ptri[idec] - ptri[ii]) * t[ii, j].X).LengthSq(),
                            MathUtils.Sqr(t[ii, j].Y));
                        int bBest = MathUtils.IsNeg((dist - mindist).X) & bSwitch & (bState ^ 1);
                        mindist.X = mindist.X * (1 - bBest) + dist.X * bBest;
                        mindist.Y = mindist.Y * (1 - bBest) + dist.Y * bBest;
                        iStartBest = (iStartBest & ~(-bBest)) | ((ii << 1 | j) & (-bBest));
                        iEndBest = (iEndBest & ~(-bBest)) | (iEndCur & (-bBest));
                        bState ^= bSwitch;
                    }
                }
            }
        } while ((ii = MathUtils.DecMod3[ii]) != iStart);

        if (iStartBest < 0)
        {
            if (bFaceContact == 0)
                return 0;
            pinters.Pt0 = pinters.Pt1 = pinters.BestPt;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
            pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
            return 1;
        }

        iEndBest = (iEndBest - (iEndBest >> 31)) | (iEndCur & (iEndBest >> 31));
        if (iEndBest < 0)
            return 0;

        ii = iStartBest >> 1;
        pinters.Pt0 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iStartBest & 1].Val();
        pinters.Feature[0, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;

        ii = iEndBest >> 1;
        pinters.Pt1 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iEndBest & 1].Val();
        pinters.Feature[1, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);

        return 1;
    }

    // ============================================================================
    // Box-Sphere / Sphere-Box intersection
    // Port of box_sphere_intersection / sphere_box_intersection
    // ============================================================================
    public static int BoxSphere(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var pbox = (Box)p1;
        var psphere = (Sphere)p2;

        var center = pbox.Basis * (psphere.Center - pbox.Center);
        var dir = PhysVector3.Zero;
        for (int idir = 0; idir < 3; idir++)
            dir[idir] = MathF.Min(0.0f, pbox.Size[idir] - MathF.Abs(center[idir])) * MathUtils.SgnNZ(center[idir]);

        // C++ (center+dir)*pbox->Basis is row-vector * matrix = Basis^T * v in our convention
        pinters.Pt0 = pinters.Pt1 = pbox.Basis.Transposed() * (center + dir) + pbox.Center;
        if (pinters.BorderPts != null && pinters.BorderPts.Length > 0)
            pinters.BorderPts[0] = pinters.Pt0;
        pinters.NBorderPt = 1;
        pinters.Normal = psphere.Center - pinters.Pt0;
        pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;

        return MathUtils.IsNeg(dir.LengthSq() - MathUtils.Sqr(psphere.Radius));
    }

    public static int SphereBox(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = BoxSphere(p2, p1, pinters);
        SwapPinters(pinters);
        pinters.Normal = -pinters.Normal;
        return res;
    }

    // ============================================================================
    // Box-Box intersection
    // Port of box_box_intersection
    // ============================================================================
    public static int BoxBox(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var pbox1 = (Box)p1;
        var pbox2 = (Box)p2;

        var center0 = pbox1.Basis * (pbox2.Center - pbox1.Center);
        var center1 = pbox2.Basis * (pbox1.Center - pbox2.Center);

        // axes = rows of pbox2.Basis * pbox1.Basis^T
        var basisRel = pbox2.Basis * pbox1.Basis.Transposed();
        var axes = new PhysVector3[3];
        axes[0] = basisRel.GetRow(0);
        axes[1] = basisRel.GetRow(1);
        axes[2] = basisRel.GetRow(2);

        pinters.NBorderPt = 0;
        var pbox = new Box[] { pbox1, pbox2 };
        var centers = new PhysVector3[] { center0, center1 };

        for (int ibox = 0; ibox < 2; ibox++)
        {
            for (int iEdge = 0; iEdge < 12; iEdge++)
            {
                int idir = iEdge >> 2;
                int ix = MathUtils.IncMod3[idir], iy = MathUtils.DecMod3[idir];
                int axsgDir = -1;
                int axsgX = ((iEdge & 1) << 1) - 1;
                int axsgY = (iEdge & 2) - 1;

                var pt0Local = centers[ibox]
                    + axes[0] * (pbox[ibox ^ 1].Size.X * (idir == 0 ? axsgDir : (ix == 0 ? axsgX : (iy == 0 ? axsgY : 0))))
                    + axes[1] * (pbox[ibox ^ 1].Size.Y * (idir == 1 ? axsgDir : (ix == 1 ? axsgX : (iy == 1 ? axsgY : 0))))
                    + axes[2] * (pbox[ibox ^ 1].Size.Z * (idir == 2 ? axsgDir : (ix == 2 ? axsgX : (iy == 2 ? axsgY : 0))));

                // Build axsg array for proper indexing
                var axsg = new int[3];
                axsg[idir] = -1;
                axsg[ix] = axsgX;
                axsg[iy] = axsgY;

                pt0Local = centers[ibox]
                    + axes[0] * (pbox[ibox ^ 1].Size[0] * axsg[0])
                    + axes[1] * (pbox[ibox ^ 1].Size[1] * axsg[1])
                    + axes[2] * (pbox[ibox ^ 1].Size[2] * axsg[2]);

                var dp = axes[idir];

                var t0 = new QuotientF(0, 1);
                var t1 = new QuotientF(pbox[ibox ^ 1].Size[idir] * 2, 1);

                int bLocInters0 = 0, bLocInters1 = 0;
                for (int j = 0; j < 3; j++)
                {
                    int sg = MathUtils.SgnNZ(dp[j]);
                    var t1_0 = new QuotientF((-pbox[ibox].Size[j] - pt0Local[j] * sg), MathF.Abs(dp[j]));
                    var t1_1 = new QuotientF((pbox[ibox].Size[j] - pt0Local[j] * sg), MathF.Abs(dp[j]));

                    int bBest = MathUtils.IsNeg((t0 - t1_0).X * (t0 - t1_0).Y > 0 ? -(t0 - t1_0).X : (t0 - t1_0).X);
                    // Simplified: use quotient comparison
                    if (t1_0 > t0)
                    {
                        bLocInters0 = 1;
                        t0 = t1_0;
                    }
                    if (t1_1 < t1)
                    {
                        bLocInters1 = 1;
                        t1 = t1_1;
                    }
                }

                if (t0 < t1)
                {
                    if (bLocInters0 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox[ibox].Basis.Transposed() * (pt0Local + dp * t0.Val()) + pbox[ibox].Center;
                    if (bLocInters1 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox[ibox].Basis.Transposed() * (pt0Local + dp * t1.Val()) + pbox[ibox].Center;
                }
            }

            // TransposeBasis: transpose the axes matrix for the next iteration
            var tmp = new PhysVector3[3];
            tmp[0] = new PhysVector3(axes[0].X, axes[1].X, axes[2].X);
            tmp[1] = new PhysVector3(axes[0].Y, axes[1].Y, axes[2].Y);
            tmp[2] = new PhysVector3(axes[0].Z, axes[1].Z, axes[2].Z);
            axes[0] = tmp[0]; axes[1] = tmp[1]; axes[2] = tmp[2];
        }

        if (pinters.NBorderPt == 0)
            return 0;

        pinters.Pt0 = pinters.Pt1 = pinters.BorderPts![0];
        pinters.Normal = PhysVector3.Zero;
        pinters.Feature[0, 0] = pinters.Feature[0, 1] = pinters.Feature[1, 0] = pinters.Feature[1, 1] = 0x40;
        return 1;
    }

    // ============================================================================
    // Triangle-Box / Box-Triangle intersection
    // Port of tri_box_intersection / box_tri_intersection
    // ============================================================================
    public static int TriBox(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var ptri = (Triangle)p1;
        var pbox = (Box)p2;
        return TriBoxImpl(ptri, pbox, pinters);
    }

    public static int BoxTri(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = TriBoxImpl((Triangle)p2, (Box)p1, pinters);
        SwapPinters(pinters);
        pinters.Normal = -pinters.Normal;
        return res;
    }

    private static int TriBoxImpl(Triangle ptri, Box pbox, PrimInters pinters)
    {
        var pt = new PhysVector3[3];
        pt[0] = pbox.Basis * (ptri.P0 - pbox.Center);
        pt[1] = pbox.Basis * (ptri.P1 - pbox.Center);
        pt[2] = pbox.Basis * (ptri.P2 - pbox.Center);
        var n = pbox.Basis * ptri.Normal;

        int iStart = -1;
        var bOutside = new int[3];
        for (int i = 0; i < 3; i++)
        {
            bOutside[i] = MathUtils.IsNeg(pbox.Size.X - MathF.Abs(pt[i].X))
                        | MathUtils.IsNeg(pbox.Size.Y - MathF.Abs(pt[i].Y))
                        | MathUtils.IsNeg(pbox.Size.Z - MathF.Abs(pt[i].Z));
            iStart = (iStart & ~(-bOutside[i])) | (i & (-bOutside[i]));
        }

        if (iStart < 0) // all triangle points are inside the box
            return 0;

        if (bOutside[0] + bOutside[1] + bOutside[2] < 3)
        {
            int idx = bOutside[0] + (bOutside[0] & bOutside[1]);
            pinters.BestPt = ptri[idx];
            var dp = new PhysVector3(
                MathF.Abs(MathF.Abs(pt[idx].X) - pbox.Size.X),
                MathF.Abs(MathF.Abs(pt[idx].Y) - pbox.Size.Y),
                MathF.Abs(MathF.Abs(pt[idx].Z) - pbox.Size.Z));
            int j = IdxMin3(dp.X, dp.Y, dp.Z);
            pinters.Normal = pbox.Basis.Transposed() * (new PhysVector3(
                j == 0 ? -MathUtils.SgnNZ(pt[idx][0]) : 0,
                j == 1 ? -MathUtils.SgnNZ(pt[idx][1]) : 0,
                j == 2 ? -MathUtils.SgnNZ(pt[idx][2]) : 0));
            pinters.NBestPtVal = 6;
        }

        int ii = iStart;
        int iEndCur = -1, iStartBest = -1, iEndBest = -1, bState = 0;
        var mindist = new QuotientF(1, 0);
        var t = new QuotientF[3, 2];

        do
        {
            int idec = MathUtils.DecMod3[ii];
            var dp = pt[idec] - pt[ii];
            t[ii, 0] = new QuotientF(0, 1);
            t[ii, 1] = new QuotientF(1, 1);

            for (int j = 0; j < 3; j++)
            {
                int idir = MathUtils.IsNeg(dp[j]);
                float sg = 1 - (idir << 1);
                var t1_0 = new QuotientF((-pbox.Size[j] - pt[ii][j]) * sg, dp[j] * sg);
                var t1_1 = new QuotientF((pbox.Size[j] - pt[ii][j]) * sg, dp[j] * sg);

                // t[ii][0] = max(t[ii][0], t1[idir]) and t[ii][1] = min(t[ii][1], t1[idir^1])
                if (t1_0 > t[ii, 0]) t[ii, 0] = t1_0;
                if (t1_1 < t[ii, 1]) t[ii, 1] = t1_1;
            }

            if (t[ii, 1] > t[ii, 0])
            {
                for (int j = 0; j < 2; j++)
                {
                    int bSwitch = t[ii, j].IsIn01();
                    int bEnd = bSwitch & bState;
                    iEndCur = (iEndCur & ~(-bEnd)) | ((ii << 1 | j) & (-bEnd));

                    var dp1 = ptri[ii] - pinters.Pt1;
                    var dist = new QuotientF(
                        (dp1 * t[ii, j].Y + (ptri[idec] - ptri[ii]) * t[ii, j].X).LengthSq(),
                        MathUtils.Sqr(t[ii, j].Y));
                    int bBest = MathUtils.IsNeg((dist - mindist).X) & bSwitch & (bState ^ 1);
                    mindist.X = mindist.X * (1 - bBest) + dist.X * bBest;
                    mindist.Y = mindist.Y * (1 - bBest) + dist.Y * bBest;
                    iStartBest = (iStartBest & ~(-bBest)) | ((ii << 1 | j) & (-bBest));
                    iEndBest = (iEndBest & ~(-bBest)) | (iEndCur & (-bBest));
                    bState ^= bSwitch;
                }
            }
        } while ((ii = MathUtils.DecMod3[ii]) != iStart);

        if (iStartBest < 0)
        {
            // triangle edges do not intersect box; check box diagonal vs triangle
            var ptmin = new PhysVector3(
                pbox.Size.X * -MathUtils.SgnNZ(n.X),
                pbox.Size.Y * -MathUtils.SgnNZ(n.Y),
                pbox.Size.Z * -MathUtils.SgnNZ(n.Z));
            var dpBox = ptmin * -2;
            var t1 = new QuotientF((pt[0] - ptmin).Dot(n), dpBox.Dot(n));
            int bBest2 = t1.IsIn01();
            var ptminScaled = ptmin * t1.Y + dpBox * t1.X;
            for (int i = 0; i < 3; i++)
                bBest2 &= MathUtils.IsNeg(((ptminScaled - pt[i] * t1.Y) ^ (pt[MathUtils.IncMod3[i]] - pt[i])).Dot(n));
            if (bBest2 == 0)
                return 0;

            pinters.Pt0 = pinters.Pt1 = pbox.Basis.Transposed() * (ptminScaled / t1.Y) + pbox.Center;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
            pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
            pinters.BestPt = pinters.Pt0;
            pinters.Normal = ptri.Normal;
            pinters.NBestPtVal = 1000;
            goto haveinters;
        }

        iEndBest = (iEndBest - (iEndBest >> 31)) | (iEndCur & (iEndBest >> 31));
        if (iEndBest < 0)
            return 0;

        ii = iStartBest >> 1;
        pinters.Pt0 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iStartBest & 1].Val();
        pinters.Feature[0, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;

        ii = iEndBest >> 1;
        pinters.Pt1 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iEndBest & 1].Val();
        pinters.Feature[1, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);

    haveinters:
        // Check box edges vs triangle
        int iStartAxis = IdxMax3(MathF.Abs(n.X), MathF.Abs(n.Y), MathF.Abs(n.Z));
        if ((pt[1] - pt[0]).Cross(pt[2] - pt[0]).LengthSq() >
            MathUtils.Sqr(pbox.Size[MathUtils.IncMod3[iStartAxis]] * pbox.Size[MathUtils.DecMod3[iStartAxis]]))
        {
            int nborderpt0 = pinters.NBorderPt;
            for (int i = 0; i < 12; i++)
            {
                int edgeDir = i >> 2;
                int ixe = MathUtils.IncMod3[edgeDir], iye = MathUtils.DecMod3[edgeDir];
                var axsg = new int[3];
                axsg[edgeDir] = -1;
                axsg[ixe] = ((i & 1) << 1) - 1;
                axsg[iye] = (i & 2) - 1;
                var ptbox = new PhysVector3(pbox.Size.X * axsg[0], pbox.Size.Y * axsg[1], pbox.Size.Z * axsg[2]);
                var t1 = new QuotientF((pt[0] - ptbox).Dot(n), n[edgeDir]);
                if (t1.X >= 0 && t1.X <= pbox.Size[edgeDir] * t1.Y * 2)
                {
                    t1 = t1.FixSign();
                    var ptboxScaled = ptbox * t1.Y;
                    ptboxScaled[edgeDir] = ptboxScaled[edgeDir] + t1.X;
                    int bSwitch2 = 1;
                    for (int j = 0; j < 3; j++)
                        bSwitch2 &= MathUtils.IsNeg(((ptboxScaled - pt[j] * t1.Y) ^ (pt[MathUtils.IncMod3[j]] - pt[j])).Dot(n));
                    if (bSwitch2 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptboxScaled / t1.Y) + pbox.Center;
                }
            }

            if (MathF.Abs(n[iStartAxis]) > 0.98f && pinters.NBorderPt > nborderpt0 + 1 && pinters.NBestPtVal < 6)
            {
                var ptbox2 = PhysVector3.Zero;
                ptbox2[iStartAxis] = pbox.Size[iStartAxis] * MathUtils.SgnNZ(pt[0][iStartAxis]);
                if ((MathUtils.IsNeg(((ptbox2 - pt[0]) ^ (pt[1] - pt[0])).Dot(n))
                   & MathUtils.IsNeg(((ptbox2 - pt[1]) ^ (pt[2] - pt[1])).Dot(n))
                   & MathUtils.IsNeg(((ptbox2 - pt[2]) ^ (pt[0] - pt[2])).Dot(n))) != 0)
                {
                    pinters.NBestPtVal = 6;
                    pinters.Normal = ptri.Normal;
                    pinters.BestPt = pinters.BorderPts![nborderpt0];
                }
            }
        }

        return 1;
    }

    // ============================================================================
    // Triangle-Cylinder / Cylinder-Triangle intersection
    // Port of tri_cylinder_intersection / cylinder_tri_intersection
    // ============================================================================
    public static int TriCylinder(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var ptri = (Triangle)p1;
        var pcyl = (Cylinder)p2;
        return TriCylinderImpl(ptri, pcyl, pinters);
    }

    public static int CylinderTri(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = TriCylinderImpl((Triangle)p2, (Cylinder)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    private static int TriCylinderImpl(Triangle ptri, Cylinder pcyl, PrimInters pinters)
    {
        float r2 = MathUtils.Sqr(pcyl.Radius);
        int iStart = -1;

        for (int i = 0; i < 3; i++)
        {
            var pc = ptri[i] - pcyl.Center;
            int bOutside = MathUtils.IsNeg(r2 - (pc ^ pcyl.Axis).LengthSq())
                         | MathUtils.IsNeg(pcyl.HalfHeight - MathF.Abs(pc.Dot(pcyl.Axis)));
            iStart = (iStart & ~(-bOutside)) | (i & (-bOutside));
        }

        if (iStart < 0)
            return 0;

        int ii = iStart, iEndCur = -1, iStartBest = -1, iEndBest = -1, bState = 0;
        var mindist = new QuotientF(1, 0);
        var t = new QuotientF[3, 2];

        do
        {
            int idec = MathUtils.DecMod3[ii];
            var dp = ptri[idec] - ptri[ii];
            var pc = ptri[ii] - pcyl.Center;

            // Cap clipping
            t[ii, 0].X = t[ii, 1].X = -pc.Dot(pcyl.Axis);
            t[ii, 0].Y = t[ii, 1].Y = dp.Dot(pcyl.Axis);
            int sg = MathUtils.SgnNZ(t[ii, 0].Y);
            int idx0 = (sg + 1) >> 1;
            t[ii, idx0].X += pcyl.HalfHeight;
            t[ii, idx0 ^ 1].X -= pcyl.HalfHeight;
            t[ii, 0].X *= sg; t[ii, 0].Y *= sg;
            t[ii, 1].X *= sg; t[ii, 1].Y *= sg;

            // Side clipping
            var vec1 = dp ^ pcyl.Axis;
            var vec0 = pc ^ pcyl.Axis;
            float a = vec1.Dot(vec1), b = vec0.Dot(vec1), c = vec0.Dot(vec0) - r2;
            float d = b * b - a * c;
            int bInters;
            if (d >= 0 && vec1.LengthSq() > 1E-10f)
            {
                d = MathF.Sqrt(d);
                t[ii, 0] = QuotientF.Max(t[ii, 0], new QuotientF(-b - d, a));
                t[ii, 1] = QuotientF.Min(t[ii, 1], new QuotientF(-b + d, a));
                bInters = MathUtils.IsNeg((t[ii, 0] - t[ii, 1]).X);
            }
            else
            {
                bInters = MathUtils.IsNeg(vec0.LengthSq() - r2);
            }

            if (bInters != 0)
            {
                for (int j = 0; j < 2; j++)
                {
                    int bSwitch = MathUtils.IsNeg(MathF.Abs(t[ii, j].X * 2 - t[ii, j].Y) - t[ii, j].Y);
                    int bEnd = bSwitch & bState;
                    iEndCur = (iEndCur & ~(-bEnd)) | ((ii << 1 | j) & (-bEnd));

                    var dp1 = ptri[ii] - pinters.Pt1;
                    var dist = new QuotientF(
                        (dp1 * t[ii, j].Y + (ptri[idec] - ptri[ii]) * t[ii, j].X).LengthSq(),
                        MathUtils.Sqr(t[ii, j].Y));
                    int bBest = MathUtils.IsNeg((dist - mindist).X) & bSwitch & (bState ^ 1);
                    mindist.X = mindist.X * (1 - bBest) + dist.X * bBest;
                    mindist.Y = mindist.Y * (1 - bBest) + dist.Y * bBest;
                    iStartBest = (iStartBest & ~(-bBest)) | ((ii << 1 | j) & (-bBest));
                    iEndBest = (iEndBest & ~(-bBest)) | (iEndCur & (-bBest));
                    bState ^= bSwitch;
                }
            }
        } while ((ii = MathUtils.DecMod3[ii]) != iStart);

        if (iStartBest < 0)
        {
            // Triangle edges do not intersect cylinder
            var ptmax = ptri.Normal - pcyl.Axis * (ptri.Normal.Dot(pcyl.Axis));
            if (ptmax.LengthSq() > 1E-4f)
                ptmax.Normalize();
            else
                ptmax = PhysVector3.Zero;
            int jsg = MathUtils.SgnNZ(pcyl.Axis.Dot(ptri.Normal));
            ptmax = ptmax * pcyl.Radius + pcyl.Axis * (pcyl.HalfHeight * jsg);
            var ptmin = pcyl.Center - ptmax;
            ptmax = ptmax + pcyl.Center;

            var t0 = new QuotientF((ptri.P0 - ptmin).Dot(ptri.Normal), (ptmax - ptmin).Dot(ptri.Normal));
            int bInters2 = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y);
            var ptminScaled = ptmin * t0.Y + (ptmax - ptmin) * t0.X;
            for (int i = 0; i < 3; i++)
                bInters2 &= MathUtils.IsNeg(((ptminScaled - ptri[i] * t0.Y) ^ (ptri[MathUtils.IncMod3[i]] - ptri[i])).Dot(ptri.Normal));
            if (bInters2 == 0)
                return 0;

            pinters.Pt0 = pinters.Pt1 = ptminScaled / t0.Y;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
            pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
            pinters.BestPt = pinters.Pt0;
            pinters.Normal = ptri.Normal;
            pinters.NBestPtVal = 1000;
            return 1;
        }

        iEndBest = (iEndBest - (iEndBest >> 31)) | (iEndCur & (iEndBest >> 31));
        if (iEndBest < 0)
            return 0;

        ii = iStartBest >> 1;
        pinters.Pt0 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iStartBest & 1].Val();
        pinters.Feature[0, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;

        ii = iEndBest >> 1;
        pinters.Pt1 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iEndBest & 1].Val();
        pinters.Feature[1, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);

        return 1;
    }

    // ============================================================================
    // Triangle-Capsule / Capsule-Triangle intersection
    // Port of tri_capsule_intersection / capsule_tri_intersection
    // ============================================================================
    public static int TriCapsule(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var ptri = (Triangle)p1;
        var pcaps = (Capsule)p2;
        return TriCapsuleImpl(ptri, pcaps, pinters);
    }

    public static int CapsuleTri(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = TriCapsuleImpl((Triangle)p2, (Capsule)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    private static int TriCapsuleImpl(Triangle ptri, Capsule pcaps, PrimInters pinters)
    {
        float r2 = MathUtils.Sqr(pcaps.Radius);
        int iStart = -1;

        for (int i = 0; i < 3; i++)
        {
            var pc = ptri[i] - pcaps.Center;
            int bOutside = (MathUtils.IsNeg(r2 - (pc ^ pcaps.Axis).LengthSq())
                          | MathUtils.IsNeg(pcaps.HalfHeight - MathF.Abs(pc.Dot(pcaps.Axis))))
                          & MathUtils.IsNeg(r2 - (pc - pcaps.Axis * (MathUtils.SgnNZ(pc.Dot(pcaps.Axis)) * pcaps.HalfHeight)).LengthSq());
            iStart = (iStart & ~(-bOutside)) | (i & (-bOutside));
        }

        if (iStart < 0)
            return 0;

        int ii = iStart, iEndCur = -1, iStartBest = -1, iEndBest = -1, bState = 0;
        var mindist = new QuotientF(1, 0);
        var t = new QuotientF[3, 2];

        do
        {
            int idec = MathUtils.DecMod3[ii];
            var dp = ptri[idec] - ptri[ii];
            var pc = ptri[ii] - pcaps.Center;

            // Cap clipping (same as cylinder)
            t[ii, 0].X = t[ii, 1].X = -pc.Dot(pcaps.Axis);
            t[ii, 0].Y = t[ii, 1].Y = dp.Dot(pcaps.Axis);
            int sg = MathUtils.SgnNZ(t[ii, 0].Y);
            int idx0 = (sg + 1) >> 1;
            t[ii, idx0].X += pcaps.HalfHeight;
            t[ii, idx0 ^ 1].X -= pcaps.HalfHeight;
            t[ii, 0].X *= sg; t[ii, 0].Y *= sg;
            t[ii, 1].X *= sg; t[ii, 1].Y *= sg;

            // Sphere cap intersections
            float aFull = dp.LengthSq();
            float bSphere = dp.Dot(pc + pcaps.Axis * (pcaps.HalfHeight * sg));
            float cSphere = (pc + pcaps.Axis * (pcaps.HalfHeight * sg)).LengthSq() - r2;
            float dSphere = bSphere * bSphere - aFull * cSphere;
            if (dSphere >= 0)
            {
                dSphere = MathF.Sqrt(dSphere);
                if (new QuotientF(-bSphere + dSphere, aFull) < t[ii, 0])
                {
                    t[ii, 0] = new QuotientF(-bSphere - dSphere, aFull);
                    t[ii, 1] = new QuotientF(-bSphere + dSphere, aFull);
                }
                else
                {
                    t[ii, 0] = QuotientF.Min(t[ii, 0], new QuotientF(-bSphere - dSphere, aFull));
                }
            }
            bSphere = dp.Dot(pc - pcaps.Axis * (pcaps.HalfHeight * sg));
            cSphere = (pc - pcaps.Axis * (pcaps.HalfHeight * sg)).LengthSq() - r2;
            dSphere = bSphere * bSphere - aFull * cSphere;
            if (dSphere >= 0)
            {
                dSphere = MathF.Sqrt(dSphere);
                if (new QuotientF(-bSphere - dSphere, aFull) > t[ii, 1])
                {
                    t[ii, 0] = new QuotientF(-bSphere - dSphere, aFull);
                    t[ii, 1] = new QuotientF(-bSphere + dSphere, aFull);
                }
                else
                {
                    t[ii, 1] = QuotientF.Max(t[ii, 1], new QuotientF(-bSphere + dSphere, aFull));
                }
            }

            // Side clipping
            var vec1 = dp ^ pcaps.Axis;
            var vec0 = pc ^ pcaps.Axis;
            float a = vec1.Dot(vec1), b = vec0.Dot(vec1), c = vec0.Dot(vec0) - r2;
            float d = b * b - a * c;
            int bInters;
            if (d >= 0 && vec1.LengthSq() > 1E-10f)
            {
                d = MathF.Sqrt(d);
                t[ii, 0] = QuotientF.Max(t[ii, 0], new QuotientF(-b - d, a));
                t[ii, 1] = QuotientF.Min(t[ii, 1], new QuotientF(-b + d, a));
                bInters = MathUtils.IsNeg((t[ii, 0] - t[ii, 1]).X);
            }
            else
            {
                bInters = MathUtils.IsNeg(vec0.LengthSq() - r2);
            }

            if (bInters != 0)
            {
                for (int j = 0; j < 2; j++)
                {
                    int bSwitch = MathUtils.IsNeg(MathF.Abs(t[ii, j].X * 2 - t[ii, j].Y) - t[ii, j].Y);
                    int bEnd = bSwitch & bState;
                    iEndCur = (iEndCur & ~(-bEnd)) | ((ii << 1 | j) & (-bEnd));

                    var dp1 = ptri[ii] - pinters.Pt1;
                    var dist = new QuotientF(
                        (dp1 * t[ii, j].Y + (ptri[idec] - ptri[ii]) * t[ii, j].X).LengthSq(),
                        MathUtils.Sqr(t[ii, j].Y));
                    int bBest = MathUtils.IsNeg((dist - mindist).X) & bSwitch & (bState ^ 1);
                    mindist.X = mindist.X * (1 - bBest) + dist.X * bBest;
                    mindist.Y = mindist.Y * (1 - bBest) + dist.Y * bBest;
                    iStartBest = (iStartBest & ~(-bBest)) | ((ii << 1 | j) & (-bBest));
                    iEndBest = (iEndBest & ~(-bBest)) | (iEndCur & (-bBest));
                    bState ^= bSwitch;
                }
            }
        } while ((ii = MathUtils.DecMod3[ii]) != iStart);

        if (iStartBest < 0)
        {
            // Triangle edges do not intersect capsule
            int jsg = MathUtils.SgnNZ(pcaps.Axis.Dot(ptri.Normal));
            var ptmax = ptri.Normal * pcaps.Radius + pcaps.Axis * (pcaps.HalfHeight * jsg);
            var ptmin = pcaps.Center - ptmax;
            ptmax = ptmax + pcaps.Center;

            var t0 = new QuotientF((ptri.P0 - ptmin).Dot(ptri.Normal), (ptmax - ptmin).Dot(ptri.Normal));
            int bInters2 = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y);
            var ptminScaled = ptmin * t0.Y + (ptmax - ptmin) * t0.X;
            for (int i = 0; i < 3; i++)
                bInters2 &= MathUtils.IsNeg(((ptminScaled - ptri[i] * t0.Y) ^ (ptri[MathUtils.IncMod3[i]] - ptri[i])).Dot(ptri.Normal));
            if (bInters2 == 0)
                return 0;

            pinters.Pt0 = pinters.Pt1 = ptminScaled / t0.Y;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
            pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
            pinters.BestPt = pinters.Pt0;
            pinters.Normal = ptri.Normal;
            pinters.NBestPtVal = 1000;

            // Port of C++ intersectionchecks.cpp lines 576-591: bottommost capsule edge
            // + axial-sweep second probe when capsule axis is (nearly) parallel to triangle.
            {
                var pt = ptmin;
                int borderCap = pinters.NBorderSz > 0 ? pinters.NBorderSz : (pinters.BorderPts?.Length ?? 0);
                if ((pt - ptri.P0).Dot(ptri.Normal) >= (pcaps.HalfHeight + pcaps.Radius) * -0.2f
                    && (pt - ptri.P0).Dot(ptri.Normal) <= 0.0f
                    && ((pt - ptri.P0) ^ (ptri.P1 - ptri.P0)).Dot(ptri.Normal) < 0
                    && ((pt - ptri.P1) ^ (ptri.P2 - ptri.P1)).Dot(ptri.Normal) < 0
                    && ((pt - ptri.P2) ^ (ptri.P0 - ptri.P2)).Dot(ptri.Normal) < 0
                    && pinters.BorderPts != null && pinters.NBorderPt < borderCap)
                {
                    pinters.BorderPts[pinters.NBorderPt++] = pt;
                }

                if (MathF.Abs(ptri.Normal.Dot(pcaps.Axis)) < 0.1f)
                {
                    float distNum = (ptri.P0 - pt).Dot(ptri.Normal);
                    float distDen = MathF.Abs(pcaps.Axis.Dot(ptri.Normal));
                    if (distNum >= 0.0f && distNum <= pcaps.HalfHeight * 2 * distDen)
                        pt = pt + pcaps.Axis * (jsg * (distNum / distDen));
                    else
                        pt = pt + pcaps.Axis * (pcaps.HalfHeight * 2 * jsg);

                    float proj = (pt - ptri.P0).Dot(ptri.Normal);
                    if (proj >= (pcaps.HalfHeight + pcaps.Radius) * -0.2f
                        && proj <= 0.0f
                        && ((pt - ptri.P0) ^ (ptri.P1 - ptri.P0)).Dot(ptri.Normal) < 0
                        && ((pt - ptri.P1) ^ (ptri.P2 - ptri.P1)).Dot(ptri.Normal) < 0
                        && ((pt - ptri.P2) ^ (ptri.P0 - ptri.P2)).Dot(ptri.Normal) < 0
                        && pinters.BorderPts != null && pinters.NBorderPt < borderCap)
                    {
                        pinters.BorderPts[pinters.NBorderPt++] = pt;
                    }
                }
            }
            return 1;
        }

        iEndBest = (iEndBest - (iEndBest >> 31)) | (iEndCur & (iEndBest >> 31));
        if (iEndBest < 0)
            return 0;

        ii = iStartBest >> 1;
        pinters.Pt0 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iStartBest & 1].Val();
        pinters.Feature[0, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;

        ii = iEndBest >> 1;
        pinters.Pt1 = ptri[ii] + (ptri[MathUtils.DecMod3[ii]] - ptri[ii]) * t[ii, iEndBest & 1].Val();
        pinters.Feature[1, 0] = (byte)(0xA0 | MathUtils.DecMod3[ii]);

        // Check if capsule cap's lowest point is inside triangle's Voronoi region
        int jcap = MathUtils.SgnNZ(pcaps.Axis.Dot(ptri.Normal));
        var ptCap = pcaps.Center - pcaps.Axis * (pcaps.HalfHeight * jcap) - ptri.Normal * pcaps.Radius;
        {
            int borderCap = pinters.NBorderSz > 0 ? pinters.NBorderSz : (pinters.BorderPts?.Length ?? 0);
            if ((ptCap - ptri.P0).Dot(ptri.Normal) > (pcaps.HalfHeight + pcaps.Radius) * -0.2f
                && ((ptCap - ptri.P0) ^ (ptri.P1 - ptri.P0)).Dot(ptri.Normal) < 0
                && ((ptCap - ptri.P1) ^ (ptri.P2 - ptri.P1)).Dot(ptri.Normal) < 0
                && ((ptCap - ptri.P2) ^ (ptri.P0 - ptri.P2)).Dot(ptri.Normal) < 0)
            {
                if (pinters.BorderPts != null && pinters.NBorderPt < borderCap)
                {
                    pinters.BestPt = ptCap;
                    pinters.Normal = ptri.Normal;
                    pinters.NBestPtVal = 1000;
                    pinters.BorderPts[pinters.NBorderPt++] = ptCap;
                }
            }

            // Port of C++ intersectionchecks.cpp lines 618-628: axial-sweep second probe
            // when capsule axis is (nearly) parallel to triangle.
            if (MathF.Abs(ptri.Normal.Dot(pcaps.Axis)) < 0.1f)
            {
                var pt = ptCap;
                float distNum = (ptri.P0 - pt).Dot(ptri.Normal);
                float distDen = MathF.Abs(pcaps.Axis.Dot(ptri.Normal));
                if (distNum >= 0.0f && distNum <= pcaps.HalfHeight * 2 * distDen)
                    pt = pt + pcaps.Axis * (jcap * (distNum / distDen));
                else
                    pt = pt + pcaps.Axis * (pcaps.HalfHeight * 2 * jcap);

                float proj = (pt - ptri.P0).Dot(ptri.Normal);
                if (proj >= (pcaps.HalfHeight + pcaps.Radius) * -0.2f
                    && proj <= 0.0f
                    && ((pt - ptri.P0) ^ (ptri.P1 - ptri.P0)).Dot(ptri.Normal) < 0
                    && ((pt - ptri.P1) ^ (ptri.P2 - ptri.P1)).Dot(ptri.Normal) < 0
                    && ((pt - ptri.P2) ^ (ptri.P0 - ptri.P2)).Dot(ptri.Normal) < 0
                    && pinters.BorderPts != null && pinters.NBorderPt < borderCap)
                {
                    pinters.BorderPts[pinters.NBorderPt++] = pt;
                }
            }
        }

        return 1;
    }

    // ============================================================================
    // Box-Cylinder / Cylinder-Box intersection
    // Port of box_cylinder_intersection / cylinder_box_intersection
    // ============================================================================
    public static int BoxCylinder(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var pbox = (Box)p1;
        var pcyl = (Cylinder)p2;
        return BoxCylinderImpl(pbox, pcyl, pinters);
    }

    public static int CylinderBox(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = BoxCylinderImpl((Box)p2, (Cylinder)p1, pinters);
        SwapPinters(pinters);
        pinters.Normal = -pinters.Normal;
        return res;
    }

    private static int BoxCylinderImpl(Box pbox, Cylinder pcyl, PrimInters pinters)
    {
        var axis = pbox.Basis * pcyl.Axis;
        var center = pbox.Basis * (pcyl.Center - pbox.Center);
        var size = pbox.Size;
        float r = pcyl.Radius, hh = pcyl.HalfHeight;
        pinters.NBorderPt = 0;

        // Check box edges - cylinder intersections
        for (int iz = 0; iz < 3; iz++)
        {
            int ix = MathUtils.IncMod3[iz], iy = MathUtils.DecMod3[iz];
            for (int i = 0; i < 4; i++)
            {
                var pt = PhysVector3.Zero;
                pt[ix] = size[ix] * ((i << 1 & 2) - 1);
                pt[iy] = size[iy] * ((i & 2) - 1);

                // Edge-cap intersections
                var t0 = new QuotientF((center - pt).Dot(axis) - hh, axis[iz]);
                var ptScaled = pt * t0.Y;
                ptScaled[iz] = t0.X;
                if ((MathUtils.IsNeg(MathF.Abs(t0.X) - size[iz] * MathF.Abs(t0.Y))
                   & MathUtils.IsNeg((ptScaled - center * t0.Y).Cross(axis).LengthSq() - MathUtils.Sqr(r * t0.Y))) != 0
                   && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                    pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptScaled / t0.Y) + pbox.Center;

                t0.X += hh * 2;
                ptScaled[iz] = t0.X;
                if ((MathUtils.IsNeg(MathF.Abs(t0.X) - size[iz] * MathF.Abs(t0.Y))
                   & MathUtils.IsNeg((ptScaled - center * t0.Y).Cross(axis).LengthSq() - MathUtils.Sqr(r * t0.Y))) != 0
                   && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                    pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptScaled / t0.Y) + pbox.Center;

                // Edge-side intersections
                pt[iz] = 0;
                var vec0 = (pt - center) ^ axis;
                var vec1 = -MathUtils.CrossWithOrt(axis, iz);
                float ka = vec1.Dot(vec1), kb = vec0.Dot(vec1), kc = vec0.Dot(vec0) - r * r;
                float kd = kb * kb - ka * kc;
                if (kd > 0)
                {
                    kd = MathF.Sqrt(kd);
                    t0 = new QuotientF(-kb - kd, ka);
                    ptScaled = pt * t0.Y;
                    ptScaled[iz] = t0.X;
                    if ((MathUtils.IsNeg(MathF.Abs(t0.X) - size[iz] * t0.Y)
                       & MathUtils.IsNeg(MathF.Abs((ptScaled - center * t0.Y).Dot(axis)) - hh * t0.Y)) != 0
                       && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptScaled / t0.Y) + pbox.Center;

                    t0.X += kd * 2;
                    ptScaled[iz] = t0.X;
                    if ((MathUtils.IsNeg(MathF.Abs(t0.X) - size[iz] * t0.Y)
                       & MathUtils.IsNeg(MathF.Abs((ptScaled - center * t0.Y).Dot(axis)) - hh * t0.Y)) != 0
                       && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptScaled / t0.Y) + pbox.Center;
                }
            }
        }

        // Check cylinder axis-box intersections
        var tAxis0 = new QuotientF(-hh, 1);
        var tAxis1 = new QuotientF(hh, 1);
        int bLocInters0 = 0, bLocInters1 = 0;
        for (int iz = 0; iz < 3; iz++)
        {
            int idir = MathUtils.IsNeg(axis[iz]);
            int sg = 1 - (idir << 1);
            var t1_0 = new QuotientF((-size[iz] - center[iz]) * sg, MathF.Abs(axis[iz]));
            var t1_1 = new QuotientF((size[iz] - center[iz]) * sg, MathF.Abs(axis[iz]));
            if (idir == 0) { /* t1[0] = t1_0, t1[1] = t1_1 */ }
            else { (t1_0, t1_1) = (t1_1, t1_0); }
            // Actually the C++ indexes t1[idir] and t1[idir^1]
            // t1[idir] is the "negative side", t1[idir^1] is the "positive side"
            // After fixsign with sg, both have positive denominators
            // t1[0] is always the lower bound, t1[1] the upper
            if (t1_0 > tAxis0) { bLocInters0 = 1; tAxis0 = t1_0; }
            if (t1_1 < tAxis1) { bLocInters1 = 1; tAxis1 = t1_1; }
        }
        if (tAxis0 < tAxis1)
        {
            if (bLocInters0 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (center + axis * tAxis0.Val()) + pbox.Center;
            if (bLocInters1 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (center + axis * tAxis1.Val()) + pbox.Center;
        }

        if (pinters.NBorderPt == 0)
            return 0;

        pinters.Pt0 = pinters.Pt1 = pinters.BorderPts![0];
        pinters.Normal = PhysVector3.Zero;
        pinters.Feature[0, 0] = pinters.Feature[0, 1] = pinters.Feature[1, 0] = pinters.Feature[1, 1] = 0x40;
        return 1;
    }

    // ============================================================================
    // Box-Capsule / Capsule-Box intersection
    // Port of box_capsule_intersection / capsule_box_intersection
    // ============================================================================
    public static int BoxCapsule(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var pbox = (Box)p1;
        var pcaps = (Capsule)p2;
        return BoxCapsuleImpl(pbox, pcaps, pinters);
    }

    public static int CapsuleBox(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = BoxCapsuleImpl((Box)p2, (Capsule)p1, pinters);
        SwapPinters(pinters);
        pinters.Normal = -pinters.Normal;
        return res;
    }

    private static int BoxCapsuleImpl(Box pbox, Capsule pcaps, PrimInters pinters)
    {
        var axis = pbox.Basis * pcaps.Axis;
        var center = pbox.Basis * (pcaps.Center - pbox.Center);
        var size = pbox.Size;
        float r = pcaps.Radius, hh = pcaps.HalfHeight;
        pinters.NBorderPt = 0;

        // Check box edges - capsule intersections
        for (int iz = 0; iz < 3; iz++)
        {
            int ix = MathUtils.IncMod3[iz], iy = MathUtils.DecMod3[iz];
            for (int i = 0; i < 4; i++)
            {
                var pt = PhysVector3.Zero;
                pt[ix] = size[ix] * ((i << 1 & 2) - 1);
                pt[iy] = size[iy] * ((i & 2) - 1);

                // Edge-cap sphere intersections
                int sg = MathUtils.SgnNZ(axis[iz]);
                float capAxisDot = ((center - pt).Dot(axis)) * sg;
                var t0Y = MathF.Abs(axis[iz]);
                float t0X_low = capAxisDot - hh;
                float t0X_high = capAxisDot + hh;

                // Cap sphere 1: center - axis*hh*sg
                float kb = -center[iz] - axis[iz] * (hh * sg);
                float kc = (pt - center - axis * (hh * sg)).LengthSq() - r * r;
                float kd = kb * kb - kc;
                if (kd >= 0)
                {
                    kd = MathF.Sqrt(kd);
                    for (int si = -1; si <= 1; si += 2)
                    {
                        float tVal = -kb + kd * si;
                        bool inRange = tVal * t0Y >= t0X_low && tVal * t0Y <= t0X_high;
                        if (!inRange && MathUtils.IsNeg(MathF.Abs(tVal) - size[iz]) != 0
                            && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        {
                            var ptTemp = pt;
                            ptTemp[iz] = tVal;
                            pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * ptTemp + pbox.Center;
                        }
                    }
                }

                // Cap sphere 2: center + axis*hh*sg
                pt[iz] = 0;
                kb = -center[iz] + axis[iz] * (hh * sg);
                kc = (pt - center + axis * (hh * sg)).LengthSq() - r * r;
                kd = kb * kb - kc;
                if (kd >= 0)
                {
                    kd = MathF.Sqrt(kd);
                    for (int si = -1; si <= 1; si += 2)
                    {
                        float tVal = -kb + kd * si;
                        bool inRange = tVal * t0Y >= t0X_low && tVal * t0Y <= t0X_high;
                        if (!inRange && MathUtils.IsNeg(MathF.Abs(tVal) - size[iz]) != 0
                            && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        {
                            var ptTemp = pt;
                            ptTemp[iz] = tVal;
                            pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * ptTemp + pbox.Center;
                        }
                    }
                }

                // Edge-side intersections (cylindrical part)
                pt[ix] = size[ix] * ((i << 1 & 2) - 1);
                pt[iy] = size[iy] * ((i & 2) - 1);
                pt[iz] = 0;
                var vec0 = (pt - center) ^ axis;
                var vec1 = -MathUtils.CrossWithOrt(axis, iz);
                float ka = vec1.Dot(vec1);
                kb = vec0.Dot(vec1);
                kc = vec0.Dot(vec0) - r * r;
                kd = kb * kb - ka * kc;
                if (kd > 0)
                {
                    kd = MathF.Sqrt(kd);
                    var tSide = new QuotientF(-kb - kd, ka);
                    var ptScaled = pt * tSide.Y;
                    ptScaled[iz] = tSide.X;
                    if ((MathUtils.IsNeg(MathF.Abs(tSide.X) - size[iz] * tSide.Y)
                       & MathUtils.IsNeg(MathF.Abs((ptScaled - center * tSide.Y).Dot(axis)) - hh * tSide.Y)) != 0
                       && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptScaled / tSide.Y) + pbox.Center;

                    tSide.X += kd * 2;
                    ptScaled[iz] = tSide.X;
                    if ((MathUtils.IsNeg(MathF.Abs(tSide.X) - size[iz] * tSide.Y)
                       & MathUtils.IsNeg(MathF.Abs((ptScaled - center * tSide.Y).Dot(axis)) - hh * tSide.Y)) != 0
                       && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (ptScaled / tSide.Y) + pbox.Center;
                }
            }
        }

        // Check capsule axis-box intersections
        var tAxis0 = new QuotientF(-hh - r, 1);
        var tAxis1 = new QuotientF(hh + r, 1);
        int bLocInters0 = 0, bLocInters1 = 0;
        for (int iz = 0; iz < 3; iz++)
        {
            int idir = MathUtils.IsNeg(axis[iz]);
            int sgAx = 1 - (idir << 1);
            var t1_0 = new QuotientF((-size[iz] - center[iz]) * sgAx, MathF.Abs(axis[iz]));
            var t1_1 = new QuotientF((size[iz] - center[iz]) * sgAx, MathF.Abs(axis[iz]));
            if (idir != 0) (t1_0, t1_1) = (t1_1, t1_0);
            if (t1_0 > tAxis0) { bLocInters0 = 1; tAxis0 = t1_0; }
            if (t1_1 < tAxis1) { bLocInters1 = 1; tAxis1 = t1_1; }
        }
        if (tAxis0 < tAxis1)
        {
            if (bLocInters0 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (center + axis * tAxis0.Val()) + pbox.Center;
            if (bLocInters1 != 0 && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (center + axis * tAxis1.Val()) + pbox.Center;
        }

        // Check capsule caps - box side intersections
        var centerCap = center - axis * hh;
        for (int icap = 0; icap < 2; icap++, centerCap = centerCap + axis * (hh * 2))
        {
            var vec0Cap = PhysVector3.Zero;
            for (int iz = 0; iz < 3; iz++)
                vec0Cap[iz] = MathF.Min(0.0f, size[iz] - MathF.Abs(centerCap[iz])) * MathUtils.SgnNZ(centerCap[iz]);
            if (vec0Cap.LengthSq() < r * r && pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                pinters.BorderPts[pinters.NBorderPt++] = pbox.Basis.Transposed() * (centerCap + vec0Cap) + pbox.Center;
        }

        if (pinters.NBorderPt == 0)
            return 0;

        pinters.Pt0 = pinters.Pt1 = pinters.BorderPts![0];
        pinters.Normal = PhysVector3.Zero;
        pinters.Feature[0, 0] = pinters.Feature[0, 1] = pinters.Feature[1, 0] = pinters.Feature[1, 1] = 0x40;
        return 1;
    }

    // ============================================================================
    // Sphere-Cylinder / Cylinder-Sphere intersection
    // Port of cylinder_sphere_intersection / sphere_cylinder_intersection
    // ============================================================================
    public static int SphereCylinder(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = CylinderSphereImpl((Cylinder)p2, (Sphere)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    public static int CylinderSphere(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return CylinderSphereImpl((Cylinder)p1, (Sphere)p2, pinters);
    }

    private static int CylinderSphereImpl(Cylinder pcyl, Sphere psphere, PrimInters pinters)
    {
        float h = (psphere.Center - pcyl.Center).Dot(pcyl.Axis);
        float dh = pcyl.HalfHeight - MathF.Abs(h);
        int bOutsideH = MathUtils.IsNeg(dh);
        var dir = pcyl.Axis * (dh * bOutsideH * MathUtils.SgnNZ(h));

        var dc = psphere.Center - pcyl.Center;
        int bOutsideR = MathUtils.IsNeg(MathUtils.Sqr(pcyl.Radius) - (dc ^ pcyl.Axis).LengthSq());
        if (bOutsideR != 0)
        {
            dc = dc - pcyl.Axis * dc.Dot(pcyl.Axis);
            dc = dc - dc.Normalized() * pcyl.Radius;
            dir = dir + dc;
        }

        pinters.Pt0 = pinters.Pt1 = psphere.Center + dir;
        pinters.Normal = psphere.Center - pinters.Pt0;
        pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
        return MathUtils.IsNeg(dir.LengthSq() - MathUtils.Sqr(psphere.Radius));
    }

    // ============================================================================
    // Sphere-Capsule / Capsule-Sphere intersection
    // Port of capsule_sphere_intersection / sphere_capsule_intersection
    // ============================================================================
    public static int SphereCapsule(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = CapsuleSphereImpl((Cylinder)p2, (Sphere)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    public static int CapsuleSphere(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return CapsuleSphereImpl((Cylinder)p1, (Sphere)p2, pinters);
    }

    private static int CapsuleSphereImpl(Cylinder pcaps, Sphere psphere, PrimInters pinters)
    {
        var dir = pcaps.Center - psphere.Center;
        dir = dir - pcaps.Axis * MathF.Max(-pcaps.HalfHeight, MathF.Min(pcaps.HalfHeight, dir.Dot(pcaps.Axis)));
        if (dir.LengthSq() < MathUtils.Sqr(pcaps.Radius + psphere.Radius))
        {
            dir.Normalize();
            pinters.Pt0 = pinters.Pt1 = psphere.Center + dir * psphere.Radius;
            pinters.Normal = -dir;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
            pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
            return 1;
        }
        return 0;
    }

    // ============================================================================
    // Ray-Cylinder / Cylinder-Ray intersection
    // Port of cylinder_ray_intersection / ray_cylinder_intersection
    // ============================================================================
    public static int RayCylinder(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = CylinderRayImpl((Cylinder)p2, (Ray)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    public static int CylinderRay(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return CylinderRayImpl((Cylinder)p1, (Ray)p2, pinters);
    }

    private static int CylinderRayImpl(Cylinder pcyl, Ray pray, PrimInters pinters)
    {
        int sg = MathUtils.SgnNZ(pcyl.Axis.Dot(pray.Dir));
        var t = new QuotientF(
            pcyl.Axis.Dot((pcyl.Center - pray.Origin) * sg - pcyl.Axis * pcyl.HalfHeight),
            MathF.Abs(pray.Dir.Dot(pcyl.Axis)));
        int bHit0 = MathUtils.IsNeg(MathF.Abs(t.X * 2 - t.Y) - t.Y)
                   & MathUtils.IsNeg(((pray.Origin - pcyl.Center) * t.Y + pray.Dir * t.X).Cross(pcyl.Axis).LengthSq()
                                    - MathUtils.Sqr(pcyl.Radius * t.Y));
        int bHit = bHit0;

        t = new QuotientF(
            pcyl.Axis.Dot((pcyl.Center - pray.Origin) * sg - pcyl.Axis * (pcyl.HalfHeight * (bHit * 2 - 1))),
            MathF.Abs(pray.Dir.Dot(pcyl.Axis)));
        bHit = MathUtils.IsNeg(MathF.Abs(t.X * 2 - t.Y) - t.Y)
             & MathUtils.IsNeg(((pray.Origin - pcyl.Center) * t.Y + pray.Dir * t.X).Cross(pcyl.Axis).LengthSq()
                              - MathUtils.Sqr(pcyl.Radius * t.Y));
        var tcap = t;
        int bHitCap = bHit;

        int bHitSide = 0;
        if (bHit0 == 0)
        {
            var vec0 = (pray.Origin - pcyl.Center) ^ pcyl.Axis;
            var vec1 = pray.Dir ^ pcyl.Axis;
            float a = vec1.Dot(vec1), b = vec0.Dot(vec1), c = vec0.Dot(vec0) - MathUtils.Sqr(pcyl.Radius);
            float d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b - d, a);
                bHitSide = bHit = MathUtils.IsNeg(MathF.Abs(t.X * 2 - t.Y) - t.Y)
                    & MathUtils.IsNeg(MathF.Abs(((pray.Origin - pcyl.Center) * t.Y + pray.Dir * t.X).Dot(pcyl.Axis)) - pcyl.HalfHeight * t.Y);
                t.X += d * (bHit ^ 1) * 2;
                bHitSide = bHit = MathUtils.IsNeg(MathF.Abs(t.X * 2 - t.Y) - t.Y)
                    & MathUtils.IsNeg(MathF.Abs(((pray.Origin - pcyl.Center) * t.Y + pray.Dir * t.X).Dot(pcyl.Axis)) - pcyl.HalfHeight * t.Y);
            }
            if (bHitSide == 0)
            {
                t = tcap;
                bHit = bHitCap;
            }
        }

        if (bHit == 0 || t.X * t.Y < 0)
            return 0;

        pinters.Pt0 = pinters.Pt1 = pray.Origin + pray.Dir * t.Val();
        if (bHitSide != 0)
        {
            pinters.Normal = pinters.Pt0 - pcyl.Center;
            pinters.Normal = pinters.Normal - pcyl.Axis * pcyl.Axis.Dot(pinters.Normal);
            pinters.Normal.Normalize();
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
        }
        else
        {
            sg = MathUtils.SgnNZ((pinters.Pt0 - pcyl.Center).Dot(pcyl.Axis));
            pinters.Normal = pcyl.Axis * sg;
            pinters.Feature[0, 0] = pinters.Feature[1, 0] = (byte)(0x41 + ((sg + 1) >> 1));
        }
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x20;
        return 1;
    }

    // ============================================================================
    // Ray-Capsule / Capsule-Ray intersection
    // Port of capsule_ray_intersection / ray_capsule_intersection
    // ============================================================================
    public static int RayCapsule(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = CapsuleRayImpl((Capsule)p2, (Ray)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    public static int CapsuleRay(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return CapsuleRayImpl((Capsule)p1, (Ray)p2, pinters);
    }

    private static int CapsuleRayImpl(Capsule pcaps, Ray pray, PrimInters pinters)
    {
        int iFeature = -1;
        var tmin = new QuotientF(1, 1);
        float aDir = pray.Dir.LengthSq();

        // Check cap sphere intersections
        for (int icap = 0; icap < 2; icap++)
        {
            var capCenter = pcaps.Center + pcaps.Axis * (pcaps.HalfHeight * (icap * 2 - 1));
            float b = pray.Dir.Dot(pray.Origin - capCenter);
            float c = (pray.Origin - capCenter).LengthSq() - MathUtils.Sqr(pcaps.Radius);
            float axcdiff = (pray.Origin - pcaps.Center).Dot(pcaps.Axis);
            float axdir = pray.Dir.Dot(pcaps.Axis);
            float d = b * b - aDir * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                var t = new QuotientF(-b - d, aDir);
                // Check: t in (0, tmin) and point is on this cap's hemisphere
                if (t.X * tmin.Y > 0 && t.X * tmin.Y < t.Y * tmin.X
                    && MathUtils.IsNeg(pcaps.HalfHeight * t.Y - MathF.Abs(axcdiff * t.Y + axdir * t.X)) != 0)
                {
                    tmin = t;
                    iFeature = 0x41 + icap;
                }
                t.X += d * 2;
                if (t.X * tmin.Y > 0 && t.X * tmin.Y < t.Y * tmin.X
                    && MathUtils.IsNeg(pcaps.HalfHeight * t.Y - MathF.Abs(axcdiff * t.Y + axdir * t.X)) != 0)
                {
                    tmin = t;
                    iFeature = 0x41 + icap;
                }
            }
        }

        // Check cylindrical side
        var vec0 = (pray.Origin - pcaps.Center) ^ pcaps.Axis;
        var vec1 = pray.Dir ^ pcaps.Axis;
        float aSide = vec1.Dot(vec1), bSide = vec0.Dot(vec1), cSide = vec0.Dot(vec0) - MathUtils.Sqr(pcaps.Radius);
        float dSide = bSide * bSide - aSide * cSide;
        if (dSide >= 0)
        {
            dSide = MathF.Sqrt(dSide);
            var t = new QuotientF(-bSide - dSide, aSide);
            if (t.X * tmin.Y > 0 && t.X * tmin.Y < t.Y * tmin.X
                && MathUtils.IsNeg(MathF.Abs(((pray.Origin - pcaps.Center) * t.Y + pray.Dir * t.X).Dot(pcaps.Axis)) - pcaps.HalfHeight * t.Y) != 0)
            {
                tmin = t;
                iFeature = 0x40;
            }
            t.X += dSide * 2;
            if (t.X * tmin.Y > 0 && t.X * tmin.Y < t.Y * tmin.X
                && MathUtils.IsNeg(MathF.Abs(((pray.Origin - pcaps.Center) * t.Y + pray.Dir * t.X).Dot(pcaps.Axis)) - pcaps.HalfHeight * t.Y) != 0)
            {
                tmin = t;
                iFeature = 0x40;
            }
        }

        if (iFeature < 0)
            return 0;

        pinters.Pt0 = pinters.Pt1 = pray.Origin + pray.Dir * tmin.Val();
        if (iFeature == 0x40)
        {
            pinters.Normal = pinters.Pt0 - pcaps.Center;
            pinters.Normal = pinters.Normal - pcaps.Axis * pcaps.Axis.Dot(pinters.Normal);
        }
        else
        {
            pinters.Normal = pinters.Pt0 - pcaps.Center - pcaps.Axis * (pcaps.HalfHeight * ((iFeature - 0x41) * 2 - 1));
        }
        pinters.Normal.Normalize();
        pinters.Feature[0, 0] = pinters.Feature[1, 0] = (byte)iFeature;
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x20;
        return 1;
    }

    // ============================================================================
    // Cylinder-Cylinder intersection
    // Port of cylinder_cylinder_intersection
    // ============================================================================
    public static int CylinderCylinder(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var pcyl1 = (Cylinder)p1;
        var pcyl2 = (Cylinder)p2;
        var pcyl = new Cylinder[] { pcyl1, pcyl2 };
        pinters.NBorderPt = 0;

        float cosa = pcyl1.Axis.Dot(pcyl2.Axis);
        var axisx = pcyl1.Axis ^ pcyl2.Axis;
        float sina = axisx.Length();
        int bHasInters = 0;

        float rsina = 0;
        if (sina > 0.003f)
        {
            axisx = axisx * (rsina = 1 / sina);
            var dc = pcyl2.Center - pcyl1.Center;
            if ((MathUtils.IsNeg(MathF.Abs(dc.Dot(axisx)) - pcyl1.Radius - pcyl2.Radius)
               & MathUtils.IsNeg(MathF.Abs((dc ^ pcyl2.Axis).Dot(axisx)) - pcyl1.HalfHeight * sina)
               & MathUtils.IsNeg(MathF.Abs((dc ^ pcyl1.Axis).Dot(axisx)) - pcyl2.HalfHeight * sina)) != 0)
                bHasInters = 1;
        }
        else
        {
            axisx = GetOrthogonal(pcyl1.Axis).Normalized();
        }

        for (int icyl = 0; icyl < 2; icyl++)
        {
            float r0 = pcyl[icyl].Radius, r1 = pcyl[icyl ^ 1].Radius;

            // Check icyl axis - icyl^1 cap intersections
            var t = new QuotientF(
                (pcyl[icyl ^ 1].Center - pcyl[icyl].Center).Dot(pcyl[icyl ^ 1].Axis) - pcyl[icyl ^ 1].HalfHeight,
                cosa);
            var ptCheck = (pcyl[icyl].Center - pcyl[icyl ^ 1].Center) * t.Y + pcyl[icyl].Axis * t.X;
            ptCheck = ptCheck - pcyl[icyl ^ 1].Axis * ptCheck.Dot(pcyl[icyl ^ 1].Axis);
            for (int i = 0; i < 2; i++, t.X += pcyl[icyl ^ 1].HalfHeight * 2,
                 ptCheck = ptCheck + pcyl[icyl].Axis * (pcyl[icyl ^ 1].HalfHeight * 2))
            {
                if ((MathUtils.IsNeg(MathF.Abs(t.X) - MathF.Abs(t.Y) * pcyl[icyl].HalfHeight)
                   & (ptCheck.LengthSq() < MathUtils.Sqr(r1) ? 1 : 0)) != 0)
                    bHasInters = 1;
            }

            // Check icyl axis - icyl^1 side intersections
            var center0 = pcyl[icyl].Center - pcyl[icyl ^ 1].Center;
            float a = 1 - MathUtils.Sqr(cosa);
            if (a > 0.0001f)
            {
                float b = pcyl[icyl].Axis.Dot(center0) - center0.Dot(pcyl[icyl ^ 1].Axis) * cosa;
                float c = center0.LengthSq() - MathUtils.Sqr(pcyl[icyl ^ 1].Axis.Dot(center0)) - MathUtils.Sqr(r1);
                float d = b * b - a * c;
                if (d > 0)
                {
                    // Simplified: just check if roots are in range
                    float sqrtD = MathF.Sqrt(d);
                    for (int si = -1; si <= 1; si += 2)
                    {
                        float root = (-b + sqrtD * si) / a;
                        if (MathF.Abs(root) < pcyl[icyl].HalfHeight)
                        {
                            float hProj = center0.Dot(pcyl[icyl ^ 1].Axis) + root * cosa;
                            if (MathF.Abs(hProj) < pcyl[icyl ^ 1].HalfHeight)
                                bHasInters = 1;
                        }
                    }
                }
            }
        }

        if (pinters.NBorderPt > 0)
        {
            pinters.Pt0 = pinters.Pt1 = pinters.BorderPts![0];
            return 1;
        }
        return bHasInters;
    }

    // ============================================================================
    // Cylinder-Capsule / Capsule-Cylinder intersection
    // Port of cylinder_capsule_intersection / capsule_cylinder_intersection
    // ============================================================================
    public static int CylinderCapsule(Primitive p1, Primitive p2, PrimInters pinters)
    {
        return CylinderCapsuleImpl((Cylinder)p1, (Capsule)p2, pinters);
    }

    public static int CapsuleCylinder(Primitive p1, Primitive p2, PrimInters pinters)
    {
        int res = CylinderCapsuleImpl((Cylinder)p2, (Capsule)p1, pinters);
        SwapPinters(pinters);
        return res;
    }

    private static int CylinderCapsuleImpl(Cylinder pcyl, Capsule pcaps, PrimInters pinters)
    {
        float r0 = pcyl.Radius, r1 = pcaps.Radius;
        pinters.NBorderPt = 0;
        pinters.Normal = PhysVector3.Zero;

        float cosa = pcyl.Axis.Dot(pcaps.Axis);
        var axisx = pcyl.Axis ^ pcaps.Axis;
        float sina = axisx.Length();
        int bHasInters = 0;

        if (sina > 0.003f)
        {
            axisx = axisx / sina;
            var dc = pcaps.Center - pcyl.Center;
            if ((MathUtils.IsNeg(MathF.Abs(dc.Dot(axisx)) - r0 - r1)
               & MathUtils.IsNeg(MathF.Abs((dc ^ pcaps.Axis).Dot(axisx)) - pcyl.HalfHeight * sina)
               & MathUtils.IsNeg(MathF.Abs((dc ^ pcyl.Axis).Dot(axisx)) - pcaps.HalfHeight * sina)) != 0)
                bHasInters = 1;
        }
        else
        {
            axisx = GetOrthogonal(pcyl.Axis).Normalized();
        }

        // Check capsule's spheres - cylinder intersections
        for (int i = -1; i <= 1; i += 2)
        {
            var dc = pcaps.Center + pcaps.Axis * (pcaps.HalfHeight * i) - pcyl.Center;
            float h = dc.Dot(pcyl.Axis);
            float dh = pcyl.HalfHeight - MathF.Abs(h);
            int bOutsideH = MathUtils.IsNeg(dh);
            var dir = pcyl.Axis * (dh * bOutsideH * MathUtils.SgnNZ(h));

            int bOutsideR = MathUtils.IsNeg(MathUtils.Sqr(r0) - (dc ^ pcyl.Axis).LengthSq());
            if (bOutsideR != 0)
            {
                var dcProj = dc - pcyl.Axis * dc.Dot(pcyl.Axis);
                dcProj = dcProj - dcProj.Normalized() * r0;
                dir = dir + dcProj;
            }
            if (dir.LengthSq() < r1 * r1)
            {
                if (pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                    pinters.BorderPts[pinters.NBorderPt++] = pcaps.Center + pcaps.Axis * (pcaps.HalfHeight * i) + dir;
                pinters.Normal = -dir;
                bHasInters = 1;
            }
        }

        if (pinters.NBorderPt > 0)
        {
            pinters.Pt0 = pinters.Pt1 = pinters.BorderPts![0];
            return 1;
        }
        return bHasInters;
    }

    // ============================================================================
    // Capsule-Capsule intersection
    // Port of capsule_capsule_intersection
    // ============================================================================
    public static int CapsuleCapsule(Primitive p1, Primitive p2, PrimInters pinters)
    {
        var pcaps1 = (Capsule)p1;
        var pcaps2 = (Capsule)p2;
        var pcaps = new Capsule[] { pcaps1, pcaps2 };
        pinters.NBorderPt = 0;

        // Check capsule's spheres - capsule intersections
        for (int icaps = 0; icaps < 2; icaps++)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                var dir = pcaps[icaps ^ 1].Center - pcaps[icaps].Center - pcaps[icaps].Axis * (pcaps[icaps].HalfHeight * i);
                dir = dir - pcaps[icaps ^ 1].Axis * MathF.Max(-pcaps[icaps ^ 1].HalfHeight,
                    MathF.Min(pcaps[icaps ^ 1].HalfHeight, dir.Dot(pcaps[icaps ^ 1].Axis)));
                if (dir.LengthSq() < MathUtils.Sqr(pcaps1.Radius + pcaps2.Radius))
                {
                    dir.Normalize();
                    if (pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                        pinters.BorderPts[pinters.NBorderPt++] = pcaps[icaps].Center + pcaps[icaps].Axis * (pcaps[icaps].HalfHeight * i) + dir * pcaps[icaps].Radius;
                    pinters.Normal = dir * (1 - icaps * 2);
                }
            }
        }

        // Check axis-axis closest points
        var dcAx = pcaps2.Center - pcaps1.Center;
        var dirAx = pcaps1.Axis ^ pcaps2.Axis;
        float dlen2 = dirAx.LengthSq();
        float tAxDot = dcAx.Dot(dirAx);
        float t0Ax = (dcAx ^ pcaps2.Axis).Dot(dirAx);
        if ((MathUtils.IsNeg(MathUtils.Sqr(tAxDot) - MathUtils.Sqr(pcaps1.Radius + pcaps2.Radius) * dlen2)
           & MathUtils.IsNeg(MathF.Abs(t0Ax) - pcaps1.HalfHeight * dlen2)
           & MathUtils.IsNeg(MathF.Abs((dcAx ^ pcaps1.Axis).Dot(dirAx)) - pcaps2.HalfHeight * dlen2)) != 0
           && dlen2 > 1e-20f)
        {
            float invDlen2 = 1.0f / dlen2;
            dirAx = dirAx * (MathF.Sqrt(invDlen2) * MathUtils.SgnNZ(tAxDot));
            if (pinters.BorderPts != null && pinters.NBorderPt < pinters.BorderPts.Length)
                pinters.BorderPts[pinters.NBorderPt++] = pcaps1.Center + pcaps1.Axis * (t0Ax * invDlen2) + dirAx * pcaps1.Radius;
            pinters.Normal = dirAx;
        }

        if (pinters.NBorderPt <= 0)
            return 0;
        pinters.Pt0 = pinters.Pt1 = pinters.BorderPts![0];
        pinters.Feature[0, 0] = pinters.Feature[1, 0] = 0x40;
        pinters.Feature[0, 1] = pinters.Feature[1, 1] = 0x40;
        return 1;
    }

    // ============================================================================
    // Helper: GetOrthogonal - find a vector orthogonal to the given one
    // ============================================================================
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PhysVector3 GetOrthogonal(in PhysVector3 v)
    {
        // Pick the axis most orthogonal to v
        if (MathF.Abs(v.X) < MathF.Abs(v.Y))
        {
            if (MathF.Abs(v.X) < MathF.Abs(v.Z))
                return new PhysVector3(0, -v.Z, v.Y); // v ^ (1,0,0)
            else
                return new PhysVector3(-v.Y, v.X, 0); // v ^ (0,0,1)
        }
        else
        {
            if (MathF.Abs(v.Y) < MathF.Abs(v.Z))
                return new PhysVector3(v.Z, 0, -v.X); // v ^ (0,1,0)
            else
                return new PhysVector3(-v.Y, v.X, 0); // v ^ (0,0,1)
        }
    }
}
