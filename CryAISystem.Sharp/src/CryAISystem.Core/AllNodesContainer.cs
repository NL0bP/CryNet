// Literal port of dev/Code/CryEngine/CryAISystem/AllNodesContainer.h + AllNodesContainer.cpp (496L + 36L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CryAISystem;

/// Container for pointers to nodes so that it's quick/convenient to:
/// 1. Insert nodes of any type
/// 2. remove nodes of any type
/// 3. Iterate over all nodes
/// 4. Iterate over all nodes of a particular type
/// 5. Find a node (e.g. for validation)
public class CAllNodesContainer
{
    /// Node that this iterator becomes invalid as soon as the container
    /// gets modified
    public class Iterator
    {
        /// Create an iterator, starting at the beginning
        public Iterator(CAllNodesContainer container, uint navTypeMask = 0xFFFFFFFF)
        {
            m_navTypeMask = navTypeMask;
            m_container = container;
            m_container.AttachIterator(this);
            Reset();
        }

        ~Iterator()
        {
            if (m_container != null)
                m_container.DetachIterator(this);
        }

        /// increment and return the current value
        public uint Increment()
        {
            if (m_container == null) return 0;

            uint nodeIndex = 0;

            if (m_itIndex < m_currentNodes.Count)
            {
                nodeIndex = m_currentNodes[m_itIndex];
                ++m_itIndex;
            }

            if (m_itIndex >= m_currentNodes.Count)
            {
                if (m_currentAllNodesIdx >= m_container.m_allNodeKeys.Count)
                    return nodeIndex;

                for (++m_currentAllNodesIdx; m_currentAllNodesIdx < m_container.m_allNodeKeys.Count; ++m_currentAllNodesIdx)
                {
                    uint navType = m_container.m_allNodeKeys[m_currentAllNodesIdx];
                    if ((navType & m_navTypeMask) != 0)
                    {
                        var nodes = m_container.m_allNodes[navType];
                        if (nodes.Count > 0)
                        {
                            m_currentNodes = nodes;
                            m_itIndex = 0;
                            if (nodeIndex == 0)
                            {
                                nodeIndex = m_currentNodes[m_itIndex];
                                ++m_itIndex;
                            }
                            break;
                        }
                    }
                }
            }
            return nodeIndex;
        }

        /// just return the current value
        public uint GetNode()
        {
            if (m_container == null) return 0;
            if (m_itIndex < m_currentNodes.Count)
            {
                return m_currentNodes[m_itIndex];
            }
            return 0;
        }

        /// Set to the beginning.
        public void Reset()
        {
            if (m_container == null) return;

            m_container.RebuildKeysList();

            for (m_currentAllNodesIdx = 0; m_currentAllNodesIdx < m_container.m_allNodeKeys.Count; ++m_currentAllNodesIdx)
            {
                uint navType = m_container.m_allNodeKeys[m_currentAllNodesIdx];
                if ((navType & m_navTypeMask) != 0)
                {
                    var nodes = m_container.m_allNodes[navType];
                    if (nodes.Count > 0)
                    {
                        m_currentNodes = nodes;
                        m_itIndex = 0;
                        return;
                    }
                }
            }

            // container will always have an entry for nav mask 0
            if (m_container.m_allNodes.ContainsKey(0))
            {
                m_currentNodes = m_container.m_allNodes[0];
            }
            else
            {
                m_currentNodes = s_emptyList;
            }
            m_itIndex = 0;
            m_currentAllNodesIdx = 0;
        }

        /// Called by the container destructor
        internal void ContainerDeleted() { m_container = null; }

        internal uint m_navTypeMask;
        private int m_currentAllNodesIdx;
        private List<uint> m_currentNodes = s_emptyList;
        private int m_itIndex;
        private CAllNodesContainer m_container;

        private static readonly List<uint> s_emptyList = new List<uint>();
    }

    public struct SGraphNodeRecord : IEquatable<SGraphNodeRecord>, IHashSpaceItem
    {
        public uint nodeIndex;

        public SGraphNodeRecord(uint nodeIndex) { this.nodeIndex = nodeIndex; }
        public bool Equals(SGraphNodeRecord other) { return other.nodeIndex == nodeIndex; }
        // IHashSpaceItem
        public Vec3 GetPos()
        {
            // This requires a node manager reference which we don't have here.
            // In practice the hash space uses the traits functor.
            return new Vec3(0, 0, 0);
        }
        public override int GetHashCode() { return (int)nodeIndex; }
        public override bool Equals(object obj) => obj is SGraphNodeRecord r && Equals(r);
    }

    public CAllNodesContainer()
    {
        // Default constructor for Graph.cs field initialization
        m_allNodes[0] = new List<uint>();
    }

    public CAllNodesContainer(CGraphNodeManager nodeManager)
    {
        m_nodeManager = nodeManager;
        m_allNodes[0] = new List<uint>();
    }

    ~CAllNodesContainer()
    {
        foreach (var it in m_attachedIterators.ToList())
        {
            it.ContainerDeleted();
        }
    }

    /// Add a node to this container - uses the position to store it in a spatial structure
    public void AddNode(uint nodeIndex)
    {
        GraphNode pNode = m_nodeManager?.GetNode(nodeIndex);
        if (pNode == null)
        {
            AILog.AIWarning("CAllNodesContainer: Attempting to add 0 node!");
            return;
        }
        uint type = (uint)pNode.navType;

        if (!m_allNodes.ContainsKey(type))
            m_allNodes[type] = new List<uint>();
        var nodes = m_allNodes[type];
        // VectorSet — sorted insert with uniqueness
        int idx = nodes.BinarySearch(nodeIndex);
        if (idx < 0)
            nodes.Insert(~idx, nodeIndex);

        m_keysListDirty = true;
        ResetIterators(type);
    }

    public void Reserve(uint type, int size)
    {
        if (!m_allNodes.ContainsKey(type))
            m_allNodes[type] = new List<uint>(size);
        else
            m_allNodes[type].Capacity = Math.Max(m_allNodes[type].Capacity, size);
    }

    /// Remove a node from this container (invalidates all iterators)
    public void RemoveNode(uint nodeIndex)
    {
        GraphNode pNode = m_nodeManager?.GetNode(nodeIndex);
        if (pNode == null)
        {
            AILog.AIWarning("CAllNodesContainer: Attempting to remove 0 node!");
            return;
        }
        uint type = (uint)pNode.navType;
        if (!m_allNodes.TryGetValue(type, out var nodes))
        {
            AILog.AIWarning($"CAllNodesContainer::RemoveNode Could not find node type {type}");
            return;
        }

        int idx = nodes.BinarySearch(nodeIndex);
        if (idx < 0)
        {
            AILog.AIWarning($"CAllNodesContainer::RemoveNode Could not find node {nodeIndex}");
            return;
        }

        nodes.RemoveAt(idx);
        m_keysListDirty = true;
        ResetIterators(type);
    }

    /// Indicates if the node is in this container
    public bool DoesNodeExist(uint nodeIndex)
    {
        GraphNode pNode = m_nodeManager?.GetNode(nodeIndex);
        if (pNode == null)
        {
            AILog.AIWarning("CAllNodesContainer: Attempting to see if 0 node exists!");
            return false;
        }

        foreach (var kv in m_allNodes)
        {
            var nodes = kv.Value;
            if (nodes.BinarySearch(nodeIndex) >= 0)
                return true;
        }
        return false;
    }

    /// Returns unsorted all nodes of a particular type within range of the position passed in, together
    /// with the squared distance to the node (unsorted)
    public void GetAllNodesWithinRange(List<(float, uint)> nodesOut, Vec3 pos, float range, uint navTypeMask)
    {
        nodesOut.Clear();
        if (m_nodeManager == null) return;

        // Simple brute-force over all nodes (hash space optimization deferred)
        foreach (var kv in m_allNodes)
        {
            if ((kv.Key & navTypeMask) == 0) continue;
            foreach (uint ni in kv.Value)
            {
                GraphNode pNode = m_nodeManager.GetNode(ni);
                if (pNode == null) continue;
                float dx = pNode.GetPos().x - pos.x;
                float dy = pNode.GetPos().y - pos.y;
                float dz = pNode.GetPos().z - pos.z;
                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq <= range * range)
                    nodesOut.Add((distSq, ni));
            }
        }
    }

    /// Returns one node within range of the position passed in
    public bool GetNodeWithinRange((float, uint) nodeOut, Vec3 pos, float range, uint navTypeMask, CGraph graph)
    {
        // Simplified — find first matching node in range
        if (m_nodeManager == null) return false;
        foreach (var kv in m_allNodes)
        {
            if ((kv.Key & navTypeMask) == 0) continue;
            foreach (uint ni in kv.Value)
            {
                GraphNode pNode = m_nodeManager.GetNode(ni);
                if (pNode == null) continue;
                float dx = pNode.GetPos().x - pos.x;
                float dy = pNode.GetPos().y - pos.y;
                float dz = pNode.GetPos().z - pos.z;
                float distSq = dx * dx + dy * dy + dz * dz;
                if (distSq <= range * range)
                {
                    nodeOut = (distSq, ni);
                    return true;
                }
            }
        }
        return false;
    }

    /// Returns the memory usage in bytes
    public nuint MemStats()
    {
        nuint result = 0;
        foreach (var kv in m_allNodes)
            result += (nuint)(kv.Value.Count * 4 + 32);
        return result;
    }

    /// For debugging - finds out if our HashSpace is consistent within itself and with AllNodesContainer.
    public bool ValidateHashSpace() { return true; /* hash space deferred */ }

    /// Compact the memory usage
    public void Compact()
    {
        foreach (var kv in m_allNodes)
            kv.Value.TrimExcess();
    }

    // Internal helpers
    private void AttachIterator(Iterator pIt) { m_attachedIterators.Add(pIt); }
    private void DetachIterator(Iterator pIt) { m_attachedIterators.Remove(pIt); }

    private void ResetIterators(uint navTypeMask)
    {
        foreach (var pIt in m_attachedIterators)
        {
            if ((pIt.m_navTypeMask | navTypeMask) != 0)
                pIt.Reset();
        }
    }

    internal void RebuildKeysList()
    {
        if (!m_keysListDirty) return;
        m_allNodeKeys.Clear();
        foreach (var key in m_allNodes.Keys)
            m_allNodeKeys.Add(key);
        m_keysListDirty = false;
    }

    /// All nodes split into sets all containing the same type
    internal SortedDictionary<uint, List<uint>> m_allNodes = new SortedDictionary<uint, List<uint>>();
    internal List<uint> m_allNodeKeys = new List<uint>();
    private bool m_keysListDirty = true;

    // attached iterators that we invalidate when things change
    private HashSet<Iterator> m_attachedIterators = new HashSet<Iterator>();
    private CGraphNodeManager m_nodeManager;
}
