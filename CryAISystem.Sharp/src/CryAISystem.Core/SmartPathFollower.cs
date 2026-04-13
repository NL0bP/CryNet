// Literal port of dev/Code/CryEngine/CryAISystem/SmartPathFollower.h + SmartPathFollower.cpp (1540L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.AILog;
using CryAISystem.CryCommon;

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

    //===================================================================
    // FindNextSegmentIndex
    //===================================================================
    public float FindNextSegmentIndex(nuint startIndex)
    {
        float addition = 0.0f;
        if (m_points.Count > 0)
        {
            int idx = (int)startIndex;
            if (idx >= 0 && idx < m_points.Count)
            {
                if (idx + 1 < m_points.Count)
                    addition = 1.0f;
            }
        }
        return (float)startIndex + addition;
    }

    //===================================================================
    // FindSegmentIndexAtDistance
    //===================================================================
    public float FindSegmentIndexAtDistance(float requestedDistance, nuint startIndex = 0)
    {
        float result = (float)m_points.Count - 1.0f;

        requestedDistance = clamp_tpl(requestedDistance, 0.0f, m_totalDistance);

        if (m_points.Count > 0)
        {
            int idx = (int)startIndex;
            float prevDistance = m_points[idx].distance;

            for (int i = idx; i < m_points.Count; ++i)
            {
                SPathControlPoint2 point = m_points[i];
                float pointDistance = point.distance;

                if (pointDistance >= requestedDistance)
                {
                    float end = (float)i;
                    float start = (i > 0) ? end - 1.0f : 0.0f;

                    float distanceWithinSegment = requestedDistance - prevDistance;
                    float segmentDistance = pointDistance - prevDistance;

                    float delta = (segmentDistance > 0.001f) ? distanceWithinSegment / segmentDistance : 0.0f;

                    result = Lerp(start, end, delta);
                    break;
                }

                prevDistance = pointDistance;
            }
        }

        return result;
    }

    //===================================================================
    // FindClosestSegmentIndex
    //===================================================================
    public float FindClosestSegmentIndex(Vec3 testPoint, float startDistance = 0.0f, float endDistance = float.MaxValue, float toleranceZ = 0.5f)
    {
        float bestIndex = -1.0f;

        int pointCount = m_points.Count;
        if (pointCount != 0)
        {
            float MIN_DISTANCE_TO_SEGMENT = 0.01f;
            float bestDistSq = float.MaxValue;

            float startIndex = FindSegmentIndexAtDistance(startDistance);
            nuint startIndexInt = (nuint)startIndex;
            float endIndex = FindSegmentIndexAtDistance(endDistance, startIndexInt);
            nuint endIndexInt = (nuint)Math.Ceiling(endIndex);

            Vec3 startPos = GetPositionAtSegmentIndex(startIndex);
            Vec3 endPos = GetPositionAtSegmentIndex(endIndex);

            bool evaluatingSinglePoint = (startIndex == endIndex);
            int iterStart = (int)startIndexInt + (evaluatingSinglePoint ? 0 : 1);
            int iterEnd = (int)endIndexInt + 1;
            if (iterEnd > m_points.Count) iterEnd = m_points.Count;

            float segmentStartIndex = startIndex;
            Vec3 segmentStartPos = startPos;

            float testZ = testPoint.z;

            for (int i = iterStart; i < iterEnd; ++i)
            {
                SPathControlPoint2 point = m_points[i];

                int segmentEndIndexInt = i;
                float segmentEndIndex = min((float)segmentEndIndexInt, endIndex);
                Vec3 segmentEndPos = (segmentEndIndexInt != (int)endIndexInt) ? point.pos : endPos;

                Lineseg segment = new Lineseg(segmentStartPos, segmentEndPos);

                if (toleranceZ == float.MaxValue ||
                    ((testZ >= min(segment.start.z, segment.end.z) - toleranceZ) &&
                     (testZ <= max(segment.start.z, segment.end.z) + toleranceZ)))
                {
                    float segmentDelta;
                    float distToSegmentSq = Distance.Point_Lineseg2DSq(testPoint, segment, out segmentDelta);

                    if (distToSegmentSq < bestDistSq)
                    {
                        bestDistSq = distToSegmentSq;

                        float segmentLength = segmentEndIndex - segmentStartIndex;
                        bestIndex = segmentStartIndex + (segmentDelta * segmentLength);

                        if (bestDistSq <= square(MIN_DISTANCE_TO_SEGMENT))
                            break;
                    }
                }

                segmentStartPos = segmentEndPos;
                segmentStartIndex = segmentEndIndex;
            }
        }

        return bestIndex;
    }

    //===================================================================
    // IsParrallelTo
    //===================================================================
    public bool IsParrallelTo(Lineseg line, float startIndex, float endIndex, float maxDeviation)
    {
        for (float index = startIndex; index < endIndex; index = floorf(index) + 1.0f)
        {
            Vec3 testPos = GetPositionAtSegmentIndex(index);
            float delta;
            if (Distance.Point_Lineseg2DSq(testPos, line, out delta) > maxDeviation)
                return false;
        }

        Vec3 endPos = GetPositionAtSegmentIndex(endIndex);
        float delta2;
        return Distance.Point_Lineseg2DSq(endPos, line, out delta2) <= maxDeviation;
    }

    //===================================================================
    // GetPositionAtSegmentIndex
    //===================================================================
    public Vec3 GetPositionAtSegmentIndex(float index)
    {
        if (m_points.Count == 0) return new Vec3(0, 0, 0);

        index = max(index, 0.0f);

        float delta = fmodf(index, 1.0f);

        int startIndex = (int)index;
        if (startIndex >= m_points.Count)
            startIndex = m_points.Count - 1;
        int endIndex = startIndex + 1;

        Vec3 startPos = m_points[startIndex].pos;
        Vec3 endPos = (endIndex < m_points.Count) ? m_points[endIndex].pos : startPos;

        return Lerp(startPos, endPos, delta);
    }

    //===================================================================
    // GetDistanceAtSegmentIndex
    //===================================================================
    public float GetDistanceAtSegmentIndex(float index)
    {
        if (m_points.Count == 0) return 0;

        index = max(index, 0.0f);

        float delta = fmodf(index, 1.0f);

        int startIndex = (int)index;
        if (startIndex >= m_points.Count)
            startIndex = m_points.Count - 1;
        int endIndex = startIndex + 1;

        float startDist = m_points[startIndex].distance;
        float endDist = (endIndex < m_points.Count) ? m_points[endIndex].distance : startDist;

        return Lerp(startDist, endDist, delta);
    }

    //===================================================================
    // GetLineSegment
    //===================================================================
    public void GetLineSegment(float startIndex, float endIndex, out Lineseg segment)
    {
        segment = new Lineseg(GetPositionAtSegmentIndex(startIndex), GetPositionAtSegmentIndex(endIndex));
    }

    //===================================================================
    // FindNextNavTypeSectionIndexAfter
    //===================================================================
    public nuint FindNextNavTypeSectionIndexAfter(nuint index)
    {
        nuint changeIndex = (nuint)(m_points.Count - 1);

        if (index < changeIndex)
        {
            IAISystem_ENavigationType prevNavType = m_points[(int)index].navType;

            for (int i = (int)index + 1; i < m_points.Count; ++i)
            {
                if (m_points[i].navType != prevNavType)
                {
                    changeIndex = (nuint)i;
                    break;
                }
            }
        }

        return changeIndex;
    }

    //===================================================================
    // FindNextInflectionIndex
    //===================================================================
    public float FindNextInflectionIndex(float startIndex, float maxDeviation = 0.1f)
    {
        Lineseg testLine;
        testLine = new Lineseg(GetPositionAtSegmentIndex(startIndex), GetPositionAtSegmentIndex(startIndex));

        float maxDeviationSq = square(maxDeviation);

        float searchStartIndex = ceilf(startIndex);
        float searchStartDist = GetDistanceAtSegmentIndex(searchStartIndex);
        float searchEndDist = min(searchStartDist + 3.0f, m_totalDistance);

        float distIncrement = 0.1f;
        float lastSafeIndex = searchStartIndex;
        int maxSteps = 64;

        for (float dist = searchStartDist; maxSteps > 0 && dist < searchEndDist; dist += distIncrement, --maxSteps)
        {
            float index = FindSegmentIndexAtDistance(dist, (nuint)startIndex);
            testLine = new Lineseg(testLine.start, GetPositionAtSegmentIndex(index));

            int subIndexEnd = (int)ceilf(index);

            for (int subIndex = (int)searchStartIndex + 1; subIndex < subIndexEnd; ++subIndex)
            {
                float delta;
                float deviationSq = Distance.Point_Lineseg2DSq(m_points[subIndex].pos, testLine, out delta);
                if (deviationSq > maxDeviationSq)
                {
                    return lastSafeIndex;
                }
            }

            lastSafeIndex = index;
        }

        return lastSafeIndex;
    }

    //===================================================================
    // ShortenToIndex
    //===================================================================
    public void ShortenToIndex(float endIndex)
    {
        if (endIndex >= 0.0f && endIndex < m_points.Count)
        {
            int cutIndex = (int)endIndex + 1;

            float delta = fmodf(endIndex, 1.0f);

            SPathControlPoint2 newEndPoint = m_points[(int)endIndex];
            newEndPoint.pos = GetPositionAtSegmentIndex(endIndex);
            newEndPoint.distance = GetDistanceAtSegmentIndex(endIndex);

            if (cutIndex < m_points.Count)
                m_points.RemoveRange(cutIndex, m_points.Count - cutIndex);

            m_points.Add(newEndPoint);
        }
    }

    public float TotalDistance() { return m_totalDistance; }

    public void clear() { m_points.Clear(); m_totalDistance = 0.0f; }

    public void push_back(SPathControlPoint2 point)
    {
        float distance = 0.0f;
        if (m_points.Count > 0)
            distance = m_points[m_points.Count - 1].pos.GetDistance(point.pos);

        m_points.Add(point);

        float cumulativeDistance = m_totalDistance + distance;
        SPathControlPoint2 last = m_points[m_points.Count - 1];
        last.distance = cumulativeDistance;
        m_points[m_points.Count - 1] = last;

        m_totalDistance = cumulativeDistance;
    }

    public int size() { return m_points.Count; }
    public bool empty() { return m_points.Count == 0; }
    public SPathControlPoint2 front() { return m_points[0]; }
    public SPathControlPoint2 back() { return m_points[m_points.Count - 1]; }
    public SPathControlPoint2 this[int index] { get { return m_points[index]; } set { m_points[index] = value; } }

    private List<SPathControlPoint2> m_points = new List<SPathControlPoint2>();
    private float m_totalDistance;
}

public class CSmartPathFollower : IPathFollower
{
    private InterpolatedPath m_path = new InterpolatedPath();
    private PathFollowerParams m_params;
    private INavPath m_pNavPath;
    private IPathObstacles m_pathObstacles;
    private int m_pathVersion;
    private Vec3 m_curPos;
    private IAISystem_ENavigationType m_followNavType;
    private float m_lastOutputSpeed;
    private Vec3 m_validatedStartPos;
    private float m_followTargetIndex;
    private float m_inflectionIndex;
    private bool m_allowCuttingCorners;
    private AABB m_lookAheadEnclosingAABB;

    //===================================================================
    // Constructor
    //===================================================================
    public CSmartPathFollower(PathFollowerParams parameters, IPathObstacles pathObstacleObject)
    {
        m_params = parameters;
        m_pathVersion = -2;
        m_followTargetIndex = 0;
        m_inflectionIndex = 0;
        m_pNavPath = null;
        m_pathObstacles = pathObstacleObject;
        m_curPos = new Vec3(0, 0, 0);
        m_followNavType = IAISystem_ENavigationType.NAV_UNSET;
        m_lookAheadEnclosingAABB = new AABB(AABB.RESET);
        m_allowCuttingCorners = true;
        m_lastOutputSpeed = 0;
        m_validatedStartPos = new Vec3(0, 0, 0);
        Reset();
    }
    public CSmartPathFollower()
    {
        m_params = new PathFollowerParams();
        m_pathVersion = -2;
        m_followTargetIndex = 0;
        m_inflectionIndex = 0;
        m_pNavPath = null;
        m_pathObstacles = null;
        m_curPos = new Vec3(0, 0, 0);
        m_followNavType = IAISystem_ENavigationType.NAV_UNSET;
        m_lookAheadEnclosingAABB = new AABB(AABB.RESET);
        m_allowCuttingCorners = true;
        m_lastOutputSpeed = 0;
        m_validatedStartPos = new Vec3(0, 0, 0);
    }

    private float DistancePointPoint(Vec3 pt1, Vec3 pt2)
    {
        return m_params.use2D ? Distance.Point_Point2D(pt1, pt2) : Distance.Point_Point(pt1, pt2);
    }

    private float DistancePointPointSq(Vec3 pt1, Vec3 pt2)
    {
        return m_params.use2D ? Distance.Point_Point2DSq(pt1, pt2) : Distance.Point_PointSq(pt1, pt2);
    }

    //===================================================================
    // Reset
    //===================================================================
    public virtual void Reset()
    {
        m_pathVersion = -2;
        m_validatedStartPos = new Vec3(0, 0, 0);
        m_followTargetIndex = 0.0f;
        m_followNavType = IAISystem_ENavigationType.NAV_UNSET;
        m_inflectionIndex = 0.0f;
        m_path.clear();
    }

    //===================================================================
    // AttachToPath
    //===================================================================
    public virtual void AttachToPath(INavPath pNavPath)
    {
        Reset();
        m_pNavPath = pNavPath;
    }

    public virtual void SetParams(PathFollowerParams parameters) { m_params = parameters; }
    public virtual PathFollowerParams GetParams() { return m_params; }

    //===================================================================
    // ProcessPath
    //===================================================================
    private void ProcessPath()
    {
        m_pathVersion = (int)m_pNavPath.GetVersion();

        m_path.clear();
        m_followTargetIndex = 0.0f;
        m_followNavType = IAISystem_ENavigationType.NAV_UNSET;
        m_inflectionIndex = 0.0f;

        CNavPathReal pNavPath = m_pNavPath as CNavPathReal;
        if (pNavPath == null) return;
        TPathPoints pathPts = pNavPath.GetPath();

        if (pathPts.Count < 2)
            return;

        SPathControlPoint2 pathPoint = new SPathControlPoint2();
        pathPoint.distance = 0.0f;

        foreach (PathPointDescriptor ppd in pathPts)
        {
            pathPoint.navType = ppd.navType;
            pathPoint.pos = ppd.vPos;
            m_path.push_back(pathPoint);
        }

        // Ensure end point at ground level
        if (m_path.size() > 1)
        {
            SPathControlPoint2 endPoint = m_path.back();
            SPathControlPoint2 prevPoint = m_path[m_path.size() - 2];
            endPoint.pos.z = min(prevPoint.pos.z + 0.1f, endPoint.pos.z);
            m_path[m_path.size() - 1] = endPoint;
        }

        // Cut path short if needed
        if (m_params.endDistance > 0.0f)
        {
            float newDistance = m_path.TotalDistance() - m_params.endDistance;
            float endIndex = m_path.FindSegmentIndexAtDistance(newDistance);
            m_path.ShortenToIndex(endIndex);
        }
    }

    //===================================================================
    // FindReachableTarget
    //===================================================================
    private bool FindReachableTarget(float startIndex, float endIndex, ref float reachableIndex)
    {
        reachableIndex = -1.0f;

        if (startIndex < endIndex)
        {
            float dist = endIndex - startIndex;

            if (CanReachTarget(endIndex))
            {
                reachableIndex = endIndex;
                return true;
            }

            float stepSize = 4.0f;
            float nextIndex;

            do
            {
                nextIndex = reachableIndex >= 0.0f ? reachableIndex : floorf(startIndex + stepSize);
                if (dist >= stepSize)
                    CanReachTargetStep(stepSize, endIndex, nextIndex, ref reachableIndex);

                if (reachableIndex >= endIndex)
                    return true;

            } while ((stepSize *= 0.5f) >= 0.5f);

            if (reachableIndex >= endIndex)
                return true;
        }
        else
        {
            float StartingAdvanceDistance = 0.5f;
            float advance = StartingAdvanceDistance;
            float start = m_path.GetDistanceAtSegmentIndex(startIndex);
            float end = m_path.GetDistanceAtSegmentIndex(endIndex);

            do
            {
                start -= advance;
                if (start < end)
                    start = end;

                float test = m_path.FindSegmentIndexAtDistance(start);

                if (CanReachTarget(test))
                {
                    reachableIndex = test;
                    break;
                }

                advance = min(1.5f, advance * 1.5f);
            } while (start > end);
        }

        return reachableIndex >= 0.0f;
    }

    private bool CanReachTargetStep(float step, float endIndex, float nextIndex, ref float reachableIndex)
    {
        if (nextIndex > endIndex)
            nextIndex = endIndex;

        while (CanReachTarget(nextIndex))
        {
            reachableIndex = nextIndex;

            if (nextIndex >= endIndex)
                break;

            nextIndex = min(nextIndex + step, endIndex);
        }

        return reachableIndex >= 0.0f;
    }

    //===================================================================
    // CanReachTarget
    //===================================================================
    private bool CanReachTarget(float testIndex)
    {
        Vec3 startPos = m_curPos;

        if (testIndex < 0.0f)
            return false;

        Vec3 testPos = m_path.GetPositionAtSegmentIndex(testIndex);

        // Navigation mesh handling
        if (m_pNavPath != null)
        {
            NavigationMeshID meshID = m_pNavPath.GetMeshID();
            if (meshID.id != 0)
            {
                Vec3 raiseUp = new Vec3(0.0f, 0.0f, 0.2f);
                Vec3 raisedStartPos = startPos + raiseUp;
                Vec3 raisedTestPos = testPos + raiseUp;

                // MNM raycast and obstacle checks — simplified for shell types
                // The full implementation requires MNM::MeshGrid internals.
                // For now perform obstacle check only.
                if (m_pathObstacles != null && m_pathObstacles.IsPathIntersectingObstacles(meshID, raisedStartPos, raisedTestPos, m_params.passRadius))
                    return false;

                return true;
            }
        }

        return false;
    }

    //===================================================================
    // GetPredictionTimeForMovingAlongPath
    //===================================================================
    private float GetPredictionTimeForMovingAlongPath(bool isInsideObstacles, float currentSpeedSq)
    {
        float predictionTime = gAIEnv.CVars.SmartPathFollower_LookAheadPredictionTimeForMovingAlongPathWalk;
        if (isInsideObstacles)
        {
            predictionTime = 0.2f;
        }
        else
        {
            float minSpeedToBeConsideredRunningOrSprintingSq = sqr(2.0f);
            if (currentSpeedSq > minSpeedToBeConsideredRunningOrSprintingSq)
            {
                predictionTime = gAIEnv.CVars.SmartPathFollower_LookAheadPredictionTimeForMovingAlongPathRunAndSprint;
            }
        }
        return predictionTime;
    }

    //===================================================================
    // Update
    //===================================================================
    public virtual bool Update(PathFollowResult result, Vec3 curPos, Vec3 curVel, float dt)
    {
        bool targetReachable = true;

        bool bPathHasChanged = (m_pathVersion != m_pNavPath.GetVersion());
        if (bPathHasChanged)
        {
            ProcessPath();
        }

        // Set result defaults
        result.reachedEnd = false;
        if (result.predictedStates != null)
            result.predictedStates.Clear();
        result.followTargetPos = curPos;
        result.inflectionPoint = curPos;
        result.velocityOut = new Vec3(0, 0, 0);

        if (m_path.empty() || m_path.TotalDistance() < float.Epsilon)
        {
            result.reachedEnd = true;
            return true;
        }

        m_curPos = curPos;

        if (m_followNavType == IAISystem_ENavigationType.NAV_UNSET)
        {
            int segmentIndex = (int)m_followTargetIndex;
            m_followNavType = (segmentIndex < m_path.size()) ? m_path[segmentIndex].navType : IAISystem_ENavigationType.NAV_UNSET;
        }

        Vec3 followTargetPos = m_path.GetPositionAtSegmentIndex(m_followTargetIndex);
        Vec3 inflectionPoint = m_path.GetPositionAtSegmentIndex(m_inflectionIndex);

        bool recalculateTarget = false;
        bool onSafeLine = false;

        float recalculationFraction = 0.25f;

        if (m_followTargetIndex > 0.0f)
        {
            Lineseg safeLine = new Lineseg(m_validatedStartPos, followTargetPos);
            float delta;
            float distToSafeLineSq = Distance.Point_Lineseg2DSq(curPos, safeLine, out delta);
            onSafeLine = distToSafeLineSq < sqr(0.15f);

            if (onSafeLine)
            {
                if (m_allowCuttingCorners && (delta > recalculationFraction))
                {
                    if (m_followTargetIndex < m_path.size() - 1.001f)
                        recalculateTarget = true;
                }
            }
            else
            {
                recalculateTarget = true;
            }
        }
        else
        {
            recalculateTarget = true;
        }

        bool isInsideObstacles = m_pathObstacles != null && m_pathObstacles.IsPointInsideObstacles(m_curPos);
        bool isAllowedToShortcut;

        if (gAIEnv.CVars.SmartPathFollower_useAdvancedPathShortcutting == 0)
        {
            isAllowedToShortcut = isInsideObstacles ? false : m_params.isAllowedToShortcut;
        }
        else
        {
            if (isInsideObstacles || !m_params.isAllowedToShortcut)
            {
                isAllowedToShortcut = false;
            }
            else
            {
                isAllowedToShortcut = true;

                float indexAtCurrentPos;
                float indexAtLookAheadPos;

                if (m_followTargetIndex > 0.0f)
                {
                    indexAtCurrentPos = m_followTargetIndex;
                }
                else
                {
                    indexAtCurrentPos = m_path.FindClosestSegmentIndex(curPos, 0.0f, 5.0f);
                }

                float currentDistance = m_path.GetDistanceAtSegmentIndex(indexAtCurrentPos);
                indexAtLookAheadPos = m_path.FindSegmentIndexAtDistance(currentDistance + gAIEnv.CVars.SmartPathFollower_LookAheadDistance);

                for (float index = indexAtCurrentPos; index <= indexAtLookAheadPos; index += 1.0f)
                {
                    float indexAtStartOfSegment = floorf(index);
                    float indexAtEndOfSegment = indexAtStartOfSegment + 1.0f;
                    indexAtEndOfSegment = min(indexAtEndOfSegment, (float)m_path.size() - 1.0f);

                    Lineseg lineseg;
                    m_path.GetLineSegment(indexAtStartOfSegment, indexAtEndOfSegment, out lineseg);

                    float maxDistanceToObstaclesToConsiderTooClose = 0.5f;
                    if (m_pathObstacles != null && m_pathObstacles.IsLineSegmentIntersectingObstaclesOrCloseToThem(lineseg, maxDistanceToObstaclesToConsiderTooClose))
                    {
                        isAllowedToShortcut = false;
                        break;
                    }
                }
            }
        }

        if (recalculateTarget && isAllowedToShortcut)
        {
            float currentIndex;
            float lookAheadIndex;

            float LookAheadDistance = gAIEnv.CVars.SmartPathFollower_LookAheadDistance;

            if (m_followTargetIndex > 0.0f)
            {
                currentIndex = m_followTargetIndex;
                float currentDistance = m_path.GetDistanceAtSegmentIndex(m_followTargetIndex);
                lookAheadIndex = m_path.FindSegmentIndexAtDistance(currentDistance + LookAheadDistance);
            }
            else
            {
                currentIndex = m_path.FindClosestSegmentIndex(curPos, 0.0f, 5.0f);
                float currentDistance = m_path.GetDistanceAtSegmentIndex(currentIndex);
                lookAheadIndex = m_path.FindSegmentIndexAtDistance(currentDistance + LookAheadDistance);
            }

            float newTargetIndex = -1.0f;

            m_lookAheadEnclosingAABB.Reset();
            m_lookAheadEnclosingAABB.Add(m_curPos);
            m_lookAheadEnclosingAABB.Add(m_path.GetPositionAtSegmentIndex(lookAheadIndex));

            for (float current = currentIndex, end2 = lookAheadIndex; current <= end2; current += 1.0f)
                m_lookAheadEnclosingAABB.Add(m_path.GetPositionAtSegmentIndex(current));
            m_lookAheadEnclosingAABB.Expand(new Vec3(m_params.passRadius + 0.005f, m_params.passRadius + 0.005f, 0.5f));

            if (!FindReachableTarget(currentIndex, lookAheadIndex, ref newTargetIndex))
            {
                if (onSafeLine || CanReachTarget(m_followTargetIndex))
                {
                    newTargetIndex = m_followTargetIndex;
                }
                else
                {
                    float lookBehindIndex = 0.0f;
                    if (!FindReachableTarget(m_followTargetIndex, lookBehindIndex, ref newTargetIndex))
                    {
                        targetReachable = false;
                    }
                }
            }

            if (newTargetIndex >= 0.0f)
            {
                m_validatedStartPos = curPos;

                if (m_followTargetIndex != newTargetIndex)
                {
                    m_followTargetIndex = newTargetIndex;
                    followTargetPos = m_path.GetPositionAtSegmentIndex(newTargetIndex);

                    m_inflectionIndex = m_path.FindNextInflectionIndex(newTargetIndex, m_params.pathRadius * 0.5f);
                    inflectionPoint = m_path.GetPositionAtSegmentIndex(m_inflectionIndex);

                    int segmentIndex = (int)m_followTargetIndex;
                    m_followNavType = m_path[segmentIndex].navType;
                }
            }
        }

        if (recalculateTarget && !isAllowedToShortcut)
        {
            float predictionTime = GetPredictionTimeForMovingAlongPath(isInsideObstacles, curVel.GetLengthSquared2D());
            float kIncreasedZTolerance = float.MaxValue;

            float currentIndex = m_path.FindClosestSegmentIndex(m_curPos, 0.0f, m_path.TotalDistance(), kIncreasedZTolerance);
            if (currentIndex < 0.0f)
            {
                return false;
            }

            if (m_followTargetIndex == 0.0f)
            {
                m_followTargetIndex = currentIndex;
                float kMinDistanceSqToFirstClosestPointInPath = sqr(0.7f);
                Vec3 pathStartPos = m_path.GetPositionAtSegmentIndex(currentIndex);
                float distanceSqToClosestPointOfPath = DistancePointPointSq(pathStartPos, m_curPos);
                if (distanceSqToClosestPointOfPath < kMinDistanceSqToFirstClosestPointInPath)
                {
                    m_followTargetIndex = m_path.FindNextSegmentIndex((nuint)m_followTargetIndex);
                }

                followTargetPos = m_path.GetPositionAtSegmentIndex(m_followTargetIndex);

                m_inflectionIndex = m_path.FindNextSegmentIndex((nuint)m_followTargetIndex);
                inflectionPoint = m_path.GetPositionAtSegmentIndex(m_inflectionIndex);
            }
            else
            {
                float currentDistance = m_path.GetDistanceAtSegmentIndex(currentIndex);
                Vec3 localNextPos = curVel * predictionTime;
                float kMinLookAheadDistanceAllowed = 1.0f;
                float lookAheadDistance = max(kMinLookAheadDistanceAllowed, localNextPos.Length());
                float lookAheadIndex = m_path.FindSegmentIndexAtDistance(currentDistance + lookAheadDistance);
                float nextFollowIndex = m_path.FindNextSegmentIndex((nuint)lookAheadIndex);

                if (m_followTargetIndex < nextFollowIndex)
                {
                    m_followTargetIndex = nextFollowIndex;
                    followTargetPos = m_path.GetPositionAtSegmentIndex(lookAheadIndex);

                    m_inflectionIndex = m_path.FindNextSegmentIndex((nuint)nextFollowIndex);
                    inflectionPoint = m_path.GetPositionAtSegmentIndex(m_inflectionIndex);
                }
            }
            m_validatedStartPos = m_curPos;
        }

        // Generate results
        {
            result.followTargetPos = followTargetPos;
            result.inflectionPoint = inflectionPoint;

            {
                Vec3 velocity = followTargetPos - curPos;
                if (m_params.use2D)
                    velocity.z = 0.0f;
                velocity = velocity.GetNormalizedSafe();
                float distToEnd = GetDistToEnd(curPos);

                float speed = m_params.normalSpeed;

                float MinDistanceToEnd = m_params.endAccuracy;

                if (m_params.stopAtEnd)
                {
                    if (distToEnd < MinDistanceToEnd)
                    {
                        result.reachedEnd = true;
                        speed = 0.0f;
                    }
                    else
                    {
                        float slowDownDist = 1.2f;
                        float decelerationMultiplier = m_params.isVehicle ?
                            gAIEnv.CVars.SmartPathFollower_decelerationVehicle :
                            gAIEnv.CVars.SmartPathFollower_decelerationHuman;
                        speed = min(speed, decelerationMultiplier * distToEnd / slowDownDist);
                    }
                }
                else
                {
                    float MaxTimeStep = 0.5f;
                    result.reachedEnd = distToEnd < max(MinDistanceToEnd, speed * min(dt, MaxTimeStep));
                }

                if (bPathHasChanged)
                {
                    Vec3 velDir = curVel;
                    Vec3 moveDir = followTargetPos - curPos;
                    if (m_params.use2D)
                    {
                        velDir.z = 0.0f;
                        moveDir.z = 0.0f;
                    }

                    float curSpeed = velDir.NormalizeSafe();
                    moveDir = moveDir.GetNormalizedSafe();

                    float dot = velDir.Dot(moveDir);
                    Limit(ref dot, 0.0f, 1.0f);

                    m_lastOutputSpeed = curSpeed * dot;
                }
                Limit(ref m_lastOutputSpeed, m_params.minSpeed, m_params.maxSpeed);
                float maxOutputSpeed = min(m_lastOutputSpeed + dt * m_params.maxAccel, m_params.maxSpeed);
                float minOutputSpeed = m_params.stopAtEnd ? 0.0f : max(m_lastOutputSpeed - dt * m_params.maxDecel, m_params.minSpeed);

                Limit(ref speed, minOutputSpeed, maxOutputSpeed);
                m_lastOutputSpeed = speed;

                velocity = velocity * speed;

                result.velocityOut = velocity;
            }
        }

        return targetReachable;
    }

    //===================================================================
    // Advance
    //===================================================================
    public virtual void Advance(float distance) { /* TODO: Remove, but needed by old implementation for now */ }

    //===================================================================
    // GetDistToEnd
    //===================================================================
    public virtual float GetDistToEnd(Vec3? pCurPos)
    {
        return GetDistToEnd(pCurPos.HasValue ? pCurPos.Value : m_curPos);
    }

    private float GetDistToEnd(Vec3 curPos)
    {
        float distanceToEnd = 0.0f;

        if (!m_path.empty())
        {
            distanceToEnd = m_path.TotalDistance() - m_path.GetDistanceAtSegmentIndex(m_followTargetIndex);
            Vec3 followTargetPos = m_path.GetPositionAtSegmentIndex(m_followTargetIndex);
            distanceToEnd += DistancePointPoint(curPos, followTargetPos);
        }

        return distanceToEnd;
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
        if (!m_path.empty())
        {
            int nPts = m_path.size();

            if (m_followTargetIndex + 1 >= nPts)
            {
                if (navType == m_path.back().navType)
                    return Distance.Point_Point(m_path.back().pos, m_curPos);

                return float.MaxValue;
            }

            float closestIndex = m_path.FindClosestSegmentIndex(m_curPos, 0.0f,
                m_path.GetDistanceAtSegmentIndex(m_followTargetIndex));
            if (closestIndex < 0.0f)
                return float.MaxValue;

            int closestIndexInt = (int)closestIndex;

            float curDist = DistancePointPoint(m_curPos, m_path[closestIndexInt].pos);
            float totalDist = 0.0f;
            if (closestIndexInt + 1 < nPts)
            {
                totalDist = DistancePointPoint(m_path[closestIndexInt].pos, m_path[closestIndexInt + 1].pos);

                float curFraction = (totalDist > 0.0f) ? (curDist / totalDist) : 0.0f;

                if ((m_path[closestIndexInt].navType == navType) &&
                    (m_path[closestIndexInt + 1].navType == navType) &&
                    (m_path[closestIndexInt].customId == m_path[closestIndexInt + 1].customId))
                {
                    return (curFraction < 0.5f) ? 0.0f : float.MaxValue;
                }
            }
            else if ((closestIndexInt + 1 == nPts) && (m_path[closestIndexInt].navType == navType))
            {
                return DistancePointPoint(m_curPos, m_path[closestIndexInt].pos);
            }
            else
                return float.MaxValue;

            float dist = 0.0f;
            Vec3 lastPos = m_curPos;
            for (int i = closestIndexInt + 1; i < nPts; ++i)
            {
                dist += DistancePointPoint(lastPos, m_path[i].pos);
                if (i + 1 < nPts)
                {
                    if ((m_path[i].navType == navType) &&
                        (m_path[i + 1].navType == navType) &&
                        (m_path[i].customId == m_path[i + 1].customId))
                        return dist;
                }
                else
                {
                    if (m_path[i].navType == navType)
                        return dist;
                }
                lastPos = m_path[i].pos;
            }
        }

        return float.MaxValue;
    }

    //===================================================================
    // GetPathPointAhead
    //===================================================================
    public virtual Vec3 GetPathPointAhead(float requestedDist, out float actualDist)
    {
        Vec3 followTargetPos = m_path.GetPositionAtSegmentIndex(m_followTargetIndex);

        float posDist = m_curPos.GetDistance(followTargetPos);
        if (requestedDist <= posDist)
        {
            actualDist = requestedDist;
            return Lerp(m_curPos, followTargetPos, (posDist > 0.0f) ? (requestedDist / posDist) : 1.0f);
        }

        requestedDist -= posDist;

        float ftDist = m_path.GetDistanceAtSegmentIndex(m_followTargetIndex);
        float endDist = ftDist + requestedDist;
        if (endDist > m_path.TotalDistance())
            endDist = m_path.TotalDistance();

        actualDist = (endDist - ftDist) + posDist;

        float endIndex = m_path.FindSegmentIndexAtDistance(endDist);

        return m_path.GetPositionAtSegmentIndex(endIndex);
    }

    //===================================================================
    // Draw
    //===================================================================
    public virtual void Draw(Vec3 drawOffset = default)
    {
        CDebugDrawContext dc = new CDebugDrawContext();
        int pathSize = m_path.size();
        for (int i = 0; i < pathSize; ++i)
        {
            Vec3 prevControlPoint = m_path.GetPositionAtSegmentIndex((float)i - 1.0f);
            Vec3 thisControlPoint = m_path.GetPositionAtSegmentIndex((float)i);

            dc.DrawLine(prevControlPoint, new ColorB(0, 0, 0), thisControlPoint, new ColorB(0, 0, 0));
            dc.DrawSphere(thisControlPoint, 0.05f, new ColorB(0, 0, 0));
        }
    }

    //===================================================================
    // Serialize
    //===================================================================
    public virtual void Serialize(TSerialize ser)
    {
        ser.Value("m_params", m_params);

        ser.Value("m_followTargetIndex", ref m_followTargetIndex);
        ser.Value("m_inflectionIndex", ref m_inflectionIndex);
        ser.Value("m_validatedStartPos", ref m_validatedStartPos);
        ser.Value("m_allowCuttingCorners", ref m_allowCuttingCorners);

        if (ser.IsReading())
        {
            m_pathVersion = -2;
        }
    }

    //===================================================================
    // CheckWalkability
    //===================================================================
    public virtual bool CheckWalkability(Vec2[] path, nuint length)
    {
        if (!m_path.empty())
        {
            if (m_pNavPath != null)
            {
                NavigationMeshID meshID = m_pNavPath.GetMeshID();
                if (meshID.id != 0)
                {
                    // MNM walkability check — simplified for shell types.
                    // Full implementation requires MNM::MeshGrid internals.
                    return true;
                }
            }
            return true;
        }
        return false;
    }

    //===================================================================
    // GetAllowCuttingCorners
    //===================================================================
    public virtual bool GetAllowCuttingCorners()
    {
        return m_allowCuttingCorners;
    }

    //===================================================================
    // SetAllowCuttingCorners
    //===================================================================
    public virtual void SetAllowCuttingCorners(bool allowCuttingCorners)
    {
        m_allowCuttingCorners = allowCuttingCorners;
    }

    public virtual void Release() { /* delete this */ }

    private static float sqr(float x) { return x * x; }
}
