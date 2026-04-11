// Port of CryPhysics rotunprojectionchecks.cpp - rotational unprojection functions
// Original: Copyright Crytek GMBH, used under license
//
// These functions compute the minimum rotation angle to separate two overlapping primitives.

using System;
using System.Runtime.CompilerServices;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Collision;

/// <summary>
/// Rotational unprojection functions ported from CryEngine's rotunprojectionchecks.cpp.
/// Each function computes the minimum rotation angle to separate two overlapping primitives
/// about a given rotation axis and center.
/// </summary>
public static class RotationalUnprojection
{
    // -----------------------------------------------------------------
    // Helper: UPDATE_IDBEST macro
    //   if bBest==1 => idbest=newid, tbest=tsin
    //   if bBest==0 => idbest unchanged, tbest unchanged
    // The C++ macro uses branchless bit tricks. We replicate the semantics.
    // -----------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateIdBest(ref int idbest, ref QuotientF tbest, int bBest, int newid, in QuotientF tsin)
    {
        int negBest = -bBest;           // bBest==1 => negBest==-1 (all bits set), bBest==0 => 0
        idbest = (idbest & ~negBest) | (newid & negBest);
        tbest.X = tbest.X * (bBest ^ 1) + tsin.X * bBest;
        tbest.Y = tbest.Y * (bBest ^ 1) + tsin.Y * bBest;
    }

    // -----------------------------------------------------------------
    // Helper: IsNeg for QuotientF comparison (port of isneg(tsin-tmin))
    // Returns 1 if a < b (as quotients), 0 otherwise.
    // -----------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IsNegQ(in QuotientF a, in QuotientF b)
    {
        return (a < b) ? 1 : 0;
    }

    // -----------------------------------------------------------------
    // Helper: inrange as branchless int (returns 1 if in range, 0 otherwise)
    // -----------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int InRange(float val, float lo, float hi)
    {
        return (val >= lo && val <= hi) ? 1 : 0;
    }

    // -----------------------------------------------------------------
    // Helper: IsNonNeg (returns 1 if x >= 0)
    // -----------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IsNonNeg(float x) => MathUtils.IsNeg(x) ^ 1;

    // -----------------------------------------------------------------
    // Polynomial helpers (port of psqr, P1, P2 from CryEngine)
    // P1(x) creates polynomial x*t (degree 1): [0, x]
    // P2(x) creates polynomial x*t^2 (degree 2): [0, 0, x]
    // PSqr(p) = p*p
    // -----------------------------------------------------------------
    private static Polynomial MakeP1(float x)
    {
        var p = new Polynomial(1);
        p[0] = 0; p[1] = x;
        return p;
    }

    private static Polynomial MakeP2(float x)
    {
        var p = new Polynomial(2);
        p[0] = 0; p[1] = 0; p[2] = x;
        return p;
    }

    private static Polynomial PSqr(in Polynomial p)
    {
        return Polynomial.Multiply(p, p);
    }

    /// <summary>
    /// Build polynomial: P2(a) + P1(b) + c  (degree 2)
    /// This is a*t^2 + b*t + c
    /// </summary>
    private static Polynomial MakePoly2(float a, float b, float c)
    {
        var p = new Polynomial(2);
        p[0] = c; p[1] = b; p[2] = a;
        return p;
    }

    /// <summary>
    /// Build polynomial: P1(a) + b  (degree 1)
    /// This is a*t + b
    /// </summary>
    private static Polynomial MakePoly1(float a, float b)
    {
        var p = new Polynomial(1);
        p[0] = b; p[1] = a;
        return p;
    }

    /// <summary>
    /// Polynomial subtraction helper: subtract polynomial b from polynomial a.
    /// </summary>
    private static Polynomial PSub(in Polynomial a, in Polynomial b)
    {
        return Polynomial.Subtract(a, b);
    }

    /// <summary>
    /// Polynomial addition helper.
    /// </summary>
    private static Polynomial PAdd(in Polynomial a, in Polynomial b)
    {
        return Polynomial.Add(a, b);
    }

    /// <summary>
    /// Polynomial scalar multiply helper.
    /// </summary>
    private static Polynomial PMul(in Polynomial p, float s)
    {
        return p * s;
    }

    /// <summary>
    /// Find polynomial roots in range and return count.
    /// Port of pn.nroots(a,b) which just checks if there are any roots.
    /// We approximate by actually finding them.
    /// </summary>
    private static bool HasRoots(in Polynomial pn, float start, float end)
    {
        Span<float> tmpRoots = stackalloc float[pn.Degree + 1];
        return pn.FindRoots(start, end, tmpRoots) > 0;
    }

    /// <summary>
    /// Find polynomial roots in [start, end].
    /// Port of pn.findroots(start, end, roots).
    /// </summary>
    private static int FindRoots(in Polynomial pn, float start, float end, Span<float> roots)
    {
        return pn.FindRoots(start, end, roots);
    }

    // -----------------------------------------------------------------
    // GetRotated helpers for PhysVector3
    // Port of Vec3::GetRotated(center, axis, cos, sin)
    // -----------------------------------------------------------------
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PhysVector3 GetRotated(in PhysVector3 v, in PhysVector3 center, in PhysVector3 axis, float cosa, float sina)
    {
        var rel = v - center;
        var vz = axis * rel.Dot(axis);
        var vx = rel - vz;
        var vy = axis ^ vx;
        return center + vz + vx * cosa + vy * sina;
    }

    // GetRotated(axis, cos, sin) -- no center, just rotation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PhysVector3 GetRotated(in PhysVector3 v, in PhysVector3 axis, float cosa, float sina)
    {
        var vz = axis * v.Dot(axis);
        var vx = v - vz;
        var vy = axis ^ vx;
        return vz + vx * cosa + vy * sina;
    }

    // ================================================================
    //   tri_tri_rot_unprojection
    // ================================================================
    public static int TriTriRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        var ptri1 = (Triangle)prim1;
        var ptri2 = (Triangle)prim2;
        var ptri = new[] { ptri1, ptri2 };

        var ptz = new PhysVector3[2, 3];
        var ptx = new PhysVector3[2, 3];
        var pty = new PhysVector3[2, 3];
        PhysVector3 pt, rotax, pt0, pt1, edge0, edge1, n0z, n0x, n0y, ptz0, ptx0, pty0, n, pt0_rot,
            pt1_rot, edge0_rot, ptz1, ptx1, pty1, dir0, dir1;
        var norm = new PhysVector3[2];
        QuotientF tsin = default, tcos = default;
        var tmin = new QuotientF(pmode.TMax, 1);
        QuotientF t0 = default, t1 = default;
        float kcos, ksin, k, a, b, c, d, sg0, sg1;
        int itri, i, j, j1, bContact, bBest, idbest = -1, imask = iFeature2 << 8 | iFeature1;

        // compute components for vec.rot(t) = vecz + vecx*cos(t) + vecy*sin(t)
        rotax = pmode.Dir;
        for (itri = 0; itri < 2; itri++, rotax = -rotax)
        {
            for (i = 0; i < 3; i++)
            {
                pt = ptri[itri][i] - pmode.Center;
                ptz[itri, i] = rotax * pt.Dot(rotax);
                ptx[itri, i] = pt - ptz[itri, i];
                pty[itri, i] = rotax ^ ptx[itri, i];
            }
        }

        // vertex-face contacts
        for (itri = 0; itri < 2; itri++)
        {
            for (i = 0; i < 3; i++)
            {
                if ((0x40 << (itri << 3) | (0x80 | i) << ((itri ^ 1) << 3)) != imask)
                {
                    kcos = ptx[itri ^ 1, i].Dot(ptri[itri].Normal);
                    ksin = pty[itri ^ 1, i].Dot(ptri[itri].Normal);
                    k = (ptz[itri ^ 1, i] - ptri[itri][0] + pmode.Center).Dot(ptri[itri].Normal);
                    a = ksin * ksin + kcos * kcos;
                    b = ksin * k;
                    c = k * k - kcos * kcos;
                    d = b * b - a * c;
                    if (d >= 0)
                    {
                        d = MathF.Sqrt(d);
                        tsin = new QuotientF(-b - d, a);
                        for (j = 0; j < 2; j++, tsin.X += d * 2)
                        {
                            if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                                 MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                            {
                                tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                                pt = ptx[itri ^ 1, i] * tcos.X + pty[itri ^ 1, i] * tsin.X +
                                     (ptz[itri ^ 1, i] + pmode.Center) * tsin.Y;
                                bContact =
                                    MathUtils.IsNeg(((pt - ptri[itri][0] * tsin.Y) ^ (ptri[itri][1] - ptri[itri][0])).Dot(ptri[itri].Normal)) &
                                    MathUtils.IsNeg(((pt - ptri[itri][1] * tsin.Y) ^ (ptri[itri][2] - ptri[itri][1])).Dot(ptri[itri].Normal)) &
                                    MathUtils.IsNeg(((pt - ptri[itri][2] * tsin.Y) ^ (ptri[itri][0] - ptri[itri][2])).Dot(ptri[itri].Normal));
                                // check that triangles don't intersect during contact
                                int incI = MathUtils.IncMod3[i];
                                int decI = MathUtils.DecMod3[i];
                                pt = ptx[itri ^ 1, incI] * tcos.X + pty[itri ^ 1, incI] * tsin.X +
                                     (ptz[itri ^ 1, incI] + pmode.Center - ptri[itri][0]) * tsin.Y;
                                sg0 = pt.Dot(ptri[itri].Normal) + pmode.MinPtDist * tsin.Y;
                                pt = ptx[itri ^ 1, decI] * tcos.X + pty[itri ^ 1, decI] * tsin.X +
                                     (ptz[itri ^ 1, decI] + pmode.Center - ptri[itri][0]) * tsin.Y;
                                sg1 = pt.Dot(ptri[itri].Normal) + pmode.MinPtDist * tsin.Y;
                                bContact &= IsNonNeg(sg0 * sg1);
                                bBest = bContact & IsNegQ(tsin, tmin);
                                UpdateIdBest(ref idbest, ref tmin, bBest, itri << 2 | i, tsin);
                            }
                        }
                    }
                    else
                    {
                        // check if triangles are already separated
                        int incI = MathUtils.IncMod3[i];
                        int decI = MathUtils.DecMod3[i];
                        sg0 = (ptri[itri ^ 1][incI] - ptri[itri][0]).Dot(ptri[itri].Normal) + pmode.MinPtDist;
                        sg1 = (ptri[itri ^ 1][decI] - ptri[itri][0]).Dot(ptri[itri].Normal) + pmode.MinPtDist;
                        bBest = IsNonNeg(sg0) & IsNonNeg(sg1);
                        tsin = new QuotientF(0, 1);
                        UpdateIdBest(ref idbest, ref tmin, bBest, itri << 2 | i, tsin);
                    }
                }
            }
        }

        rotax = pmode.Dir;
        n0z = rotax * ptri1.Normal.Dot(rotax);
        n0x = ptri1.Normal - n0z;
        n0y = rotax ^ n0x;

        // edge-edge contacts
        for (i = 0; i < 3; i++)
        {
            pt0 = ptri[0][i] - pmode.Center;
            edge0 = ptri[0][MathUtils.IncMod3[i]] - ptri[0][i];
            pt = pt0 ^ edge0;
            ptz0 = rotax * pt.Dot(rotax); ptx0 = pt - ptz0; pty0 = rotax ^ ptx0;

            for (j = 0; j < 3; j++)
            {
                if ((0xA0 | i | (0xA0 | j) << 8) != imask)
                {
                    pt1 = ptri[1][j] - pmode.Center;
                    edge1 = ptri[1][MathUtils.IncMod3[j]] - ptri[1][j];
                    pt = pt1 ^ edge1;
                    ptz1 = rotax * pt.Dot(rotax); ptx1 = pt - ptz1; pty1 = rotax ^ ptx1;

                    kcos = edge1.Dot(ptx0) + edge0.Dot(ptx1);
                    ksin = edge1.Dot(pty0) - edge0.Dot(pty1);
                    k = edge1.Dot(ptz0) + edge0.Dot(ptz1);
                    a = ksin * ksin + kcos * kcos;
                    b = ksin * k;
                    c = k * k - kcos * kcos;
                    d = b * b - a * c;
                    if (d >= 0)
                    {
                        d = MathF.Sqrt(d);
                        tsin = new QuotientF(-b - d, a);
                        for (j1 = 0; j1 < 2; j1++, tsin.X += d * 2)
                        {
                            if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                                 MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                            {
                                tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                                pt0_rot = ptx[0, i] * tcos.X + pty[0, i] * tsin.X + ptz[0, i] * tsin.Y;
                                int incI = MathUtils.IncMod3[i];
                                pt1_rot = ptx[0, incI] * tcos.X + pty[0, incI] * tsin.X + ptz[0, incI] * tsin.Y;
                                edge0_rot = pt1_rot - pt0_rot;
                                n = edge0_rot ^ edge1;
                                pt = pt1 * tsin.Y - pt0_rot;
                                t0 = new QuotientF((pt ^ edge1).Dot(n), n.LengthSq());
                                t1 = new QuotientF((pt ^ edge0_rot).Dot(n), t0.Y * tsin.Y);
                                bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y) &
                                           MathUtils.IsNeg(MathF.Abs(t1.X * 2 - t1.Y) - t1.Y);
                                // check triangles don't intersect during contact
                                dir0 = (n0z * tsin.Y + n0x * tcos.X + n0y * tsin.X) ^ edge0_rot;
                                dir1 = ptri[1].Normal ^ edge1;
                                bContact &= MathUtils.IsNeg(dir0.Dot(n) * dir1.Dot(n));
                                bBest = bContact & IsNegQ(tsin, tmin);
                                UpdateIdBest(ref idbest, ref tmin, bBest, i << 2 | j | 0x80, tsin);
                            }
                        }
                    }
                    else
                    {
                        // check if triangles are already separated
                        edge1 = ptri[1][MathUtils.IncMod3[j]] - ptri[1][j];
                        n = edge0 ^ edge1;
                        dir0 = ptri[0].Normal ^ edge0;
                        dir1 = ptri[1].Normal ^ edge1;
                        bBest = MathUtils.IsNeg(dir0.Dot(n) * dir1.Dot(n));
                        tsin = new QuotientF(0, 1);
                        // check edges intersection point won't move inside 2nd triangle during unprojection
                        bBest &= MathUtils.IsNeg(dir1.Dot(
                            (pmode.Dir ^ (ptri[0][i] - pmode.Center)) * n.LengthSq() +
                            edge0 * ((ptri[1][j] - ptri[0][i] ^ edge1).Dot(n))));
                        UpdateIdBest(ref idbest, ref tmin, bBest, i << 2 | j | 0x80, tsin);
                    }
                }
            }
        }

        if (idbest == -1)
            return 0;

        if ((idbest & 0x80) != 0)
        {
            i = (idbest >> 2) & 3; j = idbest & 3;
            pcontact.T = tmin.Val();
            pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            int incI = MathUtils.IncMod3[i];
            pt0_rot = ptx[0, i] * tAux + pty[0, i] * tVal + ptz[0, i] + pmode.Center;
            pt1_rot = ptx[0, incI] * tAux + pty[0, incI] * tVal + ptz[0, incI] + pmode.Center;
            edge0_rot = pt1_rot - pt0_rot;
            edge1 = ptri[1][MathUtils.IncMod3[j]] - ptri[1][j];
            pt = ptri[1][j] - pt0_rot;
            n = edge0_rot ^ edge1;
            t0 = new QuotientF((pt ^ edge1).Dot(n), n.LengthSq());
            pcontact.Pt = pt0_rot + edge0_rot * t0.Val();
            pcontact.Normal = n * MathUtils.SgnNZ((ptri2.Normal ^ edge1).Dot(n));
            pcontact.IFeature0 = (uint)(0xA0 | i);
            pcontact.IFeature1 = (uint)(0xA0 | j);
        }
        else
        {
            itri = idbest >> 2; i = idbest & 3;
            pcontact.T = tmin.Val();
            pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = ptz[itri ^ 1, i] + ptx[itri ^ 1, i] * MathF.Max(tAux, (float)(itri ^ 1)) +
                          pty[itri ^ 1, i] * (tVal * itri) + pmode.Center;
            norm[0] = n0z + n0x * tAux + n0y * tVal;
            norm[1] = -ptri[1].Normal;
            pcontact.Normal = norm[itri];
            pcontact.IFeature0 = (uint)(itri == 0 ? 0x40 : (0x80 | i));
            pcontact.IFeature1 = (uint)(itri == 0 ? (0x80 | i) : 0x40);
        }

        return 1;
    }

    // ================================================================
    //   ray_tri_rot_unprojection
    // ================================================================
    public static int RayTriRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        var pray = (Ray)prim1;
        var ptri = (Triangle)prim2;

        var ptz = new PhysVector3[2];
        var ptx = new PhysVector3[2];
        var pty = new PhysVector3[2];
        PhysVector3 pt, rotax = pmode.Dir, edge1, ptz0, ptx0, pty0, pt1, pt0_rot, pt1_rot, edge0_rot, ptz1, ptx1, pty1, n;
        QuotientF tsin = default, tcos = default;
        var tmin = new QuotientF(pmode.TMax, 1);
        QuotientF t0 = default, t1 = default;
        float kcos, ksin, k, a, b, c, d;
        int i, j, bContact, bBest, idbest = -1, imask = iFeature2 << 8 | iFeature1;

        pt = pray.Origin - pmode.Center;
        ptz[0] = rotax * pt.Dot(rotax); ptx[0] = pt - ptz[0]; pty[0] = rotax ^ ptx[0];
        pt = pt + pray.Dir;
        ptz[1] = rotax * pt.Dot(rotax); ptx[1] = pt - ptz[1]; pty[1] = rotax ^ ptx[1];

        // ray end - triangle face
        for (i = 1; i >= 0; i--)
        {
            kcos = ptx[i].Dot(ptri.Normal); ksin = pty[i].Dot(ptri.Normal);
            k = (ptz[i] - ptri[0] + pmode.Center).Dot(ptri.Normal);
            a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                for (j = 0; j < 2; j++, tsin.X += d * 2)
                {
                    if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                         MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                    {
                        tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                        pt = ptx[i] * tcos.X + pty[i] * tsin.X + (ptz[i] + pmode.Center) * tsin.Y;
                        bContact =
                            MathUtils.IsNeg(((pt - ptri[0] * tsin.Y) ^ (ptri[1] - ptri[0])).Dot(ptri.Normal)) &
                            MathUtils.IsNeg(((pt - ptri[1] * tsin.Y) ^ (ptri[2] - ptri[1])).Dot(ptri.Normal)) &
                            MathUtils.IsNeg(((pt - ptri[2] * tsin.Y) ^ (ptri[0] - ptri[2])).Dot(ptri.Normal));
                        bBest = bContact & IsNegQ(tsin, tmin);
                        UpdateIdBest(ref idbest, ref tmin, bBest, i, tsin);
                    }
                }
            }
            if ((pray.Origin - pmode.Center).LengthSq() < pmode.MinPtDist * pmode.MinPtDist)
                break;
        }

        if (idbest >= 0)
        {
            i = idbest & 1;
            pcontact.T = tmin.Val();
            pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = ptz[i] + ptx[i] * tAux + pty[i] * tVal + pmode.Center;
            pcontact.Normal = -ptri.Normal;
            pcontact.IFeature0 = (uint)(0x80 | i);
            pcontact.IFeature1 = 0x40;
            return 1;
        }

        // ray - triangle edge
        pt = (pray.Origin - pmode.Center) ^ pray.Dir;
        ptz0 = rotax * pt.Dot(rotax); ptx0 = pt - ptz0; pty0 = rotax ^ ptx0;
        for (i = 0; i < 3; i++)
        {
            if ((0xA0 | (0xA0 | i) << 8) != imask)
            {
                pt1 = ptri[i] - pmode.Center;
                edge1 = ptri[MathUtils.IncMod3[i]] - ptri[i];
                pt = pt1 ^ edge1;
                ptz1 = rotax * pt.Dot(rotax); ptx1 = pt - ptz1; pty1 = rotax ^ ptx1;

                kcos = edge1.Dot(ptx0) + pray.Dir.Dot(ptx1);
                ksin = edge1.Dot(pty0) - pray.Dir.Dot(pty1);
                k = edge1.Dot(ptz0) + pray.Dir.Dot(ptz1);
                a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
                if (d >= 0)
                {
                    d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                    for (j = 0; j < 2; j++, tsin.X += d * 2)
                    {
                        if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                             MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                        {
                            tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                            pt0_rot = ptx[0] * tcos.X + pty[0] * tsin.X + ptz[0] * tsin.Y;
                            pt1_rot = ptx[1] * tcos.X + pty[1] * tsin.X + ptz[1] * tsin.Y;
                            edge0_rot = pt1_rot - pt0_rot;
                            n = edge0_rot ^ edge1;
                            pt = pt1 * tsin.Y - pt0_rot;
                            t0 = new QuotientF((pt ^ edge1).Dot(n), n.LengthSq());
                            t1 = new QuotientF((pt ^ edge0_rot).Dot(n), t0.Y * tsin.Y);
                            bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y) &
                                       MathUtils.IsNeg(MathF.Abs(t1.X * 2 - t1.Y) - t1.Y);
                            bBest = bContact & IsNegQ(tsin, tmin);
                            UpdateIdBest(ref idbest, ref tmin, bBest, i | 0x80, tsin);
                        }
                    }
                }
            }
        }

        if (idbest < 0)
            return 0;

        i = idbest & 3;
        pcontact.T = tmin.Val();
        pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
        float tv = (float)pcontact.T;
        float ta = (float)pcontact.TAux;
        pt0_rot = ptx[0] * ta + pty[0] * tv + ptz[0] + pmode.Center;
        pt1_rot = ptx[1] * ta + pty[1] * tv + ptz[1] + pmode.Center;
        edge0_rot = pt1_rot - pt0_rot;
        edge1 = ptri[MathUtils.IncMod3[i]] - ptri[i];
        pt = ptri[i] - pt0_rot;
        n = edge0_rot ^ edge1;
        t0 = new QuotientF((pt ^ edge1).Dot(n), n.LengthSq());
        pcontact.Pt = pt0_rot + edge0_rot * t0.Val();
        pcontact.Normal = n * MathUtils.SgnNZ((ptri.Normal ^ edge1).Dot(n));
        pcontact.IFeature0 = 0xA0;
        pcontact.IFeature1 = (uint)(0xA0 | i);
        return 1;
    }

    // ================================================================
    //   tri_ray_rot_unprojection (reverse wrapper)
    // ================================================================
    public static int TriRayRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        pmode.Dir = -pmode.Dir;
        int res = RayTriRotUnprojection(pmode, prim2, iFeature2, prim1, iFeature1, ref pcontact, parea);
        if (res != 0)
        {
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = GetRotated(pcontact.Pt, pmode.Center, pmode.Dir, tAux, -tVal);
            pcontact.Normal = -GetRotated(pcontact.Normal, pmode.Dir, tAux, -tVal);
            uint feat = pcontact.IFeature0;
            pcontact.IFeature0 = pcontact.IFeature1;
            pcontact.IFeature1 = feat;
        }
        pmode.Dir = -pmode.Dir;
        return res;
    }

    // ================================================================
    //   ray_cyl_rot_unprojection
    // ================================================================
    public static int RayCylRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        var pray = (Ray)prim1;
        var pcyl = (Cylinder)prim2;

        PhysVector3 center, ccap, v, vx, vy, vz, l, lx, ly, lz, vsin, vcos, pt;
        var ptz = new PhysVector3[2];
        var ptx = new PhysVector3[2];
        var pty = new PhysVector3[2];
        PhysVector3 rotax = pmode.Dir, axis = pcyl.Axis;
        float a, b, c, d, k, ksin, kcos, hh = pcyl.HalfHeight, r2 = pcyl.Radius * pcyl.Radius;
        float len2 = pray.Dir.LengthSq();
        Span<float> roots = stackalloc float[5];
        var tmax = new QuotientF(0, 1);
        QuotientF tsin = default, tcos = default, t = default;
        int i, j, icap, idbest = -1, bContact, bBest;
        center = pcyl.Center - pmode.Center;

        pt = pray.Origin - pmode.Center;
        ptz[0] = rotax * pt.Dot(rotax); ptx[0] = pt - ptz[0]; pty[0] = rotax ^ ptx[0];
        pt = pt + pray.Dir;
        ptz[1] = rotax * pt.Dot(rotax); ptx[1] = pt - ptz[1]; pty[1] = rotax ^ ptx[1];

        for (i = 1; i >= 0; i--)
        {
            // ray end - cylinder cap
            kcos = ptx[i].Dot(axis); ksin = pty[i].Dot(axis);
            for (icap = -1; icap <= 1; icap += 2)
            {
                k = (ptz[i] - center - axis * (hh * icap)).Dot(axis);
                a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
                if (d >= 0)
                {
                    d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                    for (j = 0; j < 2; j++, tsin.X += d * 2)
                    {
                        if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                             MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                        {
                            tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                            pt = ptx[i] * tcos.X + pty[i] * tsin.X + ptz[i] * tsin.Y;
                            bContact = MathUtils.IsNeg((pt - (center + axis * (hh * icap)) * tsin.Y).LengthSq() - r2 * tsin.Y * tsin.Y);
                            bBest = bContact & IsNegQ(tmax, tsin);
                            UpdateIdBest(ref idbest, ref tmax, bBest, icap + 1 | i, tsin);
                        }
                    }
                }
            }

            // ray end - cylinder side
            v = (ptz[i] - center) ^ axis;
            vcos = ptx[i] ^ axis; vsin = pty[i] ^ axis;
            // Build polynomial: psqr(P2(sqr(vsin)-sqr(vcos)) + P1((v*vsin)*2) + sqr(v)+sqr(vcos)-r2) -
            //   psqr(P1(vcos*vsin)+v*vcos)*((real)1-P2(1))*4
            var poly1 = MakePoly2(vsin.LengthSq() - vcos.LengthSq(), v.Dot(vsin) * 2,
                                  v.LengthSq() + vcos.LengthSq() - r2);
            var poly2 = MakePoly1(vcos.Dot(vsin), v.Dot(vcos));
            // (1 - P2(1)) = 1 - t^2, which is a degree-2 polynomial [1, 0, -1]
            var oneMinusT2 = new Polynomial(2);
            oneMinusT2[0] = 1; oneMinusT2[1] = 0; oneMinusT2[2] = -1;
            var pn = PSub(PSqr(poly1), Polynomial.Multiply(PSqr(poly2), oneMinusT2) * 4);
            if (HasRoots(pn, 0, 1))
            {
                int nRoots = FindRoots(pn, 0, 1, roots);
                for (j = nRoots - 1; j >= 0; j--)
                {
                    tsin = new QuotientF(roots[j], 1);
                    pt = ptz[i] + ptx[i] * MathF.Sqrt(1 - tsin.X * tsin.X) + pty[i] * tsin.X - center;
                    bContact = MathUtils.IsNeg(MathF.Abs(pt.Dot(axis)) - hh) &
                               InRange((pt ^ axis).LengthSq(), r2 * 0.99f * 0.99f, r2 * 1.01f * 1.01f);
                    bBest = IsNegQ(tmax, tsin) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, 0x10 | i, tsin);
                }
            }

            if ((pray.Origin - pmode.Center).LengthSq() < pmode.MinPtDist * pmode.MinPtDist)
                break;
        }

        // ray - cylinder side
        lz = rotax * pray.Dir.Dot(rotax); lx = pray.Dir - lz; ly = rotax ^ lx;
        v = (pray.Origin - pmode.Center) ^ pray.Dir;
        vz = rotax * v.Dot(rotax); vx = v - vz; vy = rotax ^ vx;
        v = axis ^ center;
        kcos = axis.Dot(vx) - v.Dot(lx); ksin = axis.Dot(vy) - v.Dot(ly); k = axis.Dot(vz) - v.Dot(lz);
        vcos = lx ^ axis; vsin = ly ^ axis; v = lz ^ axis;
        Polynomial pnSide;
        {
            var sub = PSqr(MakePoly1((kcos * ksin - r2 * vcos.Dot(vsin)) * 2,
                                     (kcos * k - r2 * vcos.Dot(v)) * 2));
            var mainPoly = MakePoly2(
                ksin * ksin - kcos * kcos - r2 * (vsin.LengthSq() - vcos.LengthSq()),
                (ksin * k - r2 * vsin.Dot(v)) * 2,
                kcos * kcos + k * k - r2 * (vcos.LengthSq() + v.LengthSq()));
            pnSide = PSub(PSqr(mainPoly), sub);
        }
        if (HasRoots(pnSide, 0, 1))
        {
            int nRoots = FindRoots(pnSide, 0, 1, roots);
            for (j = nRoots - 1; j >= 0; j--)
            {
                tsin = new QuotientF(roots[j], 1); tcos.X = MathF.Sqrt(1 - tsin.X * tsin.X);
                pt = ptz[0] + ptx[0] * tcos.X + pty[0] * tsin.X - center;
                l = lz + lx * tcos.X + ly * tsin.X;
                v = l ^ axis; k = v.LengthSq();
                bContact = InRange(pt.Dot(v) * pt.Dot(v), r2 * k * 0.99f * 0.99f, r2 * k * 1.01f * 1.01f);
                t = new QuotientF((-pt ^ axis).Dot(v), k);
                pt = pt * t.Y + l * t.X;
                bContact &= InRange(t.X, 0, t.Y);
                bContact &= MathUtils.IsNeg(MathF.Abs(pt.Dot(axis)) - hh * t.Y);
                bBest = IsNegQ(tmax, tsin) & bContact;
                UpdateIdBest(ref idbest, ref tmax, bBest, 0x20, tsin);
            }
        }

        // ray - cylinder cap
        for (icap = -1; icap <= 1; icap += 2)
        {
            ccap = center + axis * (hh * icap);
            vcos = (axis ^ vx) - (axis ^ (ccap ^ lx));
            vsin = (axis ^ vy) - (axis ^ (ccap ^ ly));
            v = (axis ^ vz) - (axis ^ (ccap ^ lz));
            kcos = lx.Dot(axis); ksin = ly.Dot(axis); k = lz.Dot(axis);
            Polynomial pnCap;
            {
                var mainPoly = MakePoly2(
                    vsin.LengthSq() - vcos.LengthSq() - r2 * (ksin * ksin - kcos * kcos),
                    (vsin.Dot(v) - r2 * ksin * k) * 2,
                    vcos.LengthSq() + v.LengthSq() - r2 * (kcos * kcos + k * k));
                var sub = PSqr(MakePoly1((vcos.Dot(vsin) - r2 * kcos * ksin) * 2,
                                         (vcos.Dot(v) - r2 * kcos * k) * 2));
                pnCap = PSub(PSqr(mainPoly), sub);
            }
            if (HasRoots(pnCap, 0, 1))
            {
                int nRoots = FindRoots(pnCap, 0, 1, roots);
                for (j = nRoots - 1; j >= 0; j--)
                {
                    tsin = new QuotientF(roots[j], 1); tcos.X = MathF.Sqrt(1 - tsin.X * tsin.X);
                    pt = ptz[0] + ptx[0] * tcos.X + pty[0] * tsin.X;
                    l = lz + lx * tcos.X + ly * tsin.X;
                    t = new QuotientF((ccap - pt).Dot(axis), l.Dot(axis));
                    bContact = InRange(((pt - ccap) * t.Y + l * t.X).LengthSq(),
                                       r2 * t.Y * 0.96f * (t.Y * 0.96f),
                                       r2 * t.Y * 1.04f * (t.Y * 1.04f));
                    bContact &= InRange(t.X, 0, t.Y);
                    bBest = IsNegQ(tmax, tsin) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, 0x40 | ((icap + 1) >> 1), tsin);
                }
            }
        }

        if (idbest < 0)
            return 0;

        switch (idbest & 0xF0)
        {
            case 0x00: // ray end - cyl cap
                i = idbest & 1; icap = (idbest & 2) - 1;
                pcontact.T = tmax.Val();
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pcontact.Pt = ptz[i] + ptx[i] * (float)pcontact.TAux + pty[i] * (float)pcontact.T + pmode.Center;
                pcontact.Normal = axis * -icap;
                pcontact.IFeature0 = (uint)(0x80 | i);
                pcontact.IFeature1 = (uint)(0x41 + ((icap + 1) >> 1));
                break;
            case 0x10: // ray end - cyl side
                i = idbest & 1;
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pcontact.Pt = ptz[i] + ptx[i] * (float)pcontact.TAux + pty[i] * (float)pcontact.T + pmode.Center;
                pcontact.Normal = pcyl.Center - pcontact.Pt;
                pcontact.Normal = pcontact.Normal - pcyl.Axis * pcyl.Axis.Dot(pcontact.Normal);
                pcontact.Normal.Normalize();
                pcontact.IFeature0 = (uint)(0x80 | i);
                pcontact.IFeature1 = 0x40;
                break;
            case 0x20: // ray - cyl side
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pt = ptz[0] + ptx[0] * (float)pcontact.TAux + pty[0] * (float)pcontact.T + pmode.Center;
                l = lz + lx * (float)pcontact.TAux + ly * (float)pcontact.T;
                t = new QuotientF(((pcyl.Center - pt) ^ axis).Dot(l ^ axis), (l ^ axis).LengthSq());
                pcontact.Pt = pt + l * t.Val();
                pcontact.Normal = (l ^ axis).Normalized();
                pcontact.Normal = pcontact.Normal * MathUtils.SgnNZ(pcontact.Normal.Dot(pcyl.Center - pcontact.Pt));
                pcontact.IFeature0 = 0x20;
                pcontact.IFeature1 = 0x40;
                break;
            case 0x40: // ray - cyl cap
                icap = ((idbest & 1) << 1) - 1;
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pt = ptz[0] + ptx[0] * (float)pcontact.TAux + pty[0] * (float)pcontact.T;
                l = lz + lx * (float)pcontact.TAux + ly * (float)pcontact.T;
                ccap = center + axis * (hh * icap);
                t = new QuotientF((ccap - pt).Dot(axis), l.Dot(axis));
                pcontact.Pt = pt + l * t.Val();
                pcontact.Normal = pcontact.Pt - ccap;
                pcontact.Pt = pcontact.Pt + pmode.Center;
                pcontact.Normal = ((axis ^ pcontact.Normal) ^ l).Normalized();
                pcontact.Normal = pcontact.Normal * MathUtils.SgnNZ(pcontact.Normal.Dot(pcyl.Center - pcontact.Pt));
                pcontact.IFeature0 = 0x20;
                pcontact.IFeature1 = (uint)(0x20 | ((icap + 1) >> 1));
                break;
        }
        return 1;
    }

    // ================================================================
    //   cyl_ray_rot_unprojection (reverse wrapper)
    // ================================================================
    public static int CylRayRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        pmode.Dir = -pmode.Dir;
        int res = RayCylRotUnprojection(pmode, prim2, iFeature2, prim1, iFeature1, ref pcontact, parea);
        if (res != 0)
        {
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = GetRotated(pcontact.Pt, pmode.Center, pmode.Dir, tAux, -tVal);
            pcontact.Normal = -GetRotated(pcontact.Normal, pmode.Dir, tAux, -tVal);
            uint feat = pcontact.IFeature0;
            pcontact.IFeature0 = pcontact.IFeature1;
            pcontact.IFeature1 = feat;
        }
        pmode.Dir = -pmode.Dir;
        return res;
    }

    // ================================================================
    //   ray_box_rot_unprojection
    // ================================================================
    public static int RayBoxRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        var pray = (Ray)prim1;
        var pbox = (Box)prim2;

        int i, j, ifeat, idir, ix, iy, bContact, bBest, idbest = -1;
        var ptz = new PhysVector3[2];
        var ptx = new PhysVector3[2];
        var pty = new PhysVector3[2];
        PhysVector3 pt, ptz0, ptx0, pty0, pt1, pt0_rot, pt1_rot, edge0_rot, ptz1, ptx1, pty1, origin, dir, center, rotax, n, size;
        QuotientF tsin = default, tcos = default;
        var tmax = new QuotientF(0, 1);
        QuotientF t0 = default, t1 = default;
        float kcos, ksin, k, a, b, c, d;

        origin = pbox.Basis * (pray.Origin - pbox.Center);
        dir = pbox.Basis * pray.Dir;
        center = pbox.Basis * (pmode.Center - pbox.Center);
        rotax = pbox.Basis * pmode.Dir;
        size = pbox.Size;
        pt = origin - center;
        ptz[0] = rotax * pt.Dot(rotax); ptx[0] = pt - ptz[0]; pty[0] = rotax ^ ptx[0];
        pt = pt + dir;
        ptz[1] = rotax * pt.Dot(rotax); ptx[1] = pt - ptz[1]; pty[1] = rotax ^ ptx[1];

        // ray end - box face
        for (i = 1; i >= 0; i--)
        {
            for (ifeat = 0; ifeat < 6; ifeat++)
            {
                idir = ifeat >> 1; ix = MathUtils.IncMod3[idir]; iy = MathUtils.DecMod3[idir];
                kcos = ptx[i][idir]; ksin = pty[i][idir];
                k = ptz[i][idir] - size[idir] * ((ifeat & 1) * 2 - 1) + center[idir];
                a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
                if (d >= 0)
                {
                    d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                    for (j = 0; j < 2; j++, tsin.X += d * 2)
                    {
                        if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                             MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                        {
                            tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                            pt = ptx[i] * tcos.X + pty[i] * tsin.X + (ptz[i] + center) * tsin.Y;
                            bContact =
                                MathUtils.IsNeg(MathF.Abs(pt[ix]) - size[ix] * tsin.Y) &
                                MathUtils.IsNeg(MathF.Abs(pt[iy]) - size[iy] * tsin.Y);
                            bBest = bContact & IsNegQ(tmax, tsin);
                            UpdateIdBest(ref idbest, ref tmax, bBest, i | ifeat << 1, tsin);
                        }
                    }
                }
            }
            if ((pray.Origin - pmode.Center).LengthSq() < pmode.MinPtDist * pmode.MinPtDist)
                break;
        }

        // ray - box edge
        pt = (origin - center) ^ dir;
        ptz0 = rotax * pt.Dot(rotax); ptx0 = pt - ptz0; pty0 = rotax ^ ptx0;
        for (ifeat = 0; ifeat < 12; ifeat++)
        {
            idir = ifeat >> 2; ix = MathUtils.IncMod3[idir]; iy = MathUtils.DecMod3[idir];
            pt1 = PhysVector3.Zero;
            pt1[idir] = 0;
            pt1[ix] = size[ix] * ((ifeat & 1) * 2 - 1);
            pt1[iy] = size[iy] * ((ifeat & 2) - 1);
            pt1 = pt1 - center;
            pt = MathUtils.CrossWithOrt(pt1, idir);
            ptz1 = rotax * pt.Dot(rotax); ptx1 = pt - ptz1; pty1 = rotax ^ ptx1;
            kcos = ptx0[idir] + dir.Dot(ptx1);
            ksin = pty0[idir] - dir.Dot(pty1);
            k = ptz0[idir] + dir.Dot(ptz1);
            a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                for (j = 0; j < 2; j++, tsin.X += d * 2)
                {
                    if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                         MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                    {
                        tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                        pt0_rot = ptx[0] * tcos.X + pty[0] * tsin.X + ptz[0] * tsin.Y;
                        pt1_rot = ptx[1] * tcos.X + pty[1] * tsin.X + ptz[1] * tsin.Y;
                        edge0_rot = pt1_rot - pt0_rot;
                        n = MathUtils.CrossWithOrt(edge0_rot, idir);
                        pt1 = PhysVector3.Zero;
                        pt1[idir] = 0;
                        pt1[ix] = size[ix] * ((ifeat & 1) * 2 - 1);
                        pt1[iy] = size[iy] * ((ifeat & 2) - 1);
                        pt1 = pt1 - center;
                        pt = pt1 * tsin.Y - pt0_rot;
                        t0 = new QuotientF(MathUtils.CrossWithOrt(pt, idir).Dot(n), n.LengthSq());
                        t1 = new QuotientF((pt ^ edge0_rot).Dot(n), t0.Y * tsin.Y);
                        bContact = InRange(t0.X, 0, t0.Y) &
                                   MathUtils.IsNeg(MathF.Abs(t1.X) - MathF.Abs(t1.Y) * size[idir]);
                        bBest = bContact & IsNegQ(tmax, tsin);
                        UpdateIdBest(ref idbest, ref tmax, bBest, ifeat | 0x80, tsin);
                    }
                }
            }
        }

        if (idbest < 0)
            return 0;

        if ((idbest & 0x80) == 0)
        {
            i = idbest & 1; ifeat = idbest >> 1; idir = ifeat >> 1;
            pcontact.T = tmax.Val();
            pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
            float tv = (float)pcontact.T, ta = (float)pcontact.TAux;
            pcontact.Pt = (ptz[i] + ptx[i] * ta + pty[i] * tv + center);
            // Transform back to world: pt * Basis^T + center (since Basis transforms world->box,
            // Basis^T transforms box->world, which is equivalent to multiplying vector * Basis for row-major)
            pcontact.Pt = pbox.Basis.Transposed() * pcontact.Pt + pbox.Center;
            n = PhysVector3.Zero;
            n[idir] = 1 - (ifeat & 1) * 2;
            pcontact.Normal = pbox.Basis.Transposed() * n;
            pcontact.IFeature0 = (uint)(0x80 | i);
            pcontact.IFeature1 = (uint)(0x40 | ifeat);
        }
        else
        {
            ifeat = idbest & 0x7F; idir = ifeat >> 2; ix = MathUtils.IncMod3[idir]; iy = MathUtils.DecMod3[idir];
            pcontact.T = tmax.Val();
            pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
            float tv = (float)pcontact.T, ta = (float)pcontact.TAux;
            pt0_rot = ptx[0] * ta + pty[0] * tv + ptz[0] + center;
            pt1_rot = ptx[1] * ta + pty[1] * tv + ptz[1] + center;
            edge0_rot = pt1_rot - pt0_rot;
            pt = PhysVector3.Zero;
            pt[idir] = 0;
            pt[ix] = size[ix] * ((ifeat & 1) * 2 - 1);
            pt[iy] = size[iy] * ((ifeat & 2) - 1);
            n = MathUtils.CrossWithOrt(edge0_rot, idir);
            t0 = new QuotientF(MathUtils.CrossWithOrt(pt - pt0_rot, idir).Dot(n), n.LengthSq());
            pt = pt0_rot + edge0_rot * t0.Val();
            pcontact.Normal = pbox.Basis.Transposed() * (n * -MathUtils.SgnNZ(n.Dot(pt)));
            pcontact.Pt = pbox.Basis.Transposed() * pt + pbox.Center;
            pcontact.IFeature0 = 0xA0;
            pcontact.IFeature1 = (uint)(0x20 | ifeat);
        }

        return 1;
    }

    // ================================================================
    //   box_ray_rot_unprojection (reverse wrapper)
    // ================================================================
    public static int BoxRayRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        pmode.Dir = -pmode.Dir;
        int res = RayBoxRotUnprojection(pmode, prim2, iFeature2, prim1, iFeature1, ref pcontact, parea);
        if (res != 0)
        {
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = GetRotated(pcontact.Pt, pmode.Center, pmode.Dir, tAux, -tVal);
            pcontact.Normal = -GetRotated(pcontact.Normal, pmode.Dir, tAux, -tVal);
            uint feat = pcontact.IFeature0;
            pcontact.IFeature0 = pcontact.IFeature1;
            pcontact.IFeature1 = feat;
        }
        pmode.Dir = -pmode.Dir;
        return res;
    }

    // ================================================================
    //   ray_capsule_rot_unprojection
    // ================================================================
    public static int RayCapsuleRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        var pray = (Ray)prim1;
        var pcaps = (Capsule)prim2;

        PhysVector3 center, ccap, v, vx, vy, vz, l, lx, ly, lz, vsin, vcos, pt;
        var ptz = new PhysVector3[2];
        var ptx = new PhysVector3[2];
        var pty = new PhysVector3[2];
        PhysVector3 rotax = pmode.Dir, axis = pcaps.Axis;
        float a, b, c, d, k, ksin, kcos, hh = pcaps.HalfHeight, r2 = pcaps.Radius * pcaps.Radius;
        float len2 = pray.Dir.LengthSq();
        Span<float> roots = stackalloc float[5];
        var tmax = new QuotientF(0, 1);
        QuotientF tsin = default, tcos = default, t = default;
        int i, j, icap, idbest = -1, bContact, bBest;
        center = pcaps.Center - pmode.Center;

        pt = pray.Origin - pmode.Center;
        ptz[0] = rotax * pt.Dot(rotax); ptx[0] = pt - ptz[0]; pty[0] = rotax ^ ptx[0];
        pt = pt + pray.Dir;
        ptz[1] = rotax * pt.Dot(rotax); ptx[1] = pt - ptz[1]; pty[1] = rotax ^ ptx[1];

        for (i = 1; i >= 0; i--)
        {
            // ray end - capsule cap (sphere at each end)
            for (icap = -1; icap <= 1; icap += 2)
            {
                ccap = center + axis * (hh * icap);
                kcos = ptx[i].Dot(ptz[i] - ccap) * 2;
                ksin = pty[i].Dot(ptz[i] - ccap) * 2;
                k = ptx[i].LengthSq() + (ptz[i] - ccap).LengthSq() - r2;
                a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
                if (d >= 0)
                {
                    d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                    for (j = 0; j < 2; j++, tsin.X += d * 2)
                    {
                        if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                             MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                        {
                            tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                            pt = ptx[i] * tcos.X + pty[i] * tsin.X + ptz[i] * tsin.Y;
                            bBest = IsNegQ(tmax, tsin);
                            UpdateIdBest(ref idbest, ref tmax, bBest, icap + 1 | i, tsin);
                        }
                    }
                }
            }

            // ray end - capsule side
            v = (ptz[i] - center) ^ axis;
            vcos = ptx[i] ^ axis; vsin = pty[i] ^ axis;
            var poly1 = MakePoly2(vsin.LengthSq() - vcos.LengthSq(), v.Dot(vsin) * 2,
                                  v.LengthSq() + vcos.LengthSq() - r2);
            var poly2 = MakePoly1(vcos.Dot(vsin), v.Dot(vcos));
            var oneMinusT2 = new Polynomial(2);
            oneMinusT2[0] = 1; oneMinusT2[1] = 0; oneMinusT2[2] = -1;
            var pn = PSub(PSqr(poly1), Polynomial.Multiply(PSqr(poly2), oneMinusT2) * 4);
            if (HasRoots(pn, 0, 1))
            {
                int nRoots = FindRoots(pn, 0, 1, roots);
                for (j = nRoots - 1; j >= 0; j--)
                {
                    tsin = new QuotientF(roots[j], 1);
                    pt = ptz[i] + ptx[i] * MathF.Sqrt(1 - tsin.X * tsin.X) + pty[i] * tsin.X - center;
                    bContact = MathUtils.IsNeg(MathF.Abs(pt.Dot(axis)) - hh) &
                               InRange((pt ^ axis).LengthSq(), r2 * 0.99f * 0.99f, r2 * 1.01f * 1.01f);
                    bBest = IsNegQ(tmax, tsin) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, 0x10 | i, tsin);
                }
            }

            if ((pray.Origin - pmode.Center).LengthSq() < pmode.MinPtDist * pmode.MinPtDist)
                break;
        }

        // ray - capsule side
        lz = rotax * pray.Dir.Dot(rotax); lx = pray.Dir - lz; ly = rotax ^ lx;
        v = (pray.Origin - pmode.Center) ^ pray.Dir;
        vz = rotax * v.Dot(rotax); vx = v - vz; vy = rotax ^ vx;
        v = axis ^ center;
        kcos = axis.Dot(vx) - v.Dot(lx); ksin = axis.Dot(vy) - v.Dot(ly); k = axis.Dot(vz) - v.Dot(lz);
        vcos = lx ^ axis; vsin = ly ^ axis; v = lz ^ axis;
        {
            var sub = PSqr(MakePoly1((kcos * ksin - r2 * vcos.Dot(vsin)) * 2,
                                     (kcos * k - r2 * vcos.Dot(v)) * 2));
            var mainPoly = MakePoly2(
                ksin * ksin - kcos * kcos - r2 * (vsin.LengthSq() - vcos.LengthSq()),
                (ksin * k - r2 * vsin.Dot(v)) * 2,
                kcos * kcos + k * k - r2 * (vcos.LengthSq() + v.LengthSq()));
            var pn2 = PSub(PSqr(mainPoly), sub);
            if (HasRoots(pn2, 0, 1))
            {
                int nRoots = FindRoots(pn2, 0, 1, roots);
                for (j = nRoots - 1; j >= 0; j--)
                {
                    tsin = new QuotientF(roots[j], 1); tcos.X = MathF.Sqrt(1 - tsin.X * tsin.X);
                    pt = ptz[0] + ptx[0] * tcos.X + pty[0] * tsin.X - center;
                    l = lz + lx * tcos.X + ly * tsin.X;
                    v = l ^ axis; k = v.LengthSq();
                    bContact = InRange(pt.Dot(v) * pt.Dot(v), r2 * k * 0.99f * 0.99f, r2 * k * 1.01f * 1.01f);
                    t = new QuotientF((-pt ^ axis).Dot(v), k);
                    pt = pt * t.Y + l * t.X;
                    bContact &= InRange(t.X, 0, t.Y);
                    bContact &= MathUtils.IsNeg(MathF.Abs(pt.Dot(axis)) - hh * t.Y);
                    bBest = IsNegQ(tmax, tsin) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, 0x20, tsin);
                }
            }
        }

        // ray - capsule cap
        for (icap = -1; icap <= 1; icap += 2)
        {
            ccap = center + axis * (hh * icap);
            vcos = (lx ^ ccap) + vx; vsin = (ly ^ ccap) + vy; v = (lz ^ ccap) + vz;
            var poly1c = MakePoly2(vsin.LengthSq() - vcos.LengthSq(), v.Dot(vsin) * 2,
                                   v.LengthSq() + vcos.LengthSq() - r2 * pray.Dir.LengthSq());
            var poly2c = MakePoly1(vcos.Dot(vsin), v.Dot(vcos));
            var oneMinusT2 = new Polynomial(2);
            oneMinusT2[0] = 1; oneMinusT2[1] = 0; oneMinusT2[2] = -1;
            var pnCap = PSub(PSqr(poly1c), Polynomial.Multiply(PSqr(poly2c), oneMinusT2) * 4);
            if (HasRoots(pnCap, 0, 1))
            {
                int nRoots = FindRoots(pnCap, 0, 1, roots);
                for (j = nRoots - 1; j >= 0; j--)
                {
                    tsin = new QuotientF(roots[j], 1); tcos.X = MathF.Sqrt(1 - tsin.X * tsin.X);
                    pt = ptz[0] + ptx[0] * tcos.X + pty[0] * tsin.X;
                    l = lz + lx * tcos.X + ly * tsin.X;
                    t = new QuotientF((ccap - pt).Dot(l), l.LengthSq());
                    bContact = InRange(((pt - ccap) * t.Y + l * t.X).LengthSq(),
                                       r2 * t.Y * 0.99f * (t.Y * 0.99f),
                                       r2 * t.Y * 1.01f * (t.Y * 1.01f));
                    bContact &= InRange(t.X, 0, t.Y);
                    bBest = IsNegQ(tmax, tsin) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, 0x40 | ((icap + 1) >> 1), tsin);
                }
            }
        }

        if (idbest < 0)
            return 0;

        switch (idbest & 0xF0)
        {
            case 0x00: // ray end - capsule cap
                i = idbest & 1; icap = (idbest & 2) - 1;
                pcontact.T = tmax.Val();
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pcontact.Pt = ptz[i] + ptx[i] * (float)pcontact.TAux + pty[i] * (float)pcontact.T + pmode.Center;
                pcontact.Normal = (pcaps.Center + pcaps.Axis * (pcaps.HalfHeight * icap) - pcontact.Pt).Normalized();
                pcontact.IFeature0 = (uint)(0x80 | i);
                pcontact.IFeature1 = (uint)(0x41 + ((icap + 1) >> 1));
                break;
            case 0x10: // ray end - capsule side
                i = idbest & 1;
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pcontact.Pt = ptz[i] + ptx[i] * (float)pcontact.TAux + pty[i] * (float)pcontact.T + pmode.Center;
                pcontact.Normal = pcaps.Center - pcontact.Pt;
                pcontact.Normal = pcontact.Normal - pcaps.Axis * pcaps.Axis.Dot(pcontact.Normal);
                pcontact.Normal.Normalize();
                pcontact.IFeature0 = (uint)(0x80 | i);
                pcontact.IFeature1 = 0x40;
                break;
            case 0x20: // ray - capsule side
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pt = ptz[0] + ptx[0] * (float)pcontact.TAux + pty[0] * (float)pcontact.T + pmode.Center;
                l = lz + lx * (float)pcontact.TAux + ly * (float)pcontact.T;
                t = new QuotientF(((pcaps.Center - pt) ^ axis).Dot(l ^ axis), (l ^ axis).LengthSq());
                pcontact.Pt = pt + l * t.Val();
                pcontact.Normal = (l ^ axis).Normalized();
                pcontact.Normal = pcontact.Normal * MathUtils.SgnNZ(pcontact.Normal.Dot(pcaps.Center - pcontact.Pt));
                pcontact.IFeature0 = 0x20;
                pcontact.IFeature1 = 0x40;
                break;
            case 0x40: // ray - capsule cap
                icap = ((idbest & 1) << 1) - 1;
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pt = ptz[0] + ptx[0] * (float)pcontact.TAux + pty[0] * (float)pcontact.T;
                l = lz + lx * (float)pcontact.TAux + ly * (float)pcontact.T;
                ccap = center + axis * (hh * icap);
                t = new QuotientF((ccap - pt).Dot(l), l.LengthSq());
                pcontact.Pt = pt + l * t.Val();
                pcontact.Normal = (ccap - pcontact.Pt).Normalized();
                pcontact.Pt = pcontact.Pt + pmode.Center;
                pcontact.IFeature0 = 0x20;
                pcontact.IFeature1 = (uint)(0x20 | ((icap + 1) >> 1));
                break;
        }
        return 1;
    }

    // ================================================================
    //   capsule_ray_rot_unprojection (reverse wrapper)
    // ================================================================
    public static int CapsuleRayRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        pmode.Dir = -pmode.Dir;
        int res = RayCapsuleRotUnprojection(pmode, prim2, iFeature2, prim1, iFeature1, ref pcontact, parea);
        if (res != 0)
        {
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = GetRotated(pcontact.Pt, pmode.Center, pmode.Dir, tAux, -tVal);
            pcontact.Normal = -GetRotated(pcontact.Normal, pmode.Dir, tAux, -tVal);
            uint feat = pcontact.IFeature0;
            pcontact.IFeature0 = pcontact.IFeature1;
            pcontact.IFeature1 = feat;
        }
        pmode.Dir = -pmode.Dir;
        return res;
    }

    // ================================================================
    //   ray_sphere_rot_unprojection
    // ================================================================
    public static int RaySphereRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        var pray = (Ray)prim1;
        var psph = (Sphere)prim2;

        PhysVector3 center, v, vx, vy, vz, l, lx, ly, lz, vsin, vcos, pt;
        var ptz = new PhysVector3[2];
        var ptx = new PhysVector3[2];
        var pty = new PhysVector3[2];
        PhysVector3 rotax = pmode.Dir;
        float a, b, c, d, k, ksin, kcos, r2 = psph.Radius * psph.Radius;
        float len2 = pray.Dir.LengthSq();
        Span<float> roots = stackalloc float[5];
        var tmax = new QuotientF(0, 1);
        QuotientF tsin = default, tcos = default, t = default;
        int i, j, idbest = -1, bContact, bBest;
        center = psph.Center - pmode.Center;

        pt = pray.Origin - pmode.Center;
        ptz[0] = rotax * pt.Dot(rotax); ptx[0] = pt - ptz[0]; pty[0] = rotax ^ ptx[0];
        pt = pt + pray.Dir;
        ptz[1] = rotax * pt.Dot(rotax); ptx[1] = pt - ptz[1]; pty[1] = rotax ^ ptx[1];

        for (i = 1; i >= 0; i--)
        {
            // ray end - sphere
            kcos = ptx[i].Dot(ptz[i] - center) * 2;
            ksin = pty[i].Dot(ptz[i] - center) * 2;
            k = ptx[i].LengthSq() + (ptz[i] - center).LengthSq() - r2;
            a = ksin * ksin + kcos * kcos; b = ksin * k; c = k * k - kcos * kcos; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d); tsin = new QuotientF(-b - d, a);
                for (j = 0; j < 2; j++, tsin.X += d * 2)
                {
                    if ((MathUtils.IsNeg(MathF.Abs(tsin.X * 2 - tsin.Y) - tsin.Y) &
                         MathUtils.IsNeg((ksin * tsin.X + k * tsin.Y) * kcos)) != 0)
                    {
                        tcos = new QuotientF(MathF.Sqrt(tsin.Y * tsin.Y - tsin.X * tsin.X), tsin.Y);
                        pt = ptx[i] * tcos.X + pty[i] * tsin.X + ptz[i] * tsin.Y;
                        bBest = IsNegQ(tmax, tsin);
                        UpdateIdBest(ref idbest, ref tmax, bBest, i, tsin);
                    }
                }
            }
            if ((pray.Origin - pmode.Center).LengthSq() < pmode.MinPtDist * pmode.MinPtDist)
                break;
        }

        // ray - sphere
        lz = rotax * pray.Dir.Dot(rotax); lx = pray.Dir - lz; ly = rotax ^ lx;
        v = (pray.Origin - pmode.Center) ^ pray.Dir;
        vz = rotax * v.Dot(rotax); vx = v - vz; vy = rotax ^ vx;
        vcos = (lx ^ center) + vx; vsin = (ly ^ center) + vy; v = (lz ^ center) + vz;
        {
            var poly1 = MakePoly2(vsin.LengthSq() - vcos.LengthSq(), v.Dot(vsin) * 2,
                                  v.LengthSq() + vcos.LengthSq() - r2 * pray.Dir.LengthSq());
            var poly2 = MakePoly1(vcos.Dot(vsin), v.Dot(vcos));
            var oneMinusT2 = new Polynomial(2);
            oneMinusT2[0] = 1; oneMinusT2[1] = 0; oneMinusT2[2] = -1;
            var pn = PSub(PSqr(poly1), Polynomial.Multiply(PSqr(poly2), oneMinusT2) * 4);
            if (HasRoots(pn, 0, 1))
            {
                int nRoots = FindRoots(pn, 0, 1, roots);
                for (j = nRoots - 1; j >= 0; j--)
                {
                    tsin = new QuotientF(roots[j], 1); tcos.X = MathF.Sqrt(1 - tsin.X * tsin.X);
                    pt = ptz[0] + ptx[0] * tcos.X + pty[0] * tsin.X;
                    l = lz + lx * tcos.X + ly * tsin.X;
                    t = new QuotientF((center - pt).Dot(l), l.LengthSq());
                    bContact = InRange(((pt - center) * t.Y + l * t.X).LengthSq(),
                                       r2 * t.Y * 0.99f * (t.Y * 0.99f),
                                       r2 * t.Y * 1.01f * (t.Y * 1.01f));
                    bContact &= InRange(t.X, 0, t.Y);
                    bBest = IsNegQ(tmax, tsin) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, 0x40, tsin);
                }
            }
        }

        if (idbest < 0)
            return 0;

        switch (idbest & 0xF0)
        {
            case 0x00: // ray end - sphere
                i = idbest & 1;
                pcontact.T = tmax.Val();
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pcontact.Pt = ptz[i] + ptx[i] * (float)pcontact.TAux + pty[i] * (float)pcontact.T + pmode.Center;
                pcontact.Normal = (psph.Center - pcontact.Pt).Normalized();
                pcontact.IFeature0 = (uint)(0x80 | i);
                pcontact.IFeature1 = 0;
                break;
            case 0x40: // ray - sphere
                pcontact.T = tmax.X;
                pcontact.TAux = MathF.Sqrt(1 - (float)(pcontact.T * pcontact.T));
                pt = ptz[0] + ptx[0] * (float)pcontact.TAux + pty[0] * (float)pcontact.T;
                l = lz + lx * (float)pcontact.TAux + ly * (float)pcontact.T;
                t = new QuotientF((center - pt).Dot(l), l.LengthSq());
                pcontact.Pt = pt + l * t.Val();
                pcontact.Normal = (center - pcontact.Pt).Normalized();
                pcontact.Pt = pcontact.Pt + pmode.Center;
                pcontact.IFeature0 = 0x20;
                pcontact.IFeature1 = 0;
                break;
        }
        return 1;
    }

    // ================================================================
    //   sphere_ray_rot_unprojection (reverse wrapper)
    // ================================================================
    public static int SphereRayRotUnprojection(
        UnprojectionMode pmode, Primitive prim1, int iFeature1,
        Primitive prim2, int iFeature2, ref Contact pcontact, GeomContactArea? parea)
    {
        pmode.Dir = -pmode.Dir;
        int res = RaySphereRotUnprojection(pmode, prim2, iFeature2, prim1, iFeature1, ref pcontact, parea);
        if (res != 0)
        {
            float tVal = (float)pcontact.T;
            float tAux = (float)pcontact.TAux;
            pcontact.Pt = GetRotated(pcontact.Pt, pmode.Center, pmode.Dir, tAux, -tVal);
            pcontact.Normal = -GetRotated(pcontact.Normal, pmode.Dir, tAux, -tVal);
            uint feat = pcontact.IFeature0;
            pcontact.IFeature0 = pcontact.IFeature1;
            pcontact.IFeature1 = feat;
        }
        pmode.Dir = -pmode.Dir;
        return res;
    }
}
