// Port of CryPhysics linunprojectionchecks.cpp - linear unprojection functions
// Original: Copyright Crytek GMBH, used under license
// Computes minimum translation distance to separate two overlapping primitives.

using System;
using System.Runtime.CompilerServices;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Collision;

/// <summary>
/// All linear unprojection functions ported from CryEngine's linunprojectionchecks.cpp.
/// Each function computes the minimum translation along a given direction to separate
/// two overlapping primitives.
/// </summary>
public static class LinearUnprojection
{
    // Helper: UPDATE_IDBEST macro equivalent
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateIdBest(ref int idbest, ref QuotientF tbest, int bBest, in QuotientF t, int newid)
    {
        idbest = (idbest & ~(-bBest)) | (newid & (-bBest));
        tbest = new QuotientF(
            tbest.X * (bBest ^ 1) + t.X * bBest,
            tbest.Y * (bBest ^ 1) + t.Y * bBest);
    }

    // Helper to flip contact for swapped-argument functions
    private static void FlipContact(in PhysVector3 dir, ref Contact contact)
    {
        contact.Pt = contact.Pt - dir * (float)contact.T;
        contact.Normal = -contact.Normal;
        uint tmp = contact.IFeature0;
        contact.IFeature0 = contact.IFeature1;
        contact.IFeature1 = tmp;
    }

    //=========================================================================
    // DEFAULT UNPROJECTION
    //=========================================================================
    public static int DefaultUnprojection(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        return 0;
    }

    //=========================================================================
    // SPHERE - SPHERE
    //=========================================================================
    public static int SphereSphere(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var psph1 = (Sphere)prim1;
        var psph2 = (Sphere)prim2;

        PhysVector3 dc = psph1.Center - psph2.Center;
        float ka, kb, kc, kd, dclen, t;

        contact.IFeature0 = 0x40;
        contact.IFeature1 = 0x40;

        if (mode.Dir.LengthSq() == 0f)
        {
            // minimum unprojection dir requested
            dclen = dc.Length();
            mode.Dir = dclen > 0 ? dc / dclen : new PhysVector3(0, 0, 1);
            contact.T = psph1.Radius + psph2.Radius - dclen;
            contact.Normal = -mode.Dir;
            contact.Pt = psph2.Center - contact.Normal * psph2.Radius;
            return 1;
        }

        PhysVector3 vec1 = mode.Dir;
        PhysVector3 vec0 = dc;
        ka = vec1.Dot(vec1);
        kb = vec0.Dot(vec1);
        kc = vec0.Dot(vec0) - MathUtils.Sqr(psph1.Radius + psph2.Radius);
        kd = kb * kb - ka * kc;

        if ((MathUtils.IsNonNeg(kd) & (MathUtils.IsNeg(kb) | MathUtils.IsNeg(kb * kb - kd))) != 0)
        {
            t = (-kb + MathF.Sqrt(kd)) / ka;
            if (t > mode.TMin)
            {
                contact.T = t;
                contact.Normal = (psph2.Center - (psph1.Center + mode.Dir * (float)contact.T)).Normalized();
                contact.Pt = psph2.Center - contact.Normal * psph2.Radius;
                return 1;
            }
        }

        return 0;
    }

    //=========================================================================
    // TRI - SPHERE
    //=========================================================================
    public static int TriSphere(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var ptri = (Triangle)prim1;
        var psphere = (Sphere)prim2;

        var tmax = new QuotientF(mode.TMin, 1);
        float a, b, c, d, r2 = MathUtils.Sqr(psphere.Radius);
        int bBest, bContact, idbest = -1;
        QuotientF t, t0;

        // triangle face - sphere
        t = new QuotientF(
            (psphere.Center - ptri.P0).Dot(ptri.Normal) - psphere.Radius,
            mode.Dir.Dot(ptri.Normal)).FixSign();
        PhysVector3 center = psphere.Center * t.Y - mode.Dir * t.X;
        bContact = 1 ^ (MathUtils.IsNeg(((ptri.P1 - ptri.P0) ^ (center - ptri.P0 * t.Y)).Dot(ptri.Normal)) |
                        MathUtils.IsNeg(((ptri.P2 - ptri.P1) ^ (center - ptri.P1 * t.Y)).Dot(ptri.Normal)) |
                        MathUtils.IsNeg(((ptri.P0 - ptri.P2) ^ (center - ptri.P2 * t.Y)).Dot(ptri.Normal)));
        bBest = bContact & MathUtils.IsNeg((tmax - t).X);
        UpdateIdBest(ref idbest, ref tmax, bBest, t, 0);

        // triangle vertices - sphere
        for (int i = 0; i < 3; i++)
        {
            PhysVector3 vec0 = ptri[i] - psphere.Center;
            PhysVector3 vec1 = mode.Dir;
            a = vec1.Dot(vec1); b = vec0.Dot(vec1); c = vec0.Dot(vec0) - r2; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b - d, a);
                bBest = MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | i);
                t = new QuotientF(-b + d, a);
                bBest = MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | i);
            }
        }

        // triangle edges - sphere
        for (int i = 0; i < 3; i++)
        {
            PhysVector3 edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
            PhysVector3 dp = ptri[i] - psphere.Center;
            PhysVector3 vec0 = edge ^ dp;
            PhysVector3 vec1 = edge ^ mode.Dir;
            a = vec1.Dot(vec1); b = vec0.Dot(vec1); c = vec0.Dot(vec0) - r2 * edge.LengthSq(); d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b - d, a);
                t0 = new QuotientF(((psphere.Center - ptri[i]) * t.Y - mode.Dir * t.X).Dot(edge), t.Y * edge.LengthSq());
                bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | i);

                t = new QuotientF(t.X + d * 2, t.Y);
                t0 = new QuotientF(((psphere.Center - ptri[i]) * t.Y - mode.Dir * t.X).Dot(edge), t.Y * edge.LengthSq());
                bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | i);
            }
        }

        if (idbest == -1)
            return 0;

        switch (idbest & 0xC0)
        {
            case 0: // triangle face - sphere
                contact.T = tmax.Val();
                contact.Pt = psphere.Center - ptri.Normal * psphere.Radius;
                contact.Normal = ptri.Normal;
                contact.IFeature0 = 0x40;
                contact.IFeature1 = 0x40;
                break;

            case 0x40: // triangle vertex - sphere
            {
                int i = idbest & 3;
                contact.T = tmax.Val();
                contact.Pt = ptri[i] + mode.Dir * (float)contact.T;
                contact.Normal = psphere.Center - contact.Pt;
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = 0x40;
                break;
            }

            case 0x80: // triangle edge - sphere
            {
                int i = idbest & 3;
                PhysVector3 edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
                t0 = new QuotientF(
                    ((psphere.Center - ptri[i]) * tmax.Y - mode.Dir * tmax.X).Dot(edge),
                    tmax.Y * edge.LengthSq());
                float invT0Y = 1.0f / t0.Y;
                contact.T = tmax.X * invT0Y * edge.LengthSq();
                contact.Pt = ptri[i] + edge * (t0.X * invT0Y) + mode.Dir * (float)contact.T;
                contact.Normal = psphere.Center - contact.Pt;
                contact.IFeature0 = (uint)(0xA0 | i);
                contact.IFeature1 = 0x40;
                break;
            }
        }

        return 1;
    }

    //=========================================================================
    // SPHERE - TRI
    //=========================================================================
    public static int SphereTri(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = TriSphere(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // TRI - TRI
    //=========================================================================
    public static int TriTri(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var ptri1 = (Triangle)prim1;
        var ptri2 = (Triangle)prim2;
        Triangle[] ptri = { ptri1, ptri2 };

        PhysVector3 edge0, edge1, n, dp, dir;
        QuotientF t;
        var tmax = new QuotientF(0, 1);
        QuotientF t0, t1;
        int itri, i, j, bContact, bBest, idbest = -1, isg;
        float dist, mindist, sg;

        for (itri = 0; itri < 2; itri++)
        {
            // check for vertex-face contacts (itri provides face, itri^1 - vertex)
            isg = (itri << 1) - 1;
            sg = MathUtils.SgnNZ(ptri[itri].Normal.Dot(mode.Dir)) * isg;

            i = 0;
            mindist = ptri[itri ^ 1][0].Dot(ptri[itri].Normal) * sg;
            dist = ptri[itri ^ 1][1].Dot(ptri[itri].Normal) * sg;
            i += MathUtils.IsNeg(dist - mindist);
            mindist = MathF.Min(dist, mindist);
            bBest = MathUtils.IsNeg(ptri[itri ^ 1][2].Dot(ptri[itri].Normal) * sg - mindist);
            i = (i & (bBest ^ 1)) | (bBest << 1);

            t = new QuotientF(
                (ptri[itri][0] - ptri[itri ^ 1][i]).Dot(ptri[itri].Normal) * isg,
                mode.Dir.Dot(ptri[itri].Normal)).FixSign();
            dir = mode.Dir * isg;
            dp = ptri[itri ^ 1][i] * t.Y + dir * t.X;
            bContact = MathUtils.IsNeg(MathF.Max(MathF.Max(
                ((dp - ptri[itri][0] * t.Y) ^ (ptri[itri][1] - ptri[itri][0])).Dot(ptri[itri].Normal),
                ((dp - ptri[itri][1] * t.Y) ^ (ptri[itri][2] - ptri[itri][1])).Dot(ptri[itri].Normal)),
                ((dp - ptri[itri][2] * t.Y) ^ (ptri[itri][0] - ptri[itri][2])).Dot(ptri[itri].Normal)));
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, itri << 2 | i);
        }

        for (i = 0; i < 3; i++)
        {
            // check for edge-edge contacts
            edge0 = ptri1[MathUtils.IncMod3[i]] - ptri1[i];
            for (j = 0; j < 3; j++)
            {
                edge1 = ptri2[MathUtils.IncMod3[j]] - ptri2[j];
                dp = ptri2[j] - ptri1[i];
                n = edge0 ^ edge1;
                if (n.LengthSq() < 1E-8f * edge0.LengthSq() * edge1.LengthSq())
                    continue;
                t = new QuotientF(dp.Dot(n), mode.Dir.Dot(n)).FixSign();
                dp = dp * t.Y - mode.Dir * t.X;
                t0 = new QuotientF((dp ^ edge1).Dot(n), n.LengthSq() * t.Y);
                t1 = new QuotientF((dp ^ edge0).Dot(n), n.LengthSq() * t.Y);
                bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y)
                         & MathUtils.IsNeg(MathF.Abs(t1.X * 2 - t1.Y) - t1.Y);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, i << 2 | j | 0x80);
            }
        }

        if (idbest == -1 || MathF.Abs(tmax.Y) < 1E-20f)
            return 0;

        if ((idbest & 0x80) != 0)
        {
            i = (idbest >> 2) & 3;
            j = idbest & 3;
            edge0 = ptri1[MathUtils.IncMod3[i]] - ptri1[i];
            edge1 = ptri2[MathUtils.IncMod3[j]] - ptri2[j];
            dp = ptri2[j] - ptri1[i];
            n = edge0 ^ edge1;
            t0 = new QuotientF(((dp * tmax.Y - mode.Dir * tmax.X) ^ edge1).Dot(n), n.LengthSq() * tmax.Y);
            float invT0Y = 1.0f / t0.Y;
            contact.T = tmax.X * invT0Y * n.LengthSq();
            contact.Pt = ptri1[i] + edge0 * (t0.X * invT0Y) + mode.Dir * (float)contact.T;
            contact.Normal = n * MathUtils.SgnNZ(((ptri2.Normal ^ edge1)).Dot(n));
            contact.IFeature0 = (uint)(0xA0 | i);
            contact.IFeature1 = (uint)(0xA0 | j);
        }
        else
        {
            itri = idbest >> 2;
            i = idbest & 3;
            contact.T = tmax.Val();
            contact.Pt = ptri[itri ^ 1][i] + mode.Dir * ((float)contact.T * itri);
            contact.Normal = ptri[itri].Normal * (1 - (itri << 1));
            contact.IFeature0 = (uint)(itri == 0 ? 0x40 : (0x80 | i));
            contact.IFeature1 = (uint)(itri == 0 ? (0x80 | i) : 0x40);
            // Original: iFeature[itri] = 0x40, iFeature[itri^1] = 0x80|i
            if (itri == 0)
            {
                contact.IFeature0 = 0x40;
                contact.IFeature1 = (uint)(0x80 | i);
            }
            else
            {
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = 0x40;
            }
        }

        return 1;
    }

    //=========================================================================
    // TRI - BOX
    //=========================================================================
    public static int TriBox(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var ptri = (Triangle)prim1;
        var pbox = (Box)prim2;

        PhysVector3[] pt = new PhysVector3[3];
        PhysVector3 n, dir, edge0, ptbox, dp, ncontact, dp_x_edge1;
        int i, j, idir, idbest = -1, bContact, bBest, ix, iy, isgx, isgy, isg;
        QuotientF t;
        var tmax = new QuotientF(0, 1);
        QuotientF t0, t1;
        float dist, mindist;

        pt[0] = pbox.Basis * (ptri.P0 - pbox.Center);
        pt[1] = pbox.Basis * (ptri.P1 - pbox.Center);
        pt[2] = pbox.Basis * (ptri.P2 - pbox.Center);
        n = pbox.Basis * ptri.Normal;
        dir = pbox.Basis * mode.Dir;
        int sgdirX = MathUtils.SgnNZ(dir.X), sgdirY = MathUtils.SgnNZ(dir.Y), sgdirZ = MathUtils.SgnNZ(dir.Z);
        int sgnormX = MathUtils.SgnNZ(n.X), sgnormY = MathUtils.SgnNZ(n.Y), sgnormZ = MathUtils.SgnNZ(n.Z);

        // box vertex - triangle face
        ptbox = new PhysVector3(-pbox.Size.X * sgnormX, -pbox.Size.Y * sgnormY, -pbox.Size.Z * sgnormZ);
        t = new QuotientF((pt[0] - ptbox).Dot(n), -(dir.Dot(n))).FixSign();
        PhysVector3 ptboxScaled = ptbox * t.Y - dir * t.X;
        bContact = 1 ^ (MathUtils.IsNeg(((pt[1] - pt[0]) ^ (ptboxScaled - pt[0] * t.Y)).Dot(n))
                       | MathUtils.IsNeg(((pt[2] - pt[1]) ^ (ptboxScaled - pt[1] * t.Y)).Dot(n))
                       | MathUtils.IsNeg(((pt[0] - pt[2]) ^ (ptboxScaled - pt[2] * t.Y)).Dot(n)));
        bBest = bContact & MathUtils.IsNeg((tmax - t).X);
        UpdateIdBest(ref idbest, ref tmax, bBest, t, 0);

        // triangle vertex - box face
        int[] sgdir = { sgdirX, sgdirY, sgdirZ };
        for (j = 0; j < 3; j++)
        {
            i = 0;
            mindist = pt[0][j] * sgdir[j];
            dist = pt[1][j] * sgdir[j];
            i += MathUtils.IsNeg(dist - mindist);
            mindist = MathF.Min(dist, mindist);
            dist = pt[2][j] * sgdir[j];
            bBest = MathUtils.IsNeg(dist - mindist);
            i = (i & (bBest ^ 1)) | (bBest << 1);

            ix = MathUtils.IncMod3[j];
            iy = MathUtils.DecMod3[j];
            t = new QuotientF(pbox.Size[j] - pt[i][j] * sgdir[j], dir[j] * sgdir[j]);
            bContact = MathUtils.IsNeg(MathF.Abs(pt[i][ix] * t.Y + dir[ix] * t.X) - pbox.Size[ix] * t.Y)
                     & MathUtils.IsNeg(MathF.Abs(pt[i][iy] * t.Y + dir[iy] * t.X) - pbox.Size[iy] * t.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | (i << 2 | j));
        }

        // triangle edge - box edge
        for (i = 0; i < 3; i++)
        {
            edge0 = pt[MathUtils.IncMod3[i]] - pt[i];
            for (idir = 0; idir < 3; idir++)
            {
                ix = MathUtils.IncMod3[idir];
                iy = MathUtils.DecMod3[idir];
                ncontact = PhysVector3.Zero;
                ncontact[idir] = 0;
                ncontact[ix] = edge0[iy];
                ncontact[iy] = -edge0[ix];
                if (ncontact.LengthSq() < 1E-8f * edge0.LengthSq())
                    continue;
                t = new QuotientF(0, dir.Dot(ncontact));
                t.Y *= isg = MathUtils.SgnNZ(t.Y);
                isgx = MathUtils.SgnNZ(ncontact[ix]) * isg;
                isgy = MathUtils.SgnNZ(ncontact[iy]) * isg;
                ptbox = PhysVector3.Zero;
                ptbox[idir] = -pbox.Size[idir];
                ptbox[ix] = pbox.Size[ix] * isgx;
                ptbox[iy] = pbox.Size[iy] * isgy;
                dp = ptbox - pt[i];
                t.X = dp.Dot(ncontact) * isg;
                dp = dp * t.Y - dir * t.X;
                dp_x_edge1 = PhysVector3.Zero;
                dp_x_edge1[idir] = 0;
                dp_x_edge1[ix] = dp[iy];
                dp_x_edge1[iy] = -dp[ix];
                t0 = new QuotientF(dp_x_edge1.Dot(ncontact), ncontact.LengthSq() * t.Y);
                t1 = new QuotientF((dp ^ edge0).Dot(ncontact), t0.Y);
                bContact = t0.IsIn01()
                         & MathUtils.IsNeg(MathF.Abs(t1.X - t1.Y * pbox.Size[idir]) - t1.Y * pbox.Size[idir]);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | (i << 4 | idir << 2 | (isgy + 1) | ((isgx + 1) >> 1)));
            }
        }

        if (idbest == -1)
            return 0;

        switch (idbest & 0xC0)
        {
            case 0: // triangle face - box vertex
                ptbox = new PhysVector3(-pbox.Size.X * sgnormX, -pbox.Size.Y * sgnormY, -pbox.Size.Z * sgnormZ);
                contact.T = tmax.Val();
                contact.Pt = pbox.Center + pbox.Basis.Transposed() * ptbox;
                contact.Normal = ptri.Normal;
                contact.IFeature0 = 0x40;
                contact.IFeature1 = (uint)(((1 - sgnormZ) << 1) | (1 - sgnormY) | ((1 - sgnormX) >> 1));
                break;

            case 0x40: // triangle vertex - box face
                i = (idbest >> 2) & 3;
                j = idbest & 3;
                contact.T = tmax.Val();
                contact.Pt = ptri[i] + mode.Dir * (float)contact.T;
                contact.Normal = pbox.Basis.GetRow(j) * -sgdir[j];
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = (uint)(0x40 | (j << 1) | ((sgdir[j] + 1) >> 1));
                break;

            default: // triangle edge - box edge
                i = (idbest >> 4) & 3;
                idir = (idbest >> 2) & 3;
                isgy = (idbest & 2) - 1;
                isgx = ((idbest & 1) << 1) - 1;
                edge0 = pt[MathUtils.IncMod3[i]] - pt[i];
                ix = MathUtils.IncMod3[idir];
                iy = MathUtils.DecMod3[idir];
                ncontact = PhysVector3.Zero;
                ncontact[idir] = 0;
                ncontact[ix] = edge0[iy];
                ncontact[iy] = -edge0[ix];
                isg = MathUtils.SgnNZ(dir.Dot(ncontact));
                isgx = MathUtils.SgnNZ(ncontact[ix]) * isg;
                isgy = MathUtils.SgnNZ(ncontact[iy]) * isg;
                ptbox = PhysVector3.Zero;
                ptbox[idir] = -pbox.Size[idir];
                ptbox[ix] = pbox.Size[ix] * isgx;
                ptbox[iy] = pbox.Size[iy] * isgy;
                dp = (ptbox - pt[i]) * tmax.Y - dir * tmax.X;
                dp_x_edge1 = PhysVector3.Zero;
                dp_x_edge1[idir] = 0;
                dp_x_edge1[ix] = dp[iy];
                dp_x_edge1[iy] = -dp[ix];
                t0 = new QuotientF(dp_x_edge1.Dot(ncontact), ncontact.LengthSq() * tmax.Y);
                float invT0 = 1.0f / t0.Y;
                contact.T = tmax.X * invT0 * ncontact.LengthSq();
                contact.Pt = ptri[i] + (ptri[MathUtils.IncMod3[i]] - ptri[i]) * (t0.X * invT0) + mode.Dir * (float)contact.T;
                contact.Normal = pbox.Basis.Transposed() * ncontact * MathUtils.SgnNZ((pt[i] - pt[MathUtils.DecMod3[i]]).Dot(ncontact));
                contact.IFeature0 = (uint)(0xA0 | i);
                contact.IFeature1 = (uint)(0x20 | (idbest & 0xF));
                break;
        }

        return 1;
    }

    //=========================================================================
    // BOX - TRI
    //=========================================================================
    public static int BoxTri(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = TriBox(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // TRI - CYLINDER
    //=========================================================================
    public static int TriCylinder(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var ptri = (Triangle)prim1;
        var pcyl = (Cylinder)prim2;

        PhysVector3 n, center, dp, edge, vec0, vec1, ptcyl;
        QuotientF t, t0, t1;
        var tmax = new QuotientF(0, 1);
        float nlen = 1f, nlen2, a, b, c, d, r2 = MathUtils.Sqr(pcyl.Radius), dist, mindist;
        int i, j, idbest = -1, bContact, bBest, bCapped = MathUtils.IsZero(feature2 - 0x43) ^ 1;

        // triangle vertices - cylinder side
        for (i = 0; i < 3; i++)
        {
            dp = ptri[i] - pcyl.Center;
            vec0 = dp ^ pcyl.Axis;
            vec1 = mode.Dir ^ pcyl.Axis;
            a = vec1.Dot(vec1); b = vec1.Dot(vec0); c = vec0.Dot(vec0) - r2; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b + d, a);
                bContact = MathUtils.IsNeg(MathF.Abs((dp * t.Y + mode.Dir * t.X).Dot(pcyl.Axis)) - t.Y * pcyl.HalfHeight);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | i);
            }
        }

        // triangle edges - cylinder side
        for (i = 0; i < 3; i++)
        {
            edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
            dp = ptri[i] - pcyl.Center;
            n = edge ^ pcyl.Axis;
            nlen2 = n.LengthSq();
            c = dp.Dot(n); a = mode.Dir.Dot(n); b = a * c; a *= a;
            c = c * c - r2 * nlen2; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b + d, a);
                dp = (pcyl.Center - ptri[i]) * t.Y - mode.Dir * t.X;
                t0 = new QuotientF((dp ^ pcyl.Axis).Dot(n), nlen2 * t.Y);
                t1 = new QuotientF((dp ^ edge).Dot(n), nlen2 * t.Y);
                bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y)
                         & MathUtils.IsNeg(MathF.Abs(t1.X) - t1.Y * pcyl.HalfHeight);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x60 | i);
            }
        }

        if (bCapped != 0)
        {
            // cylinder cap edges - triangle face
            n = pcyl.Axis * pcyl.Axis.Dot(ptri.Normal) - ptri.Normal;
            nlen = n.Length();
            i = MathUtils.IsNeg(pcyl.Axis.Dot(ptri.Normal));
            center = pcyl.Center + pcyl.Axis * (pcyl.HalfHeight * ((i << 1) - 1));
            t = new QuotientF(
                ((center - ptri.P0) * nlen + n * pcyl.Radius).Dot(ptri.Normal),
                ptri.Normal.Dot(mode.Dir)).FixSign();
            ptcyl = center * (t.Y * nlen) + n * (pcyl.Radius * t.Y) - mode.Dir * t.X;
            t = new QuotientF(t.X, t.Y * nlen);
            bContact = 1 ^ (MathUtils.IsNeg(((ptri.P1 - ptri.P0) ^ (ptcyl - ptri.P0 * t.Y)).Dot(ptri.Normal))
                           | MathUtils.IsNeg(((ptri.P2 - ptri.P1) ^ (ptcyl - ptri.P1 * t.Y)).Dot(ptri.Normal))
                           | MathUtils.IsNeg(((ptri.P0 - ptri.P2) ^ (ptcyl - ptri.P2 * t.Y)).Dot(ptri.Normal)));
            bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, i);

            // triangle vertices - cylinder cap faces
            j = (MathUtils.IsNonNeg(pcyl.Axis.Dot(mode.Dir)) << 1) - 1;
            i = 0;
            mindist = ptri[0].Dot(pcyl.Axis) * j;
            dist = ptri[1].Dot(pcyl.Axis) * j;
            i += MathUtils.IsNeg(dist - mindist);
            mindist = MathF.Min(mindist, dist);
            dist = ptri[2].Dot(pcyl.Axis) * j;
            bBest = MathUtils.IsNeg(dist - mindist);
            i = (i & (bBest ^ 1)) | (bBest << 1);

            center = pcyl.Center + pcyl.Axis * (pcyl.HalfHeight * j);
            t = new QuotientF((center - ptri[i]).Dot(pcyl.Axis), mode.Dir.Dot(pcyl.Axis)).FixSign();
            bContact = MathUtils.IsNeg((ptri[i] * t.Y + mode.Dir * t.X - center * t.Y).LengthSq() - r2 * t.Y * t.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x20 | (i << 1 | ((j + 1) >> 1)));

            // triangle edges - cylinder cap edges
            for (i = 0; i < 3; i++)
            {
                edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
                for (j = -1; j <= 1; j += 2)
                {
                    center = pcyl.Center + pcyl.Axis * pcyl.HalfHeight * j;
                    dp = ptri[i] - center;
                    vec0 = pcyl.Axis ^ (dp ^ edge);
                    vec1 = pcyl.Axis ^ (mode.Dir ^ edge);
                    a = vec1.Dot(vec1); b = vec0.Dot(vec1);
                    c = vec0.Dot(vec0) - r2 * MathUtils.Sqr(edge.Dot(pcyl.Axis));
                    d = b * b - a * c;
                    if (d >= 0)
                    {
                        d = MathF.Sqrt(d);
                        t = new QuotientF(-b + d, a);
                        t0 = new QuotientF(-((dp * t.Y + mode.Dir * t.X).Dot(pcyl.Axis)), edge.Dot(pcyl.Axis) * t.Y);
                        bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - MathF.Abs(t0.Y));
                        bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
                        UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | (i << 1) | ((j + 1) >> 1));
                    }
                }
            }
        }

        if (idbest == -1 || MathF.Abs(tmax.Y) < 1E-20f)
            return 0;

        switch (idbest & 0xE0)
        {
            case 0: // triangle face - cylinder cap edge
                center = pcyl.Center + pcyl.Axis * (pcyl.HalfHeight * ((idbest << 1) - 1));
                n = pcyl.Axis * pcyl.Axis.Dot(ptri.Normal) - ptri.Normal;
                float invTYNlen = 1.0f / (tmax.Y * nlen);
                contact.T = tmax.X * nlen * invTYNlen;
                contact.Pt = center + n * (pcyl.Radius * invTYNlen * tmax.Y);
                contact.Normal = ptri.Normal;
                contact.IFeature0 = 0x40;
                contact.IFeature1 = (uint)(0x20 | idbest);
                break;

            case 0x20: // triangle vertex - cylinder cap face
                i = (idbest >> 1) & 3;
                j = idbest & 1;
                contact.T = tmax.Val();
                contact.Pt = ptri[i] + mode.Dir * (float)contact.T;
                contact.Normal = pcyl.Axis * (1 - (j << 1));
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = (uint)(0x40 | (j + 1));
                break;

            case 0x40: // triangle vertex - cylinder side
                i = idbest & 3;
                contact.T = tmax.Val();
                contact.Pt = ptri[i] + mode.Dir * (float)contact.T;
                contact.Normal = pcyl.Center - contact.Pt;
                contact.Normal = contact.Normal - pcyl.Axis * pcyl.Axis.Dot(contact.Normal);
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = 0x40;
                break;

            case 0x60: // triangle edge - cylinder side
                i = idbest & 3;
                edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
                dp = pcyl.Center - ptri[i];
                n = edge ^ pcyl.Axis;
                nlen2 = n.LengthSq();
                dp = dp * tmax.Y - mode.Dir * tmax.X;
                t0 = new QuotientF((dp ^ pcyl.Axis).Dot(n), nlen2 * tmax.Y);
                float invT0e = 1.0f / t0.Y;
                contact.T = tmax.X * invT0e * nlen2;
                contact.Pt = ptri[i] + edge * (t0.X * invT0e) + mode.Dir * (float)contact.T;
                contact.Normal = pcyl.Center - contact.Pt;
                contact.Normal = contact.Normal - pcyl.Axis * pcyl.Axis.Dot(contact.Normal);
                contact.IFeature0 = (uint)(0xA0 | i);
                contact.IFeature1 = 0x40;
                break;

            default: // triangle edge - cylinder cap edge
                i = (idbest >> 1) & 3;
                j = idbest & 1;
                center = pcyl.Center + pcyl.Axis * pcyl.HalfHeight * ((j << 1) - 1);
                edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
                dp = ptri[i] - center;
                t0 = new QuotientF(-((dp * tmax.Y + mode.Dir * tmax.X).Dot(pcyl.Axis)), edge.Dot(pcyl.Axis) * tmax.Y);
                float invT0c = 1.0f / t0.Y;
                contact.T = tmax.X * invT0c * edge.Dot(pcyl.Axis);
                contact.Pt = ptri[i] + edge * (t0.X * invT0c) + mode.Dir * (float)contact.T;
                contact.Normal = contact.Pt - pcyl.Center;
                contact.Normal = contact.Normal - pcyl.Axis * pcyl.Axis.Dot(contact.Normal);
                contact.Normal = edge ^ (pcyl.Axis ^ contact.Normal);
                contact.Normal = contact.Normal * MathUtils.SgnNZ((pcyl.Center - contact.Pt).Dot(contact.Normal));
                contact.IFeature0 = (uint)(0xA0 | i);
                contact.IFeature1 = (uint)(0x20 | j);
                break;
        }

        return 1;
    }

    //=========================================================================
    // CYLINDER - TRI
    //=========================================================================
    public static int CylinderTri(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = TriCylinder(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // TRI - CAPSULE
    //=========================================================================
    public static int TriCapsule(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var ptri = (Triangle)prim1;
        var pcaps = (Capsule)prim2;

        float tmin0 = mode.TMin;
        int bContact0, bContact1, bContact2;
        var sph = new Sphere();
        contact.T = 0;

        bContact0 = TriCylinder(mode, ptri, feature1, pcaps, 0x43, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact0;

        sph.Center = pcaps.Center - pcaps.Axis * pcaps.HalfHeight;
        sph.Radius = pcaps.Radius;
        bContact1 = TriSphere(mode, ptri, feature1, sph, -1, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact1;
        contact.IFeature1 += (uint)bContact1;

        sph.Center = pcaps.Center + pcaps.Axis * pcaps.HalfHeight;
        bContact2 = TriSphere(mode, ptri, feature1, sph, -1, ref contact, area);
        contact.IFeature1 += (uint)(bContact2 * 2);
        mode.TMin = tmin0;

        return bContact0 | bContact1 | bContact2;
    }

    //=========================================================================
    // CAPSULE - TRI
    //=========================================================================
    public static int CapsuleTri(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = TriCapsule(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // TRI - RAY
    //=========================================================================
    public static int TriRay(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var ptri = (Triangle)prim1;
        var pray = (Ray)prim2;

        PhysVector3 pt, ptnew, n, dp, edge;
        QuotientF t, t0, t1;
        var tmax = new QuotientF(0, 1);
        int i, bContact, bBest, idbest = -1;
        float nlen2;

        // triangle - ray ends
        pt = pray.Origin;
        for (i = 0; i < 2; i++, pt = pt + pray.Dir)
        {
            t = new QuotientF((pt - ptri.P0).Dot(ptri.Normal), mode.Dir.Dot(ptri.Normal));
            ptnew = pt * t.Y - mode.Dir * t.X;
            bContact = 1 ^ (MathUtils.IsNeg(((ptri.P1 - ptri.P0) ^ (ptnew - ptri.P0 * t.Y)).Dot(ptri.Normal))
                           | MathUtils.IsNeg(((ptri.P2 - ptri.P1) ^ (ptnew - ptri.P1 * t.Y)).Dot(ptri.Normal))
                           | MathUtils.IsNeg(((ptri.P0 - ptri.P2) ^ (ptnew - ptri.P2 * t.Y)).Dot(ptri.Normal)));
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, i);
        }

        // triangle edges - ray
        for (i = 0; i < 3; i++)
        {
            edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
            n = edge ^ pray.Dir;
            nlen2 = n.LengthSq();
            dp = pray.Origin - ptri[i];
            t = new QuotientF(n.Dot(dp), n.Dot(mode.Dir)).FixSign();
            dp = dp * t.Y - mode.Dir * t.X;
            t0 = new QuotientF((dp ^ pray.Dir).Dot(n), nlen2 * t.Y);
            t1 = new QuotientF((dp ^ edge).Dot(n), nlen2 * t.Y);
            bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y)
                     & MathUtils.IsNeg(MathF.Abs(t1.X * 2 - t1.Y) - t1.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | i);
        }

        if (idbest == -1)
            return 0;

        if ((idbest & 0x80) != 0)
        {
            // triangle edge - ray
            i = idbest & 3;
            edge = ptri[MathUtils.IncMod3[i]] - ptri[i];
            n = edge ^ pray.Dir;
            nlen2 = n.LengthSq();
            dp = (pray.Origin - ptri[i]) * tmax.Y - mode.Dir * tmax.X;
            t0 = new QuotientF((dp ^ pray.Dir).Dot(n), nlen2 * tmax.Y);
            float invT0 = 1.0f / t0.Y;
            contact.T = tmax.X * invT0 * nlen2;
            contact.Pt = ptri[i] + edge * (t0.X * invT0) + mode.Dir * (float)contact.T;
            contact.Normal = pray.Dir ^ edge;
            contact.Normal = contact.Normal * MathUtils.SgnNZ((ptri[i] - ptri[MathUtils.DecMod3[i]]).Dot(contact.Normal));
            contact.IFeature0 = (uint)(0xA0 | i);
            contact.IFeature1 = 0xA0;
        }
        else
        {
            // triangle face - ray end
            contact.T = tmax.Val();
            contact.Pt = pray.Origin + pray.Dir * idbest;
            contact.Normal = ptri.Normal;
            contact.IFeature0 = 0x40;
            contact.IFeature1 = (uint)(0x80 | idbest);
        }

        return 1;
    }

    //=========================================================================
    // RAY - TRI
    //=========================================================================
    public static int RayTri(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = TriRay(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // BOX - BOX (simplified - no area contacts for now)
    //=========================================================================
    public static int BoxBox(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pbox1 = (Box)prim1;
        var pbox2 = (Box)prim2;
        Box[] pbox = { pbox1, pbox2 };

        PhysVector3[] axes = new PhysVector3[3];
        PhysVector3[] centerArr = new PhysVector3[2];
        PhysVector3 origin, pt0, pt1, pt, dp, n;
        PhysVector3[] dirArr = new PhysVector3[2];
        PhysVector3[] sizeArr = { pbox1.Size, pbox2.Size };
        float nlen2;
        int i, iz, ibox, idir0, idir1, ix, iy, ix0, iy0, ix1, iy1, bFindMinUnproj;
        int idbest = -1;
        int icode, sg, bBest;
        QuotientF t;
        var tmin = new QuotientF(mode.TMax, 1);
        QuotientF t0, t1;

        centerArr[0] = pbox1.Basis * (pbox2.Center - pbox1.Center);
        centerArr[1] = pbox2.Basis * (pbox1.Center - pbox2.Center);
        dirArr[0] = pbox1.Basis * mode.Dir;
        dirArr[1] = pbox2.Basis * (-mode.Dir);

        // Compute axes = pbox2.Basis * pbox1.Basis.T() rows
        var basis21 = pbox2.Basis * pbox1.Basis.Transposed();
        axes[0] = basis21.GetRow(0);
        axes[1] = basis21.GetRow(1);
        axes[2] = basis21.GetRow(2);

        bFindMinUnproj = MathUtils.IsZero((int)(mode.Dir.LengthSq() < 1e-10f ? 0 : 1)) == 1 ? 0 : 0;
        // More correct check:
        bFindMinUnproj = mode.Dir.LengthSq() < 1e-10f ? 1 : 0;

        // face - closest vertex
        for (ibox = 0, icode = 0; ibox < 2; ibox++, icode += 8)
        {
            for (iz = 0; iz < 3; iz++, icode += 8)
            {
                if (bFindMinUnproj != 0)
                {
                    dirArr[ibox] = PhysVector3.Zero;
                    dirArr[ibox][iz] = -MathUtils.SgnNZ(centerArr[ibox][iz]);
                }
                ix = MathUtils.IncMod3[iz];
                iy = MathUtils.DecMod3[iz];
                int isg0z = -MathUtils.SgnNZ(dirArr[ibox][iz]);
                origin = PhysVector3.Zero;
                origin[iz] = sizeArr[ibox][iz] * isg0z;
                int isg1x = MathUtils.SgnNZ(axes[0][iz]);
                int isg1y = MathUtils.SgnNZ(axes[1][iz]);
                int isg1z = MathUtils.SgnNZ(axes[2][iz]);
                pt = centerArr[ibox] - (axes[0] * (sizeArr[ibox ^ 1].X * isg1x)
                                       + axes[1] * (sizeArr[ibox ^ 1].Y * isg1y)
                                       + axes[2] * (sizeArr[ibox ^ 1].Z * isg1z)) * isg0z;
                t = new QuotientF(pt[iz] - origin[iz], dirArr[ibox][iz]).FixSign();
                bBest = MathUtils.IsNeg(-t.X) & MathUtils.IsNeg((t - tmin).X);
                UpdateIdBest(ref idbest, ref tmin, bBest, t, icode + (isg1z + 1) * 2 + (isg1y + 1) + ((isg1x + 1) >> 1));
            }
            // TransposeBasis: swap axes for the other box
            var tmp0 = axes[0]; var tmp1 = axes[1]; var tmp2 = axes[2];
            axes[0] = new PhysVector3(tmp0.X, tmp1.X, tmp2.X);
            axes[1] = new PhysVector3(tmp0.Y, tmp1.Y, tmp2.Y);
            axes[2] = new PhysVector3(tmp0.Z, tmp1.Z, tmp2.Z);
        }

        // Recompute axes for edge-edge
        axes[0] = basis21.GetRow(0);
        axes[1] = basis21.GetRow(1);
        axes[2] = basis21.GetRow(2);

        // edge - edge
        for (idir1 = 0, icode = 256; idir1 < 3; idir1++, icode += 52)
        {
            for (idir0 = 0; idir0 < 3; idir0++, icode += 4)
            {
                ix0 = MathUtils.IncMod3[idir0]; iy0 = MathUtils.DecMod3[idir0];
                ix1 = MathUtils.IncMod3[idir1]; iy1 = MathUtils.DecMod3[idir1];
                n = PhysVector3.Zero;
                n[idir0] = 0;
                n[ix0] = -axes[idir1][iy0];
                n[iy0] = axes[idir1][ix0];
                nlen2 = n.LengthSq();
                if (nlen2 < 1E-8f)
                    continue;
                if (bFindMinUnproj != 0)
                    dirArr[0] = n * -MathUtils.SgnNZ(n.Dot(centerArr[0]));
                t = new QuotientF(0, dirArr[0].Dot(n));
                t.Y *= sg = MathUtils.SgnNZ(t.Y);
                int isg0x = -MathUtils.SgnNZ(n[ix0]) * sg;
                int isg0y = -MathUtils.SgnNZ(n[iy0]) * sg;
                int isg1x_e = MathUtils.SgnNZ(axes[ix1].Dot(n)) * sg;
                int isg1y_e = MathUtils.SgnNZ(axes[iy1].Dot(n)) * sg;
                pt0 = PhysVector3.Zero;
                pt0[idir0] = -sizeArr[0][idir0]; pt0[ix0] = sizeArr[0][ix0] * isg0x; pt0[iy0] = sizeArr[0][iy0] * isg0y;
                pt1 = centerArr[0] - axes[idir1] * sizeArr[1][idir1] + axes[ix1] * (sizeArr[1][ix1] * isg1x_e) + axes[iy1] * (sizeArr[1][iy1] * isg1y_e);
                dp = pt1 - pt0;
                t.X = dp.Dot(n) * sg;
                bBest = MathUtils.IsNeg(-t.X) & MathUtils.IsNeg((t - tmin).X);
                UpdateIdBest(ref idbest, ref tmin, bBest, t,
                    icode + (isg1y_e + 1 + ((isg1x_e + 1) >> 1)) * 16 + (isg0y + 1 + ((isg0x + 1) >> 1)));
            }
        }

        if (idbest == -1)
            return 0;

        // Recompute axes for result extraction
        axes[0] = basis21.GetRow(0);
        axes[1] = basis21.GetRow(1);
        axes[2] = basis21.GetRow(2);

        int bContact = 0;
        if ((idbest & 0x100) != 0)
        {
            // edge-edge
            idir0 = (idbest >> 2) & 3;
            int isg0y_e = (idbest & 2) - 1;
            int isg0x_e = ((idbest & 1) << 1) - 1;
            idir1 = (idbest >> 6) & 3;
            int isg1y_e = ((idbest >> 4) & 2) - 1;
            int isg1x_e = (((idbest >> 4) & 1) << 1) - 1;
            ix0 = MathUtils.IncMod3[idir0]; iy0 = MathUtils.DecMod3[idir0];
            ix1 = MathUtils.IncMod3[idir1]; iy1 = MathUtils.DecMod3[idir1];
            n = PhysVector3.Zero;
            n[idir0] = 0; n[ix0] = -axes[idir1][iy0]; n[iy0] = axes[idir1][ix0];
            nlen2 = n.LengthSq();
            if (bFindMinUnproj != 0)
            {
                dirArr[0] = n * -MathUtils.SgnNZ(n.Dot(centerArr[0]));
                mode.Dir = pbox[0].Basis.Transposed() * dirArr[0];
                dirArr[1] = pbox[1].Basis * (-mode.Dir);
            }
            pt0 = PhysVector3.Zero;
            pt0[idir0] = -sizeArr[0][idir0]; pt0[ix0] = sizeArr[0][ix0] * isg0x_e; pt0[iy0] = sizeArr[0][iy0] * isg0y_e;
            pt1 = centerArr[0] - axes[idir1] * sizeArr[1][idir1] + axes[ix1] * (sizeArr[1][ix1] * isg1x_e) + axes[iy1] * (sizeArr[1][iy1] * isg1y_e);
            dp = pt1 - pt0;
            dp = dp * tmin.Y - dirArr[0] * tmin.X;
            t0 = new QuotientF((dp ^ axes[idir1]).Dot(n), nlen2 * tmin.Y);
            float invT0 = 1.0f / t0.Y;
            contact.T = tmin.X * invT0 * nlen2;
            pt0[idir0] += t0.X * invT0;
            contact.Pt = pbox1.Center + pbox1.Basis.Transposed() * pt0 + mode.Dir * (float)contact.T;
            contact.Normal = (pbox1.Basis.Transposed() * n) * MathUtils.SgnNZ(n.Dot(centerArr[0]));
            contact.IFeature0 = (uint)(0x20 | (idir0 << 2) | (isg0y_e + 1) | ((isg0x_e + 1) >> 1));
            contact.IFeature1 = (uint)(0x20 | (idir1 << 2) | (isg1y_e + 1) | ((isg1x_e + 1) >> 1));
            bContact = 1; // simplified - skip full validation
        }
        else
        {
            // face-vertex
            ibox = idbest >> 5;
            iz = (idbest >> 3) & 3;
            if (bFindMinUnproj != 0)
            {
                dirArr[ibox] = PhysVector3.Zero;
                dirArr[ibox][iz] = -MathUtils.SgnNZ(centerArr[ibox][iz]);
                mode.Dir = (pbox[ibox].Basis.Transposed() * dirArr[ibox]) * (1 - (ibox << 1));
                dirArr[ibox ^ 1] = pbox[ibox ^ 1].Basis * (mode.Dir * ((ibox << 1) - 1));
            }
            int isg0z = -MathUtils.SgnNZ(dirArr[ibox][iz]);
            int isg1z_v = ((idbest >> 1) & 2) - 1;
            int isg1y_v = (idbest & 2) - 1;
            int isg1x_v = ((idbest << 1) & 2) - 1;
            contact.T = tmin.Val();
            PhysVector3 vtxPos = new PhysVector3(
                pbox[ibox ^ 1].Size.X * isg1x_v,
                pbox[ibox ^ 1].Size.Y * isg1y_v,
                pbox[ibox ^ 1].Size.Z * isg1z_v) * isg0z;
            contact.Pt = pbox[ibox ^ 1].Center + mode.Dir * ((float)contact.T * ibox)
                       - pbox[ibox ^ 1].Basis.Transposed() * vtxPos;
            contact.Normal = pbox[ibox].Basis.GetRow(iz) * (isg0z * (1 - (ibox << 1)));
            if (ibox == 0)
            {
                contact.IFeature0 = (uint)(0x40 | iz | ((isg0z + 1) >> 1));
                contact.IFeature1 = (uint)(((idbest ^ (isg0z >> 31)) ^ 7) & 7);
            }
            else
            {
                contact.IFeature1 = (uint)(0x40 | iz | ((isg0z + 1) >> 1));
                contact.IFeature0 = (uint)(((idbest ^ (isg0z >> 31)) ^ 7) & 7);
            }
            bContact = 1;
        }

        return bContact;
    }

    //=========================================================================
    // BOX - SPHERE
    //=========================================================================
    public static int BoxSphere(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pbox = (Box)prim1;
        var psph = (Sphere)prim2;

        PhysVector3 centerL, dir, pt, vec0, vec1;
        PhysVector3 size = pbox.Size;
        float r = psph.Radius, ka, kb, kc, kd;
        var tmax = new QuotientF(mode.TMin, 1);
        int i, ix, iy, iz, idbest = -1, bBest, bContact;
        QuotientF t;

        centerL = pbox.Basis * (psph.Center - pbox.Center);
        dir = pbox.Basis * mode.Dir;

        contact.IFeature1 = 0x40;

        // box face - sphere
        for (iz = 0; iz < 3; iz++)
        {
            int isgz = -MathUtils.SgnNZ(dir[iz]);
            ix = MathUtils.IncMod3[iz]; iy = MathUtils.DecMod3[iz];
            t = new QuotientF(centerL[iz] * -isgz + (size[iz] + r), MathF.Abs(dir[iz]));
            bContact = MathUtils.IsNeg(MathF.Abs(centerL[ix] * t.Y - dir[ix] * t.X) - size[ix] * t.Y)
                     & MathUtils.IsNeg(MathF.Abs(centerL[iy] * t.Y - dir[iy] * t.X) - size[iy] * t.Y);
            bBest = MathUtils.IsNeg((tmax - t).X) & bContact;
            UpdateIdBest(ref idbest, ref tmax, bBest, t, (iz << 1) | ((isgz + 1) >> 1));
        }

        // box edge - sphere
        for (iz = 0; iz < 3; iz++)
        {
            ix = MathUtils.IncMod3[iz]; iy = MathUtils.DecMod3[iz];
            for (i = 0; i < 4; i++)
            {
                int isgx = ((i << 1) & 2) - 1;
                int isgy = (i & 2) - 1;
                pt = PhysVector3.Zero;
                pt[ix] = size[ix] * isgx; pt[iy] = size[iy] * isgy; pt[iz] = 0;
                vec0 = MathUtils.CrossWithOrt(centerL - pt, iz);
                vec1 = MathUtils.CrossWithOrt(-dir, iz);
                ka = vec1.Dot(vec1); kb = vec0.Dot(vec1); kc = vec0.Dot(vec0) - r * r; kd = kb * kb - ka * kc;
                if ((MathUtils.IsNonNeg(kd) & (MathUtils.IsNeg(kb) | MathUtils.IsNeg(kb * kb - kd))) != 0)
                {
                    t = new QuotientF(-kb + MathF.Sqrt(kd), ka);
                    bContact = MathUtils.IsNeg(MathF.Abs(centerL[iz] * t.Y - dir[iz] * t.X) - size[iz] * t.Y);
                    bBest = MathUtils.IsNeg((tmax - t).X) & bContact;
                    UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x10 | (iz << 2) | (isgy + 1) | ((isgx + 1) >> 1));
                }
            }
        }

        // box vertex - sphere
        for (i = 0; i < 8; i++)
        {
            int isgx = ((i << 1) & 2) - 1;
            int isgy = (i & 2) - 1;
            int isgz = ((i >> 1) & 2) - 1;
            pt = new PhysVector3(size.X * isgx, size.Y * isgy, size.Z * isgz);
            vec0 = centerL - pt; vec1 = -dir;
            ka = vec1.Dot(vec1); kb = vec0.Dot(vec1); kc = vec0.Dot(vec0) - r * r; kd = kb * kb - ka * kc;
            if ((MathUtils.IsNonNeg(kd) & (MathUtils.IsNeg(kb) | MathUtils.IsNeg(kb * kb - kd))) != 0)
            {
                t = new QuotientF(-kb + MathF.Sqrt(kd), ka);
                bBest = MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x20 | (((isgz + 1) << 1) | (isgy + 1) | ((isgx + 1) >> 1)));
            }
        }

        if (idbest == -1)
            return 0;

        contact.T = tmax.Val();
        switch (idbest & 0xF0)
        {
            case 0x00: // box face - sphere
                iz = idbest >> 1;
                int isgzf = ((idbest << 1) & 2) - 1;
                contact.Normal = pbox.Basis.GetRow(iz) * isgzf;
                contact.IFeature0 = (uint)(0x40 | (iz << 1) | ((isgzf + 1) >> 1));
                break;
            case 0x10: // box edge - sphere
            {
                iz = (idbest >> 2) & 3;
                ix = MathUtils.IncMod3[iz]; iy = MathUtils.DecMod3[iz];
                int isgy2 = (idbest & 2) - 1;
                int isgx2 = ((idbest << 1) & 2) - 1;
                pt = PhysVector3.Zero;
                pt[ix] = size[ix] * isgx2; pt[iy] = size[iy] * isgy2;
                pt[iz] = centerL[iz] - dir[iz] * (float)contact.T;
                contact.Normal = pbox.Basis.Transposed() * (centerL - dir * (float)contact.T - pt).Normalized();
                contact.IFeature0 = (uint)(0x20 | (idbest & 0xF));
                break;
            }
            default: // box vertex - sphere
            {
                int isgzv = ((idbest >> 1) & 2) - 1;
                int isgyv = (idbest & 2) - 1;
                int isgxv = ((idbest << 1) & 2) - 1;
                pt = new PhysVector3(size.X * isgxv, size.Y * isgyv, size.Z * isgzv);
                contact.Normal = pbox.Basis.Transposed() * (centerL - dir * (float)contact.T - pt).Normalized();
                contact.IFeature0 = (uint)(idbest & 0xF);
                break;
            }
        }
        contact.Pt = psph.Center - contact.Normal * r;

        return 1;
    }

    //=========================================================================
    // SPHERE - BOX
    //=========================================================================
    public static int SphereBox(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = BoxSphere(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // CYLINDER - SPHERE
    //=========================================================================
    public static int CylinderSphere(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pcyl = (Cylinder)prim1;
        var psph = (Sphere)prim2;

        PhysVector3 axis, center, dir, vec0, vec1, c, pt2d;
        float rs = psph.Radius, rc = pcyl.Radius, hh = pcyl.HalfHeight;
        float ka, kb, kc, kd;
        int icap, bContact, bBest, idbest = -1, bCapped = MathUtils.IsZero(feature1 - 0x43) ^ 1;
        QuotientF t;
        var tmax = new QuotientF(mode.TMin, 1);
        axis = pcyl.Axis;
        center = psph.Center - pcyl.Center;
        dir = mode.Dir;

        contact.IFeature1 = 0x40;

        // sphere - cyl.side
        vec1 = -(dir ^ axis); // Note: in C++ it's -dir^axis, same as -(dir^axis)
        vec1 = (-dir) ^ axis;
        vec0 = center ^ axis;
        ka = vec1.Dot(vec1); kb = vec0.Dot(vec1); kc = vec0.Dot(vec0) - MathUtils.Sqr(rc + rs); kd = kb * kb - ka * kc;
        if ((MathUtils.IsNonNeg(kd) & (MathUtils.IsNeg(kb) | MathUtils.IsNeg(kb * kb - kd))) != 0)
        {
            t = new QuotientF(-kb + MathF.Sqrt(kd), ka);
            bContact = MathUtils.IsNeg(MathF.Abs((center * t.Y - dir * t.X).Dot(axis)) - hh)
                     & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
            bBest = MathUtils.IsNeg((tmax - t).X) & bContact;
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x10);
        }

        if (bCapped != 0)
        {
            // sphere - cyl.cap face
            icap = -MathUtils.SgnNZ(dir.Dot(axis));
            t = new QuotientF((rs + hh) - center.Dot(axis) * icap, MathF.Abs(dir.Dot(axis)));
            bContact = MathUtils.IsNeg((center * t.Y - dir * t.X ^ axis).LengthSq() - MathUtils.Sqr(rc * t.Y))
                     & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
            bBest = MathUtils.IsNeg((tmax - t).X) & bContact;
            UpdateIdBest(ref idbest, ref tmax, bBest, t, (icap + 1) >> 1);
        }

        if (idbest < 0)
            return 0;

        contact.T = tmax.Val();
        icap = ((idbest << 1) & 2) - 1;
        switch (idbest & 0xF0)
        {
            case 0x00: // sphere - cyl.cap face
                contact.Normal = pcyl.Axis * icap;
                contact.IFeature0 = (uint)(0x40 | (((icap + 1) >> 1) + 1));
                break;
            case 0x10: // sphere - cyl.side
                contact.Normal = center - dir * (float)contact.T;
                contact.Normal = contact.Normal - axis * axis.Dot(contact.Normal);
                contact.Normal.Normalize();
                contact.IFeature0 = 0x40;
                break;
            case 0x20: // sphere - cyl.cap edge
                c = center - axis * (hh * icap) - dir * (float)contact.T;
                pt2d = (c - axis * c.Dot(axis)).Normalized() * rc;
                contact.Normal = (c - pt2d).Normalized();
                contact.IFeature0 = (uint)(0x20 | ((icap + 1) >> 1));
                break;
        }
        contact.Pt = psph.Center - contact.Normal * psph.Radius;
        return 1;
    }

    //=========================================================================
    // SPHERE - CYLINDER
    //=========================================================================
    public static int SphereCylinder(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = CylinderSphere(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // BOX - CAPSULE
    //=========================================================================
    public static int BoxCapsule(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pbox = (Box)prim1;
        var pcaps = (Capsule)prim2;

        float tmin0 = mode.TMin;
        int bContact0, bContact1, bContact2;
        var sph = new Sphere();
        contact.T = 0;

        // Use the box-cylinder test with the capsule treated as uncapped cylinder
        bContact0 = BoxCylinder(mode, pbox, feature1, pcaps, 0x43, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact0;

        sph.Center = pcaps.Center - pcaps.Axis * pcaps.HalfHeight;
        sph.Radius = pcaps.Radius;
        bContact1 = BoxSphere(mode, pbox, feature1, sph, -1, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact1;
        contact.IFeature1 += (uint)bContact1;

        sph.Center = pcaps.Center + pcaps.Axis * pcaps.HalfHeight;
        bContact2 = BoxSphere(mode, pbox, feature1, sph, -1, ref contact, area);
        contact.IFeature1 += (uint)(bContact2 * 2);
        mode.TMin = tmin0;

        return bContact0 | bContact1 | bContact2;
    }

    //=========================================================================
    // CAPSULE - BOX
    //=========================================================================
    public static int CapsuleBox(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = BoxCapsule(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // BOX - CYLINDER (simplified - no area contacts)
    //=========================================================================
    public static int BoxCylinder(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pbox = (Box)prim1;
        var pcyl = (Cylinder)prim2;

        PhysVector3 centerL, axis, dir, size = pbox.Size, c, n, pt, vec0, vec1;
        float nlen, dlen, r = pcyl.Radius, hh = pcyl.HalfHeight, ka, kb, kc, kd, e = mode.MinPtDist;
        var tmin = new QuotientF(mode.TMax, 1);
        QuotientF t, t0, tmax2;
        int i, j, idbest = -1, bFindMinUnproj, bBest, bSeparated, icap, bContact, bCapped = MathUtils.IsZero(feature2 - 0x43) ^ 1;

        centerL = pbox.Basis * (pcyl.Center - pbox.Center);
        axis = pbox.Basis * pcyl.Axis;
        dir = pbox.Basis * mode.Dir;
        bFindMinUnproj = mode.Dir.LengthSq() < 1e-10f ? 1 : 0;
        kd = MathUtils.SgnNZ((centerL - axis * hh).LengthSq() - (centerL + axis * hh).LengthSq()) * 0.0005f;

        // box.face - cyl.capedge
        for (int icz = 0; icz < 3; icz++)
        {
            if (bFindMinUnproj != 0)
            {
                dir = PhysVector3.Zero;
                dir[icz] = -MathUtils.SgnNZ(centerL[icz]);
            }
            int isgz = -MathUtils.SgnNZ(dir[icz]);
            icap = MathUtils.SgnNZ(axis[icz] * -isgz + kd);
            float cz = centerL[icz] + axis[icz] * (hh * icap);
            n = axis * axis[icz];
            n[icz] -= 1;
            n = n * isgz;
            nlen = MathF.Max(0.001f, n.Length());
            t = new QuotientF(((cz - size[icz] * isgz) * nlen + n[icz] * r) * -isgz, MathF.Abs(dir[icz]) * nlen);
            bBest = MathUtils.IsNeg(-t.X) & MathUtils.IsNeg((t - tmin).X);
            UpdateIdBest(ref idbest, ref tmin, bBest, t, (icz << 2) | (isgz + 1) | ((icap + 1) >> 1));
        }

        if (bCapped != 0)
        {
            // box.vertex - cyl.cap
            if (bFindMinUnproj != 0)
                dir = axis * -MathUtils.SgnNZ(centerL.Dot(axis));
            icap = MathUtils.SgnNZ(dir.Dot(axis));
            t = new QuotientF(0, MathF.Abs(dir.Dot(axis)));
            float ka2 = axis.X * size.X + axis.Y * size.Y + axis.Z * size.Z;
            // Simplified: just use absolute values
            ka2 = MathF.Abs(axis.X) * size.X + MathF.Abs(axis.Y) * size.Y + MathF.Abs(axis.Z) * size.Z;
            t.X = centerL.Dot(axis) * icap + hh + ka2;
            bBest = MathUtils.IsNeg(-t.X) & MathUtils.IsNeg((t - tmin).X);
            UpdateIdBest(ref idbest, ref tmin, bBest, t, 0x20 | ((icap + 1) >> 1));
        }

        // box.vertex - cyl.side
        for (i = 0; i < 8; i++)
        {
            int isgx = ((i >> 1) & 2) - 1;
            int isgy = (i & 2) - 1;
            int isgz_v = ((i << 1) & 2) - 1;
            pt = new PhysVector3(size.X * isgx, size.Y * isgy, size.Z * isgz_v) - centerL;
            if (bFindMinUnproj != 0)
            {
                PhysVector3 ptPerp = pt - axis * pt.Dot(axis);
                float ptPerpLen = ptPerp.Length();
                if (ptPerpLen > 1e-8f)
                    dir = ptPerp / ptPerpLen;
                else
                    dir = PhysVector3.UnitX;
                dir = dir * -MathUtils.SgnNZ(dir.Dot(centerL));
            }
            vec0 = pt ^ axis; vec1 = dir ^ axis;
            ka = vec1.Dot(vec1); kb = vec0.Dot(vec1); kc = vec0.Dot(vec0) - r * r; kd = kb * kb - ka * kc;
            if ((MathUtils.IsNonNeg(kd) & (MathUtils.IsNeg(kb) | MathUtils.IsNeg(kb * kb - kd))) != 0)
            {
                t = new QuotientF(-kb + MathF.Sqrt(kd), ka);
                PhysVector3 ptContact = pt * t.Y + dir * t.X;
                ptContact = ptContact - axis * ptContact.Dot(axis);
                bSeparated = MathUtils.IsNeg(ptContact[0] * isgx - e)
                           & MathUtils.IsNeg(ptContact[1] * isgy - e)
                           & MathUtils.IsNeg(ptContact[2] * isgz_v - e);
                bBest = MathUtils.IsNeg((t - tmin).X) & bSeparated;
                UpdateIdBest(ref idbest, ref tmin, bBest, t, 0x40 | i);
            }
        }

        // box.edge - cyl.side
        for (int icz = 0; icz < 3; icz++)
        {
            int icx = MathUtils.IncMod3[icz], icy = MathUtils.DecMod3[icz];
            n = -MathUtils.CrossWithOrt(axis, icz);
            nlen = n.Length();
            if (bFindMinUnproj != 0)
            {
                dir = n * -MathUtils.SgnNZ(n.Dot(centerL));
                dlen = nlen;
            }
            else
                dlen = 1;
            int isgz_e = MathUtils.SgnNZ(dir.Dot(n));
            int isgx_e = -MathUtils.SgnNZ(n[icx]) * isgz_e;
            int isgy_e = -MathUtils.SgnNZ(n[icy]) * isgz_e;
            pt = PhysVector3.Zero;
            pt[icz] = 0; pt[icx] = size[icx] * isgx_e; pt[icy] = size[icy] * isgy_e;
            t = new QuotientF((r * nlen + ((centerL - pt).Dot(n)) * isgz_e) * dlen, MathF.Abs(dir.Dot(n)));
            bBest = MathUtils.IsNeg(-t.X) & MathUtils.IsNeg((t - tmin).X);
            UpdateIdBest(ref idbest, ref tmin, bBest, t,
                0x60 | (icz << 3) | ((isgz_e + 1) << 1) | (isgy_e + 1) | ((isgx_e + 1) >> 1));
        }

        if (idbest == -1)
            return 0;

        if (bFindMinUnproj != 0)
        {
            if (MathF.Abs(dir.LengthSq() - 1.0f) > 0.001f)
                dir = dir.Normalized();
            mode.Dir = pbox.Basis.Transposed() * dir;
        }

        bContact = 1; // simplified validation
        switch (idbest & 0xE0)
        {
            case 0x00: // box.face - cyl.capedge
            {
                int icz = idbest >> 2;
                int isgz2 = (idbest & 2) - 1;
                icap = ((idbest << 1) & 2) - 1;
                contact.T = tmin.Val();
                n = axis * axis[icz]; n[icz] -= 1; n = n * isgz2;
                nlen = n.Length();
                if (nlen < 0.001f)
                {
                    n = dir * (float)contact.T - centerL;
                    nlen = MathF.Max(1e-10f, (n - axis * n.Dot(axis)).Length());
                    n = n - axis * n.Dot(axis);
                }
                pt = centerL + axis * (hh * icap) + n * (r / nlen);
                contact.Pt = pbox.Basis.Transposed() * pt + pbox.Center;
                contact.Normal = pbox.Basis.GetRow(icz) * isgz2;
                contact.IFeature0 = (uint)(0x40 | (icz << 1) | ((isgz2 + 1) >> 1));
                contact.IFeature1 = (uint)(0x20 | ((icap + 1) >> 1));
                break;
            }
            case 0x20: // box.vertex - cyl.cap
            {
                icap = ((idbest << 1) & 2) - 1;
                int isgx3 = -MathUtils.SgnNZ(dir.X);
                int isgy3 = -MathUtils.SgnNZ(dir.Y);
                int isgz3 = -MathUtils.SgnNZ(dir.Z);
                contact.T = tmin.Val();
                pt = new PhysVector3(size.X * isgx3, size.Y * isgy3, size.Z * isgz3) + dir * (float)contact.T;
                contact.Pt = pbox.Basis.Transposed() * pt + pbox.Center;
                contact.Normal = pcyl.Axis * (hh * -icap);
                contact.IFeature0 = (uint)(((isgz3 + 1) << 1) | (isgy3 + 1) | ((isgx3 + 1) >> 1));
                contact.IFeature1 = (uint)(0x41 + ((icap + 1) >> 1));
                break;
            }
            case 0x40: // box.vertex - cyl.side
            {
                int isgx4 = ((idbest >> 1) & 2) - 1;
                int isgy4 = (idbest & 2) - 1;
                int isgz4 = ((idbest << 1) & 2) - 1;
                pt = new PhysVector3(size.X * isgx4, size.Y * isgy4, size.Z * isgz4);
                contact.T = tmin.Val();
                pt = pt + dir * (float)contact.T;
                contact.Pt = pbox.Basis.Transposed() * pt + pbox.Center;
                contact.Normal = pcyl.Center - contact.Pt;
                contact.Normal = contact.Normal - pcyl.Axis * contact.Normal.Dot(pcyl.Axis);
                contact.IFeature0 = (uint)(((isgz4 + 1) << 1) | (isgy4 + 1) | ((isgx4 + 1) >> 1));
                contact.IFeature1 = 0x40;
                break;
            }
            case 0x60: // box.edge - cyl.side
            {
                int icz2 = (idbest >> 3) & 3;
                int icx2 = MathUtils.IncMod3[icz2], icy2 = MathUtils.DecMod3[icz2];
                int isgx5 = ((idbest << 1) & 2) - 1;
                int isgy5 = (idbest & 2) - 1;
                int isgz5 = ((idbest >> 1) & 2) - 1;
                n = -MathUtils.CrossWithOrt(axis, icz2);
                pt = PhysVector3.Zero;
                pt[icz2] = 0; pt[icx2] = size[icx2] * isgx5; pt[icy2] = size[icy2] * isgy5;
                t0 = new QuotientF(((centerL - pt) * tmin.Y - dir * tmin.X ^ axis).Dot(n), n.LengthSq() * tmin.Y);
                float invT0_es = 1.0f / t0.Y;
                pt[icz2] = t0.X * invT0_es;
                contact.T = tmin.X * invT0_es * n.LengthSq();
                pt = pt + dir * (float)contact.T;
                contact.Pt = pbox.Basis.Transposed() * pt + pbox.Center;
                PhysVector3 nWorld = pcyl.Center - contact.Pt;
                contact.Normal = nWorld - pcyl.Axis * nWorld.Dot(pcyl.Axis);
                contact.IFeature0 = (uint)(0x20 | (icz2 << 2) | (isgy5 + 1) | ((isgx5 + 1) >> 1));
                contact.IFeature1 = 0x40;
                break;
            }
            default: // box.edge - cyl.capedge
            {
                int icz3 = (idbest >> 3) & 3;
                int icx3 = MathUtils.IncMod3[icz3], icy3 = MathUtils.DecMod3[icz3];
                icap = ((idbest >> 1) & 2) - 1;
                int isgy6 = (idbest & 2) - 1;
                int isgx6 = ((idbest << 1) & 2) - 1;
                pt = PhysVector3.Zero;
                pt[icz3] = 0; pt[icx3] = size[icx3] * isgx6; pt[icy3] = size[icy3] * isgy6;
                c = centerL + axis * (hh * icap);
                t0 = new QuotientF(((c - pt) * tmin.Y - dir * tmin.X).Dot(axis), axis[icz3] * tmin.Y);
                float invT0_ce = 1.0f / t0.Y;
                pt[icz3] += t0.X * invT0_ce;
                contact.T = tmin.X * invT0_ce * axis[icz3];
                PhysVector3 vec0_ce = pt;
                pt = pt + dir * (float)contact.T;
                contact.Pt = pbox.Basis.Transposed() * pt + pbox.Center;
                PhysVector3 nce = MathUtils.CrossWithOrt((pt - c) ^ axis, icz3);
                contact.Normal = pbox.Basis.Transposed() * (nce * MathUtils.SgnNZ(nce.Dot(vec0_ce)));
                contact.IFeature0 = (uint)(0x20 | (icz3 << 2) | (isgy6 + 1) | ((isgx6 + 1) >> 1));
                contact.IFeature1 = (uint)(0x20 | ((icap + 1) >> 1));
                break;
            }
        }

        return bContact;
    }

    //=========================================================================
    // CYLINDER - BOX
    //=========================================================================
    public static int CylinderBox(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = BoxCylinder(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // CYL - CYL (simplified - no area contacts, no polynomial root finder)
    //=========================================================================
    public static int CylCyl(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pcyl1 = (Cylinder)prim1;
        var pcyl2 = (Cylinder)prim2;

        PhysVector3 axisx, dc, dir, dp, n;
        var tmax = new QuotientF(0, 1);
        QuotientF t, t0, t1;
        float cosa, sina, a;
        int bContact, bBest, idbest = -1, bFindMinUnproj;

        int bCapped0 = MathUtils.IsZero(feature1 - 0x43) ^ 1;
        int bCapped1 = MathUtils.IsZero(feature2 - 0x43) ^ 1;

        bFindMinUnproj = mode.Dir.LengthSq() < 0.5f ? 1 : 0;
        if (bFindMinUnproj != 0)
            tmax.X = mode.TMax;

        cosa = pcyl1.Axis.Dot(pcyl2.Axis);
        axisx = pcyl1.Axis ^ pcyl2.Axis;
        sina = axisx.Length();
        dc = pcyl2.Center - pcyl1.Center;
        axisx = sina > 0.001f
            ? axisx * (1.0f / sina)
            : (dc - pcyl1.Axis * pcyl1.Axis.Dot(dc)).Normalized();

        // side - side
        dir = bFindMinUnproj != 0
            ? axisx * -MathUtils.SgnNZ(dc.Dot(axisx))
            : mode.Dir;
        t = new QuotientF(
            dc.Dot(axisx) * MathUtils.SgnNZ(dir.Dot(axisx)) - pcyl1.Radius - pcyl2.Radius,
            MathF.Abs(dir.Dot(axisx)));
        for (int ii = 0; ii < 2; ii++, t.X += (pcyl1.Radius + pcyl2.Radius) * 2)
        {
            dp = dc * t.Y - dir * t.X;
            t0 = new QuotientF((dp ^ pcyl2.Axis).Dot(axisx), t.Y);
            t1 = new QuotientF((dp ^ pcyl1.Axis).Dot(axisx), t.Y);
            bContact = MathUtils.IsNeg(MathF.Abs(t0.X) - t0.Y * pcyl1.HalfHeight * sina)
                     & MathUtils.IsNeg(MathF.Abs(t1.X) - t1.Y * pcyl2.HalfHeight * sina);
            if (bFindMinUnproj != 0)
            {
                bBest = MathUtils.IsNeg(-t.X) & MathUtils.IsNeg((t - tmax).X);
            }
            else
            {
                bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            }
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0xC0);
        }

        if (idbest == -1)
            return 0;

        if (bFindMinUnproj != 0)
            mode.Dir = dir.Normalized();

        // Output: side-side contact
        contact.T = tmax.Val();
        dp = pcyl2.Center - pcyl1.Center - mode.Dir * (float)contact.T;
        t1 = new QuotientF((dp ^ pcyl1.Axis).Dot(axisx), 1);
        contact.Pt = pcyl2.Center + pcyl2.Axis * t1.X;
        contact.Normal = axisx * MathUtils.SgnNZ(axisx.Dot(pcyl2.Center - pcyl1.Center));
        contact.Pt = contact.Pt - contact.Normal * pcyl2.Radius;
        contact.IFeature0 = 0x40;
        contact.IFeature1 = 0x40;

        return 1;
    }

    //=========================================================================
    // CYLINDER - CAPSULE
    //=========================================================================
    public static int CylinderCapsule(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pcyl = (Cylinder)prim1;
        var pcaps = (Capsule)prim2;

        float tmin0 = mode.TMin;
        int bContact0, bContact1, bContact2;
        var sph = new Sphere();
        contact.T = 0;

        bContact0 = CylCyl(mode, pcyl, feature1, pcaps, 0x43, ref contact, area);
        float tcyl = (float)contact.T;
        mode.TMin += ((float)contact.T - mode.TMin) * bContact0;

        sph.Center = pcaps.Center - pcaps.Axis * pcaps.HalfHeight;
        sph.Radius = pcaps.Radius;
        bContact1 = CylinderSphere(mode, pcyl, feature1, sph, -1, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact1;
        contact.IFeature1 += (uint)bContact1;

        sph.Center = pcaps.Center + pcaps.Axis * pcaps.HalfHeight;
        bContact2 = CylinderSphere(mode, pcyl, feature1, sph, -1, ref contact, area);
        contact.IFeature1 += (uint)(bContact2 * 2);
        mode.TMin = tmin0;

        return bContact0 | bContact1 | bContact2;
    }

    //=========================================================================
    // CAPSULE - CYLINDER
    //=========================================================================
    public static int CapsuleCylinder(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = CylinderCapsule(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // CAPSULE - CAPSULE
    //=========================================================================
    public static int CapsuleCapsule(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pcaps1 = (Capsule)prim1;
        var pcaps2 = (Capsule)prim2;
        Capsule[] pcaps = { pcaps1, pcaps2 };

        float tmin0 = mode.TMin;
        int bContact, bContactSph, icaps;
        var sph = new Sphere();
        var sph1 = new Sphere();
        contact.T = 0;

        bContact = CylCyl(mode, pcaps1, 0x43, pcaps2, 0x43, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact;

        // Test each capsule hemisphere against the other's cylinder and hemispheres
        for (mode.Dir = -mode.Dir, icaps = 0; icaps < 2; icaps++, mode.Dir = -mode.Dir)
        {
            for (int ii = -1; ii <= 1; ii += 2)
            {
                sph.Center = pcaps[icaps].Center + pcaps[icaps].Axis * (pcaps[icaps].HalfHeight * ii);
                sph.Radius = pcaps[icaps].Radius;
                bContact |= (bContactSph = CylinderSphere(mode, pcaps[icaps ^ 1], 0x43, sph, -1, ref contact, area));
                mode.TMin += ((float)contact.T - mode.TMin) * bContactSph;
                if ((bContactSph & (icaps ^ 1)) != 0)
                {
                    contact.Pt = contact.Pt - mode.Dir * (float)contact.T;
                    contact.Normal = -contact.Normal;
                }
                sph1.Center = pcaps[icaps ^ 1].Center + pcaps[icaps ^ 1].Axis * (pcaps[icaps ^ 1].HalfHeight * ii * (1 - icaps * 2));
                sph1.Radius = pcaps[icaps ^ 1].Radius;
                bContact |= (bContactSph = SphereSphere(mode, sph1, -1, sph, -1, ref contact, area));
                mode.TMin += ((float)contact.T - mode.TMin) * bContactSph;
                if ((bContactSph & (icaps ^ 1)) != 0)
                {
                    contact.Pt = contact.Pt - mode.Dir * (float)contact.T;
                    contact.Normal = -contact.Normal;
                }
            }
        }
        mode.Dir = -mode.Dir;
        mode.TMin = tmin0;

        return bContact;
    }

    //=========================================================================
    // SPHERE - CAPSULE
    //=========================================================================
    public static int SphereCapsule(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var psph = (Sphere)prim1;
        var pcaps = (Capsule)prim2;

        float tmin0 = mode.TMin;
        int bContact0, bContact1, bContact2;
        var sph = new Sphere();
        contact.T = 0;

        bContact0 = SphereCylinder(mode, psph, feature1, pcaps, 0x43, ref contact, area);
        float tcyl = (float)contact.T;
        mode.TMin += ((float)contact.T - mode.TMin) * bContact0;

        sph.Center = pcaps.Center - pcaps.Axis * pcaps.HalfHeight;
        sph.Radius = pcaps.Radius;
        bContact1 = SphereSphere(mode, psph, -1, sph, -1, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact1;
        contact.IFeature1 += (uint)bContact1;

        sph.Center = pcaps.Center + pcaps.Axis * pcaps.HalfHeight;
        bContact2 = SphereSphere(mode, psph, -1, sph, -1, ref contact, area);
        contact.IFeature1 += (uint)(bContact2 * 2);
        mode.TMin = tmin0;

        return bContact0 | bContact1 | bContact2;
    }

    //=========================================================================
    // CAPSULE - SPHERE
    //=========================================================================
    public static int CapsuleSphere(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = SphereCapsule(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // RAY - BOX
    //=========================================================================
    public static int RayBox(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pray = (Ray)prim1;
        var pbox = (Box)prim2;

        PhysVector3[] pt = new PhysVector3[2];
        PhysVector3 dir, edge0, ptbox, dp, ncontact, dp_x_edge1;
        int i = 0, j, idir, idbest = -1, bContact, bBest, ix, iy, isgx, isgy, isg;
        QuotientF t, t0, t1;
        var tmax = new QuotientF(0, 1);
        float dist, mindist;

        pt[0] = pbox.Basis * (pray.Origin - pbox.Center);
        pt[1] = pbox.Basis * (pray.Origin + pray.Dir - pbox.Center);
        dir = pbox.Basis * mode.Dir;
        int sgdirX = MathUtils.SgnNZ(dir.X), sgdirY = MathUtils.SgnNZ(dir.Y), sgdirZ = MathUtils.SgnNZ(dir.Z);
        int[] sgdir = { sgdirX, sgdirY, sgdirZ };

        // ray end - box face
        for (j = 0; j < 3; j++)
        {
            i = 0;
            mindist = pt[0][j] * sgdir[j];
            dist = pt[1][j] * sgdir[j];
            i += MathUtils.IsNeg(dist - mindist);
            mindist = MathF.Min(dist, mindist);

            ix = MathUtils.IncMod3[j]; iy = MathUtils.DecMod3[j];
            t = new QuotientF(pbox.Size[j] - pt[i][j] * sgdir[j], dir[j] * sgdir[j]);
            bContact = MathUtils.IsNeg(MathF.Abs(pt[i][ix] * t.Y + dir[ix] * t.X) - pbox.Size[ix] * t.Y)
                     & MathUtils.IsNeg(MathF.Abs(pt[i][iy] * t.Y + dir[iy] * t.X) - pbox.Size[iy] * t.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | (i << 2 | j));
        }

        // ray - box edge
        edge0 = pt[1] - pt[0];
        for (idir = 0; idir < 3; idir++)
        {
            ix = MathUtils.IncMod3[idir]; iy = MathUtils.DecMod3[idir];
            ncontact = PhysVector3.Zero;
            ncontact[idir] = 0;
            ncontact[ix] = edge0[iy];
            ncontact[iy] = -edge0[ix];
            if (ncontact.LengthSq() > 1E-8f * edge0.LengthSq())
            {
                t = new QuotientF(0, dir.Dot(ncontact));
                t.Y *= isg = MathUtils.SgnNZ(t.Y);
                isgx = MathUtils.SgnNZ(ncontact[ix]) * isg;
                isgy = MathUtils.SgnNZ(ncontact[iy]) * isg;
                ptbox = PhysVector3.Zero;
                ptbox[idir] = -pbox.Size[idir]; ptbox[ix] = pbox.Size[ix] * isgx; ptbox[iy] = pbox.Size[iy] * isgy;
                dp = ptbox - pt[0];
                t.X = dp.Dot(ncontact) * isg;
                dp = dp * t.Y - dir * t.X;
                dp_x_edge1 = PhysVector3.Zero;
                dp_x_edge1[idir] = 0; dp_x_edge1[ix] = dp[iy]; dp_x_edge1[iy] = -dp[ix];
                t0 = new QuotientF(dp_x_edge1.Dot(ncontact), ncontact.LengthSq() * t.Y);
                t1 = new QuotientF((dp ^ edge0).Dot(ncontact), t0.Y);
                bContact = t0.IsIn01()
                         & MathUtils.IsNeg(MathF.Abs(t1.X - t1.Y * pbox.Size[idir]) - t1.Y * pbox.Size[idir]);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | (idir << 2 | (isgy + 1) | ((isgx + 1) >> 1)));
            }
        }

        if (idbest == -1)
            return 0;

        switch (idbest & 0xC0)
        {
            case 0x40: // ray end - box face
                i = (idbest >> 2) & 3;
                j = idbest & 3;
                contact.T = tmax.Val();
                contact.Pt = pray.Origin + pray.Dir * i + mode.Dir * (float)contact.T;
                contact.Normal = pbox.Basis.GetRow(j) * -sgdir[j];
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = (uint)(0x40 | (j << 1) | ((sgdir[j] + 1) >> 1));
                break;

            default: // ray - box edge
                idir = (idbest >> 2) & 3;
                isgy = (idbest & 2) - 1;
                isgx = ((idbest & 1) << 1) - 1;
                edge0 = pt[1] - pt[0];
                ix = MathUtils.IncMod3[idir]; iy = MathUtils.DecMod3[idir];
                ncontact = PhysVector3.Zero;
                ncontact[idir] = 0; ncontact[ix] = edge0[iy]; ncontact[iy] = -edge0[ix];
                isg = MathUtils.SgnNZ(dir.Dot(ncontact));
                isgx = MathUtils.SgnNZ(ncontact[ix]) * isg;
                isgy = MathUtils.SgnNZ(ncontact[iy]) * isg;
                ptbox = PhysVector3.Zero;
                ptbox[idir] = -pbox.Size[idir]; ptbox[ix] = pbox.Size[ix] * isgx; ptbox[iy] = pbox.Size[iy] * isgy;
                dp = (ptbox - pt[0]) * tmax.Y - dir * tmax.X;
                dp_x_edge1 = PhysVector3.Zero;
                dp_x_edge1[idir] = 0; dp_x_edge1[ix] = dp[iy]; dp_x_edge1[iy] = -dp[ix];
                t0 = new QuotientF(dp_x_edge1.Dot(ncontact), ncontact.LengthSq() * tmax.Y);
                float invT0 = 1.0f / t0.Y;
                contact.T = tmax.X * invT0 * ncontact.LengthSq();
                contact.Pt = pray.Origin + pray.Dir * (t0.X * invT0) + mode.Dir * (float)contact.T;
                contact.Normal = pbox.Basis.Transposed() * ncontact;
                contact.Normal = contact.Normal * MathUtils.SgnNZ((pbox.Center - contact.Pt).Dot(contact.Normal));
                contact.IFeature0 = (uint)(0xA0 | i);
                contact.IFeature1 = (uint)(0x20 | (idbest & 0xF));
                break;
        }

        return 1;
    }

    //=========================================================================
    // BOX - RAY
    //=========================================================================
    public static int BoxRay(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = RayBox(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // RAY - CYLINDER
    //=========================================================================
    public static int RayCylinder(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pray = (Ray)prim1;
        var pcyl = (Cylinder)prim2;

        PhysVector3 n, center, dp, edge, vec0, vec1;
        QuotientF t, t0, t1;
        var tmax = new QuotientF(0, 1);
        float nlen2, a, b, c, d, r2 = MathUtils.Sqr(pcyl.Radius), dist, mindist;
        int i, j, idbest = -1, bContact, bBest, bCapped = MathUtils.IsZero(feature2 - 0x43) ^ 1;

        // ray ends - cylinder side
        for (i = 0; i < 2; i++)
        {
            dp = pray.Origin + pray.Dir * i - pcyl.Center;
            vec0 = dp ^ pcyl.Axis; vec1 = mode.Dir ^ pcyl.Axis;
            a = vec1.Dot(vec1); b = vec1.Dot(vec0); c = vec0.Dot(vec0) - r2; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b + d, a);
                bContact = MathUtils.IsNeg(MathF.Abs((dp * t.Y + mode.Dir * t.X).Dot(pcyl.Axis)) - t.Y * pcyl.HalfHeight);
                bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | i);
            }
        }

        // ray - cylinder side
        edge = pray.Dir;
        dp = pray.Origin - pcyl.Center;
        n = edge ^ pcyl.Axis;
        nlen2 = n.LengthSq();
        c = dp.Dot(n); a = mode.Dir.Dot(n); b = a * c; a *= a;
        c = c * c - r2 * nlen2; d = b * b - a * c;
        if (d >= 0)
        {
            d = MathF.Sqrt(d);
            t = new QuotientF(-b + d, a);
            dp = (pcyl.Center - pray.Origin) * t.Y - mode.Dir * t.X;
            t0 = new QuotientF((dp ^ pcyl.Axis).Dot(n), nlen2 * t.Y);
            t1 = new QuotientF((dp ^ edge).Dot(n), nlen2 * t.Y);
            bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y)
                     & MathUtils.IsNeg(MathF.Abs(t1.X) - t1.Y * pcyl.HalfHeight);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x60);
        }

        if (bCapped != 0)
        {
            // ray ends - cylinder cap faces
            j = (MathUtils.IsNonNeg(pcyl.Axis.Dot(mode.Dir)) << 1) - 1;
            i = 0;
            mindist = pray.Origin.Dot(pcyl.Axis) * j;
            dist = (pray.Origin + pray.Dir).Dot(pcyl.Axis) * j;
            i += MathUtils.IsNeg(dist - mindist);
            mindist = MathF.Min(mindist, dist);

            center = pcyl.Center + pcyl.Axis * (pcyl.HalfHeight * j);
            t = new QuotientF((center - pray.Origin - pray.Dir * i).Dot(pcyl.Axis), mode.Dir.Dot(pcyl.Axis)).FixSign();
            bContact = MathUtils.IsNeg(((pray.Origin + pray.Dir * i) * t.Y + mode.Dir * t.X - center * t.Y).LengthSq() - r2 * t.Y * t.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x20 | (i << 1 | ((j + 1) >> 1)));

            // ray - cylinder cap edges
            for (j = -1; j <= 1; j += 2)
            {
                center = pcyl.Center + pcyl.Axis * pcyl.HalfHeight * j;
                dp = pray.Origin - center;
                vec0 = pcyl.Axis ^ (dp ^ edge); vec1 = pcyl.Axis ^ (mode.Dir ^ edge);
                a = vec1.Dot(vec1); b = vec0.Dot(vec1);
                c = vec0.Dot(vec0) - r2 * MathUtils.Sqr(edge.Dot(pcyl.Axis));
                d = b * b - a * c;
                if (d >= 0)
                {
                    d = MathF.Sqrt(d);
                    t = new QuotientF(-b + d, a);
                    t0 = new QuotientF(-((dp * t.Y + mode.Dir * t.X).Dot(pcyl.Axis)), edge.Dot(pcyl.Axis) * t.Y);
                    bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - MathF.Abs(t0.Y));
                    bBest = bContact & MathUtils.IsNeg((tmax - t).X) & MathUtils.IsNeg(t.X - mode.TMax * t.Y);
                    UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | ((j + 1) >> 1));
                }
            }
        }

        if (idbest == -1 || MathF.Abs(tmax.Y) < 1E-20f)
            return 0;

        edge = pray.Dir; // restore edge
        switch (idbest & 0xE0)
        {
            case 0x20: // ray end - cylinder cap face
                i = (idbest >> 1) & 3;
                j = idbest & 1;
                contact.T = tmax.Val();
                contact.Pt = pray.Origin + pray.Dir * i + mode.Dir * (float)contact.T;
                contact.Normal = pcyl.Axis * (1 - (j << 1));
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = (uint)(0x40 | (j + 1));
                break;

            case 0x40: // ray end - cylinder side
                i = idbest & 3;
                contact.T = tmax.Val();
                contact.Pt = pray.Origin + pray.Dir * i + mode.Dir * (float)contact.T;
                contact.Normal = pcyl.Center - contact.Pt;
                contact.Normal = contact.Normal - pcyl.Axis * pcyl.Axis.Dot(contact.Normal);
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = 0x40;
                break;

            case 0x60: // ray - cylinder side
                dp = pcyl.Center - pray.Origin;
                n = edge ^ pcyl.Axis;
                nlen2 = n.LengthSq();
                dp = dp * tmax.Y - mode.Dir * tmax.X;
                t0 = new QuotientF((dp ^ pcyl.Axis).Dot(n), nlen2 * tmax.Y);
                float invT0 = 1.0f / t0.Y;
                contact.T = tmax.X * invT0 * nlen2;
                contact.Pt = pray.Origin + edge * (t0.X * invT0) + mode.Dir * (float)contact.T;
                contact.Normal = pcyl.Center - contact.Pt;
                contact.Normal = contact.Normal - pcyl.Axis * pcyl.Axis.Dot(contact.Normal);
                contact.IFeature0 = 0xA0;
                contact.IFeature1 = 0x40;
                break;

            default: // ray - cylinder cap edge
                j = idbest & 1;
                center = pcyl.Center + pcyl.Axis * pcyl.HalfHeight * ((j << 1) - 1);
                dp = pray.Origin - center;
                t0 = new QuotientF(-((dp * tmax.Y + mode.Dir * tmax.X).Dot(pcyl.Axis)), edge.Dot(pcyl.Axis) * tmax.Y);
                float invT0c = 1.0f / t0.Y;
                contact.T = tmax.X * invT0c * edge.Dot(pcyl.Axis);
                contact.Pt = pray.Origin + edge * (t0.X * invT0c) + mode.Dir * (float)contact.T;
                contact.Normal = contact.Pt - pcyl.Center;
                contact.Normal = contact.Normal - pcyl.Axis * pcyl.Axis.Dot(contact.Normal);
                contact.Normal = edge ^ (pcyl.Axis ^ contact.Normal);
                contact.Normal = contact.Normal * MathUtils.SgnNZ((pcyl.Center - contact.Pt).Dot(contact.Normal));
                contact.IFeature0 = 0xA0;
                contact.IFeature1 = (uint)(0x20 | j);
                break;
        }

        return 1;
    }

    //=========================================================================
    // CYLINDER - RAY
    //=========================================================================
    public static int CylinderRay(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = RayCylinder(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // RAY - SPHERE
    //=========================================================================
    public static int RaySphere(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pray = (Ray)prim1;
        var psphere = (Sphere)prim2;

        var tmax = new QuotientF(mode.TMin, 1);
        PhysVector3 edge, vec0, vec1, dp;
        QuotientF t, t0;
        float a, b, c, d, r2 = MathUtils.Sqr(psphere.Radius);
        int i = 0, bBest, bContact, idbest = -1;

        // ray ends - sphere
        for (i = 0; i < 2; i++)
        {
            vec0 = pray.Origin + pray.Dir * i - psphere.Center;
            vec1 = mode.Dir;
            a = vec1.Dot(vec1); b = vec0.Dot(vec1); c = vec0.Dot(vec0) - r2; d = b * b - a * c;
            if (d >= 0)
            {
                d = MathF.Sqrt(d);
                t = new QuotientF(-b - d, a);
                bBest = MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | i);
                t = new QuotientF(-b + d, a);
                bBest = MathUtils.IsNeg((tmax - t).X);
                UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x40 | i);
            }
        }

        // ray - sphere
        edge = pray.Dir;
        dp = pray.Origin - psphere.Center;
        vec0 = edge ^ dp; vec1 = edge ^ mode.Dir;
        a = vec1.Dot(vec1); b = vec0.Dot(vec1); c = vec0.Dot(vec0) - r2 * edge.LengthSq(); d = b * b - a * c;
        if (d >= 0)
        {
            d = MathF.Sqrt(d);
            t = new QuotientF(-b - d, a);
            t0 = new QuotientF(((psphere.Center - pray.Origin) * t.Y - mode.Dir * t.X).Dot(edge), t.Y * edge.LengthSq());
            bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | i);
            t = new QuotientF(t.X + d * 2, t.Y);
            t0 = new QuotientF(((psphere.Center - pray.Origin) * t.Y - mode.Dir * t.X).Dot(edge), t.Y * edge.LengthSq());
            bContact = MathUtils.IsNeg(MathF.Abs(t0.X * 2 - t0.Y) - t0.Y);
            bBest = bContact & MathUtils.IsNeg((tmax - t).X);
            UpdateIdBest(ref idbest, ref tmax, bBest, t, 0x80 | i);
        }

        if (idbest == -1)
            return 0;

        switch (idbest & 0xC0)
        {
            case 0x40:
                i = idbest & 3;
                contact.T = tmax.Val();
                contact.Pt = pray.Origin + pray.Dir * i + mode.Dir * (float)contact.T;
                contact.Normal = psphere.Center - contact.Pt;
                contact.IFeature0 = (uint)(0x80 | i);
                contact.IFeature1 = 0x40;
                break;

            case 0x80:
                edge = pray.Dir;
                t0 = new QuotientF(
                    ((psphere.Center - pray.Origin) * tmax.Y - mode.Dir * tmax.X).Dot(edge),
                    tmax.Y * edge.LengthSq());
                float invT0 = 1.0f / t0.Y;
                contact.T = tmax.X * invT0 * edge.LengthSq();
                contact.Pt = pray.Origin + edge * (t0.X * invT0) + mode.Dir * (float)contact.T;
                contact.Normal = psphere.Center - contact.Pt;
                contact.IFeature0 = (uint)(0xA0 | i);
                contact.IFeature1 = 0x40;
                break;
        }

        return 1;
    }

    //=========================================================================
    // SPHERE - RAY
    //=========================================================================
    public static int SphereRay(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = RaySphere(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }

    //=========================================================================
    // RAY - CAPSULE
    //=========================================================================
    public static int RayCapsule(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        var pray = (Ray)prim1;
        var pcaps = (Capsule)prim2;

        float tmin0 = mode.TMin;
        int bContact0, bContact1, bContact2;
        var sph = new Sphere();
        contact.T = 0;

        bContact0 = RayCylinder(mode, pray, feature1, pcaps, 0x43, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact0;

        sph.Center = pcaps.Center - pcaps.Axis * pcaps.HalfHeight;
        sph.Radius = pcaps.Radius;
        bContact1 = RaySphere(mode, pray, feature1, sph, -1, ref contact, area);
        mode.TMin += ((float)contact.T - mode.TMin) * bContact1;
        contact.IFeature1 += (uint)bContact1;

        sph.Center = pcaps.Center + pcaps.Axis * pcaps.HalfHeight;
        bContact2 = RaySphere(mode, pray, feature1, sph, -1, ref contact, area);
        contact.IFeature1 += (uint)(bContact2 * 2);
        mode.TMin = tmin0;

        return bContact0 | bContact1 | bContact2;
    }

    //=========================================================================
    // CAPSULE - RAY
    //=========================================================================
    public static int CapsuleRay(
        UnprojectionMode mode, Primitive prim1, int feature1,
        Primitive prim2, int feature2, ref Contact contact, GeomContactArea? area)
    {
        mode.Dir = -mode.Dir;
        int res = RayCapsule(mode, prim2, feature2, prim1, feature1, ref contact, area);
        if (res != 0)
            FlipContact(mode.Dir, ref contact);
        mode.Dir = -mode.Dir;
        return res;
    }
}
