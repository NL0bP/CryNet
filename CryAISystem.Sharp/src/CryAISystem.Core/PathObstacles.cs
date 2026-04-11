// Literal port of dev/Code/CryEngine/CryAISystem/PathObstacles.h
// .cpp impl (1420L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : interface for the CGraph class.

using System.Collections.Generic;

namespace CryAISystem;

// typedef std::vector<Vec3> TVectorOfVectors;
public class TVectorOfVectors : List<Vec3> { }

// Note: SPathObstacleShape2D and CPathObstacle were forward-declared in NavPath.cs.
// Replace those with full literal port here. Use distinct names to avoid clashes.
public class SPathObstacleShape2DReal
{
    public SPathObstacleShape2DReal() { aabb = new AABB(); aabb.Reset(); minZ = maxZ = 0.0f; }
    public void CalcAABB() { /* impl in .cpp */ }
    public uint GetHash() { return 0; /* impl in .cpp */ }

    public TVectorOfVectors pts = new TVectorOfVectors();
    public AABB aabb;
    public float minZ, maxZ;
}

public struct SPathObstacleCircle2D
{
    public uint GetHash() { return 0; /* impl in .cpp */ }

    public Vec3 center;
    public float radius;
}

public class CPathObstacleReal /* : _reference_target_t */
{
    public enum EPathObstacleType
    {
        ePOT_Unset,
        ePOT_Circle2D,
        ePOT_Shape2D,
        ePOT_Sphere3D
    }

    public CPathObstacleReal(EPathObstacleType type = EPathObstacleType.ePOT_Unset) { m_type = type; /* impl in .cpp */ }
    // ~CPathObstacle();

    public void Free() { /* impl in .cpp */ }
    public CPathObstacleReal AssignFrom(CPathObstacleReal other) { return this; /* impl in .cpp */ }

    public EPathObstacleType GetType() { return m_type; }
    public SPathObstacleShape2DReal GetShape2D() { AILog.AIAssert(m_type == EPathObstacleType.ePOT_Shape2D); return (SPathObstacleShape2DReal)m_pData; }
    public SPathObstacleCircle2D GetCircle2D() { AILog.AIAssert(m_type == EPathObstacleType.ePOT_Circle2D); return (SPathObstacleCircle2D)m_pData; }

    public uint GetHash() { return 0; /* impl in .cpp */ }

    private EPathObstacleType m_type;
    private object m_pData;
}

// typedef _smart_ptr<CPathObstacle> CPathObstaclePtr;
// typedef std::vector<CPathObstaclePtr> TPathObstacles;
public class TPathObstacles : List<CPathObstacleReal> { }


public class CPathObstaclesReal : IPathObstacles
{
    public CPathObstaclesReal() { /* impl in .cpp */ }
    // ~CPathObstacles();

    public void CalculateObstaclesAroundActor(CPipeUser pPipeUser) { /* impl in .cpp */ }
    public void CalculateObstaclesAroundLocation(Vec3 location, AgentMovementAbility movementAbility, CNavPath pNavPath) { /* impl in .cpp */ }

    public TPathObstacles GetCombinedObstacles() { return m_combinedObstacles; }

    public virtual bool IsPointInsideObstacles(Vec3 pt) { return false; /* impl in .cpp */ }

    public virtual bool IsLineSegmentIntersectingObstaclesOrCloseToThem(Lineseg linesegToTest, float maxDistanceToConsiderClose) { return false; /* impl in .cpp */ }

    public virtual bool IsPathIntersectingObstacles(NavigationMeshID meshID, Vec3 start, Vec3 end, float radius) { return false; /* impl in .cpp */ }

    public Vec3 GetPointOutsideObstacles(Vec3 pt, float extraDist) { return pt; /* impl in .cpp */ }

    public void Reset() { /* impl in .cpp */ }

    public static void ResetOfStaticData() { /* impl in .cpp */ }

    // typedef std::vector<IPhysicalEntity*> TCheckedPhysicalEntities;
    private class TCheckedPhysicalEntities : List<IPhysicalEntity> { }
    // typedef std::vector<Sphere> TDynamicObstacleSpheres;
    private class TDynamicObstacleSpheres : List<Sphere> { }

    private struct SPathObstaclesInfo
    {
        public float minAvRadius;
        public float maxSpeed;
        public float maxDistToCheckAhead;
        public float maxPathDeviation;

        public CNavPath pNavPath;
        public CAIActor pAIActor;
        public AgentMovementAbility movementAbility;

        public AABB foundObjectsAABB;

        public TPathObstacles outObstacles;
        public TCheckedPhysicalEntities queuedPhysicsEntities;
        public TCheckedPhysicalEntities checkedPhysicsEntities;
        public TDynamicObstacleSpheres dynamicObstacleSpheres;

        public SPathObstaclesInfo(TPathObstacles _obstacles, AgentMovementAbility _movementAbility,
            CNavPath _pNavPath, CAIActor _pAIActor, float _maxPathDeviation)
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

    private void GetPathObstacles(TPathObstacles obstacles, AgentMovementAbility movementAbility, CNavPath pNavPath, CAIActor pAIActor) { /* impl in .cpp */ }
    private void GetPathObstacles_AIObject(CAIObject pObject, ref SPathObstaclesInfo pathObstaclesInfo, float maximumDistanceToPath) { /* impl in .cpp */ }
    private void GetPathObstacles_Vehicle(CAIObject pObject, ref SPathObstaclesInfo pathObstaclesInfo) { /* impl in .cpp */ }
    private void GetPathObstacles_PhysicalEntity(IPhysicalEntity pEntity, ref SPathObstaclesInfo pathObstaclesInfo, bool bIsPushable, float fCullShapeScale, CNavPath navPath) { /* impl in .cpp */ }
    private void GetPathObstacles_DamageRegion(Sphere damageRegionSphere, ref SPathObstaclesInfo pathObstaclesInfo) { /* impl in .cpp */ }

    private bool IsObstaclePushable(CAIActor pAIActor, IPhysicalEntity pEntity, ref SPathObstaclesInfo pathObstaclesInfo, out float outCullShapeScale)
    { outCullShapeScale = 0.0f; return false; /* impl in .cpp */ }

    private bool IsPhysicalEntityUsedAsDynamicObstacle(IPhysicalEntity pPhysicalEntity) { return false; /* impl in .cpp */ }

    private TPathObstacles m_combinedObstacles = new TPathObstacles();
    private TPathObstacles m_simplifiedObstacles = new TPathObstacles();

    private CTimeValue m_lastCalculateTime;
    private Vec3 m_lastCalculatePos;
    private int m_lastCalculatePathVersion;

    private static int s_obstacleCacheSize;
    // typedef std::vector<struct SCachedObstacle *> TCachedObstacles;
    private class TCachedObstacles : List<SCachedObstacle> { }
    private static TCachedObstacles s_cachedObstacles = new TCachedObstacles();

    private static TPathObstacles s_pathObstacles = new TPathObstacles();

    // typedef std::map<EntityId, bool> TCachedDynamicObstacleFlags;
    private static SortedDictionary<uint, bool> s_dynamicObstacleFlags = new SortedDictionary<uint, bool>();
    private const int s_maxDynamicObstacleFlags = 256;

    private static SCachedObstacle GetOrClearCachedObstacle(IPhysicalEntity entity, float extraRadius) { return null; /* impl in .cpp */ }
    private static void AddCachedObstacle(SCachedObstacle obstacle) { /* impl in .cpp */ }
    private static SCachedObstacle GetNewCachedObstacle() { return null; /* impl in .cpp */ }

    public bool AddEntityBoxesToObstacles(IPhysicalEntity entity, TPathObstacles obstacles, float extraRadius, float terrainZ, bool debug) { return false; /* impl in .cpp */ }
    public void DebugDraw() { /* impl in .cpp */ }

    public struct SDebugBox
    {
        public OBB obb;
        public Quat q;
        public Vec3 pos;
    }
#if PATHOBSTACLES_DEBUG
    public /*mutable*/ List<SDebugBox> m_debugPathAdjustmentBoxes = new List<SDebugBox>();
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
public struct Sphere { public Vec3 center; public float radius; }
public class SCachedObstacle { }
