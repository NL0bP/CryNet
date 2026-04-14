// Literal port of the bop_* (boolean operation) data structures from
// dev/Code/CryEngine/CryCommon/physinterface.h:2047-2115 and the tessvtx/tesspoly
// helpers from boolean3d.cpp:298-342.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.BVTrees;
using CryPhysics.Geometry;
using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Algorithms;

/// <summary>New-vertex record. Port of `struct bop_newvtx` (physinterface.h:2047-2051).</summary>
public struct BopNewVtx
{
    public int Idx;             // vertex index in the resulting A phys mesh
    public int IBvtx;           // -1 if intersection vertex, >=0 if B vertex
    public int IdxTri0, IdxTri1; // intersecting triangles' foreign indices
}

/// <summary>New-triangle record. Port of `struct bop_newtri` (physinterface.h:2053-2060).</summary>
public class BopNewTri
{
    public int IdxNew;          // newly generated foreign index
    public int Iop;             // triangle source 0=A, 1=B
    public int IdxOrg;          // original triangulated tri's foreign idx
    public int[] IVtx = new int[3]; // per-vertex existing index (>=0) or -(new+1) (<0)
    public float AreaOrg;
    public PhysVector3[] Area = new PhysVector3[3];
}

/// <summary>Vertex-weld record. Port of `struct bop_vtxweld` (physinterface.h:2062-2066).</summary>
public struct BopVtxWeld
{
    public int IvtxDst;     // C++ bitfield int:16; modeled as plain int
    public int IvtxWelded;
    public void Set(int dst, int welded) { IvtxDst = dst; IvtxWelded = welded; }
}

/// <summary>T-junction fix record. Port of `struct bop_TJfix` (physinterface.h:2068-2078).</summary>
public struct BopTJFix
{
    public int IABC;
    public int IACJ;
    public int IAC;
    public int ICA;
    public int ITJvtx;
    public void Set(int iACJ, int iAC, int iABC, int iCA, int iTJvtx)
    { IACJ = iACJ; IAC = iAC; IABC = iABC; ICA = iCA; ITJvtx = iTJvtx; }
}

/// <summary>
/// Mesh-update record produced by CSG operations. Linked-list refcount-tracking thunk
/// + payload arrays. Port of `struct bop_meshupdate_thunk` + `struct bop_meshupdate`
/// from physinterface.h:2082-2115 plus `bop_meshupdate::~bop_meshupdate` from boolean3d.cpp:36-50.
/// </summary>
public class BopMeshUpdate
{
    // bop_meshupdate_thunk fields.
    public BopMeshUpdate? PrevRef, NextRef;

    // Payload.
    public IGeometry?[] PMesh = new IGeometry?[2]; // 0-dst (A), 1-src (B)
    public int[]? PRemovedVtx; public int NRemovedVtx;
    public int[]? PRemovedTri; public int NRemovedTri;
    public BopNewVtx[]? PNewVtx; public int NNewVtx;
    public BopNewTri[]? PNewTri; public int NNewTri;
    public BopVtxWeld[]? PWeldedVtx; public int NWeldedVtx;
    public BopTJFix[]? PTJFixes; public int NTJFixes;
    public BopMeshUpdate? Next;
    public Box[]? PMovedBoxes; public int NMovedBoxes;
    public float RelScale;

    public BopMeshUpdate() { PrevRef = NextRef = this; Reset(); }

    /// Port of `void Reset()` from physinterface.h:2092-2096.
    public void Reset()
    {
        PRemovedVtx = null; PRemovedTri = null; PNewVtx = null; PNewTri = null;
        PWeldedVtx = null; PTJFixes = null; PMovedBoxes = null;
        NRemovedVtx = NRemovedTri = NNewVtx = NNewTri = NWeldedVtx = NTJFixes = NMovedBoxes = 0;
        Next = null;
        PMesh[0] = PMesh[1] = null;
        RelScale = 1.0f;
    }

    /// Port of `bop_meshupdate::~bop_meshupdate` (boolean3d.cpp:36-50). Detaches the
    /// thunk from its linked list and releases the meshes.
    public void Dispose()
    {
        // Linked-list detach (matches PrevRef->NextRef = NextRef etc.)
        if (PrevRef != null) PrevRef.NextRef = NextRef;
        if (NextRef != null) NextRef.PrevRef = PrevRef;
        PrevRef = NextRef = this;
        // C++ also deletes `next` — port via direct chain disposal.
        Next?.Dispose();
        Next = null;
        // C++ Releases pMesh[0/1]; in C# the GC handles ref counting on geometries.
        PMesh[0] = PMesh[1] = null;
    }
}

/// <summary>Tessellation-vertex node. Port of `struct tessvtx` (boolean3d.cpp:299-309).</summary>
public class TessVtx
{
    public int IVtx;
    public int INext, INextBrd;
    public int IPrev;
    public int ICont;       // -1 if source A, -2 if source B
    public int ITwin;
    public int IPoly;
    public float T;
    public PhysVector3 Pt;
    public int Flags;
}

/// <summary>Tessellation-polygon node. Port of `struct tesspoly` (boolean3d.cpp:310-321).</summary>
public class TessPoly
{
    public int ITri;
    public int IVtx;
    public int IVtx0;
    public int IVtxCont;
    public int NVtx;
    public PhysVector3 N;
    public int Id;
    public float HoleArea;
    public float AreaOrg;
    public sbyte Mat;
}

/// <summary>Tessellation-vertex flag bits. Port of `enum tessvtx_flags` (boolean3d.cpp:298).</summary>
public static class TessVtxFlags
{
    public const int ContourEnd = 4;
    public const int VtxProcessed = 8;
    public const int VtxInstableptLog2 = 4;
    public const int VtxInstable = 4 << VtxInstableptLog2;
}

/// <summary>
/// Helpers used by CTriMesh::Subtract during border-vertex insertion. Literal port of
/// `insertBorderVtx` (boolean3d.cpp:323-342).
/// </summary>
public static class TessHelpers
{
    private static readonly int[] IncMod3 = { 1, 2, 0 };

    public static void InsertBorderVtx(TessVtx[] pVtx, int ivtx0, int ivtx, in PhysVector3 n, int bEnd)
    {
        var pt0 = pVtx[ivtx0].Pt;
        var pt1 = pVtx[ivtx0 + 1].Pt;
        var pt2 = pVtx[ivtx0 + 2].Pt;
        float[] estart = { 0,
                          (pt1 - pt0).LengthSq(),
                          (pt2 - pt1).LengthSq(),
                          (pt0 - pt2).LengthSq() };
        var ptc = (pt0 + pt1 + pt2) * (1.0f / 3f);
        var dpt = pVtx[ivtx].Pt - ptc;
        float[] eArr = new float[4];
        eArr[0] = eArr[3] = MathF.Min(estart[1], estart[3]);
        eArr[1] = MathF.Min(estart[1], estart[2]);
        eArr[2] = MathF.Min(estart[2], estart[3]);
        estart[2] += estart[1];
        estart[3] += estart[2];

        int i;
        for (i = 0; i < 2; i++)
        {
            float c0 = (pVtx[ivtx0 + i].Pt - ptc).Cross(dpt).Dot(n);
            float c1 = (pVtx[ivtx0 + IncMod3[i]].Pt - ptc).Cross(dpt).Dot(n);
            if (c0 > 0 && c1 < 0) break;
        }
        i = System.Math.Min(i, 2);

        float t = (pVtx[ivtx].Pt - pVtx[ivtx0 + i].Pt).Dot(pVtx[ivtx0 + IncMod3[i]].Pt - pVtx[ivtx0 + i].Pt) + estart[i];
        pVtx[ivtx].T = t;

        int iter = ivtx0;
        while ((pVtx[iter].T <= t ? 1 : 0)
             + (pVtx[pVtx[iter].INextBrd].T > t ? 1 : 0)
             + (pVtx[iter].T > pVtx[pVtx[iter].INextBrd].T ? 1 : 0) < 2)
        {
            iter = pVtx[iter].INextBrd;
        }
        pVtx[ivtx].INextBrd = pVtx[iter].INextBrd;
        pVtx[iter].INextBrd = ivtx;
        pVtx[ivtx].Flags |= (i + 1) & -bEnd;

        int idx;
        for (idx = 0; idx < 4 && MathF.Abs(t - estart[idx]) > eArr[idx] * MathUtils.Sqr(0.005f); idx++) { }
        pVtx[ivtx].Flags |= (((idx & 4) ^ 4) | (idx & -(idx < 3 ? 1 : 0))) << TessVtxFlags.VtxInstableptLog2;
    }
}
