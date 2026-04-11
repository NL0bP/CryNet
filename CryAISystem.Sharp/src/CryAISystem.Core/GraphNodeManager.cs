// Literal port of dev/Code/CryEngine/CryAISystem/GraphNodeManager.h
// .cpp impl deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public class CGraphNodeManager
{
    private const int BUCKET_SIZE_SHIFT = 7;
    private const int BUCKET_SIZE = 128;

    public CGraphNodeManager() { /* impl in .cpp */ }
    // ~CGraphNodeManager();

    // NOTE Oct 20, 2009: only called from Graph::Clear() and own destructor
    public void Clear(uint typeMask) { /* impl in .cpp */ }

    public uint CreateNode(uint type, Vec3 pos, uint ID) { return 0; /* impl in .cpp */ }
    public void DestroyNode(uint index) { /* impl in .cpp */ }

    public GraphNode GetNode(uint index)
    {
        if (index == 0)
            return null;
        int bucket = (int)((index - 1) >> BUCKET_SIZE_SHIFT);
        // FIXME: dummy node hack
        if (m_buckets[bucket] == null)
            return GetDummyNode();
        return null; /* impl in .cpp — pointer arithmetic on raw bucket bytes deferred */
    }

    public nuint NodeMemorySize() { return 0; /* impl in .cpp */ }
    public void GetMemoryStatistics(ICrySizer pSizer) { /* impl in .cpp */ }

    private class BucketHeader
    {
        public const ushort InvalidNextFreeBucketIdx = 0xffff;
        public const byte InvalidNextAvailableIdx = 0xff;

        public uint type;
        public ushort nextFreeBucketIdx;
        public byte nodeSizeS2;
        public byte nextAvailable;
        public byte[] nodes;

        public nuint GetNodeSize() { return ((nuint)nodeSizeS2) << 2; }
        public void SetNodeSize(nuint sz) { nodeSizeS2 = (byte)(sz >> 2); }
    }

    private GraphNode GetDummyNode()
    {
        // C++ uses placement new on static buffer; C# returns a singleton
        return s_dummy;
    }
    private static GraphNode s_dummy = new GraphNode_Triangular(IAISystem_ENavigationType.NAV_TRIANGULAR, new Vec3(0, 0, 0), 0xCECECECE);

    private int TypeSizeFromTypeIndex(uint typeIndex) { return 0; /* impl in .cpp */ }

    private List<BucketHeader> m_buckets = new List<BucketHeader>();
    private List<ushort> m_freeBuckets = new List<ushort>();
    private int[] m_typeSizes = new int[16]; // IAISystem::NAV_TYPE_COUNT — placeholder
}

// Forward decl shells
public class GraphNode_Triangular : GraphNode
{
    public GraphNode_Triangular(IAISystem_ENavigationType type, Vec3 pos, uint id) { }
}
