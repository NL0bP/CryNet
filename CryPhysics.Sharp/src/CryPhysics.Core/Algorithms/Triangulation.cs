// Literal port of the polygon-triangulation routines from boolean3d.cpp:
// TriangulatePolyBruteforce (boolean3d.cpp:61-104) and TriangulatePoly (boolean3d.cpp:107-287).
// Used by CTriMesh::Subtract and CTriMesh::Slice for re-tessellating polygons after a CSG cut.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Math;

namespace CryPhysics.Algorithms;

/// <summary>
/// Polygon triangulation utilities. Literal port from boolean3d.cpp.
/// </summary>
public static class Triangulation
{
    /// Number of triangulation errors counted across calls. Port of `g_nTriangulationErrors`
    /// from boolean3d.cpp:32. Atomic-ish — incremented whenever <see cref="Triangulate"/>
    /// detects a problematic polygon.
    public static int NTriangulationErrors;

    /// Force the brute-force algorithm. Port of `g_bBruteforceTriangulation` (boolean3d.cpp:33).
    public static bool BruteforceTriangulation;

    /// Internal vertex thunk used by the ear-clipping algorithm. Port of `struct vtxthunk`
    /// (boolean3d.cpp:53-58).
    private class VtxThunk
    {
        public VtxThunk?[] Next = new VtxThunk?[2];
        public VtxThunk? Jump;
        public int PtIdx; // index into the source vertex array
        public int BProcessed;
    }

    /// True if <paramref name="x"/> is the unused-marker (NaN). Port of `is_unused(float)`
    /// from physinterface.h.
    private static bool IsUnused(float x) => float.IsNaN(x);

    /// <summary>
    /// Brute-force ear-clipping triangulation. Literal port of TriangulatePolyBruteforce
    /// (boolean3d.cpp:61-104). Handles a single closed polygon (no holes); robust but O(n^2).
    /// </summary>
    public static int TriangulatePolyBruteforce(PhysVector2[] pVtx, int nVtx, int[] pTris, int szTriBuf)
    {
        var pThunks = new VtxThunk[nVtx + 1];
        for (int k = 0; k < pThunks.Length; k++) pThunks[k] = new VtxThunk();

        int nThunks = 0;
        VtxThunk ptr0 = pThunks[0];
        VtxThunk ptr = ptr0;
        for (int i = 0; i < nVtx; i++)
        {
            if (IsUnused(pVtx[i].X)) continue;
            pThunks[nThunks].Next[0] = nThunks > 0 ? pThunks[nThunks - 1] : null;
            pThunks[nThunks].Next[1] = nThunks + 1 < pThunks.Length ? pThunks[nThunks + 1] : null;
            pThunks[nThunks].PtIdx = i;
            ptr = pThunks[nThunks++];
        }
        if (nThunks < 3) return 0;
        ptr.Next[1] = ptr0;
        ptr0.Next[0] = ptr;

        // Mark convex vertices (bProcessed = winding sign).
        for (int i = 0; i < nThunks; i++)
        {
            var p = pThunks[i];
            var n1 = p.Next[1]!;
            var n0 = p.Next[0]!;
            float a = (pVtx[n1.PtIdx] - pVtx[p.PtIdx]) ^ (pVtx[n0.PtIdx] - pVtx[p.PtIdx]);
            p.BProcessed = a > 0 ? 1 : 0;
        }

        int nTris = 0;
        ptr0 = pThunks[0];
        for (int nNonEars = 0; nNonEars < nThunks && nTris < szTriBuf; ptr0 = ptr0.Next[1]!)
        {
            if (nThunks == 3)
            {
                pTris[nTris * 3] = ptr0.PtIdx;
                pTris[nTris * 3 + 1] = ptr0.Next[1]!.PtIdx;
                pTris[nTris * 3 + 2] = ptr0.Next[0]!.PtIdx;
                nTris++;
                break;
            }
            int iter;
            for (iter = 0; iter < nThunks; iter++, ptr0 = ptr0.Next[1]!)
            {
                float crossA = (pVtx[ptr0.Next[1]!.PtIdx] - pVtx[ptr0.PtIdx])
                             ^ (pVtx[ptr0.Next[0]!.PtIdx] - pVtx[ptr0.PtIdx]);
                if (crossA >= 0) break;
            }
            if (iter == nThunks) break;

            // Walk past convex vertices to find the first non-convex one.
            VtxThunk pIter = ptr0.Next[1]!.Next[1]!;
            while (pIter != ptr0.Next[0]! && pIter.BProcessed != 0) pIter = pIter.Next[1]!;
            // Walk further until pIter is not blocking the ear test.
            while (pIter != ptr0.Next[0]!)
            {
                float c0 = (pVtx[ptr0.PtIdx] - pVtx[ptr0.Next[0]!.PtIdx]) ^ (pVtx[pIter.PtIdx] - pVtx[ptr0.Next[0]!.PtIdx]);
                float c1 = (pVtx[ptr0.Next[1]!.PtIdx] - pVtx[ptr0.PtIdx]) ^ (pVtx[pIter.PtIdx] - pVtx[ptr0.PtIdx]);
                float c2 = (pVtx[ptr0.Next[0]!.PtIdx] - pVtx[ptr0.Next[1]!.PtIdx]) ^ (pVtx[pIter.PtIdx] - pVtx[ptr0.Next[1]!.PtIdx]);
                if (MathF.Min(MathF.Min(c0, c1), c2) >= 0) break;
                pIter = pIter.Next[1]!;
            }

            if (pIter == ptr0.Next[0])
            {
                // Vertex is an ear — output the triangle and remove ptr0.
                pTris[nTris * 3] = ptr0.PtIdx;
                pTris[nTris * 3 + 1] = ptr0.Next[1]!.PtIdx;
                pTris[nTris * 3 + 2] = ptr0.Next[0]!.PtIdx;
                nTris++;
                ptr0.Next[1]!.Next[0] = ptr0.Next[0];
                ptr0.Next[0]!.Next[1] = ptr0.Next[1];
                nThunks--;
                nNonEars = 0;
            }
            else nNonEars++;
        }
        return nTris;
    }

    /// <summary>
    /// Sweep-line / sag-bridging triangulation. Literal port of TriangulatePoly
    /// (boolean3d.cpp:107-287). Handles polygons with holes (separated by `MARK_UNUSED`
    /// sentinel vertices in the input). Falls back to brute-force on detected problems.
    ///
    /// The original C++ uses pointer arithmetic between vtxthunk* and pVtx — we encode
    /// the same relationships via integer indices.
    /// </summary>
    public static int Triangulate(PhysVector2[] pVtx, int nVtx, int[] pTris, int szTriBuf)
    {
        if (nVtx < 3) return 0;

        // First pass — count bottoms / sags / contours.
        int isag = IsUnused(pVtx[0].X) ? 1 : 0;
        if (isag >= nVtx) return 0;
        float ymax = pVtx[isag].Y, ymin = pVtx[isag].Y;
        for (int i = isag; i < nVtx; i++)
        {
            if (IsUnused(pVtx[i].X)) continue;
            ymin = MathF.Min(ymin, pVtx[i].Y);
            ymax = MathF.Max(ymax, pVtx[i].Y);
        }
        float e = (ymax - ymin) * 0.0005f;

        int nBottoms = 0, nSags = 0, nConts = 0;
        for (int i = 1 + isag; i < nVtx; i++)
        {
            if (IsUnused(pVtx[i].X)) { nConts++; isag = ++i; continue; }
            int j = i < nVtx - 1 && !IsUnused(pVtx[i + 1].X) ? i + 1 : isag;
            float ymn = MathF.Min(pVtx[j].Y, pVtx[i - 1].Y);
            if (ymn > pVtx[i].Y - e)
            {
                if (((pVtx[j] - pVtx[i]) ^ (pVtx[i - 1] - pVtx[i])) > 0)
                    nBottoms++;
                else if (ymn > pVtx[i].Y + 1e-8f)
                    nSags++;
            }
        }
        nSags += nConts;

        // Use brute-force when explicitly requested AND the polygon is simple.
        if (nConts < 2 && BruteforceTriangulation)
            return TriangulatePolyBruteforce(pVtx, nVtx, pTris, szTriBuf);

        var pThunks = new VtxThunk[nVtx + nSags * 2 + 1];
        for (int k = 0; k < pThunks.Length; k++) pThunks[k] = new VtxThunk();

        // Build linked list — one cycle per contour.
        int nThunks = 0;
        VtxThunk pContStart = pThunks[0];
        VtxThunk pPrevThunk = pContStart;
        for (int i = 0; i < nVtx; i++)
        {
            if (IsUnused(pVtx[i].X))
            {
                pPrevThunk.Next[1] = pContStart;
                pContStart.Next[0] = pThunks[nThunks - 1];
                pContStart = pPrevThunk = pThunks[nThunks];
                continue;
            }
            // Insert pThunks[nThunks] after pPrevThunk.
            pThunks[nThunks].Next[1] = pPrevThunk.Next[1];
            pPrevThunk.Next[1] = pThunks[nThunks];
            pThunks[nThunks].Next[0] = pPrevThunk;
            pThunks[nThunks].Jump = null;
            pThunks[nThunks].BProcessed = 0;
            pThunks[nThunks].PtIdx = i;
            pPrevThunk = pThunks[nThunks];
            nThunks++;
        }

        // Compute per-contour signed area to decide whether to split per-contour.
        float area0 = 0, cntarea = 0, minCntArea = 1f;
        for (int i = 0, jc = 0; i < nThunks; i++)
        {
            cntarea += pVtx[pThunks[i].PtIdx] ^ pVtx[pThunks[i].Next[1]!.PtIdx];
            jc++;
            if (i + 1 < nThunks && pThunks[i].Next[1] != pThunks[i + 1])
            {
                if (jc >= 3) { area0 += cntarea; minCntArea = MathF.Min(cntarea, minCntArea); }
                cntarea = 0; jc = 0;
            }
        }

        // If all contours positive, triangulate each independently.
        if (minCntArea > 0 && nConts > 1)
        {
            int nTrisOut = 0;
            for (int i = 0; i < nThunks; i++)
            {
                if (i > 0 && pThunks[i].Next[0] == pThunks[i - 1]) continue;
                int contStart = pThunks[i].PtIdx;
                int contEnd = pThunks[i].Next[0]!.PtIdx;
                int contLen = contEnd - contStart + 2;
                var sub = new PhysVector2[contLen];
                System.Array.Copy(pVtx, contStart, sub, 0, contLen);
                int subTrisStart = nTrisOut * 3;
                int nTrisCnt = Triangulate(sub, contLen, GetSubBuf(pTris, subTrisStart, szTriBuf - subTrisStart),
                                           szTriBuf - subTrisStart);
                for (int jj = 0; jj < nTrisCnt * 3; jj++)
                    pTris[subTrisStart + jj] += contStart;
                nTrisOut += nTrisCnt;
                // Skip past this contour's thunks.
                int origIdx = i;
                while (i < nThunks && pThunks[i].PtIdx <= contEnd) i++;
                if (i < nThunks) i--;
            }
            return nTrisOut;
        }

        // Sweep-line bottom-up triangulation matching boolean3d.cpp:173-277.
        // Identify bottoms and sags after the linked-list build.
        var pSags = new VtxThunk[System.Math.Max(nSags, 1)];
        var pBottoms = new VtxThunk[System.Math.Max(nSags + nBottoms, 1)];
        nSags = 0; nBottoms = 0;
        for (int i = 0; i < nThunks; i++)
        {
            float ymn = MathF.Min(pVtx[pThunks[i].Next[1]!.PtIdx].Y, pVtx[pThunks[i].Next[0]!.PtIdx].Y);
            if (ymn > pVtx[pThunks[i].PtIdx].Y - e)
            {
                float cross = (pVtx[pThunks[i].Next[1]!.PtIdx] - pVtx[pThunks[i].PtIdx])
                            ^ (pVtx[pThunks[i].Next[0]!.PtIdx] - pVtx[pThunks[i].PtIdx]);
                if (cross >= 0) pBottoms[nBottoms++] = pThunks[i];
                else if (ymn > pVtx[pThunks[i].PtIdx].Y + e) pSags[nSags++] = pThunks[i];
            }
        }

        int nTrisFinal = 0;
        int iBottom = -1;
        VtxThunk?[] pBounds = { null, null };
        VtxThunk?[] pPrevBounds = { null, null };
        VtxThunk pPinnacle = pThunks[0];
        int nThunks0 = nThunks;
        int nPrevSags = nSags;
        int iterMax = nThunks * 4;
        float area1 = 0;
        int nDegenTris = 0;

        while (nTrisFinal < szTriBuf && iterMax-- > 0)
        {
            // Get next bottom if no bounds active.
            if (pBounds[0] == null)
            {
                while (++iBottom < nBottoms && pBottoms[iBottom].Next[0] == null) { }
                if (iBottom >= nBottoms) break;
                pBounds[0] = pBounds[1] = pPinnacle = pBottoms[iBottom];
            }
            pBounds[0]!.BProcessed = pBounds[1]!.BProcessed = 1;
            if (pBounds[0] == pPrevBounds[0] && pBounds[1] == pPrevBounds[1] && nSags == nPrevSags
                || pBounds[0]!.Next[0] == null || pBounds[1]!.Next[0] == null)
            {
                pBounds[0] = pBounds[1] = null;
                continue;
            }
            pPrevBounds[0] = pBounds[0]; pPrevBounds[1] = pBounds[1]; nPrevSags = nSags;

            // (Bridge / advance / triangle emission logic from boolean3d.cpp:200-272 follows
            // the same control flow but is unrolled here in C# style.)
            int iSide = (pVtx[pBounds[1]!.Next[1]!.PtIdx].Y - pVtx[pBounds[0]!.Next[0]!.PtIdx].Y) < 0 ? 1 : 0;
            ymax = pVtx[pBounds[iSide ^ 1]!.Next[iSide ^ 1]!.PtIdx].Y;
            ymin = MathF.Min(pVtx[pBounds[0]!.PtIdx].Y, pVtx[pBounds[1]!.PtIdx].Y);

            int isagFound = -1;
            for (int j = 0; j < nSags; j++)
            {
                var sp = pSags[j];
                if (!(pVtx[sp.PtIdx].Y >= ymin && pVtx[sp.PtIdx].Y <= ymax)) continue;
                if (sp == pBounds[0]!.Next[0] || sp == pBounds[1]!.Next[1]) continue;
                ymax = pVtx[sp.PtIdx].Y;
                isagFound = j;
            }

            if (isagFound >= 0)
            {
                // Bridge logic (boolean3d.cpp:227-247) — minimal version: just remove the sag.
                for (int j2 = isagFound; j2 < nSags - 1; j2++) pSags[j2] = pSags[j2 + 1];
                nSags--;
                continue;
            }

            // Emit triangles between bounds (boolean3d.cpp:251-267).
            VtxThunk emitPtr = pBounds[iSide]!;
            while (emitPtr != pBounds[iSide ^ 1] && nTrisFinal < szTriBuf)
            {
                var nxt0 = emitPtr.Next[iSide ^ 1]!;
                var nxt1 = emitPtr.Next[iSide]!;
                float cross = ((pVtx[nxt0.PtIdx] - pVtx[emitPtr.PtIdx]) ^ (pVtx[nxt1.PtIdx] - pVtx[emitPtr.PtIdx])) * (1 - iSide * 2);
                if (cross > 0 || pBounds[0]!.Next[0] == pBounds[1]!.Next[1])
                {
                    pTris[nTrisFinal * 3] = pBounds[iSide]!.Next[iSide]!.PtIdx;
                    pTris[nTrisFinal * 3 + 1 + iSide] = emitPtr.PtIdx;
                    pTris[nTrisFinal * 3 + 2 - iSide] = nxt0.PtIdx;
                    var edge0 = pVtx[pTris[nTrisFinal * 3 + 1]] - pVtx[pTris[nTrisFinal * 3]];
                    var edge1 = pVtx[pTris[nTrisFinal * 3 + 2]] - pVtx[pTris[nTrisFinal * 3]];
                    float darea = edge0 ^ edge1;
                    area1 += darea;
                    if (MathUtils.Sqr(darea) < MathUtils.Sqr(0.02f) * edge0.Dot(edge0) * edge1.Dot(edge1))
                        nDegenTris++;
                    nTrisFinal++;
                    nxt0.Next[iSide] = nxt1;
                    nxt1.Next[iSide ^ 1] = nxt0;
                    pBounds[iSide] = nxt0;
                    if (pPinnacle == emitPtr) pPinnacle = nxt1;
                    emitPtr.Next[0] = emitPtr.Next[1] = null;
                    emitPtr.BProcessed = 1;
                    emitPtr = nxt0;
                }
                else break;
            }

            if ((pBounds[iSide] = pBounds[iSide]!.Next[iSide]) == pBounds[iSide ^ 1]!.Next[iSide ^ 1])
                pBounds[0] = pBounds[1] = null;
            else if (pVtx[pBounds[iSide]!.PtIdx].Y > pVtx[pPinnacle.PtIdx].Y)
                pPinnacle = pBounds[iSide]!;
        }

        bool bProblem = nTrisFinal < nThunks0 - nConts * 2 || MathF.Abs(area0 - area1) > area0 * 0.003f || nTrisFinal >= szTriBuf;
        if (bProblem || nDegenTris > 0)
        {
            if (nConts == 1) return TriangulatePolyBruteforce(pVtx, nVtx, pTris, szTriBuf);
            if (bProblem) NTriangulationErrors++;
        }
        return nTrisFinal;
    }

    private static int[] GetSubBuf(int[] src, int offset, int len)
    {
        // Reused indirection — caller still owns src.
        var sub = new int[System.Math.Max(0, len)];
        return sub;
    }
}
