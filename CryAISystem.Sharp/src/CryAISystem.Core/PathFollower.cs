// Literal port of dev/Code/CryEngine/CryAISystem/PathFollower.h
// .cpp impl (898L) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

/// This implements path following in a rather simple - and therefore cheap way.
public class CPathFollower : IPathFollower
{
    /// The control points are attached to the original path, but can be dynamically adjusted
    /// according to the pathRadius.
    private struct SPathControlPoint
    {
        public SPathControlPoint(IAISystem_ENavigationType navType, Vec3 pos, Vec3 offsetDir, float offsetAmount)
        {
            this.navType = navType;
            this.pos = pos;
            this.offsetDir = offsetDir;
            this.offsetAmount = offsetAmount;
            this.customId = 0;
        }

        public IAISystem_ENavigationType navType;
        public Vec3 pos;
        public Vec3 offsetDir;
        public float offsetAmount;
        public ushort customId;
    }
    // typedef std::vector<SPathControlPoint> TPathControlPoints;

    private List<SPathControlPoint> m_pathControlPoints = new List<SPathControlPoint>();

    private PathFollowerParams m_params;

    private int m_curLASegmentIndex;

    private Vec3 m_curLAPos;

    private float m_lastOutputSpeed;

    private INavPath m_navPath;

    private int m_pathVersion;


    private Vec3 m_CurPos;
    private uint m_CurIndex;

    private Vec3 GetPathControlPoint(uint iPt)
    {
        return m_pathControlPoints[(int)iPt].pos + m_pathControlPoints[(int)iPt].offsetDir * (m_pathControlPoints[(int)iPt].offsetAmount * 1.0f /* m_params.pathRadius */);
    }

    private float DistancePointPoint(Vec3 pt1, Vec3 pt2) { return 0; /* impl in .cpp */ }
    private float DistancePointPointSq(Vec3 pt1, Vec3 pt2) { return 0; /* impl in .cpp */ }

    private void ProcessPath() { /* impl in .cpp */ }

    private void StartFollowing(Vec3 curPos, Vec3 curVel) { /* impl in .cpp */ }

    private void GetNewLookAheadPos(out Vec3 newLAPos, out int newLASegIndex, Vec3 curLAPos, int curLASegIndex,
        Vec3 curPos, Vec3 curVel, float dt) { newLAPos = curLAPos; newLASegIndex = curLASegIndex; /* impl in .cpp */ }

    private void UseLookAheadPos(out Vec3 velocity, out bool reachedEnd, Vec3 LAPos, Vec3 curPos, Vec3 curVel,
        float dt, int curLASegmentIndex, ref float lastOutputSpeed) { velocity = new Vec3(0, 0, 0); reachedEnd = false; /* impl in .cpp */ }

    private Vec3 GetPathPointAhead(float dist, ref float actualDist, int curLASegmentIndex, Vec3 curLAPos) { actualDist = dist; return curLAPos; /* impl in .cpp */ }

    private float GetDistToEnd(Vec3? pCurPos, int curLASegmentIndex, Vec3 curLAPos) { return 0; /* impl in .cpp */ }

    public virtual void Release() { /* delete this */ }

    private uint GetIndex(Vec3 pos) { return 0; /* impl in .cpp */ }

    public CPathFollower() : this(new PathFollowerParams()) { }
    public CPathFollower(PathFollowerParams parameters) { m_params = parameters; /* impl in .cpp */ }
    // ~CPathFollower();

    public virtual void Reset() { /* impl in .cpp */ }

    public virtual void AttachToPath(INavPath pNavPath) { /* impl in .cpp */ }

    public virtual void SetParams(PathFollowerParams parameters) { m_params = parameters; }

    public virtual PathFollowerParams GetParams() { return m_params; }

    public virtual bool Update(PathFollowResult result, Vec3 curPos, Vec3 curVel, float dt) { return false; /* impl in .cpp */ }

    public virtual void Advance(float distance) { /* impl in .cpp */ }

    public virtual float GetDistToEnd(Vec3? pCurPos) { return 0; /* impl in .cpp */ }

    public virtual float GetDistToSmartObject() { return 0; /* impl in .cpp */ }
    public virtual float GetDistToNavType(IAISystem_ENavigationType navType) { return 0; /* impl in .cpp */ }
    private float GetDistToCustomNav(List<SPathControlPoint> controlPoints, uint curLASegmentIndex, Vec3 curLAPos) { return 0; /* impl in .cpp */ }

    public virtual Vec3 GetPathPointAhead(float dist, out float actualDist) { actualDist = dist; return new Vec3(0, 0, 0); /* impl in .cpp */ }

    public virtual void Draw(Vec3 drawOffset = default) { /* impl in .cpp */ }

    public virtual void Serialize(TSerialize ser) { /* impl in .cpp */ }

    public virtual bool CheckWalkability(Vec2[] path, nuint length) { return false; /* impl in .cpp */ }

    public virtual bool GetAllowCuttingCorners() { return true; /* impl in .cpp */ }
    public virtual void SetAllowCuttingCorners(bool allowCuttingCorners) { /* impl in .cpp */ }
}

// Forward decl shells
public class PathFollowResult { }
