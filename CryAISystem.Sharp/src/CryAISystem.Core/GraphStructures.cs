// Literal port of dev/Code/CryEngine/CryAISystem/GraphStructures.h + GraphStructures.cpp (767L + 104L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CryAISystem;

public static class NavGraphUtils
{
    public static int16 InCentimeters(float distance)
    {
        return (int16)(distance * 100.0f);
    }

    public static float InMeters(int16 distanceInCm)
    {
        return (float)distanceInCm * 0.01f;
    }
}

// ObstacleIndexVector — fixed-capacity 3-element inline vector of int
public struct ObstacleIndexVector
{
    private int m_idx0, m_idx1, m_idx2;

    public ObstacleIndexVector(int dummy = 0) { m_idx0 = -1; m_idx1 = -1; m_idx2 = -1; }

    public int Count
    {
        get { return (m_idx2 < 0) ? (-m_idx2 - 1) : 3; }
    }

    public bool IsEmpty => m_idx2 == -1;

    public void Clear() { m_idx0 = -1; m_idx1 = -1; m_idx2 = -1; }

    public void PushBack(int val)
    {
        Debug.Assert(val >= 0);
        Debug.Assert(m_idx2 < 0);
        m_idx2--;
        SetIdx(Count - 1, val);
    }

    public int this[int idx]
    {
        get { Debug.Assert(idx >= 0 && idx < Count); return GetIdx(idx); }
    }

    public void Swap(ref ObstacleIndexVector rhs)
    {
        int t;
        t = m_idx0; m_idx0 = rhs.m_idx0; rhs.m_idx0 = t;
        t = m_idx1; m_idx1 = rhs.m_idx1; rhs.m_idx1 = t;
        t = m_idx2; m_idx2 = rhs.m_idx2; rhs.m_idx2 = t;
    }

    private int GetIdx(int i) => i switch { 0 => m_idx0, 1 => m_idx1, 2 => m_idx2, _ => throw new IndexOutOfRangeException() };
    private void SetIdx(int i, int v) { switch (i) { case 0: m_idx0 = v; break; case 1: m_idx1 = v; break; case 2: m_idx2 = v; break; } }
}

// GraphLinkBidirectionalData — bitfield-based struct (internal to CGraphLinkManager)
public struct GraphLinkBidirectionalData
{
    public struct DirectionalData
    {
        public int maxRadiusInCm;         // signed 16-bit
        public int origMaxRadiusInCm;     // signed 16-bit
        public uint nextLinkIndex;        // 23 bits
        public uint simplePassabilityCheck; // 1 bit
        public uint waterDepthInCmLow;    // 8 bits unsigned
        public int waterDepthInCmHigh;    // 8 bits signed
        public uint nextNodeIndex;        // 20 bits
        public uint startIndex;           // 2 bits
        public uint endIndex;             // 2 bits
    }

    public DirectionalData[] directionalData;
    public float fExposure;
    public Vec3 vEdgeCenter;

    public GraphLinkBidirectionalData(int dummy = 0)
    {
        directionalData = new DirectionalData[2];
        fExposure = 0.0f;
        vEdgeCenter = new Vec3(0.0f, 0.0f, 0.0f);
    }
}

public struct SCachedPassabilityResult
{
    public nuint spatialHash;
    public bool walkableResult;

    public SCachedPassabilityResult(nuint hash = 0, bool walkable = false)
    {
        spatialHash = hash;
        walkableResult = walkable;
    }

    public void Reset(nuint hash, bool walkable)
    {
        spatialHash = hash;
        walkableResult = walkable;
    }
}

// STriangularNavData
public struct STriangularNavData
{
    public ObstacleIndexVector vertices;
    public byte isForbidden;        // :1
    public byte isForbiddenDesigner; // :1

    public STriangularNavData(int dummy = 0)
    {
        vertices = new ObstacleIndexVector(0);
        isForbidden = 0;
        isForbiddenDesigner = 0;
    }

    public double GetDegeneracyValue() { return 0.0; /* impl requires link manager / vertex access — Phase 3e deferred detail */ }
    public void MakeAntiClockwise() { /* impl deferred */ }
    public bool IsAntiClockwise() { return false; /* impl deferred */ }
    public double GetCross(CGraphLinkManager linkManager, Vec3 vCutStart, Vec3 vDir, uint theLink) { return 0.0; /* impl deferred */ }
}

// Waypoint link type enums
public enum EWaypointLinkType : int
{
    WLT_AUTO_PASS = -100,
    WLT_EDITOR_PASS = -200,
    WLT_EDITOR_IMPASS = -300,
    WLT_AUTO_IMPASS = -400,
    WLT_UNKNOWN_TYPE = -500,
}

public enum EWaypointNodeType : int
{
    WNT_UNSET = 0,
    WNT_WAYPOINT = 1,
    WNT_HIDE = 2,
    WNT_ENTRYEXIT = 3,
    WNT_EXITONLY = 4,
    WNT_HIDESECONDARY = 5,
}

// SWaypointNavData
public class SWaypointNavData
{
    public IVisArea pArea;

    public static EWaypointLinkType GetLinkTypeFromRadius(float radius)
    {
        if (Math.Abs(radius - (int)EWaypointLinkType.WLT_AUTO_PASS) < 0.001f)
            return EWaypointLinkType.WLT_AUTO_PASS;
        else if (Math.Abs(radius - (int)EWaypointLinkType.WLT_EDITOR_PASS) < 0.001f)
            return EWaypointLinkType.WLT_EDITOR_PASS;
        else if (Math.Abs(radius - (int)EWaypointLinkType.WLT_EDITOR_IMPASS) < 0.001f)
            return EWaypointLinkType.WLT_EDITOR_IMPASS;
        else if (Math.Abs(radius - (int)EWaypointLinkType.WLT_AUTO_IMPASS) < 0.001f)
            return EWaypointLinkType.WLT_AUTO_IMPASS;
        else
            return EWaypointLinkType.WLT_UNKNOWN_TYPE;
    }

    public static EWaypointLinkType GetLinkTypeFromRadiusInCm(int16 radius)
    {
        if (radius == ((int)EWaypointLinkType.WLT_AUTO_PASS * 100))
            return EWaypointLinkType.WLT_AUTO_PASS;
        else if (radius == (int)EWaypointLinkType.WLT_EDITOR_PASS * 100)
            return EWaypointLinkType.WLT_EDITOR_PASS;
        else if (radius == (int)EWaypointLinkType.WLT_EDITOR_IMPASS * 100)
            return EWaypointLinkType.WLT_EDITOR_IMPASS;
        else if (radius == (int)EWaypointLinkType.WLT_AUTO_IMPASS * 100)
            return EWaypointLinkType.WLT_AUTO_IMPASS;
        else
            return EWaypointLinkType.WLT_UNKNOWN_TYPE;
    }

    public int nBuildingID;
    public EWaypointNodeType type;

    // if it's a hide point then store the direction the hide point faces.
    public Vec3 dir;
    // Stores the local up (z) axis of the point - used in nav regions where type = 1 (3D surface)
    public Vec3 up;

    public SWaypointNavData()
    {
        pArea = null;
        nBuildingID = -1;
        type = EWaypointNodeType.WNT_UNSET;
        dir = new Vec3(0, 0, 0);
        up = new Vec3(0, 0, 1);
    }
}

public struct SFlightNavData
{
    public int nSpanIdx;
}

public struct SVolumeNavData
{
    public int nVolimeIdx;
}

public struct SRoadNavData
{
    /// total width of the road at this point
    public float fRoadWidth;
}

public class SSmartObjectNavData
{
    public CSmartObjectClass pClass;
    public SmartObjectHelper pHelper;
    public CSmartObject pSmartObject;
}

public struct SLayeredMeshNavData
{
    public const uint s_polyIndexWidth = 20;
    public const uint s_modifIndexWidth = 8;
    public const uint s_agentTypeWidth = 4;

    public const uint s_polyIndexShift = 0;
    public const uint s_modifIndexShift = s_polyIndexWidth;
    public const uint s_agentTypeShift = s_polyIndexWidth + s_modifIndexWidth;

    public const uint s_polyIndexMask = (1u << (int)s_polyIndexWidth) - 1;
    public const uint s_modifIndexMask = (1u << (int)s_modifIndexWidth) - 1;
    public const uint s_agentTypeMask = (1u << (int)s_agentTypeWidth) - 1;

    public uint polygonIndex;   // :s_polyIndexWidth
    public uint navModifIndex;  // :s_modifIndexWidth
    public uint agentType;      // :s_agentTypeWidth

    // NOTE Feb 18, 2009: <pvl> unpack members from a single int (useful for deserialisation)
    public void SetFromPacked(uint packed)
    {
        polygonIndex = (packed >> (int)s_polyIndexShift) & s_polyIndexMask;
        navModifIndex = (packed >> (int)s_modifIndexShift) & s_modifIndexMask;
        agentType = (packed >> (int)s_agentTypeShift) & s_agentTypeMask;
    }
}

// SCustomNavData
public class SCustomNavData
{
    public delegate float CostFunction(object node1Data, object node2Data, PathfindingHeuristicProperties pathFindProperties);

    public object pCustomData;
    public CostFunction pCostFunction;
    public uint16 customId;

    public SCustomNavData()
    {
        pCustomData = null;
        pCostFunction = null;
        customId = 0;
    }
}

// Forward decl shells
public class CSmartObjectClass { }
// SmartObjectHelper already declared in PipeUser.cs
public class PathfindingHeuristicProperties { }
public interface IVisArea
{
    // Phase 3e shell — I3DEngine::GetVisAreaFromPos returns IVisArea*
}

// EObstacleFlags
[Flags]
public enum EObstacleFlags : byte
{
    OBSTACLE_COLLIDABLE = 1 << 0,
    OBSTACLE_HIDEABLE = 1 << 1,
}

// ObstacleData — literal port of GraphStructures.h + GraphStructures.cpp
public class ObstacleData
{
    public Vec3 vPos;
    public Vec3 vDir;
    /// this radius is approximate - it is estimated during link generation. if -ve it means
    /// that it shouldn't be used (i.e. object is significantly non-circular)
    public float fApproxRadius;
    public byte flags;
    public byte approxHeight; // height in 4.4 fixed point format.

    public void SetCollidable(bool state) { if (state) flags |= (byte)EObstacleFlags.OBSTACLE_COLLIDABLE; else flags &= unchecked((byte)~(byte)EObstacleFlags.OBSTACLE_COLLIDABLE); }
    public void SetHideable(bool state) { if (state) flags |= (byte)EObstacleFlags.OBSTACLE_HIDEABLE; else flags &= unchecked((byte)~(byte)EObstacleFlags.OBSTACLE_HIDEABLE); }
    public bool IsCollidable() { return (flags & (byte)EObstacleFlags.OBSTACLE_COLLIDABLE) != 0; }
    public bool IsHideable() { return (flags & (byte)EObstacleFlags.OBSTACLE_HIDEABLE) != 0; }

    /// Sets the approximate height and does the necessary conversion.
    public void SetApproxHeight(float h) { approxHeight = (byte)Math.Clamp(h * (1 << 4), 0.0f, 255.0f); }
    /// Returns the approximate height and does the necessary conversion.
    public float GetApproxHeight() { return (float)approxHeight / (float)(1 << 4); }

    public List<GraphNode> GetNavNodes() { return navNodes; }
    public void ClearNavNodes() { navNodes.Clear(); }

    // GraphStructures.cpp — Serialize
    public void Serialize(TSerialize ser)
    {
        ser.Value("vPos", ref vPos);
        ser.Value("vDir", ref vDir);
        ser.Value("fApproxRadius", ref fApproxRadius);
        ser.Value("flags", ref flags);
        if (ser.IsReading())
        {
            navNodes.Clear();
        }
    }

    // GraphStructures.cpp — SetNavNodes
    public void SetNavNodes(List<GraphNode> nodes)
    {
        navNodes = new List<GraphNode>(nodes);
    }

    // GraphStructures.cpp — AddNavNode
    public void AddNavNode(GraphNode pNode)
    {
        navNodes.Add(pNode);
    }

    /// Note - this is used during triangulation where we can't have two objects
    /// sitting on top of each other - even if they have different directions etc
    public bool EqualsPosition(ObstacleData other)
    {
        return (Math.Abs(vPos.x - other.vPos.x) < 0.001f) && (Math.Abs(vPos.y - other.vPos.y) < 0.001f);
    }

    public ObstacleData() : this(new Vec3(0, 0, 0), new Vec3(0, 0, 0)) { }
    public ObstacleData(Vec3 pos, Vec3 dir)
    {
        vPos = pos;
        vDir = dir;
        fApproxRadius = -1.0f;
        approxHeight = 0;
        flags = (byte)EObstacleFlags.OBSTACLE_COLLIDABLE;
    }

    // pointer to the triangular navigation nodes that contains us (result of GetEnclosing).
    private List<GraphNode> navNodes = new List<GraphNode>();
    private bool needToEvaluateNavNodes;
}

// typedef std::vector<ObstacleData> Obstacles;
public class Obstacles : List<ObstacleData> { }

//====================================================================
// GraphNode
// In principle this is a constant structure in that the pathfinder
// doesn't modify it. However, in practice during pathfinding it's
// convenient to cache some info along with the node - hence the
// mutable members
//====================================================================
public class GraphNode
{
    /// navType is const so that nodes can be stored in lists of their respective types
    public readonly IAISystem_ENavigationType navType;

    /// The position of the node
    public Vec3 GetPos() { return pos; }

    public virtual STriangularNavData GetTriangularNavData() { Debug.Assert(navType == IAISystem_ENavigationType.NAV_TRIANGULAR); return ((GraphNode_Triangular)this).navData; }
    public virtual SWaypointNavData GetWaypointNavData()
    {
        Debug.Assert(navType == IAISystem_ENavigationType.NAV_WAYPOINT_3DSURFACE || navType == IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN);
        return ((GraphNode_Waypoint)this).navData;
    }
    public virtual SFlightNavData GetFlightNavData() { Debug.Assert(navType == IAISystem_ENavigationType.NAV_FLIGHT); return ((GraphNode_Flight)this).navData; }
    public virtual SVolumeNavData GetVolumeNavData() { Debug.Assert(navType == IAISystem_ENavigationType.NAV_VOLUME); return ((GraphNode_Volume)this).navData; }
    public virtual SRoadNavData GetRoadNavData() { Debug.Assert(navType == IAISystem_ENavigationType.NAV_ROAD); return ((GraphNode_Road)this).navData; }
    public virtual SSmartObjectNavData GetSmartObjectNavData() { Debug.Assert(navType == IAISystem_ENavigationType.NAV_SMARTOBJECT); return ((GraphNode_SmartObject)this).navData; }

    /// Used as a flag for costs that aren't calculated yet
    public static readonly float fInvalidCost = 999999.0f;

    // Returns the link index that goes to destNode. -1 if no link found
    public int GetLinkIndex(CGraphNodeManager nodeManager, CGraphLinkManager linkManager, GraphNode destNode)
    {
        int linkIndexIndex = -1;
        int i = 0;
        for (uint link = firstLinkIndex; link != 0; link = linkManager.GetNextLink(link), ++i)
            if (nodeManager.GetNode(linkManager.GetNextNode(link)) == destNode)
                linkIndexIndex = i;
        return linkIndexIndex;
    }

    public uint GetLinkTo(CGraphNodeManager nodeManager, CGraphLinkManager linkManager, GraphNode destNode)
    {
        uint correctLink = 0;
        for (uint link = firstLinkIndex; link != 0; link = linkManager.GetNextLink(link))
            if (nodeManager.GetNode(linkManager.GetNextNode(link)) == destNode)
                correctLink = link;
        return correctLink;
    }

    public int GetLinkCount(CGraphLinkManager linkManager)
    {
        int count = 0;
        for (uint link = firstLinkIndex; link != 0; link = linkManager.GetNextLink(link))
            ++count;
        return count;
    }

    public void RemoveLinkTo(CGraphLinkManager linkManager, uint nodeIndex)
    {
        uint prevLink = 0;
        for (uint link = firstLinkIndex; link != 0; )
        {
            uint nextLink = linkManager.GetNextLink(link);
            if (linkManager.GetNextNode(link) == nodeIndex)
            {
                if (prevLink == 0)
                    firstLinkIndex = nextLink;
                else
                    linkManager.SetNextLink(prevLink, nextLink);
            }
            prevLink = link;
            link = nextLink;
        }
    }

    /// Returns the maximum radius from all the links
    public float GetMaxLinkRadius(CGraphLinkManager linkManager)
    {
        float r = -float.MaxValue;
        for (uint link = firstLinkIndex; link != 0; link = linkManager.GetNextLink(link))
            if (linkManager.GetRadius(link) > r)
                r = linkManager.GetRadius(link);
        return r;
    }

    public uint FindNewLink(CGraphLinkManager linkManager)
    {
        // Finds the next available link slot — placeholder
        return 0;
    }

    public bool Release()
    {
        Debug.Assert(nRefCount >= 1);
        --nRefCount;
        if (nRefCount == 0)
            return true;
        return false;
    }

    public void AddRef()
    {
        Debug.Assert(nRefCount < ushort.MaxValue);
        ++nRefCount;
    }

    // this resets our IDs and assigns a unique ID to all the nodes in the container.
    // Should just be called on navigation graph saving. pNodeForID1 is a special node
    // that will have ID 1 assigned to it
    public static void ResetIDs(CGraphNodeManager nodeManager, CAllNodesContainer allNodes, GraphNode pNodeForID1)
    {
        AILog.AIAssert(pNodeForID1 != null);

        var itAll = new CAllNodesContainer.Iterator(allNodes, 0xffffffff);

        freeIDs.Clear();

        maxID = 1;
        GraphNode pCurrent;
        while ((pCurrent = nodeManager.GetNode(itAll.Increment())) != null)
        {
            if (pCurrent == pNodeForID1)
                pCurrent.ID = 1;
            else
                pCurrent.ID = ++maxID;
        }
    }

    public nuint MemStats() { return (nuint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(GraphNode)); }

    public static void ClearFreeIDs() { freeIDs.Clear(); freeIDs.TrimExcess(); }

    // Protected constructor — This must ONLY be called via the graph node creation function.
    // If _ID == 0 then an ID will be assigned automatically
    public GraphNode() { } // default for dummy node
    public GraphNode(IAISystem_ENavigationType type, Vec3 inpos, uint _ID)
    {
        navType = type;
        pos = inpos;
        firstLinkIndex = 0;
        mark = 0;
        nRefCount = 0;
        if (_ID == 0)
        {
            if (freeIDs.Count == 0)
            {
                ID = maxID + 1;
            }
            else
            {
                ID = freeIDs[freeIDs.Count - 1];
                freeIDs.RemoveAt(freeIDs.Count - 1);
            }
        }
        else
        {
            ID = _ID;
            freeIDs.Remove(ID);
        }
        if (ID > maxID)
            maxID = ID;
    }

    // ~GraphNode() equivalent — call when destroying
    public void OnDestroy()
    {
        freeIDs.Add(ID);
    }

    // Links are stored in a linked-list - this is the index of the next link for the nodes at each end.
    public uint firstLinkIndex;

    // unique ID - preserved when saving.
    internal uint ID;

    private Vec3 pos;

    /// The pool of free IDs - populated when nodes are deleted
    internal static List<uint> freeIDs = new List<uint>();
    /// The highest ID currently in use (0 is invalid), so maxID+1 is a valid unique ID
    internal static uint maxID = 0;

    public ushort nRefCount;

    /// General marker used to help traversing the graph of nodes.
    public byte mark;

    internal void SetPos(Vec3 newPos) { pos = newPos; }
}

// Concrete GraphNode subtypes — each carries its own navData

public class GraphNode_Unset : GraphNode
{
    public GraphNode_Unset(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
}

public class GraphNode_Triangular : GraphNode
{
    public STriangularNavData navData;
    public GraphNode_Triangular(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { navData = new STriangularNavData(0); }
    public override STriangularNavData GetTriangularNavData() { return navData; }
}

public class GraphNode_Waypoint : GraphNode
{
    public SWaypointNavData navData = new SWaypointNavData();
    public GraphNode_Waypoint(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
    public override SWaypointNavData GetWaypointNavData() { return navData; }
}

public class GraphNode_WaypointHuman : GraphNode_Waypoint
{
    public GraphNode_WaypointHuman(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
}

public class GraphNode_Waypoint3DSurface : GraphNode_Waypoint
{
    public GraphNode_Waypoint3DSurface(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
}

public class GraphNode_Flight : GraphNode
{
    public SFlightNavData navData;
    public GraphNode_Flight(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
    public override SFlightNavData GetFlightNavData() { return navData; }
}

public class GraphNode_Volume : GraphNode
{
    public SVolumeNavData navData;
    public GraphNode_Volume(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
    public override SVolumeNavData GetVolumeNavData() { return navData; }
}

public class GraphNode_Road : GraphNode
{
    public SRoadNavData navData;
    public GraphNode_Road(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
    public override SRoadNavData GetRoadNavData() { return navData; }
}

public class GraphNode_SmartObject : GraphNode
{
    public SSmartObjectNavData navData = new SSmartObjectNavData();
    public GraphNode_SmartObject(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
    public override SSmartObjectNavData GetSmartObjectNavData() { return navData; }
}

public class GraphNode_Free2D : GraphNode
{
    public GraphNode_Free2D(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }
}

public class GraphNode_CustomNav : GraphNode
{
    public SCustomNavData navData = new SCustomNavData();

    public GraphNode_CustomNav(IAISystem_ENavigationType type, Vec3 pos, uint id) : base(type, pos, id) { }

    public void SetCustomData(object customData) { navData.pCustomData = customData; }
    public void SetCustomId(uint16 customId) { navData.customId = customId; }
    public object GetCustomData() { return navData.pCustomData; }
    public uint16 GetCustomId() { return navData.customId; }
    public void SetCostFunction(SCustomNavData.CostFunction pCostFunction) { navData.pCostFunction = pCostFunction; }
    public float CustomLinkCostFactor(object node1Data, object node2Data, PathfindingHeuristicProperties pathFindProperties)
    {
        float ret = float.MaxValue;
        if (navData.pCostFunction != null)
        {
            ret = navData.pCostFunction(node1Data, node2Data, pathFindProperties);
        }
        return ret;
    }
}
