// Literal port of dev/Code/CryEngine/CryAISystem/GraphNodeManager.h + GraphNodeManager.cpp (100L + 222L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Manager class for graph nodes.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CryAISystem;

public class CGraphNodeManager
{
    private const int BUCKET_SIZE_SHIFT = 7;
    private const int BUCKET_SIZE = 128;
    public const int NAV_TYPE_COUNT = 10;

    public CGraphNodeManager()
    {
        m_freeBuckets = new List<ushort>(NAV_TYPE_COUNT);
        for (int i = 0; i < NAV_TYPE_COUNT; i++)
            m_freeBuckets.Add(BucketHeader.InvalidNextFreeBucketIdx);

        // C++ stores sizeof each node type; C# manages objects directly, so we just store a type index.
        // The type sizes are not needed for the C# managed approach, but kept for API fidelity.
        // We set all to 1 to indicate "one slot per node".
        for (int i = 0; i < m_typeSizes.Length; i++)
            m_typeSizes[i] = 1;
    }

    // ~CGraphNodeManager
    // Call Clear(~0u) to destroy all nodes

    // NOTE Oct 20, 2009: <pvl> only called from Graph::Clear() and own destructor
    public void Clear(uint typeMask)
    {
        for (int i = 0, count = m_buckets.Count; i < count; ++i)
        {
            if (m_buckets[i] != null && Match(m_buckets[i].type, typeMask))
            {
                // Destroy all nodes in the bucket
                for (int j = 0; j < BUCKET_SIZE; j++)
                {
                    if (m_buckets[i].nodes[j] != null)
                    {
                        m_buckets[i].nodes[j].OnDestroy();
                        m_buckets[i].nodes[j] = null;
                    }
                }
                m_buckets[i] = null;
            }
        }

        for (int i = 0, numTypes = m_freeBuckets.Count; i < numTypes; ++i)
        {
            uint type = TypeFromTypeIndex(i);
            if (Match(type, typeMask))
            {
                m_freeBuckets[i] = BucketHeader.InvalidNextFreeBucketIdx;
            }
        }
    }

    public uint CreateNode(uint type, Vec3 pos, uint ID)
    {
        int typeIndex = GraphHelpers.TypeIndexFromType(type);
        if (typeIndex < 0)
            return 0;

        if (typeIndex >= m_freeBuckets.Count)
        {
            while (m_freeBuckets.Count <= typeIndex)
                m_freeBuckets.Add(BucketHeader.InvalidNextFreeBucketIdx);
        }

        ushort freeBucketIdx = m_freeBuckets[typeIndex];

        if (freeBucketIdx == BucketHeader.InvalidNextFreeBucketIdx)
        {
            BucketHeader pHeader = new BucketHeader();
            pHeader.type = type;
            pHeader.nextAvailable = 0;
            pHeader.nodes = new GraphNode[BUCKET_SIZE];
            // Initialize free list chain: each slot points to the next
            pHeader.nextFreeChain = new int[BUCKET_SIZE];
            for (int i = 0; i < BUCKET_SIZE - 1; ++i)
                pHeader.nextFreeChain[i] = i + 1;
            pHeader.nextFreeChain[BUCKET_SIZE - 1] = BucketHeader.InvalidNextAvailableIdx;
            pHeader.nextFreeBucketIdx = BucketHeader.InvalidNextFreeBucketIdx;

            freeBucketIdx = (ushort)m_buckets.Count;
            m_buckets.Add(pHeader);
            m_freeBuckets[typeIndex] = freeBucketIdx;
        }

        BucketHeader freeBucket = m_buckets[freeBucketIdx];

        int allocIdx = freeBucket.nextAvailable;
        Debug.Assert(allocIdx < BUCKET_SIZE);

        freeBucket.nextAvailable = freeBucket.nextFreeChain[allocIdx];

        if (freeBucket.nextAvailable == BucketHeader.InvalidNextAvailableIdx)
        {
            m_freeBuckets[typeIndex] = freeBucket.nextFreeBucketIdx;
            freeBucket.nextFreeBucketIdx = BucketHeader.InvalidNextFreeBucketIdx;
        }

        IAISystem_ENavigationType actualType = (IAISystem_ENavigationType)type;
        GraphNode pNode = actualType switch
        {
            IAISystem_ENavigationType.NAV_UNSET => new GraphNode_Unset(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_TRIANGULAR => new GraphNode_Triangular(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN => new GraphNode_WaypointHuman(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_WAYPOINT_3DSURFACE => new GraphNode_Waypoint3DSurface(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_FLIGHT => new GraphNode_Flight(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_VOLUME => new GraphNode_Volume(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_ROAD => new GraphNode_Road(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_SMARTOBJECT => new GraphNode_SmartObject(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_FREE_2D => new GraphNode_Free2D(actualType, pos, ID),
            IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION => new GraphNode_CustomNav(actualType, pos, ID),
            _ => new GraphNode_Unset(actualType, pos, ID),
        };

        freeBucket.nodes[allocIdx] = pNode;

        Debug.Assert(pNode.nRefCount == 0);

        return (uint)((freeBucketIdx << BUCKET_SIZE_SHIFT) + allocIdx) + 1;
    }

    public void DestroyNode(uint index)
    {
        GraphNode pNode = GetNode(index);
        if (pNode == null)
            return;

        // Call OnDestroy to return ID to free pool
        pNode.OnDestroy();

        // Push node onto bucket free list
        int bucketIdx = (int)((index - 1) / BUCKET_SIZE);
        int idxInBucket = (int)((index - 1) % BUCKET_SIZE);
        int typeIndex = GraphHelpers.TypeIndexFromType((uint)pNode.navType);

        BucketHeader pBucket = m_buckets[bucketIdx];
        if (pBucket.nextAvailable == BucketHeader.InvalidNextAvailableIdx)
        {
            pBucket.nextFreeBucketIdx = m_freeBuckets[typeIndex];
            m_freeBuckets[typeIndex] = (ushort)bucketIdx;
        }

        pBucket.nodes[idxInBucket] = null;
        pBucket.nextFreeChain[idxInBucket] = pBucket.nextAvailable;
        pBucket.nextAvailable = idxInBucket;
    }

    public GraphNode GetNode(uint index)
    {
        if (index == 0)
            return null;
        int bucket = (int)((index - 1) >> BUCKET_SIZE_SHIFT);
        if (bucket >= m_buckets.Count || m_buckets[bucket] == null)
            return GetDummyNode();
        int idxInBucket = (int)((index - 1) & (BUCKET_SIZE - 1));
        GraphNode node = m_buckets[bucket].nodes[idxInBucket];
        return node ?? GetDummyNode();
    }

    public nuint NodeMemorySize()
    {
        nuint mem = 0;
        for (int i = 0, count = m_buckets.Count; i < count; ++i)
        {
            if (m_buckets[i] == null)
                continue;
            // Rough estimate: each bucket holds BUCKET_SIZE node references
            mem += (nuint)(BUCKET_SIZE * 64); // approximate per-node overhead
        }
        return mem;
    }

    public void GetMemoryStatistics(ICrySizer pSizer) { /* impl — profiler pass-through */ }

    private class BucketHeader
    {
        public const ushort InvalidNextFreeBucketIdx = 0xffff;
        public const int InvalidNextAvailableIdx = 0xff;

        public uint type;
        public ushort nextFreeBucketIdx;
        public int nextAvailable;
        public GraphNode[] nodes;
        public int[] nextFreeChain; // free list chain within bucket
    }

    private GraphNode GetDummyNode()
    {
        // C++ uses placement new on static buffer; C# returns a singleton
        return s_dummy;
    }
    private static readonly GraphNode s_dummy = new GraphNode_Triangular(IAISystem_ENavigationType.NAV_TRIANGULAR, new Vec3(0, 0, 0), 0xCECECECE);

    private static uint TypeFromTypeIndex(int typeIndex)
    {
        return 1u << typeIndex;
    }

    private static bool Match(uint type, uint mask)
    {
        return (type & mask) != 0;
    }

    private List<BucketHeader> m_buckets = new List<BucketHeader>();
    private List<ushort> m_freeBuckets;
    private int[] m_typeSizes = new int[NAV_TYPE_COUNT];
}
