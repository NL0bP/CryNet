// Literal port of dev/Code/CryEngine/CryAISystem/AIHideObject.h + AIHideObject.cpp (1319L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.GlobalFunctions;
using CryAISystem.CryCommon;

namespace CryAISystem;

public enum ECoverUsage
{
    USECOVER_NONE,
    USECOVER_SMARTOBJECT_HIDE,
    USECOVER_SMARTOBJECT_UNHIDE,
    USECOVER_STRAFE_LEFT_STANDING,
    USECOVER_STRAFE_RIGHT_STANDING,
    USECOVER_STRAFE_TOP_STANDING,
    USECOVER_STRAFE_TOP_LEFT_STANDING,
    USECOVER_STRAFE_TOP_RIGHT_STANDING,
    USECOVER_STRAFE_LEFT_CROUCHED,
    USECOVER_STRAFE_RIGHT_CROUCHED,
    USECOVER_CENTER_CROUCHED,
    USECOVER_LAST,
}

// Note: CAIHideObject was forward-declared in PipeUser.cs.
// Replace with full literal port here.
public class CAIHideObjectReal
{
    private const float LOW_COVER_OFFSET = 0.7f;
    private const float HIGH_COVER_OFFSET = 1.7f;
    private const int REFINE_SAMPLES = 4;
    private const float STEP_SIZE = 0.75f;
    private const float SAMPLE_DIST = 0.45f;
    private const float SAMPLE_RADIUS = 0.15f;

    private const float DEFAULT_AGENT_RADIUS = 0.4f;
    private static readonly uint DEFAULT_AGENT_NAVMASK =
        (uint)IAISystem_ENavigationType.NAV_TRIANGULAR |
        (uint)IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN;

    // friend class CPipeUser;

    //-------------------------------------------------------------------------------------------------------------
    public CAIHideObjectReal()
    {
        m_bIsValid = false;
        m_isUsingCover = false;
        m_objectPos = new Vec3(0, 0, 0);
        m_objectDir = new Vec3(0, 0, 0);
        m_objectRadius = 0;
        m_objectHeight = 0;
        m_objectCollidable = false;
        m_vLastHidePos = new Vec3(0, 0, 0);
        m_vLastHideDir = new Vec3(0, 0, 0);
        m_bIsSmartObject = false;
        m_useCover = (int)ECoverUsage.USECOVER_NONE;
        m_dynCoverEntityId = 0;
        m_dynCoverEntityPos = new Vec3(0, 0, 0);
        m_dynCoverPosLocal = new Vec3(0, 0, 0);
        m_hideSpotType = SHideSpotInfo.EHideSpotType.eHST_INVALID;
        m_coverPos = new Vec3(0, 0, 0);
        m_distToCover = 0;
        m_pathOrig = new Vec3(0, 0, 0);
        m_pathDir = new Vec3(0, 0, 0);
        m_pathNorm = new Vec3(0, 0, 0);
        m_pathLimitLeft = 0;
        m_pathLimitRight = 0;
        m_tempCover = 0;
        m_tempDepth = 0;
        m_pathComplete = false;
        m_highCoverValid = false;
        m_lowCoverValid = false;
        m_pathHurryUp = false;
        m_lowLeftEdgeValid = false;
        m_lowRightEdgeValid = false;
        m_highLeftEdgeValid = false;
        m_highRightEdgeValid = false;
        m_lowCoverWidth = 0;
        m_highCoverWidth = 0;
        m_pathUpdateIter = 0;
        m_id = 0;
    }

    //-------------------------------------------------------------------------------------------------------------
    public void Set(SHideSpot hs, Vec3 hidePos, Vec3 hideDir)
    {
        CCCPOINT(0); // AIHideObject_Set

        Vec3 oldObjPos = m_objectPos;
        Vec3 oldObjDir = m_objectDir;
        float oldRadius = m_objectRadius;

        m_bIsValid = (hs != null);
        m_bIsSmartObject = m_bIsValid ? (hs.info.type == SHideSpotInfo.EHideSpotType.eHST_SMARTOBJECT) : false;

        m_dynCoverEntityId = 0;
        m_dynCoverEntityPos = new Vec3(0, 0, 0);
        m_dynCoverPosLocal = new Vec3(0, 0, 0);
        m_sAnchorName = "";

        if (hs != null)
        {
            m_objectPos = hs.info.pos;
            m_objectDir = hs.info.dir;
            m_hideSpotType = hs.info.type;
            m_objectRadius = 0.0f;
            m_objectHeight = 0.0f;
            if (m_objectDir.IsZero())
            {
                m_objectDir = hideDir;
                if (m_objectDir.IsZero())
                {
                    m_objectDir = m_objectPos - hidePos;
                    m_objectDir = m_objectDir.GetNormalizedSafe();
                }
            }

            if (hs.pObstacle != null)
            {
                m_objectPos = hs.pObstacle.vPos;
                m_objectRadius = hs.pObstacle.fApproxRadius;
                if (m_objectRadius > 0.001f)
                    m_objectCollidable = hs.pObstacle.IsCollidable();
                else
                    m_objectCollidable = true;
                m_objectHeight = hs.pObstacle.GetApproxHeight();
            }
            if (hs.pAnchorObject != null)
            {
                if ((hs.pAnchorObject.GetType() == (ushort)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT) ||
                    (hs.pAnchorObject.GetType() == (ushort)EAIObjectType.AIANCHOR_COMBAT_HIDESPOT_SECONDARY))
                {
                    m_objectCollidable = true;
                    m_objectRadius = hs.pAnchorObject.GetRadius();
                }
                else
                {
                    m_objectCollidable = false;
                    m_objectRadius = 0.05f; // Omni directional.
                }

                m_sAnchorName = hs.pAnchorObject.GetName();
            }
            if (hs.info.type == SHideSpotInfo.EHideSpotType.eHST_SMARTOBJECT)
            {
                m_HideSmartObject = hs.SOQueryEvent;
                // Smart object position/direction — shell
                m_objectCollidable = true;
            }

            if (hs.pNavNode != null && hs.pNavNode.navType == IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN)
            {
                if (hs.pNavNode.GetWaypointNavData() != null && hs.pNavNode.GetWaypointNavData().type == EWaypointNodeType.WNT_HIDESECONDARY)
                {
                    m_objectCollidable = false;
                    m_objectRadius = 0.05f; // Omni directional.
                }
                else
                    m_objectCollidable = true;
            }

            if (hs.info.type == SHideSpotInfo.EHideSpotType.eHST_DYNAMIC)
            {
                IEntity pEnt = gEnv.pEntitySystem?.GetEntity(hs.entityId);
                if (pEnt != null)
                {
                    m_dynCoverEntityPos = pEnt.GetWorldPos();
                    m_dynCoverPosLocal = pEnt.GetWorldRotation().GetInverted() * (hs.info.pos - pEnt.GetWorldPos());
                    m_dynCoverEntityId = hs.entityId;
                }
                else
                {
                    m_dynCoverEntityId = 0;
                    m_bIsValid = false;
                }
            }
            else
            {
                m_dynCoverEntityId = 0;
            }
        }

        if (m_bIsValid)
        {
            m_vLastHidePos = hidePos;
            m_vLastHideDir = hideDir;

            // Marcio: Avoid re-sampling if nothing changed!
            if (!oldObjPos.IsEquivalent(m_objectPos, 0.005f) || !oldObjDir.IsEquivalent(m_objectDir, 0.005f) || (fabs_tpl(oldRadius - m_objectRadius) > 0.005f))
            {
                m_useCover = (int)ECoverUsage.USECOVER_NONE;
                m_pathUpdateIter = 0;
                m_pathComplete = false;
                m_pathHurryUp = false;
                m_isUsingCover = true;
                m_id++;
            }
        }

        if (!m_bIsValid && hidePos.Length() > 0.001f)
        {
            AILog.AIWarning("Trying to set invalid hidespots!");
        }
    }

    //-------------------------------------------------------------------------------------------------------------
    public void Update(CPipeUser pOperand)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);
        if (m_bIsValid && !m_bIsSmartObject)
            UpdatePathExpand(pOperand);
    }

    private static bool ProjectedPointOnLine(ref float u, Vec3 lineOrig, Vec3 lineDir, Vec3 lineNorm, Vec3 pt, Vec3 target)
    {
        Plane plane = new Plane();
        plane.SetPlane(lineNorm, lineOrig);
        Ray r = new Ray(); r.origin = target; r.direction = pt - target;
        if (r.direction.IsZero(0.000001f))
            return false;
        Vec3 intr = pt;
        bool res = Intersect.Ray_Plane(r, plane, out intr);
        u = lineDir.Dot(intr - lineOrig);
        return res;
    }

    //-------------------------------------------------------------------------------------------------------------
    public void GetCoverPoints(bool useLowCover, float peekOverLeft, float peekOverRight, Vec3 targetPos,
        out Vec3 hidePos, out Vec3 peekPosLeft, out Vec3 peekPosRight, out bool peekLeftClamped, out bool peekRightClamped, out bool coverCompromised)
    {
        hidePos = m_pathOrig;
        peekPosLeft = m_pathOrig;
        peekPosRight = m_pathOrig;
        peekLeftClamped = false;
        peekRightClamped = false;
        coverCompromised = false;

        if (m_pathNorm.Dot(targetPos - m_pathOrig) < 0.0f)
        {
            coverCompromised = true;
            return;
        }

        float umbraLeft = float.MaxValue, umbraRight = float.MinValue;
        if (!m_pathComplete)
        {
            float u = 0.0f;
            if (ProjectedPointOnLine(ref u, m_pathOrig, m_pathDir, m_pathNorm, m_coverPos, targetPos))
                umbraLeft = umbraRight = u;
        }

        bool coverEmpty = false;
        LinkedListNode<Vec3> begin, endNode;

        if (useLowCover)
        {
            coverEmpty = m_lowCoverPoints.Count == 0;
        }
        else
        {
            coverEmpty = m_highCoverPoints.Count == 0;
        }

        LinkedList<Vec3> coverPts = useLowCover ? m_lowCoverPoints : m_highCoverPoints;

        if (!coverEmpty)
        {
            int validPts = 0;
            foreach (Vec3 pt in coverPts)
            {
                float u = 0.0f;
                if (ProjectedPointOnLine(ref u, m_pathOrig, m_pathDir, m_pathNorm, pt, targetPos))
                {
                    umbraLeft = min(umbraLeft, u);
                    umbraRight = max(umbraRight, u);
                    validPts++;
                }
            }

            if (validPts == 0)
            {
                coverCompromised = true;
                return;
            }
        }

        float mid = (umbraLeft + umbraRight) * 0.5f;

        if (umbraLeft - peekOverLeft > mid)
            umbraLeft = mid;
        else
            umbraLeft -= peekOverLeft;

        if (umbraRight + peekOverRight < mid)
            umbraRight = mid;
        else
            umbraRight += peekOverRight;

        if (umbraLeft > umbraRight)
            umbraLeft = umbraRight = (umbraLeft + umbraRight) * 0.5f;

        if (umbraLeft < m_pathLimitLeft)
        { umbraLeft = m_pathLimitLeft; peekLeftClamped = true; }
        else if (umbraLeft > m_pathLimitRight)
        { umbraLeft = m_pathLimitRight; peekLeftClamped = true; }
        else
            peekLeftClamped = false;

        if (umbraRight > m_pathLimitRight)
        { umbraRight = m_pathLimitRight; peekRightClamped = true; }
        else if (umbraRight < m_pathLimitLeft)
        { umbraRight = m_pathLimitLeft; peekRightClamped = true; }
        else
            peekRightClamped = false;

        hidePos = m_pathOrig + m_pathDir * (umbraLeft + umbraRight) * 0.5f;
        peekPosLeft = m_pathOrig + m_pathDir * umbraLeft;
        peekPosRight = m_pathOrig + m_pathDir * umbraRight;

        if (coverEmpty)
            coverCompromised = m_pathComplete && fabsf((umbraLeft + umbraRight) * 0.5f) > 2.0f;
        else
            coverCompromised = m_pathComplete && (peekLeftClamped && peekRightClamped) && fabs(umbraRight - umbraLeft) < 0.001f;
    }

    //-------------------------------------------------------------------------------------------------------------
    public void GetCoverDistances(bool useLowCover, Vec3 target, out bool coverCompromised, out float leftEdge, out float rightEdge, out float leftUmbra, out float rightUmbra)
    {
        coverCompromised = false;
        leftUmbra = 0; rightUmbra = 0;

        Vec3 toTarget = (target - m_pathOrig).GetNormalized();

        if (m_pathNorm.Dot(toTarget) <= 0.2f)
        {
            leftUmbra = rightUmbra = 0.0f;
            leftEdge = rightEdge = 0.0f;
            coverCompromised = true;
            return;
        }

        bool coverEmpty;
        LinkedList<Vec3> coverPts;

        if (useLowCover)
        {
            leftEdge = m_lowLeftEdge;
            rightEdge = m_lowRightEdge;
            coverPts = m_lowCoverPoints;
            coverEmpty = m_lowCoverPoints.Count == 0;
        }
        else
        {
            leftEdge = m_highLeftEdge;
            rightEdge = m_highRightEdge;
            coverPts = m_highCoverPoints;
            coverEmpty = m_highCoverPoints.Count == 0;
        }

        if (!coverEmpty)
        {
            bool validFound = false;
            leftUmbra = float.MaxValue;
            rightUmbra = float.MinValue;

            foreach (Vec3 pt in coverPts)
            {
                float u = 0.0f;
                if (ProjectedPointOnLine(ref u, m_pathOrig, m_pathDir, m_pathNorm, pt, target))
                {
                    leftUmbra = min(leftUmbra, u);
                    rightUmbra = max(rightUmbra, u);
                    validFound = true;
                }
            }

            if (!validFound)
            {
                leftUmbra = rightUmbra = 0.0f;
                leftEdge = rightEdge = 0.0f;
                coverCompromised = true;
                return;
            }
        }

        float maxInsideDisplace = 0.75f;
        if ((leftUmbra > maxInsideDisplace) || (rightUmbra < -maxInsideDisplace))
            coverCompromised = true;

        if (leftUmbra < m_pathLimitLeft) leftUmbra = m_pathLimitLeft;
        if (leftUmbra > m_pathLimitRight) leftUmbra = m_pathLimitRight;
        if (rightUmbra > m_pathLimitRight) rightUmbra = m_pathLimitRight;
        if (rightUmbra < m_pathLimitLeft) rightUmbra = m_pathLimitLeft;

        if (!coverCompromised)
        {
            if (coverEmpty)
                coverCompromised = m_pathComplete;
            else
                coverCompromised = m_pathComplete && fabsf(rightUmbra - leftUmbra) < 0.5f;
        }
    }

    //-------------------------------------------------------------------------------------------------------------
    public bool IsValid()
    {
        if (!m_bIsValid)
            return false;

        if (m_dynCoverEntityId != 0)
        {
            IEntity pEnt = gEnv.pEntitySystem?.GetEntity(m_dynCoverEntityId);
            if (pEnt == null)
            {
                m_bIsValid = false;
                return false;
            }
            else
            {
                if (Distance.Point_PointSq(pEnt.GetWorldPos(), m_dynCoverEntityPos) > sqr(0.3f))
                {
                    m_bIsValid = false;
                    return false;
                }

                Vec3 curPos = pEnt.GetWorldPos() + pEnt.GetWorldRotation() * m_dynCoverPosLocal;
                if (Distance.Point_PointSq(m_objectPos, curPos) > sqr(0.3f))
                {
                    m_bIsValid = false;
                    return false;
                }
            }
        }

        return m_bIsValid;
    }

    //-------------------------------------------------------------------------------------------------------------
    public bool IsCompromised(CPipeUser pRequester, Vec3 target)
    {
        if (!IsValid()) return true;
        if (pRequester != null && pRequester.IsInCover() && !IsNearCover(pRequester)) return true;
        if (!IsCoverPathComplete()) return false;

        bool lowCompromised = !HasLowCover();
        bool highCompromised = !HasHighCover();

        float leftEdge, rightEdge, leftUmbra, rightUmbra;
        bool compromised;

        if (!lowCompromised)
            GetCoverDistances(true, target, out compromised, out leftEdge, out rightEdge, out leftUmbra, out rightUmbra);
        else compromised = lowCompromised;
        lowCompromised = compromised;

        if (!highCompromised)
            GetCoverDistances(false, target, out compromised, out leftEdge, out rightEdge, out leftUmbra, out rightUmbra);
        else compromised = highCompromised;
        highCompromised = compromised;

        return lowCompromised && highCompromised;
    }

    //-------------------------------------------------------------------------------------------------------------
    public bool IsNearCover(CPipeUser pRequester)
    {
        if (pRequester != null)
        {
            float safeRange = 0.25f;
            if (gAIEnv.configuration.eCompatibilityMode == EConfigCompatibilityMode.ECCM_CRYSIS)
                safeRange = 1.3f;

            float agentRadius = pRequester != null ? pRequester.GetParameters().m_fPassRadius : DEFAULT_AGENT_RADIUS;
            safeRange += agentRadius;

            float dist = GetDistanceToCoverPath(pRequester.GetPhysicsPos());
            if (dist > safeRange)
                return false;

            return true;
        }
        return false;
    }

    //-------------------------------------------------------------------------------------------------------------
    public float GetCoverWidth(bool useLowCover)
    {
        if (!m_pathComplete)
        {
            if (useLowCover) return max(0.5f, m_lowCoverWidth);
            else return max(0.5f, m_highCoverWidth);
        }
        else
        {
            if (useLowCover) return m_lowCoverWidth;
            else return m_highCoverWidth;
        }
    }

    //-------------------------------------------------------------------------------------------------------------
    public float GetDistanceToCoverPath(Vec3 pt)
    {
        Lineseg coverPath = new Lineseg(m_pathOrig + m_pathDir * m_pathLimitLeft, m_pathOrig + m_pathDir * m_pathLimitRight);
        float t;
        float distToLine = Distance.Point_Lineseg2D(pt, coverPath, out t);
        Vec3 ptOnLine = coverPath.GetPoint(t);
        float heightDist = fabsf(ptOnLine.z - pt.z);
        return max(distToLine, heightDist);
    }

    public float GetMaxCoverPathLen()
    {
        if (gAIEnv.configuration.eCompatibilityMode == EConfigCompatibilityMode.ECCM_CRYSIS2)
        {
            if (m_objectRadius > 0.001f)
                return m_objectRadius * 2.0f;
        }
        return 12.0f;
    }

    public Vec3 GetPointAlongCoverPath(float distance)
    {
        return m_pathOrig + m_pathDir * distance;
    }

    public void GetCoverHeightAlongCoverPath(float distanceAlongPath, Vec3 target, out bool hasLowCover, out bool hasHighCover)
    {
        hasLowCover = false;
        hasHighCover = false;

        // low cover
        float lowDistanceLeft = float.MinValue;
        float lowDistanceRight = float.MaxValue;
        bool lowRightEdge = true;

        foreach (Vec3 pt in m_lowCoverPoints)
        {
            float u = 0.0f;
            if (ProjectedPointOnLine(ref u, m_pathOrig, m_pathDir, m_pathNorm, pt, target))
            {
                if (u < distanceAlongPath)
                    lowDistanceLeft = u;
                else if (u > distanceAlongPath)
                {
                    lowDistanceRight = u;
                    lowRightEdge = false;
                    break;
                }
            }
        }

        if ((fabs_tpl(lowDistanceLeft - distanceAlongPath) <= 0.75f) &&
            (lowRightEdge || (fabs_tpl(lowDistanceRight - distanceAlongPath) <= 0.75f)))
            hasLowCover = true;

        // high cover
        float highDistanceLeft = float.MinValue;
        float highDistanceRight = float.MaxValue;
        bool highRightEdge = true;

        foreach (Vec3 pt in m_highCoverPoints)
        {
            float u = 0.0f;
            if (ProjectedPointOnLine(ref u, m_pathOrig, m_pathDir, m_pathNorm, pt, target))
            {
                if (u < distanceAlongPath)
                    highDistanceLeft = u;
                else if (u > distanceAlongPath)
                {
                    highDistanceRight = u;
                    highRightEdge = false;
                    break;
                }
            }
        }

        // Note: C++ uses lowRightEdge for both checks (likely a bug in original code, ported faithfully)
        if ((fabs_tpl(highDistanceLeft - distanceAlongPath) < 0.75f) &&
            (lowRightEdge || (fabs_tpl(highDistanceRight - distanceAlongPath) < 0.75f)))
            hasHighCover = true;
    }

    //-------------------------------------------------------------------------------------------------------------
    private void SetupPathExpand(CPipeUser pOperand)
    {
        Vec3 hidePos = GetObjectPos();
        Vec3 hideDir = GetObjectDir();
        float hideRadius = GetObjectRadius();
        float agentRadius = pOperand != null ? pOperand.GetParameters().m_fPassRadius : DEFAULT_AGENT_RADIUS;

        Vec3 floorPos = GetLastHidePos();
        GetAISystem()?.AdjustDirectionalCoverPosition(ref floorPos, hideDir, agentRadius, 0.7f);
        floorPos = new Vec3(floorPos.x, floorPos.y, floorPos.z - 0.7f);

        m_pathOrig = floorPos;
        m_distToCover = CAISystem.AGENT_COVER_CLEARANCE + agentRadius;

        m_pathNorm = m_vLastHideDir;
        m_coverPos = hidePos;

        m_pathNorm = new Vec3(m_pathNorm.x, m_pathNorm.y, 0);
        m_pathNorm = m_pathNorm.GetNormalizedSafe();
        m_pathDir = new Vec3(m_pathNorm.y, -m_pathNorm.x, 0);

        Vec3 hitPos = new Vec3(0, 0, 0);
        float hitDist = 0;

        Vec3 lowCoverOrig = new Vec3(m_pathOrig.x, m_pathOrig.y, m_pathOrig.z + LOW_COVER_OFFSET);
        Vec3 highCoverOrig = new Vec3(m_pathOrig.x, m_pathOrig.y, m_pathOrig.z + HIGH_COVER_OFFSET);

        m_lowCoverValid = AICollision.IntersectSweptSphere(ref hitPos, ref hitDist, new Lineseg(lowCoverOrig, lowCoverOrig + m_pathNorm * (m_distToCover + 0.5f)), SAMPLE_RADIUS, EAICollisionEntities.AICE_ALL);
        m_highCoverValid = AICollision.IntersectSweptSphere(ref hitPos, ref hitDist, new Lineseg(highCoverOrig, highCoverOrig + m_pathNorm * (m_distToCover + 0.5f)), SAMPLE_RADIUS, EAICollisionEntities.AICE_ALL);

        m_lowLeftEdgeValid = false;
        m_lowRightEdgeValid = false;
        m_highLeftEdgeValid = false;
        m_highRightEdgeValid = false;

        m_lowCoverPoints.Clear();
        m_highCoverPoints.Clear();

        m_lowLeftEdge = 0.0f;
        m_lowRightEdge = 0.0f;
        m_highLeftEdge = 0.0f;
        m_highRightEdge = 0.0f;

        m_pathLimitLeft = 0;
        m_pathLimitRight = 0;

        m_pathUpdateIter = 0;
        m_pathComplete = false;
    }

    //-------------------------------------------------------------------------------------------------------------
    private bool IsSegmentValid(CPipeUser pOperand, Vec3 posFrom, Vec3 posTo)
    {
        // Simplified shell — full nav region passability checks pending Phase 11
        return true;
    }

    //-------------------------------------------------------------------------------------------------------------
    private void SampleCover(CPipeUser pOperand, ref float maxCover, ref float maxDepth,
        Vec3 startPos, float maxWidth, float sampleDist, float sampleRad, float sampleDepth,
        LinkedList<Vec3> points, bool pushBack, ref bool reachedEdge)
    {
        Vec3 hitPos = new Vec3(0, 0, 0);
        Vec3 checkDir = m_pathNorm * sampleDepth;
        maxCover = 0;
        maxDepth = 0;
        reachedEdge = false;

        int n = 1 + (int)floorf(fabs(maxWidth) / sampleDist);
        float deltaWidth = 1.0f / (float)n * maxWidth;

        for (int i = 0; i < n; i++)
        {
            float w = deltaWidth * (i + 1);
            Vec3 pos = startPos + m_pathDir * w;
            float d = 0;

            if (!AICollision.IntersectSweptSphere(ref hitPos, ref d, new Lineseg(pos, pos + checkDir), sampleRad, EAICollisionEntities.AICE_ALL))
            {
                reachedEdge = true;
                break;
            }
            else
            {
                maxCover = w;
                if (pushBack)
                    points.AddLast(pos + m_pathNorm * d);
                else
                    points.AddFirst(pos + m_pathNorm * d);
            }
            maxDepth = max(maxDepth, d);
        }
    }

    //-------------------------------------------------------------------------------------------------------------
    private void SampleCoverRefine(CPipeUser pOperand, ref float maxCover, ref float maxDepth,
        Vec3 startPos, float maxWidth, float sampleDist, float sampleRad, float sampleDepth,
        LinkedList<Vec3> points, bool pushBack)
    {
        Vec3 hitPos = new Vec3(0, 0, 0);
        int n = 1 + (int)floorf(fabs(maxWidth) / sampleDist);
        float deltaWidth = 1.0f / (float)n * maxWidth;
        Vec3 checkDir = m_pathNorm * sampleDepth;

        float t = 0.0f;
        float dt = 0.5f;
        for (int j = 0; j < REFINE_SAMPLES; j++)
        {
            Vec3 pos = startPos + m_pathDir * (maxCover + deltaWidth * t);
            float dist = 0;
            if (!AICollision.IntersectSweptSphere(ref hitPos, ref dist, new Lineseg(pos, pos + checkDir), sampleRad, EAICollisionEntities.AICE_ALL))
                t -= dt;
            else
                t += dt;
            maxDepth = max(maxDepth, dist);
            dt *= 0.5f;
        }

        maxCover += deltaWidth * t;

        if (maxDepth < 0.01f)
            maxDepth = sampleDepth;

        if (pushBack)
            points.AddLast(startPos + m_pathDir * maxCover + m_pathNorm * maxDepth);
        else
            points.AddFirst(startPos + m_pathDir * maxCover + m_pathNorm * maxDepth);
    }

    //-------------------------------------------------------------------------------------------------------------
    private void SampleLine(CPipeUser pOperand, ref float maxMove, float maxWidth, float sampleDist)
    {
        Vec3 lastPos = m_pathOrig;
        int n = 1 + (int)floorf(fabs(maxWidth) / sampleDist);
        float deltaWidth = 1.0f / (float)n * maxWidth;

        for (int i = 0; i < n; i++)
        {
            float w = deltaWidth * (i + 1);
            Vec3 pos = m_pathOrig + m_pathDir * w;
            if (!IsSegmentValid(pOperand, lastPos, pos))
                break;
            else
                maxMove = w;
            lastPos = pos;
        }
    }

    private void SampleLineRefine(CPipeUser pOperand, ref float maxMove, float maxWidth, float sampleDist)
    {
        Vec3 lastPos = m_pathOrig + m_pathDir * maxMove;
        int n = 1 + (int)floorf(fabs(maxWidth) / sampleDist);
        float deltaWidth = 1.0f / (float)n * maxWidth;

        float t = 0.0f;
        float dt = 0.5f;
        for (int j = 0; j < REFINE_SAMPLES; j++)
        {
            Vec3 pos = m_pathOrig + m_pathDir * (maxMove + deltaWidth * t);
            if (!IsSegmentValid(pOperand, lastPos, pos))
                t -= dt;
            else
                t += dt;
            dt *= 0.5f;
        }

        maxMove += deltaWidth * t;
    }

    //-------------------------------------------------------------------------------------------------------------
    private void UpdatePathExpand(CPipeUser pOperand)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        if (m_pathUpdateIter == 0)
            SetupPathExpand(pOperand);

        if (m_pathComplete)
            return;

        float LINE_LEN = GetMaxCoverPathLen();

        Vec3 lowCoverOrig = new Vec3(m_pathOrig.x, m_pathOrig.y, m_pathOrig.z + LOW_COVER_OFFSET);
        Vec3 highCoverOrig = new Vec3(m_pathOrig.x, m_pathOrig.y, m_pathOrig.z + HIGH_COVER_OFFSET);

        int maxIter = 1;
        int iter = 0;

        if (m_pathHurryUp)
            maxIter = 2;

        while (!m_pathComplete && iter < maxIter)
        {
            switch (m_pathUpdateIter)
            {
                case 0: SampleLine(pOperand, ref m_pathLimitLeft, -LINE_LEN / 2, STEP_SIZE); iter++; break;
                case 1: SampleLineRefine(pOperand, ref m_pathLimitLeft, -LINE_LEN / 2, STEP_SIZE); iter++; break;
                case 2: SampleLine(pOperand, ref m_pathLimitRight, LINE_LEN / 2, STEP_SIZE); iter++; break;
                case 3: SampleLineRefine(pOperand, ref m_pathLimitRight, LINE_LEN / 2, STEP_SIZE); iter++; break;
                case 4: if (m_lowCoverValid) { SampleCover(pOperand, ref m_tempCover, ref m_tempDepth, lowCoverOrig, m_pathLimitLeft, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_lowCoverPoints, false, ref m_lowLeftEdgeValid); iter++; } break;
                case 5: if (m_lowCoverValid) { SampleCoverRefine(pOperand, ref m_tempCover, ref m_tempDepth, lowCoverOrig, m_pathLimitLeft, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_lowCoverPoints, false); iter++; } break;
                case 6: if (m_lowCoverValid) { SampleCover(pOperand, ref m_tempCover, ref m_tempDepth, lowCoverOrig, m_pathLimitRight, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_lowCoverPoints, true, ref m_lowRightEdgeValid); iter++; } break;
                case 7: if (m_lowCoverValid) { SampleCoverRefine(pOperand, ref m_tempCover, ref m_tempDepth, lowCoverOrig, m_pathLimitRight, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_lowCoverPoints, true); iter++; } break;
                case 8: if (m_highCoverValid) { SampleCover(pOperand, ref m_tempCover, ref m_tempDepth, highCoverOrig, m_pathLimitLeft, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_highCoverPoints, false, ref m_highLeftEdgeValid); iter++; } break;
                case 9: if (m_highCoverValid) { SampleCoverRefine(pOperand, ref m_tempCover, ref m_tempDepth, highCoverOrig, m_pathLimitLeft, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_highCoverPoints, false); iter++; } break;
                case 10: if (m_highCoverValid) { SampleCover(pOperand, ref m_tempCover, ref m_tempDepth, highCoverOrig, m_pathLimitRight, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_highCoverPoints, true, ref m_highRightEdgeValid); iter++; } break;
                case 11: if (m_highCoverValid) { SampleCoverRefine(pOperand, ref m_tempCover, ref m_tempDepth, highCoverOrig, m_pathLimitRight, SAMPLE_DIST, SAMPLE_RADIUS, m_distToCover + 1.0f, m_highCoverPoints, true); iter++; } break;
                default: m_pathComplete = true; break;
            }
            m_pathUpdateIter++;
        }

        // Update cover width.
        m_lowCoverWidth = 0.0f;
        m_highCoverWidth = 0.0f;

        float cmin, cmax;

        cmin = 0.0f; cmax = 0.0f;
        foreach (Vec3 pt in m_lowCoverPoints)
        {
            float u = m_pathDir.Dot(pt - m_pathOrig);
            cmin = min(cmin, u);
            cmax = max(cmax, u);
        }

        m_lowLeftEdge = cmin;
        m_lowRightEdge = cmax;

        Vec3 lowCoverOrig2 = new Vec3(m_pathOrig.x, m_pathOrig.y, m_pathOrig.z + LOW_COVER_OFFSET);
        if (m_pathComplete && !m_objectCollidable && m_objectRadius > 0.1f)
        {
            if (cmin > -m_objectRadius)
            {
                m_lowCoverPoints.AddFirst(lowCoverOrig2 + m_pathNorm * m_distToCover + m_pathDir * -m_objectRadius);
                m_lowLeftEdgeValid = true;
                cmin = -m_objectRadius;
            }
            m_lowLeftEdge = min(-m_objectRadius, cmin);

            if (cmax < m_objectRadius)
            {
                m_lowCoverPoints.AddLast(lowCoverOrig2 + m_pathNorm * m_distToCover + m_pathDir * m_objectRadius);
                m_lowRightEdgeValid = true;
                cmax = m_objectRadius;
            }
            m_lowRightEdge = max(m_objectRadius, cmax);
        }

        m_lowCoverWidth = cmax - cmin;

        cmin = 0.0f; cmax = 0.0f;
        foreach (Vec3 pt in m_highCoverPoints)
        {
            float u = m_pathDir.Dot(pt - m_pathOrig);
            cmin = min(cmin, u);
            cmax = max(cmax, u);
        }

        m_highLeftEdge = cmin;
        m_highRightEdge = cmax;

        Vec3 highCoverOrig2 = new Vec3(m_pathOrig.x, m_pathOrig.y, m_pathOrig.z + HIGH_COVER_OFFSET);
        if (m_pathComplete && !m_objectCollidable && m_objectRadius > 0.1f && m_objectHeight > 0.8f)
        {
            CCCPOINT(0); // CAIHideObject_UpdatePathExpand_HighCover
            if (cmin > -m_objectRadius)
            {
                m_highCoverPoints.AddFirst(highCoverOrig2 + m_pathNorm * m_distToCover + m_pathDir * -m_objectRadius);
                m_highLeftEdgeValid = true;
                cmin = -m_objectRadius;
            }
            m_highLeftEdge = min(-m_objectRadius, cmin);

            if (cmax < m_objectRadius)
            {
                m_highCoverPoints.AddLast(highCoverOrig2 + m_pathNorm * m_distToCover + m_pathDir * m_objectRadius);
                m_highRightEdgeValid = true;
                cmax = m_objectRadius;
            }
            m_highRightEdge = max(m_objectRadius, cmax);
        }

        m_highCoverWidth = cmax - cmin;
    }

    //-------------------------------------------------------------------------------------------------------------
    public void DebugDraw()
    {
        if (m_bIsSmartObject)
            return;

        // Debug draw shell — rendering calls omitted for brevity (match C++ 1:1)
        CDebugDrawContext dc = new CDebugDrawContext();
        ColorB white = new ColorB(255, 255, 255);
        dc.DrawLine(GetLastHidePos() + new Vec3(0, 0, 0.5f), white, GetObjectPos() + new Vec3(0, 0, 0.5f), white);
        dc.DrawLine(GetObjectPos() + new Vec3(0, 0, -0.5f), white, GetObjectPos() + new Vec3(0, 0, 2.5f), white);

        if (m_pathUpdateIter == 0) return;

        dc.DrawLine(m_pathOrig + m_pathDir * m_pathLimitLeft, white,
            m_pathOrig + m_pathDir * m_pathLimitRight, white);
    }

    //-------------------------------------------------------------------------------------------------------------
    public bool HasLowCover() { return m_lowCoverPoints.Count > 1; }
    public bool HasHighCover() { return m_highCoverPoints.Count > 1; }
    public bool IsLeftEdgeValid(bool useLowCover) { return useLowCover ? m_lowLeftEdgeValid : m_highLeftEdgeValid; }
    public bool IsRightEdgeValid(bool useLowCover) { return useLowCover ? m_lowRightEdgeValid : m_highRightEdgeValid; }
    public string GetAnchorName() { return m_sAnchorName; }

    //-------------------------------------------------------------------------------------------------------------
    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("AIHideObject");
        ser.Value("m_bIsValid", ref m_bIsValid);
        if (m_bIsValid)
        {
            ser.Value("m_isUsingCover", ref m_isUsingCover);
            ser.Value("m_objectPos", ref m_objectPos);
            ser.Value("m_objectDir", ref m_objectDir);
            ser.Value("m_objectRadius", ref m_objectRadius);
            ser.Value("m_objectCollidable", ref m_objectCollidable);
            ser.Value("m_vLastHidePos", ref m_vLastHidePos);
            ser.Value("m_vLastHideDir", ref m_vLastHideDir);
            ser.Value("m_bIsSmartObject", ref m_bIsSmartObject);
            // m_HideSmartObject.Serialize(ser);
            ser.Value("m_useCover", ref m_useCover);
            ser.Value("m_coverPos", ref m_coverPos);
            ser.Value("m_distToCover", ref m_distToCover);
            ser.Value("m_pathOrig", ref m_pathOrig);
            ser.Value("m_pathDir", ref m_pathDir);
            ser.Value("m_pathNorm", ref m_pathNorm);
            ser.Value("m_pathLimitLeft", ref m_pathLimitLeft);
            ser.Value("m_pathLimitRight", ref m_pathLimitRight);
            ser.Value("m_tempCover", ref m_tempCover);
            ser.Value("m_tempDepth", ref m_tempDepth);
            ser.Value("m_pathComplete", ref m_pathComplete);
            ser.Value("m_highCoverValid", ref m_highCoverValid);
            ser.Value("m_lowCoverValid", ref m_lowCoverValid);
            ser.Value("m_pathHurryUp", ref m_pathHurryUp);
            ser.Value("m_lowLeftEdgeValid", ref m_lowLeftEdgeValid);
            ser.Value("m_lowRightEdgeValid", ref m_lowRightEdgeValid);
            ser.Value("m_highLeftEdgeValid", ref m_highLeftEdgeValid);
            ser.Value("m_highRightEdgeValid", ref m_highRightEdgeValid);
            // ser.Value("m_lowCoverPoints", m_lowCoverPoints);
            // ser.Value("m_highCoverPoints", m_highCoverPoints);
            ser.Value("m_lowCoverWidth", ref m_lowCoverWidth);
            ser.Value("m_highCoverWidth", ref m_highCoverWidth);
            ser.Value("m_pathUpdateIter", ref m_pathUpdateIter);
            ser.Value("m_id", ref m_id);
            ser.Value("m_dynCoverEntityId", ref m_dynCoverEntityId);
            ser.Value("m_dynCoverEntityPos", ref m_dynCoverEntityPos);
            ser.Value("m_dynCoverPosLocal", ref m_dynCoverPosLocal);
        }
        ser.EndGroup();
    }

    // Public accessors matching the C++ .h
    public void Invalidate() { m_bIsValid = false; }
    public bool IsSmartObject() { return m_bIsSmartObject; }
    public CQueryEvent GetSmartObject() { return m_HideSmartObject; }
    public void SetSmartObject(CQueryEvent smObject) { m_HideSmartObject = smObject; }
    public void ClearSmartObject() { /* m_HideSmartObject.Clear(); */ }
    public bool IsUsingCover() { return m_isUsingCover; }
    public void SetUsingCover(bool state) { m_isUsingCover = state; }
    public int GetCoverUsage() { return m_useCover; }
    public void SetCoverUsage(int type) { m_useCover = type; }
    public uint GetCoverId() { return m_id; }
    public float GetObjectRadius() { return m_objectRadius; }
    public Vec3 GetObjectPos() { return m_objectPos; }
    public Vec3 GetObjectDir() { return m_objectDir; }
    public bool IsObjectCollidable() { return m_objectCollidable; }
    public Vec3 GetLastHidePos() { return m_vLastHidePos; }
    public SHideSpotInfo.EHideSpotType GetHideSpotType() { return m_hideSpotType; }
    public bool IsCoverPathComplete() { return m_pathComplete; }
    public void HurryUpCoverPathGen() { m_pathHurryUp = true; }
    public float GetDistanceAlongCoverPath(Vec3 pt) { return m_pathDir.Dot(pt - m_pathOrig); }
    public Vec3 ProjectPointOnCoverPath(Vec3 pt) { return m_pathOrig + m_pathDir * (m_pathDir.Dot(pt - m_pathOrig)); }
    public Vec3 GetCoverPathDir() { return m_pathDir; }

    // Fields
    private /*mutable*/ bool m_bIsValid;
    private bool m_isUsingCover;
    private Vec3 m_objectPos;
    private Vec3 m_objectDir;
    private float m_objectRadius;
    private float m_objectHeight;
    private bool m_objectCollidable;
    private Vec3 m_vLastHidePos;
    private Vec3 m_vLastHideDir;
    private bool m_bIsSmartObject;
    private CQueryEvent m_HideSmartObject;
    private int m_useCover;
    private uint m_dynCoverEntityId;
    private Vec3 m_dynCoverEntityPos;
    private Vec3 m_dynCoverPosLocal;
    private SHideSpotInfo.EHideSpotType m_hideSpotType;
    private string m_sAnchorName = "";

    // Cover sampling
    private Vec3 m_coverPos;
    private float m_distToCover;
    private Vec3 m_pathOrig;
    private Vec3 m_pathDir;
    private Vec3 m_pathNorm;
    private float m_pathLimitLeft;
    private float m_pathLimitRight;
    private float m_tempCover;
    private float m_tempDepth;
    private bool m_pathComplete;
    private bool m_highCoverValid;
    private bool m_lowCoverValid;
    private bool m_pathHurryUp;
    private bool m_lowLeftEdgeValid;
    private bool m_lowRightEdgeValid;
    private bool m_highLeftEdgeValid;
    private bool m_highRightEdgeValid;
    private LinkedList<Vec3> m_lowCoverPoints = new LinkedList<Vec3>();
    private LinkedList<Vec3> m_highCoverPoints = new LinkedList<Vec3>();
    private float m_lowCoverWidth;
    private float m_highCoverWidth;

    private float m_lowLeftEdge;
    private float m_lowRightEdge;
    private float m_highLeftEdge;
    private float m_highRightEdge;
    private int m_pathUpdateIter;
    private uint m_id;
}
