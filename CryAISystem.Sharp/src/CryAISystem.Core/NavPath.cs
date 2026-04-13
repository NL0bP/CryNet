// Literal port of dev/Code/CryEngine/CryAISystem/NavPath.h + NavPath.cpp (2065L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Linq;
using static CryAISystem.CryMath;
using static CryAISystem.AILog;
using CryAISystem.CryCommon;
using OffMeshLink_SmartObject = CryAISystem.Navigation.MNM.OffMeshLink_SmartObject;

namespace CryAISystem;

// CNavPath was forward-declared in PipeUser.cs as `public class CNavPath : INavPath { }`
// Replace with full literal port. Use new file with same name to override.
public class CNavPathReal : INavPath
{
    public CNavPathReal()
    {
        m_endDir = new Vec3(0, 0, 0);
        m_currentFrac = 0.0f;
        m_stuckTime = 0.0f;
        m_pathEndIsAsRequested = true;
        m_fDiscardedPathLength = 0;
    }
    // virtual ~CNavPath();

    public NavigationMeshID GetMeshID() { return m_params.meshID; }

    public int GetVersion() { return m_version.v; }

    public void SetVersion(int v) { m_version.v = v; }

    public void SetParams(SNavPathParams parameters) { m_params = parameters; }
    public SNavPathParams GetParams() { return m_params; }

    //====================================================================
    // Draw
    //====================================================================
    public void Draw(Vec3 drawOffset = default)
    {
        bool useTerrain = false;

        CDebugDrawContext dc = new CDebugDrawContext();

        if (m_pathPoints.Count > 0)
        {
            int li = 0;
            int linext = 1;
            while (linext < m_pathPoints.Count)
            {
                Vec3 p0 = m_pathPoints[li].vPos;
                Vec3 p1 = m_pathPoints[linext].vPos;
                if (m_pathPoints[li].navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
                    useTerrain = true;

                p0.z = dc.GetDebugDrawZ(p0, m_pathPoints[li].navType == IAISystem_ENavigationType.NAV_TRIANGULAR);
                p1.z = dc.GetDebugDrawZ(p1, m_pathPoints[linext].navType == IAISystem_ENavigationType.NAV_TRIANGULAR);
                dc.DrawLine(p0, Col.Col_SteelBlue, p1, Col.Col_SteelBlue);

                ColorB color = Col.Col_Grey;

                switch (m_pathPoints[li].navType)
                {
                case IAISystem_ENavigationType.NAV_SMARTOBJECT:
                    color = Col.Col_SlateBlue;
                    break;
                case IAISystem_ENavigationType.NAV_TRIANGULAR:
                    color = Col.Col_Brown;
                    break;
                case IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN:
                    color = Col.Col_Cyan;
                    break;
                default:
                    color = Col.Col_Grey;
                    break;
                }

                li = linext;
                ++linext;
            }
        }

        if (m_remainingPathPoints.Count > 0)
        {
            int li = 0;
            int linext = 1;
            while (linext < m_remainingPathPoints.Count)
            {
                Vec3 p0 = m_remainingPathPoints[li].vPos;
                Vec3 p1 = m_remainingPathPoints[linext].vPos;
                if (m_remainingPathPoints[li].navType == IAISystem_ENavigationType.NAV_TRIANGULAR)
                    useTerrain = true;

                p0.z = dc.GetDebugDrawZ(p0, m_remainingPathPoints[li].navType == IAISystem_ENavigationType.NAV_TRIANGULAR);
                p1.z = dc.GetDebugDrawZ(p1, m_remainingPathPoints[linext].navType == IAISystem_ENavigationType.NAV_TRIANGULAR);
                dc.DrawLine(p0, Col.Col_DarkOrchid, p1, Col.Col_DarkOrchid);

                ColorB color = Col.Col_Grey;

                switch (m_remainingPathPoints[li].navType)
                {
                case IAISystem_ENavigationType.NAV_SMARTOBJECT:
                    color = Col.Col_SlateBlue;
                    break;
                case IAISystem_ENavigationType.NAV_TRIANGULAR:
                    color = Col.Col_Brown;
                    break;
                case IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN:
                    color = Col.Col_Cyan;
                    break;
                default:
                    color = Col.Col_Grey;
                    break;
                }
                dc.DrawSphere(p1, 0.125f, color);

                li = linext;
                ++linext;
            }
        }

        foreach (SDebugLine line in m_debugLines)
        {
            SDebugLine l = line;
            l.P0.z = dc.GetDebugDrawZ(l.P0, useTerrain);
            l.P1.z = dc.GetDebugDrawZ(l.P1, useTerrain);
            dc.DrawLine(l.P0, l.col, l.P1, l.col);
        }
        foreach (SDebugSphere sphere in m_debugSpheres)
        {
            SDebugSphere s = sphere;
            s.pos.z = dc.GetDebugDrawZ(s.pos, useTerrain);
            dc.DrawSphere(s.pos, s.r, s.col);
        }
    }

    //====================================================================
    // Dump
    //====================================================================
    public void Dump(string name)
    {
        AILogAlways("Path for {0}", name);
        int i = 0;
        foreach (PathPointDescriptor ppd in m_pathPoints)
        {
            AILogAlways("pt {0,4}: {1:F2} {2:F2} {3:F2}", i, ppd.vPos.x, ppd.vPos.y, ppd.vPos.z);
            ++i;
        }
    }

    //====================================================================
    // GetPathLength
    //====================================================================
    public float GetPathLength(bool b2D)
    {
        Vec3 vPos;
        if (!GetPosAlongPath(out vPos))
            return 0.0f;

        float length = 0.0f;
        if (m_pathPoints.Count > 1)
        {
            for (int li = 1; li < m_pathPoints.Count; ++li)
            {
                Vec3 vDelta = m_pathPoints[li].vPos - vPos;
                length += b2D ? vDelta.GetLength2D() : vDelta.Length();
                vPos = m_pathPoints[li].vPos;
            }
        }
        return length;
    }

    //====================================================================
    // PushFront
    //====================================================================
    public void PushFront(PathPointDescriptor newPathPoint, bool force = false)
    {
        ++m_version.v;
        if (!force && m_pathPoints.Count > 0)
        {
            IAISystem_ENavigationType newType = newPathPoint.navType;
            IAISystem_ENavigationType curType = m_pathPoints[0].navType;

            bool twoD = ((curType & (IAISystem_ENavigationType.NAV_TRIANGULAR | IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN)) != 0) ||
                ((newType & (IAISystem_ENavigationType.NAV_TRIANGULAR | IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN)) != 0);

            // Don't allow null length segments!
            if (twoD ? IsEquivalent2D(newPathPoint.vPos, m_pathPoints[0].vPos) : IsEquivalent_b(newPathPoint.vPos, m_pathPoints[0].vPos, VEC_EPSILON))
            {
                if (newType != IAISystem_ENavigationType.NAV_SMARTOBJECT && curType != IAISystem_ENavigationType.NAV_SMARTOBJECT &&
                    newType != IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION && curType != IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION)
                    return;
            }
        }
        m_pathPoints.Insert(0, newPathPoint);
    }

    //====================================================================
    // PushBack
    //====================================================================
    public void PushBack(PathPointDescriptor newPathPoint, bool force = false)
    {
        ++m_version.v;
        // Don't allow null length segments!
        if (!force && m_pathPoints.Count > 0)
        {
            IAISystem_ENavigationType newType = newPathPoint.navType;
            IAISystem_ENavigationType curType = m_pathPoints[m_pathPoints.Count - 1].navType;
            bool twoD = ((curType & (IAISystem_ENavigationType.NAV_TRIANGULAR | IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN)) != 0) ||
                ((newType & (IAISystem_ENavigationType.NAV_TRIANGULAR | IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN)) != 0);

            if (twoD ? IsEquivalent2D(newPathPoint.vPos, m_pathPoints[m_pathPoints.Count - 1].vPos) : IsEquivalent_b(newPathPoint.vPos, m_pathPoints[m_pathPoints.Count - 1].vPos, VEC_EPSILON))
            {
                if (newType != IAISystem_ENavigationType.NAV_SMARTOBJECT && curType != IAISystem_ENavigationType.NAV_SMARTOBJECT &&
                    newType != IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION && curType != IAISystem_ENavigationType.NAV_CUSTOM_NAVIGATION)
                    return;
            }
        }
        m_pathPoints.Add(newPathPoint);
    }

    //====================================================================
    // Clear
    //====================================================================
    public void Clear(string dbgString)
    {
        ++m_version.v;
        if (gAIEnv.CVars.DebugPathFinding != 0 && m_pathPoints.Count > 0)
        {
            PathPointDescriptor ppd = m_pathPoints[m_pathPoints.Count - 1];
            AILogAlways("CNavPath::Clear old path end is ({0:F2}, {1:F2}, {2:F2}) {3}",
                ppd.vPos.x, ppd.vPos.y, ppd.vPos.z, dbgString);
        }
        m_params.Clear();
        m_pathPoints.Clear();
        m_debugLines.Clear();
        m_debugSpheres.Clear();
        m_currentFrac = 0.0f;
        m_stuckTime = 0.0f;
        m_pathEndIsAsRequested = true;
        m_fDiscardedPathLength = 0;
    }

    //====================================================================
    // Advance
    //====================================================================
    public bool Advance(out PathPointDescriptor nextPathPoint)
    {
        nextPathPoint = null;
        if (m_pathPoints.Count <= 1)
            return false;
        ++m_version.v;
        m_pathPoints.RemoveAt(0);
        if (m_pathPoints.Count == 1)
        {
            m_currentFrac = 0.0f;
            return false;
        }
        nextPathPoint = GetNextPathPoint();
        return true;
    }

    public bool GetPathEndIsAsRequested() { return m_pathEndIsAsRequested; }
    public void SetPathEndIsAsRequested(bool val) { m_pathEndIsAsRequested = val; }

    //====================================================================
    // Empty
    //====================================================================
    public bool Empty() { return m_pathPoints.Count < 2; }

    public PathPointDescriptor GetLastPathPoint() { return m_pathPoints.Count == 0 ? null : m_pathPoints[m_pathPoints.Count - 1]; }
    public PathPointDescriptor GetPrevPathPoint() { return m_pathPoints.Count == 0 ? null : m_pathPoints[0]; }
    public PathPointDescriptor GetNextPathPoint() { return m_pathPoints.Count > 1 ? m_pathPoints[1] : null; }
    public PathPointDescriptor GetNextNextPathPoint() { return m_pathPoints.Count > 2 ? m_pathPoints[2] : null; }

    public Vec3 GetNextPathPos(Vec3 defaultPos = default) { PathPointDescriptor ppd = GetNextPathPoint(); return ppd != null ? ppd.vPos : defaultPos; }
    public Vec3 GetLastPathPos(Vec3 defaultPos = default) { PathPointDescriptor ppd = GetLastPathPoint(); return ppd != null ? ppd.vPos : defaultPos; }

    //====================================================================
    // GetPosAlongPath
    //====================================================================
    public bool GetPosAlongPath(out Vec3 posOut, float dist = 0.0f, bool b2D = false, bool bExtrapolateBeyondEnd = false, IAISystem_ENavigationType[] pNextPointType = null)
    {
        posOut = new Vec3(0, 0, 0);
        if (m_pathPoints.Count == 0)
            return false;

        if (m_pathPoints.Count == 1)
        {
            PathPointDescriptor pointDesc = m_pathPoints[0];
            posOut = pointDesc.vPos;
            if (pNextPointType != null && pNextPointType.Length > 0)
                pNextPointType[0] = pointDesc.navType;
            return true;
        }

        // We are at prevPt, a point of m_currentFrac completion of segment ["previous point", thisPt]
        int itIdx = 1;
        Vec3 thisPt = m_pathPoints[itIdx].vPos;
        Vec3 prevPt = m_currentFrac * thisPt + (1.0f - m_currentFrac) * GetPrevPathPoint().vPos;

        // Some clients want to know only the current position on path
        if (dist == 0.0f)
        {
            posOut = prevPt;
            if (pNextPointType != null && pNextPointType.Length > 0)
                pNextPointType[0] = m_pathPoints[itIdx].navType;
            return true;
        }

        // No initialization; the loop below will run at least once (we have >= 2 path points here).
        Vec3 vDelta = new Vec3(0, 0, 0);
        float fDeltaLen = 0.0f;

        float fDistRemaining = dist;

        for (int it = 1; it < m_pathPoints.Count; ++it)
        {
            thisPt = m_pathPoints[it].vPos;
            vDelta = thisPt - prevPt;
            fDeltaLen = b2D ? vDelta.GetLength2D() : vDelta.Length();
            if (fDistRemaining <= fDeltaLen)
            {
                float frac = fDeltaLen > 0.0f ? fDistRemaining / fDeltaLen : 0.0f;
                posOut = frac * thisPt + (1.0f - frac) * prevPt;
                if (pNextPointType != null && pNextPointType.Length > 0)
                    pNextPointType[0] = m_pathPoints[it].navType;
                return true;
            }
            fDistRemaining -= fDeltaLen;
            prevPt = thisPt;
        }

        posOut = thisPt;

        if (bExtrapolateBeyondEnd)
        {
            float frac = fDeltaLen > 0.0f ? fDistRemaining / fDeltaLen : 0.0f;
            posOut = posOut + frac * vDelta;
        }

        if (pNextPointType != null && pNextPointType.Length > 0)
            pNextPointType[0] = m_pathPoints[m_pathPoints.Count - 1].navType;

        return true;
    }

    //====================================================================
    // GetDistToPath
    //====================================================================
    public float GetDistToPath(out Vec3 pathPosOut, out float distAlongPathOut, Vec3 pos, float dist, bool twoD)
    {
        pathPosOut = pos;
        distAlongPathOut = 0;

        float distOut = float.MaxValue;
        float traversedDist = 0.0f;

        for (int pathIt = 0; pathIt < m_pathPoints.Count; ++pathIt)
        {
            int pathNextIt = pathIt + 1;
            if (pathNextIt >= m_pathPoints.Count)
                break;

            Lineseg seg = new Lineseg(m_pathPoints[pathIt].vPos, m_pathPoints[pathNextIt].vPos);

            float segLen = twoD ? Distance.Point_Point2D(seg.start, seg.end) : Distance.Point_Point(seg.start, seg.end);
            if (segLen < 0.001f)
                continue;
            if (segLen + traversedDist > dist)
            {
                float frac = (dist - traversedDist) / (segLen + traversedDist);
                seg = new Lineseg(seg.start, frac * seg.end + (1.0f - frac) * seg.start);
            }

            float t;
            float thisDist = twoD ? Distance.Point_Lineseg2D(pos, seg, out t) : Distance.Point_Lineseg(pos, seg, out t);
            if (thisDist < distOut)
            {
                distOut = thisDist;
                pathPosOut = seg.GetPoint(t);
                distAlongPathOut = traversedDist + t * segLen;
            }

            traversedDist += segLen;
            if (traversedDist > dist)
                break;
        }
        return distOut < float.MaxValue ? distOut : -1.0f;
    }

    //====================================================================
    // GetDirectionToPathFromPoint
    //====================================================================
    public void GetDirectionToPathFromPoint(Vec3 point, out Vec3 dirOut)
    {
        if (m_pathPoints.Count == 0)
        {
            dirOut = new Vec3(0, 0, 0);
        }
        else if (m_pathPoints.Count == 1)
        {
            dirOut = m_pathPoints[0].vPos - point;
        }
        else
        {
            Vec3 currentPositionOnPath = GetPrevPathPoint().vPos * (1.0f - m_currentFrac) + GetNextPathPoint().vPos * m_currentFrac;
            dirOut = currentPositionOnPath - point;
        }
    }

    //====================================================================
    // GetDistToSmartObject
    //====================================================================
    public float GetDistToSmartObject(bool b2D)
    {
        if (m_pathPoints.Count < 2)
            return float.MaxValue;

        int pathIt = 0;
        IAISystem_ENavigationType typeCur = m_pathPoints[pathIt].navType;
        ++pathIt;
        IAISystem_ENavigationType typeNext = m_pathPoints[pathIt].navType;

        if (typeCur == IAISystem_ENavigationType.NAV_SMARTOBJECT && typeNext == IAISystem_ENavigationType.NAV_SMARTOBJECT)
            return 0.0f;

        Vec3 curPos;
        GetPosAlongPath(out curPos);

        float dist = 0.0f;
        for (; pathIt < m_pathPoints.Count; ++pathIt)
        {
            Vec3 thisPos = m_pathPoints[pathIt].vPos;
            dist += b2D ? Distance.Point_Point2D(curPos, m_pathPoints[pathIt].vPos) : Distance.Point_Point(curPos, m_pathPoints[pathIt].vPos);

            int pathItNext = pathIt + 1;
            if (pathItNext < m_pathPoints.Count)
            {
                if (m_pathPoints[pathIt].navType == IAISystem_ENavigationType.NAV_SMARTOBJECT && m_pathPoints[pathItNext].navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
                    return dist;
            }
            else
            {
                // End of the path
                if (m_pathPoints[pathIt].navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
                    return dist;
            }

            curPos = m_pathPoints[pathIt].vPos;
        }
        return float.MaxValue;
    }

    //====================================================================
    // GetLastPathPointAnimNavSOData
    //====================================================================
    public PathPointDescriptor.SmartObjectNavDataPtr GetLastPathPointAnimNavSOData()
    {
        if (m_pathPoints.Count > 0)
        {
            PathPointDescriptor lastPoint = m_pathPoints[m_pathPoints.Count - 1];
            if (lastPoint.navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
            {
                if (lastPoint.navSOMethod == ENavSOMethod.nSOmSignalAnimation || lastPoint.navSOMethod == ENavSOMethod.nSOmActionAnimation)
                {
                    return lastPoint.pSONavData;
                }
            }
        }
        return null;
    }

    //====================================================================
    // GetLastPathPointMNNSOData
    //====================================================================
    public PathPointDescriptor.OffMeshLinkData GetLastPathPointMNNSOData()
    {
        if (m_pathPoints.Count > 0)
        {
            PathPointDescriptor lastPoint = m_pathPoints[m_pathPoints.Count - 1];
            if (lastPoint.navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
            {
                if (lastPoint.navSOMethod == ENavSOMethod.nSOmSignalAnimation || lastPoint.navSOMethod == ENavSOMethod.nSOmActionAnimation)
                {
                    return lastPoint.offMeshLinkData;
                }
            }
        }
        return null;
    }

    //====================================================================
    // SetPreviousPoint
    //====================================================================
    public void SetPreviousPoint(PathPointDescriptor previousPoint)
    {
        if (m_pathPoints.Count == 0)
            return;
        ++m_version.v;
        m_pathPoints[0] = previousPoint;
    }

    public TPathPoints GetPath() { return m_pathPoints; }

    public void SetPathPoints(TPathPoints points) { m_pathPoints = points; ++m_version.v; }

    //====================================================================
    // GetMaxDistanceFromStart
    //====================================================================
    public float GetMaxDistanceFromStart()
    {
        if (m_pathPoints.Count < 2)
            return 0.0f;

        float maxDistanceSq = 0.0f;
        Vec3 startPathPointPosition = m_pathPoints[0].vPos;
        for (int it = 1; it < m_pathPoints.Count; ++it)
        {
            PathPointDescriptor ppd = m_pathPoints[it];
            float distanceSq = Distance.Point_PointSq(startPathPointPosition, ppd.vPos);
            maxDistanceSq = fsel(maxDistanceSq - distanceSq, maxDistanceSq, distanceSq);
        }

        return sqrtf(maxDistanceSq);
    }

    //====================================================================
    // GetAABB
    //====================================================================
    public AABB GetAABB(float dist)
    {
        float distLeft = dist;
        AABB result = new AABB(AABB.RESET);

        if (m_pathPoints.Count < 2)
            return result;

        Vec3 thisPt;
        if (!GetPosAlongPath(out thisPt))
            return result;

        result.Add(thisPt);

        // remaining path
        for (int it = 1; it < m_pathPoints.Count; ++it)
        {
            PathPointDescriptor ppd = m_pathPoints[it];

            Vec3 delta = ppd.vPos - thisPt;
            float deltaLen = delta.NormalizeSafe();
            if (deltaLen > distLeft)
            {
                result.Add(thisPt + distLeft * delta);
                return result;
            }
            thisPt = ppd.vPos;
            result.Add(thisPt);
            distLeft -= deltaLen;
        }
        return result;
    }

    //====================================================================
    // GetPathPropertiesAhead
    //====================================================================
    public bool GetPathPropertiesAhead(float distAhead, bool twoD, out Vec3 posOut, out Vec3 dirOut,
        out float invROut, out float lowestPathDotOut, bool scaleOutputWithDist)
    {
        posOut = new Vec3(0, 0, 0);
        dirOut = new Vec3(0, 0, 0);
        invROut = 0;
        lowestPathDotOut = 1.0f;

        float dist = distAhead;
        bool extrapolateBeyondEnd = false;

        if (m_pathPoints.Count < 2)
            return false;

        Vec3 thisPt = GetNextPathPoint().vPos;
        Vec3 currentPos = m_currentFrac * thisPt + (1.0f - m_currentFrac) * GetPrevPathPoint().vPos;
        Vec3 prevPt = currentPos;

        Vec3 firstSegDir = thisPt - currentPos;
        if (twoD) firstSegDir.z = 0.0f;
        firstSegDir = firstSegDir.NormalizeSafeVec(new Vec3(0, 1, 0));
        dirOut = firstSegDir;

        int it;
        for (it = 1; it < m_pathPoints.Count; ++it)
        {
            thisPt = m_pathPoints[it].vPos;
            Vec3 delta = thisPt - prevPt;
            float deltaLen = twoD ? delta.GetLength2D() : delta.Length();

            Vec3 currentSegDir = delta;
            if (twoD) currentSegDir.z = 0.0f;
            float segLen = currentSegDir.NormalizeSafe();
            if (segLen < 0.001f)
                continue;
            dirOut = currentSegDir;
            float pathDirDot = currentSegDir.Dot(firstSegDir);
            if (scaleOutputWithDist)
            {
                float frac = dist / distAhead;
                pathDirDot = frac * pathDirDot + (1.0f - frac) * 1.0f;
            }

            if (pathDirDot < lowestPathDotOut)
                lowestPathDotOut = pathDirDot;

            // calculate posOut here so it is correct when we exit the loop
            float frac2 = deltaLen > 0.0f ? dist / deltaLen : 1.0f;
            posOut = frac2 * thisPt + (1.0f - frac2) * prevPt;

            if (dist <= deltaLen)
                break;
            dist -= deltaLen;
            prevPt = thisPt;
        }

        if (it == m_pathPoints.Count && !extrapolateBeyondEnd)
        {
            // fallen off the end
            posOut = thisPt;
        }

        {
            // estimate the radius of curvature
            float directDistance = twoD ? Distance.Point_Point2D(posOut, currentPos) : Distance.Point_Point(posOut, currentPos);

            if (directDistance < distAhead)
            {
                float d = 0.5f * directDistance;
                float hScale = 0.5f;
                float h = sqrtf(square(0.5f * distAhead) - square(d));
                h *= hScale;
                float R = 0.5f * (square(h) + square(d)) / h;
                invROut = 1.0f / R;
            }
            else
            {
                invROut = 0.0f;
            }
        }
        return true;
    }

    //====================================================================
    // Serialize
    //====================================================================
    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("CNavPath");

        int pathSize = m_pathPoints.Count;
        ser.Value("pathSize", ref pathSize);

        if (ser.IsReading())
        {
            m_pathPoints.Clear();
            for (int i = 0; i < pathSize; ++i)
                m_pathPoints.Add(new PathPointDescriptor());
        }

        ser.Value("m_endDir", ref m_endDir);
        m_debugLines.Clear();
        m_debugSpheres.Clear();
        ser.Value("m_currentFrac", ref m_currentFrac);
        ser.Value("m_stuckTime", ref m_stuckTime);
        ser.Value("m_pathEndIsAsRequested", ref m_pathEndIsAsRequested);
        m_params.Serialize(ser);

        ser.Value("m_fDiscardedPathLength", ref m_fDiscardedPathLength);
        m_version.Serialize(ser);

        ser.EndGroup();
    }

    public Vec3 GetEndDir() { return m_endDir; }
    public void SetEndDir(Vec3 endDir) { m_endDir = endDir; }

    //====================================================================
    // UpdateAndSteerAlongPath
    //====================================================================
    public bool UpdateAndSteerAlongPath(out Vec3 dirOut, out float distToEndOut, out float distToPathOut, out bool isResolvingSticking,
        out Vec3 pathDirOut, out Vec3 pathAheadDirOut, out Vec3 pathAheadPosOut, Vec3 currentPos, Vec3 currentVel,
        float lookAhead, float pathRadius, float dt, bool resolveSticking, bool twoD)
    {
        dirOut = new Vec3(0, 0, 0);
        distToEndOut = 0;
        distToPathOut = 0;
        isResolvingSticking = false;
        pathDirOut = new Vec3(0, 0, 0);
        pathAheadDirOut = new Vec3(0, 0, 0);
        pathAheadPosOut = new Vec3(0, 0, 0);

        m_debugLines.Clear();
        m_debugSpheres.Clear();

        if (m_pathPoints.Count < 2)
            return false;

        // use a kind-of mid-point method
        Vec3 workingPos = currentPos;

        Vec3 origPathPosition = currentPos;
        origPathPosition = GetPrevPathPoint().vPos * (1.0f - m_currentFrac) + GetNextPathPoint().vPos * m_currentFrac;

        // update the path position.
        distToEndOut = UpdatePathPosition(workingPos, lookAhead, twoD, true);
        distToPathOut = 0.0f;

        if (m_pathPoints.Count < 2)
            return false;

        float workingLookAhead = lookAhead;
        isResolvingSticking = false;
        if (resolveSticking && distToEndOut > lookAhead)
        {
            Vec3 newPathPosition = GetPrevPathPoint().vPos * (1.0f - m_currentFrac) + GetNextPathPoint().vPos * m_currentFrac;
            float dist2 = (newPathPosition - origPathPosition).Length();
            float expectedDist = twoD ? dt * currentVel.GetLength2D() : dt * currentVel.Length();

            distToPathOut = twoD ? (currentPos - newPathPosition).GetLength2D() : (currentPos - newPathPosition).Length();

            float stickFrac = 0.1f;
            if (dist2 < stickFrac * expectedDist && distToPathOut > 0.5f * pathRadius)
                m_stuckTime += dt;
            else
                m_stuckTime -= min(m_stuckTime, dt);

            float minStickTime = 0.2f;
            float maxStickTime = 1.0f;
            float stickTimeRate = 1.0f;
            if (m_stuckTime > minStickTime)
            {
                isResolvingSticking = true;
                workingLookAhead *= 1.0f / (1.0f + stickTimeRate * (m_stuckTime - minStickTime));
            }
            if (m_stuckTime > maxStickTime)
                m_stuckTime = maxStickTime;
        }
        else
        {
            Vec3 newPathPosition = GetPrevPathPoint().vPos * (1.0f - m_currentFrac) + GetNextPathPoint().vPos * m_currentFrac;
            distToPathOut = twoD ? (currentPos - newPathPosition).GetLength2D() : (currentPos - newPathPosition).Length();
        }

        distToEndOut += distToPathOut;

        float minLookAheadFrac = 0.1f;
        float minLookAheadAlongPath = minLookAheadFrac * lookAhead;
        pathAheadPosOut = CalculateTargetPos(workingPos, workingLookAhead, minLookAheadAlongPath, pathRadius, twoD);

        DebugLine(currentPos, pathAheadPosOut, new ColorF(1, 0, 0));
        DebugSphere(pathAheadPosOut, 0.3f, new ColorF(1, 0, 0));

        dirOut = (pathAheadPosOut - currentPos).GetNormalizedSafe(new Vec3(1, 0, 0));

        pathDirOut = (GetNextPathPoint().vPos - GetPrevPathPoint().vPos).GetNormalizedSafe();
        pathAheadDirOut = pathDirOut;

        // Sometimes even if the agent reached the steering point it wouldn't advance the path position
        if ((currentPos - pathAheadPosOut).Dot(pathAheadPosOut - workingPos) > 0.0f)
        {
            float advanceDist = twoD ? (pathAheadPosOut - workingPos).GetLength2D() : (pathAheadPosOut - workingPos).Length();
            Vec3 pathNextPoint;
            if (GetPosAlongPath(out pathNextPoint, advanceDist, true, false))
                UpdatePathPosition(pathNextPoint, 100.0f, true, false);
        }

        return true;
    }

    //====================================================================
    // PrepareNavigationalSmartObjectsForMNM
    //====================================================================
    public void PrepareNavigationalSmartObjectsForMNM(IAIPathAgent pAgent)
    {
        m_fDiscardedPathLength = 0.0f;

        IEntity pEntity = pAgent != null ? pAgent.GetPathAgentEntity() : null;
        IAIObject pAIObject = pEntity != null ? pEntity.GetAI() : null;
        CPipeUser pPipeUser = pAIObject != null ? pAIObject.CastToCPipeUser() : null;

        if (pPipeUser == null)
            return;

        for (int it = 0; it < m_pathPoints.Count; ++it)
        {
            if (m_pathPoints[it].navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
            {
                PathPointDescriptor pathPointDescriptor = m_pathPoints[it];
                ++it;
                Navigation.MNM.OffMeshLink pOffMeshLink = gAIEnv.pNavigationSystem != null ?
                    gAIEnv.pNavigationSystem.GetOffMeshNavigationManager()?.GetOffMeshNavigationForMesh(new NavigationMeshID { id = pathPointDescriptor.offMeshLinkData.meshID })?.GetObjectLinkInfo(pathPointDescriptor.offMeshLinkData.offMeshLinkID) : null;
                OffMeshLink_SmartObject pSOLink = pOffMeshLink != null ? pOffMeshLink.CastTo_SmartObject() : null;
                if (pSOLink != null)
                {
                    pathPointDescriptor.navSOMethod = (ENavSOMethod)(
                        gAIEnv.pSmartObjectManager != null ?
                        gAIEnv.pSmartObjectManager.GetNavigationalSmartObjectActionTypeForMNM(
                            pPipeUser, pSOLink.m_pSmartObject, pSOLink.m_pSmartObjectClass,
                            pSOLink.m_pFromHelper, pSOLink.m_pToHelper) : 0);

                    ENavSOMethod navSOMethod = pathPointDescriptor.navSOMethod;
                    if (navSOMethod == ENavSOMethod.nSOmStraight)
                    {
                        if (it < m_pathPoints.Count)
                            continue;
                        else
                            return;
                    }

                    ++m_version.v;
                    if (it < m_pathPoints.Count)
                    {
                        Vec3 vPos = pathPointDescriptor.vPos;
                        for (int itNext = it; itNext < m_pathPoints.Count; ++itNext)
                        {
                            Vec3 vNextPos = m_pathPoints[itNext].vPos;
                            m_fDiscardedPathLength += Distance.Point_Point(vPos, vNextPos);
                            vPos = vNextPos;
                        }

                        m_remainingPathPoints.Clear();
                        for (int k = it; k < m_pathPoints.Count; ++k)
                            m_remainingPathPoints.Add(m_pathPoints[k]);

                        m_pathPoints.RemoveRange(it, m_pathPoints.Count - it);
                        return;
                    }
                    else
                    {
                        m_pathPoints.RemoveAt(m_pathPoints.Count - 1);
                        return;
                    }
                }
                else
                {
                    AILog.AIAssert(false);
                    return;
                }
            }
        }
    }

    //====================================================================
    // ResurrectRemainingPath
    //====================================================================
    public void ResurrectRemainingPath()
    {
        if (m_remainingPathPoints.Count > 0)
        {
            m_fDiscardedPathLength = 0.0f;
            // swap
            TPathPoints temp = m_pathPoints;
            m_pathPoints = m_remainingPathPoints;
            m_remainingPathPoints = temp;
            m_remainingPathPoints.Clear();
            m_version.v++;
        }
    }

    //====================================================================
    // TrimPath
    //====================================================================
    public void TrimPath(float trimLength, bool twoD)
    {
        if (m_pathPoints.Count < 2)
            return;

        float initialLength = GetPathLength(twoD);

        if (trimLength > 0.0f)
            trimLength = initialLength - trimLength;
        else
            trimLength = -trimLength;

        if (trimLength > initialLength)
            return;

        if (trimLength < 0.01f)
        {
            // Trim the whole path.
            ++m_version.v;
            PathPointDescriptor pt = m_pathPoints[m_pathPoints.Count - 1];
            m_pathPoints.Clear();
            m_pathPoints.Add(pt);
            m_pathPoints.Add(pt);
            return;
        }

        float runLength = 0.0f;
        for (int it = 0; it < m_pathPoints.Count; ++it)
        {
            int itNext = it + 1;
            if (itNext >= m_pathPoints.Count)
                break;

            PathPointDescriptor start = m_pathPoints[it];
            PathPointDescriptor end = m_pathPoints[itNext];

            float segmentLength;
            if (twoD)
                segmentLength = Distance.Point_Point2D(start.vPos, end.vPos);
            else
                segmentLength = Distance.Point_Point(start.vPos, end.vPos);

            if (trimLength >= runLength && trimLength < (runLength + segmentLength))
            {
                // If end point is navso, reset it.
                if (start.navType == IAISystem_ENavigationType.NAV_SMARTOBJECT && end.navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
                {
                    return;
                }
                else
                {
                    if (end.navType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
                    {
                        end.navType = start.navType;
                        end.pSONavData = null;
                        end.navSOMethod = ENavSOMethod.nSOmNone;
                    }

                    float u = (trimLength - runLength) / segmentLength;
                    end.vPos = start.vPos + u * (end.vPos - start.vPos);
                    m_pathPoints[itNext] = end;

                    ++itNext;
                }

                // Remove the end of the path.
                if (itNext < m_pathPoints.Count)
                    m_pathPoints.RemoveRange(itNext, m_pathPoints.Count - itNext);

                ++m_version.v;

                return;
            }

            runLength += segmentLength;
        }
    }

    public float GetDiscardedPathLength() { return m_fDiscardedPathLength; }

    //====================================================================
    // AdjustPathAroundObstacles (with navCapMask)
    //====================================================================
    public bool AdjustPathAroundObstacles(CPathObstacles obstacles, uint navCapMask)
    {
        MovePathEndsOutOfObstacles(obstacles);

        foreach (CPathObstacleReal obstacle in obstacles.GetCombinedObstacles())
        {
            if (!AdjustPathAroundObstacle(obstacle, navCapMask))
                return false;
        }
        return true;
    }

    //====================================================================
    // AdjustPathAroundObstacles (with movementAbility)
    //====================================================================
    public bool AdjustPathAroundObstacles(Vec3 currentpos, AgentMovementAbility movementAbility)
    {
        m_obstacles.CalculateObstaclesAroundLocation(currentpos, movementAbility, this);

        MovePathEndsOutOfObstacles(m_obstacles);

        foreach (CPathObstacleReal obstacle in m_obstacles.GetCombinedObstacles())
        {
            if (!AdjustPathAroundObstacle(obstacle, movementAbility.pathfindingProperties.navCapMask))
                return false;
        }
        return true;
    }

    //====================================================================
    // UpdatePathPosition
    //====================================================================
    public float UpdatePathPosition(Vec3 agentPos, float lookAhead, bool twoD, bool allowPathToFinish)
    {
        float totalLen = GetPathLength(twoD);

        if (twoD)
            agentPos.z = 0.0f;

        while (m_pathPoints.Count >= 2)
        {
            // start and end of this segment
            Vec3 segStart = m_pathPoints[0].vPos;
            Vec3 segEnd = m_pathPoints[1].vPos;

            float segLen = twoD ? (segStart - segEnd).GetLength2D() : (segStart - segEnd).Length();
            if (segLen > 0.1f)
            {
                // the end of the next segment (may be faked)
                Vec3 segEndNext;

                if (m_pathPoints.Count > 2)
                    segEndNext = m_pathPoints[2].vPos;
                else
                    segEndNext = segEnd + (segEnd - segStart);

                if (twoD)
                {
                    segStart.z = 0.0f;
                    segEnd.z = 0.0f;
                    segEndNext.z = 0.0f;
                }

                Vec3 segDir = (segEnd - segStart).GetNormalizedSafe();

                // don't advance until we're within the path width of the segment end
                Plane planeSeg = Plane.CreatePlane(segDir, segEnd);
                float planeSegDist = planeSeg.DistFromPlane(agentPos);
                if (planeSegDist < -0.6f * lookAhead)
                {
                    float newFrac = m_currentFrac;
                    Distance.Point_LinesegSq(agentPos, new Lineseg(segStart, segEnd), out newFrac);
                    if (newFrac > 1.0f)
                        m_currentFrac = 1.0f;
                    else if (newFrac > m_currentFrac)
                        m_currentFrac = newFrac;
                    return totalLen;
                }

                // past the divider between this and the next?
                Vec3 nextSegDir = (segEndNext - segEnd).GetNormalizedSafe(segDir);
                Vec3 avDir = (segDir + nextSegDir).GetNormalizedSafe(segDir);

                Plane planeSegDiv = Plane.CreatePlane(avDir, segEnd);
                float planeSegDivDist = planeSegDiv.DistFromPlane(agentPos);

                if (planeSegDivDist <= 0.0f)
                {
                    float newFrac = m_currentFrac;
                    Distance.Point_LinesegSq(agentPos, new Lineseg(segStart, segEnd), out newFrac);
                    if (newFrac > 1.0f)
                        m_currentFrac = 1.0f;
                    else if (newFrac > m_currentFrac)
                        m_currentFrac = newFrac;

                    return totalLen;
                }
            } // trivial segment length

            if (!allowPathToFinish && m_pathPoints.Count == 2)
            {
                m_currentFrac = 0.99f;
                return totalLen;
            }

            // moving onto the next segment
            PathPointDescriptor junk;
            Advance(out junk);
            m_currentFrac = 0.0f;
        }
        // got to the end
        m_currentFrac = 0.0f;
        return totalLen;
    }

    //====================================================================
    // CalculateTargetPos
    //====================================================================
    public Vec3 CalculateTargetPos(Vec3 agentPos, float lookAhead, float minLookAheadAlongPath, float pathRadius, bool twoD)
    {
        if (m_pathPoints.Count == 0)
            return agentPos;
        if (m_pathPoints.Count == 1)
            return m_pathPoints[0].vPos;

        Vec3 pathPos = GetPrevPathPoint().vPos * (1.0f - m_currentFrac) + GetNextPathPoint().vPos * m_currentFrac;

        DebugLine(agentPos, pathPos, new ColorF(0, 1, 0));
        DebugSphere(pathPos, 0.3f, new ColorF(0, 1, 0));

        float distAgentToPathPos = twoD ? (pathPos - agentPos).GetLength2D() : (pathPos - agentPos).Length();
        Vec3 pathDelta = GetNextPathPoint().vPos - pathPos;
        float distPathPosToCurrent = twoD ? pathDelta.GetLength2D() : pathDelta.Length();

        lookAhead -= distAgentToPathPos;

        if (lookAhead < minLookAheadAlongPath)
            lookAhead = minLookAheadAlongPath;

        if (lookAhead < distPathPosToCurrent)
        {
            Vec3 targetPos = pathPos + lookAhead * pathDelta / distPathPosToCurrent;
            return targetPos;
        }

        if (m_pathPoints.Count == 2)
        {
            lookAhead -= distPathPosToCurrent;

            Vec3 delta = GetNextPathPoint().vPos - GetPrevPathPoint().vPos;
            if (twoD) delta.z = 0.0f;
            float deltaLen = delta.NormalizeSafe();
            if (deltaLen < 0.01f)
            {
                return GetNextPathPoint().vPos;
            }
            else
            {
                delta = delta * lookAhead;
                return GetNextPathPoint().vPos + delta;
            }
        }
        // walk forward through the remaining path
        Vec3 targetPos2;
        if (false == GetPosAlongPath(out targetPos2, lookAhead, twoD, true))
            return GetNextPathPoint().vPos;

        Lineseg curSeg = new Lineseg(GetPrevPathPoint().vPos, GetNextPathPoint().vPos);
        // if almost on the next seg use that one
        PathPointDescriptor pNextNext = GetNextNextPathPoint();
        if (pNextNext != null)
        {
            Lineseg nextSeg = new Lineseg(curSeg.end, pNextNext.vPos);
            float junk;
            float distToNextSegSq = twoD ? Distance.Point_Lineseg2DSq(agentPos, nextSeg, out junk) : Distance.Point_LinesegSq(agentPos, nextSeg, out junk);
            if (distToNextSegSq < square(pathRadius))
                curSeg = nextSeg;
        }

        if (WouldTargetPosExceedPathSeg(targetPos2, curSeg, pathRadius, twoD))
        {
            lookAhead = minLookAheadAlongPath + 0.5f * (lookAhead - minLookAheadAlongPath);
            float delta2 = lookAhead * 0.5f;
            for (int i = 0; i < 8; ++i)
            {
                if (false == GetPosAlongPath(out targetPos2, lookAhead, twoD, true))
                    return GetNextPathPoint().vPos;
                if (WouldTargetPosExceedPathSeg(targetPos2, curSeg, pathRadius, twoD))
                    lookAhead -= delta2;
                else
                    lookAhead += delta2;
                delta2 *= 0.5f;
            }
        }
        return targetPos2;
    }

    //====================================================================
    // CanTargetPointBeReached
    //====================================================================
    public ETriState CanTargetPointBeReached(CTargetPointRequest request, CAIActor pAIActor, bool twoD)
    {
        request.result = ETriState.eTS_false;

        if (m_pathPoints.Count == 0)
            return ETriState.eTS_false;

        string szAIActorName = pAIActor.GetName();

        PathPointDescriptor lastPathPoint = m_pathPoints[m_pathPoints.Count - 1];
        Vec3 curEndPt = lastPathPoint.vPos;
        curEndPt.z = request.targetPoint.z + 0.5f;

        request.pathID = m_version.v;
        request.itIndex = 0;
        request.itBeforeIndex = 1;
        request.splitPoint = curEndPt;

        Vec3 delta = request.targetPoint - curEndPt;
        if (twoD) delta.z = 0.0f;
        float dist = delta.Length();

        float criticalMinDist = 0.5f;
        float criticalMaxDist = 4.0f;

        // blindly allow very small changes
        if (dist < criticalMinDist)
        {
            AILogComment("CNavPath::CanTargetPointBeReached trivial distance for {0}", szAIActorName);
            return request.result = ETriState.eTS_true;
        }

        // forbid huge changes
        if (dist > criticalMaxDist)
        {
            AILogComment("CNavPath::CanTargetPointBeReached excessive distance for {0}", szAIActorName);
            return request.result = ETriState.eTS_maybe;
        }

        if (m_pathPoints.Count < 2)
            return ETriState.eTS_false;

        // walk back dist along the path from the end
        int itIdx = m_pathPoints.Count - 1;
        int itBeforeIdx = itIdx;
        float distLeft = dist;
        float lastFrac2 = 1.0f;
        Vec3 candidatePt = curEndPt;
        for (int itb = itIdx - 1; itIdx >= 0 && itb >= 0; --itIdx, --itb)
        {
            Vec3 pos = m_pathPoints[itIdx].vPos;
            Vec3 posBefore = m_pathPoints[itb].vPos;
            float thisSegLen = twoD ? (pos - posBefore).GetLength2D() : (pos - posBefore).Length();
            if (thisSegLen <= 0.0001f)
                continue;
            if (thisSegLen < distLeft)
            {
                distLeft -= thisSegLen;
                candidatePt = posBefore;
            }
            else
            {
                lastFrac2 = distLeft / thisSegLen;
                candidatePt = lastFrac2 * posBefore + (1.0f - lastFrac2) * pos;
                itBeforeIdx = itb;
                break;
            }
        }
        candidatePt.z = request.targetPoint.z + 0.5f;

        request.itIndex = m_pathPoints.Count - 1 - itIdx;
        request.itBeforeIndex = m_pathPoints.Count - 1 - itBeforeIdx;
        request.splitPoint = candidatePt;

        float radius = pAIActor.m_Parameters.m_fPassRadius;
        if (twoD)
        {
            if (!AICollision.CheckWalkability(candidatePt, request.targetPoint + new Vec3(0, 0, 0.5f), radius))
            {
                AILogComment("CNavPath::CanTargetPointBeReached no 2D walkability for {0}", szAIActorName);
                return request.result = ETriState.eTS_false;
            }
        }
        else
        {
            if (AICollision.OverlapCapsule(new Lineseg(candidatePt, request.targetPoint), radius, EAICollisionEntities.AICE_ALL))
            {
                AILogComment("CNavPath::CanTargetPointBeReached no 3D passability for {0}", szAIActorName);
                return request.result = ETriState.eTS_false;
            }
        }

        AILogComment("CNavPath::CanTargetPointBeReached Accepting new path target point ({0:F2}, {1:F2}, {2:F2}) for {3}",
            request.targetPoint.x, request.targetPoint.y, request.targetPoint.z, szAIActorName);

        return request.result = ETriState.eTS_true;
    }

    //====================================================================
    // UseTargetPointRequest
    //====================================================================
    public bool UseTargetPointRequest(CTargetPointRequestEx request, CAIActor pAIActor, bool twoD)
    {
        if (m_pathPoints.Count == 0)
            return false;

        string szAIActorName = pAIActor.GetName();

        PathPointDescriptor lastPathPoint = m_pathPoints[m_pathPoints.Count - 1];
        Vec3 curEndPt = lastPathPoint.vPos;
        PathPointDescriptor.SmartObjectNavDataPtr pSONavData = lastPathPoint.pSONavData;
        IAISystem_ENavigationType lastNavType = lastPathPoint.navType;

        float criticalMinDist = 0.5f;
        float dist2 = twoD ? Distance.Point_Point2D(request.targetPoint, curEndPt) : Distance.Point_Point(request.targetPoint, curEndPt);
        // blindly allow very small changes
        if (dist2 < criticalMinDist)
        {
            AILogComment("CNavPath::UseTargetPointRequest trivial distance for {0} ({1:F3})", szAIActorName, dist2);
            lastPathPoint.vPos = request.targetPoint;
            m_pathPoints[m_pathPoints.Count - 1] = lastPathPoint;
            m_pathPoints.Insert(0, new PathPointDescriptor(IAISystem_ENavigationType.NAV_UNSET, pAIActor.GetPhysicsPos()));
            return true;
        }

        if (m_pathPoints.Count == 1)
        {
            AILogComment("CNavPath::UseTargetPointRequest excessive for {0} when path size = 1", szAIActorName);
            return false;
        }

        CTargetPointRequest workingReq = request;
        if (request.pathID != m_version.v || request.result != ETriState.eTS_true)
        {
            ETriState res = CanTargetPointBeReached(workingReq, pAIActor, twoD);
            if (res != ETriState.eTS_true)
                return false;
        }

        Limit(ref workingReq.itIndex, 0, m_pathPoints.Count - 1);
        Limit(ref workingReq.itBeforeIndex, 0, m_pathPoints.Count - 1);

        // reverse iteration via index from end
        int itFromEnd = workingReq.itIndex;
        int itBeforeFromEnd = workingReq.itBeforeIndex;

        if (itBeforeFromEnd >= m_pathPoints.Count)
        {
            // replace the whole path
            while (m_pathPoints.Count > 1)
                m_pathPoints.RemoveAt(1);
            PathPointDescriptor clonedPt = new PathPointDescriptor(m_pathPoints[m_pathPoints.Count - 1].navType, m_pathPoints[m_pathPoints.Count - 1].vPos);
            clonedPt.vPos = workingReq.targetPoint;
            m_pathPoints.Add(clonedPt);
        }
        else
        {
            int fwdIdx = m_pathPoints.Count - 1 - itFromEnd;
            if (fwdIdx >= 0 && fwdIdx < m_pathPoints.Count)
            {
                PathPointDescriptor fwdPt = m_pathPoints[fwdIdx];
                fwdPt.vPos = workingReq.splitPoint;
                m_pathPoints[fwdIdx] = fwdPt;
                // delete everything after
                while (m_pathPoints.Count > fwdIdx + 1)
                    m_pathPoints.RemoveAt(m_pathPoints.Count - 1);
            }
            PathPointDescriptor clonedPt = new PathPointDescriptor(m_pathPoints[m_pathPoints.Count - 1].navType, m_pathPoints[m_pathPoints.Count - 1].vPos);
            clonedPt.vPos = workingReq.targetPoint;
            m_pathPoints.Add(clonedPt);
            if (lastNavType == IAISystem_ENavigationType.NAV_SMARTOBJECT)
                m_pathPoints[m_pathPoints.Count - 1].navType = lastNavType;
        }

        m_pathPoints[m_pathPoints.Count - 1].pSONavData = pSONavData;
        m_pathPoints[m_pathPoints.Count - 1].vPos = workingReq.targetPoint;
        m_params.inhibitPathRegeneration = true;
        m_params.continueMovingAtEnd = (lastNavType == IAISystem_ENavigationType.NAV_SMARTOBJECT);
        ++m_version.v;
        return true;
    }

    public virtual void Release() { /* delete this */ }
    public virtual void CopyTo(INavPath pRecipient)
    {
        if (pRecipient is CNavPathReal other)
        {
            other.m_pathPoints = new TPathPoints(m_pathPoints);
            other.m_endDir = m_endDir;
            other.m_currentFrac = m_currentFrac;
            other.m_stuckTime = m_stuckTime;
            other.m_pathEndIsAsRequested = m_pathEndIsAsRequested;
            other.m_params = m_params;
            other.m_fDiscardedPathLength = m_fDiscardedPathLength;
            other.m_remainingPathPoints = new TPathPoints(m_remainingPathPoints);
            other.m_version.v = m_version.v + 1;
        }
    }
    public virtual INavPath Clone()
    {
        CNavPathReal clone = new CNavPathReal();
        CopyTo(clone);
        return clone;
    }

    //====================================================================
    // AdjustPathAroundObstacle
    //====================================================================
    private bool AdjustPathAroundObstacle(CPathObstacleReal obstacle, uint navCapMask)
    {
        switch (obstacle.GetType())
        {
        case CPathObstacleReal.EPathObstacleType.ePOT_Shape2D:
            return AdjustPathAroundObstacleShape2D(obstacle.GetShape2D(), navCapMask);
        default:
            AILog.AIError("CNavPath::AdjustPathAroundObstacle unhandled type {0}", (int)obstacle.GetType());
            break;
        }
        return false;
    }

    //====================================================================
    // AdjustPathAroundObstacleShape2D
    //====================================================================
    private bool AdjustPathAroundObstacleShape2D(SPathObstacleShape2DReal obstacle, uint navCapMask)
    {
        if ((navCapMask & ((uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_ROAD)) == 0)
            return true;

        if (m_pathPoints.Count == 0 || obstacle.pts.Count == 0)
            return true;

        int itCutEntrance = -1;
        int itCutExit = -1;
        Vec3 entrancePoint = new Vec3(0, 0, 0);
        Vec3 exitPoint = new Vec3(0, 0, 0);
        float entranceCounter = float.MaxValue;
        float exitCounter = float.MinValue;
        int entranceShapeIndex = -1;
        int exitShapeIndex = -1;
        float entranceShapeFrac = -1;
        float exitShapeFrac = -1;

        int pointAmount = obstacle.pts.Count;
        Vec3 polyCenter = new Vec3(0, 0, 0);
        foreach (Vec3 pt in obstacle.pts)
            polyCenter = polyCenter + pt;
        polyCenter = polyCenter / (float)pointAmount;

        float offset = 0.1f;

        bool usingMNM = (GetMeshID().id != 0);
        uint startNavTypeFilter = usingMNM ?
            ((uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_ROAD | (uint)IAISystem_ENavigationType.NAV_UNSET) :
            ((uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_ROAD);
        uint endNavTypeFilter = startNavTypeFilter | (uint)IAISystem_ENavigationType.NAV_SMARTOBJECT;

        int segCounter = 0;
        for (int it = 0; it < m_pathPoints.Count; ++it, ++segCounter)
        {
            int itNext = it + 1;
            if (itNext >= m_pathPoints.Count)
                break;

            PathPointDescriptor start = m_pathPoints[it];
            PathPointDescriptor end = m_pathPoints[itNext];

            if (((uint)start.navType & startNavTypeFilter) == 0 || ((uint)end.navType & endNavTypeFilter) == 0)
                continue;

            Lineseg pathSegment = new Lineseg(start.vPos, end.vPos);

            for (int pointIndex = 0; pointIndex < pointAmount; ++pointIndex)
            {
                int nextPointIndex = (pointIndex + 1) % pointAmount;
                Lineseg obstacleSegment = new Lineseg(obstacle.pts[pointIndex], obstacle.pts[nextPointIndex]);

                float pathSegmentFraction, obstacleSegmentFraction;
                if (!Intersect.Lineseg_Lineseg2D(pathSegment, obstacleSegment, out pathSegmentFraction, out obstacleSegmentFraction))
                    continue;

                Vec3 intersectionPoint = pathSegment.GetPoint(pathSegmentFraction);

                float zTolerance = 0.1f;
                bool isObstacleOnSameLayer = (intersectionPoint.z > obstacle.minZ - zTolerance) && (intersectionPoint.z < obstacle.maxZ + zTolerance);
                if (!isObstacleOnSameLayer)
                    continue;

                if ((segCounter + pathSegmentFraction) < entranceCounter)
                {
                    entranceCounter = segCounter + pathSegmentFraction;
                    itCutEntrance = it;
                    entrancePoint = intersectionPoint;
                    entranceShapeIndex = pointIndex;
                    entranceShapeFrac = obstacleSegmentFraction;
                    Vec2 diff = new Vec2(entrancePoint.x - polyCenter.x, entrancePoint.y - polyCenter.y);
                    Vec2 norm = diff.GetNormalized();
                    entrancePoint = entrancePoint + new Vec3(norm.x * offset, norm.y * offset, 0);
                }
                if ((segCounter + pathSegmentFraction) > exitCounter)
                {
                    exitCounter = segCounter + pathSegmentFraction;
                    itCutExit = it;
                    exitPoint = intersectionPoint;
                    exitShapeIndex = pointIndex;
                    exitShapeFrac = obstacleSegmentFraction;
                    Vec2 diff = new Vec2(entrancePoint.x - polyCenter.x, entrancePoint.y - polyCenter.y);
                    Vec2 norm = diff.GetNormalized();
                    exitPoint = exitPoint + new Vec3(norm.x * offset, norm.y * offset, 0);
                }
            }
        }

        if (itCutEntrance == -1 || itCutExit == -1)
            return true;

        ++m_version.v;

        int itCutExitAfter = itCutExit + 1;

        // Remove intermediate points
        for (int it = itCutEntrance + 1; it < itCutExitAfter && it < m_pathPoints.Count; )
        {
            m_pathPoints.RemoveAt(it);
            --itCutExitAfter;
        }

        // now insert the extra points before itCutExitAfter
        IAISystem_ENavigationType navType = m_pathPoints[itCutEntrance].navType;

        float distFwd = 0.0f;
        float distBwd = 0.0f;
        TPathPoints pathFwd = new TPathPoints();
        TPathPoints pathBwd = new TPathPoints();

        pathFwd.Add(new PathPointDescriptor(navType, entrancePoint));
        pathBwd.Add(new PathPointDescriptor(navType, entrancePoint));

        if (entranceShapeIndex == exitShapeIndex)
        {
            bool shortestRouteIsFwd = entranceShapeFrac < exitShapeFrac;
            distFwd = shortestRouteIsFwd ? 0.0f : 1.0f;
            distBwd = shortestRouteIsFwd ? 1.0f : 0.0f;

            if (shortestRouteIsFwd)
            {
                bool doingFirst = true;
                for (int iPt = entranceShapeIndex; doingFirst || iPt != exitShapeIndex; )
                {
                    doingFirst = false;
                    Vec3 aheadPt = obstacle.pts[iPt];
                    aheadPt.z = entrancePoint.z;
                    Vec2 diff = new Vec2(aheadPt.x - polyCenter.x, aheadPt.y - polyCenter.y);
                    Vec2 norm = diff.GetNormalized();
                    aheadPt = aheadPt + new Vec3(norm.x * offset, norm.y * offset, 0);
                    pathBwd.Add(new PathPointDescriptor(navType, aheadPt));
                    if (--iPt < 0) iPt = pointAmount - 1;
                }
            }
            else
            {
                bool doingFirst = true;
                for (int iPt = entranceShapeIndex; doingFirst || iPt != exitShapeIndex; )
                {
                    doingFirst = false;
                    int iAheadPt = (iPt + 1) % pointAmount;
                    Vec3 aheadPt = obstacle.pts[iAheadPt];
                    aheadPt.z = entrancePoint.z;
                    Vec2 diff = new Vec2(aheadPt.x - polyCenter.x, aheadPt.y - polyCenter.y);
                    Vec2 norm = diff.GetNormalized();
                    aheadPt = aheadPt + new Vec3(norm.x * offset, norm.y * offset, 0);
                    pathFwd.Add(new PathPointDescriptor(navType, aheadPt));
                    if (++iPt >= pointAmount) iPt = 0;
                }
            }
        }
        else
        {
            distFwd = (obstacle.pts[exitShapeIndex] - exitPoint).GetLength2D();
            distBwd = (obstacle.pts[(exitShapeIndex + 1) % pointAmount] - exitPoint).GetLength2D();

            // fwd
            Vec3 lastPt = entrancePoint;
            for (int iPt = entranceShapeIndex; iPt != exitShapeIndex; )
            {
                int iAheadPt = (iPt + 1) % pointAmount;
                Vec3 aheadPt = obstacle.pts[iAheadPt];
                aheadPt.z = entrancePoint.z;
                Vec2 diff = new Vec2(aheadPt.x - polyCenter.x, aheadPt.y - polyCenter.y);
                Vec2 norm = diff.GetNormalized();
                aheadPt = aheadPt + new Vec3(norm.x * offset, norm.y * offset, 0);
                pathFwd.Add(new PathPointDescriptor(navType, aheadPt));
                distFwd += (aheadPt - lastPt).GetLength2D();
                lastPt = aheadPt;
                if (++iPt >= pointAmount) iPt = 0;
            }
            // bwd
            lastPt = entrancePoint;
            for (int iPt = entranceShapeIndex; iPt != exitShapeIndex; )
            {
                Vec3 aheadPt = obstacle.pts[iPt];
                aheadPt.z = entrancePoint.z;
                Vec2 diff = new Vec2(aheadPt.x - polyCenter.x, aheadPt.y - polyCenter.y);
                Vec2 norm = diff.GetNormalized();
                aheadPt = aheadPt + new Vec3(norm.x * offset, norm.y * offset, 0);
                pathBwd.Add(new PathPointDescriptor(navType, aheadPt));
                distBwd += (aheadPt - lastPt).GetLength2D();
                lastPt = aheadPt;
                if (--iPt < 0) iPt = pointAmount - 1;
            }
        }

        pathFwd.Add(new PathPointDescriptor(navType, exitPoint));
        pathBwd.Add(new PathPointDescriptor(navType, exitPoint));

        float checkRadius = 0.3f;
        int insertAt = itCutEntrance + 1;
        if (distFwd < distBwd)
        {
            if (CheckPath(pathFwd, checkRadius))
                m_pathPoints.InsertRange(insertAt, pathFwd);
            else if (CheckPath(pathBwd, checkRadius))
                m_pathPoints.InsertRange(insertAt, pathBwd);
            else
                return false;
            return true;
        }
        else
        {
            if (CheckPath(pathBwd, checkRadius))
                m_pathPoints.InsertRange(insertAt, pathBwd);
            else if (CheckPath(pathFwd, checkRadius))
                m_pathPoints.InsertRange(insertAt, pathFwd);
            else
                return false;
            return true;
        }
    }

    //====================================================================
    // CheckPath
    //====================================================================
    private bool CheckPath(TPathPoints pathList, float radius)
    {
        bool usingMNM = (GetMeshID().id != 0);

        if (usingMNM)
        {
            // MNM raycast check — simplified
            // The full implementation requires MNM::MeshGrid internals which are shell types.
            // For now, return true (path is valid) matching the shell behavior.
            return true;
        }
        else
        {
            Vec3 closestPoint = new Vec3(0, 0, 0);
            for (int it = 0; it < pathList.Count; ++it)
            {
                int itNext = it + 1;
                if (itNext >= pathList.Count)
                    return true;
                Vec3 from = pathList[it].vPos;
                Vec3 to = pathList[itNext].vPos;
                if (gAIEnv.pNavigation != null && gAIEnv.pNavigation.IntersectsForbidden(from, to, out closestPoint))
                    return false;
            }
        }

        return true;
    }

    //====================================================================
    // MovePathEndsOutOfObstacles
    //====================================================================
    private void MovePathEndsOutOfObstacles(CPathObstacles obstacles)
    {
        if (m_pathPoints.Count == 0)
            return;

        ++m_version.v;
        float extra = 0.1f;

        bool usingMNM = (GetMeshID().id != 0);

        if (usingMNM)
        {
            uint navTypeFilter = (uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_ROAD | (uint)IAISystem_ENavigationType.NAV_UNSET;

            if (((uint)m_pathPoints[0].navType & navTypeFilter) != 0)
            {
                Vec3 newPosition = obstacles.GetPointOutsideObstacles(m_pathPoints[0].vPos, extra);
                if (!newPosition.IsEquivalent(m_pathPoints[0].vPos))
                {
                    PathPointDescriptor ppd = m_pathPoints[0];
                    ppd.vPos = newPosition;
                    m_pathPoints[0] = ppd;
                }
            }

            if (((uint)m_pathPoints[m_pathPoints.Count - 1].navType & navTypeFilter) != 0)
            {
                Vec3 newPosition = obstacles.GetPointOutsideObstacles(m_pathPoints[m_pathPoints.Count - 1].vPos, extra);
                if (!newPosition.IsEquivalent(m_pathPoints[m_pathPoints.Count - 1].vPos))
                {
                    PathPointDescriptor ppd = m_pathPoints[m_pathPoints.Count - 1];
                    ppd.vPos = newPosition;
                    m_pathPoints[m_pathPoints.Count - 1] = ppd;
                }
            }
        }
        else
        {
            uint navTypeFilter = (uint)IAISystem_ENavigationType.NAV_TRIANGULAR | (uint)IAISystem_ENavigationType.NAV_ROAD;

            if (((uint)m_pathPoints[0].navType & navTypeFilter) != 0)
            {
                PathPointDescriptor ppd = m_pathPoints[0];
                ppd.vPos = obstacles.GetPointOutsideObstacles(ppd.vPos, extra);
                m_pathPoints[0] = ppd;
            }

            if (((uint)m_pathPoints[m_pathPoints.Count - 1].navType & navTypeFilter) != 0)
            {
                PathPointDescriptor ppd = m_pathPoints[m_pathPoints.Count - 1];
                ppd.vPos = obstacles.GetPointOutsideObstacles(ppd.vPos, extra);
                m_pathPoints[m_pathPoints.Count - 1] = ppd;
            }
        }
    }

    //====================================================================
    // GetPathDeviationDistance
    //====================================================================
    private float GetPathDeviationDistance(out Vec3 deviationOut, float criticalDeviation, bool twoD)
    {
        deviationOut = new Vec3(0, 0, 0);
        if (m_pathPoints.Count < 3)
            return 0.0f;

        int it = 0;
        Lineseg thisSeg;
        thisSeg = new Lineseg(m_pathPoints[it].vPos, m_pathPoints[it + 1].vPos);
        it += 2;

        float distOut = twoD ? (1.0f - m_currentFrac) * Distance.Point_Point2D(thisSeg.start, thisSeg.end) :
            m_currentFrac * Distance.Point_Point(thisSeg.start, thisSeg.end);
        Vec3 lastPt = thisSeg.end;
        float lastDeviation = 0.0f;
        for (; it < m_pathPoints.Count; ++it)
        {
            Vec3 pt = m_pathPoints[it].vPos;
            float t;
            float deviation = twoD ? Distance.Point_Lineseg2D(pt, new Lineseg(thisSeg.start, thisSeg.end), out t) :
                Distance.Point_Lineseg(pt, new Lineseg(thisSeg.start, thisSeg.end), out t);
            float segLen = twoD ? Distance.Point_Point2D(lastPt, pt) : Distance.Point_Point(lastPt, pt);
            if (deviation > criticalDeviation)
            {
                float frac = 1.0f - (deviation - criticalDeviation) / (deviation - lastDeviation);
                distOut += frac * segLen;
                Vec3 exitPt = frac * pt + (1.0f - frac) * lastPt;

                Vec3 exitDelta = exitPt - thisSeg.start;
                Vec3 segDir = (thisSeg.end - thisSeg.start).GetNormalizedSafe();
                deviationOut = exitDelta - exitDelta.Dot(segDir) * segDir;
                if (twoD)
                    deviationOut.z = 0.0f;
                return distOut;
            }
            distOut += segLen;
            lastPt = pt;
            lastDeviation = deviation;
        }

        return distOut;
    }

    // ---- Fields ----
    private TPathPoints m_pathPoints = new TPathPoints();
    private Vec3 m_endDir;

    private struct SDebugLine
    {
        public Vec3 P0, P1;
        public ColorF col;
    }
    private void DebugLine(Vec3 P0, Vec3 P1, ColorF col)
    {
        if (gAIEnv.CVars.DebugDraw == 0)
            return;
        SDebugLine line;
        line.P0 = P0;
        line.P1 = P1;
        line.col = col;
        m_debugLines.AddLast(line);
    }
    private LinkedList<SDebugLine> m_debugLines = new LinkedList<SDebugLine>();

    private struct SDebugSphere
    {
        public Vec3 pos; public float r; public ColorF col;
    }
    private void DebugSphere(Vec3 P, float r, ColorF col)
    {
        if (gAIEnv.CVars.DebugDraw == 0)
            return;
        SDebugSphere sphere;
        sphere.pos = P;
        sphere.r = r;
        sphere.col = col;
        m_debugSpheres.AddLast(sphere);
    }
    private LinkedList<SDebugSphere> m_debugSpheres = new LinkedList<SDebugSphere>();

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
        public void Serialize(TSerialize ser) { ser.Value("version", ref v); }
    }
    private SVersion m_version = new SVersion { v = -1 };

    // Helper constants
    private const float VEC_EPSILON = 0.05f;

    private static bool IsEquivalent2D(Vec3 v0, Vec3 v1, float epsilon = VEC_EPSILON)
    {
        return (fabs_tpl(v0.x - v1.x) <= epsilon) && (fabs_tpl(v0.y - v1.y) <= epsilon);
    }

    private static float fsel(float a, float b, float c) { return a >= 0.0f ? b : c; }

    //====================================================================
    // WouldTargetPosExceedPathSeg
    //====================================================================
    private static bool WouldTargetPosExceedPathSeg(Vec3 targetPos, Lineseg pathSeg, float radius, bool twoD)
    {
        if (twoD)
        {
            Vec3 segDir = pathSeg.end - pathSeg.start;
            segDir.z = 0.0f;
            segDir = segDir.GetNormalizedSafe();
            Vec3 delta = targetPos - pathSeg.start;
            delta.z = 0.0f;
            Vec3 perp = delta - segDir * delta.Dot(segDir);
            float distSq = perp.x * perp.x + perp.y * perp.y;
            return distSq > square(radius);
        }
        else
        {
            Vec3 segDir = (pathSeg.end - pathSeg.start).GetNormalizedSafe();
            Vec3 delta = targetPos - pathSeg.start;
            Vec3 perp = delta - segDir * delta.Dot(segDir);
            float distSq = perp.GetLengthSquared();
            return distSq > square(radius);
        }
    }
}

// Forward decls / shells
public struct NavigationMeshID
{
    public uint id;
    public static implicit operator NavigationMeshID(uint v) => new NavigationMeshID { id = v };
    public static implicit operator uint(NavigationMeshID m) => m.id;
}
public class SNavPathParams
{
    public Vec3 start;
    public Vec3 end;
    public Vec3 startDir;
    public Vec3 endDir;
    public int nForceBuildingID = -1;
    public bool allowDangerousDestination;
    public float endDistance;
    public bool precalculatedPath;
    public NavigationMeshID meshID;
    public bool inhibitPathRegeneration;
    public bool continueMovingAtEnd;
    public bool isDirectional;

    public SNavPathParams()
    {
        start = new Vec3(0, 0, 0);
        end = new Vec3(0, 0, 0);
        startDir = new Vec3(0, 0, 0);
        endDir = new Vec3(0, 0, 0);
    }

    public SNavPathParams(Vec3 _start, Vec3 _end, Vec3 _startDir, Vec3 _endDir,
        int _nForceBuildingID = -1, bool _allowDangerousDestination = false, float _endDistance = 0.0f,
        bool _continueMovingAtEnd = false, bool _isDirectional = false)
    {
        start = _start; end = _end; startDir = _startDir; endDir = _endDir;
        nForceBuildingID = _nForceBuildingID; allowDangerousDestination = _allowDangerousDestination;
        endDistance = _endDistance; precalculatedPath = false; inhibitPathRegeneration = false;
        continueMovingAtEnd = _continueMovingAtEnd; isDirectional = _isDirectional;
    }

    public void Clear() { start = new Vec3(0,0,0); end = new Vec3(0,0,0); startDir = new Vec3(0,0,0); endDir = new Vec3(0,0,0); nForceBuildingID = -1; allowDangerousDestination = false; endDistance = 0; precalculatedPath = false; meshID = default; inhibitPathRegeneration = false; continueMovingAtEnd = false; isDirectional = false; }
    public void Serialize(TSerialize ser) { /* simplified */ }
}
public class PathPointDescriptor
{
    public Vec3 vPos;
    public IAISystem_ENavigationType navType;
    public ENavSOMethod navSOMethod;
    public SmartObjectNavDataPtr pSONavData;
    public OffMeshLinkData offMeshLinkData = new OffMeshLinkData();
    public ushort navTypeCustomId;
    public uint iTriId;

    public class SmartObjectNavDataPtr
    {
        public uint fromIndex;
        public uint toIndex;
    }
    public class OffMeshLinkData
    {
        public uint meshID;
        public uint offMeshLinkID;
    }
    public PathPointDescriptor() { }
    public PathPointDescriptor(IAISystem_ENavigationType navType) { this.navType = navType; this.vPos = new Vec3(0, 0, 0); }
    public PathPointDescriptor(IAISystem_ENavigationType navType, Vec3 pos) { this.navType = navType; this.vPos = pos; }
}
public class TPathPoints : List<PathPointDescriptor> { public TPathPoints() { } public TPathPoints(IEnumerable<PathPointDescriptor> c) : base(c) { } }
public class CPathObstacle { }
public class SPathObstacleShape2D { }

// CTargetPointRequest extension — the base class is defined in AIActor.cs.
// We extend it here with additional fields needed by CNavPath.
public class CTargetPointRequestEx : CTargetPointRequest
{
    public Vec3 targetPoint;
    public ETriState result;
    public int pathID;
    public int itIndex;
    public int itBeforeIndex;
    public Vec3 splitPoint;
    public bool continueMovingAtEnd;
}

// Plane.DistFromPlane extension
public static class PlaneExt
{
    public static float DistFromPlane(this Plane p, Vec3 pt)
    {
        return p.n.Dot(pt) + p.d;
    }
}
