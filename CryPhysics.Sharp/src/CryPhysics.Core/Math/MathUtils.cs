// Port of CryPhysics utils.h - branchless math utilities
// Original: Copyright Crytek GMBH, used under license

using System.Runtime.CompilerServices;

namespace CryPhysics.Math;

/// <summary>
/// Branchless math utilities ported from CryEngine's physics utils.h.
/// All comparisons use bit manipulation for branch-free performance.
/// </summary>
public static class MathUtils
{
    public const float PI = MathF.PI;
    public const float Sqrt3 = 1.7320508075688772935f;
    public const float Epsilon = 1e-10f;

    // inc_mod3[i] = (i+1)%3, dec_mod3[i] = (i+2)%3
    public static readonly int[] IncMod3 = { 1, 2, 0 };
    public static readonly int[] DecMod3 = { 2, 0, 1 };

    /// <summary>Returns 1 if x &lt; 0, else 0 (branchless).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsNeg(float x)
    {
        return (int)((uint)BitConverter.SingleToInt32Bits(x) >> 31);
    }

    /// <summary>Returns 1 if x &lt; 0, else 0 (branchless, int version).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsNeg(int x)
    {
        return (int)((uint)x >> 31);
    }

    /// <summary>Returns 1 if x &gt;= 0, else 0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsNonNeg(float x)
    {
        return IsNeg(x) ^ 1;
    }

    /// <summary>Returns 1 if x &gt;= 0, else 0 (int version).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsNonNeg(int x)
    {
        return IsNeg(x) ^ 1;
    }

    /// <summary>Returns 1 if x == 0, else 0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IsZero(int x)
    {
        return ((x | -x) >> 31) + 1;
    }

    /// <summary>Returns 1 if x != 0, else 0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NotZero(int x)
    {
        return (int)(((uint)x >> 31) | ((uint)(-x) >> 31));
    }

    /// <summary>Returns sign of x: -1, 0, or 1. Correctly returns 0 for +/-0.0f.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sgn(float x)
    {
        // Port of C++ sgn(): (u.i>>31)+((u.i-1)>>31)+1
        int i = BitConverter.SingleToInt32Bits(x);
        return (i >> 31) + ((i - 1) >> 31) + 1;
    }

    /// <summary>Returns sign of x, never zero: -1 if x &lt; 0, else 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SgnNZ(float x)
    {
        return 1 - (IsNeg(x) << 1);
    }

    /// <summary>Returns sign of integer x, never zero: -1 or 1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SgnNZ(int x)
    {
        return 1 - (IsNeg(x) << 1);
    }

    /// <summary>Branchless max for integers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MaxFast(int op1, int op2)
    {
        return op1 - ((op1 - op2) & ((op1 - op2) >> 31));
    }

    /// <summary>Branchless min for integers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MinFast(int op1, int op2)
    {
        return op2 + ((op1 - op2) & ((op1 - op2) >> 31));
    }

    /// <summary>Branchless min for floats (uses abs trick).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Min(float op1, float op2)
    {
        return (op1 + op2 - MathF.Abs(op1 - op2)) * 0.5f;
    }

    /// <summary>Branchless max for floats.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Max(float op1, float op2)
    {
        return (op1 + op2 + MathF.Abs(op1 - op2)) * 0.5f;
    }

    /// <summary>Branchless min for doubles.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Min(double op1, double op2)
    {
        return (op1 + op2 - System.Math.Abs(op1 - op2)) * 0.5;
    }

    /// <summary>Branchless max for doubles.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Max(double op1, double op2)
    {
        return (op1 + op2 + System.Math.Abs(op1 - op2)) * 0.5;
    }

    /// <summary>Returns min if bMax==0, max if bMax==1 (branchless).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MinMax(float op1, float op2, int bMax)
    {
        return (op1 + op2 + MathF.Abs(op1 - op2) * (bMax * 2 - 1)) * 0.5f;
    }

    /// <summary>Safe min (standard comparison).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MinSafe(float op1, float op2) => op1 < op2 ? op1 : op2;

    /// <summary>Safe max (standard comparison).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MaxSafe(float op1, float op2) => op1 > op2 ? op1 : op2;

    /// <summary>Returns true if val is in [lo, hi] range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InRange(float val, float lo, float hi)
    {
        return val >= lo && val <= hi;
    }

    /// <summary>Branchless negmask: returns all 1s if x &lt; 0, all 0s otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NegMask(int x)
    {
        return x >> 31;
    }

    /// <summary>Branchless clamp with masks.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ApplyMin(int x, int lower, out int mask)
    {
        mask = (x - lower) >> 31;
        return (x & ~mask) | (lower & mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ApplyMax(int x, int upper, out int mask)
    {
        mask = (upper - x) >> 31;
        return (x & ~mask) | (upper & mask);
    }

    /// <summary>Approximate cube root using bit manipulation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubertApprox(float x)
    {
        int ix = BitConverter.SingleToInt32Bits(x);
        ix = 0x2a51067f + ix / 3;
        return BitConverter.Int32BitsToSingle(ix);
    }

    /// <summary>Precise cube root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cubert(float x)
    {
        return MathF.Abs(x) > 1e-20f
            ? MathF.Exp(MathF.Log(MathF.Abs(x)) * (1.0f / 3.0f)) * SgnNZ(x)
            : x;
    }

    /// <summary>Precise cube root for doubles.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cubert(double x)
    {
        return System.Math.Abs(x) > 1e-20
            ? System.Math.Exp(System.Math.Log(System.Math.Abs(x)) * (1.0 / 3.0)) * SgnNZ((float)x)
            : x;
    }

    /// <summary>Float to int with rounding (portable version).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Float2Int(float x)
    {
        return (int)(x < 0f ? x - 0.5f : x + 0.5f);
    }

    /// <summary>Returns index of max among 3 elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IdxMax3(float[] a)
    {
        int idx = 0;
        if (a[1] > a[idx]) idx = 1;
        if (a[2] > a[idx]) idx = 2;
        return idx;
    }

    /// <summary>Returns index of max among 3 elements (span version).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IdxMax3(Span<float> a)
    {
        int idx = 0;
        if (a[1] > a[idx]) idx = 1;
        if (a[2] > a[idx]) idx = 2;
        return idx;
    }

    /// <summary>Cube of a value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cube(float x) => x * x * x;

    /// <summary>Square of a value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sqr(float x) => x * x;

    /// <summary>Square of a value (double).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqr(double x) => x * x;

    /// <summary>
    /// Checks if a 2D line segment crosses the edge of a 2D box.
    /// Returns false if the segment is completely inside or completely outside.
    /// Port of box_segment_intersect from utils.h.
    /// </summary>
    public static bool BoxSegmentIntersect(
        System.Numerics.Vector2 boxMin, System.Numerics.Vector2 boxMax,
        System.Numerics.Vector2 p0, System.Numerics.Vector2 p1)
    {
        var d = p1 - p0;

        // Cross X sides
        if (d.X != 0f)
        {
            for (int i = 0; i < 2; i++)
            {
                float bx = i == 0 ? boxMin.X : boxMax.X;
                float t = (bx - p0.X) / d.X;
                if (t > 0f && t < 1f)
                {
                    float y = p0.Y + d.Y * t;
                    if (y > boxMin.Y && y < boxMax.Y)
                        return true;
                }
            }
        }

        // Cross Y sides
        if (d.Y != 0f)
        {
            for (int i = 0; i < 2; i++)
            {
                float by = i == 0 ? boxMin.Y : boxMax.Y;
                float t = (by - p0.Y) / d.Y;
                if (t > 0f && t < 1f)
                {
                    float x = p0.X + d.X * t;
                    if (x > boxMin.X && x < boxMax.X)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Offset inertia tensor by center-of-mass displacement (parallel axis theorem).
    /// Port of OffsetInertiaTensor from utils.h.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OffsetInertiaTensor(ref PhysMatrix33 I, in PhysVector3 center, float M)
    {
        I.M00 += M * (center.Y * center.Y + center.Z * center.Z);
        I.M11 += M * (center.X * center.X + center.Z * center.Z);
        I.M22 += M * (center.X * center.X + center.Y * center.Y);
        I.M10 = I.M01 = I.M01 - M * center.X * center.Y;
        I.M20 = I.M02 = I.M02 - M * center.X * center.Z;
        I.M21 = I.M12 = I.M12 - M * center.Y * center.Z;
    }

    /// <summary>
    /// Compute the volume of a pyramid (tetrahedron) from 4 vertices.
    /// Port of CalcPyramidVolume from trimesh.cpp line 2890.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CalcPyramidVolume(in PhysVector3 pt0, in PhysVector3 pt1,
        in PhysVector3 pt2, in PhysVector3 pt3, out PhysVector3 com)
    {
        com = (pt0 + pt1 + pt2 + pt3) * 0.25f;
        return MathF.Abs(((pt1 - pt0) ^ (pt2 - pt0)).Dot(pt3 - pt0)) * (1.0f / 6f);
    }

    /// <summary>
    /// Crop a convex polygon with a half-plane defined by n*pt &lt; d.
    /// Port of crop_polygon_with_plane from utils.cpp line 818.
    /// Returns the number of output vertices.
    /// </summary>
    public static int CropPolygonWithPlane(ReadOnlySpan<PhysVector3> ptsrc, int nsrc,
        Span<PhysVector3> ptdst, in PhysVector3 n, float d)
    {
        // Find first vertex that is below the plane (n*pt < d)
        int i0;
        for (i0 = 0; i0 < nsrc && ptsrc[i0].Dot(n) >= d; i0++) ;
        if (i0 == nsrc)
            return 0;

        int ndst = 0;
        for (int iCount = 0; iCount < nsrc; iCount++)
        {
            int i1 = (i0 + 1) < nsrc ? (i0 + 1) : 0;
            float d0 = ptsrc[i0].Dot(n) - d;
            float d1 = ptsrc[i1].Dot(n) - d;
            if (d0 < 0)
                ptdst[ndst++] = ptsrc[i0];
            if (d0 * d1 < 0)
            {
                float t = d0 / (d0 - d1);
                ptdst[ndst++] = ptsrc[i0] * (1.0f - t) + ptsrc[i1] * t;
            }
            i0 = i1;
        }
        return ndst;
    }

    /// <summary>
    /// Calculate medium (water/air) resistance force and torque for a polygon face.
    /// Port of CalcMediumResistance from utils.cpp line 835.
    /// Accumulates impulse P and angular impulse L.
    /// </summary>
    public static void CalcMediumResistance(ReadOnlySpan<PhysVector3> ptsrc, int npt,
        in PhysVector3 n, in PhysVector3 planeNormal, float planeD,
        in PhysVector3 vworld, in PhysVector3 wworld, in PhysVector3 com,
        ref PhysVector3 P, ref PhysVector3 L)
    {
        // Allocate working buffers on stack
        Span<PhysVector3> pt0 = stackalloc PhysVector3[16];
        Span<PhysVector3> pt = stackalloc PhysVector3[17]; // +1 for wraparound

        // Crop with water plane: keep submerged part
        npt = CropPolygonWithPlane(ptsrc, npt, pt0, planeNormal, planeD);

        // Shift to COM-relative coordinates
        for (int i = 0; i < npt; i++)
            pt0[i] = pt0[i] - com;

        // Crop with velocity-normal plane: keep the side facing the flow
        var vnPlane = wworld ^ n;
        float vnD = vworld.Dot(n);
        npt = CropPolygonWithPlane(pt0.Slice(0, npt), npt, pt, vnPlane, vnD);

        // Build rotation to align n with Z axis
        var rotax = n ^ PhysVector3.UnitZ;
        float sina = rotax.Length();
        if (sina > 0.001f)
            rotax = rotax * (1f / sina);
        else
            rotax = PhysVector3.UnitX;
        float cosa = n.Z; // n dot (0,0,1)

        // Rotate velocity, angular velocity, and polygon into XY-aligned frame
        var v = vworld.GetRotated(rotax, cosa, sina);
        var w = wworld.GetRotated(rotax, cosa, sina);
        for (int i = 0; i < npt; i++)
            pt[i] = pt[i].GetRotated(rotax, cosa, sina);
        if (npt > 0)
            pt[npt] = pt[0]; // wraparound for edge iteration

        // Integrate force/torque contributions using area integrals
        var dP = PhysVector3.Zero;
        var dL = PhysVector3.Zero;
        float square = 0;

        for (int i = 0; i < npt; i++)
        {
            float x0 = pt[i].X, y0 = pt[i].Y;
            float dx = pt[i + 1].X - x0, dy = pt[i + 1].Y - y0;

            square += x0 * pt[i + 1].Y - pt[i + 1].X * y0;

            float Fxy = x0 * y0 + (dx * y0 + dy * x0) * 0.5f + dx * dy * (1.0f / 3f);
            float Fxx = x0 * x0 + dx * x0 + dx * dx * (1.0f / 3f);
            dP.Z += dy * (w.X * Fxy - w.Y * 0.5f * Fxx);
            dL.X += v.Z * dy * Fxy;
            dL.Y -= v.Z * dy * 0.5f * Fxx;

            float Fxxy = dx * dx * dy * 0.25f + (dx * dx * y0 + dx * dy * x0 * 2f) * (1.0f / 3f)
                       + (x0 * y0 * dx * 2f + x0 * x0 * dy) * 0.5f + x0 * x0 * y0;
            float Fxyy = dy * dy * dx * 0.25f + (dy * dy * x0 + dy * dx * y0 * 2f) * (1.0f / 3f)
                       + (y0 * x0 * dy * 2f + y0 * y0 * dx) * 0.5f + y0 * y0 * x0;
            float Fxxx = dx * dx * dx * 0.25f + dx * dx * x0 + dx * x0 * x0 * 1.5f + x0 * x0 * x0;
            dL.X += dy * (w.X * Fxyy - w.Y * 0.5f * Fxxy);
            dL.Y -= dy * (w.X * 0.5f * Fxxy - w.Y * (1.0f / 3f) * Fxxx);
        }
        dP.Z += v.Z * square * 0.5f;

        // Rotate back to world space and accumulate
        P = P - dP.GetRotated(rotax, cosa, -sina);
        L = L - dL.GetRotated(rotax, cosa, -sina);
    }

    /// <summary>Cross product with an orthonormal basis vector. Port of cross_with_ort.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PhysVector3 CrossWithOrt(in PhysVector3 vec, int iz)
    {
        int ix = IncMod3[iz], iy = DecMod3[iz];
        var res = PhysVector3.Zero;
        res[ix] = vec[iy];
        res[iy] = -vec[ix];
        return res;
    }

    /// <summary>
    /// Branchless approximate atan2 returning a Quotient instead of a float.
    /// Port of fake_atan2 from quotient.h. Result ranges from 0 to 8 (representing 0 to 2*pi).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QuotientF FakeAtan2(float y, float x)
    {
        float[] src = { x, y };
        int ix = IsNeg(x), iy = IsNeg(y);
        int iflip = IsNeg(MathF.Abs(x) - MathF.Abs(y));
        float num = src[iflip ^ 1] * (1 - iflip * 2) * SgnNZ(src[iflip]);
        float den = MathF.Abs(src[iflip]);
        // Offset: (iy*2 + (ix^iy) + (iflip^ix^iy)) * 2
        int offset = (iy * 2 + (ix ^ iy) + (iflip ^ ix ^ iy)) * 2;
        return new QuotientF(num + den * offset, den);
    }

    /// <summary>
    /// Compute mass properties (volume, center of mass, inertia tensor) of a closed triangle mesh
    /// using the divergence theorem. Port of ComputeMassProperties from utils.cpp.
    /// Returns the signed volume (6x the actual volume). Center and inertia are output parameters.
    /// The inertia tensor is about the center of mass.
    /// </summary>
    public static float ComputeMassProperties(
        ReadOnlySpan<PhysVector3> vertices, ReadOnlySpan<int> faces, int nFaces,
        out PhysVector3 center, out PhysMatrix33 inertia)
    {
        float M = 0;
        Span<float> fi = stackalloc float[12];
        Span<float> diag = stackalloc float[3];
        Span<PhysVector3> perm = stackalloc PhysVector3[4];
        Span<float> unperm = stackalloc float[12];
        diag.Clear();
        center = PhysVector3.Zero;
        inertia = PhysMatrix33.Zero;
        PhysVector3 p0, p1, p2;

        for (int f = nFaces - 1; f >= 0; f--)
        {
            int i0 = faces[f * 3], i1 = faces[f * 3 + 1], i2 = faces[f * 3 + 2];
            p0 = vertices[i0]; p1 = vertices[i1]; p2 = vertices[i2];

            var n = (p1 - p0).Cross(p2 - p0);
            if (n.LengthSq() < 1e-30f) continue;
            n = n.Normalized();

            // Find dominant axis g for projection
            float nmax = -1;
            int g = 0;
            for (int i = 0; i < 3; i++)
                if (n[i] * n[i] > nmax) { nmax = n[i] * n[i]; g = i; }

            // Permute vertices so that the dominant axis is z
            perm[0] = Permute(p0, g);
            perm[1] = Permute(p1, g);
            perm[2] = Permute(p2, g);
            perm[3] = perm[0];
            var np = Permute(n, g);

            ComputeFaceIntegrals(perm, np, fi);

            // Un-permute the integrals
            int gInv = g ^ (g >> 1) ^ 1;
            for (int i = 0; i < 4; i++)
            {
                var pr = new PhysVector3(fi[i * 3], fi[i * 3 + 1], fi[i * 3 + 2]);
                var p1u = Permute(pr, gInv);
                unperm[i * 3] = p1u.X; unperm[i * 3 + 1] = p1u.Y; unperm[i * 3 + 2] = p1u.Z;
            }
            var nu = Permute(np, gInv);

            M += nu.X * unperm[0];
            for (int i = 0; i < 3; i++)
            {
                center[i] += nu[i] * unperm[i + 3];
                diag[i] += nu[i] * unperm[i + 6];
            }
            inertia[0, 1] += nu.X * unperm[9];
            inertia[1, 2] += nu.Y * unperm[10];
            inertia[0, 2] += nu.Z * unperm[11];
        }

        if (M > 0)
            center /= M * 2f;

        inertia[0, 0] = (diag[1] + diag[2]) * (1.0f / 3f) - M * (center.Y * center.Y + center.Z * center.Z);
        inertia[1, 1] = (diag[0] + diag[2]) * (1.0f / 3f) - M * (center.X * center.X + center.Z * center.Z);
        inertia[2, 2] = (diag[0] + diag[1]) * (1.0f / 3f) - M * (center.X * center.X + center.Y * center.Y);
        inertia[1, 0] = inertia[0, 1] = -inertia[0, 1] * 0.5f + M * center.X * center.Y;
        inertia[2, 0] = inertia[0, 2] = -inertia[0, 2] * 0.5f + M * center.X * center.Z;
        inertia[2, 1] = inertia[1, 2] = -inertia[1, 2] * 0.5f + M * center.Y * center.Z;
        return M;
    }

    /// <summary>
    /// Compute projection integrals for a triangle face (helper for ComputeMassProperties).
    /// Port of compute_projection_integrals from utils.cpp.
    /// </summary>
    private static void ComputeProjectionIntegrals(ReadOnlySpan<PhysVector3> ab, Span<float> pi)
    {
        pi.Clear();
        Span<float> a0 = stackalloc float[4];
        Span<float> b0 = stackalloc float[4];
        Span<float> a1 = stackalloc float[3];
        Span<float> b1 = stackalloc float[3];
        float[,] C = new float[4, 3];

        for (int edge = 0; edge < 3; edge++)
        {
            var cur = ab[edge];
            var next = ab[edge + 1];

            a0[0] = cur.X; for (int i = 1; i < 4; i++) a0[i] = a0[i - 1] * cur.X;
            b0[0] = cur.Y; for (int i = 1; i < 4; i++) b0[i] = b0[i - 1] * cur.Y;
            a1[0] = next.X; for (int i = 1; i < 3; i++) a1[i] = a1[i - 1] * next.X;
            b1[0] = next.Y; for (int i = 1; i < 3; i++) b1[i] = b1[i - 1] * next.Y;

            C[0, 0] = a1[1] + a1[0] * a0[0] + a0[1];
            for (int i = 1; i < 3; i++) C[0, i] = a1[0] * C[0, i - 1] + a0[i + 1];
            C[1, 0] = b1[1] + b1[0] * b0[0] + b0[1];
            for (int i = 1; i < 3; i++) C[1, i] = b1[0] * C[1, i - 1] + b0[i + 1];

            float da = a1[0] - a0[0], db = b1[0] - b0[0];
            C[2, 0] = 3 * a1[1] + 2 * a1[0] * a0[0] + a0[1];
            C[2, 1] = a0[0] * C[2, 0] + 4 * a1[2];
            C[2, 2] = 4 * b1[2] + 3 * b1[1] * b0[0] + 2 * b1[0] * b0[1] + b0[2];
            C[3, 0] = 3 * a0[1] + 2 * a0[0] * a1[0] + a1[1];
            C[3, 1] = a1[0] * C[3, 0] + 4 * a0[2];
            C[3, 2] = 4 * b0[2] + 3 * b0[1] * b1[0] + 2 * b0[0] * b1[1] + b1[2];

            pi[0] += db * (a0[0] + a1[0]);
            for (int i = 0; i < 3; i++)
            {
                pi[i + 1] += db * C[0, i];
                pi[i + 4] += da * C[1, i];
            }
            pi[7] += db * (b1[0] * C[2, 0] + b0[0] * C[3, 0]);
            pi[8] += db * (b1[0] * C[2, 1] + b0[0] * C[3, 1]);
            pi[9] += da * (a1[0] * C[2, 2] + a0[0] * C[3, 2]);
        }

        pi[0] *= 0.5f;
        pi[1] *= 1.0f / 6f; pi[2] *= 1.0f / 12f; pi[3] *= 1.0f / 20f;
        pi[4] *= -1.0f / 6f; pi[5] *= -1.0f / 12f; pi[6] *= -1.0f / 20f;
        pi[7] *= 1.0f / 24f; pi[8] *= 1.0f / 60f; pi[9] *= -1.0f / 60f;
    }

    /// <summary>
    /// Compute face integrals for mass properties (helper for ComputeMassProperties).
    /// Port of compute_face_integrals from utils.cpp.
    /// </summary>
    private static void ComputeFaceIntegrals(ReadOnlySpan<PhysVector3> p, in PhysVector3 n, Span<float> fi)
    {
        Span<float> pi = stackalloc float[10];
        ComputeProjectionIntegrals(p, pi);

        float w = -(n.X * p[0].X + n.Y * p[0].Y + n.Z * p[0].Z);
        Span<float> k = stackalloc float[4];
        k[0] = 1.0f / n.Z;
        for (int i = 1; i < 4; i++) k[i] = k[i - 1] * k[0];

        // a, a2, a3
        for (int i = 0; i < 3; i++) fi[i * 3 + 0] = k[0] * pi[i + 1];
        // b, b2, b3
        for (int i = 0; i < 3; i++) fi[i * 3 + 1] = k[0] * pi[i + 4];

        // a2b, b2g, g2a
        fi[9] = k[0] * pi[8];
        fi[10] = -k[1] * (n.X * pi[9] + n.Y * pi[6] + w * pi[5]);
        fi[11] = k[2] * (n.X * n.X * pi[3] + n.Y * n.Y * pi[9] + w * w * pi[1]
                        + 2 * (n.X * n.Y * pi[8] + n.X * w * pi[2] + n.Y * w * pi[7]));

        // Apply n multipliers to pi for g, g2, g3 computation
        float t = n.X;
        for (int i = 0; i < 3; i++) { pi[i + 1] *= t; t *= n.X; }
        t = n.Y;
        for (int i = 0; i < 3; i++) { pi[i + 4] *= t; t *= n.Y; }
        for (int i = 0; i < 3; i++) pi[i + 7] *= n.X * n.Y;
        pi[8] *= n.X; pi[9] *= n.Y;

        // g, g2, g3
        fi[2] = -k[1] * (pi[1] + pi[4] + w * pi[0]);
        fi[5] = k[2] * (pi[2] + 2 * pi[7] + pi[5] + w * (2 * (pi[1] + pi[4]) + w * pi[0]));
        fi[8] = -k[3] * (pi[3] + 3 * (pi[8] + pi[9]) + pi[6]
                        + w * (3 * (pi[2] + pi[5] + 2 * pi[7] + w * (pi[1] + pi[4])) + w * w * pi[0]));
    }

    /// <summary>Permute vector components cyclically by g positions. Port of GetPermutated.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PhysVector3 Permute(in PhysVector3 v, int g)
    {
        return g switch
        {
            0 => v,
            1 => new PhysVector3(v.Y, v.Z, v.X),
            2 => new PhysVector3(v.Z, v.X, v.Y),
            _ => v
        };
    }
}

/// <summary>
/// Generator for orthonormal basis vectors. Port of ort_gen.
/// ort[0] = (1,0,0), ort[1] = (0,1,0), ort[2] = (0,0,1)
/// </summary>
public struct OrtGen
{
    public PhysVector3 this[int i] => new(
        MathUtils.IsZero(i),
        i & 1,
        i >> 1
    );
}
