// Literal port shell of dev/Code/CryEngine/CryAISystem/SmartPathFollower.h
// Full literal port of declarations + .cpp impl (1540L combined) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

public struct SPathControlPoint2
{
    public SPathControlPoint2(IAISystem_ENavigationType navType, Vec3 pos, ushort customId = 0)
    {
        this.navType = navType; this.pos = pos; this.customId = customId; this.distance = 0;
    }
    public Vec3 pos;
    public IAISystem_ENavigationType navType;
    public float distance;
    public ushort customId;
}

public class InterpolatedPath
{
    public InterpolatedPath() { m_totalDistance = 0; }

    public float FindNextSegmentIndex(nuint startIndex) { return 0; /* impl in .cpp */ }
    public float FindSegmentIndexAtDistance(float distance, nuint startIndex = 0) { return 0; /* impl in .cpp */ }
    public float FindClosestSegmentIndex(Vec3 testPoint, float startDistance = 0.0f, float endDistance = float.MaxValue, float toleranceZ = 0.5f) { return 0; /* impl in .cpp */ }
    public bool IsParrallelTo(Lineseg line, float startIndex, float endIndex, float maxDeviation) { return false; /* impl in .cpp */ }
    public Vec3 GetPositionAtSegmentIndex(float index) { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public float GetDistanceAtSegmentIndex(float index) { return 0; /* impl in .cpp */ }
    public void GetLineSegment(float startIndex, float endIndex, out Lineseg segment) { segment = new Lineseg(); /* impl in .cpp */ }
    public nuint FindNextNavTypeSectionIndexAfter(nuint index) { return 0; /* impl in .cpp */ }
    public float FindNextInflectionIndex(float startIndex, float maxDeviation = 0.1f) { return 0; /* impl in .cpp */ }
    public void ShortenToIndex(float endIndex) { /* impl in .cpp */ }
    public float TotalDistance() { return m_totalDistance; }

    public void clear() { m_points.Clear(); m_totalDistance = 0.0f; }

    private List<SPathControlPoint2> m_points = new List<SPathControlPoint2>();
    private float m_totalDistance;
}

public class CSmartPathFollower : IPathFollower
{
    public CSmartPathFollower(PathFollowerParams parameters, IPathObstacles pathObstacleObject) { /* impl in .cpp */ }
    public CSmartPathFollower() { /* impl in .cpp */ }
    // ~CSmartPathFollower();

    public virtual void Reset() { /* impl in .cpp */ }
    public virtual void AttachToPath(INavPath pNavPath) { /* impl in .cpp */ }
    public virtual void SetParams(PathFollowerParams parameters) { /* impl in .cpp */ }
    public virtual PathFollowerParams GetParams() { return new PathFollowerParams(); /* impl in .cpp */ }
    public virtual bool Update(PathFollowResult result, Vec3 curPos, Vec3 curVel, float dt) { return false; /* impl in .cpp */ }
    public virtual void Advance(float distance) { /* impl in .cpp */ }
    public virtual float GetDistToEnd(Vec3? pCurPos) { return 0; /* impl in .cpp */ }
    public virtual float GetDistToSmartObject() { return 0; /* impl in .cpp */ }
    public virtual float GetDistToNavType(IAISystem_ENavigationType navType) { return 0; /* impl in .cpp */ }
    public virtual Vec3 GetPathPointAhead(float dist, out float actualDist) { actualDist = dist; return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public virtual void Draw(Vec3 drawOffset = default) { /* impl in .cpp */ }
    public virtual void Serialize(TSerialize ser) { /* impl in .cpp */ }
    public virtual bool CheckWalkability(Vec2[] path, nuint length) { return false; /* impl in .cpp */ }
    public virtual bool GetAllowCuttingCorners() { return true; /* impl in .cpp */ }
    public virtual void SetAllowCuttingCorners(bool allowCuttingCorners) { /* impl in .cpp */ }

    public virtual void Release() { /* delete this */ }
}
