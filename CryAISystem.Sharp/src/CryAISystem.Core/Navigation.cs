// Literal port of dev/Code/CryEngine/CryAISystem/Navigation.h
// Navigation.cpp impl (1340L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : interface for the CGraph class.

using System.Collections.Generic;
using CryAISystem.Walkability;

namespace CryAISystem;

public class SpecialArea
{
    // (MATT) Note that this must correspond exactly to the enum in the Editor's Navigation.h {2009/06/17}
    public enum EType
    {
        TYPE_WAYPOINT_HUMAN,
        TYPE_VOLUME,
        TYPE_FLIGHT,
        TYPE_WATER,
        TYPE_WAYPOINT_3DSURFACE,
        TYPE_FREE_2D,
        TYPE_TRIANGULATION,
        TYPE_LAYERED_NAV_MESH,
        TYPE_FLIGHT2,
    }

    public void SetPolygon(ListPositions polygon)
    {
        lstPolygon = polygon; CalcAABB();
    }
    public ListPositions GetPolygon() { return lstPolygon; }

    public AABB GetAABB() { return aabb; }

    public EType type; // : 6
    public EWaypointConnections waypointConnections; // : 6
    public EAILightLevel lightLevel; // : 4
    public short nBuildingID;

    public float fMinZ;
    public float fMaxZ;
    public float fHeight;
    public float fNodeAutoConnectDistance;

    public bool bAltered; // : 1
    public bool bCritterOnly; // : 1

    public SpecialArea()
    {
        nBuildingID = -1;
        type = EType.TYPE_WAYPOINT_HUMAN;
        waypointConnections = EWaypointConnections.WPCON_DESIGNER_NONE;
        fNodeAutoConnectDistance = 0.0f;
        bAltered = false;
        bCritterOnly = false;
        lightLevel = EAILightLevel.AILL_NONE;
        fMinZ = float.MaxValue;
        fMaxZ = -float.MaxValue;
        fHeight = 0.0f;
        aabb = new AABB();
        aabb.Reset();
    }

    private ListPositions lstPolygon = new ListPositions();
    private AABB aabb;

    private void CalcAABB()
    {
        aabb.Reset();
        for (int i = 0; i < lstPolygon.Count; ++i)
            aabb.Add(lstPolygon[i]);
    }
}

// typedef std::map<string, int> SpecialAreaNames;
public class SpecialAreaNames : SortedDictionary<string, int> { }
// typedef std::vector<SpecialArea> SpecialAreas;
public class SpecialAreas : List<SpecialArea> { }
// typedef std::vector<int16> FreeSpecialAreaIDs;
public class FreeSpecialAreaIDs : List<short> { }


public class SExtraLinkCostShape
{
    public SExtraLinkCostShape(ListPositions shape, AABB aabb, float costFactor) { this.shape = shape; this.aabb = aabb; this.costFactor = costFactor; this.origCostFactor = costFactor; }
    public SExtraLinkCostShape(ListPositions shape, float costFactor)
    {
        this.shape = shape;
        this.costFactor = costFactor;
        this.origCostFactor = costFactor;
        aabb = new AABB(); aabb.Reset();
        for (int i = 0; i < shape.Count; ++i)
            aabb.Add(shape[i]);
    }
    public ListPositions shape;
    public AABB aabb;
    public float costFactor;
    public float origCostFactor;
}

// typedef std::map<string, SExtraLinkCostShape> ExtraLinkCostShapeMap;
public class ExtraLinkCostShapeMap : SortedDictionary<string, SExtraLinkCostShape> { }


public struct CutEdgeIdx
{
    public int idx1;
    public int idx2;

    public CutEdgeIdx(int i1, int i2) { idx1 = i1; idx2 = i2; }
}

// typedef std::vector<CutEdgeIdx> NewCutsVector;
public class NewCutsVector : List<CutEdgeIdx> { }

// FIXME: these should probably be members of their respective classes
public static class NavigationFileHelpers
{
    public static void ReadArea(CCryBufferedFileReader file, int version, ref string name, SpecialArea sa) { /* impl in .cpp */ }
    public static bool ReadForbiddenArea(CCryBufferedFileReader file, int version, CAIShape shape) { return false; /* impl in .cpp */ }
    public static bool ReadPolygonArea(CCryBufferedFileReader file, int version, ref string name, ListPositions pts) { return false; /* impl in .cpp */ }
    public static void ReadExtraLinkCostArea(CCryBufferedFileReader file, int version, ref string name, SExtraLinkCostShape shape) { /* impl in .cpp */ }
}


public class CNavigation : INavigation
{
    public CNavigation(ISystem pSystem) { /* impl in .cpp */ }
    // ~CNavigation();

    // INavigation
    public virtual uint GetPath(string szPathName, Vec3[] points, uint maxpoints) { return 0; /* impl in .cpp */ }
    public virtual float GetNearestPointOnPath(string szPathName, Vec3 vPos, out Vec3 vResult, out bool bLoopPath, out float totalLength)
    { vResult = vPos; bLoopPath = false; totalLength = 0; return 0; /* impl in .cpp */ }
    public virtual void GetPointOnPathBySegNo(string szPathName, out Vec3 vResult, float segNo) { vResult = new Vec3(0, 0, 0); /* impl in .cpp */ }
    public virtual bool IsSegmentValid(uint navCap, float rad, Vec3 posFrom, ref Vec3 posTo, out IAISystem_ENavigationType navTypeFrom)
    { navTypeFrom = IAISystem_ENavigationType.NAV_UNSET; return false; /* impl in .cpp */ }
    //~INavigation

    public SpecialAreas GetSpecialAreas() { return m_specialAreas; }

    public SpecialArea GetSpecialArea(Vec3 pos, SpecialArea.EType areaType) { return null; /* impl in .cpp */ }
    public SpecialArea GetSpecialArea(int buildingID) { return null; /* impl in .cpp */ }
    public SpecialArea GetSpecialArea(string name) { return null; /* impl in .cpp */ }
    public SpecialArea GetSpecialAreaNearestPos(Vec3 pos, SpecialArea.EType areaType) { return null; /* impl in .cpp */ }
    public string GetSpecialAreaName(int buildingID) { return ""; /* impl in .cpp */ }

    // typedef std::vector<std::pair<string, const SpecialArea*> > VolumeRegions;
    public class VolumeRegions : List<(string, SpecialArea)> { }

    public void GetVolumeRegions(VolumeRegions volumeRegions) { /* impl in .cpp */ }
    public ShapeMap GetDesignerPaths() { return m_mapDesignerPaths; }

    public bool GetDesignerPath(string szName, SShape path) { return false; /* impl in .cpp */ }

    public bool Init() { return false; /* impl in .cpp */ }
    public void Reset(EResetReason reason) { /* impl in .cpp */ }
    public void ShutDown() { /* impl in .cpp */ }
    public void FlushSystemNavigation(bool bDeleteAll) { /* impl in .cpp */ }
    public void OnMissionLoaded() { /* impl in .cpp */ }
    public void LoadNavigationData(string szLevel, string szMission) { /* impl in .cpp */ }

    public float GetDynamicLinkConnectionTimeModifier() { return 0; /* impl in .cpp */ }
    public void BumpDynamicLinkConnectionUpdateTime(float modifier, nuint durationFrames) { /* impl in .cpp */ }

    public void Serialize(TSerialize ser) { /* impl in .cpp */ }

    public void ReadAreasFromFile(CCryBufferedFileReader file, int fileVersion) { /* impl in .cpp */ }
    public void ReadAreasFromFile_Old(CCryBufferedFileReader file, int fileVersion) { /* impl in .cpp */ }

    public void OffsetAllAreas(Vec3 additionalOffset) { /* impl in .cpp */ }

    public void Update(CTimeValue currentTime, float frameTime) { /* impl in .cpp */ }
    public void UpdateNavRegions() { /* impl in .cpp */ }

    public enum ENavDataState { NDS_UNSET, NDS_OK, NDS_BAD }
    public ENavDataState GetNavDataState() { return m_navDataState; }

    public void FlushAllAreas() { /* impl in .cpp */ }
    public void FlushSpecialAreas() { /* impl in .cpp */ }

    public void InsertSpecialArea(string name, SpecialArea sa) { /* impl in .cpp */ }
    public void EraseSpecialArea(string name) { /* impl in .cpp */ }

    public string GetNavigationShapeName(int nBuildingID) { return ""; /* impl in .cpp */ }
    public bool DoesNavigationShapeExists(string szName, EnumAreaType areaType, bool road = false) { return false; /* impl in .cpp */ }
    public bool CreateNavigationShape(SNavigationShapeParams parameters) { return false; /* impl in .cpp */ }
    public void DeleteNavigationShape(string szName) { /* impl in .cpp */ }
    public void DisableModifier(string name) { /* impl in .cpp */ }

    public bool IsPointInForbiddenRegion(Vec3 pos, bool checkAutoGenRegions = true) { return false; }
    public bool IsPointInForbiddenRegion(Vec3 pos, out CAIShape ppShape, bool checkAutoGenRegions) { ppShape = null; return false; }
    public bool IsPointInWaterAreas(Vec3 pt) { return false; /* impl in .cpp */ }
    public static bool IsPointInSpecialArea(Vec3 pos, SpecialArea sa) { return false; /* impl in .cpp */ }
    public bool IsPointInTriangulationAreas(Vec3 pos) { return false; /* impl in .cpp */ }

    public virtual bool IntersectsForbidden(Vec3 vStart, Vec3 vEnd, out Vec3 vClosestPoint, string nameToSkip = null, Vec3? pNormal = null,
        EIFMode mode = EIFMode.IF_AREASBOUNDARIES, bool bForceNormalOutwards = false)
    { vClosestPoint = vEnd; return false; }
    public virtual bool IntersectsForbidden(Vec3 vStart, Vec3 vEnd, float radius, EIFMode mode = EIFMode.IF_AREASBOUNDARIES) { return false; }
    public bool IntersectsSpecialArea(Vec3 start, Vec3 end, out Vec3 closestPoint, SpecialArea.EType type) { closestPoint = end; return false; /* impl in .cpp */ }

    public virtual bool IsPathForbidden(Vec3 start, Vec3 end) { return false; }
    public virtual bool IsPointForbidden(Vec3 pos, float tol, out Vec3 pNormal) { pNormal = new Vec3(0, 0, 0); return false; }
    public virtual Vec3 GetPointOutsideForbidden(ref Vec3 pos, float distance, Vec3? startPos = null) { return pos; /* impl in .cpp */ }

    public bool GetBuildingInfo(int nBuildingID, out SBuildingInfo info) { info = new SBuildingInfo(); return false; /* impl in .cpp */ }
    public bool IsPointInBuilding(Vec3 pos, int nBuildingID) { return false; /* impl in .cpp */ }

    public virtual string GetNearestPathOfTypeInRange(IAIObject requester, Vec3 pos, float range, int type, float devalue, bool useStartNode) { return ""; /* impl in .cpp */ }

    public void DisableNavigationInBrokenRegion(List<Vec3> outline) { /* impl in .cpp */ }

    public virtual void ModifyNavCostFactor(string navModifierName, float factor) { }
    public virtual void GetVolumeRegionFiles(string szLevel, string szMission, List<string> filenames) { /* impl in .cpp */ }

    public CNavRegion GetNavRegion(IAISystem_ENavigationType type, CGraph pGraph) { return null; /* impl in .cpp */ }
    public List<float> Get3DPassRadii() { return m_3DPassRadii; }
    public IAISystem_ENavigationType CheckNavigationType(Vec3 pos, ref int nBuildingID, uint navCapMask) { return IAISystem_ENavigationType.NAV_UNSET; /* impl in .cpp */ }

    public void GetMemoryStatistics(ICrySizer pSizer) { /* impl in .cpp */ }
    public nuint GetMemoryUsage() { return 0; /* impl in .cpp */ }

#if CRYAISYSTEM_DEBUG
    public void DebugDraw() { /* impl in .cpp */ }
#endif

    private ENavDataState m_navDataState;

    private List<float> m_3DPassRadii = new List<float>();

    private ShapeMap m_mapDesignerPaths = new ShapeMap();
    private SpecialAreas m_specialAreas = new SpecialAreas();
    private FreeSpecialAreaIDs m_freeSpecialAreaIDs = new FreeSpecialAreaIDs();
    private SpecialAreaNames m_specialAreaNames = new SpecialAreaNames();

    private uint m_nNumBuildings;

    private struct SValidationErrorMarker
    {
        public SValidationErrorMarker(string msg, Vec3 pos, OBB obb, ColorB col) { this.msg = msg; this.pos = pos; this.obb = obb; this.col = col; }
        public SValidationErrorMarker(string msg, Vec3 pos, ColorB col) { this.msg = msg; this.pos = pos; this.col = col; obb = new OBB(); /* SetOBBfromAABB in .cpp */ }
        public Vec3 pos;
        public OBB obb;
        public string msg;
        public ColorB col;
    }

    private List<SValidationErrorMarker> m_validationErrorMarkers = new List<SValidationErrorMarker>();

    private CTriangulator m_pTriangulator;
    private List<Tri> m_vTriangles = new List<Tri>();
    private VARRAY m_vVertices = new VARRAY();

    private float m_dynamicLinkUpdateTimeBump;
    private nuint m_dynamicLinkUpdateTimeBumpDuration;
    private nuint m_dynamicLinkUpdateTimeBumpElapsed;
}

// Forward decls / shells
public interface INavigation
{
    uint GetPath(string szPathName, Vec3[] points, uint maxpoints);
    float GetNearestPointOnPath(string szPathName, Vec3 vPos, out Vec3 vResult, out bool bLoopPath, out float totalLength);
    void GetPointOnPathBySegNo(string szPathName, out Vec3 vResult, float segNo);
    bool IsSegmentValid(uint navCap, float rad, Vec3 posFrom, ref Vec3 posTo, out IAISystem_ENavigationType navTypeFrom);

    enum EIFMode { IF_AREASBOUNDARIES, IF_AREAS, IF_BOUNDARIES }
}
public enum EIFMode { IF_AREASBOUNDARIES, IF_AREAS, IF_BOUNDARIES }
public enum EnumAreaType { AREATYPE_PATH, AREATYPE_FORBIDDEN, AREATYPE_NAVIGATIONMODIFIER, AREATYPE_OCCLUSION_PLANE }
public enum EWaypointConnections { WPCON_DESIGNER_NONE = 0, WPCON_DESIGNER_PARTIAL = 1, WPCON_DESIGNER_AUTOMATIC = 2 }
public enum EResetReason { RESET_INTERNAL = 0, RESET_LOAD_LEVEL, RESET_UNLOAD_LEVEL, RESET_ENTER_GAME, RESET_EXIT_GAME, RESET_INTERNAL_LOAD }
public class SBuildingInfo { }
public class SNavigationShapeParams { }
public class CTriangulator { }
public class Tri { }
public class VARRAY : List<int> { }
public class CCryBufferedFileReader { }
