// Literal port of dev/Code/CryEngine/CryPhysics/boolean2d.cpp.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Math;

namespace CryPhysics.Algorithms;

/// <summary>
/// 2D boolean operation type. Port of `enum booltype` (boolean2d.cpp implicit).
/// Values match C++: BOP_INTERSECTION=0, BOP_UNION=1.
/// </summary>
public enum BoolType
{
    Intersection = 0,
    Union = 1,
}

/// <summary>
/// 2D polygon boolean operations. Literal port of boolean2d.cpp:18-293.
/// The C++ uses per-thread scratch arrays (g_BoolPtBufThread, g_BoolGridThread,
/// g_BoolHashThread, g_BoolIntersThread). We allocate per-call to keep the code
/// thread-safe without porting the per-iCaller pool.
/// </summary>
public static class Boolean2D
{
    private const int BufSize = 4096;
    private const int IntersBufSize = 256;
    private const int HashBufSize = 8192;

    /// <summary>Helper struct mirroring `struct inters2d` (boolean2d.cpp:22-25).</summary>
    private struct Inters2D
    {
        public PhysVector2 Pt;
        public int IEdge0, IEdge1;
    }

    /// <summary>
    /// Line-segment intersection. Literal port of `line_seg_inters` (boolean2d.cpp:30-57).
    /// Returns 0 (no intersection), 1 (point), or 2 (overlapping collinear segments).
    /// </summary>
    public static int LineSegInters(PhysVector2[] seg0, int seg0Off, PhysVector2[] seg1, int seg1Off, PhysVector2[] ptres)
    {
        var dp0 = seg0[seg0Off + 1] - seg0[seg0Off + 0];
        var dp1 = seg1[seg1Off + 1] - seg1[seg1Off + 0];
        var ds = seg1[seg1Off + 0] - seg0[seg0Off + 0];
        float denom = dp0 ^ dp1;

        if (MathUtils.Sqr(denom) > 1e-6f * dp0.GetLength2() * dp1.GetLength2())
        {
            float t0 = ds ^ dp1;
            float t1 = ds ^ dp0;
            int sg = MathUtils.SgnNZ(denom);
            denom *= sg; t0 *= sg; t1 *= sg;
            if ((MathUtils.IsNeg(MathF.Abs(t0 * 2 - denom) - denom) & MathUtils.IsNeg(MathF.Abs(t1 * 2 - denom) - denom)) != 0)
            {
                ptres[0] = seg0[seg0Off + 0] + dp0 * (t0 / denom);
                return 1;
            }
            return 0;
        }

        // Parallel case
        if (MathUtils.Sqr(ds ^ dp0) < 1e-6f * ds.GetLength2() * dp0.GetLength2())
        {
            // segments are [almost] parallel and touching
            float[,] t = new float[2, 2];
            int[,] pptIdx = new int[2, 2]; // 0/1 selects seg0/seg1 base, +0/+1 selects which endpoint
            int[,] pptOff = new int[2, 2];
            t[0, 0] = 0; t[0, 1] = dp0.GetLength2();
            pptIdx[0, 0] = 0; pptOff[0, 0] = seg0Off;
            pptIdx[0, 1] = 0; pptOff[0, 1] = seg0Off + 1;
            float ta = (seg1[seg1Off + 0] - seg0[seg0Off + 0]).Dot(dp0);
            float tb = (seg1[seg1Off + 1] - seg0[seg0Off + 0]).Dot(dp0);
            int idir = MathUtils.IsNeg(tb - ta);
            t[1, idir] = ta; t[1, idir ^ 1] = tb;
            pptIdx[1, idir] = 1; pptOff[1, idir] = seg1Off;
            pptIdx[1, idir ^ 1] = 1; pptOff[1, idir ^ 1] = seg1Off + 1;

            if (MathF.Max(t[0, 0], t[1, 0]) > MathF.Min(t[0, 1], t[1, 1])) return 0;

            int pick0 = MathUtils.IsNeg(t[0, 0] - t[1, 0]);
            int pick1 = MathUtils.IsNeg(t[1, 1] - t[0, 1]);
            ptres[0] = pptIdx[0, pick0] == 0 ? seg0[pptOff[0, pick0]] : seg1[pptOff[0, pick0]];
            ptres[1] = pptIdx[1, pick1] == 0 ? seg0[pptOff[1, pick1]] : seg1[pptOff[1, pick1]];
            return 2;
        }
        return 0;
    }

    private static Vector2i GetCell(in PhysVector2 pt, in PhysVector2 rstep)
    {
        return new Vector2i(
            (int)MathF.Floor(pt.X * rstep.X - 0.5f),
            (int)MathF.Floor(pt.Y * rstep.Y - 0.5f));
    }

    private static void GetRect(Vector2i ipt0, Vector2i ipt1, Vector2i[] irect, Vector2i isz)
    {
        irect[0].X = System.Math.Max(0, System.Math.Min(ipt0.X, ipt1.X));
        irect[0].Y = System.Math.Min(isz.Y, System.Math.Max(0, System.Math.Min(ipt0.Y, ipt1.Y)));
        irect[1].X = System.Math.Min(isz.X - 1, System.Math.Max(ipt0.X, ipt1.X));
        irect[1].Y = System.Math.Min(isz.Y, System.Math.Max(ipt0.Y, ipt1.Y));
    }

    /// <summary>Literal port of `check_if_inside` (boolean2d.cpp:71-89).</summary>
    private static int CheckIfInside(int iobj, ref Vector2i ipt, Vector2i isz,
        PhysVector2[] ptsrc, PhysVector2 pt, int[] grid, uint[] hash)
    {
        int bInside, bStop = 0;
        if ((uint)ipt.X >= (uint)isz.X || ipt.Y < 0) return 0;
        for (bInside = 0; ipt.Y <= isz.Y && bStop == 0; ipt.Y++)
        {
            int rowBase = ipt.Y * isz.X + ipt.X;
            for (int i = grid[rowBase]; i < grid[rowBase + 1]; i++)
            {
                if ((int)(hash[i] >> 31) != iobj) continue;
                int idx = (int)(hash[i] & 0x7FFFFFFF);
                var pt0 = ptsrc[idx];
                var pt1 = ptsrc[idx + 1];
                var dp = pt1 - pt0;
                var ycur = new QuotientF((dp ^ pt0) + pt.X * dp.Y, dp.X).FixSign();
                if ((MathUtils.IsNeg(MathF.Abs((pt0.X + pt1.X) - pt.X * 2) - MathF.Abs(pt0.X - pt1.X))
                    & MathUtils.IsNeg(pt.Y - ycur.Val())) != 0)
                {
                    bInside -= MathUtils.Sgn(dp.X);
                    bStop = 1;
                }
            }
        }
        return MathUtils.IsNeg(-bInside);
    }

    /// <summary>
    /// 2D polygon boolean operation. Literal port of `boolean2d` (boolean2d.cpp:92-293).
    /// Returns the number of result points, with positions in <paramref name="ptres"/>
    /// and IDs (encoding source edges) in <paramref name="pidres"/>.
    ///
    /// `bClosed=true` means both inputs are closed polygons; `bClosed=false` indicates
    /// open polylines (used for sweep-test strips).
    /// </summary>
    public static int Compute(BoolType type,
        PhysVector2[] ptbuf1, int npt1,
        PhysVector2[] ptbuf2, int npt2,
        bool bClosed,
        out PhysVector2[] ptres, out int[] pidres)
    {
        var boolPtBuf = new PhysVector2[BufSize];
        var boolIdBuf = new int[BufSize];
        var boolGrid = new int[BufSize];
        var boolHash = new uint[HashBufSize];
        var boolInters = new Inters2D[IntersBufSize];

        var ptsrc = new[] { ptbuf1, ptbuf2 };
        var npt = new[] { npt1, npt2 };
        pidres = boolIdBuf;

        // Trivial degenerate cases.
        if (npt1 < 3) { ptres = ptbuf2; return bClosed ? npt2 : 0; }
        if (npt2 < 2 + (bClosed ? 1 : 0)) { ptres = ptbuf1; return bClosed ? npt1 : 0; }

        // Compute bounding boxes per polygon.
        var ptmin = new PhysVector2[2];
        var ptmax = new PhysVector2[2];
        for (int iobj = 0; iobj < 2; iobj++)
        {
            ptmin[iobj] = ptmax[iobj] = ptsrc[iobj][0];
            for (int i = 1; i < npt[iobj]; i++)
            {
                ptmin[iobj].X = MathF.Min(ptmin[iobj].X, ptsrc[iobj][i].X);
                ptmin[iobj].Y = MathF.Min(ptmin[iobj].Y, ptsrc[iobj][i].Y);
                ptmax[iobj].X = MathF.Max(ptmax[iobj].X, ptsrc[iobj][i].X);
                ptmax[iobj].Y = MathF.Max(ptmax[iobj].Y, ptsrc[iobj][i].Y);
            }
        }
        var ptbox = new PhysVector2[2];
        ptbox[0].X = MathF.Max(ptmin[0].X, ptmin[1].X);
        ptbox[0].Y = MathF.Max(ptmin[0].Y, ptmin[1].Y);
        ptbox[1].X = MathF.Min(ptmax[0].X, ptmax[1].X);
        ptbox[1].Y = MathF.Min(ptmax[0].Y, ptmax[1].Y);
        var sz = ptbox[1] - ptbox[0];
        sz.X += MathF.Abs(sz.Y) * 1e-5f;
        sz.Y += MathF.Abs(sz.X) * 1e-5f;
        if (MathF.Min(sz.X, sz.Y) < MathF.Min(
            MathF.Max(ptmax[0].X - ptmin[0].X, ptmax[0].Y - ptmin[0].Y),
            MathF.Max(ptmax[1].X - ptmin[1].X, ptmax[1].Y - ptmin[1].Y)) * 0.001f)
        { ptres = boolPtBuf; return 0; }
        ptbox[0] = ptbox[0] - sz * 0.01f;
        ptbox[1] = ptbox[1] + sz * 0.01f;
        sz = ptbox[1] - ptbox[0];
        sz.X += MathF.Abs(sz.Y) * 0.01f;
        sz.Y += MathF.Abs(sz.X) * 0.01f;

        // Allocate hash grid.
        int gridLimit = boolGrid.Length - 1;
        int npttmp = System.Math.Min((npt[0] + npt[1]) << 1, gridLimit);
        float ratioyx = MathF.Max(MathF.Min(sz.Y / sz.X, npttmp), 1.0f);
        float ratioxy = MathF.Max(MathF.Min(sz.X / sz.Y, npttmp), 1.0f);
        var isz = new Vector2i();
        isz.Y = System.Math.Max(1, (int)(MathF.Sqrt(npttmp * 4 * ratioyx + 1) * 0.5f - 1.0f));
        isz.X = System.Math.Max(1, (int)(isz.Y * ratioxy - 0.5f));
        if (isz.X * (isz.Y + 1) > gridLimit)
        {
            isz.Y = System.Math.Min(isz.Y, gridLimit);
            isz.X = gridLimit / (isz.Y + 1);
        }
        var rstep = new PhysVector2(isz.X / sz.X, isz.Y / sz.Y);
        int nsz = isz.X * (isz.Y + 1);
        if (nsz >= boolGrid.Length - 1) { ptres = boolPtBuf; return 0; }
        npt[1] -= bClosed ? 0 : 1;

        for (int i = 0; i <= nsz; i++) boolGrid[i] = 0;

        var ipt0 = new Vector2i();
        var ipt1 = new Vector2i();
        var irect = new Vector2i[2];

        // Count cells per segment.
        for (int iobj = 0; iobj < 2; iobj++)
        {
            ipt0 = GetCell(ptsrc[iobj][0] - ptbox[0], rstep);
            for (int i = 0; i < npt[iobj]; i++)
            {
                ipt1 = GetCell(ptsrc[iobj][i + 1] - ptbox[0], rstep);
                GetRect(ipt0, ipt1, irect, isz);
                for (int ix = irect[0].X; ix <= irect[1].X; ix++)
                    for (int iy = irect[0].Y; iy <= irect[1].Y; iy++)
                        boolGrid[iy * isz.X + ix]++;
                ipt0 = ipt1;
            }
        }
        for (int i = 1; i <= nsz; i++) boolGrid[i] += boolGrid[i - 1];
        if (boolGrid[nsz - 1] > boolHash.Length) { ptres = boolPtBuf; return 0; }

        // Insert segments into hash cells.
        for (int iobj = 0; iobj < 2; iobj++)
        {
            ipt0 = GetCell(ptsrc[iobj][0] - ptbox[0], rstep);
            for (int i = 0; i < npt[iobj]; i++)
            {
                ipt1 = GetCell(ptsrc[iobj][i + 1] - ptbox[0], rstep);
                GetRect(ipt0, ipt1, irect, isz);
                for (int ix = irect[0].X; ix <= irect[1].X; ix++)
                    for (int iy = irect[0].Y; iy <= irect[1].Y; iy++)
                        boolHash[--boolGrid[iy * isz.X + ix]] = (uint)(iobj << 31 | i);
                ipt0 = ipt1;
            }
        }

        // Pick the polygon with shorter edges as the traversal driver.
        int iobjT;
        if (bClosed)
        {
            iobjT = MathUtils.IsNeg(
                (ptmax[1].X - ptmin[1].X + ptmax[1].Y - ptmin[1].Y) * npt[0] -
                (ptmax[0].X - ptmin[0].X + ptmax[0].Y - ptmin[0].Y) * npt[1]);
        }
        else iobjT = 1;

        // Build intersection list by traversing iobjT's edges.
        int ninters = 0;
        var ptint = new PhysVector2[2];
        if (!bClosed)
        {
            boolInters[0].Pt = ptsrc[1][0];
            boolInters[0].IEdge0 = -1;
            boolInters[0].IEdge1 = 0;
            ninters = 1;
        }
        ipt0 = GetCell(ptsrc[iobjT][0] - ptbox[0], rstep);
        for (int i = 0; i < npt[iobjT]; i++)
        {
            ipt1 = GetCell(ptsrc[iobjT][i + 1] - ptbox[0], rstep);
            GetRect(ipt0, ipt1, irect, isz);
            int istart = ninters;

            for (int ix = irect[0].X; ix <= irect[1].X; ix++)
                for (int iy = irect[0].Y; iy <= irect[1].Y; iy++)
                    for (int j = boolGrid[iy * isz.X + ix]; j < boolGrid[iy * isz.X + ix + 1]; j++)
                    {
                        if ((int)(boolHash[j] >> 31) == iobjT) continue;
                        int otherIdx = (int)(boolHash[j] & 0x7FFFFFFF);
                        int n = LineSegInters(ptsrc[iobjT], i, ptsrc[iobjT ^ 1], otherIdx, ptint) - 1;
                        for (; n >= 0; n--)
                        {
                            var dp = ptsrc[iobjT][i + 1] - ptsrc[iobjT][i];
                            float t = (ptint[n] - ptsrc[iobjT][i]).Dot(dp);

                            int idx;
                            for (idx = istart; idx < ninters
                                && MathF.Abs((boolInters[idx].Pt - ptsrc[iobjT][i]).Dot(dp) - t) > t * 1e-7f; idx++) { }
                            if (idx < ninters) continue;
                            if (ninters == boolInters.Length - 1) { ptres = boolPtBuf; return 0; }
                            for (idx = ninters - 1; idx >= istart && (boolInters[idx].Pt - ptsrc[iobjT][i]).Dot(dp) > t; idx--)
                                boolInters[idx + 1] = boolInters[idx];

                            var ins = new Inters2D { Pt = ptint[n] };
                            if (iobjT == 0) { ins.IEdge0 = i; ins.IEdge1 = otherIdx; }
                            else { ins.IEdge1 = i; ins.IEdge0 = otherIdx; }
                            boolInters[idx + 1] = ins;
                            ninters++;
                        }
                    }
            ipt0 = ipt1;
        }

        if (!bClosed)
        {
            if (ninters == boolInters.Length - 1) { ptres = boolPtBuf; return 0; }
            boolInters[ninters].Pt = ptsrc[1][npt[1]];
            boolInters[ninters].IEdge0 = -1;
            boolInters[ninters].IEdge1 = npt[1];
            boolInters[ninters + 1] = boolInters[ninters];
            ninters++;
        }
        else if (ninters > 0)
        {
            boolInters[ninters] = boolInters[0];
        }

        int nptres = 0;
        ptres = boolPtBuf;

        // No intersections: return the polygon that is inside the other.
        if (ninters - (bClosed ? 0 : 1) * 2 == 0)
        {
            ipt0 = GetCell(ptsrc[iobjT][0] - ptbox[0], rstep);
            int bIns = CheckIfInside(iobjT, ref ipt0, isz, ptsrc[iobjT ^ 1], ptsrc[iobjT][0], boolGrid, boolHash);
            npt[1] += bClosed ? 0 : 1;
            int chosen = iobjT;
            if (bClosed) chosen ^= bIns ^ 1;
            ptres = ptsrc[chosen];
            for (nptres = 0; nptres < npt[chosen]; nptres++)
                boolIdBuf[nptres] = (nptres + 1) << (chosen * 16);
            return nptres & -(bClosed ? 1 : bIns);
        }
        npt[1] += bClosed ? 0 : 1;

        // Build the boolean intersection by selecting the more-inward stripe at each crossing.
        int bPrevInside = 0;
        for (int idx = 0; idx < ninters; idx++)
        {
            int idxPrev = idx - 1; idxPrev = (idxPrev & ~(idxPrev >> 31)) | ((ninters - 1) & (idxPrev >> 31));
            int idxNext = idx + 1;
            int iEdge0Self = iobjT == 0 ? boolInters[idx].IEdge0 : boolInters[idx].IEdge1;
            int iSelf = iEdge0Self;
            int inext = iSelf + 1 & ((iSelf + 1 - npt[iobjT]) >> 31);
            var dpSelf = ptsrc[iobjT][inext] - ptsrc[iobjT][iSelf];

            int iOther = iobjT == 0 ? boolInters[idx].IEdge1 : boolInters[idx].IEdge0;
            int bInside;
            if (iOther >= 0)
            {
                var dpOther = ptsrc[iobjT ^ 1][iOther + 1 & ((iOther + 1 - npt[iobjT ^ 1]) >> 31)] - ptsrc[iobjT ^ 1][iOther];
                bInside = MathUtils.IsNeg(dpSelf ^ dpOther);
            }
            else
            {
                var pttest = boolInters[idx].Pt;
                ipt0 = GetCell(pttest - ptbox[0], rstep);
                bInside = CheckIfInside(iobjT, ref ipt0, isz, ptsrc[iobjT ^ 1], pttest, boolGrid, boolHash);
            }

            int iobj1 = iobjT ^ bInside ^ 1;
            if ((bInside | bPrevInside | (bClosed ? 1 : 0)) != 0)
            {
                boolPtBuf[nptres] = boolInters[idx].Pt;
                boolIdBuf[nptres++] = ((boolInters[idx].IEdge1 + 1) << 16) | (boolInters[idx].IEdge0 + 1);
                if (nptres >= boolPtBuf.Length) return nptres;
            }
            if ((bInside | (bClosed ? 1 : 0)) != 0)
            {
                int ie = iobj1 == 0 ? boolInters[idx].IEdge0 : boolInters[idx].IEdge1;
                int iCur = ie;
                int iNxt = iCur + 1 & ~(((npt[iobj1] - 2 - iCur) >> 31));
                var dp = ptsrc[iobj1][iNxt] - ptsrc[iobj1][iCur];
                int nextEdge = iobj1 == 0 ? boolInters[idxNext].IEdge0 : boolInters[idxNext].IEdge1;
                int prevEdge = iobj1 == 0 ? boolInters[idxPrev].IEdge0 : boolInters[idxPrev].IEdge1;
                bool bForceFirstStep = iCur == nextEdge &&
                    (boolInters[idx].Pt - ptsrc[iobj1][iCur]).Dot(dp) > (boolInters[idxNext].Pt - ptsrc[iobj1][iCur]).Dot(dp);
                while (bForceFirstStep ||
                    iCur != nextEdge && (iobj1 == iobjT || iCur != prevEdge))
                {
                    boolPtBuf[nptres] = ptsrc[iobj1][iCur + 1];
                    boolIdBuf[nptres++] = (iCur + 2) << (iobj1 * 16);
                    if (nptres >= boolPtBuf.Length) return nptres;
                    bForceFirstStep = false;
                    iCur = iCur + 1 & ~(((npt[iobj1] - 2 - iCur) >> 31));
                }
            }
            bPrevInside = bInside;
        }

        return nptres;
    }
}
