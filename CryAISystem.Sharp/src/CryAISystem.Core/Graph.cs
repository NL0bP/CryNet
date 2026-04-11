// Literal port of dev/Code/CryEngine/CryAISystem/Graph.h
// Graph.cpp impl (1457L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : interface for the CGraph class.

using System.Collections.Generic;

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
        for (typeIndex = 9 - 1; typeIndex >= 0 && ((1u << typeIndex) & type) == 0; --typeIndex) ;
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

    public Vec3 GetPos() { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public float GetApproxRadius() { return 0; /* impl in .cpp */ }
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


public class CGraph
{
    public CGraph() { /* impl in .cpp */ }
    // ~CGraph();

    public CGraphLinkManager GetLinkManager() { return m_pGraphLinkManager; }
    public CGraphNodeManager GetNodeManager() { return m_pGraphNodeManager; }

    public void Reset() { /* impl in .cpp */ }
    public void ResetIDs() { /* impl in .cpp */ }
    public void Clear(uint navTypeMask) { /* impl in .cpp */ }

    public void ConnectInCm(uint oneIndex, uint twoIndex, short radiusOneToTwoCm, short radiusTwoToOneCm,
        ref uint pLinkOneTwo, ref uint pLinkTwoOne) { /* impl in .cpp */ }
    public void ConnectInCm(uint oneIndex, uint twoIndex, short radiusOneToTwoCm = 10000, short radiusTwoToOneCm = 10000) { /* overload — impl in .cpp */ }

    public void Connect(uint oneIndex, uint twoIndex, float radiusOneToTwo, float radiusTwoToOne,
        ref uint pLinkOneTwo, ref uint pLinkTwoOne) { /* impl in .cpp */ }
    public void Connect(uint oneIndex, uint twoIndex, float radiusOneToTwo = 100.0f, float radiusTwoToOne = 100.0f) { /* overload — impl in .cpp */ }

    public void Disconnect(uint nodeIndex, bool bDelete = true) { /* impl in .cpp */ }
    public void Disconnect(uint nodeIndex, uint linkId) { /* impl in .cpp */ }

    public bool Validate(string msg, bool checkPassable) { return false; /* impl in .cpp */ }

    public bool ValidateNode(uint nodeIndex, bool fullCheck) { return false; /* impl in .cpp */ }
    public bool ValidateHashSpace() { return false; /* impl in .cpp */ }

    public void RestoreAllNavigation() { /* impl in .cpp */ }

    public bool ReadFromFile(string szName) { return false; /* impl in .cpp */ }

    public CAllNodesContainer GetAllNodes() { return m_allNodes; }

    public bool CheckForEmpty(uint navTypeMask = 0xFFFFFFFF) { return false; /* impl in .cpp */ }

    public void MarkNode(uint nodeIndex) { /* impl in .cpp */ }
    public void ClearMarks() { /* impl in .cpp */ }

    public void SetBBox(Vec3 min, Vec3 max) { /* impl in .cpp */ }
    public bool InsideOfBBox(Vec3 pos) { return false; /* impl in .cpp */ }

    public uint CreateNewNode(uint type, Vec3 pos, uint ID = 0) { return 0; /* impl in .cpp */ }
    public GraphNode GetNode(uint index) { return null; /* impl in .cpp */ }

    public void MoveNode(uint nodeIndex, Vec3 newPos) { /* impl in .cpp */ }

    public MapConstNodesDistance GetNodesInRange(MapConstNodesDistance result, Vec3 startPos, float maxDist,
        uint navCapMask, float passRadius, uint startNodeIndex = 0, CAIObject pRequester = null) { return result; /* impl in .cpp */ }

    public nuint MemStats() { return 0; /* impl in .cpp */ }
    public nuint NodeMemStats(uint navTypeMask) { return 0; /* impl in .cpp */ }
    public void GetMemoryStatistics(ICrySizer pSizer) { /* impl in .cpp */ }

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

    private void FindNodesWithinRange(MapConstNodesDistance result, float curDist, float maxDist,
        GraphNode pNode, float passRadius, CAIObject pRequester) { /* impl in .cpp */ }

    private bool DbgCheckList(ListNodeIds nodesList) { return false; /* impl in .cpp */ }

    public void DeleteGraph(uint navTypeMask) { /* impl in .cpp */ }

    private GraphNode GetEntrance(int nBuildingID, Vec3 pos) { return null; /* impl in .cpp */ }
    private bool GetEntrances(int nBuildingID, Vec3 pos, List<uint> nodes) { return false; /* impl in .cpp */ }
    private bool ReadNodes(CCryFile file) { return false; /* impl in .cpp */ }
    private void DeleteNode(uint nodeIndex) { /* impl in .cpp */ }

    private bool ValidateNodeFullCheck(GraphNode pNode) { return false; /* impl in .cpp */ }

    private uint m_currentIndex;
    private GraphNode m_pCurrent;
    private uint m_firstIndex;
    private GraphNode m_pFirst;

    private /*mutable*/ VectorConstNodeIndices m_markedNodes = new VectorConstNodeIndices();
    private /*mutable*/ VectorConstNodeIndices m_taggedNodes = new VectorConstNodeIndices();

    private CAllNodesContainer m_allNodes = new CAllNodesContainer();

    private CGraphLinkManager m_pGraphLinkManager;

    private AABB m_triangularBBox;

    private EntranceMap m_mapEntrances = new EntranceMap();
    private EntranceMap m_mapExits = new EntranceMap();
    // friend class CFlightNavRegion;
    // friend class CVolumeNavRegion;
}

// Forward decls / shells
public class CGraphLinkManager { }
public class CAllNodesContainer { }
public class CCondition { }
public class NodeDescriptor { }
public class LinkDescriptor { }
public class SCachedPassabilityResult { }

// Helper struct
public struct SMarkClearer
{
    private CGraph m_pGraph;
    public SMarkClearer(CGraph pGraph) { m_pGraph = pGraph; m_pGraph.ClearMarks(); }
    // Dispose-equivalent of dtor — m_pGraph.ClearMarks();
}
