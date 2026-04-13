// Literal port of dev/Code/CryEngine/CryAISystem/Graph.h + Graph.cpp (200L + 1457L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : interface for the CGraph class.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static CryAISystem.CryMath;

namespace CryAISystem;

public enum EPathfinderResult
{
    PATHFINDER_STILLFINDING,
    PATHFINDER_BEAUTIFYINGPATH,
    PATHFINDER_POPNEWREQUEST,
    PATHFINDER_PATHFOUND,
    PATHFINDER_NOPATH,
    PATHFINDER_ABORT,
    PATHFINDER_MAXVALUE
}

public static class GraphHelpers
{
    public static int TypeIndexFromType(uint type)
    {
        int typeIndex;
        for (typeIndex = CGraphNodeManager.NAV_TYPE_COUNT - 1; typeIndex >= 0 && ((1u << typeIndex) & type) == 0; --typeIndex) ;
        return typeIndex;
    }

    public static string StringFromTypeIndex(int typeIndex)
    {
        string[] navTypeStrings = {
            "NAV_UNSET",
            "NAV_TRIANGULAR",
            "NAV_WAYPOINT_HUMAN",
            "NAV_WAYPOINT_3DSURFACE",
            "NAV_FLIGHT",
            "NAV_VOLUME",
            "NAV_ROAD",
            "NAV_SMARTOBJECT",
            "NAV_FREE_2D",
            "NAV_CUSTOM_NAVIGATION",
        };

        if (typeIndex < 0)
            return "<Invalid Nav Type>";
        else
            return navTypeStrings[typeIndex];
    }

    public static string StringFromType(IAISystem_ENavigationType type)
    {
        return StringFromTypeIndex(TypeIndexFromType((uint)type));
    }
}

public class CObstacleRef
{
    protected CWeakRef<CAIObject> m_refAnchor = new CWeakRef<CAIObject>();
    protected int m_vertexIndex;
    protected uint m_nodeIndex;
    protected GraphNode m_pNode;
    protected CSmartObject m_pSmartObject;
    protected CCondition m_pRule;

    public CAIObject GetAnchor() { return m_refAnchor.GetAIObject(); }
    public int GetVertex() { return m_vertexIndex; }
    public uint GetNodeIndex() { return m_nodeIndex; }
    public GraphNode GetNode() { return m_pNode; }
    public CSmartObject GetSmartObject() { return m_pSmartObject; }
    public CCondition GetRule() { return m_pRule; }

    public CObstacleRef() { m_vertexIndex = -1; m_nodeIndex = 0; m_pNode = null; m_pSmartObject = null; m_pRule = null; }
    public CObstacleRef(CObstacleRef other)
    {
        m_refAnchor = other.m_refAnchor;
        m_vertexIndex = other.m_vertexIndex;
        m_nodeIndex = other.m_nodeIndex;
        m_pNode = other.m_pNode;
        m_pSmartObject = other.m_pSmartObject;
        m_pRule = other.m_pRule;
    }
    public CObstacleRef(CWeakRef<CAIObject> refAnchor) { m_refAnchor = refAnchor; m_vertexIndex = -1; m_nodeIndex = 0; m_pNode = null; m_pSmartObject = null; m_pRule = null; }
    public CObstacleRef(int vertexIndex) { m_vertexIndex = vertexIndex; m_nodeIndex = 0; m_pNode = null; m_pSmartObject = null; m_pRule = null; }
    public CObstacleRef(uint nodeIndex, GraphNode pNode) { m_vertexIndex = -1; m_nodeIndex = nodeIndex; m_pNode = pNode; m_pSmartObject = null; m_pRule = null; }
    public CObstacleRef(CSmartObject pSmartObject, CCondition pRule) { m_vertexIndex = -1; m_nodeIndex = 0; m_pNode = null; m_pSmartObject = pSmartObject; m_pRule = pRule; }

    // Graph.cpp — CObstacleRef::GetPos
    public Vec3 GetPos()
    {
        if (m_refAnchor.IsValid())
        {
            return m_refAnchor.GetAIObject().GetPos();
        }
        else if (m_pNode != null)
        {
            return m_pNode.GetPos();
        }
        else if (m_pSmartObject != null && m_pRule != null)
        {
            return new Vec3(0, 0, 0); // m_pSmartObject.GetPos() — deferred until SmartObject port
        }
        else
        {
            Debug.Assert(false);
            return new Vec3(0, 0, 0);
        }
    }

    // Graph.cpp — CObstacleRef::GetApproxRadius
    public float GetApproxRadius()
    {
        Debug.Assert(false);
        return 0.0f;
    }
}

// typedef std::multimap<int64,unsigned> EntranceMap;
public class EntranceMap : SortedDictionary<long, List<uint>> { }
// typedef std::vector<ObstacleData> ListObstacles;
public class ListObstacles : List<ObstacleData> { }
// typedef std::multimap<float, ObstacleData> MultimapRangeObstacles;
public class MultimapRangeObstacles : SortedDictionary<float, List<ObstacleData>> { }
// typedef std::vector<NodeDescriptor> NodeDescBuffer;
public class NodeDescBuffer : List<NodeDescriptor> { }
// typedef std::vector<LinkDescriptor> LinkDescBuffer;
public class LinkDescBuffer : List<LinkDescriptor> { }
// typedef std::list<unsigned> ListNodeIds;
public class ListNodeIds : LinkedList<uint> { }
// typedef std::set<GraphNode*> SetNodes;
public class SetNodes : HashSet<GraphNode> { }
// typedef std::set<const GraphNode*> SetConstNodes;
public class SetConstNodes : HashSet<GraphNode> { }
// typedef std::list<const GraphNode *> ListConstNodes;
public class ListConstNodes : LinkedList<GraphNode> { }
// typedef std::vector<const GraphNode *> VectorConstNodes;
public class VectorConstNodes : List<GraphNode> { }
// typedef std::multimap<float,GraphNode*> CandidateMap;
public class CandidateMap : SortedDictionary<float, List<GraphNode>> { }
// typedef std::multimap<float,unsigned> CandidateIdMap;
public class CandidateIdMap : SortedDictionary<float, List<uint>> { }
// typedef std::set< CObstacleRef > SetObstacleRefs;
public class SetObstacleRefs : HashSet<CObstacleRef> { }
// typedef std::vector<unsigned> VectorConstNodeIndices;
public class VectorConstNodeIndices : List<uint> { }
// typedef VectorMap<unsigned, SCachedPassabilityResult> PassabilityCache;
public class PassabilityCache : SortedDictionary<uint, SCachedPassabilityResult> { }

public static class AStarSearchNode
{
    public static readonly float fInvalidCost = 999999.0f;
}

// PointInTriangle — NAV_TRIANGULAR is replaced by MNM
public static class GraphGlobals
{
    public static bool PointInTriangle(Vec3 pos, GraphNode pNode)
    {
        // NAV_TRIANGULAR is replaced by MNM
        Debug.Assert(false);
        return false;
    }

    public const int BAI_TRI_FILE_VERSION = 54;
}


public class CGraph
{
    // Graph.cpp — CGraph::CGraph()
    public CGraph()
    {
        m_pGraphLinkManager = new CGraphLinkManager();
        m_pGraphNodeManager = new CGraphNodeManager();
        m_allNodes = new CAllNodesContainer(m_pGraphNodeManager);
        m_triangularBBox = new AABB(AABB.RESET);

        m_safeFirstIndex = CreateNewNode((uint)IAISystem_ENavigationType.NAV_UNSET, new Vec3(0, 0, 0), 0);
        m_firstIndex = m_safeFirstIndex;
        m_pFirst = GetNodeManager().GetNode(m_safeFirstIndex);
        m_pSafeFirst = m_pFirst;
        m_pCurrent = m_pFirst;
        m_currentIndex = m_firstIndex;
        m_pCurrent.firstLinkIndex = 0;
    }

    public CGraphLinkManager GetLinkManager() { return m_pGraphLinkManager; }
    public CGraphNodeManager GetNodeManager() { return m_pGraphNodeManager; }

    // Graph.cpp — CGraph::Reset()
    public void Reset()
    {
        ClearMarks();
        RestoreAllNavigation();
    }

    // Graph.cpp — CGraph::ResetIDs()
    public void ResetIDs()
    {
        GraphNode.ResetIDs(GetNodeManager(), m_allNodes, m_pSafeFirst);
    }

    // Graph.cpp — CGraph::Clear()
    public void Clear(uint navTypeMask)
    {
        ClearMarks();
        DeleteGraph(navTypeMask);
        if ((navTypeMask & ((uint)IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN | (uint)IAISystem_ENavigationType.NAV_WAYPOINT_3DSURFACE)) != 0)
        {
            // Clear entrances/exits for matching types
            ClearEntrancesForMask(m_mapEntrances, navTypeMask);
            ClearEntrancesForMask(m_mapExits, navTypeMask);
        }

        GetNodeManager().Clear(navTypeMask);

        if (m_pSafeFirst != null)
        {
            Disconnect(m_safeFirstIndex, false);
        }
        else
        {
            m_safeFirstIndex = CreateNewNode((uint)IAISystem_ENavigationType.NAV_UNSET, new Vec3(0, 0, 0), 0);
            m_pSafeFirst = GetNodeManager().GetNode(m_safeFirstIndex);
        }
        m_pFirst = m_pSafeFirst;
        m_firstIndex = m_safeFirstIndex;
        m_pCurrent = m_pFirst;
        m_currentIndex = m_firstIndex;
    }

    private void ClearEntrancesForMask(EntranceMap map, uint navTypeMask)
    {
        var keysToRemove = new List<(long, int)>();
        foreach (var kv in map)
        {
            for (int i = kv.Value.Count - 1; i >= 0; --i)
            {
                GraphNode pNode = GetNodeManager().GetNode(kv.Value[i]);
                if (pNode != null && ((uint)pNode.navType & navTypeMask) != 0)
                    kv.Value.RemoveAt(i);
            }
        }
    }

    // Graph.cpp — CGraph::ConnectInCm
    public void ConnectInCm(uint oneNodeIndex, uint twoNodeIndex, short radiusOneToTwoCm, short radiusTwoToOneCm,
        ref uint pLinkOneTwo, ref uint pLinkTwoOne)
    {
        GraphNode one = GetNodeManager().GetNode(oneNodeIndex);
        GraphNode two = GetNodeManager().GetNode(twoNodeIndex);

        pLinkOneTwo = 0;
        pLinkTwoOne = 0;

        if (one == two) return;
        if (one == null || two == null) return;

        if (!ValidateNode(oneNodeIndex, false) || !ValidateNode(twoNodeIndex, false))
        {
            AILog.AIError("CGraph::Connect Attempt to connect nodes that aren't created [Code bug]");
            return;
        }

        if ((one == m_pSafeFirst || two == m_pSafeFirst) && m_pSafeFirst.firstLinkIndex != 0)
        {
            AILog.AIWarning("Second link being made to safe/first node");
            return;
        }

        uint linkIndexOne = one.GetLinkTo(GetNodeManager(), GetLinkManager(), two);
        uint linkIndexTwo = two.GetLinkTo(GetNodeManager(), GetLinkManager(), one);

        if ((linkIndexOne == 0) != (linkIndexTwo == 0))
        {
            AILog.AIWarning("Trying to connect links but one is already connected, other isn't");
            return;
        }

        uint linkIndex = linkIndexOne;
        if (linkIndex == 0 && linkIndexTwo != 0)
            linkIndex = linkIndexTwo ^ 1;
        if (linkIndex == 0)
            linkIndex = m_pGraphLinkManager.CreateLink();

        Vec3 midPos = (one.GetPos() + two.GetPos()) * 0.5f;

        if (linkIndexOne == 0)
        {
            if (one.firstLinkIndex == 0)
                one.firstLinkIndex = linkIndex;
            else
            {
                uint lastLink = one.firstLinkIndex;
                uint nextLink2;
                while ((nextLink2 = m_pGraphLinkManager.GetNextLink(lastLink)) != 0)
                    lastLink = nextLink2;
                m_pGraphLinkManager.SetNextLink(lastLink, linkIndex);
            }

            linkIndexOne = linkIndex;
            GetLinkManager().SetNextNode(linkIndex, twoNodeIndex);
            GetLinkManager().SetRadiusInCm(linkIndex, two == m_pSafeFirst ? (short)-100 : radiusOneToTwoCm);
            GetLinkManager().GetEdgeCenter(linkIndex) = midPos;
            two.AddRef();
        }
        else
        {
            if (radiusOneToTwoCm != 0)
                GetLinkManager().ModifyRadiusInCm(linkIndexOne, radiusOneToTwoCm);
        }

        if (linkIndexTwo == 0)
        {
            if (two.firstLinkIndex == 0)
                two.firstLinkIndex = linkIndex ^ 1;
            else
            {
                uint lastLink = two.firstLinkIndex;
                uint nextLink2;
                while ((nextLink2 = m_pGraphLinkManager.GetNextLink(lastLink)) != 0)
                    lastLink = nextLink2;
                m_pGraphLinkManager.SetNextLink(lastLink, linkIndex ^ 1);
            }

            linkIndexTwo = linkIndex ^ 1;
            GetLinkManager().SetNextNode(linkIndex ^ 1, oneNodeIndex);
            GetLinkManager().SetRadiusInCm(linkIndex ^ 1, one == m_pSafeFirst ? (short)-100 : radiusTwoToOneCm);
            GetLinkManager().GetEdgeCenter(linkIndex ^ 1) = midPos;
            one.AddRef();
        }
        else
        {
            if (radiusTwoToOneCm != 0)
                GetLinkManager().ModifyRadiusInCm(linkIndexTwo, radiusTwoToOneCm);
        }

        pLinkOneTwo = linkIndexOne;
        pLinkTwoOne = linkIndexTwo;

        if (m_pSafeFirst.firstLinkIndex != 0)
            return;

        if (one.navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
        {
            uint dummy1 = 0, dummy2 = 0;
            ConnectInCm(m_safeFirstIndex, oneNodeIndex, 10000, -100, ref dummy1, ref dummy2);
            m_pFirst = one;
            m_firstIndex = oneNodeIndex;
        }
        else if (two.navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
        {
            uint dummy1 = 0, dummy2 = 0;
            ConnectInCm(m_safeFirstIndex, twoNodeIndex, 10000, -100, ref dummy1, ref dummy2);
            m_pFirst = two;
            m_firstIndex = twoNodeIndex;
        }
    }

    public void ConnectInCm(uint oneIndex, uint twoIndex, short radiusOneToTwoCm = 10000, short radiusTwoToOneCm = 10000)
    {
        uint dummy1 = 0, dummy2 = 0;
        ConnectInCm(oneIndex, twoIndex, radiusOneToTwoCm, radiusTwoToOneCm, ref dummy1, ref dummy2);
    }

    // Graph.cpp — CGraph::Connect
    public void Connect(uint oneIndex, uint twoIndex, float radiusOneToTwo, float radiusTwoToOne,
        ref uint pLinkOneTwo, ref uint pLinkTwoOne)
    {
        short radiusOneToTwoInCm = NavGraphUtils.InCentimeters(radiusOneToTwo);
        short radiusTwoToOneInCm = NavGraphUtils.InCentimeters(radiusTwoToOne);
        ConnectInCm(oneIndex, twoIndex, radiusOneToTwoInCm, radiusTwoToOneInCm, ref pLinkOneTwo, ref pLinkTwoOne);
    }

    public void Connect(uint oneIndex, uint twoIndex, float radiusOneToTwo = 100.0f, float radiusTwoToOne = 100.0f)
    {
        uint dummy1 = 0, dummy2 = 0;
        Connect(oneIndex, twoIndex, radiusOneToTwo, radiusTwoToOne, ref dummy1, ref dummy2);
    }

    // Graph.cpp — CGraph::Disconnect(nodeIndex, linkId)
    public void Disconnect(uint nodeIndex, uint linkId)
    {
        GraphNode pNode = m_pGraphNodeManager.GetNode(nodeIndex);

        if (!ValidateNode(nodeIndex, false))
        {
            AILog.AIError("CGraph::Disconnect Attempt to disconnect link from node that isn't created [Code bug]");
            return;
        }
        uint otherNodeIndex = GetLinkManager().GetNextNode(linkId);
        GraphNode pOtherNode = GetNodeManager().GetNode(otherNodeIndex);
        if (!ValidateNode(otherNodeIndex, false))
        {
            AILog.AIError("CGraph::Disconnect Attempt to disconnect link from other node that isn't created [Code bug]");
            return;
        }

        pNode.RemoveLinkTo(GetLinkManager(), otherNodeIndex);
        pNode.Release();

        pOtherNode.RemoveLinkTo(GetLinkManager(), nodeIndex);
        pOtherNode.Release();

        GetLinkManager().DestroyLink(linkId);
    }

    // Graph.cpp — CGraph::Disconnect(nodeIndex, bDelete)
    public void Disconnect(uint nodeIndex, bool bDelete = true)
    {
        GraphNode pDisconnected = m_pGraphNodeManager.GetNode(nodeIndex);

        if (!ValidateNode(nodeIndex, false))
        {
            AILog.AIError("CGraph::Disconnect Attempt to disconnect node that isn't created [Code bug]");
            return;
        }

        if (pDisconnected == m_pCurrent)
        {
            if (pDisconnected.firstLinkIndex != 0)
            {
                m_currentIndex = GetLinkManager().GetNextNode(pDisconnected.firstLinkIndex);
                m_pCurrent = GetNodeManager().GetNode(m_currentIndex);
            }
            else
            {
                m_currentIndex = m_safeFirstIndex;
                m_pCurrent = m_pSafeFirst;
            }
        }

        if (m_pFirst == pDisconnected)
        {
            if (pDisconnected.firstLinkIndex != 0)
            {
                m_firstIndex = GetLinkManager().GetNextNode(pDisconnected.firstLinkIndex);
                m_pFirst = GetNodeManager().GetNode(m_firstIndex);
            }
            else
            {
                if (m_pFirst != m_pSafeFirst)
                {
                    m_pFirst = m_pSafeFirst;
                    m_firstIndex = m_safeFirstIndex;
                }
                else
                {
                    m_pFirst = null;
                    m_firstIndex = 0;
                }
            }
        }

        // now disconnect this node from its links
        for (uint link = pDisconnected.firstLinkIndex; link != 0; )
        {
            uint nextLink = GetLinkManager().GetNextLink(link);
            uint nextNodeIndex = GetLinkManager().GetNextNode(link);
            GraphNode pNextNode = GetNodeManager().GetNode(nextNodeIndex);
            pNextNode.RemoveLinkTo(GetLinkManager(), nodeIndex);
            pNextNode.Release();
            pDisconnected.Release();
            GetLinkManager().DestroyLink(link);
            link = nextLink;
        }

        pDisconnected.firstLinkIndex = 0;

        if (pDisconnected.nRefCount != 1)
            AILog.AIWarning("Node reference count is not 1 after disconnecting");

        if (bDelete)
            DeleteNode(nodeIndex);

        if (m_pSafeFirst == null)
            return;

        if (pDisconnected != m_pSafeFirst && m_pSafeFirst.firstLinkIndex == 0)
        {
            uint firstIndex = m_firstIndex;
            if (firstIndex == m_safeFirstIndex)
            {
                if (m_currentIndex == m_safeFirstIndex)
                {
                    if (m_mapEntrances.Count > 0)
                        firstIndex = m_mapEntrances.First().Value[0];
                    else
                        return;
                }
                else
                {
                    firstIndex = m_currentIndex;
                }
            }

            if (firstIndex != 0)
            {
                GraphNode pFirst = GetNodeManager().GetNode(firstIndex);
                if (pFirst.navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
                    ConnectInCm(m_safeFirstIndex, firstIndex, 10000, -100);
            }
        }
    }

    // Graph.cpp — CGraph::ValidateNode
    public bool ValidateNode(uint nodeIndex, bool fullCheck)
    {
        GraphNode pNode = GetNodeManager().GetNode(nodeIndex);

        if (nodeIndex == 0)
            return false;
        if (!m_allNodes.DoesNodeExist(nodeIndex))
            return false;
        if (!fullCheck)
            return true;
        else
            return ValidateNodeFullCheck(pNode);
    }

    // Graph.cpp — CGraph::ValidateNodeFullCheck
    private bool ValidateNodeFullCheck(GraphNode pNode)
    {
        bool result = true;
        AILog.AIAssert(pNode != null);
        int nNonRoadLinks = 0;
        uint nTriLinks = 0;
        for (uint linkId = pNode.firstLinkIndex; linkId != 0; linkId = GetLinkManager().GetNextLink(linkId))
        {
            uint nextNodeIndex = GetLinkManager().GetNextNode(linkId);
            GraphNode next = GetNodeManager().GetNode(nextNodeIndex);
            if (!ValidateNode(nextNodeIndex, false))
                result = false;
            if (next.navType != IAISystem_ENavigationType.NAV_ROAD)
                ++nNonRoadLinks;
            if (next.navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
                ++nTriLinks;
        }
        if (nNonRoadLinks > 50)
        {
            AILog.AIWarning($"Too many non-road links ({nNonRoadLinks}) from node type {pNode.navType} at ({pNode.GetPos().x:F2}, {pNode.GetPos().y:F2}, {pNode.GetPos().z:F2})");
        }
        return result;
    }

    public bool ValidateHashSpace() { return m_allNodes.ValidateHashSpace(); }

    // Graph.cpp — CGraph::RestoreAllNavigation
    public void RestoreAllNavigation()
    {
        CAllNodesContainer.Iterator it = new CAllNodesContainer.Iterator(m_allNodes, 0xFFFFFFFF);
        uint nodeIndex;
        while ((nodeIndex = it.Increment()) != 0)
        {
            GraphNode node = GetNodeManager().GetNode(nodeIndex);
            if (node == null) continue;
            for (uint link = node.firstLinkIndex; link != 0; link = GetLinkManager().GetNextLink(link))
            {
                GetLinkManager().RestoreLink(link);
            }
        }
    }

    // Graph.cpp — CGraph::ReadFromFile
    public bool ReadFromFile(string szName)
    {
        // File reading deferred — requires CCryFile literal port
        return false;
    }

    public CAllNodesContainer GetAllNodes() { return m_allNodes; }

    // Graph.cpp — CGraph::CheckForEmpty
    public bool CheckForEmpty(uint navTypeMask = 0xFFFFFFFF)
    {
        uint count = 0;
        CAllNodesContainer.Iterator it = new CAllNodesContainer.Iterator(m_allNodes, navTypeMask);
        uint nodeIndex;
        while ((nodeIndex = it.Increment()) != 0)
        {
            GraphNode node = GetNodeManager().GetNode(nodeIndex);
            if (node == null) continue;
            ++count;
            AILog.AILogEvent($"Unexpected Node type = {node.navType}, pos = ({node.GetPos().x:F2} {node.GetPos().y:F2} {node.GetPos().z:F2})");
        }
        if (count > 0)
            AILog.AIWarning($"Detected {count} unexpected nodes whilst checking types {navTypeMask}");
        return count == 0;
    }

    // Graph.cpp — CGraph::MarkNode
    public void MarkNode(uint nodeIndex)
    {
        GetNodeManager().GetNode(nodeIndex).mark = 1;
        m_markedNodes.Add(nodeIndex);
    }

    // Graph.cpp — CGraph::ClearMarks
    public void ClearMarks()
    {
        while (m_markedNodes.Count > 0)
        {
            GetNodeManager().GetNode(m_markedNodes[m_markedNodes.Count - 1]).mark = 0;
            m_markedNodes.RemoveAt(m_markedNodes.Count - 1);
        }
    }

    // Graph.cpp — CGraph::SetBBox
    public void SetBBox(Vec3 min, Vec3 max)
    {
        m_triangularBBox.min = min;
        m_triangularBBox.max = max;
    }

    // Graph.cpp — CGraph::InsideOfBBox
    public bool InsideOfBBox(Vec3 pos)
    {
        return pos.x > m_triangularBBox.min.x && pos.x < m_triangularBBox.max.x && pos.y > m_triangularBBox.min.y && pos.y < m_triangularBBox.max.y;
    }

    // Graph.cpp — CGraph::CreateNewNode
    public uint CreateNewNode(uint type, Vec3 pos, uint ID = 0)
    {
        uint nodeIndex = m_pGraphNodeManager.CreateNode(type, pos, ID);
        GraphNode pNode = m_pGraphNodeManager.GetNode(nodeIndex);
        pNode.AddRef();
        m_allNodes.AddNode(nodeIndex);

        if ((IAISystem_ENavigationType)type != IAISystem_ENavigationType.NAV_UNSET)
        {
            CNavRegion pNavRegion = gAIEnv.pNavigation?.GetNavRegion((IAISystem_ENavigationType)type, this);
            // pNavRegion?.NodeCreated(nodeIndex); — Phase 3e+ nav region callbacks
        }

        return nodeIndex;
    }

    // Graph.cpp — CGraph::GetNode
    public GraphNode GetNode(uint index) { return GetNodeManager().GetNode(index); }

    // Graph.cpp — CGraph::MoveNode
    public void MoveNode(uint nodeIndex, Vec3 newPos)
    {
        GraphNode pNode = GetNodeManager().GetNode(nodeIndex);
        if (pNode.GetPos().IsEquivalent(newPos, 0.01f))
            return;
        m_allNodes.RemoveNode(nodeIndex);
        pNode.SetPos(newPos);
        m_allNodes.AddNode(nodeIndex);
    }

    // Graph.cpp — CGraph::GetNodesInRange
    public MapConstNodesDistance GetNodesInRange(MapConstNodesDistance result, Vec3 startPos, float maxDist,
        uint navCapMask, float passRadius, uint startNodeIndex = 0, CAIObject pRequester = null)
    {
        result.Clear();

        GraphNode pStart = GetNodeManager().GetNode(startNodeIndex);

        CAllNodesContainer allNodes = GetAllNodes();
        CAllNodesContainer.Iterator it = new CAllNodesContainer.Iterator(allNodes,
            (uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_VOLUME);
        if (it.GetNode() == 0)
            return result; // no navigation

        uint nodeIndex = 0;
        if (pStart != null && !allNodes.DoesNodeExist(startNodeIndex))
        {
            startNodeIndex = 0;
            pStart = null;
        }

        if (pStart != null)
        {
            if (((uint)pStart.navType & navCapMask) != 0 && startPos.IsEquivalent(pStart.GetPos(), 0.01f))
            {
                nodeIndex = startNodeIndex;
            }
        }

        if (nodeIndex == 0)
            return result;

        float curDist = 0.0f;
        FindNodesWithinRange(result, curDist, maxDist, GetNodeManager().GetNode(nodeIndex), passRadius, pRequester);
        return result;
    }

    public nuint MemStats() { return 0; }
    public nuint NodeMemStats(uint navTypeMask) { return 0; }
    public void GetMemoryStatistics(ICrySizer pSizer) { }

    public struct SBadGraphData
    {
        public enum EType { BAD_PASSABLE, BAD_IMPASSABLE }
        public EType mType;
        public Vec3 mPos1, mPos2;
    }
    public /*mutable*/ List<SBadGraphData> mBadGraphData = new List<SBadGraphData>();

    public GraphNode m_pSafeFirst;
    public uint m_safeFirstIndex;

    public CGraphNodeManager m_pGraphNodeManager;

    // Graph.cpp — CGraph::FindNodesWithinRange (simplified — full dijkstra)
    private void FindNodesWithinRange(MapConstNodesDistance result, float curDist, float maxDist,
        GraphNode pStartNode, float passRadius, CAIObject pRequester)
    {
        if (pStartNode == null) return;

        var openList = new SortedList<float, GraphNode>();
        pStartNode.mark = 1;
        if (!openList.ContainsKey(0.0f))
            openList.Add(0.0f, pStartNode);

        result[pStartNode] = 0.0f;

        while (openList.Count > 0)
        {
            var front = openList.First();
            GraphNode pNode = front.Value;
            float cd = front.Key;
            openList.RemoveAt(0);
            pNode.mark = 0;

            for (uint link = pNode.firstLinkIndex; link != 0; link = GetLinkManager().GetNextLink(link))
            {
                uint nextNodeIndex = GetLinkManager().GetNextNode(link);
                GraphNode pNext = GetNodeManager().GetNode(nextNodeIndex);
                if (pNext == null) continue;

                if (GetLinkManager().GetRadius(link) < passRadius)
                    continue;

                float linkLen = 0.01f + (pNode.GetPos() - pNext.GetPos()).GetLength();
                float totalDist = cd + linkLen;
                if (totalDist <= maxDist)
                {
                    if (result.ContainsKey(pNext))
                    {
                        if (totalDist >= result[pNext])
                            continue;
                        result[pNext] = totalDist;
                    }
                    else
                    {
                        result[pNext] = totalDist;
                    }

                    // Try to add to open list (simplified — may have duplicate keys)
                    while (openList.ContainsKey(totalDist))
                        totalDist += 0.0001f;
                    pNext.mark = 1;
                    openList.Add(totalDist, pNext);
                }
            }
        }
    }

    private bool DbgCheckList(ListNodeIds nodesList) { return true; }

    // Graph.cpp — CGraph::DeleteGraph
    public void DeleteGraph(uint navTypeMask)
    {
        List<uint> nodesToDelete = new List<uint>();
        CAllNodesContainer.Iterator it = new CAllNodesContainer.Iterator(m_allNodes, navTypeMask);
        uint nodeIndex;
        while ((nodeIndex = it.Increment()) != 0)
        {
            nodesToDelete.Add(nodeIndex);
        }

        for (int i = 0; i < nodesToDelete.Count; ++i)
        {
            GraphNode pNode = GetNodeManager().GetNode(nodesToDelete[i]);
            Disconnect(nodesToDelete[i]);
            if (pNode == m_pSafeFirst)
            {
                m_pSafeFirst = null;
                m_safeFirstIndex = 0;
            }
        }

        m_allNodes.Compact();
    }

    // Graph.cpp — CGraph::GetEntrance
    private GraphNode GetEntrance(int nBuildingID, Vec3 pos)
    {
        GraphNode pEntrance = null;
        float mindist = 1000000;
        if (m_mapEntrances.TryGetValue(nBuildingID, out var entrList))
        {
            foreach (var idx in entrList)
            {
                GraphNode n = GetNodeManager().GetNode(idx);
                float d = (n.GetPos() - pos).GetLengthSquared();
                if (d <= mindist) { mindist = d; pEntrance = n; }
            }
        }
        if (m_mapExits.TryGetValue(nBuildingID, out var exitList))
        {
            foreach (var idx in exitList)
            {
                GraphNode n = GetNodeManager().GetNode(idx);
                float d = (n.GetPos() - pos).GetLengthSquared();
                if (d <= mindist) { mindist = d; pEntrance = n; }
            }
        }
        return pEntrance;
    }

    // Graph.cpp — CGraph::GetEntrances
    private bool GetEntrances(int nBuildingID, Vec3 pos, List<uint> nodes)
    {
        if (m_mapEntrances.TryGetValue(nBuildingID, out var entrList))
            nodes.AddRange(entrList);
        if (m_mapExits.TryGetValue(nBuildingID, out var exitList))
            nodes.AddRange(exitList);
        return nodes.Count > 0;
    }

    // Graph.cpp — CGraph::ReadNodes (deferred — requires CCryFile)
    private bool ReadNodes(CCryFile file) { return false; }

    // Graph.cpp — CGraph::DeleteNode
    private void DeleteNode(uint nodeIndex)
    {
        GraphNode pNode = GetNodeManager().GetNode(nodeIndex);

        if (!ValidateNode(nodeIndex, false))
        {
            AILog.AIError("CGraph::DeleteNode Attempting to delete node that doesn't exist [Code bug]");
            return;
        }

        if (pNode.firstLinkIndex != 0)
        {
            AILog.AIWarning("Deleting node but it is still connected - disconnecting");
            Disconnect(nodeIndex, false);
        }

        if (pNode.Release())
        {
            m_allNodes.RemoveNode(nodeIndex);

            m_taggedNodes.RemoveAll(x => x == nodeIndex);
            m_markedNodes.RemoveAll(x => x == nodeIndex);

            m_pGraphNodeManager.DestroyNode(nodeIndex);
        }
    }

    // Graph.cpp — CGraph::Validate
    public bool Validate(string msg, bool checkPassable)
    {
        // Full validation deferred — returns true by default
        return true;
    }

    private uint m_currentIndex;
    private GraphNode m_pCurrent;
    private uint m_firstIndex;
    private GraphNode m_pFirst;

    private /*mutable*/ VectorConstNodeIndices m_markedNodes = new VectorConstNodeIndices();
    private /*mutable*/ VectorConstNodeIndices m_taggedNodes = new VectorConstNodeIndices();

    private CAllNodesContainer m_allNodes;

    private CGraphLinkManager m_pGraphLinkManager;

    private AABB m_triangularBBox;

    private EntranceMap m_mapEntrances = new EntranceMap();
    private EntranceMap m_mapExits = new EntranceMap();
}

// Forward decls / shells
public class CGraphLinkManager
{
    // Full literal port deferred — link manager stores GraphLinkBidirectionalData in pooled arrays.
    public uint GetNextLink(uint linkIndex) { return 0; }
    public uint GetNextNode(uint linkIndex) { return 0; }
    public void SetNextLink(uint linkIndex, uint next) { }
    public void SetNextNode(uint linkIndex, uint nodeIndex) { }
    public float GetRadius(uint linkIndex) { return 0.0f; }
    public void SetRadius(uint linkIndex, float radius) { }
    public void SetRadiusInCm(uint linkIndex, int16 radiusCm) { }
    public void ModifyRadiusInCm(uint linkIndex, int16 radiusCm) { }
    public ref Vec3 GetEdgeCenter(uint linkIndex) { return ref _edgeCenterDummy; }
    private Vec3 _edgeCenterDummy;
    public void SetExposure(uint linkIndex, float exposure) { }
    public void SetMaxWaterDepth(uint linkIndex, float depth) { }
    public void SetMinWaterDepth(uint linkIndex, float depth) { }
    public int16 GetMaxWaterDepthInCm(uint linkIndex) { return 0; }
    public void SetSimple(uint linkIndex, bool simple) { }
    public void SetStartIndex(uint linkIndex, uint startIdx) { }
    public void SetEndIndex(uint linkIndex, uint endIdx) { }
    public void RestoreLink(uint linkIndex) { }
    public void DestroyLink(uint linkIndex) { }
    public uint CreateLink() { return 0; }
}
public class CCondition { }
public class NodeDescriptor
{
    public uint ID;
    public int navType;
    public Vec3 pos;
    public int type;
    public Vec3 dir;
    public Vec3 up;
    public int index;
    public bool bRemovable;
}
public class LinkDescriptor
{
    public long nSourceNode;
    public long nTargetNode;
    public float fMaxPassRadius;
    public uint nStartIndex;
    public uint nEndIndex;
    public Vec3 vEdgeCenter;
    public float fExposure;
    public float fMaxWaterDepth;
    public float fMinWaterDepth;
    public bool bSimplePassabilityCheck;
}
public class CCryFile
{
    public bool Open(string name, string mode) { return false; /* binary file I/O not ported */ }
    public void Close() { }
    public void ReadType<T>(ref T val) { }
    public void ReadType<T>(T[] arr, int count) { }
}

// ObstacleDataDesc — serialisation-only struct from GraphStructures.h
public class ObstacleDataDesc
{
    public Vec3 vPos;
    public Vec3 vDir;
    public float fApproxRadius;
    public byte flags;
    public byte approxHeight;
}

// typedef std::map<const GraphNode*, float> MapConstNodesDistance;
public class MapConstNodesDistance : Dictionary<GraphNode, float> { }

// SVolumeHideSpot
public struct SVolumeHideSpot { public Vec3 pos; public Vec3 dir; }

// Helper struct
public struct SMarkClearer : IDisposable
{
    private CGraph m_pGraph;
    public SMarkClearer(CGraph pGraph) { m_pGraph = pGraph; m_pGraph.ClearMarks(); }
    public void Dispose() { m_pGraph?.ClearMarks(); }
}
