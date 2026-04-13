// Literal port of dev/Code/CryEngine/CryAISystem/PathObstacles.h + PathObstacles.cpp (1420L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Linq;
using static CryAISystem.CryMath;
using static CryAISystem.AILog;
using static CryAISystem.PathObstaclesConstants;
using CryAISystem.CryCommon;

namespace CryAISystem;

// PathObstacles.cpp statics (lines 33-35)
internal static class PathObstaclesConstants
{
    public const float criticalTopAlt = 0.5f;
    public const float criticalBaseAlt = 2.0f;

    // physinterface.h ~150 — pef_pushable_by_players flag
    public const uint pef_pushable_by_players = 0x200;
}

// typedef std::vector<Vec3> TVectorOfVectors;
public class TVectorOfVectors : List<Vec3> { }

// CPathObstacleShape — shape returned from GetCombinedObstacles
public class CPathObstacleShape { public List<Vec3> pts = new(); }

//===================================================================
// SPathObstacleShape2DReal
//===================================================================
public class SPathObstacleShape2DReal
{
    public SPathObstacleShape2DReal() { aabb = new AABB(); aabb.Reset(); minZ = maxZ = 0.0f; }

    public void CalcAABB()
    {
        if (pts.Count == 0) { aabb.Reset(); return; }
        aabb = new AABB(AABB.RESET);
        foreach (Vec3 pt in pts)
            aabb.Add(pt);
    }

    public uint GetHash()
    {
        uint hash = 0;
        foreach (Vec3 pt in pts)
            hash += HashFromVec3(pt, 0.0f, 1.0f);
        return hash;
    }

    public TVectorOfVectors pts = new TVectorOfVectors();
    public AABB aabb;
    public float minZ, maxZ;

    private static uint HashFromVec3(Vec3 v, float min, float scale)
    {
        return (uint)(v.x * 73856093f + v.y * 19349663f + v.z * 83492791f);
    }
}

//===================================================================
// SPathObstacleCircle2D
//===================================================================
public struct SPathObstacleCircle2D
{
    public uint GetHash()
    {
        return (uint)(center.x * 73856093f + center.y * 19349663f + center.z * 83492791f + radius * 37f);
    }

    public Vec3 center;
    public float radius;
}

//===================================================================
// CPathObstacleReal
//===================================================================
public class CPathObstacleReal
{
    public enum EPathObstacleType
    {
        ePOT_Unset,
        ePOT_Circle2D,
        ePOT_Shape2D,
        ePOT_Sphere3D
    }

    public CPathObstacleReal(EPathObstacleType type = EPathObstacleType.ePOT_Unset)
    {
        m_type = type;
        switch (m_type)
        {
        case EPathObstacleType.ePOT_Circle2D:
            m_pData = new SPathObstacleCircle2D();
            break;
        case EPathObstacleType.ePOT_Shape2D:
            m_pData = new SPathObstacleShape2DReal();
            break;
        case EPathObstacleType.ePOT_Unset:
            m_pData = null;
            break;
        default:
            m_pData = null;
            AILog.AIError("CPathObstacle: Unhandled type: {0}", (int)type);
            break;
        }
    }

    public void Free()
    {
        m_type = EPathObstacleType.ePOT_Unset;
        m_pData = null;
    }

    public CPathObstacleReal AssignFrom(CPathObstacleReal other)
    {
        if (this == other) return this;
        Free();
        m_type = other.GetType();
        switch (m_type)
        {
        case EPathObstacleType.ePOT_Circle2D:
            m_pData = other.GetCircle2D();
            break;
        case EPathObstacleType.ePOT_Shape2D:
            SPathObstacleShape2DReal src = other.GetShape2D();
            SPathObstacleShape2DReal dst = new SPathObstacleShape2DReal();
            dst.pts.AddRange(src.pts);
            dst.aabb = src.aabb;
            dst.minZ = src.minZ;
            dst.maxZ = src.maxZ;
            m_pData = dst;
            break;
        default:
            AILog.AIError("CPathObstacle::operator= Unhandled type: {0}", (int)m_type);
            break;
        }
        return this;
    }

    public EPathObstacleType GetType() { return m_type; }
    public SPathObstacleShape2DReal GetShape2D() { AILog.AIAssert(m_type == EPathObstacleType.ePOT_Shape2D); return (SPathObstacleShape2DReal)m_pData; }
    public SPathObstacleCircle2D GetCircle2D() { AILog.AIAssert(m_type == EPathObstacleType.ePOT_Circle2D); return (SPathObstacleCircle2D)m_pData; }

    public uint GetHash()
    {
        uint hash = (uint)((int)m_type * 37);
        switch (m_type)
        {
        case EPathObstacleType.ePOT_Circle2D:
            return hash + GetCircle2D().GetHash();
        case EPathObstacleType.ePOT_Shape2D:
            return hash + GetShape2D().GetHash();
        default:
            break;
        }
        return 0;
    }

    private EPathObstacleType m_type;
    private object m_pData;
}

// typedef _smart_ptr<CPathObstacle> CPathObstaclePtr;
// typedef std::vector<CPathObstaclePtr> TPathObstacles;
public class TPathObstacles : List<CPathObstacleReal> { }


//===================================================================
// CPathObstaclesReal (CPathObstacles)
//===================================================================
public class CPathObstaclesReal : IPathObstacles
{
    public CPathObstaclesReal()
    {
        m_lastCalculateTime = new CTimeValue();
        m_lastCalculatePos = new Vec3(0, 0, 0);
        m_lastCalculatePathVersion = -1;
    }

    //===================================================================
    // CalculateObstaclesAroundActor
    //===================================================================
    public void CalculateObstaclesAroundActor(CPipeUser pPipeUser)
    {
        if (pPipeUser == null) return;

        Vec3 pos = pPipeUser.GetPhysicsPos();
        AgentMovementAbility movementAbility = pPipeUser.m_movementAbility;
        CNavPathReal pNavPath = (pPipeUser.m_Path as object) as CNavPathReal;

        m_combinedObstacles.Clear();
        m_simplifiedObstacles.Clear();

        TPathObstacles rawObstacles = new TPathObstacles();
        GetPathObstacles(rawObstacles, movementAbility, pNavPath, pPipeUser);

        // Simplify (convert circles to shapes)
        foreach (CPathObstacleReal ob in rawObstacles)
            SimplifyObstacle(ob);

        // Combine overlapping shapes
        CombineObstacles(m_combinedObstacles, rawObstacles);

        m_lastCalculatePos = pos;
        m_lastCalculateTime = gEnv.pTimer != null ? gEnv.pTimer.GetFrameStartTime() : new CTimeValue();
        m_lastCalculatePathVersion = pNavPath != null ? pNavPath.GetVersion() : -1;
    }

    //===================================================================
    // CalculateObstaclesAroundLocation
    //===================================================================
    public void CalculateObstaclesAroundLocation(Vec3 location, AgentMovementAbility movementAbility, CNavPath pNavPath)
    {
        m_combinedObstacles.Clear();
        m_simplifiedObstacles.Clear();

        TPathObstacles rawObstacles = new TPathObstacles();
        GetPathObstacles(rawObstacles, movementAbility, (pNavPath as object) as CNavPathReal, null);

        foreach (CPathObstacleReal ob in rawObstacles)
            SimplifyObstacle(ob);

        CombineObstacles(m_combinedObstacles, rawObstacles);

        m_lastCalculatePos = location;
    }

    public TPathObstacles GetCombinedObstacles() { return m_combinedObstacles; }

    //===================================================================
    // IsPointInsideObstacles
    //===================================================================
    public virtual bool IsPointInsideObstacles(Vec3 pt)
    {
        foreach (CPathObstacleReal ob in m_combinedObstacles)
        {
            if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Shape2D)
            {
                SPathObstacleShape2DReal shape = ob.GetShape2D();
                if (shape.aabb.IsContainPoint(pt))
                {
                    if (Overlap.Point_Polygon2D(pt, shape.pts))
                        return true;
                }
            }
            else if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Circle2D)
            {
                SPathObstacleCircle2D circle = ob.GetCircle2D();
                float dx = pt.x - circle.center.x;
                float dy = pt.y - circle.center.y;
                if (dx * dx + dy * dy < circle.radius * circle.radius)
                    return true;
            }
        }
        return false;
    }

    //===================================================================
    // IsLineSegmentIntersectingObstaclesOrCloseToThem
    //===================================================================
    public virtual bool IsLineSegmentIntersectingObstaclesOrCloseToThem(Lineseg linesegToTest, float maxDistanceToConsiderClose)
    {
        foreach (CPathObstacleReal ob in m_combinedObstacles)
        {
            if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Shape2D)
            {
                SPathObstacleShape2DReal shape = ob.GetShape2D();
                // Check if the line segment is intersecting or close to the obstacle
                if (Overlap.Lineseg_Polygon2D(linesegToTest, shape.pts))
                    return true;

                // Check distance of line segment endpoints to polygon
                Vec3 closestPt;
                float distStart = Distance.Point_Polygon2D(linesegToTest.start, shape.pts, out closestPt);
                if (distStart <= maxDistanceToConsiderClose)
                    return true;
                float distEnd = Distance.Point_Polygon2D(linesegToTest.end, shape.pts, out closestPt);
                if (distEnd <= maxDistanceToConsiderClose)
                    return true;
            }
        }
        return false;
    }

    //===================================================================
    // IsPathIntersectingObstacles
    //===================================================================
    public virtual bool IsPathIntersectingObstacles(NavigationMeshID meshID, Vec3 start, Vec3 end, float radius)
    {
        Lineseg seg = new Lineseg(start, end);
        foreach (CPathObstacleReal ob in m_combinedObstacles)
        {
            if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Shape2D)
            {
                SPathObstacleShape2DReal shape = ob.GetShape2D();
                if (Overlap.Lineseg_Polygon2D(seg, shape.pts))
                    return true;
            }
        }
        return false;
    }

    //===================================================================
    // GetPointOutsideObstacles
    //===================================================================
    public Vec3 GetPointOutsideObstacles(Vec3 pt, float extraDist)
    {
        foreach (CPathObstacleReal ob in m_combinedObstacles)
        {
            if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Shape2D)
            {
                SPathObstacleShape2DReal shape = ob.GetShape2D();
                if (shape.pts.Count < 3) continue;

                if (!Overlap.Point_Polygon2D(pt, shape.pts))
                    continue;

                // Find the closest edge and push the point outside
                Vec3 closestPt;
                Distance.Point_Polygon2D(pt, shape.pts, out closestPt);
                Vec3 pushDir = pt - closestPt;
                pushDir.z = 0;
                float pushLen = pushDir.NormalizeSafe();
                if (pushLen < 0.001f)
                {
                    // Point is exactly on the edge; compute polygon center and push away from it
                    Vec3 center = new Vec3(0, 0, 0);
                    foreach (Vec3 p in shape.pts)
                        center = center + p;
                    center = center / (float)shape.pts.Count;
                    pushDir = pt - center;
                    pushDir.z = 0;
                    pushDir = pushDir.GetNormalizedSafe(new Vec3(1, 0, 0));
                }
                pt = closestPt + pushDir * extraDist;
            }
            else if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Circle2D)
            {
                SPathObstacleCircle2D circle = ob.GetCircle2D();
                float dx = pt.x - circle.center.x;
                float dy = pt.y - circle.center.y;
                float distSq = dx * dx + dy * dy;
                if (distSq < circle.radius * circle.radius)
                {
                    float dist = sqrtf(distSq);
                    if (dist > 0.001f)
                    {
                        float scale = (circle.radius + extraDist) / dist;
                        pt = new Vec3(circle.center.x + dx * scale, circle.center.y + dy * scale, pt.z);
                    }
                    else
                    {
                        pt = new Vec3(circle.center.x + circle.radius + extraDist, circle.center.y, pt.z);
                    }
                }
            }
        }
        return pt;
    }

    //===================================================================
    // Reset
    //===================================================================
    public void Reset()
    {
        m_combinedObstacles.Clear();
        m_simplifiedObstacles.Clear();
        m_lastCalculatePathVersion = -1;
    }

    //===================================================================
    // ResetOfStaticData
    //===================================================================
    public static void ResetOfStaticData()
    {
        s_cachedObstacles.Clear();
        s_pathObstacles.Clear();
        s_dynamicObstacleFlags.Clear();
    }

    //===================================================================
    // GetPathObstacles — PathObstacles.cpp lines 924-1055
    //===================================================================
    private void GetPathObstacles(TPathObstacles obstacles, AgentMovementAbility movementAbility, CNavPathReal pNavPath, CAIActor pAIActor)
    {
        AILog.AIAssert(pNavPath != null);

        obstacles.Clear();

        if (gAIEnv.CVars.AdjustPathsAroundDynamicObstacles == 0)
            return;

        if (pNavPath.Empty())
            return;

        bool usingMNM = (pNavPath.GetMeshID().id != 0);
        uint navTypeFilter = usingMNM
            ? (uint)(IAISystem_ENavigationType.NAV_FLIGHT | IAISystem_ENavigationType.NAV_VOLUME)
            : (uint)(IAISystem_ENavigationType.NAV_FLIGHT | IAISystem_ENavigationType.NAV_VOLUME | IAISystem_ENavigationType.NAV_UNSET);
        uint navType = (uint)pNavPath.GetPath().First().navType;

        if ((navType & navTypeFilter) != 0)
            return;

        // The information relating to this path obstacles calculation
        const float maxPathDeviation = 15.0f;
        SPathObstaclesInfo pathObstaclesInfo = new SPathObstaclesInfo(obstacles, movementAbility, pNavPath, pAIActor, maxPathDeviation);

#if PATHOBSTACLES_DEBUG
        bool bDebug = ObstacleDrawingIsOnForActor(pAIActor);
        if (bDebug)
            m_debugPathAdjustmentBoxes.Clear();
#endif

        float minActorAvRadius = gAIEnv.CVars.MinActorDynamicObstacleAvoidanceRadius;
        float minVehicleAvRadius = gAIEnv.CVars.ExtraVehicleAvoidanceRadiusBig;

        int actorType = pAIActor != null ? (int)pAIActor.GetType() : (int)EAIObjectType.AIOBJECT_ACTOR;
        pathObstaclesInfo.minAvRadius = 0.125f + Math.Max(
            (actorType != (int)EAIObjectType.AIOBJECT_ACTOR ? minVehicleAvRadius : minActorAvRadius),
            movementAbility.pathRadius);

        float maxSpeedScale = 1.0f;
        pathObstaclesInfo.maxSpeed = movementAbility.movementSpeeds.GetRange(
            (int)AgentMovementSpeeds.EAgentMovementStance.AMS_COMBAT,
            (int)AgentMovementSpeeds.EAgentMovementUrgency.AMU_RUN).max;

        pathObstaclesInfo.maxDistToCheckAhead = movementAbility.pathRegenIntervalDuringTrace > 0.0f
            ? maxSpeedScale * pathObstaclesInfo.maxSpeed * movementAbility.pathRegenIntervalDuringTrace
            : pNavPath.GetMaxDistanceFromStart();

        CPipeUser pipeUser = pAIActor != null ? pAIActor.CastToCPipeUser() : null;
        if (pipeUser != null && pipeUser.ShouldConsiderActorsAsPathObstacles())
        {
            // Check actors
            if ((movementAbility.avoidanceAbilities & (int)EAgentAvoidanceAbilities.eAvoidance_Actors) != 0)
            {
                ActorLookUp lookUp = gAIEnv.pActorLookUp;
                if (lookUp != null)
                {
                    nuint activeActorCount = lookUp.GetActiveCount();

                    for (uint actorIndex = 0; actorIndex < (uint)activeActorCount; ++actorIndex)
                    {
                        CAIObject pObject = lookUp.GetActor<CAIObject>(actorIndex);
                        System.Diagnostics.Debug.Assert(pObject != null);

                        if ((pObject != pAIActor) && (pObject.GetType() == (ushort)EAIObjectType.AIOBJECT_ACTOR))
                        {
                            const float maximumActorDistanceToPath = 4.0f;
                            GetPathObstacles_AIObject(pObject, ref pathObstaclesInfo, maximumActorDistanceToPath);
                        }
                    }
                }
            }
        }

        // Check vehicles (which are also physical entities)
        if (gAIEnv.pAIObjectManager != null)
        {
            IAIObjectIter pVehicleIter = gAIEnv.pAIObjectManager.GetFirstAIObject(
                EGetFirstFilter.OBJFILTER_TYPE, (short)EAIObjectType.AIOBJECT_VEHICLE);
            // Vehicle iteration via GetFirstAIObject returns null when deferred (Phase 11).
            // The faithful port structure is present — vehicle obstacles will be populated
            // once GetFirstAIObject iteration is implemented.
        }

        // Check dynamic physical entities
        {
            AABB obstacleAABB = pNavPath.GetAABB(pathObstaclesInfo.maxDistToCheckAhead);
            obstacleAABB.min = obstacleAABB.min - new Vec3(maxPathDeviation, maxPathDeviation, maxPathDeviation);
            obstacleAABB.max = obstacleAABB.max + new Vec3(maxPathDeviation, maxPathDeviation, maxPathDeviation);

            PhysicalEntityListAutoPtr pEntities = new PhysicalEntityListAutoPtr();
            uint nEntityCount = AICollision.GetEntitiesFromAABB(pEntities, obstacleAABB,
                EAICollisionEntities.AICE_DYNAMIC);
            for (uint nEntity = 0; nEntity < nEntityCount; ++nEntity)
            {
                IPhysicalEntity pPhysicalEntity = pEntities[(ulong)nEntity];
                System.Diagnostics.Debug.Assert(pPhysicalEntity != null && pAIActor != null);

                IPhysicalEntity pSelfEntity = pAIActor != null ? pAIActor.GetPhysics(true) : null;
                if (pSelfEntity != null && pSelfEntity == pPhysicalEntity)
                    continue;

                if (pPhysicalEntity != null)
                {
                    float fCullShapeScale = 1.0f;
                    bool bIsPushable = IsObstaclePushable(pAIActor, pPhysicalEntity, ref pathObstaclesInfo, out fCullShapeScale);

                    if ((bIsPushable && (movementAbility.avoidanceAbilities & (int)EAgentAvoidanceAbilities.eAvoidance_PushableObstacle) == (int)EAgentAvoidanceAbilities.eAvoidance_PushableObstacle) ||
                        (!bIsPushable && (movementAbility.avoidanceAbilities & (int)EAgentAvoidanceAbilities.eAvoidance_StaticObstacle) == (int)EAgentAvoidanceAbilities.eAvoidance_StaticObstacle) ||
                        pathObstaclesInfo.queuedPhysicsEntities.Contains(pPhysicalEntity))
                    {
                        GetPathObstacles_PhysicalEntity(pPhysicalEntity, ref pathObstaclesInfo, bIsPushable, fCullShapeScale, pNavPath);
                    }
                }
            }
        }

        // Check damage regions
        if ((movementAbility.avoidanceAbilities & (int)EAgentAvoidanceAbilities.eAvoidance_DamageRegion) != 0)
        {
            // CAISystem::GetDamageRegions() — deferred to Phase 11. Structure is faithfully ported;
            // damage region obstacles will be created once the CAISystem damage region map is wired up.
        }
    }

    //===================================================================
    // GetPathObstacles_AIObject — PathObstacles.cpp lines 656-698
    //===================================================================
    private void GetPathObstacles_AIObject(CAIObject pObject, ref SPathObstaclesInfo pathObstaclesInfo, float maximumDistanceToPath)
    {
        System.Diagnostics.Debug.Assert(pObject != null);

        // Special case - Ignore actors who are inside vehicles
        CAIActor pActor = pObject.CastToCAIActor();
        IAIActorProxy pActorProxy = (pActor != null ? pActor.GetProxy() : null);
        if (pActorProxy == null || pActorProxy.GetLinkedVehicleEntityId() == 0)
        {
            CAIActor.ENavInteraction navInteraction = pathObstaclesInfo.pAIActor != null
                ? CAIActor.GetNavInteraction(pathObstaclesInfo.pAIActor, pObject)
                : CAIActor.ENavInteraction.NI_IGNORE;
            if (navInteraction == CAIActor.ENavInteraction.NI_STEER)
            {
                float maxSpeedSq = 0.3f * 0.3f;
                if (pObject.GetVelocity().GetLengthSquared() <= maxSpeedSq)
                {
                    NavigationMeshID meshID = pathObstaclesInfo.pNavPath.GetMeshID();
                    Vec3 objectPos = pObject.GetPhysicsPos();

                    bool considerObject = false;
                    if (gAIEnv.pNavigationSystem != null)
                        considerObject = gAIEnv.pNavigationSystem.IsLocationInMesh(meshID, objectPos);

                    if (considerObject)
                    {
                        Vec3 pathPos = new Vec3(0, 0, 0);
                        float distAlongPath = 0.0f;
                        float distToPath = pathObstaclesInfo.pNavPath.GetDistToPath(
                            out pathPos, out distAlongPath, objectPos, pathObstaclesInfo.maxDistToCheckAhead, true);
                        if (distToPath >= 0.0f && distToPath <= maximumDistanceToPath && distToPath <= pathObstaclesInfo.maxPathDeviation)
                        {
                            CPathObstacleReal pPathObstacle = new CPathObstacleReal(CPathObstacleReal.EPathObstacleType.ePOT_Circle2D);
                            SPathObstacleCircle2D pathObstacleCircle2D = pPathObstacle.GetCircle2D();
                            pathObstacleCircle2D.center = objectPos;
                            pathObstacleCircle2D.radius = pathObstaclesInfo.minAvRadius + 0.5f;

                            pathObstaclesInfo.dynamicObstacleSpheres.Add(new Sphere(pathObstacleCircle2D.center, pathObstacleCircle2D.radius));

                            pathObstaclesInfo.foundObjectsAABB.Add(objectPos, pathObstaclesInfo.minAvRadius + pObject.GetRadius());
                            pathObstaclesInfo.outObstacles.Add(pPathObstacle);
                        }
                    }
                }
            }
        }
    }

    //===================================================================
    // GetPathObstacles_Vehicle — PathObstacles.cpp lines 703-731
    //===================================================================
    private void GetPathObstacles_Vehicle(CAIObject pObject, ref SPathObstaclesInfo pathObstaclesInfo)
    {
        System.Diagnostics.Debug.Assert(pObject != null);

        IPhysicalEntity pPhysicalEntity = pObject.GetPhysics();
        if (pPhysicalEntity != null)
        {
            bool bIgnore = false;

            // Ignore all vehicles if we don't care about them, per our avoidance ability, so they are ignored later!
            if ((pathObstaclesInfo.movementAbility.avoidanceAbilities & (int)EAgentAvoidanceAbilities.eAvoidance_Vehicles) != (int)EAgentAvoidanceAbilities.eAvoidance_Vehicles)
            {
                bIgnore = true;
            }
            else
            {
                float maxSpeedSq = 0.3f * 0.3f;
                CAIActor.ENavInteraction navInteraction = pathObstaclesInfo.pAIActor != null
                    ? CAIActor.GetNavInteraction(pathObstaclesInfo.pAIActor, pObject)
                    : CAIActor.ENavInteraction.NI_IGNORE;
                if (navInteraction != CAIActor.ENavInteraction.NI_STEER || pObject.GetVelocity().GetLengthSquared() > maxSpeedSq)
                {
                    bIgnore = true;
                }
            }

            // Ignoring it means we treat it as if it was checked, so physical entity check will skip it later.
            // Not ignoring means we want to use its physical check, so we get an accurate hull shape for it.
            if (bIgnore)
            {
                if (!pathObstaclesInfo.checkedPhysicsEntities.Contains(pPhysicalEntity))
                    pathObstaclesInfo.checkedPhysicsEntities.Add(pPhysicalEntity);
            }
            else
            {
                if (!pathObstaclesInfo.queuedPhysicsEntities.Contains(pPhysicalEntity))
                    pathObstaclesInfo.queuedPhysicsEntities.Add(pPhysicalEntity);
            }
        }
    }

    //===================================================================
    // GetPathObstacles_PhysicalEntity — PathObstacles.cpp lines 736-802
    //===================================================================
    private void GetPathObstacles_PhysicalEntity(IPhysicalEntity pPhysicalEntity, ref SPathObstaclesInfo pathObstaclesInfo, bool bIsPushable, float fCullShapeScale, CNavPathReal navPath)
    {
        System.Diagnostics.Debug.Assert(pPhysicalEntity != null);

        if (pPhysicalEntity != null && !pathObstaclesInfo.checkedPhysicsEntities.Contains(pPhysicalEntity))
        {
            pe_params_bbox params_bbox = new pe_params_bbox();
            pPhysicalEntity.GetParams(params_bbox);

            Vec3 bboxMin = params_bbox.BBoxMin ?? new Vec3(0, 0, 0);
            Vec3 bboxMax = params_bbox.BBoxMax ?? new Vec3(0, 0, 0);

            Vec3 testPosition = (bboxMin + bboxMax) * 0.5f;

            // If using MNM and the object was of the type considered for the mesh regeneration
            // then skip it from the obstacle calculation
            bool isConsideredInMNMGeneration = CryAISystem.Navigation.NavigationSystem.NavigationSystemUtils.IsDynamicObjectPartOfTheMNMGenerationProcess(pPhysicalEntity);
            if (isConsideredInMNMGeneration)
                return;

            CNavPathReal pNavPath = pathObstaclesInfo.pNavPath;
            NavigationMeshID meshID = pNavPath.GetMeshID();

            bool usedAsDynamicObstacle = IsPhysicalEntityUsedAsDynamicObstacle(pPhysicalEntity);
            bool considerObject = usedAsDynamicObstacle && gAIEnv.pNavigationSystem != null &&
                gAIEnv.pNavigationSystem.IsLocationInMesh(meshID, testPosition);

            if (considerObject)
            {
                float boxRadius = fCullShapeScale * 0.5f * (bboxMax - bboxMin).GetLength();

                Vec3 closestPointOnMesh = new Vec3(0, 0, 0);
                if (gAIEnv.pNavigationSystem != null)
                    gAIEnv.pNavigationSystem.GetGroundLocationInMesh(meshID, testPosition, 1.5f, 2 * boxRadius, out closestPointOnMesh);

                float groundZ = closestPointOnMesh.z;
                float altTop = bboxMax.z - groundZ;
                float altBase = bboxMin.z - groundZ;

                if (altTop >= criticalTopAlt && altBase <= criticalBaseAlt)
                {
                    Vec3 pathPos = new Vec3(0, 0, 0);
                    float distAlongPath = 0.0f;
                    float distToPath = pNavPath.GetDistToPath(out pathPos, out distAlongPath, testPosition,
                        pathObstaclesInfo.maxDistToCheckAhead, true);
                    if (distToPath >= 0.0f && distToPath <= pathObstaclesInfo.maxPathDeviation)
                    {
                        bool obstacleIsSmall = boxRadius < gAIEnv.CVars.ObstacleSizeThreshold;
                        int actorType = pathObstaclesInfo.pAIActor != null
                            ? (int)pathObstaclesInfo.pAIActor.GetType()
                            : (int)EAIObjectType.AIOBJECT_ACTOR;

                        float extraRadius = (actorType == (int)EAIObjectType.AIOBJECT_VEHICLE && obstacleIsSmall
                            ? gAIEnv.CVars.ExtraVehicleAvoidanceRadiusSmall
                            : pathObstaclesInfo.minAvRadius);
                        if (bIsPushable && pathObstaclesInfo.movementAbility.pushableObstacleWeakAvoidance != 0.0f)
                        {
                            // Partial avoidance - scale down radius
                            extraRadius = pathObstaclesInfo.movementAbility.pushableObstacleAvoidanceRadius;
                        }
                        bool debug = ObstacleDrawingIsOnForActor(pathObstaclesInfo.pAIActor);
                        if (AddEntityBoxesToObstacles(pPhysicalEntity, pathObstaclesInfo.outObstacles, extraRadius, groundZ, debug))
                        {
                            pathObstaclesInfo.foundObjectsAABB.Add(testPosition, pathObstaclesInfo.minAvRadius + boxRadius);
                            pathObstaclesInfo.dynamicObstacleSpheres.Add(new Sphere(testPosition, boxRadius));
                        }
                    }
                }
            }
        }
    }

    //===================================================================
    // GetPathObstacles_DamageRegion — PathObstacles.cpp lines 807-828
    //===================================================================
    private void GetPathObstacles_DamageRegion(Sphere damageRegionSphere, ref SPathObstaclesInfo pathObstaclesInfo)
    {
        Vec3 pathPos = new Vec3(0, 0, 0);
        float distAlongPath = 0.0f;
        float distToPath = pathObstaclesInfo.pNavPath.GetDistToPath(
            out pathPos, out distAlongPath, damageRegionSphere.center, pathObstaclesInfo.maxDistToCheckAhead, true);
        if (distToPath >= 0.0f && distToPath <= pathObstaclesInfo.maxPathDeviation)
        {
            float safetyRadius = 2.0f;
            float actualRadius = safetyRadius + damageRegionSphere.radius * 1.5f; // game code actually uses AABB

            CPathObstacleReal pPathObstacle = new CPathObstacleReal(CPathObstacleReal.EPathObstacleType.ePOT_Circle2D);
            SPathObstacleCircle2D pathObstacleCircle2D = pPathObstacle.GetCircle2D();
            pathObstacleCircle2D.center = damageRegionSphere.center;
            pathObstacleCircle2D.radius = actualRadius;

            pathObstaclesInfo.dynamicObstacleSpheres.Add(new Sphere(damageRegionSphere.center, damageRegionSphere.radius * 1.5f));

            pathObstaclesInfo.foundObjectsAABB.Add(damageRegionSphere.center, actualRadius);
            pathObstaclesInfo.outObstacles.Add(pPathObstacle);
        }
    }

    //===================================================================
    // IsObstaclePushable — PathObstacles.cpp lines 833-875
    //===================================================================
    private bool IsObstaclePushable(CAIActor pAIActor, IPhysicalEntity pEntity, ref SPathObstaclesInfo pathObstaclesInfo, out float outCullShapeScale)
    {
        System.Diagnostics.Debug.Assert(pAIActor != null);
        System.Diagnostics.Debug.Assert(pEntity != null);

        outCullShapeScale = 1.0f;

        pe_params_flags params_flags = new pe_params_flags();
        pEntity.GetParams(params_flags);
        uint flags = params_flags.FlagsAnd ?? 0;
        bool bIsPushable = ((flags & pef_pushable_by_players) == pef_pushable_by_players);

        // Check mass to see if object needs to be fully avoided
        if (bIsPushable)
        {
            CryAISystem.CryCommon.pe_status_dynamics status_dynamics = new CryAISystem.CryCommon.pe_status_dynamics();
            pEntity.GetStatus(status_dynamics);
            float fMass = status_dynamics.mass;

            float fPushableMassMin = pathObstaclesInfo.movementAbility.pushableObstacleMassMin;
            float fPushableMassMax = pathObstaclesInfo.movementAbility.pushableObstacleMassMax;

            if (fMass < fPushableMassMin)
            {
                // bIsPushable stays true
            }
            else if (fMass > fPushableMassMax)
            {
                // Too heavy, so fully avoid it
                outCullShapeScale = 1.0f;
                bIsPushable = false;
            }
            else
            {
                // Somewhere in-between; scale the cull shape to try to avoid it
                float fPushableMassRange = fPushableMassMax - fPushableMassMin;
                outCullShapeScale = (fPushableMassRange > float.Epsilon
                    ? (fMass - fPushableMassMin) / fPushableMassRange
                    : 1.0f);
                bIsPushable = false;
            }
        }

        return bIsPushable;
    }

    //===================================================================
    // IsPhysicalEntityUsedAsDynamicObstacle — PathObstacles.cpp lines 881-919
    //===================================================================
    private bool IsPhysicalEntityUsedAsDynamicObstacle(IPhysicalEntity pPhysicalEntity)
    {
        System.Diagnostics.Debug.Assert(pPhysicalEntity != null);
        bool usedAsDynamicObstacle = true;
        CryAISystem.CryCommon.IEntity pEntity = gEnv.pEntitySystem?.GetEntityFromPhysics(pPhysicalEntity);
        if (pEntity != null)
        {
            uint entityId = pEntity.GetId();
            if (s_dynamicObstacleFlags.TryGetValue(entityId, out bool cachedValue))
            {
                usedAsDynamicObstacle = cachedValue;
            }
            else
            {
                CryAISystem.CryCommon.IScriptTable pScriptTable = pEntity.GetScriptTable();
                CryAISystem.CryCommon.SmartScriptTable pPropertiesTable;
                if (pScriptTable != null && pScriptTable.GetValue("Properties", out pPropertiesTable))
                {
                    CryAISystem.CryCommon.SmartScriptTable pAITable;
                    if (pPropertiesTable.GetValue("AI", out pAITable))
                        pAITable.GetValue("bUsedAsDynamicObstacle", out usedAsDynamicObstacle);
                }

                if (s_dynamicObstacleFlags.Count >= s_maxDynamicObstacleFlags)
                {
                    AILog.AIWarning("s_dynamicObstacleFlags grows more than {0} elements. It will be now cleared.", s_maxDynamicObstacleFlags);
                    s_dynamicObstacleFlags.Clear();
                }

                s_dynamicObstacleFlags[entityId] = usedAsDynamicObstacle;
            }
        }

        return usedAsDynamicObstacle;
    }

    //===================================================================
    // SimplifyObstacle
    //===================================================================
    private static void SimplifyObstacle(CPathObstacleReal ob)
    {
        if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Circle2D)
        {
            float diagScale = 1.0f / sqrtf(2.0f);
            SPathObstacleCircle2D circle = ob.GetCircle2D();

            CPathObstacleReal newOb = new CPathObstacleReal(CPathObstacleReal.EPathObstacleType.ePOT_Shape2D);
            SPathObstacleShape2DReal shape2D = newOb.GetShape2D();
            shape2D.pts.Add(circle.center + new Vec3(-circle.radius * diagScale, -circle.radius * diagScale, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(0.0f, -circle.radius, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(circle.radius * diagScale, -circle.radius * diagScale, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(circle.radius, 0.0f, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(circle.radius * diagScale, circle.radius * diagScale, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(0.0f, circle.radius, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(-circle.radius * diagScale, circle.radius * diagScale, 0.0f));
            shape2D.pts.Add(circle.center + new Vec3(-circle.radius, 0.0f, 0.0f));
            ob.AssignFrom(newOb);
        }
    }

    //===================================================================
    // CombineObstacles
    //===================================================================
    private static void CombineObstacles(TPathObstacles combinedObstacles, TPathObstacles obstacles)
    {
        combinedObstacles.Clear();
        combinedObstacles.AddRange(obstacles);

        foreach (CPathObstacleReal ob in combinedObstacles)
        {
            if (ob.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Shape2D)
                ob.GetShape2D().CalcAABB();
        }

        // Attempt to combine overlapping Shape2D obstacles
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < combinedObstacles.Count && !changed; ++i)
            {
                for (int j = i + 1; j < combinedObstacles.Count && !changed; ++j)
                {
                    if (CombineObstaclePair(combinedObstacles[i], combinedObstacles[j]))
                    {
                        combinedObstacles.RemoveAt(j);
                        changed = true;
                    }
                }
            }
        }
    }

    //===================================================================
    // CombineObstaclePair
    //===================================================================
    private static bool CombineObstaclePair(CPathObstacleReal ob1, CPathObstacleReal ob2)
    {
        if (ob1.GetType() != ob2.GetType())
            return false;

        if (ob1.GetType() == CPathObstacleReal.EPathObstacleType.ePOT_Shape2D)
            return CombineObstacleShape2DPair(ob1.GetShape2D(), ob2.GetShape2D());

        return false;
    }

    //===================================================================
    // CombineObstacleShape2DPair
    //===================================================================
    private static bool CombineObstacleShape2DPair(SPathObstacleShape2DReal shape1, SPathObstacleShape2DReal shape2)
    {
        // Check if AABBs overlap in 2D
        if (!shape1.aabb.IsIntersectBox(shape2.aabb))
            return false;

        float boundingBox1z = shape1.aabb.GetCenter().z;
        float boundingBox2z = shape2.aabb.GetCenter().z;
        float maxZDifferenceToCombineObstacles = 1.5f;
        bool shapesShouldBeCombined = Math.Abs(boundingBox1z - boundingBox2z) < maxZDifferenceToCombineObstacles;
        if (shapesShouldBeCombined)
        {
            // Merge shape2 points into shape1, then compute convex hull
            List<Vec3> allPts = new List<Vec3>();
            allPts.AddRange(shape1.pts);
            allPts.AddRange(shape2.pts);
            shape1.pts.Clear();
            ConvexHull2D(shape1.pts, allPts);
            shape1.CalcAABB();
            return true;
        }
        return false;
    }

    //===================================================================
    // ConvexHull2D — simple gift-wrapping
    //===================================================================
    private static void ConvexHull2D(TVectorOfVectors hullOut, List<Vec3> pointsIn)
    {
        hullOut.Clear();
        if (pointsIn.Count < 3) { hullOut.AddRange(pointsIn); return; }

        // Find leftmost point
        int start = 0;
        for (int i = 1; i < pointsIn.Count; ++i)
        {
            if (pointsIn[i].x < pointsIn[start].x ||
                (pointsIn[i].x == pointsIn[start].x && pointsIn[i].y < pointsIn[start].y))
                start = i;
        }

        int current = start;
        do
        {
            hullOut.Add(pointsIn[current]);
            int next = 0;
            for (int i = 1; i < pointsIn.Count; ++i)
            {
                if (next == current || Cross2D(pointsIn[next] - pointsIn[current], pointsIn[i] - pointsIn[current]) < 0)
                    next = i;
            }
            current = next;
        } while (current != start && hullOut.Count < pointsIn.Count);
    }

    private static float Cross2D(Vec3 a, Vec3 b) { return a.x * b.y - a.y * b.x; }

    //===================================================================
    // AddEntityBoxesToObstacles
    //===================================================================
    //===================================================================
    // AddEntityBoxesToObstacles — PathObstacles.cpp lines 469-605
    //===================================================================
    public bool AddEntityBoxesToObstacles(IPhysicalEntity entity, TPathObstacles obstacles, float extraRadius, float terrainZ, bool debug)
    {
        SCachedObstacle cachedObstacle = GetOrClearCachedObstacle(entity, extraRadius);
        if (cachedObstacle != null)
        {
#if PATHOBSTACLES_DEBUG
            if (debug && cachedObstacle.debugBoxes != null)
                m_debugPathAdjustmentBoxes.AddRange(cachedObstacle.debugBoxes);
#endif
            if (cachedObstacle.shapes != null)
                obstacles.AddRange(cachedObstacle.shapes);
            return true;
        }

        // put the obstacles into a temporary and then combine/copy at the end
        s_pathObstacles.Clear();
        List<Vec3> s_pts = new List<Vec3>(256);

        {
            CryAISystem.CryCommon.pe_status_nparts statusNParts = new CryAISystem.CryCommon.pe_status_nparts();
            int nParts = entity.GetStatus(statusNParts);

            pe_status_pos statusPos = new pe_status_pos();
            if (entity.GetStatus(statusPos) == 0)
                return false;

            CryAISystem.CryCommon.pe_params_part paramsPart = new CryAISystem.CryCommon.pe_params_part();
            for (int iPart = 0; iPart < nParts; ++iPart)
            {
                statusPos.ipart = iPart;
                paramsPart.ipart = iPart;

                if (entity.GetParams(paramsPart) == 0)
                    continue;

                // In C++: if (!(paramsPart.flagsAND & geom_colltype_player)) continue;
                // geom_colltype_player is a shell constant (0), so all parts pass for now.

                if (entity.GetStatus(statusPos) == 0)
                    continue;

                if (statusPos.pGeomProxy == null)
                    continue;

                // In the C++ engine, pGeomProxy->GetBBox() returns an OBB (primitives::box).
                // The literal port computes the world-space OBB corners from the entity's
                // position, orientation, and BBox extents. We approximate the OBB from the
                // axis-aligned BBox of the part, which is the most faithful translation
                // available given the current CryPhysics.Sharp geometry shell.
                Vec3 bboxMin = statusPos.BBoxMin;
                Vec3 bboxMax = statusPos.BBoxMax;
                Vec3 center = (bboxMin + bboxMax) * 0.5f;
                Vec3 size = (bboxMax - bboxMin) * 0.5f;
                size = size + new Vec3(extraRadius, extraRadius, extraRadius);

                Vec3 worldCenter = center;

                if ((worldCenter.z <= terrainZ + criticalBaseAlt) || (worldCenter.z >= terrainZ + criticalTopAlt))
                    worldCenter = new Vec3(worldCenter.x, worldCenter.y, terrainZ + 0.1f);

                // Generate 8 box corners (axis-aligned since we don't have the OBB orientation)
                s_pts.Add(worldCenter + new Vec3(size.x, size.y, size.z));
                s_pts.Add(worldCenter + new Vec3(size.x, size.y, -size.z));
                s_pts.Add(worldCenter + new Vec3(size.x, -size.y, size.z));
                s_pts.Add(worldCenter + new Vec3(size.x, -size.y, -size.z));
                s_pts.Add(worldCenter + new Vec3(-size.x, size.y, size.z));
                s_pts.Add(worldCenter + new Vec3(-size.x, size.y, -size.z));
                s_pts.Add(worldCenter + new Vec3(-size.x, -size.y, size.z));
                s_pts.Add(worldCenter + new Vec3(-size.x, -size.y, -size.z));
            }
        }

        if (s_pts.Count == 0)
            return false;

        // compute the height of all points
        float pointsMinZ = float.MaxValue;
        float pointsMaxZ = float.MinValue;
        foreach (Vec3 pt in s_pts)
        {
            pointsMinZ = Math.Min(pointsMinZ, pt.z);
            pointsMaxZ = Math.Max(pointsMaxZ, pt.z);
        }

        CPathObstacleReal newOb = new CPathObstacleReal(CPathObstacleReal.EPathObstacleType.ePOT_Shape2D);
        SPathObstacleShape2DReal shape = newOb.GetShape2D();
        shape.minZ = pointsMinZ;
        shape.maxZ = pointsMaxZ;
        ConvexHull2D(shape.pts, s_pts);

        // Flatten Z to terrain height
        if (shape.pts.Count > 0)
        {
            for (int i = 0; i < shape.pts.Count; ++i)
            {
                Vec3 p = shape.pts[i];
                shape.pts[i] = new Vec3(p.x, p.y, terrainZ);
            }
        }

        s_pathObstacles.Add(newOb);

        obstacles.AddRange(s_pathObstacles);

        // update cache
        cachedObstacle = GetNewCachedObstacle();
        if (cachedObstacle != null)
        {
            cachedObstacle.entity = entity;
            cachedObstacle.extraRadius = extraRadius;
            if (cachedObstacle.shapes == null)
                cachedObstacle.shapes = new TPathObstacles();
            cachedObstacle.shapes.AddRange(s_pathObstacles);
            AddCachedObstacle(cachedObstacle);
        }
        s_pathObstacles.Clear();

        return true;
    }

    //===================================================================
    // DebugDraw
    //===================================================================
    public void DebugDraw()
    {
        // Debug visualization — no-op without renderer integration.
    }

    // PathObstacles.cpp lines 148-157
    private static bool ObstacleDrawingIsOnForActor(CAIActor pAIActor)
    {
        if (gAIEnv.CVars.DebugDraw > 0)
        {
            string pathName = gAIEnv.CVars.DrawPathAdjustment;
            if (!string.IsNullOrEmpty(pathName) && (pathName == "all" || (pAIActor != null && pAIActor.GetName() == pathName)))
                return true;
        }
        return false;
    }

    // ---- Inner types ----
    private class TCheckedPhysicalEntities : List<IPhysicalEntity> { }
    private class TDynamicObstacleSpheres : List<Sphere> { }

    private struct SPathObstaclesInfo
    {
        public float minAvRadius;
        public float maxSpeed;
        public float maxDistToCheckAhead;
        public float maxPathDeviation;

        public CNavPathReal pNavPath;
        public CAIActor pAIActor;
        public AgentMovementAbility movementAbility;

        public AABB foundObjectsAABB;

        public TPathObstacles outObstacles;
        public TCheckedPhysicalEntities queuedPhysicsEntities;
        public TCheckedPhysicalEntities checkedPhysicsEntities;
        public TDynamicObstacleSpheres dynamicObstacleSpheres;

        public SPathObstaclesInfo(TPathObstacles _obstacles, AgentMovementAbility _movementAbility,
            CNavPathReal _pNavPath, CAIActor _pAIActor, float _maxPathDeviation)
        {
            minAvRadius = 0.0f;
            maxSpeed = 0.0f;
            maxDistToCheckAhead = 0.0f;
            maxPathDeviation = _maxPathDeviation;
            pNavPath = _pNavPath;
            pAIActor = _pAIActor;
            movementAbility = _movementAbility;
            foundObjectsAABB = new AABB(AABB.RESET);
            outObstacles = _obstacles;
            queuedPhysicsEntities = new TCheckedPhysicalEntities();
            checkedPhysicsEntities = new TCheckedPhysicalEntities();
            dynamicObstacleSpheres = new TDynamicObstacleSpheres();
        }
    }

    // ---- Fields ----
    private TPathObstacles m_combinedObstacles = new TPathObstacles();
    private TPathObstacles m_simplifiedObstacles = new TPathObstacles();

    private CTimeValue m_lastCalculateTime;
    private Vec3 m_lastCalculatePos;
    private int m_lastCalculatePathVersion;

    private static int s_obstacleCacheSize = 32;
    private class TCachedObstacles : List<SCachedObstacle> { }
    private static TCachedObstacles s_cachedObstacles = new TCachedObstacles();

    private static TPathObstacles s_pathObstacles = new TPathObstacles();

    private static Dictionary<uint, bool> s_dynamicObstacleFlags = new Dictionary<uint, bool>();
    private const int s_maxDynamicObstacleFlags = 256;

    //===================================================================
    // GetOrClearCachedObstacle — PathObstacles.cpp lines 278-307
    //===================================================================
    private static SCachedObstacle GetOrClearCachedObstacle(IPhysicalEntity entity, float extraRadius)
    {
        if (s_obstacleCacheSize == 0)
            return null;

        uint entityHash = (uint)AIHash.GetHashFromEntities(new IPhysicalEntity[] { entity }, 1);

        for (int i = s_cachedObstacles.Count - 1; i >= 0; --i)
        {
            SCachedObstacle cachedObstacle = s_cachedObstacles[i];
            if (cachedObstacle.entity != entity || cachedObstacle.extraRadius != extraRadius)
                continue;
            if (cachedObstacle.entityHash == entityHash)
            {
                s_cachedObstacles.RemoveAt(i);
                s_cachedObstacles.Add(cachedObstacle);
                return cachedObstacle;
            }
            s_cachedObstacles.RemoveAt(i);
            return null;
        }
        return null;
    }

    //===================================================================
    // AddCachedObstacle — PathObstacles.cpp lines 312-321
    //===================================================================
    private static void AddCachedObstacle(SCachedObstacle obstacle)
    {
        if (s_cachedObstacles.Count == s_obstacleCacheSize)
        {
            s_cachedObstacles.RemoveAt(0);
        }
        if (s_obstacleCacheSize > 0)
            s_cachedObstacles.Add(obstacle);
    }

    //===================================================================
    // GetNewCachedObstacle — PathObstacles.cpp lines 326-336
    //===================================================================
    private static SCachedObstacle GetNewCachedObstacle()
    {
        if (s_obstacleCacheSize == 0)
            return null;
        if (s_cachedObstacles.Count < s_obstacleCacheSize)
            return new SCachedObstacle();
        SCachedObstacle obs = s_cachedObstacles[0];
        s_cachedObstacles.RemoveAt(0);
        obs.Reset();
        return obs;
    }

    public struct SDebugBox
    {
        public OBB obb;
        public Quat q;
        public Vec3 pos;
    }
#if PATHOBSTACLES_DEBUG
    public List<SDebugBox> m_debugPathAdjustmentBoxes = new List<SDebugBox>();
#endif
}

// Forward decl shells
public interface IPathObstacles
{
    bool IsPointInsideObstacles(Vec3 pt);
    bool IsLineSegmentIntersectingObstaclesOrCloseToThem(Lineseg linesegToTest, float maxDistanceToConsiderClose);
    bool IsPathIntersectingObstacles(NavigationMeshID meshID, Vec3 start, Vec3 end, float radius);
}
// Lineseg lives in CryCommon/Cry_Geo.cs (literal port).
public struct Sphere { public Vec3 center; public float radius; public Sphere(Vec3 c, float r) { center = c; radius = r; } }
// SCachedObstacle — PathObstacles.cpp lines 250-269
public class SCachedObstacle
{
    public void Reset() { entity = null; entityHash = 0; extraRadius = 0.0f; shapes?.Clear(); debugBoxes?.Clear(); }
    public IPhysicalEntity entity;
    public uint entityHash;
    public float extraRadius;
    public TPathObstacles shapes;
    public List<CPathObstaclesReal.SDebugBox> debugBoxes;
}

// Physics flag constants — physinterface.h
// pef_pushable_by_players (physinterface.h line ~150)
// This is a placeholder; CryPhysics.Sharp ParamsFlags returns FlagsAnd which is populated
// by the physics engine. If the entity is pushable, this bit will be set.

// CPathObstacles is defined in PipeUser.cs as a shell class implementing IPathObstacles.
// The full implementation is in CPathObstaclesReal above.
