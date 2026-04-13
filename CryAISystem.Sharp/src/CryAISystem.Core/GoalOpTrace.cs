// Literal port of dev/Code/CryEngine/CryAISystem/GoalOpTrace.{h,cpp} (1776L + 202L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CryAISystem.CryMath;
using static CryAISystem.AISignalConstants;
using static CryAISystem.AIPhysConstants;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.NilRefHelper;
using static CryAISystem.EAIObjectType;
using static CryAISystem.GlobalFunctions;
using CryAISystem.CryCommon;

namespace CryAISystem;

// GoalOpTrace.h:24-69 — StuckDetector
public class StuckDetector
{
    public enum Status
    {
        UserIsStuck,
        UserIsMovingOnFine
    }

    public StuckDetector()
    {
        Reset();
    }

    public void Reset()
    {
        m_closest = float.MaxValue;
        m_lastProgress = gEnv.pTimer.GetCurrTime();
    }

    public Status Update(float distToEnd)
    {
        float now = gEnv.pTimer.GetCurrTime();

        if (distToEnd + 0.05f < m_closest)
        {
            m_closest = distToEnd;
            m_lastProgress = now;
        }
        else
        {
            float timeWithoutProgress = now - m_lastProgress;

            if (timeWithoutProgress > 2.0f)
            {
                return Status.UserIsStuck;
            }
        }

        return Status.UserIsMovingOnFine;
    }

    private float m_closest;
    private float m_lastProgress;
}

// GoalOpTrace.h:77-202 — COPTrace
public class COPTrace : IGoalOp
{
    //////////////////////////////////////////////////////////////////////////
    public enum EManeuver { eMV_None, eMV_Back, eMV_Fwd }
    public enum EManeuverDir { eMVD_Clockwise, eMVD_AntiClockwise }

    public EManeuver m_Maneuver;
    public EManeuverDir m_ManeuverDir;
    public ETraceEndMode m_eTraceEndMode;

    public float m_fEndAccuracy;

    public bool m_passingStraightNavSO;

    // GoalOpTrace.cpp:23-24
    private static List<PathFollowResult.SPredictedState> s_tmpPredictedStates = new List<PathFollowResult.SPredictedState>();
    private static int s_instanceCount;

    //===================================================================
    // COPTrace — GoalOpTrace.cpp:29-63
    //===================================================================
    public COPTrace(
        bool bExactFollow,
        float fEndAccuracy = 1.0f,
        bool bForceReturnPartialPath = false,
        bool bStopOnAnimationStart = false,
        ETraceEndMode eTraceEndMode = ETraceEndMode.eTEM_FixedDistance)
    {
        m_bBlock_ExecuteTrace_untilFullUpdateThenReset = false;
        m_bExactFollow = bExactFollow;
        m_fEndAccuracy = fEndAccuracy;
        m_bForceReturnPartialPath = bForceReturnPartialPath;
        m_stopOnAnimationStart = bStopOnAnimationStart;
        m_Maneuver = EManeuver.eMV_None;
        m_ManeuverDir = EManeuverDir.eMVD_Clockwise;
        m_eTraceEndMode = eTraceEndMode;
        m_ManeuverDist = 0.0f;
        m_ManeuverTime = new CTimeValue(0.0f);
        m_landHeight = 0.0f;
        m_landingDir = new Vec3(0, 0, 0);
        m_landingPos = new Vec3(0, 0, 0);
        m_workingLandingHeightOffset = 0.0f;
        m_TimeStep = 0.1f;
        m_prevFrameStartTime = new CTimeValue(-1000);
        m_fTotalTracingTime = 0.0f;
        m_lastPosition = new Vec3(0, 0, 0);
        m_fTravelDist = 0.0f;
        m_inhibitPathRegen = false;
        m_looseAttentionId = 0;
        m_bWaitingForPathResult = false;
        m_bWaitingForBusySmartObject = false;
        m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;
        m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_None;
        m_passingStraightNavSO = false;
        m_bControlSpeed = true;
        m_accumulatedFailureTime = 0.0f;
        m_earlyPathRegen = false;

        if (gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPTrace::COPTrace {0}", this);

        ++s_instanceCount;
    }

    // Copy constructor — GoalOpTrace.cpp:100-131
    public COPTrace(COPTrace rhs)
    {
        m_bBlock_ExecuteTrace_untilFullUpdateThenReset = false;
        m_bExactFollow = rhs.m_bExactFollow;
        m_fEndAccuracy = rhs.m_fEndAccuracy;
        m_bForceReturnPartialPath = rhs.m_bForceReturnPartialPath;
        m_stopOnAnimationStart = rhs.m_stopOnAnimationStart;
        m_Maneuver = EManeuver.eMV_None;
        m_ManeuverDir = EManeuverDir.eMVD_Clockwise;
        m_eTraceEndMode = ETraceEndMode.eTEM_FixedDistance;
        m_ManeuverDist = 0.0f;
        m_ManeuverTime = new CTimeValue(0.0f);
        m_landHeight = 0.0f;
        m_landingDir = new Vec3(0, 0, 0);
        m_landingPos = new Vec3(0, 0, 0);
        m_workingLandingHeightOffset = 0.0f;
        m_TimeStep = 0.1f;
        m_prevFrameStartTime = new CTimeValue(-1000);
        m_fTotalTracingTime = 0.0f;
        m_lastPosition = new Vec3(0, 0, 0);
        m_fTravelDist = 0.0f;
        m_inhibitPathRegen = false;
        m_looseAttentionId = 0;
        m_bWaitingForPathResult = false;
        m_bWaitingForBusySmartObject = false;
        m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;
        m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_None;
        m_passingStraightNavSO = false;
        m_bControlSpeed = true;
        m_accumulatedFailureTime = 0.0f;
        m_earlyPathRegen = false;

        ++s_instanceCount;
    }

    //===================================================================
    // ~COPTrace — GoalOpTrace.cpp:136-165
    //===================================================================
    ~COPTrace()
    {
        CCCPOINT("COPTrace_Destructor");

        CPipeUser pPipeUser = m_refPipeUser.GetAIObject();
        if (pPipeUser != null)
        {
            pPipeUser.CancelRequestedPath(false);
            pPipeUser.SetLooseAttentionTarget(NILREF, m_looseAttentionId);
            m_looseAttentionId = 0;

            SOBJECTSTATE state = pPipeUser.m_State;
            if (!state.continuousMotion)
                state.fDesiredSpeed = 0.0f;

            pPipeUser.ClearPath("COPTrace::~COPTrace m_Path");
        }

        m_refPipeUser.Reset();
        m_refNavTarget.Release();

        if (gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPTrace::~COPTrace {0} {1}", this, GetNameSafe(pPipeUser));

        --s_instanceCount;
        if (s_instanceCount == 0)
        {
            s_tmpPredictedStates.Clear();
            s_tmpPredictedStates.Capacity = 0;
        }
    }

    //===================================================================
    // Reset — GoalOpTrace.cpp:171-235
    //===================================================================
    public void Reset(CPipeUser pPipeUser)
    {
        CCCPOINT("COPTrace_Reset");

        if (pPipeUser != null && gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPTrace::Reset {0}", GetNameSafe(pPipeUser));

        if (pPipeUser != null)
        {
            CCCPOINT("COPTrace_Reset_A");

            pPipeUser.SetLooseAttentionTarget(NILREF, m_looseAttentionId);
            m_looseAttentionId = 0;

            if (!pPipeUser.m_Path.Empty())
            {
                pPipeUser.CancelRequestedPath(false);
            }

            pPipeUser.m_nPathDecision = PATHFINDER_ABORT;
            pPipeUser.m_State.fDesiredSpeed = 0.0f;
            pPipeUser.ClearPath("COPTrace::Reset m_Path");
        }

        m_refPipeUser.Reset();
        m_refNavTarget.Release();

        m_fTotalTracingTime = 0.0f;
        m_Maneuver = EManeuver.eMV_None;
        m_ManeuverDist = 0.0f;
        m_ManeuverTime = GetAISystem().GetFrameStartTime();
        m_landHeight = 0.0f;
        m_landingDir = new Vec3(0, 0, 0);
        m_workingLandingHeightOffset = 0.0f;
        m_inhibitPathRegen = false;
        m_bBlock_ExecuteTrace_untilFullUpdateThenReset = false;
        m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;
        m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_None;
        m_bWaitingForPathResult = false;
        m_bWaitingForBusySmartObject = false;
        m_passingStraightNavSO = false;
        m_fTravelDist = 0;

        m_TimeStep = 0.1f;
        m_prevFrameStartTime = new CTimeValue(-1000);
        m_accumulatedFailureTime = 0.0f;

        if (pPipeUser != null)
        {
            if (m_bExactFollow)
            {
                pPipeUser.m_bLooseAttention = false;
            }

            pPipeUser.ClearInvalidatedSOLinks();
        }
    }

    //===================================================================
    // Serialize — GoalOpTrace.cpp:240-275
    //===================================================================
    public void Serialize(TSerialize ser)
    {
        ser.Value("m_bBlock_ExecuteTrace_untilFullUpdateThenReset", ref m_bBlock_ExecuteTrace_untilFullUpdateThenReset);
        ser.Value("m_ManeuverDist", ref m_ManeuverDist);
        ser.Value("m_ManeuverTime", ref m_ManeuverTime);
        ser.Value("m_landHeight", ref m_landHeight);
        ser.Value("m_workingLandingHeightOffset", ref m_workingLandingHeightOffset);
        ser.Value("m_landingPos", ref m_landingPos);
        ser.Value("m_landingDir", ref m_landingDir);

        ser.Value("m_bExactFollow", ref m_bExactFollow);
        ser.Value("m_bForceReturnPartialPath", ref m_bForceReturnPartialPath);
        ser.Value("m_lastPosition", ref m_lastPosition);
        ser.Value("m_prevFrameStartTime", ref m_prevFrameStartTime);
        ser.Value("m_fTravelDist", ref m_fTravelDist);
        ser.Value("m_TimeStep", ref m_TimeStep);

        m_refPipeUser.Serialize(ser, "m_refPipeUser");

        int maneuver = (int)m_Maneuver;
        ser.Value("m_Maneuver", ref maneuver);
        m_Maneuver = (EManeuver)maneuver;

        int maneuverDir = (int)m_ManeuverDir;
        ser.Value("m_ManeuverDir", ref maneuverDir);
        m_ManeuverDir = (EManeuverDir)maneuverDir;

        ser.Value("m_fTotalTracingTime", ref m_fTotalTracingTime);
        ser.Value("m_inhibitPathRegen", ref m_inhibitPathRegen);
        ser.Value("m_fEndAccuracy", ref m_fEndAccuracy);
        ser.Value("m_looseAttentionId", ref m_looseAttentionId);

        ser.Value("m_bWaitingForPathResult", ref m_bWaitingForPathResult);
        ser.Value("m_bWaitingForBusySmartObject", ref m_bWaitingForBusySmartObject);
        ser.Value("m_passingStraightNavSO", ref m_passingStraightNavSO);

        int actorTargetReq = (int)m_actorTargetRequester;
        ser.Value("m_actorTargetRequester", ref actorTargetReq);
        m_actorTargetRequester = (ETraceActorTgtRequest)actorTargetReq;

        int pendingActorTargetReq = (int)m_pendingActorTargetRequester;
        ser.Value("m_pendingActorTargetRequester", ref pendingActorTargetReq);
        m_pendingActorTargetRequester = (ETraceActorTgtRequest)pendingActorTargetReq;

        ser.Value("m_stopOnAnimationStart", ref m_stopOnAnimationStart);

        m_refNavTarget.Serialize(ser, "m_refNavTarget");
    }

    //===================================================================
    // Execute — GoalOpTrace.cpp:281-315
    //===================================================================
    public EGoalOpResult Execute(CPipeUser pPipeUser)
    {
        CCCPOINT("COPTrace_Execute");
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        bool bTraceFinished = ExecuteTrace(pPipeUser, /* full update */ true);

        SOBJECTSTATE pipeUserState = pPipeUser.m_State;

        if (pPipeUser.m_nPathDecision == PATHFINDER_STILLFINDING)
        {
            if (bTraceFinished)
            {
                pipeUserState.fDesiredSpeed = 0.0f;
            }
            return EGoalOpResult.eGOR_IN_PROGRESS;
        }
        else
        {
            if (bTraceFinished)
            {
                // Kevin - Clean up residual data that is causing problems elsewhere
                pipeUserState.fDistanceToPathEnd = 0.0f;

                // Done tracing, allow to try to use invalid objects again.
                pPipeUser.ClearInvalidatedSOLinks();

                if (pipeUserState.curActorTargetPhase == EActorTargetPhase.eATP_Error)
                {
                    return EGoalOpResult.eGOR_FAILED;
                }
            }
            return bTraceFinished ? EGoalOpResult.eGOR_SUCCEEDED : EGoalOpResult.eGOR_IN_PROGRESS;
        }
    }

    //===================================================================
    // ExecuteTrace — GoalOpTrace.cpp:324-479
    //===================================================================
    public bool ExecuteTrace(CPipeUser pPipeUser, bool bFullUpdate)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        // The destructor needs to know the most recent pipe user
        if (m_refPipeUser.GetAIObject() != pPipeUser)
            m_refPipeUser = WeakRefHelpers.GetWeakRef(pPipeUser);

        // HACK: Special case fix for drivers in fall&play
        if (IsVehicleAndDriverIsFallen(pPipeUser))
            return false;   // Trace not finished.

        bool bTraceFinished = false;

        if (m_bWaitingForPathResult)
            if (!HandlePathResult(pPipeUser, ref bTraceFinished))
                return bTraceFinished;

        if (m_bBlock_ExecuteTrace_untilFullUpdateThenReset)
        {
            if (bFullUpdate)
            {
                Reset(pPipeUser);
            }
            else
            {
                StopMovement(pPipeUser);
            }
            return true;    // Trace finished
        }

        bool bForceRegeneratePath = false;
        bool bExactPositioning = false;

        // Handle exact positioning and vaSOs.
        if (!HandleAnimationPhase(pPipeUser, bFullUpdate, ref bForceRegeneratePath, ref bExactPositioning, ref bTraceFinished))
            return bTraceFinished;

        // On first frame:
        if (m_prevFrameStartTime.GetValue() < 0)
        {
            // Reset the action input before starting to move.
            pPipeUser.GetProxy()?.SetAGInput(AIAG_ACTION, "idle");

            // Change the SO state to match the movement.
            IEntity pEntity = pPipeUser.GetEntity();
            IEntity pNullEntity = null;
            gAIEnv.pSmartObjectManager?.SmartObjectEvent("OnMove", pEntity, pNullEntity);
        }

        CTimeValue now = GetAISystem().GetFrameStartTime();

        float ftimeStep = (float)((m_prevFrameStartTime.GetValue() > 0)
            ? (now - m_prevFrameStartTime).GetMilliSecondsAsInt64()
            : 0);
        m_prevFrameStartTime = now;

        bool isUsing3DNavigation = pPipeUser.IsUsing3DNavigation();

        if (bExactPositioning)
        {
            m_passingStraightNavSO = false;
        }
        else
        {
            CPathFollower pPathFollower = (gAIEnv.CVars.PredictivePathFollowing != 0) ? pPipeUser.GetPathFollower() as CPathFollower : null;
            float distToSmartObject = pPathFollower != null ? pPathFollower.GetDistToSmartObject() : pPipeUser.m_Path.GetDistToSmartObject(!isUsing3DNavigation);
            m_passingStraightNavSO = distToSmartObject < 1.0f;
        }

        if (bFullUpdate)
        {
            float fDistanceToPathEnd = pPipeUser.m_State.fDistanceToPathEnd;
            float fExactPosTriggerDistance = 2.5f;

            m_bWaitingForBusySmartObject = false;

            if ((fDistanceToPathEnd >= 0.0f && fDistanceToPathEnd <= fExactPosTriggerDistance) ||
                pPipeUser.m_Path.GetPathLength(!pPipeUser.IsUsing3DNavigation()) <= fExactPosTriggerDistance)
            {
                TriggerExactPositioning(pPipeUser, ref bForceRegeneratePath, ref bExactPositioning);
            }
        }

#if DEBUG
        ExecuteTraceDebugDraw(pPipeUser);
#endif

        float timeStep = max(0.0f, ftimeStep * 0.001f);
        m_fTotalTracingTime += timeStep;
        m_TimeStep = timeStep;

        // If this path was generated with the pathfinder quietly regenerate the path
        if (bForceRegeneratePath || (bFullUpdate && !m_inhibitPathRegen && !m_passingStraightNavSO && !m_bWaitingForBusySmartObject &&
            pPipeUser.m_movementAbility.pathRegenIntervalDuringTrace > 0.01f &&
            pPipeUser.m_movementAbility.pathRegenIntervalDuringTrace < m_fTotalTracingTime &&
            !pPipeUser.m_Path.GetParams().precalculatedPath &&
            !pPipeUser.m_Path.GetParams().inhibitPathRegeneration))
        {
            if ((gAIEnv.CVars.CrowdControlInPathfind != 0) || (gAIEnv.CVars.AdjustPathsAroundDynamicObstacles != 0))
            {
                RegeneratePath(pPipeUser, ref bForceRegeneratePath);
            }

            if (bForceRegeneratePath)
            {
                m_bWaitingForPathResult = true;
                return false;       // Trace not finished
            }
        }

        //////////////////////////////////////////////////////////////////////////
        // ExecuteTrace Core
        if (!m_bWaitingForPathResult && !m_bWaitingForBusySmartObject)
        {
            CPathFollower pPathFollower2 = (gAIEnv.CVars.PredictivePathFollowing != 0) ? pPipeUser.GetPathFollower() as CPathFollower : null;
            bTraceFinished = pPathFollower2 != null ? ExecutePathFollower(pPipeUser, bFullUpdate, pPathFollower2)
                                                    : isUsing3DNavigation ? Execute3D(pPipeUser, bFullUpdate) : Execute2D(pPipeUser, bFullUpdate);
        }
        //////////////////////////////////////////////////////////////////////////

        if (bExactPositioning)
        {
            bTraceFinished = false;
        }

        // prevent future updates unless we get an external reset
        if (bTraceFinished && pPipeUser.m_nPathDecision != PATHFINDER_STILLFINDING)
        {
            if (bFullUpdate)
            {
                Reset(pPipeUser);
                return true;    // Full update, trace finished
            }
            else
            {
                StopMovement(pPipeUser);
                m_bBlock_ExecuteTrace_untilFullUpdateThenReset = true;
                return false;   // Waiting for full update, trace not yet finished at the moment
            }
        }

        return bTraceFinished;
    }

    //===================================================================
    // ExecuteManeuver — GoalOpTrace.cpp:484-632
    //===================================================================
    private void ExecuteManeuver(CPipeUser pPipeUser, Vec3 steerDir)
    {
        if (fabs(pPipeUser.m_State.fDesiredSpeed) < 0.001f)
        {
            m_Maneuver = EManeuver.eMV_None;
            return;
        }

#if DEBUG
        // Update the debug movement reason.
        // pPipeUser.m_DEBUGmovementReason = CPipeUser.AIMORE_MANEUVER;
#endif

        float cosTrh = pPipeUser.m_movementAbility.maneuverTrh;
        if (pPipeUser.m_movementAbility.maneuverTrh >= 1.0f || pPipeUser.m_IsSteering)
            return;

        Vec3 myDir = pPipeUser.GetMoveDir();
        if (pPipeUser.m_State.fMovementUrgency < 0.0f)
            myDir *= -1.0f;
        myDir.z = 0.0f;
        myDir.NormalizeSafe();
        Vec3 reqDir = steerDir;
        reqDir.z = 0.0f;
        reqDir.NormalizeSafe();
        Vec3 myVel = pPipeUser.GetVelocity();
        Vec3 myPos = pPipeUser.GetPhysicsPos();

        float diffCos = reqDir.Dot(myDir);
        if (diffCos > cosTrh && m_Maneuver == EManeuver.eMV_None)
            return;

        CTimeValue now = GetAISystem().GetFrameStartTime();

        int maneuverTimeMinLimitMs = 300;
        int maneuverTimeMaxLimitMs = 5000;
        int manTimeMs = (int)(m_Maneuver != EManeuver.eMV_None ? (now - m_ManeuverTime).GetMilliSecondsAsInt64() : 0);

        float exitDiffCos = 0.98f;
        if (diffCos > exitDiffCos && m_Maneuver != EManeuver.eMV_None && manTimeMs > maneuverTimeMinLimitMs)
        {
            m_Maneuver = EManeuver.eMV_None;
            return;
        }

        // hack for instant turning — requires ViewCamera/SetRotation (Phase 11)
        // Skipped: camera visibility check and instant rotation not available in shell.

        // set the direction
        Vec3 dirCross = myDir.Cross(reqDir);
        m_ManeuverDir = dirCross.z > 0.0f ? EManeuverDir.eMVD_AntiClockwise : EManeuverDir.eMVD_Clockwise;

        bool movingFwd = myDir.Dot(myVel) > 0.0f;

        float maneuverDistLimit = 5;

        // start a new maneuver?
        if (m_Maneuver == EManeuver.eMV_None)
        {
            m_Maneuver = EManeuver.eMV_Back;
            m_ManeuverDist = 0.5f * maneuverDistLimit;
            m_ManeuverTime = GetAISystem().GetFrameStartTime();
        }
        else
        {
            Vec3 delta = myPos - m_lastPosition;
            float dist = fabs(delta.Dot(myDir));
            if (movingFwd && m_Maneuver == EManeuver.eMV_Back)
                dist = 0.0f;
            else if (!movingFwd && m_Maneuver == EManeuver.eMV_Fwd)
                dist = 0.0f;
            m_ManeuverDist += dist;

            if (manTimeMs > maneuverTimeMaxLimitMs)
            {
                m_Maneuver = m_Maneuver == EManeuver.eMV_Fwd ? EManeuver.eMV_Back : EManeuver.eMV_Fwd;
                m_ManeuverDist = 0.0f;
                m_ManeuverTime = now;
            }
            else if (m_Maneuver == EManeuver.eMV_Back)
            {
                if (fabsf(reqDir.Dot(myDir)) < cosf(DEG2RAD(85.0f)))
                {
                    m_Maneuver = EManeuver.eMV_Fwd;
                    m_ManeuverDist = 0.0f;
                    m_ManeuverTime = now;
                }
            }
            else
            {
                if (fabsf(reqDir.Dot(myDir)) > cosf(DEG2RAD(5.0f)))
                {
                    m_Maneuver = EManeuver.eMV_Back;
                    m_ManeuverDist = 0.0f;
                    m_ManeuverTime = now;
                }
            }
        }

        // now turn these into actual requests
        float normalSpeed, minSpeed, maxSpeed;
        pPipeUser.GetMovementSpeedRange(AISPEED_WALK, false, out normalSpeed, out minSpeed, out maxSpeed);

        pPipeUser.m_State.fDesiredSpeed = minSpeed;
        if (m_Maneuver == EManeuver.eMV_Back)
            pPipeUser.m_State.fDesiredSpeed = -5.0f;

        Vec3 leftDir = new Vec3(-myDir.y, myDir.x, 0.0f);

        if (m_ManeuverDir == EManeuverDir.eMVD_AntiClockwise)
            pPipeUser.m_State.vMoveDir = leftDir;
        else
            pPipeUser.m_State.vMoveDir = -leftDir;

        if (pPipeUser.m_State.fMovementUrgency < 0.0f)
            pPipeUser.m_State.vMoveDir *= -1.0f;
    }

    //===================================================================
    // ExecutePreamble — GoalOpTrace.cpp:637-670
    //===================================================================
    private bool ExecutePreamble(CPipeUser pPipeUser)
    {
        CCCPOINT("COPTrace_ExecutePreamble");
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        if (m_lastPosition.IsZero())
            m_lastPosition = pPipeUser.GetPhysicsPos();

        if (m_refNavTarget.GetAIObject() == null)
        {
            if (pPipeUser.m_Path.Empty())
            {
                pPipeUser.m_State.fDesiredSpeed = 0.0f;
                m_inhibitPathRegen = true;
            }
            else
            {
                // Obtain a NavTarget
                string name = "navTarget_" + GetNameSafe(pPipeUser);

                gAIEnv.pAIObjectManager.CreateDummyObject(m_refNavTarget, name, ESubType.STP_REFPOINT);
                m_refNavTarget.GetAIObject().SetPos(pPipeUser.GetPhysicsPos());

                m_inhibitPathRegen = false;
            }
        }
        else
        {
            m_inhibitPathRegen = false;
        }

        return m_inhibitPathRegen;
    }

    //===================================================================
    // ExecutePostamble — GoalOpTrace.cpp:675-719
    //===================================================================
    private bool ExecutePostamble(CPipeUser pPipeUser, ref bool reachedEnd, bool fullUpdate, bool b2D)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        Vec3 opPos = pPipeUser.GetPhysicsPos();
        m_fTravelDist += b2D ? Distance.Point_Point2D(opPos, m_lastPosition) : Distance.Point_Point(opPos, m_lastPosition);
        m_lastPosition = opPos;

        SOBJECTSTATE pipeUserState = pPipeUser.m_State;

        // only consider trace to be done once the agent has stopped.
        if (reachedEnd && m_fEndAccuracy >= 0.0f)
        {
            Vec3 vel = pPipeUser.GetVelocity();
            vel.z = 0.0f;
            float speed = vel.Dot(pipeUserState.vMoveDir);
            float criticalSpeed = 0.01f;
            if (speed > criticalSpeed)
            {
                if (gAIEnv.CVars.DebugPathFinding != 0)
                    AILog.AILogAlways("COPTrace reached end but waiting for speed {0:F2} to fall below {1:F2} {2}",
                        speed, criticalSpeed, GetNameSafe(pPipeUser));

                reachedEnd = false;
                pipeUserState.fDesiredSpeed = 0.0f;
                m_inhibitPathRegen = true;
            }
        }

        if (reachedEnd)
        {
            pipeUserState.fDesiredSpeed = 0.0f;
            m_inhibitPathRegen = true;
            return true;    // Trace finished
        }

        // code below here checks/handles dynamic objects
        if (pPipeUser.m_Path.GetParams().precalculatedPath)
            return false;

        return false;
    }

    //===================================================================
    // ExecutePathFollower — GoalOpTrace.cpp:724-901
    //===================================================================
    private bool ExecutePathFollower(CPipeUser pPipeUser, bool fullUpdate, CPathFollower pPathFollower)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        Debug.Assert(pPathFollower != null);
        if (pPathFollower == null)
            return true;

        if (m_TimeStep <= 0.0f)
            return false;

        if (ExecutePreamble(pPipeUser))
        {
            return true;
        }

        CCCPOINT("COPTrace_ExecutePathFollower");

        SOBJECTSTATE pipeUserState = pPipeUser.m_State;

        float fNormalSpeed, fMinSpeed, fMaxSpeed;
        pPipeUser.GetMovementSpeedRange(pipeUserState.fMovementUrgency, pipeUserState.allowStrafing,
            out fNormalSpeed, out fMinSpeed, out fMaxSpeed);

        PathFollowerParams parms = pPathFollower.GetParams();
        parms.minSpeed = fMinSpeed;
        parms.maxSpeed = fMaxSpeed;
        parms.normalSpeed = clamp_tpl(fNormalSpeed, parms.minSpeed, parms.maxSpeed);

        parms.endDistance = 0.0f;

        bool bContinueMovingAtEnd = pPipeUser.m_Path.GetParams().continueMovingAtEnd;

        if (bContinueMovingAtEnd && m_pendingActorTargetRequester == ETraceActorTgtRequest.eTATR_None)
        {
            CAIObject pPathFindTarget = pPipeUser.m_refPathFindTarget.GetAIObject();
            if (pPathFindTarget != null && (pPathFindTarget.GetSubType() == ESubType.STP_FORMATION))
            {
                parms.endDistance = 1.0f;
            }
        }

        parms.maxAccel = pPipeUser.m_movementAbility.maxAccel;
        parms.maxDecel = pPipeUser.m_movementAbility.maxDecel;
        parms.stopAtEnd = !bContinueMovingAtEnd;
        parms.isAllowedToShortcut = true;

        PathFollowResult result = new PathFollowResult();

        List<PathFollowResult.SPredictedState> predictedStates = s_tmpPredictedStates;

        bool highPriority;
        CPuppet pPuppet = pPipeUser.CastToCPuppet();
        if (pPuppet != null)
        {
            EPuppetUpdatePriority ePuppetUpdatePriority = pPuppet.GetUpdatePriority();
            highPriority = (ePuppetUpdatePriority == EPuppetUpdatePriority.AIPUP_VERY_HIGH) || (ePuppetUpdatePriority == EPuppetUpdatePriority.AIPUP_HIGH);
        }
        else
        {
            highPriority = true;
        }

        float PREDICTION_DELTA_TIME = 0.1f;
        float PREDICTION_TIME = 1.0f;

        if (highPriority)
        {
            result.desiredPredictionTime = PREDICTION_TIME;
            int count = (int)(PREDICTION_TIME / PREDICTION_DELTA_TIME + 0.5f);
            predictedStates.Clear();
            for (int i = 0; i < count; i++) predictedStates.Add(new PathFollowResult.SPredictedState());
            result.predictedStates = predictedStates;
        }
        else
        {
            result.desiredPredictionTime = 0.0f;
            predictedStates.Clear();
            result.predictedStates = null;
        }

        result.predictionDeltaTime = PREDICTION_DELTA_TIME;

        Vec3 curPos = pPipeUser.GetPhysicsPos();

        // If there's an animation in progress (typically SO playing)
        bool runningSO = false;
        if (m_actorTargetRequester == ETraceActorTgtRequest.eTATR_NavSO)
        {
            m_stuckDetector.Reset();

            switch (pipeUserState.curActorTargetPhase)
            {
            case EActorTargetPhase.eATP_Playing:
            case EActorTargetPhase.eATP_Finished:
            case EActorTargetPhase.eATP_StartedAndFinished:
                Debug.Assert(!pipeUserState.curActorTargetFinishPos.IsZero());
                curPos = pipeUserState.curActorTargetFinishPos;
                runningSO = true;
                break;
            }
        }

        Vec3 curVel = pPipeUser.GetVelocity();

        bool targetReachable = pPathFollower.Update(result, curPos, curVel, m_TimeStep);

        float distToEnd = pPathFollower.GetDistToEnd(curPos);

        if (targetReachable)
        {
            Vec3 desiredMoveDir = result.velocityOut;
            float desiredSpeed = desiredMoveDir.NormalizeSafe();

            pipeUserState.fDesiredSpeed = desiredSpeed;
            pipeUserState.vMoveDir = desiredMoveDir;
            pipeUserState.fDistanceToPathEnd = distToEnd;

            int num = min(predictedStates.Count, SAIPredictedCharacterStates.maxStates);
            pipeUserState.predictedCharacterStates.nStates = num;
            for (int i = 0; i < num; ++i)
            {
                PathFollowResult.SPredictedState state = predictedStates[i];
                pipeUserState.predictedCharacterStates.states[i].Set(state.pos, state.vel, (1 + i) * PREDICTION_DELTA_TIME);
            }

            pipeUserState.vMoveTarget = result.followTargetPos;
            pipeUserState.vInflectionPoint = result.inflectionPoint;

            bool reachedEnd = result.reachedEnd;
            return ExecutePostamble(pPipeUser, ref reachedEnd, fullUpdate, parms.use2D);
        }
        else
        {
            if (!runningSO) // do not regenerate path while running SO
            {
                m_accumulatedFailureTime += gEnv.pTimer.GetFrameTime();

                if (m_accumulatedFailureTime > 0.5f)
                {
                    m_accumulatedFailureTime = 0.0f;

                    bool forceRegeneratePath = true;
                    RegeneratePath(pPipeUser, ref forceRegeneratePath);
                    m_bWaitingForPathResult = true;
                }
            }

            return false; // StillTracing
        }
    }

    //====================================================================
    // Execute2D — GoalOpTrace.cpp:906-1113
    //====================================================================
    private bool Execute2D(CPipeUser pPipeUser, bool fullUpdate)
    {
        if (ExecutePreamble(pPipeUser))
            return true;

        // input
        Vec3 fwdDir = pPipeUser.GetMoveDir();
        if (pPipeUser.m_State.fMovementUrgency < 0.0f)
            fwdDir *= -1.0f;
        Vec3 opPos = pPipeUser.GetPhysicsPos();
        pe_status_dynamics dSt = new pe_status_dynamics();
        pPipeUser.GetPhysics()?.GetStatus(dSt);
        Vec3 opVel = m_Maneuver == EManeuver.eMV_None ? dSt.v : fwdDir * 5.0f;
        float lookAhead = pPipeUser.m_movementAbility.pathLookAhead;
        float pathRadius = pPipeUser.m_movementAbility.pathRadius;
        bool resolveSticking = pPipeUser.m_movementAbility.resolveStickingInTrace;

        // output
        Vec3 steerDir;
        float distToEnd;
        float distToPath;
        Vec3 pathDir;
        Vec3 pathAheadDir;
        Vec3 pathAheadPos;

        bool isResolvingSticking;

        bool stillTracingPath = pPipeUser.m_Path.UpdateAndSteerAlongPath(
            out steerDir, out distToEnd, out distToPath, out isResolvingSticking,
            out pathDir, out pathAheadDir, out pathAheadPos,
            opPos, opVel, lookAhead, pathRadius, m_TimeStep, resolveSticking, true);
        pPipeUser.m_State.fDistanceToPathEnd = max(0.0f, distToEnd);
        Vec3 dirOffPath;
        pPipeUser.m_Path.GetDirectionToPathFromPoint(opPos, out dirOffPath);
        pPipeUser.m_State.vDirOffPath = dirOffPath;

        pathAheadDir.z = 0.0f;
        pathAheadDir.NormalizeSafe();
        Vec3 steerDir2D = new Vec3(steerDir);
        steerDir2D.z = 0.0f;
        steerDir2D.NormalizeSafe();

#if DEBUG
        // pPipeUser.m_DEBUGmovementReason = CPipeUser.AIMORE_TRACE;
#endif

        distToEnd -= -pPipeUser.m_Path.GetDiscardedPathLength();
        bool reachedEnd = false;
        if (stillTracingPath && distToEnd > 0.1f)
        {
            Vec3 targetPos;
            if (m_refNavTarget.GetAIObject() != null && pPipeUser.m_Path.GetPosAlongPath(out targetPos, lookAhead, true, true))
                m_refNavTarget.GetAIObject().SetPos(targetPos);

            //turning maneuvering
            bool doManeuver = (gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_CRYSIS2);
            if (doManeuver)
                ExecuteManeuver(pPipeUser, steerDir);

            if (m_Maneuver != EManeuver.eMV_None)
            {
                Vec3 curPos = pPipeUser.GetPhysicsPos();
                m_fTravelDist += Distance.Point_Point2D(curPos, m_lastPosition);
                m_lastPosition = curPos;
                // prevent path regen
                m_fTotalTracingTime = 0.0f;
                return false;
            }

            float normalSpeed, minSpeed2, maxSpeed;
            pPipeUser.GetMovementSpeedRange(pPipeUser.m_State.fMovementUrgency, pPipeUser.m_State.allowStrafing, out normalSpeed, out minSpeed2, out maxSpeed);

            float dirSpeedMod = 1.0f;
            float curveSpeedMod = 1.0f;
            float endSpeedMod = 1.0f;
            float slopeMod = 1.0f;
            float moveDirMod = 1.0f;

            // speed falloff
            if (pPipeUser.GetType() == (ushort)AIOBJECT_VEHICLE)
            {
                float offset = 1.0f;
                float velFalloff = offset * pathAheadDir.Dot(fwdDir);
                float velFalloffD = 1 - velFalloff;
                if (velFalloffD > 0.0f && pPipeUser.m_movementAbility.velDecay > 0.0f)
                    dirSpeedMod = velFalloff / (velFalloffD * pPipeUser.m_movementAbility.velDecay);
            }

            // slow down due to the path curvature
            float lookAheadForSpeedControl;
            if (pPipeUser.m_movementAbility.pathSpeedLookAheadPerSpeed < 0.0f)
                lookAheadForSpeedControl = -pPipeUser.m_movementAbility.pathSpeedLookAheadPerSpeed * lookAhead * pPipeUser.m_State.fMovementUrgency;
            else
                lookAheadForSpeedControl = pPipeUser.m_movementAbility.pathSpeedLookAheadPerSpeed * pPipeUser.GetVelocity().GetLength();

            if (lookAheadForSpeedControl > 0.0f)
            {
                Vec3 pos, dir;
                float lowestPathDot = 0.0f;
                bool curveOK = pPipeUser.m_Path.GetPathPropertiesAhead(lookAheadForSpeedControl, true, out pos, out dir, 0, ref lowestPathDot, true);
                Vec3 thisPathSegDir = (pPipeUser.m_Path.GetNextPathPoint().vPos - pPipeUser.m_Path.GetPrevPathPoint().vPos);
                thisPathSegDir.z = 0.0f;
                thisPathSegDir.NormalizeSafe();
                float thisDot = thisPathSegDir.Dot(steerDir2D);
                if (thisDot < lowestPathDot)
                    lowestPathDot = thisDot;
                if (curveOK)
                {
                    float a = 1.0f - 2.0f * pPipeUser.m_movementAbility.cornerSlowDown;
                    float b = 1.0f - a;
                    curveSpeedMod = a + b * lowestPathDot;
                }

                // slow down at end
                if (m_fEndAccuracy >= 0.0f && m_eTraceEndMode != ETraceEndMode.eTEM_MinimumDistance)
                {
                    float slowDownDistScale = 2.0f;
                    float minEndSpeedMod = 0.1f;
                    float slowDownDist = slowDownDistScale * lookAheadForSpeedControl;
                    float workingDistToEnd = m_fEndAccuracy + distToEnd - 0.2f * lookAheadForSpeedControl;
                    if (slowDownDist > 0.1f && workingDistToEnd < slowDownDist)
                    {
                        endSpeedMod = workingDistToEnd / slowDownDist;
                        Limit(ref endSpeedMod, minEndSpeedMod, 1.0f);
                    }
                }
            }

            float slopeModCoeff = pPipeUser.m_movementAbility.slopeSlowDown;
            // slow down when going down steep slopes
            int buildingID = -1;
            if ((slopeModCoeff > 0) &&
                (gAIEnv.pNavigation.CheckNavigationType(pPipeUser.GetPos(), ref buildingID,
                pPipeUser.GetMovementAbility().pathfindingProperties.navCapMask) == IAISystem_ENavigationType.NAV_WAYPOINT_HUMAN))
            {
                float slowDownSlope = 0.5f;
                float pathHorDist = steerDir.GetLength2D();
                if (pathHorDist > 0.0f && steerDir.z < 0.0f)
                {
                    float slope = -steerDir.z / pathHorDist * slopeModCoeff;
                    slopeMod = 1.0f - slope / slowDownSlope;
                    float minSlopeMod = 0.5f;
                    Limit(ref slopeMod, minSlopeMod, 1.0f);
                }
            }

            // slow down when going up steep slopes
            if (slopeModCoeff > 0)
            {
                IPhysicalEntity pPhysics = pPipeUser.GetPhysics();
                pe_status_living status = new pe_status_living();
                int valid = pPhysics != null ? pPhysics.GetStatus(status) : 0;
                if (valid != 0)
                {
                    if (status.bFlying == 0)
                    {
                        Vec3 sideDir = new Vec3(-steerDir2D.y, steerDir2D.x, 0.0f);
                        Vec3 slopeN = status.groundSlope - status.groundSlope.Dot(sideDir) * sideDir;
                        slopeN.NormalizeSafe();
                        float d = steerDir2D.Dot(status.groundSlope);
                        Limit(ref d, -0.99f, 0.99f);
                        float uphillSlopeMod = (1 + d / (1.0f - square(d))) * slopeModCoeff;
                        float minUphillSlopeMod = 0.5f;
                        if (uphillSlopeMod < minUphillSlopeMod)
                            uphillSlopeMod = minUphillSlopeMod;
                        if (uphillSlopeMod < 1.0f)
                            slopeMod = min(slopeMod, uphillSlopeMod);
                    }
                }
            }

            float maxMod = min(min(min(min(dirSpeedMod, curveSpeedMod), endSpeedMod), slopeMod), moveDirMod);
            Limit(ref maxMod, 0.0f, 1.0f);

            if (m_bControlSpeed == true)
            {
                float newDesiredSpeed = (1.0f - maxMod) * minSpeed2 + maxMod * normalSpeed;

                float change = newDesiredSpeed - pPipeUser.m_State.fDesiredSpeed;
                if (change > m_TimeStep * pPipeUser.m_movementAbility.maxAccel)
                    change = m_TimeStep * pPipeUser.m_movementAbility.maxAccel;
                else if (change < -m_TimeStep * pPipeUser.m_movementAbility.maxDecel)
                    change = -m_TimeStep * pPipeUser.m_movementAbility.maxDecel;
                pPipeUser.m_State.fDesiredSpeed += change;
            }

            pPipeUser.m_State.vMoveDir = steerDir2D;
            if (pPipeUser.m_State.fMovementUrgency < 0.0f)
                pPipeUser.m_State.vMoveDir *= -1.0f;

            pPipeUser.m_State.predictedCharacterStates.nStates = 0;
        }
        else
        {
            reachedEnd = true;
        }

        return ExecutePostamble(pPipeUser, ref reachedEnd, fullUpdate, true);
    }

    //====================================================================
    // Execute3D — GoalOpTrace.cpp:1118-1285
    //====================================================================
    private bool Execute3D(CPipeUser pPipeUser, bool fullUpdate)
    {
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        if (ExecutePreamble(pPipeUser))
            return true;

        if (fullUpdate)
        {
            if (pPipeUser.GetType() == (ushort)AIOBJECT_VEHICLE && m_fEndAccuracy == 0.0f && pPipeUser.m_Path.GetPath().Count > 0)
            {
                Vec3 endPt = pPipeUser.m_Path.GetPath()[pPipeUser.m_Path.GetPath().Count - 1].vPos;
                bool gotFloor = AICollision.GetFloorPos(ref m_landingPos, endPt, 0.5f, 1.0f, 1.0f, CryCommon.EAICollisionEntities.AICE_STATIC);
                if (gotFloor)
                    m_landHeight = 2.0f;
                else
                    m_landHeight = 0.0f;
                if (m_workingLandingHeightOffset > 0.0f)
                    m_inhibitPathRegen = true;
            }
            else
            {
                m_landHeight = 0.0f;
                m_inhibitPathRegen = false;
            }
        }

        // input
        Vec3 opPos = pPipeUser.GetPhysicsPos();
        Vec3 fakeOpPos = opPos;
        fakeOpPos.z -= m_workingLandingHeightOffset;
        if (pPipeUser.m_IsSteering)
            fakeOpPos.z -= pPipeUser.m_flightSteeringZOffset;

        pe_status_dynamics dSt = new pe_status_dynamics();
        pPipeUser.GetPhysics()?.GetStatus(dSt);
        Vec3 opVel = dSt.v;
        float lookAhead = pPipeUser.m_movementAbility.pathLookAhead;
        float pathRadius = pPipeUser.m_movementAbility.pathRadius;
        bool resolveSticking = pPipeUser.m_movementAbility.resolveStickingInTrace;

        Vec3 steerDir;
        float distToEnd;
        float distToPath;
        Vec3 pathDir;
        Vec3 pathAheadDir;
        Vec3 pathAheadPos;

        bool isResolvingSticking;

        bool stillTracingPath = pPipeUser.m_Path.UpdateAndSteerAlongPath(out steerDir, out distToEnd, out distToPath, out isResolvingSticking,
            out pathDir, out pathAheadDir, out pathAheadPos,
            fakeOpPos, opVel, lookAhead, pathRadius, m_TimeStep, resolveSticking, false);
        pPipeUser.m_State.fDistanceToPathEnd = max(0.0f, distToEnd);

#if DEBUG
        // pPipeUser.m_DEBUGmovementReason = CPipeUser.AIMORE_TRACE;
#endif

        distToEnd -= distToPath;
        distToEnd -= m_landHeight * 2.0f;
        if (distToEnd < 0.0f)
            stillTracingPath = false;

        if (!stillTracingPath && m_landHeight > 0.0f)
        {
            return ExecuteLanding(pPipeUser, m_landingPos);
        }

        distToEnd -= -pPipeUser.m_Path.GetDiscardedPathLength();
        bool reachedEnd = !stillTracingPath;
        if (stillTracingPath && distToEnd > 0.0f)
        {
            Vec3 targetPos;
            if (m_refNavTarget.GetAIObject() != null && pPipeUser.m_Path.GetPosAlongPath(out targetPos, lookAhead, true, true))
                m_refNavTarget.GetAIObject().SetPos(targetPos);

            float normalSpeed, minSpeed2, maxSpeed;
            pPipeUser.GetMovementSpeedRange(pPipeUser.m_State.fMovementUrgency, pPipeUser.m_State.allowStrafing, out normalSpeed, out minSpeed2, out maxSpeed);

            float dirSpeedMod = 1.0f;
            float curveSpeedMod = 1.0f;
            float endSpeedMod = 1.0f;
            float moveDirMod = 1.0f;

            float lookAheadForSpeedControl;
            if (pPipeUser.m_movementAbility.pathSpeedLookAheadPerSpeed < 0.0f)
                lookAheadForSpeedControl = lookAhead * pPipeUser.m_State.fMovementUrgency;
            else
                lookAheadForSpeedControl = pPipeUser.m_movementAbility.pathSpeedLookAheadPerSpeed * pPipeUser.GetVelocity().GetLength();

            lookAheadForSpeedControl -= distToPath;
            if (lookAheadForSpeedControl < 0.0f)
                lookAheadForSpeedControl = 0.0f;

            if (lookAheadForSpeedControl > 0.0f)
            {
                Vec3 pos, dir;
                float lowestPathDot = 0.0f;
                bool curveOK = pPipeUser.m_Path.GetPathPropertiesAhead(lookAheadForSpeedControl, true, out pos, out dir, 0, ref lowestPathDot, true);
                Vec3 thisPathSegDir = (pPipeUser.m_Path.GetNextPathPoint().vPos - pPipeUser.m_Path.GetPrevPathPoint().vPos).GetNormalizedSafe();
                float thisDot = thisPathSegDir.Dot(steerDir);
                if (thisDot < lowestPathDot)
                    lowestPathDot = thisDot;
                if (curveOK)
                {
                    float a = 1.0f - 2.0f * pPipeUser.m_movementAbility.cornerSlowDown;
                    float b = 1.0f - a;
                    curveSpeedMod = a + b * lowestPathDot;
                }
            }

            // slow down at end
            if (m_fEndAccuracy >= 0.0f)
            {
                float slowDownDistScale = 1.0f;
                float slowDownDist = slowDownDistScale * lookAheadForSpeedControl;
                float workingDistToEnd = m_fEndAccuracy + distToEnd - 0.2f * lookAheadForSpeedControl;
                if (slowDownDist > 0.1f && workingDistToEnd < slowDownDist)
                {
                    minSpeed2 *= 0.1f;
                    endSpeedMod = workingDistToEnd / slowDownDist;
                    Limit(ref endSpeedMod, 0.0f, 1.0f);
                    m_workingLandingHeightOffset = (1.0f - endSpeedMod) * m_landHeight;
                }
                else
                {
                    m_workingLandingHeightOffset = 0.0f;
                }
            }

            float maxMod = min(min(min(dirSpeedMod, curveSpeedMod), endSpeedMod), moveDirMod);
            Limit(ref maxMod, 0.0f, 1.0f);

            float newDesiredSpeed = (1.0f - maxMod) * minSpeed2 + maxMod * normalSpeed;
            float change = newDesiredSpeed - pPipeUser.m_State.fDesiredSpeed;
            if (change > m_TimeStep * pPipeUser.m_movementAbility.maxAccel)
                change = m_TimeStep * pPipeUser.m_movementAbility.maxAccel;
            else if (change < -m_TimeStep * pPipeUser.m_movementAbility.maxDecel)
                change = -m_TimeStep * pPipeUser.m_movementAbility.maxDecel;
            pPipeUser.m_State.fDesiredSpeed += change;

            pPipeUser.m_State.vMoveDir = steerDir;
            if (pPipeUser.m_State.fMovementUrgency < 0.0f)
                pPipeUser.m_State.vMoveDir *= -1.0f;

            pPipeUser.m_State.predictedCharacterStates.nStates = 0;
        }
        else
        {
            reachedEnd = true;
        }

        return ExecutePostamble(pPipeUser, ref reachedEnd, fullUpdate, false);
    }

    //===================================================================
    // ExecuteLanding — GoalOpTrace.cpp:1290-1340
    //===================================================================
    private bool ExecuteLanding(CPipeUser pPipeUser, Vec3 pathEnd)
    {
        m_inhibitPathRegen = true;
        float normalSpeed, minSpeed, maxSpeed;
        pPipeUser.GetMovementSpeedRange(pPipeUser.m_State.fMovementUrgency, false, out normalSpeed, out minSpeed, out maxSpeed);
        Vec3 opPos = pPipeUser.GetPhysicsPos();

        Vec3 horMoveDir = pathEnd - opPos;
        horMoveDir.z = 0.0f;
        float error = horMoveDir.NormalizeSafe();

        Limit(ref error, 0.0f, 1.0f);
        float horSpeed = 0.3f * minSpeed * error;
        float verSpeed = 1.0f;

        pPipeUser.m_State.vMoveDir = horSpeed * horMoveDir - new Vec3(0, 0, verSpeed);
        pPipeUser.m_State.vMoveDir.NormalizeSafe();
        pPipeUser.m_State.fDesiredSpeed = sqrtf(square(horSpeed) + square(verSpeed));

        if (pPipeUser.m_State.fMovementUrgency < 0.0f)
            pPipeUser.m_State.vMoveDir *= -1.0f;

        // set look dir
        if (m_landingDir.IsZero())
        {
            if (gAIEnv.CVars.DebugPathFinding != 0)
                AILog.AILogAlways("COPTrace::ExecuteLanding starting final landing {0}", GetNameSafe(pPipeUser));

            m_landingDir = pPipeUser.GetMoveDir();
            m_landingDir.z = 0.0f;
            m_landingDir.NormalizeSafe(Vec3Constants.fVec3_OneX);
        }
        Vec3 navTargetPos = opPos + 100.0f * m_landingDir;
        m_refNavTarget.GetAIObject()?.SetPos(navTargetPos);

        if (!pPipeUser.m_bLooseAttention)
        {
            m_looseAttentionId = pPipeUser.SetLooseAttentionTarget(m_refNavTarget.GetAIObject() != null ? WeakRefHelpers.GetWeakRef(m_refNavTarget.GetAIObject()) : NILREF);
        }

        // check for collision — pe_status_collisions not ported yet; simplified
        // In C++ this checks `pPipeUser->GetPhysics()->GetStatus(&stat)` where stat is pe_status_collisions.
        // Since pe_status_collisions is not ported, we return false (not landed).
        return false;
    }

    //===================================================================
    // DebugDraw — GoalOpTrace.cpp:1345-1353
    //===================================================================
    public void DebugDraw(CPipeUser pPipeUser)
    {
        if (IsPathRegenerationInhibited())
        {
            CDebugDrawContext dc = new CDebugDrawContext();
            dc.Draw3dLabel(pPipeUser.GetPhysicsPos(), 1.5f, "PATH LOCKED\n{0} {1}",
                m_inhibitPathRegen ? "Inhibit" : "", m_passingStraightNavSO ? "NavSO" : "");
        }
    }

    //===================================================================
    // ExecuteTraceDebugDraw — GoalOpTrace.cpp:1355-1372
    //===================================================================
    private void ExecuteTraceDebugDraw(CPipeUser pPipeUser)
    {
        if (gAIEnv.CVars.DebugPathFinding != 0)
        {
            IAIDebugRenderer pRenderer = gAIEnv.GetDebugRenderer();
            if (pRenderer != null)
            {
                if (m_bWaitingForPathResult)
                {
                    pRenderer.DrawSphere(pPipeUser.GetPos() + new Vec3(0, 0, 1.0f), 0.5f, new ColorB(255, 255, 0));
                }

                if (m_bWaitingForBusySmartObject)
                {
                    pRenderer.DrawSphere(pPipeUser.GetPos() + new Vec3(0, 0, 1.2f), 0.5f, new ColorB(255, 0, 0));
                }
            }
        }
    }

    //===================================================================
    // HandleAnimationPhase — GoalOpTrace.cpp:1374-1521
    //===================================================================
    private bool HandleAnimationPhase(CPipeUser pPipeUser, bool bFullUpdate, ref bool bForceRegeneratePath, ref bool bExactPositioning, ref bool bTraceFinished)
    {
        SOBJECTSTATE pipeUserState = pPipeUser.m_State;

        switch (pipeUserState.curActorTargetPhase)
        {
        case EActorTargetPhase.eATP_Error:
            {
                if (m_actorTargetRequester == ETraceActorTgtRequest.eTATR_None)
                {
                    m_actorTargetRequester = m_pendingActorTargetRequester;
                    m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_None;
                }

                switch (m_actorTargetRequester)
                {
                case ETraceActorTgtRequest.eTATR_EndOfPath:

                    if (gAIEnv.CVars.DebugPathFinding != 0)
                        AILog.AILogAlways("COPTrace::ExecuteTrace resetting since error occurred during exact positioning {0}", GetNameSafe(pPipeUser));

                    if (bFullUpdate)
                    {
                        Reset(pPipeUser);
                    }
                    else
                    {
                        StopMovement(pPipeUser);
                        m_bBlock_ExecuteTrace_untilFullUpdateThenReset = true;
                    }
                    bTraceFinished = true;
                    return false;

                case ETraceActorTgtRequest.eTATR_NavSO:
                    bForceRegeneratePath = true;
                    m_inhibitPathRegen = false;
                    break;
                }

                m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;
                m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_None;
            }
            break;

        case EActorTargetPhase.eATP_Waiting:
            bExactPositioning = true;
            break;

        case EActorTargetPhase.eATP_Playing:
            bExactPositioning = true;
            pPipeUser.m_Path.ResurrectRemainingPath();
            break;

        case EActorTargetPhase.eATP_Starting:
        case EActorTargetPhase.eATP_Started:
            bExactPositioning = true;

            if (m_pendingActorTargetRequester != ETraceActorTgtRequest.eTATR_None)
            {
                m_actorTargetRequester = m_pendingActorTargetRequester;
                m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_None;
            }

            if (m_stopOnAnimationStart && m_actorTargetRequester == ETraceActorTgtRequest.eTATR_EndOfPath)
            {
                if (bFullUpdate)
                {
                    Reset(pPipeUser);
                }
                else
                {
                    StopMovement(pPipeUser);
                    m_bBlock_ExecuteTrace_untilFullUpdateThenReset = true;
                }
                bTraceFinished = true;
                return false;
            }
            break;

        case EActorTargetPhase.eATP_Finished:
        case EActorTargetPhase.eATP_StartedAndFinished:
            switch (m_actorTargetRequester)
            {
            case ETraceActorTgtRequest.eTATR_EndOfPath:
                m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;

                if (gAIEnv.CVars.DebugPathFinding != 0)
                    AILog.AILogAlways("COPTrace::ExecuteTrace resetting since exact position reached/animation finished {0}", GetNameSafe(pPipeUser));

                if (bFullUpdate)
                {
                    Reset(pPipeUser);
                }
                else
                {
                    StopMovement(pPipeUser);
                    m_bBlock_ExecuteTrace_untilFullUpdateThenReset = true;
                }
                bTraceFinished = true;
                return false;

            case ETraceActorTgtRequest.eTATR_NavSO:
                {
                    pPipeUser.m_State.fDistanceToPathEnd = pPipeUser.m_Path.GetDiscardedPathLength();
                    pPipeUser.m_Path.ResurrectRemainingPath();
                    pPipeUser.m_Path.PrepareNavigationalSmartObjectsForMNM(pPipeUser);
                    pPipeUser.AdjustPath();
                    bForceRegeneratePath = false;

                    m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;
                    m_prevFrameStartTime = new CTimeValue(0);

                    Vec3 opPos = pPipeUser.GetPhysicsPos();
                    m_fTravelDist += !pPipeUser.IsUsing3DNavigation() ?
                                Distance.Point_Point2D(opPos, m_lastPosition) : Distance.Point_Point(opPos, m_lastPosition);
                    m_lastPosition = opPos;

                    m_bWaitingForPathResult = false;
                    m_inhibitPathRegen = true;
                }
                break;

            default:
                bForceRegeneratePath = true;
                m_actorTargetRequester = ETraceActorTgtRequest.eTATR_None;
                m_bWaitingForPathResult = true;
                m_bWaitingForBusySmartObject = false;
                m_inhibitPathRegen = false;
                break;
            }
            break;
        }

        return true;
    }

    //===================================================================
    // HandlePathResult — GoalOpTrace.cpp:1524-1546
    //===================================================================
    private bool HandlePathResult(CPipeUser pPipeUser, ref bool bReturnValue)
    {
        switch (pPipeUser.m_nPathDecision)
        {
        case PATHFINDER_PATHFOUND:
            m_bWaitingForPathResult = false;
            return true;

        case PATHFINDER_NOPATH:
            m_bWaitingForPathResult = false;
            m_bBlock_ExecuteTrace_untilFullUpdateThenReset = true;
            bReturnValue = true;
            return false;

        default:
            StopMovement(pPipeUser);
            bReturnValue = false;
            return false;
        }
    }

    //===================================================================
    // IsVehicleAndDriverIsFallen — GoalOpTrace.cpp:1549-1578
    //===================================================================
    private bool IsVehicleAndDriverIsFallen(CPipeUser pPipeUser)
    {
        if (pPipeUser.GetType() == (ushort)AIOBJECT_VEHICLE)
        {
            uint driverId = pPipeUser.GetProxy()?.GetLinkedDriverEntityId() ?? 0;
            if (driverId != 0)
            {
                IEntity pDriverEntity = gEnv.pEntitySystem?.GetEntity(driverId);
                if (pDriverEntity != null)
                {
                    IAIObject pDriverAI = pDriverEntity.GetAI();
                    if (pDriverAI != null)
                    {
                        CAIActor pDriverActor = pDriverAI.CastToCAIActor();
                        if (pDriverActor != null)
                        {
                            IAIActorProxy pDriverProxy = pDriverActor.GetProxy();
                            if (pDriverProxy != null)
                            {
                                if (pDriverProxy.GetActorIsFallen())
                                {
                                    StopMovement(pPipeUser);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    //===================================================================
    // RegeneratePath — GoalOpTrace.cpp:1581-1584
    //===================================================================
    private void RegeneratePath(CPipeUser pPipeUser, ref bool bForceRegeneratePath)
    {
        GetAISystem()?.LogComment("COPTrace::RegeneratePath", "Currently regenerate the path in the GoalOp trace is not supported by the MNM Navigation System.");
    }

    //===================================================================
    // StopMovement — GoalOpTrace.cpp:1587-1594
    //===================================================================
    private void StopMovement(CPipeUser pPipeUser)
    {
        SOBJECTSTATE pipeUserState = pPipeUser.m_State;

        pipeUserState.vMoveDir = new Vec3(0, 0, 0);
        pipeUserState.fDesiredSpeed = 0.0f;
        pipeUserState.predictedCharacterStates.nStates = 0;
    }

    //===================================================================
    // TriggerExactPositioning — GoalOpTrace.cpp:1597-1762
    //===================================================================
    private void TriggerExactPositioning(CPipeUser pPipeUser, ref bool bForceRegeneratePath, ref bool bExactPositioning)
    {
        SOBJECTSTATE pipeUserState = pPipeUser.m_State;
        SAIActorTargetRequest pipeUserActorTargetRequest = pipeUserState.actorTargetReq;

        switch (pipeUserState.curActorTargetPhase)
        {
        case EActorTargetPhase.eATP_None:
        {
            if (gAIEnv.CVars.DebugPathFinding != 0)
            {
                SNavSOStates pipeUserPendingNavSOStates = pPipeUser.m_pendingNavSOStates;
                if (!pipeUserPendingNavSOStates.IsEmpty())
                {
                    IEntity pEntity = gEnv.pEntitySystem?.GetEntity(pipeUserPendingNavSOStates.objectEntId);
                    if (pEntity != null)
                    {
                        AILog.AILogAlways("COPTrace::ExecuteTrace {0} trying to use exact positioning while a navSO (entity={1}) is still active.",
                            GetNameSafe(pPipeUser), pEntity.GetName());
                    }
                    else
                    {
                        AILog.AILogAlways("COPTrace::ExecuteTrace {0} trying to use exact positioning while a navSO (entityId={1}) is still active.",
                            GetNameSafe(pPipeUser), pipeUserPendingNavSOStates.objectEntId);
                    }
                }
            }

            // Handle the exact positioning request
            PathPointDescriptor.OffMeshLinkData pSmartObjectMNMData = pPipeUser.m_Path.GetLastPathPointMNNSOData();
            bool smartObject = pSmartObjectMNMData != null ? (pSmartObjectMNMData.meshID != 0 && pSmartObjectMNMData.offMeshLinkID != 0) : false;

            if (smartObject)
            {
                // Smart object exact positioning — delegates to SmartObjectManager (Phase 11)
                // Full implementation requires OffMeshNavigation linkage.
                // Shell: attempt PrepareNavigateSmartObject or mark as waiting.
                m_bWaitingForBusySmartObject = true;
            }
            else
            {
                SAIActorTargetRequest pActiveActorTargetRequest = pPipeUser.GetActiveActorTargetRequest();
                if (pActiveActorTargetRequest != null)
                {
                    // Actor target requested at the end of the path.
                    pipeUserActorTargetRequest.approachLocation = pActiveActorTargetRequest.approachLocation;
                    pipeUserActorTargetRequest.approachDirection = pActiveActorTargetRequest.approachDirection;
                    pipeUserActorTargetRequest.animation = pActiveActorTargetRequest.animation;
                    pipeUserActorTargetRequest.vehicleName = pActiveActorTargetRequest.vehicleName;
                    pipeUserActorTargetRequest.vehicleSeat = pActiveActorTargetRequest.vehicleSeat;
                    pipeUserActorTargetRequest.id = ++pPipeUser.m_actorTargetReqId;
                    pipeUserActorTargetRequest.lowerPrecision = false;
                    m_pendingActorTargetRequester = ETraceActorTgtRequest.eTATR_EndOfPath;

                    bExactPositioning = true;

                    pPipeUser.m_Path.GetParams().inhibitPathRegeneration = true;
                    pPipeUser.CancelRequestedPath(false);

                    // #ifdef _DEBUG
                    // pPipeUser.m_DEBUGCanTargetPointBeReached.clear();
                    // pPipeUser.m_DEBUGUseTargetPointRequest.zero();
                    // #endif
                }
            }
            break;
        }

        case EActorTargetPhase.eATP_Error:
            break;

        default:
            bExactPositioning = true;
            break;
        }
    }

    //===================================================================
    // Teleport — GoalOpTrace.cpp:1764-1777
    //===================================================================
    private void Teleport(CPipeUser pipeUser, Vec3 teleportDestination)
    {
        IEntity entity = pipeUser.GetEntity();
        if (entity != null)
        {
            Matrix34 transform = entity.GetWorldTM();
            transform.SetTranslation(teleportDestination);
            entity.SetWorldTM(transform);
            m_stuckDetector.Reset();
            bool forceGeneratePath = true;
            RegeneratePath(pipeUser, ref forceGeneratePath);
            m_bWaitingForPathResult = true;
        }
    }

    public bool IsPathRegenerationInhibited() { return m_inhibitPathRegen || m_passingStraightNavSO; }

    public void SetControlSpeed(bool bValue) { m_bControlSpeed = bValue; }

    public void ExecuteDry(CPipeUser u) { }

    //===================================================================
    // Private members — GoalOpTrace.h:118-199
    //===================================================================
    private bool m_bBlock_ExecuteTrace_untilFullUpdateThenReset;

    private float m_ManeuverDist;
    private CTimeValue m_ManeuverTime;
    private float m_landHeight;
    private float m_workingLandingHeightOffset;
    private Vec3 m_landingPos;
    private Vec3 m_landingDir;

    private bool m_bExactFollow;
    private bool m_bForceReturnPartialPath;
    private Vec3 m_lastPosition;
    private CTimeValue m_prevFrameStartTime;
    private float m_TimeStep;
    private CWeakRef<CPipeUser> m_refPipeUser = new CWeakRef<CPipeUser>();
    private int m_looseAttentionId;
    private float m_fTotalTracingTime;
    private bool m_inhibitPathRegen;

    private bool m_bWaitingForPathResult;
    private bool m_bWaitingForBusySmartObject;

    private bool m_earlyPathRegen;
    private bool m_bControlSpeed;

    private float m_fTravelDist;
    private float m_accumulatedFailureTime;

    private enum ETraceActorTgtRequest
    {
        eTATR_None,
        eTATR_NavSO,
        eTATR_EndOfPath,
    }

    private ETraceActorTgtRequest m_actorTargetRequester;
    private ETraceActorTgtRequest m_pendingActorTargetRequester;
    private CStrongRef<CAIObject> m_refNavTarget = new CStrongRef<CAIObject>();

    private StuckDetector m_stuckDetector = new StuckDetector();

    private bool m_stopOnAnimationStart;

    // PATHFINDER constants — mirrored from EPathDecision
    private const int PATHFINDER_PATHFOUND = 1;
    private const int PATHFINDER_NOPATH = 0;
    private const int PATHFINDER_STILLFINDING = 2;
    private const int PATHFINDER_ABORT = 3;

    // AIAG_ACTION constant
    private const string AIAG_ACTION = "Action";

    // GetNameSafe helper — GoalOp.h:137
    private static string GetNameSafe(CAIObject pObject)
    {
        return pObject != null ? pObject.GetName() : "<null>";
    }
}

// ETraceEndMode — GoalOp.h:130-134
public enum ETraceEndMode
{
    eTEM_FixedDistance,
    eTEM_MinimumDistance
}
