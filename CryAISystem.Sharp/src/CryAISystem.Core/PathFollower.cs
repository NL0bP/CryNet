// Literal port of dev/Code/CryEngine/CryAISystem/PathFollower.h + PathFollower.cpp (898L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.AILog;
using CryAISystem.CryCommon;

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
        return m_pathControlPoints[(int)iPt].pos + m_pathControlPoints[(int)iPt].offsetDir * (m_pathControlPoints[(int)iPt].offsetAmount * m_params.pathRadius);
    }
    private Vec3 GetPathControlPoint(int iPt)
    {
        return m_pathControlPoints[iPt].pos + m_pathControlPoints[iPt].offsetDir * (m_pathControlPoints[iPt].offsetAmount * m_params.pathRadius);
    }

    //===================================================================
    // DistancePointPoint
    //===================================================================
    private float DistancePointPoint(Vec3 pt1, Vec3 pt2)
    {
        return m_params.use2D ? Distance.Point_Point2D(pt1, pt2) : Distance.Point_Point(pt1, pt2);
    }

    //===================================================================
    // DistancePointPointSq
    //===================================================================
    private float DistancePointPointSq(Vec3 pt1, Vec3 pt2)
    {
        return m_params.use2D ? Distance.Point_Point2DSq(pt1, pt2) : Distance.Point_PointSq(pt1, pt2);
    }

    //===================================================================
    // ProcessPath
    //===================================================================
    private void ProcessPath()
    {
        uint swingOutTypes = (uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN | (uint)IAISystem_ENavigationType.NAV_VOLUME;

        m_pathVersion = (int)m_navPath.GetVersion();

        m_pathControlPoints.Clear();

        CNavPathReal pNavPath = m_navPath as CNavPathReal;
        if (pNavPath == null) return;
        TPathPoints pathPts = pNavPath.GetPath();

        if (pathPts.Count < 2)
            return;

        float totalLength = 0.0f;
        Vec3 prevPos = pathPts[0].vPos;
        foreach (PathPointDescriptor ppd in pathPts)
        {
            Vec3 delta = ppd.vPos - prevPos;
            totalLength += m_params.use2D ? delta.GetLength2D() : delta.Length();
            prevPos = ppd.vPos;
        }

        bool useSwingout = true;

        float distFromStart = 0.0f;
        float distFromEnd = totalLength;

        float startSwingoutDist = 1.0f;
        float endSwingoutDist = 1.0f;
        float swingoutAmount = 1.0f;
        float criticalDot = 0.9f;

        Vec3 oldPos = pathPts[0].vPos;
        for (int itIdx = 0; itIdx < pathPts.Count; ++itIdx)
        {
            float pathCurveDist = 2.0f;
            PathPointDescriptor ppd = pathPts[itIdx];

            float prevSegLen = m_params.use2D ? Distance.Point_Point2D(ppd.vPos, oldPos) : Distance.Point_Point(ppd.vPos, oldPos);
            distFromStart += prevSegLen;
            distFromEnd -= prevSegLen;

            if (useSwingout && distFromStart > startSwingoutDist && distFromEnd > endSwingoutDist && ((uint)ppd.navType & swingOutTypes) != 0)
            {
                // current pt
                {
                    Vec3 prevPos2 = GetPathPointAheadStatic(pathPts, itIdx, -pathCurveDist);
                    Vec3 nextPos = GetPathPointAheadStatic(pathPts, itIdx, pathCurveDist);

                    Vec3 swingOutDir = -(nextPos + prevPos2 - 2.0f * ppd.vPos) / square(pathCurveDist);
                    float diff2Len = swingOutDir.NormalizeSafe();

                    float swingoutScale = 1.0f;
                    float swingout = diff2Len * swingoutScale;
                    Limit(ref swingout, 0.0f, 1.0f);

                    Vec3 prevDir = ppd.vPos - oldPos;
                    if (m_params.use2D) prevDir.z = 0.0f;
                    prevDir = prevDir.GetNormalizedSafe();

                    int itNext = itIdx + 1;
                    Vec3 nextPt = (itNext >= pathPts.Count) ? ppd.vPos : pathPts[itNext].vPos;
                    Vec3 nextDir = nextPt - ppd.vPos;
                    if (m_params.use2D) nextDir.z = 0.0f;
                    nextDir = nextDir.GetNormalizedSafe();

                    Vec3 prevOutDir = new Vec3(prevDir.y, -prevDir.x, 0.0f);
                    Vec3 nextOutDir = new Vec3(nextDir.y, -nextDir.x, 0.0f);

                    if (prevOutDir.Dot(swingOutDir) < 0.0f)
                        prevOutDir = prevOutDir * -1.0f;
                    if (nextOutDir.Dot(swingOutDir) < 0.0f)
                        nextOutDir = nextOutDir * -1.0f;

                    if (prevDir.Dot(nextDir) > criticalDot)
                    {
                        SPathControlPoint cp = new SPathControlPoint(ppd.navType, ppd.vPos, (prevOutDir + nextOutDir).GetNormalizedSafe(), swingout * swingoutAmount);
                        cp.customId = ppd.navTypeCustomId;
                        m_pathControlPoints.Add(cp);
                    }
                    else
                    {
                        SPathControlPoint cp1 = new SPathControlPoint(ppd.navType, ppd.vPos, prevOutDir, swingout * swingoutAmount);
                        cp1.customId = ppd.navTypeCustomId;
                        m_pathControlPoints.Add(cp1);
                        SPathControlPoint cp2 = new SPathControlPoint(ppd.navType, ppd.vPos, nextOutDir, swingout * swingoutAmount);
                        cp2.customId = ppd.navTypeCustomId;
                        m_pathControlPoints.Add(cp2);
                    }
                }
            }
            else
            {
                SPathControlPoint cp = new SPathControlPoint(ppd.navType, ppd.vPos, new Vec3(0, 0, 0), 0.0f);
                cp.customId = ppd.navTypeCustomId;
                m_pathControlPoints.Add(cp);
            }
            oldPos = ppd.vPos;
        }

        // Now cut the path short if we should stop before the end
        if (m_params.endDistance > 0.0f)
        {
            float cutDist = 0.0f;
            for (int iPt = m_pathControlPoints.Count - 2; iPt >= 0; --iPt)
            {
                int iNext = iPt + 1;
                Vec3 pt = GetPathControlPoint(iPt);
                Vec3 next = GetPathControlPoint(iNext);
                float dist = DistancePointPoint(pt, next);
                if (m_pathControlPoints.Count > 2 && (dist < 0.01f || cutDist + dist < m_params.endDistance))
                {
                    m_pathControlPoints.RemoveAt(m_pathControlPoints.Count - 1);
                    cutDist += dist;
                }
                else
                {
                    float frac = clamp_tpl(1.0f - (m_params.endDistance - cutDist) / dist, 0.001f, 1.0f);
                    SPathControlPoint last = m_pathControlPoints[m_pathControlPoints.Count - 1];
                    last.pos = pt + (next - pt) * frac;
                    m_pathControlPoints[m_pathControlPoints.Count - 1] = last;
                    break;
                }
            }
        }

        Lineseg lastSeg;
        lastSeg.end = m_pathControlPoints[m_pathControlPoints.Count - 1].pos;
        lastSeg.start = m_pathControlPoints[0].pos;
        for (int iPt = m_pathControlPoints.Count - 1; iPt >= 0; --iPt)
        {
            Vec3 pt = m_pathControlPoints[iPt].pos;
            float dist = DistancePointPoint(pt, lastSeg.end);
            if (dist > 0.01f)
            {
                lastSeg.start = pt;
                break;
            }
        }
        if (m_params.use2D)
            lastSeg.end.z = lastSeg.start.z;

        // replicate the last point, but advance it in the last segment direction
        SPathControlPoint lastCP = m_pathControlPoints[m_pathControlPoints.Count - 1];
        Vec3 dir = (lastSeg.end - lastSeg.start).GetNormalizedSafe(new Vec3(1, 0, 0));
        lastCP.offsetDir = dir;
        lastCP.offsetAmount = m_params.stopAtEnd ? 0.01f : 10.0f;
        m_pathControlPoints.Add(lastCP);

        m_curLASegmentIndex = 0;
        m_curLAPos = GetPathControlPoint(0);
    }

    //===================================================================
    // StartFollowing
    //===================================================================
    private void StartFollowing(Vec3 curPos, Vec3 curVel)
    {
        float bestPathDistSq = float.MaxValue;
        int nPts = m_pathControlPoints.Count;
        for (int i = 0; i < nPts - 1; ++i)
        {
            float t;
            Lineseg lineseg = new Lineseg(GetPathControlPoint(i), GetPathControlPoint(i + 1));
            float distSq = Distance.Point_Lineseg2DSq(curPos, lineseg, out t);
            if (distSq < bestPathDistSq)
            {
                bestPathDistSq = distSq;
                m_curLAPos = lineseg.GetPoint(t);
                m_curLASegmentIndex = i;
            }
        }

        Vec3 newLAPos;
        int newLASegIndex;
        GetNewLookAheadPos(out newLAPos, out newLASegIndex, m_curLAPos, m_curLASegmentIndex, curPos, curVel, 100.0f);
        m_curLAPos = newLAPos;
        m_curLASegmentIndex = newLASegIndex;

        Vec3 velDir = curVel;
        Vec3 moveDir = m_curLAPos - curPos;
        if (m_params.use2D)
        {
            velDir.z = 0.0f;
            moveDir.z = 0.0f;
        }
        velDir = velDir.GetNormalizedSafe();
        moveDir = moveDir.GetNormalizedSafe();
        float dot = velDir.Dot(moveDir);
        Limit(ref dot, 0.0f, 1.0f);
        float curSpeed = m_params.use2D ? curVel.GetLength2D() : curVel.Length();
        m_lastOutputSpeed = curSpeed * dot;
    }

    //===================================================================
    // GetNewLookAheadPos
    //===================================================================
    private void GetNewLookAheadPos(out Vec3 newLAPos, out int newLASegIndex, Vec3 curLAPos, int curLASegIndex,
        Vec3 curPos, Vec3 curVel, float dt)
    {
        newLASegIndex = curLASegIndex;
        newLAPos = curLAPos;

        int nPts = m_pathControlPoints.Count;
        if (nPts < 2)
            return;

        float probeDist = m_params.pathLookAheadDist * 0.01f;

        float maxDistScale = 3.0f;
        float maxProbeDist = maxDistScale * dt * m_params.maxSpeed;
        float totalProbeDist = 0.0f;

        float curDist;

        // CALC_SEGMENT macro equivalent
        Vec3 prevPt = GetPathControlPoint(newLASegIndex);
        int nextPtIndex = newLASegIndex + 1;
        if (nextPtIndex == nPts) { newLAPos = prevPt; return; }
        Vec3 nextPt = GetPathControlPoint(nextPtIndex);
        float segLen = DistancePointPoint(prevPt, nextPt);
        float distToNext = DistancePointPoint(newLAPos, nextPt);

        while ((curDist = DistancePointPoint(newLAPos, curPos)) < m_params.pathLookAheadDist)
        {
            float thisProbeDist = max(probeDist, m_params.pathLookAheadDist - curDist);
            bool returnEarly = false;
            if (thisProbeDist + totalProbeDist > maxProbeDist)
            {
                thisProbeDist = maxProbeDist - totalProbeDist;
                returnEarly = true;
            }

            if (thisProbeDist > distToNext)
            {
                newLAPos = nextPt;
                ++newLASegIndex;
                totalProbeDist += distToNext;
                if (newLASegIndex > nPts - 2)
                {
                    newLASegIndex = nPts - 2;
                    return;
                }
                // CALC_SEGMENT
                prevPt = GetPathControlPoint(newLASegIndex);
                nextPtIndex = newLASegIndex + 1;
                if (nextPtIndex == nPts) { newLAPos = prevPt; return; }
                nextPt = GetPathControlPoint(nextPtIndex);
                segLen = DistancePointPoint(prevPt, nextPt);
                distToNext = DistancePointPoint(newLAPos, nextPt);
            }
            else
            {
                float distFromNext = distToNext - thisProbeDist;
                float frac = segLen > 0.0f ? distFromNext / segLen : 0.0f;
                newLAPos = frac * prevPt + (1.0f - frac) * nextPt;
                totalProbeDist += thisProbeDist;
                if (returnEarly)
                    return;
                distToNext -= thisProbeDist;
            }
        }
    }

    //===================================================================
    // UseLookAheadPos
    //===================================================================
    private void UseLookAheadPos(out Vec3 velocity, out bool reachedEnd, Vec3 LAPos, Vec3 curPos, Vec3 curVel,
        float dt, int curLASegmentIndex, ref float lastOutputSpeed)
    {
        velocity = LAPos - curPos;
        if (m_params.use2D)
            velocity.z = 0.0f;
        float dist = velocity.NormalizeSafe();
        float speed = m_params.normalSpeed * dist / m_params.pathLookAheadDist;

        // speed control depending on curvature
        bool doCurvatureSpeedControl = true;
        if (doCurvatureSpeedControl)
        {
            float actualPathDist = 0;
            float pathDist = 3.0f;
            Vec3 posAhead = GetPathPointAhead(pathDist, ref actualPathDist, curLASegmentIndex, LAPos);
            if (actualPathDist > 0.1f)
            {
                float distToPosAhead = DistancePointPoint(posAhead, LAPos);
                float ratio = distToPosAhead / actualPathDist;
                float minSpeedScale = 0.5f;
                float multiplier = 3.0f;
                float speedScale = 1 + (ratio - 1) * multiplier;
                Limit(ref speedScale, minSpeedScale, 1.0f);
                speed *= speedScale;
            }
        }

        // speed control based on slowing down to stop at the end
        if (m_params.stopAtEnd)
        {
            float cutoffDist = 0.5f * square(speed) / min(3.0f, m_params.maxDecel);
            float directDistToEnd = DistancePointPoint(curPos, GetPathControlPoint(m_pathControlPoints.Count - 1));
            if (directDistToEnd < cutoffDist)
            {
                float distToEnd = GetDistToEnd(curPos, curLASegmentIndex, LAPos);
                float maxSpeed = sqrtf(2.0f * m_params.maxDecel * distToEnd);
                if (speed > maxSpeed)
                    speed = maxSpeed;
            }
        }

        float maxOutputSpeed = min(lastOutputSpeed + dt * m_params.maxAccel, m_params.maxSpeed);
        float minOutputSpeed = m_params.stopAtEnd ? 0.0f : max(lastOutputSpeed - dt * m_params.maxDecel, m_params.minSpeed);

        Limit(ref speed, minOutputSpeed, maxOutputSpeed);
        velocity = velocity * speed;
        lastOutputSpeed = speed;

        // only finish when we go just beyond the start of the last segment
        if (curLASegmentIndex >= m_pathControlPoints.Count - 2)
        {
            Vec3 segStart = GetPathControlPoint(m_pathControlPoints.Count - 2);
            float distSq = (curPos - segStart).GetLengthSquared2D();

            if (m_params.stopAtEnd)
                reachedEnd = distSq < 0.05f * 0.05f;
            else
                reachedEnd = distSq < 0.25f * 0.25f;
        }
        else
            reachedEnd = false;
    }

    //===================================================================
    // GetIndex
    //===================================================================
    private uint GetIndex(Vec3 pos)
    {
        float bestPathDistSq = float.MaxValue;
        int nPts = m_pathControlPoints.Count;
        uint ret = 0;

        for (int i = 0; i < nPts - 1; ++i)
        {
            float t;
            Lineseg lineseg = new Lineseg(GetPathControlPoint(i), GetPathControlPoint(i + 1));
            float distSq = Distance.Point_Lineseg2DSq(pos, lineseg, out t);
            if (distSq < bestPathDistSq)
            {
                bestPathDistSq = distSq;
                ret = (uint)i;
            }
        }

        return ret;
    }

    public CPathFollower() : this(new PathFollowerParams()) { }
    public CPathFollower(PathFollowerParams parameters)
    {
        m_params = parameters;
        m_pathVersion = -2;
        m_curLASegmentIndex = 0;
        m_curLAPos = new Vec3(0, 0, 0);
        m_lastOutputSpeed = 0.0f;
        m_navPath = null;
        m_CurPos = new Vec3(0, 0, 0);
        m_CurIndex = 0;
    }
    // ~CPathFollower();

    //===================================================================
    // Reset
    //===================================================================
    public virtual void Reset()
    {
        m_pathVersion = -2;
        m_curLASegmentIndex = 0;
        m_curLAPos = new Vec3(0, 0, 0);
        m_lastOutputSpeed = 0.0f;
        m_pathControlPoints.Clear();
    }

    //===================================================================
    // AttachToPath
    //===================================================================
    public virtual void AttachToPath(INavPath pNavPath)
    {
        m_navPath = pNavPath;
    }

    public virtual void SetParams(PathFollowerParams parameters) { m_params = parameters; }

    public virtual PathFollowerParams GetParams() { return m_params; }

    //===================================================================
    // Update
    //===================================================================
    public virtual bool Update(PathFollowResult result, Vec3 curPos, Vec3 curVel, float dt)
    {
        float curSpeed = m_params.use2D ? curVel.GetLength2D() : curVel.Length();

        if (m_pathVersion != m_navPath.GetVersion())
        {
            ProcessPath();
            StartFollowing(curPos, curVel);
        }

        float speedClampTol = 0.5f;
        if (m_lastOutputSpeed > curSpeed + speedClampTol)
            m_lastOutputSpeed = curSpeed + speedClampTol;

        result.velocityOut = new Vec3(0, 0, 0);
        if (result.predictedStates != null)
            result.predictedStates.Clear();
        result.reachedEnd = true;

        if (m_pathControlPoints.Count < 2)
            return false;

        Vec3 newLAPos;
        int newLASegIndex;
        GetNewLookAheadPos(out newLAPos, out newLASegIndex, m_curLAPos, m_curLASegmentIndex, curPos, curVel, dt);

        m_CurPos = curPos;
        m_CurIndex = GetIndex(curPos);

        m_curLAPos = newLAPos;
        m_curLASegmentIndex = newLASegIndex;

        UseLookAheadPos(out result.velocityOut, out result.reachedEnd, m_curLAPos, curPos, curVel, dt, m_curLASegmentIndex, ref m_lastOutputSpeed);

        if (result.reachedEnd)
        {
            result.velocityOut = new Vec3(0, 0, 0);
            return true;
        }

        if (result.predictedStates != null)
        {
            result.predictedStates.Clear();
            int nDesiredPredictions = (int)(result.desiredPredictionTime / result.predictionDeltaTime + 0.5f);

            float idealDt = 0.05f;
            int stepsPerPrediction = (int)(result.predictionDeltaTime / idealDt);
            if (stepsPerPrediction < 1) stepsPerPrediction = 1;
            float actualDt = result.predictionDeltaTime / stepsPerPrediction;

            Vec3 predCurLAPos = m_curLAPos;
            int predCurLASegmentIndex = m_curLASegmentIndex;
            Vec3 predCurPos = curPos;
            Vec3 predCurVel = curVel;
            float lastOutputSpeedPred = m_lastOutputSpeed;

            for (int iPrediction = 0; iPrediction != nDesiredPredictions; ++iPrediction)
            {
                for (int iStep = 0; iStep < stepsPerPrediction; ++iStep)
                {
                    Vec3 newLAPos2;
                    int newLASegIndex2;
                    GetNewLookAheadPos(out newLAPos2, out newLASegIndex2, predCurLAPos, predCurLASegmentIndex, predCurPos, predCurVel, actualDt);

                    predCurLAPos = newLAPos2;
                    predCurLASegmentIndex = newLASegIndex2;

                    Vec3 velocity2;
                    bool reachedEnd2;
                    UseLookAheadPos(out velocity2, out reachedEnd2, predCurLAPos, predCurPos, predCurVel, actualDt, predCurLASegmentIndex, ref lastOutputSpeedPred);

                    if (reachedEnd2 && m_params.stopAtEnd)
                    {
                        predCurVel = new Vec3(0, 0, 0);
                        predCurPos = GetPathControlPoint(m_pathControlPoints.Count - 2);
                    }
                    else
                    {
                        predCurPos = predCurPos + predCurVel * actualDt;
                        predCurVel = velocity2;
                    }
                }

                result.predictedStates.Add(new PathFollowResult.SPredictedState(predCurPos, predCurVel));
            }
        }

        if (gAIEnv.CVars.DrawPathFollower == 1)
        {
            Draw();
        }

        // This path follower always *assumes* the follow target is always reachable.
        return true;
    }

    //===================================================================
    // Advance
    //===================================================================
    public virtual void Advance(float distance)
    {
        float distLeft = distance;
        while (distLeft > 0.0f && m_curLASegmentIndex < m_pathControlPoints.Count - 1)
        {
            Vec3 nextPt = GetPathControlPoint(m_curLASegmentIndex + 1);
            float distToNext = DistancePointPoint(m_curLAPos, nextPt);
            if (distToNext > distLeft && distToNext > 0.0f)
            {
                float frac = distLeft / distToNext;
                m_curLAPos = frac * nextPt + (1.0f - frac) * m_curLAPos;
                return;
            }
            distLeft -= distToNext;
            m_curLAPos = nextPt;
            ++m_curLASegmentIndex;
        }
    }

    //===================================================================
    // GetDistToEnd (private, with state)
    //===================================================================
    private float GetDistToEnd(Vec3 curPos, int curLASegmentIndex, Vec3 curLAPos)
    {
        if (m_pathControlPoints.Count == 0)
            return 0.0f;

        float dist = 0.0f;
        int nPts = m_pathControlPoints.Count;
        for (int i = curLASegmentIndex + 1; i < nPts - 2; ++i)
            dist += DistancePointPoint(GetPathControlPoint(i), GetPathControlPoint(i + 1));

        dist += DistancePointPoint(curLAPos, GetPathControlPoint(min(curLASegmentIndex + 1, nPts - 1)));
        dist += DistancePointPoint(curPos, curLAPos);
        return dist;
    }

    //===================================================================
    // GetDistToEnd (public)
    //===================================================================
    public virtual float GetDistToEnd(Vec3? pCurPos)
    {
        if (pCurPos.HasValue)
            return GetDistToEnd(pCurPos.Value, m_curLASegmentIndex, m_curLAPos);
        return GetDistToEnd(m_curLAPos, m_curLASegmentIndex, m_curLAPos);
    }

    //===================================================================
    // GetDistToSmartObject
    //===================================================================
    public virtual float GetDistToSmartObject()
    {
        return GetDistToNavType(IAISystem_ENavigationType.NAV_SMARTOBJECT);
    }

    //===================================================================
    // GetDistToNavType
    //===================================================================
    public virtual float GetDistToNavType(IAISystem_ENavigationType navType)
    {
        uint nPts = (uint)m_pathControlPoints.Count;

        if (m_CurIndex + 1 >= nPts)
            return float.MaxValue;

        float curDist = DistancePointPoint(m_CurPos, m_pathControlPoints[(int)m_CurIndex].pos);
        float totalDist = DistancePointPoint(m_pathControlPoints[(int)m_CurIndex].pos, m_pathControlPoints[(int)m_CurIndex + 1].pos);
        float curFraction = (totalDist > 0.0f) ? (curDist / totalDist) : 0.0f;

        if ((m_pathControlPoints[(int)m_CurIndex].navType == navType) &&
            (m_pathControlPoints[(int)m_CurIndex + 1].navType == navType) &&
            (m_pathControlPoints[(int)m_CurIndex].customId == m_pathControlPoints[(int)m_CurIndex + 1].customId))
            return (curFraction < 0.5f) ? 0.0f : float.MaxValue;

        float dist = 0.0f;
        Vec3 lastPos = m_CurPos;
        for (uint i = m_CurIndex + 1; i < nPts; ++i)
        {
            dist += DistancePointPoint(lastPos, m_pathControlPoints[(int)i].pos);
            if (i + 1 < nPts)
            {
                if ((m_pathControlPoints[(int)i].navType == navType) &&
                    (m_pathControlPoints[(int)i + 1].navType == navType) &&
                    (m_pathControlPoints[(int)i].customId == m_pathControlPoints[(int)i + 1].customId))
                    return dist;
            }
            else
            {
                if (m_pathControlPoints[(int)i].navType == navType)
                    return dist;
            }
            lastPos = m_pathControlPoints[(int)i].pos;
        }
        return float.MaxValue;
    }

    //===================================================================
    // GetDistToCustomNav
    //===================================================================
    private float GetDistToCustomNav(List<SPathControlPoint> controlPoints, uint curLASegmentIndex, Vec3 curLAPos)
    {
        int nPts = controlPoints.Count;

        float dist = float.MaxValue;

        if (curLASegmentIndex + 1 < (uint)nPts)
        {
            dist = 0.0f;

            if (controlPoints[(int)curLASegmentIndex].navType != IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION ||
                controlPoints[(int)curLASegmentIndex + 1].navType != IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION)
            {
                Vec3 lastPos = curLAPos;

                for (int i = (int)curLASegmentIndex + 1; i < nPts; ++i)
                {
                    dist += DistancePointPoint(lastPos, m_pathControlPoints[i].pos);
                    if (i + 1 < nPts)
                    {
                        if (controlPoints[i].navType == IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION &&
                            controlPoints[i + 1].navType == IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION)
                            break;
                    }
                    else
                    {
                        if (controlPoints[i].navType == IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION)
                            break;
                    }

                    lastPos = controlPoints[i].pos;
                }
            }
        }

        return dist;
    }

    //===================================================================
    // GetPathPointAhead (private, with state)
    //===================================================================
    private Vec3 GetPathPointAhead(float dist, ref float actualDist, int curLASegmentIndex, Vec3 curLAPos)
    {
        Vec3 pt = curLAPos;
        actualDist = 0.0f;
        int nPts1 = m_pathControlPoints.Count - 1;
        int idx = curLASegmentIndex;
        while (idx++ < nPts1)
        {
            Vec3 nextPathPt = GetPathControlPoint(idx);
            float distToNext = DistancePointPoint(pt, nextPathPt);

            float newDist = actualDist + distToNext;
            if (newDist > dist && (newDist - actualDist) > 0.0f)
            {
                float frac = (dist - actualDist) / (newDist - actualDist);
                pt = frac * nextPathPt + (1.0f - frac) * pt;
                actualDist = dist;
                return pt;
            }
            actualDist = newDist;
            pt = nextPathPt;
        }
        return pt;
    }

    //===================================================================
    // GetPathPointAhead (public)
    //===================================================================
    public virtual Vec3 GetPathPointAhead(float dist, out float actualDist)
    {
        actualDist = 0;
        return GetPathPointAhead(dist, ref actualDist, (int)m_CurIndex, m_CurPos);
    }

    //===================================================================
    // Draw
    //===================================================================
    public virtual void Draw(Vec3 drawOffset = default)
    {
        bool useTerrain = false;

        if (m_pathControlPoints.Count > 0 && m_pathControlPoints[0].navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
            useTerrain = true;

        CDebugDrawContext dc = new CDebugDrawContext();

        Sphere LASphere = new Sphere(m_curLAPos, 0.2f);
        LASphere.center.z = dc.GetDebugDrawZ(LASphere.center, useTerrain);
        dc.DrawSphere(LASphere.center, LASphere.radius, new ColorB(255, 0, 0));

        int nPts = m_pathControlPoints.Count;
        for (int i = 1; i < nPts; ++i)
        {
            dc.DrawLine(GetPathControlPoint(i - 1), new ColorB(0, 0, 0), GetPathControlPoint(i), new ColorB(0, 0, 0));
            dc.DrawSphere(GetPathControlPoint(i - 1), 0.05f, new ColorB(0, 0, 0));
        }
    }

    //===================================================================
    // Serialize
    //===================================================================
    public virtual void Serialize(TSerialize ser)
    {
        ser.Value("m_params", m_params);
        ser.Value("m_curLAPos", ref m_curLAPos);
        ser.Value("m_curLASegmentIndex", ref m_curLASegmentIndex);
        ser.Value("m_lastOutputSpeed", ref m_lastOutputSpeed);
    }

    //===================================================================
    // CheckWalkability
    //===================================================================
    public virtual bool CheckWalkability(Vec2[] path, nuint length)
    {
        // CRY_ASSERT(false); not implemented in C++ original
        return false;
    }

    //===================================================================
    // GetAllowCuttingCorners
    //===================================================================
    public virtual bool GetAllowCuttingCorners()
    {
        return true;
    }

    //===================================================================
    // SetAllowCuttingCorners
    //===================================================================
    public virtual void SetAllowCuttingCorners(bool allowCuttingCorners)
    {
        // CRY_ASSERT(false); not implemented in C++ original
    }

    public virtual void Release() { /* delete this */ }

    //===================================================================
    // GetPathPointAheadStatic — free function from PathFollower.cpp
    //===================================================================
    private static Vec3 GetPathPointAheadStatic(TPathPoints pathPts, int itIdx, float dist)
    {
        if (pathPts.Count == 0) return new Vec3(0, 0, 0);

        if (dist >= 0.0f)
        {
            float curDist = 0.0f;
            Vec3 oldPos = pathPts[itIdx].vPos;
            for (int i = itIdx + 1; i < pathPts.Count; ++i)
            {
                Vec3 pos = pathPts[i].vPos;
                float segDist = Distance.Point_Point(oldPos, pos);

                float newDist = segDist + curDist;
                if (newDist > dist && segDist > 0.0f)
                {
                    float frac = (dist - curDist) / segDist;
                    return frac * pos + (1.0f - frac) * oldPos;
                }

                curDist = newDist;
                oldPos = pos;
            }
            return pathPts[pathPts.Count - 1].vPos;
        }
        else
        {
            TPathPoints pathPtsRev = new TPathPoints(pathPts);
            pathPtsRev.Reverse();
            int n = itIdx;
            int itRev = pathPtsRev.Count - (n + 1);
            return GetPathPointAheadStatic(pathPtsRev, itRev, -dist);
        }
    }
}

// Forward decl shells
public class PathFollowResult
{
    public Vec3 velocityOut;
    public bool reachedEnd;
    public Vec3 followTargetPos;
    public Vec3 inflectionPoint;
    public List<SPredictedState> predictedStates;
    public float desiredPredictionTime;
    public float predictionDeltaTime;

    public struct SPredictedState
    {
        public Vec3 pos;
        public Vec3 vel;
        public SPredictedState(Vec3 p, Vec3 v) { pos = p; vel = v; }
    }
}
