// Literal port of dev/Code/CryEngine/CryAISystem/Navigation.h + Navigation.cpp (150L + 1340L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : interface for the CGraph class.

using System;
using System.Collections.Generic;
using System.Linq;
using static CryAISystem.CryMath;
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

    public EType type;
    public EWaypointConnections waypointConnections;
    public EAILightLevel lightLevel;
    public short nBuildingID;

    public float fMinZ;
    public float fMaxZ;
    public float fHeight;
    public float fNodeAutoConnectDistance;

    public bool bAltered;
    public bool bCritterOnly;

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

// Free-standing file reading helpers — Navigation.cpp lines 213-364
public static class NavigationFileHelpers
{
    private const int maxForbiddenNameLen = 1024;

    //====================================================================
    // ReadArea — Navigation.cpp lines 213-273
    //====================================================================
    public static void ReadArea(CCryBufferedFileReader file, int version, ref string name, SpecialArea sa)
    {
        uint nameLen;
        file.ReadType(out nameLen);
        char[] tmpName = new char[nameLen + 1];
        file.ReadRaw(tmpName, (int)nameLen);
        name = new string(tmpName, 0, (int)nameLen);

        long type = 0;
        file.ReadType(out type);
        sa.type = (SpecialArea.EType)type;

        long waypointConnections = 0;
        file.ReadType(out waypointConnections);
        sa.waypointConnections = (EWaypointConnections)waypointConnections;

        byte altered = 0;
        file.ReadType(out altered);
        sa.bAltered = altered != 0;
        file.ReadType(out sa.fHeight);
        if (version <= 16)
        {
            float junk;
            file.ReadType(out junk);
        }
        file.ReadType(out sa.fNodeAutoConnectDistance);
        file.ReadType(out sa.fMaxZ);
        file.ReadType(out sa.fMinZ);

        int nBuildingID;
        file.ReadType(out nBuildingID);
        sa.nBuildingID = (short)nBuildingID;

        if (version >= 18)
        {
            byte lightLevel = 0;
            file.ReadType(out lightLevel);
            sa.lightLevel = (EAILightLevel)lightLevel;
        }

        if (version >= 23)
        {
            byte critterOnly = 0;
            file.ReadType(out critterOnly);
            sa.bCritterOnly = critterOnly != 0;
        }

        // now the area itself
        ListPositions pts = new ListPositions();
        uint ptsSize;
        file.ReadType(out ptsSize);

        for (uint iPt = 0; iPt < ptsSize; ++iPt)
        {
            Vec3 pt;
            file.ReadType(out pt);
            pts.Add(pt);
        }
        sa.SetPolygon(pts);
    }

    //====================================================================
    // ReadPolygonArea — Navigation.cpp lines 278-302
    //====================================================================
    public static bool ReadPolygonArea(CCryBufferedFileReader file, int version, ref string name, ListPositions pts)
    {
        uint nameLen = (uint)maxForbiddenNameLen;
        file.ReadType(out nameLen);
        if (nameLen >= maxForbiddenNameLen)
        {
            AILog.AIWarning("Excessive forbidden area name length - AI loading failed");
            return false;
        }
        char[] tmpName = new char[nameLen + 1];
        file.ReadRaw(tmpName, (int)nameLen);
        name = new string(tmpName, 0, (int)nameLen);

        uint ptsSize;
        file.ReadType(out ptsSize);

        for (uint iPt = 0; iPt < ptsSize; ++iPt)
        {
            Vec3 pt;
            file.ReadType(out pt);
            pts.Add(pt);
        }
        return true;
    }

    //====================================================================
    // ReadForbiddenArea — Navigation.cpp lines 307-336
    //====================================================================
    public static bool ReadForbiddenArea(CCryBufferedFileReader file, int version, CAIShape shape)
    {
        uint nameLen = (uint)maxForbiddenNameLen;
        file.ReadType(out nameLen);
        if (nameLen >= maxForbiddenNameLen)
        {
            AILog.AIWarning("Excessive forbidden area name length - AI loading failed");
            return false;
        }
        char[] tmpName = new char[nameLen + 1];
        file.ReadRaw(tmpName, (int)nameLen);
        shape.SetName(new string(tmpName, 0, (int)nameLen));

        uint ptsSize;
        file.ReadType(out ptsSize);

        ShapePointContainer pts = shape.GetPoints();
        pts.Clear();

        for (uint i = 0; i < ptsSize; ++i)
        {
            Vec3 pt;
            file.ReadType(out pt);
            pts.Add(pt);
        }

        // Call build AABB since the point container was filled directly.
        shape.BuildAABB();

        return true;
    }

    //====================================================================
    // ReadExtraLinkCostArea — Navigation.cpp lines 341-364
    //====================================================================
    public static void ReadExtraLinkCostArea(CCryBufferedFileReader file, int version, ref string name, SExtraLinkCostShape shape)
    {
        uint nameLen;
        file.ReadType(out nameLen);
        char[] tmpName = new char[nameLen + 1];
        file.ReadRaw(tmpName, (int)nameLen);
        name = new string(tmpName, 0, (int)nameLen);

        file.ReadType(out shape.origCostFactor);
        shape.costFactor = shape.origCostFactor;
        Vec3 aabbMin, aabbMax;
        file.ReadType(out aabbMin);
        file.ReadType(out aabbMax);
        shape.aabb = new AABB(); shape.aabb.Reset();
        shape.aabb.Add(aabbMin); shape.aabb.Add(aabbMax);

        uint ptsSize;
        file.ReadType(out ptsSize);

        for (uint iPt = 0; iPt < ptsSize; ++iPt)
        {
            Vec3 pt;
            file.ReadType(out pt);
            shape.shape.Add(pt);
        }
    }
}


public class CNavigation : INavigation
{
    // Navigation.cpp — CNavigation::CNavigation
    public CNavigation(ISystem pSystem)
    {
        m_navDataState = ENavDataState.NDS_UNSET;
        m_nNumBuildings = 0;
        m_pTriangulator = null;
        m_dynamicLinkUpdateTimeBump = 1.0f;
        m_dynamicLinkUpdateTimeBumpDuration = 0;
        m_dynamicLinkUpdateTimeBumpElapsed = 0;
    }

    // INavigation
    // Navigation.cpp — CNavigation::GetPath
    public virtual uint GetPath(string szPathName, Vec3[] points, uint maxpoints)
    {
        SShape pathShape = new SShape();
        uint count = 0;

        if (GetDesignerPath(szPathName, pathShape))
        {
            for (int i = 0; i < pathShape.shape.Count && count < maxpoints; ++i)
            {
                points[count] = pathShape.shape[i];
                ++count;
            }
        }

        return count;
    }

    // Navigation.cpp — CNavigation::GetNearestPointOnPath
    public virtual float GetNearestPointOnPath(string szPathName, Vec3 vPos, out Vec3 vResult, out bool bLoopPath, out float totalLength)
    {
        vResult = new Vec3(0, 0, 0);
        bLoopPath = false;
        totalLength = 0;
        float result = -1.0f;

        SShape pathShape = new SShape();
        if (!GetDesignerPath(szPathName, pathShape) || pathShape.shape.Count == 0)
            return result;

        float dist = float.MaxValue;
        bool bFound = false;
        float segNo = 0.0f;
        float howmanypath = 0.0f;

        Vec3 vPointOnLine = new Vec3(0, 0, 0);
        float lengthTmp = 0.0f;

        for (int ci = 0; ci < pathShape.shape.Count - 1; ++ci)
        {
            Vec3 cur = pathShape.shape[ci];
            Vec3 next = pathShape.shape[ci + 1];

            lengthTmp += (cur - next).GetLength();

            Lineseg seg = new Lineseg(cur, next);
            float t;
            float d = Distance_Point_Lineseg(vPos, seg, out t);
            if (d < dist)
            {
                dist = d;
                bFound = true;
                result = segNo + t;
                vPointOnLine = seg.GetPoint(t);
            }
            segNo += 1.0f;
            howmanypath += 1.0f;
        }

        if (howmanypath == 0.0f)
            return result;

        if (!bFound)
        {
            segNo = 0.0f;
            for (int ci = 0; ci < pathShape.shape.Count; ++ci)
            {
                float d = (vPos - pathShape.shape[ci]).GetLength();
                if (d < dist)
                {
                    dist = d;
                    bFound = true;
                    result = segNo;
                    vPointOnLine = pathShape.shape[ci];
                }
                segNo += 1.0f;
            }
        }

        vResult = vPointOnLine;

        Vec3 first = pathShape.shape[0];
        Vec3 last = pathShape.shape[pathShape.shape.Count - 1];
        if ((first - last).GetLength() < 0.0001f)
            bLoopPath = true;

        totalLength = lengthTmp;

        return result * 100.0f / howmanypath;
    }

    // Navigation.cpp — CNavigation::GetPointOnPathBySegNo
    public virtual void GetPointOnPathBySegNo(string szPathName, out Vec3 vResult, float segNo)
    {
        vResult = new Vec3(0, 0, 0);

        SShape pathShape = new SShape();
        if (segNo < 0f || segNo > 100f || !GetDesignerPath(szPathName, pathShape))
            return;

        int size = pathShape.shape.Count;
        if (size == 0) return;
        if (size == 1) { vResult = pathShape.shape[0]; return; }

        float totalLength = 0.0f;
        for (int ci = 0; ci < size - 1; ++ci)
            totalLength += (pathShape.shape[ci + 1] - pathShape.shape[ci]).GetLength();

        float segLength = totalLength * segNo / 100.0f;
        float currentLength = 0.0f;

        for (int ci = 0; ci < size - 1; ++ci)
        {
            Vec3 curPoint = pathShape.shape[ci];
            Vec3 currentSeg = pathShape.shape[ci + 1] - curPoint;
            float currentSegLength = currentSeg.GetLength();

            if (currentLength + currentSegLength > segLength)
            {
                vResult = curPoint;
                if (currentSegLength > 0.0003f)
                    vResult = vResult + ((segLength - currentLength) / currentSegLength) * currentSeg;
                return;
            }
            currentLength += currentSegLength;
        }
        vResult = pathShape.shape[size - 1];
    }

    // Navigation.cpp — CNavigation::IsSegmentValid
    public virtual bool IsSegmentValid(uint navCap, float rad, Vec3 posFrom, ref Vec3 posTo, out IAISystem_ENavigationType navTypeFrom)
    {
        int nBuildingID = -1;
        navTypeFrom = CheckNavigationType(posFrom, ref nBuildingID, navCap);

        if (navTypeFrom == IAISystem_ENavigationType.NAV_TRIANGULAR)
        {
            if (IsPathForbidden(posFrom, posTo))
                return false;
            Vec3 normalDummy;
            if (IsPointForbidden(posTo, rad, out normalDummy))
                return false;
        }
        return true;
    }

    public SpecialAreas GetSpecialAreas() { return m_specialAreas; }

    // Navigation.cpp — CNavigation::GetSpecialArea (by pos, type)
    public SpecialArea GetSpecialArea(Vec3 pos, SpecialArea.EType areaType)
    {
        for (int i = 0; i < m_specialAreas.Count; ++i)
        {
            SpecialArea sa = m_specialAreas[i];
            if (sa.type == areaType && IsPointInSpecialArea(pos, sa))
                return sa;
        }
        return null;
    }

    // Navigation.cpp — CNavigation::GetSpecialArea (by buildingID)
    public SpecialArea GetSpecialArea(int buildingID)
    {
        if (buildingID >= 0 && buildingID < m_specialAreas.Count)
            return m_specialAreas[buildingID];
        return null;
    }

    // Navigation.cpp — CNavigation::GetSpecialArea (by name)
    public SpecialArea GetSpecialArea(string name)
    {
        if (m_specialAreaNames.TryGetValue(name, out int buildingID))
            return GetSpecialArea(buildingID);
        return null;
    }

    // Navigation.cpp — CNavigation::GetSpecialAreaNearestPos
    public SpecialArea GetSpecialAreaNearestPos(Vec3 pos, SpecialArea.EType areaType)
    {
        SpecialArea direct = GetSpecialArea(pos, areaType);
        if (direct != null) return direct;

        float bestSq = float.MaxValue;
        SpecialArea result = null;

        for (int i = 0; i < m_specialAreas.Count; ++i)
        {
            SpecialArea sa = m_specialAreas[i];
            if (sa.type == areaType)
            {
                if (sa.fHeight > 0.00001f && (pos.z < sa.fMinZ || pos.z > sa.fMaxZ))
                    continue;

                // Simplified distance calculation
                float dx = pos.x - sa.GetAABB().GetCenter().x;
                float dy = pos.y - sa.GetAABB().GetCenter().y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestSq)
                {
                    bestSq = distSq;
                    result = sa;
                }
            }
        }
        return result;
    }

    // Navigation.cpp — CNavigation::GetSpecialAreaName
    public string GetSpecialAreaName(int buildingID)
    {
        foreach (var kv in m_specialAreaNames)
        {
            if (kv.Value == buildingID)
                return kv.Key;
        }
        return "<Unknown>";
    }

    public class VolumeRegions : List<(string, SpecialArea)> { }

    // Navigation.cpp — CNavigation::GetVolumeRegions
    public void GetVolumeRegions(VolumeRegions volumeRegions)
    {
        volumeRegions.Clear();
        for (int i = 0; i < m_specialAreas.Count; ++i)
        {
            SpecialArea sa = m_specialAreas[i];
            if (sa.type == SpecialArea.EType.TYPE_VOLUME)
                volumeRegions.Add((GetSpecialAreaName(sa.nBuildingID), sa));
        }
    }

    public ShapeMap GetDesignerPaths() { return m_mapDesignerPaths; }

    // Navigation.cpp — CNavigation::GetDesignerPath
    public bool GetDesignerPath(string szName, SShape path)
    {
        if (m_mapDesignerPaths.TryGetValue(szName, out SShape found))
        {
            path.shape = found.shape;
            path.aabb = found.aabb;
            path.navType = found.navType;
            path.type = found.type;
            path.closed = found.closed;
            path.devalueTime = found.devalueTime;
            return true;
        }
        return false;
    }

    // Navigation.cpp — CNavigation::Init
    public bool Init() { m_nNumBuildings = 0; return true; }

    // Navigation.cpp — CNavigation::Reset
    public void Reset(EResetReason reason)
    {
        if (reason == EResetReason.RESET_ENTER_GAME)
            m_validationErrorMarkers.Clear();

        for (int i = 0; i < m_specialAreas.Count; ++i)
            m_specialAreas[i].bAltered = false;

        foreach (var kv in m_mapDesignerPaths)
            kv.Value.devalueTime = 0;
    }

    // Navigation.cpp — CNavigation::ShutDown
    public void ShutDown()
    {
        FlushAllAreas();
        m_pTriangulator = null;
    }

    // Navigation.cpp — CNavigation::FlushSystemNavigation
    public void FlushSystemNavigation(bool bDeleteAll)
    {
        FlushSpecialAreas();
        if (gAIEnv.pGraph != null)
        {
            if (bDeleteAll)
            {
                // reconstruct
                gAIEnv.pGraph = new CGraph();
            }
            else
            {
                gAIEnv.pGraph.Clear(0xFFFFFFFF); // NAVMASK_ALL
                gAIEnv.pGraph.ResetIDs();
            }
        }
    }

    public void OnMissionLoaded() { }

    // Navigation.cpp — CNavigation::LoadNavigationData
    public void LoadNavigationData(string szLevel, string szMission)
    {
        m_nNumBuildings = 0;
        m_navDataState = ENavDataState.NDS_OK;
    }

    private static readonly nuint DynamicLinkConnectionBumpDelayFrames = 10;

    // Navigation.cpp — CNavigation::GetDynamicLinkConnectionTimeModifier
    public float GetDynamicLinkConnectionTimeModifier()
    {
        if (m_dynamicLinkUpdateTimeBumpElapsed >= DynamicLinkConnectionBumpDelayFrames &&
            m_dynamicLinkUpdateTimeBumpElapsed < m_dynamicLinkUpdateTimeBumpDuration + DynamicLinkConnectionBumpDelayFrames)
            return m_dynamicLinkUpdateTimeBump;
        return 1.0f;
    }

    // Navigation.cpp — CNavigation::BumpDynamicLinkConnectionUpdateTime
    public void BumpDynamicLinkConnectionUpdateTime(float modifier, nuint durationFrames)
    {
        m_dynamicLinkUpdateTimeBump = modifier;
        m_dynamicLinkUpdateTimeBumpDuration = durationFrames;
        m_dynamicLinkUpdateTimeBumpElapsed = 0;
    }

    public void Serialize(TSerialize ser) { }

    // Navigation.cpp — CNavigation::Update
    public void Update(CTimeValue currentTime, float frameTime)
    {
        if (m_dynamicLinkUpdateTimeBumpElapsed <= m_dynamicLinkUpdateTimeBumpDuration + DynamicLinkConnectionBumpDelayFrames)
            ++m_dynamicLinkUpdateTimeBumpElapsed;

        UpdateNavRegions();

        foreach (var kv in m_mapDesignerPaths)
            kv.Value.devalueTime = Math.Max(0.0f, kv.Value.devalueTime - frameTime);
    }

    // Navigation.cpp — CNavigation::UpdateNavRegions
    public void UpdateNavRegions() { }

    public enum ENavDataState { NDS_UNSET, NDS_OK, NDS_BAD }
    public ENavDataState GetNavDataState() { return m_navDataState; }

    // Navigation.cpp — CNavigation::FlushAllAreas
    public void FlushAllAreas()
    {
        FlushSpecialAreas();
        m_mapDesignerPaths.Clear();
    }

    // Navigation.cpp — CNavigation::FlushSpecialAreas
    public void FlushSpecialAreas()
    {
        m_specialAreaNames.Clear();
        m_specialAreas.Clear();
        m_freeSpecialAreaIDs.Clear();
    }

    // Navigation.cpp — CNavigation::InsertSpecialArea
    public void InsertSpecialArea(string name, SpecialArea sa)
    {
        m_specialAreaNames[name] = sa.nBuildingID;
        if (sa.nBuildingID >= m_specialAreas.Count)
        {
            while (m_specialAreas.Count <= sa.nBuildingID)
                m_specialAreas.Add(new SpecialArea());
        }
        m_specialAreas[sa.nBuildingID] = sa;
    }

    // Navigation.cpp — CNavigation::EraseSpecialArea
    public void EraseSpecialArea(string name)
    {
        if (!m_specialAreaNames.TryGetValue(name, out int idx))
            return;
        SpecialArea sa = m_specialAreas[idx];
        m_freeSpecialAreaIDs.Add(sa.nBuildingID);
        m_specialAreaNames.Remove(name);
        sa.nBuildingID = -1;
    }

    public string GetNavigationShapeName(int nBuildingID) { return GetSpecialAreaName(nBuildingID); }

    // Navigation.cpp — CNavigation::DoesNavigationShapeExists
    public bool DoesNavigationShapeExists(string szName, EnumAreaType areaType, bool road = false)
    {
        if (areaType == EnumAreaType.AREATYPE_PATH && !road)
            return m_mapDesignerPaths.ContainsKey(szName);
        return false;
    }

    // Navigation.cpp — CNavigation::CreateNavigationShape
    public bool CreateNavigationShape(SNavigationShapeParams parameters)
    {
        if (parameters.areaType == EnumAreaType.AREATYPE_PATH)
        {
            var listPts = new ListPositions();
            if (parameters.points != null)
                listPts.AddRange(parameters.points);
            if (listPts.Count < 2) return true;
            if (m_mapDesignerPaths.ContainsKey(parameters.szPathName))
            {
                AILog.AIError($"CAISystem::CreateNavigationShape: Designer path '{parameters.szPathName}' already exists, please rename the path.");
                return false;
            }
            if (parameters.closed)
            {
                if (listPts.Count > 0 && (listPts[0] - listPts[listPts.Count - 1]).GetLength() > 0.1f)
                    listPts.Add(listPts[0]);
            }
            m_mapDesignerPaths[parameters.szPathName] = new SShape(listPts, false, (IAISystem_ENavigationType)parameters.nNavType, parameters.nAuxType, parameters.closed);
        }
        return true;
    }

    // Navigation.cpp — CNavigation::DeleteNavigationShape
    public void DeleteNavigationShape(string szName)
    {
        m_mapDesignerPaths.Remove(szName);
    }

    // Navigation.cpp — CNavigation::DisableModifier
    public void DisableModifier(string name)
    {
        if (m_specialAreaNames.TryGetValue(name, out int idx))
        {
            SpecialArea sa = m_specialAreas[idx];
            if (sa.bAltered) return;
            sa.bAltered = true;
        }
    }

    public bool IsPointInForbiddenRegion(Vec3 pos, bool checkAutoGenRegions = true) { return false; }
    public bool IsPointInForbiddenRegion(Vec3 pos, out CAIShape ppShape, bool checkAutoGenRegions) { ppShape = null; return false; }

    // Navigation.cpp — CNavigation::IsPointInWaterAreas
    public bool IsPointInWaterAreas(Vec3 pt)
    {
        for (int i = 0; i < m_specialAreas.Count; ++i)
        {
            SpecialArea sa = m_specialAreas[i];
            if (sa.type == SpecialArea.EType.TYPE_WATER)
            {
                if (Overlap.Point_AABB2D(pt, sa.GetAABB()))
                    if (Overlap.Point_Polygon2D(pt, sa.GetPolygon()))
                        return true;
            }
        }
        return false;
    }

    // Navigation.cpp — CNavigation::IsPointInSpecialArea
    public static bool IsPointInSpecialArea(Vec3 pos, SpecialArea sa)
    {
        if (sa.fHeight > 0.00001f && (pos.z < sa.fMinZ || pos.z > sa.fMaxZ))
            return false;
        return Overlap.Point_Polygon2D(pos, sa.GetPolygon(), sa.GetAABB());
    }

    // Navigation.cpp — CNavigation::IsPointInTriangulationAreas
    public bool IsPointInTriangulationAreas(Vec3 pos)
    {
        bool foundOne = false;
        for (int i = 0; i < m_specialAreas.Count; ++i)
        {
            SpecialArea sa = m_specialAreas[i];
            if (sa.type == SpecialArea.EType.TYPE_TRIANGULATION)
            {
                if (Overlap.Point_Polygon2D(pos, sa.GetPolygon(), sa.GetAABB()))
                    return true;
                foundOne = true;
            }
        }
        return !foundOne;
    }

    public virtual bool IntersectsForbidden(Vec3 vStart, Vec3 vEnd, out Vec3 vClosestPoint, string nameToSkip = null, Vec3? pNormal = null,
        EIFMode mode = EIFMode.IF_AREASBOUNDARIES, bool bForceNormalOutwards = false)
    { vClosestPoint = vEnd; return false; }
    public virtual bool IntersectsForbidden(Vec3 vStart, Vec3 vEnd, float radius, EIFMode mode = EIFMode.IF_AREASBOUNDARIES) { return false; }
    public bool IntersectsSpecialArea(Vec3 start, Vec3 end, out Vec3 closestPoint, SpecialArea.EType type) { closestPoint = end; return false; }

    public virtual bool IsPathForbidden(Vec3 start, Vec3 end) { return false; }
    public virtual bool IsPointForbidden(Vec3 pos, float tol, out Vec3 pNormal) { pNormal = new Vec3(0, 0, 0); return false; }
    public virtual Vec3 GetPointOutsideForbidden(ref Vec3 pos, float distance, Vec3? startPos = null) { return pos; }

    // Navigation.cpp — CNavigation::GetBuildingInfo
    public bool GetBuildingInfo(int nBuildingID, out SBuildingInfo info)
    {
        info = new SBuildingInfo();
        SpecialArea sa = GetSpecialArea(nBuildingID);
        if (sa != null)
        {
            info.fNodeAutoConnectDistance = sa.fNodeAutoConnectDistance;
            info.waypointConnections = sa.waypointConnections;
            return true;
        }
        return false;
    }

    // Navigation.cpp — CNavigation::IsPointInBuilding
    public bool IsPointInBuilding(Vec3 pos, int nBuildingID)
    {
        SpecialArea sa = GetSpecialArea(nBuildingID);
        if (sa != null)
            return IsPointInSpecialArea(pos, sa);
        return false;
    }

    public virtual string GetNearestPathOfTypeInRange(IAIObject requester, Vec3 pos, float range, int type, float devalue, bool useStartNode) { return ""; }

    // Navigation.cpp — CNavigation::DisableNavigationInBrokenRegion
    public void DisableNavigationInBrokenRegion(List<Vec3> outline) { }

    public virtual void ModifyNavCostFactor(string navModifierName, float factor) { }
    public virtual void GetVolumeRegionFiles(string szLevel, string szMission, List<string> filenames) { filenames.Clear(); }

    // Navigation.cpp — CNavigation::GetNavRegion
    public CNavRegion GetNavRegion(IAISystem_ENavigationType type, CGraph pGraph)
    {
        switch (type)
        {
        case IAISystem_ENavigationType.NAV_UNSET: return null;
        }
        return null;
    }

    public List<float> Get3DPassRadii() { return m_3DPassRadii; }

    // Navigation.cpp — CNavigation::CheckNavigationType
    public IAISystem_ENavigationType CheckNavigationType(Vec3 pos, ref int nBuildingID, uint navCapMask)
    {
        if ((navCapMask & (uint)IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN) != 0)
        {
            for (int i = 0; i < m_specialAreas.Count; ++i)
            {
                SpecialArea sa = m_specialAreas[i];
                if (sa.type == SpecialArea.EType.TYPE_WAYPOINT_HUMAN)
                {
                    if (IsPointInSpecialArea(pos, sa))
                    {
                        nBuildingID = sa.nBuildingID;
                        return IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN;
                    }
                }
            }
        }

        if ((navCapMask & (uint)IAISystem_ENavigationType.NAV_VOLUME) != 0)
        {
            for (int i = 0; i < m_specialAreas.Count; ++i)
            {
                SpecialArea sa = m_specialAreas[i];
                if (sa.type == SpecialArea.EType.TYPE_VOLUME)
                    if (IsPointInSpecialArea(pos, sa)) { nBuildingID = sa.nBuildingID; return IAISystem_ENavigationType.NAV_VOLUME; }
            }
        }

        if ((navCapMask & (uint)IAISystem_ENavigationType.NAV_FLIGHT) != 0)
        {
            for (int i = 0; i < m_specialAreas.Count; ++i)
            {
                SpecialArea sa = m_specialAreas[i];
                if (sa.type == SpecialArea.EType.TYPE_FLIGHT)
                    if (IsPointInSpecialArea(pos, sa)) { nBuildingID = sa.nBuildingID; return IAISystem_ENavigationType.NAV_FLIGHT; }
            }
        }

        if ((navCapMask & (uint)IAISystem_ENavigationType.NAV_FREE_2D) != 0)
        {
            for (int i = 0; i < m_specialAreas.Count; ++i)
            {
                SpecialArea sa = m_specialAreas[i];
                if (sa.type == SpecialArea.EType.TYPE_FREE_2D)
                    if (IsPointInSpecialArea(pos, sa)) { nBuildingID = sa.nBuildingID; return IAISystem_ENavigationType.NAV_FREE_2D; }
            }
        }

        if ((navCapMask & (uint)IAISystem_ENavigationType.NAV_TRIANGULAR) != 0)
            return IAISystem_ENavigationType.NAV_TRIANGULAR;

        if ((navCapMask & (uint)IAISystem_ENavigationType.NAV_FREE_2D) != 0)
            return IAISystem_ENavigationType.NAV_FREE_2D;

        return IAISystem_ENavigationType.NAV_UNSET;
    }

    public void GetMemoryStatistics(ICrySizer pSizer) { }
    public nuint GetMemoryUsage() { return 0; }

    //===================================================================
    // ReadAreasFromFile — Navigation.cpp lines 473-509
    //===================================================================
    public void ReadAreasFromFile(CCryBufferedFileReader file, int fileVersion)
    {
        FlushAllAreas();

        uint numAreas;

        // Read designer paths
        {
            if (!file.ReadType(out numAreas)) return;
            // vague sanity check
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                ListPositions lp = new ListPositions();
                string name = "";
                if (!NavigationFileHelpers.ReadPolygonArea(file, fileVersion, ref name, lp))
                    return;

                int navType = 0; int type = 0;
                bool closed = false;
                file.ReadType(out navType);
                file.ReadType(out type);

                if (fileVersion >= 22)
                {
                    file.ReadType(out closed);
                }

                if (m_mapDesignerPaths.ContainsKey(name))
                {
                    AILog.AIError("CNavigation::ReadAreasFromFile: Designer path '{0}' already exists, please rename the path and reexport.", name);
                }
                else
                {
                    m_mapDesignerPaths[name] = new SShape(lp, false, (IAISystem_ENavigationType)navType, type, closed);
                }
            }
        }
    }

    //===================================================================
    // ReadAreasFromFile_Old — Navigation.cpp lines 370-469
    //===================================================================
    public void ReadAreasFromFile_Old(CCryBufferedFileReader file, int fileVersion)
    {
        FlushAllAreas();

        uint numAreas;

        // Read forbidden areas
        {
            if (!file.ReadType(out numAreas)) return;
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                CAIShape pShape = new CAIShape();
                if (!NavigationFileHelpers.ReadForbiddenArea(file, fileVersion, pShape))
                    return;
            }
        }

        // Read navigation modifiers (special areas)
        {
            if (!file.ReadType(out numAreas)) return;
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            m_specialAreas.Clear();
            for (uint i = 0; i < numAreas; ++i)
                m_specialAreas.Add(new SpecialArea());

            string name = "";
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                SpecialArea sa = new SpecialArea();
                NavigationFileHelpers.ReadArea(file, fileVersion, ref name, sa);
            }
        }

        // Read designer forbidden areas
        {
            if (!file.ReadType(out numAreas)) return;
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                CAIShape pShape = new CAIShape();
                NavigationFileHelpers.ReadForbiddenArea(file, fileVersion, pShape);
            }
        }

        // Read forbidden boundaries
        {
            if (!file.ReadType(out numAreas)) return;
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                CAIShape pShape = new CAIShape();
                NavigationFileHelpers.ReadForbiddenArea(file, fileVersion, pShape);
            }
        }

        // Read extra link costs
        {
            if (!file.ReadType(out numAreas)) return;
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                SExtraLinkCostShape shape = new SExtraLinkCostShape(new ListPositions(), 0.0f);
                string name = "";
                NavigationFileHelpers.ReadExtraLinkCostArea(file, fileVersion, ref name, shape);
            }
        }

        // Read designer paths
        {
            if (!file.ReadType(out numAreas)) return;
            System.Diagnostics.Debug.Assert(numAreas < 1000000);
            for (uint iArea = 0; iArea < numAreas; ++iArea)
            {
                ListPositions lp = new ListPositions();
                string name = "";
                NavigationFileHelpers.ReadPolygonArea(file, fileVersion, ref name, lp);

                int navType = 0; int type = 0;
                bool closed = false;
                file.ReadType(out navType);
                file.ReadType(out type);

                if (fileVersion >= 22)
                {
                    file.ReadType(out closed);
                }

                if (m_mapDesignerPaths.ContainsKey(name))
                    AILog.AIError("CNavigation::ReadAreasFromFile_Old: Designer path '{0}' already exists, please rename the path and reexport.", name);
                else
                    m_mapDesignerPaths[name] = new SShape(lp, false, (IAISystem_ENavigationType)navType, type, closed);
            }
        }
    }
    // Navigation.cpp lines 512-516 — OffsetAllAreas
    public void OffsetAllAreas(Vec3 additionalOffset)
    {
        foreach (var kv in m_mapDesignerPaths)
            kv.Value.OffsetShape(additionalOffset);
    }

#if CRYAISYSTEM_DEBUG
    public void DebugDraw() { }
#endif

    // Helper
    private static float Distance_Point_Lineseg(Vec3 p, Lineseg seg, out float t)
    {
        Vec3 diff = p - seg.start;
        Vec3 dir = seg.end - seg.start;
        float lenSq = dir.GetLengthSquared();
        if (lenSq < 0.000001f) { t = 0; return diff.GetLength(); }
        t = (diff.x * dir.x + diff.y * dir.y + diff.z * dir.z) / lenSq;
        t = Math.Clamp(t, 0.0f, 1.0f);
        Vec3 closest = seg.start + dir * t;
        return (p - closest).GetLength();
    }

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
        public SValidationErrorMarker(string msg, Vec3 pos, ColorB col) { this.msg = msg; this.pos = pos; this.col = col; obb = new OBB(); }
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

// Forward decls / shells — types now ported in their own files
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
public class SBuildingInfo
{
    public float fNodeAutoConnectDistance;
    public EWaypointConnections waypointConnections;
}
public class SNavigationShapeParams
{
    public string szPathName;
    public int nPoints;
    public Vec3[] points;
    public int nNavType;
    public int nAuxType;
    public bool closed;
    public EnumAreaType areaType;
    public bool pathIsRoad;
}
// CCryBufferedFileReader — CryBufferedFileReader.h literal shell.
// The actual binary reading is deferred to full file I/O integration;
// each ReadType overload returns false (no bytes read) until then.
// The shell preserves the C++ call shape exactly.
public class CCryBufferedFileReader
{
    public bool ReadType<T>(out T val) where T : struct { val = default; return false; }
    public bool ReadType<T>(T[] buffer, int count = 0) { return false; }
    public bool ReadRaw(char[] buffer, int count) { return false; }
    // Overloads for specific types used by Navigation file parsing
    public bool ReadType(out uint val) { val = 0; return false; }
    public bool ReadType(out int val) { val = 0; return false; }
    public bool ReadType(out long val) { val = 0; return false; }
    public bool ReadType(out float val) { val = 0; return false; }
    public bool ReadType(out byte val) { val = 0; return false; }
    public bool ReadType(out bool val) { val = false; return false; }
    public bool ReadType(out Vec3 val) { val = new Vec3(0, 0, 0); return false; }
}
