// Literal port of dev/Code/CryEngine/CryAISystem/PathMarker.{h,cpp}
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// Description : CPathMarker contains the history of a given moving point (i.e. an entity position)
//               and can return a previous position interpolated given the distance from the current
//               point computed along the path.

using System.Collections.Generic;

namespace CryAISystem;

public class CSteeringDebugInfo
{
    public List<Vec3> pts = new List<Vec3>();
    public Vec3 segStart, segEnd;
}

public struct pathStep_t
{
    public Vec3 vPoint;
    public float fDistance;
    public bool bPassed;

    public pathStep_t(int dummy = 0)
    {
        vPoint = new Vec3(0, 0, 0);
        fDistance = 0f;
        bPassed = false;
    }
}

public class CPathMarker
{
    private List<pathStep_t> m_cBuffer = new List<pathStep_t>(); //using vector instead of deque as a FIFO, optimizing memory allocation and speed
    private float m_fStep;
    private int m_iCurrentPoint;
    private int m_iSize;
    private int m_iUsed;
    private float m_fTotalDistanceRun;

    public CPathMarker(float fMaxDistanceNeeded, float fStep)
    {
        if (fStep <= 0.0f)
            m_fStep = 0.1f;
        else
            m_fStep = fStep;
        m_iSize = (int)System.Math.Ceiling(fMaxDistanceNeeded / m_fStep) + 1;
        // m_cBuffer.resize(m_iSize)
        for (int i = 0; i < m_iSize; ++i)
            m_cBuffer.Add(new pathStep_t(0));
        m_iCurrentPoint = 0;
        m_fTotalDistanceRun = 0;
        m_iUsed = 0;
    }

    public void Update(Vec3 vNewPoint, bool b2D = false)
    {
        Vec3 vMovement = (vNewPoint - m_cBuffer[m_iCurrentPoint].vPoint);
        if (b2D)
            vMovement.z = 0;

        float fDistance = vMovement.Length();

        if (fDistance >= m_fStep)
        {
            ++m_iCurrentPoint;
            if (m_iCurrentPoint >= m_iSize)
                m_iCurrentPoint = 0;
            pathStep_t step = m_cBuffer[m_iCurrentPoint];
            step.vPoint = vNewPoint;
            step.fDistance = fDistance;
            m_cBuffer[m_iCurrentPoint] = step;
            m_fTotalDistanceRun += fDistance;
            if (m_iUsed < m_iSize) m_iUsed++;
        }
    }

    public Vec3 GetPointAtDistance(Vec3 vTargetPoint, float fDesiredDistance)
    {
        if (m_iUsed == 0)
            return vTargetPoint;

        float fComputedDistance = (vTargetPoint - m_cBuffer[m_iCurrentPoint].vPoint).Length();
        Vec3 vPoint;
        int iPoint = m_iCurrentPoint;

        Vec3 segStart = vTargetPoint;
        Vec3 segEnd = m_cBuffer[m_iCurrentPoint].vPoint;
        float segLen = fComputedDistance;

        for (int i = 0; i < m_iUsed - 1; i++)
        {
            --iPoint;
            if (iPoint < 0)
                iPoint = m_iSize - 1;

            if (fComputedDistance >= fDesiredDistance)
            {
                if (segLen > 0)
                {
                    float fAlpha;
                    fAlpha = 1.0f - (fComputedDistance - fDesiredDistance) / segLen;
                    if (fAlpha < 0) fAlpha = 0.0f;
                    if (fAlpha > 1) fAlpha = 1.0f;
                    return segStart + fAlpha * (segEnd - segStart);
                }
            }

            segStart = segEnd;
            segEnd = m_cBuffer[iPoint].vPoint;
            segLen = m_cBuffer[iPoint].fDistance;

            fComputedDistance += segLen;
        }

        return segEnd;
    }

    public Vec3 GetPointAtDistanceFromNewestPoint(float fDesiredDistanceFromNewestPoint)
    {
        if (m_iUsed == 0)
            return new Vec3(0, 0, 0);

        if (m_iUsed < 2 || fDesiredDistanceFromNewestPoint <= 0.0f)
            return m_cBuffer[m_iCurrentPoint].vPoint;

        pathStep_t segStart = default;
        pathStep_t segEnd = default;
        bool segValid = false;

        for (int i = 0; i < m_iUsed - 1; i++)
        {
            int indexSegEnd = m_iCurrentPoint - i;
            int indexSegStart = m_iCurrentPoint - i - 1;

            if (indexSegStart < 0)
                indexSegStart += m_iUsed;
            if (indexSegEnd < 0)
                indexSegEnd += m_iUsed;

            segEnd = m_cBuffer[indexSegEnd];
            segStart = m_cBuffer[indexSegStart];
            segValid = true;

            float segLen = segEnd.fDistance;

            if (fDesiredDistanceFromNewestPoint > segLen)
            {
                fDesiredDistanceFromNewestPoint -= segLen;
            }
            else
            {
                Vec3 dir = segStart.vPoint - segEnd.vPoint;
                dir.NormalizeSafe();
                return segEnd.vPoint + dir * fDesiredDistanceFromNewestPoint;
            }
        }

        return segValid ? segEnd.vPoint : new Vec3(0, 0, 0);
    }


    //
    //----------------------------------------------------------------------------------------------------------------
    public Vec3 GetDirectionAtDistance(Vec3 vTargetPoint, float fDesiredDistance)
    {
        if (m_iUsed == 0)
            return new Vec3(0, 0, 0);

        float fComputedDistance = (vTargetPoint - m_cBuffer[m_iCurrentPoint].vPoint).Length();
        Vec3 vDir = new Vec3(0, 0, 0);
        int iPoint = m_iCurrentPoint;

        Vec3 segStart = vTargetPoint;
        Vec3 segEnd = m_cBuffer[m_iCurrentPoint].vPoint;
        float segLen = fComputedDistance;

        for (int i = 0; i < m_iUsed; i++)
        {
            if (--iPoint < 0) iPoint = m_iSize - 1;

            if (fComputedDistance >= fDesiredDistance)
                break;

            segStart = segEnd;
            segEnd = m_cBuffer[iPoint].vPoint;
            segLen = m_cBuffer[iPoint].fDistance;

            fComputedDistance += segLen;
        }

        if (segLen > 0)
        {
            vDir = segStart - segEnd;
            vDir.NormalizeSafe();
        }

        return vDir;
    }

    //
    //----------------------------------------------------------------------------------------------------------------
    public Vec3 GetDirectionAtDistanceFromNewestPoint(float fDistanceFromNewestPoint)
    {
        if (m_iUsed == 0)
            return new Vec3(0, 0, 0);

        pathStep_t segEnd = default;
        pathStep_t segStart = default;
        bool segValid = false;

        for (int i = 0; i < m_iUsed; i++)
        {
            int indexSegEnd = m_iCurrentPoint - i;
            int indexSegStart = m_iCurrentPoint - 1 - i;

            if (indexSegStart < 0)
                indexSegStart += m_iUsed;
            if (indexSegEnd < 0)
                indexSegEnd += m_iUsed;

            segEnd = m_cBuffer[indexSegEnd];
            segStart = m_cBuffer[indexSegStart];
            segValid = true;

            if ((fDistanceFromNewestPoint -= segStart.fDistance) <= 0.0f)
                break;
        }

        if (!segValid)
            return new Vec3(0, 0, 0);

        Vec3 vDir = segEnd.vPoint - segStart.vPoint;
        vDir.NormalizeSafe();

        return vDir;
    }

    //
    //----------------------------------------------------------------------------------------------------------------
    public Vec3 GetMoveDirectionAtDistance(ref Vec3 vTargetPoint, float fDesiredDistance, Vec3 vUserPos,
                                           float falloff, ref float alignmentWithPath,
                                           CSteeringDebugInfo debugInfo)
    {
        int iPoint = m_iCurrentPoint;
        float closestDist = float.MaxValue;
        int closestIndex = iPoint; // default to last point
        int closestPtsBack = 0;
        int ptsBack = 0;
        for (int i = 0; i < m_iUsed; i++)
        {
            // guarantee first point won't be chosen so we can use closestIndex and
            // closestIndex + 1
            if (i != 0)
            {
                Vec3 delta = m_cBuffer[iPoint].vPoint - vUserPos;
                float dist = delta.Length();
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestIndex = iPoint;
                    closestPtsBack = ptsBack;
                }
            }
            if (--iPoint < 0) iPoint = m_iSize - 1;
            ++ptsBack;
        }

        // The direction is based on this segment, but as if this segment is moved ahead.
        // Without a lookahead then we don't anticipte corners. If we just use a segment
        // from in front then its direction on a corner will counteract any advantage.
        // This way if the path ahead turns to the right and we're already on the right
        // of the path, parallel to the path where we are, then we won't turn.
        int nextIndex = (closestIndex + 1) % m_iSize;
        // Get the orientation of the closest line segment
        Vec3 segDir = m_cBuffer[nextIndex].vPoint - m_cBuffer[closestIndex].vPoint;
        segDir.z = 0.0f;
        segDir.NormalizeSafe();
        Vec3 segRightDir = new Vec3(segDir.y, -segDir.x, 0.0f);

        // path can be wonky at the start - if so then use a fallback
        if ((segDir.Dot(vTargetPoint - vUserPos)) < 0.0f)
        {
            segDir = vTargetPoint - vUserPos;
            segDir.z = 0.0f;
            segDir.NormalizeSafe();
            alignmentWithPath = 0.0f;
        }

        // now lookahead
        float lookahead = falloff;
        iPoint = closestIndex;
        for (int i = 0; i < closestPtsBack; ++i)
        {
            iPoint = (iPoint + 1) % m_iSize;
            lookahead -= m_cBuffer[iPoint].fDistance;
            if (lookahead <= 0.0f)
                break;
        }

        // Work out how far to one side (the right) we are
        Vec3 deltaPos = vUserPos - m_cBuffer[iPoint].vPoint;
        deltaPos.z = 0.0f;
        Vec3 sideVec = deltaPos - (deltaPos.Dot(segDir)) * segDir;
        float sideDist = sideVec.Dot(segRightDir);

        // Now blend in a steering based on this distance and the falloff
        float steerFactor = -sideDist / falloff;
        if (steerFactor > 1.0f)
            steerFactor = 1.0f;
        else if (steerFactor < -1.0f)
            steerFactor = -1.0f;

        Vec3 userDir = segDir + steerFactor * segRightDir;
        userDir.NormalizeSafe();

        alignmentWithPath = userDir.Dot(segDir);

        if (debugInfo != null)
        {
            debugInfo.segStart = m_cBuffer[iPoint].vPoint;
            debugInfo.segEnd = debugInfo.segStart + segDir;

            debugInfo.pts.Clear();
            iPoint = m_iCurrentPoint;
            for (int i = 0; i < m_iUsed; i++)
            {
                debugInfo.pts.Add(m_cBuffer[iPoint].vPoint);
                if (--iPoint < 0)
                    iPoint = m_iSize - 1;
            }
        }

        return userDir;
    }

    //
    //----------------------------------------------------------------------------------------------------------------
    public float GetDistanceToPoint(Vec3 vTargetPoint, Vec3 vMyPoint)
    {
        float fComputedDistance = (vTargetPoint - m_cBuffer[m_iCurrentPoint].vPoint).Length();
        //the closest navpoint to the given MyPoint is chosen to compute the distance along the path
        //TO DO: that's not always exact, in some cases it could be the wrong point
        float fMinDistance2 = 100000000.0f;
        int iClosestPoint = m_iCurrentPoint;
        for (int i = 0; i < m_iUsed; i++)
        {
            float fCurrentDistance = (vMyPoint - m_cBuffer[i].vPoint).len2();
            if (fCurrentDistance <= fMinDistance2)
            {
                fMinDistance2 = fCurrentDistance;
                iClosestPoint = i;
            }
        }

        int iPoint2 = m_iCurrentPoint;
        while (iPoint2 != iClosestPoint)
        {
            fComputedDistance += m_cBuffer[iPoint2].fDistance;
            if (--iPoint2 < 0) iPoint2 = m_iSize - 1;
        }
        //final adjustment to distance : instead of adding the fDistance of iClosestPoint,
        // we add the distance from the previous point (iClosestPoint+1) to vMyPoint
        iPoint2 = (iClosestPoint + 1) % m_iSize;
        fComputedDistance += (vMyPoint - m_cBuffer[iPoint2].vPoint).Length();
        return fComputedDistance;
    }

    public float GetTotalDistanceRun() { return m_fTotalDistanceRun; }

    //
    //----------------------------------------------------------------------------------------------------------------
    public void Init(Vec3 vEndPoint, Vec3 vInitPoint)
    {
        pathStep_t step0 = m_cBuffer[0];
        step0.vPoint = vEndPoint;
        step0.fDistance = 0;
        step0.bPassed = false;
        m_cBuffer[0] = step0;
        m_iCurrentPoint = 0;
        m_fTotalDistanceRun = 0;
        m_iUsed = 1;
    }

    //
    //----------------------------------------------------------------------------------------------------------------
    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("AIPathMarker");
        {
            ser.Value("m_fStep", ref m_fStep);
            ser.Value("m_iCurrentPoint", ref m_iCurrentPoint);
            ser.Value("m_iSize", ref m_iSize);
            ser.Value("m_iUsed", ref m_iUsed);
            ser.Value("m_fTotalDistanceRun", ref m_fTotalDistanceRun);
            ser.BeginGroup("AIPathMarkerPoints");
            {
                if (ser.IsReading())
                {
                    m_cBuffer.Clear();
                    for (int i = 0; i < m_iSize; ++i)
                        m_cBuffer.Add(new pathStep_t(0));
                }
                for (int i = 0; i < m_iUsed; i++)
                {
                    ser.BeginGroup("point");
                    pathStep_t point = m_cBuffer[i];
                    ser.Value("pos", ref point.vPoint);
                    ser.Value("dist", ref point.fDistance);
                    ser.Value("passed", ref point.bPassed);
                    m_cBuffer[i] = point;
                    ser.EndGroup();
                }
                ser.EndGroup();
            }
            ser.EndGroup();
        }
    }

    public nuint GetPointCount() { return (nuint)m_cBuffer.Count; }

    public void DebugDraw()
    {
        if (m_iUsed < 2)
            return;

        int prev = m_iCurrentPoint;
        int cur = m_iCurrentPoint - 1;
        if (cur < 0) cur = m_iSize - 1;

        CDebugDrawContext dc = new CDebugDrawContext();

        for (int i = 0; i < m_iUsed - 1; ++i)
        {
            dc.DrawLine(m_cBuffer[prev].vPoint, new ColorB(255, 255, 255, 128),
                m_cBuffer[cur].vPoint, new ColorB(255, 255, 255, 128));
            prev = cur;
            --cur;
            if (cur < 0) cur = m_iSize - 1;
        }
    }
}
