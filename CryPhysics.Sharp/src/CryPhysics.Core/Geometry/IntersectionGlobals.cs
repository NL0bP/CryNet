// Port of the global scratch state used by CGeometry::Intersect (geometry.cpp + globals).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using CryPhysics.Math;
using CryPhysics.Primitives;

namespace CryPhysics.Geometry;

/// <summary>
/// Per-thread scratch buffers for intersection. Mirrors the C++ globals indexed by
/// `iCaller`: g_Contacts[], g_AreaBuf[], g_AreaPtBuf[], g_BrdPtBuf[], g_nTotContacts,
/// g_Overlapper, plus the surface/edge/idbuf/iFeatureBuf/usedNodesMap/usedNodesIdx pools.
/// One instance lives per physics thread.
/// </summary>
public class IntersectionScratch
{
    // Maximum sizes — match the C++ static arrays.
    public const int MaxContacts = 1024;
    public const int MaxAreaBufs = 64;
    public const int MaxAreaPts = 256;
    public const int MaxBrdPts = 1024;
    public const int MaxSurfaceDescs = 256;
    public const int MaxEdgeDescs = 256;
    public const int MaxIdBuf = 4096;
    public const int MaxFeatureBuf = 4096;
    public const int MaxUsedNodes = 4096;

    public readonly GeomContact[] Contacts = new GeomContact[MaxContacts];
    public readonly GeomContactArea[] AreaBuf = new GeomContactArea[MaxAreaBufs];
    public readonly PhysVector3[] AreaPtBuf = new PhysVector3[MaxAreaPts];
    public readonly int[] AreaPrimBuf0 = new int[MaxAreaPts];
    public readonly int[] AreaPrimBuf1 = new int[MaxAreaPts];
    public readonly int[] AreaFeatureBuf0 = new int[MaxAreaPts];
    public readonly int[] AreaFeatureBuf1 = new int[MaxAreaPts];
    public readonly PhysVector3[] BrdPtBuf = new PhysVector3[MaxBrdPts];

    public int NTotContacts;
    public int NAreas;
    public int NAreaPt;
    public int BrdPtBufPos;
    public int SurfaceDescBufPos;
    public int EdgeDescBufPos;
    public int IdBufPos;
    public int IFeatureBufPos;
    public int UsedNodesMapPos;
    public int UsedNodesIdxPos;

    public int MaxContactsCap = MaxContacts;

    /// Reset the per-call buffers (port of the body of CGeometry::Intersect that
    /// clears g_nAreas/g_nAreaPt/g_nTotContacts/g_BrdPtBufPos when bKeepPrevContacts is false).
    public void ResetForCall(bool keepPrevContacts)
    {
        if (!keepPrevContacts)
        {
            NAreas = 0;
            NAreaPt = 0;
            NTotContacts = 0;
            BrdPtBufPos = 0;
        }
        SurfaceDescBufPos = 0;
        EdgeDescBufPos = 0;
        IdBufPos = 0;
        IFeatureBufPos = 0;
        UsedNodesMapPos = 0;
        UsedNodesIdxPos = 0;
    }
}

/// <summary>
/// Global registry of per-thread <see cref="IntersectionScratch"/> instances.
/// Port of the C++ pattern of indexing globals by `iCaller` (0..MAX_PHYS_THREADS).
/// </summary>
public static class IntersectionScratchPool
{
    private const int MaxThreads = 16;
    private static readonly IntersectionScratch[] _scratch = new IntersectionScratch[MaxThreads];
    private static readonly object _lock = new();

    public static IntersectionScratch Get(int iCaller)
    {
        int i = System.Math.Clamp(iCaller, 0, MaxThreads - 1);
        var s = _scratch[i];
        if (s == null)
        {
            lock (_lock) { _scratch[i] ??= new IntersectionScratch(); s = _scratch[i]; }
        }
        return s!;
    }
}
