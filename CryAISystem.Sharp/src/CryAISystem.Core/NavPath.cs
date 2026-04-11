// Literal port of dev/Code/CryEngine/CryAISystem/NavPath.h
// .cpp impl (2065L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// CNavPath was forward-declared in PipeUser.cs as `public class CNavPath : INavPath { }`
// Replace with full literal port. Use new file with same name to override.
public class CNavPathReal : INavPath
{
    public CNavPathReal() { /* impl in .cpp */ }
    // virtual ~CNavPath();

    public NavigationMeshID GetMeshID() { return new NavigationMeshID(); /* impl in .cpp */ }

    public int GetVersion() { return m_version.v; }

    public void SetVersion(int v) { m_version.v = v; }

    public void SetParams(SNavPathParams parameters) { m_params = parameters; }
    public SNavPathParams GetParams() { return m_params; }

    public void Draw(Vec3 drawOffset = default) { /* impl in .cpp */ }
    public void Dump(string name) { /* impl in .cpp */ }

    public float GetPathLength(bool b2D) { return 0; /* impl in .cpp */ }

    public void PushFront(PathPointDescriptor newPathPoint, bool force = false) { /* impl in .cpp */ }

    public void PushBack(PathPointDescriptor newPathPoint, bool force = false) { /* impl in .cpp */ }

    public void Clear(string dbgString) { /* impl in .cpp */ }

    public bool Advance(out PathPointDescriptor nextPathPoint) { nextPathPoint = null; return false; /* impl in .cpp */ }

    public bool GetPathEndIsAsRequested() { return m_pathEndIsAsRequested; }
    public void SetPathEndIsAsRequested(bool val) { m_pathEndIsAsRequested = val; }

    public bool Empty() { return false; /* impl in .cpp */ }

    public PathPointDescriptor GetLastPathPoint() { return m_pathPoints.Count == 0 ? null : m_pathPoints[m_pathPoints.Count - 1]; }
    public PathPointDescriptor GetPrevPathPoint() { return m_pathPoints.Count == 0 ? null : m_pathPoints[0]; }
    public PathPointDescriptor GetNextPathPoint() { return m_pathPoints.Count > 1 ? m_pathPoints[1] : null; }
    public PathPointDescriptor GetNextNextPathPoint() { return m_pathPoints.Count > 2 ? m_pathPoints[2] : null; }

    public Vec3 GetNextPathPos(Vec3 defaultPos = default) { PathPointDescriptor ppd = GetNextPathPoint(); return ppd != null ? ppd.vPos : defaultPos; }
    public Vec3 GetLastPathPos(Vec3 defaultPos = default) { PathPointDescriptor ppd = GetLastPathPoint(); return ppd != null ? ppd.vPos : defaultPos; }

    public bool GetPosAlongPath(out Vec3 posOut, float dist = 0.0f, bool b2D = false, bool bExtrapolateBeyondEnd = false, IAISystem_ENavigationType[] pNextPointType = null)
    { posOut = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }

    public float GetDistToPath(out Vec3 pathPosOut, out float distAlongPathOut, Vec3 pos, float dist, bool twoD)
    { pathPosOut = pos; distAlongPathOut = 0; return -1; /* impl in .cpp */ }

    public void GetDirectionToPathFromPoint(Vec3 point, out Vec3 dirOut) { dirOut = new Vec3(0, 0, 0); /* impl in .cpp */ }

    public float GetDistToSmartObject(bool b2D) { return 0; /* impl in .cpp */ }

    public PathPointDescriptor.SmartObjectNavDataPtr GetLastPathPointAnimNavSOData() { return null; /* impl in .cpp */ }
    public PathPointDescriptor.OffMeshLinkData GetLastPathPointMNNSOData() { return null; /* impl in .cpp */ }

    public void SetPreviousPoint(PathPointDescriptor previousPoint) { /* impl in .cpp */ }

    public TPathPoints GetPath() { return m_pathPoints; }

    public void SetPathPoints(TPathPoints points) { m_pathPoints = points; ++m_version.v; }

    public float GetMaxDistanceFromStart() { return 0; /* impl in .cpp */ }

    public AABB GetAABB(float dist) { return new AABB(); /* impl in .cpp */ }

    public bool GetPathPropertiesAhead(float distAhead, bool twoD, out Vec3 posOut, out Vec3 dirOut,
        out float invROut, out float lowestPathDotOut, bool scaleOutputWithDist)
    { posOut = new Vec3(0, 0, 0); dirOut = new Vec3(0, 0, 0); invROut = 0; lowestPathDotOut = 0; return false; /* impl in .cpp */ }

    public void Serialize(TSerialize ser) { /* impl in .cpp */ }

    public Vec3 GetEndDir() { return m_endDir; }
    public void SetEndDir(Vec3 endDir) { m_endDir = endDir; }

    public bool UpdateAndSteerAlongPath(out Vec3 dirOut, out float distToEndOut, out float distToPathOut, out bool isResolvingSticking,
        out Vec3 pathDirOut, out Vec3 pathAheadDirOut, out Vec3 pathAheadPosOut, Vec3 currentPos, Vec3 currentVel,
        float lookAhead, float pathRadius, float dt, bool resolveSticking, bool twoD)
    {
        dirOut = new Vec3(0, 0, 0); distToEndOut = 0; distToPathOut = 0; isResolvingSticking = false;
        pathDirOut = new Vec3(0, 0, 0); pathAheadDirOut = new Vec3(0, 0, 0); pathAheadPosOut = new Vec3(0, 0, 0);
        return false; /* impl in .cpp */
    }

    public void PrepareNavigationalSmartObjectsForMNM(IAIPathAgent pAgent) { /* impl in .cpp */ }
    public void ResurrectRemainingPath() { /* impl in .cpp */ }

    public void TrimPath(float length, bool twoD) { /* impl in .cpp */ }

    public float GetDiscardedPathLength() { return m_fDiscardedPathLength; }

    public bool AdjustPathAroundObstacles(CPathObstacles obstacles, uint navCapMask) { return false; /* impl in .cpp */ }
    public bool AdjustPathAroundObstacles(Vec3 currentpos, AgentMovementAbility movementAbility) { return false; /* impl in .cpp */ }

    public float UpdatePathPosition(Vec3 agentPos, float pathLookahead, bool twoD, bool allowPathToFinish) { return 0; /* impl in .cpp */ }

    public Vec3 CalculateTargetPos(Vec3 agentPos, float lookAhead, float minLookAheadAlongPath, float pathRadius, bool twoD) { return agentPos; /* impl in .cpp */ }

    public ETriState CanTargetPointBeReached(CTargetPointRequest request, CAIActor pAIActor, bool twoD) { return ETriState.eTS_maybe; /* impl in .cpp */ }
    public bool UseTargetPointRequest(CTargetPointRequest request, CAIActor pAIActor, bool twoD) { return false; /* impl in .cpp */ }

    public virtual void Release() { /* delete this */ }
    public virtual void CopyTo(INavPath pRecipient) { /* impl in .cpp */ }
    public virtual INavPath Clone() { return new CNavPathReal(); /* impl in .cpp */ }

    private bool AdjustPathAroundObstacle(CPathObstacle obstacle, uint navCapMask) { return false; /* impl in .cpp */ }
    private bool AdjustPathAroundObstacleShape2D(SPathObstacleShape2D obstacle, uint navCapMask) { return false; /* impl in .cpp */ }
    private bool CheckPath(TPathPoints pathList, float radius) { return false; /* impl in .cpp */ }
    private void MovePathEndsOutOfObstacles(CPathObstacles obstacles) { /* impl in .cpp */ }

    private float GetPathDeviationDistance(out Vec3 deviationOut, float criticalDeviation, bool twoD) { deviationOut = new Vec3(0, 0, 0); return 0; /* impl in .cpp */ }

    private TPathPoints m_pathPoints = new TPathPoints();
    private Vec3 m_endDir;

    private struct SDebugLine
    {
        public Vec3 P0, P1;
        public ColorF col;
    }
    private void DebugLine(Vec3 P0, Vec3 P1, ColorF col) { /* impl in .cpp */ }
    private /*mutable*/ LinkedList<SDebugLine> m_debugLines = new LinkedList<SDebugLine>();

    private struct SDebugSphere
    {
        public Vec3 pos; public float r; public ColorF col;
    }
    private void DebugSphere(Vec3 P, float r, ColorF col) { /* impl in .cpp */ }
    private /*mutable*/ LinkedList<SDebugSphere> m_debugSpheres = new LinkedList<SDebugSphere>();

    private float m_currentFrac;

    private float m_stuckTime;

    private bool m_pathEndIsAsRequested;

    private SNavPathParams m_params = new SNavPathParams();

    private float m_fDiscardedPathLength;
    private TPathPoints m_remainingPathPoints = new TPathPoints();

    private CPathObstacles m_obstacles = new CPathObstacles();

    private struct SVersion
    {
        public int v;
        public void Assign(SVersion other) { v = other.v + 1; }
        public void Serialize(TSerialize ser) { /* impl in .cpp */ }
    }
    private SVersion m_version = new SVersion { v = -1 };
}

// Forward decls / shells
public struct NavigationMeshID { public uint id; }
public class SNavPathParams { }
public class PathPointDescriptor
{
    public Vec3 vPos;
    public class SmartObjectNavDataPtr { }
    public class OffMeshLinkData { }
}
public class TPathPoints : List<PathPointDescriptor> { }
public class CPathObstacle { }
public class SPathObstacleShape2D { }
