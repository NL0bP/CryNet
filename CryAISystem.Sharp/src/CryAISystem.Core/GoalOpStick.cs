// Literal port of dev/Code/CryEngine/CryAISystem/GoalOpStick.{h,cpp} (1208L + 152L C++).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CryAISystem.CryMath;
using static CryAISystem.AISignalConstants;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.NilRefHelper;
using static CryAISystem.EAIObjectType;
using static CryAISystem.GlobalFunctions;
using CryAISystem.CryCommon;

namespace CryAISystem;

// GoalOp flag constants from IAgent.h — needed by COPStick constructor
public static class GoalOpFlags
{
    public const int AILASTOPRES_USE = 0x01;
    public const int AILASTOPRES_LOOKAT = 0x02;
    public const int AI_REQUEST_PARTIAL_PATH = 0x400;
    public const int AI_CONSTANT_SPEED = 0x2000;
    public const int AI_STOP_ON_ANIMATION_START = 0x8000;
    public const int AI_ADJUST_SPEED = 0x20000;
}

// C++ #define from GoalOpStick.cpp:23
// threshold (in m) used in COPStick and COPApproach, to detect if the returned path
// is bringing the agent too far from the expected destination
internal static class StickConstants
{
    public const float C_MaxDistanceForPathOffset = 2;
}

////////////////////////////////////////////////////////////
//
//          STICK - the agent keeps at a constant distance to his target
//
////////////////////////////////////////////////////////////
public class COPStick : IGoalOp
{
    // PATHFINDER constants
    private const int PATHFINDER_PATHFOUND = 1;
    private const int PATHFINDER_NOPATH = 0;
    private const int PATHFINDER_STILLFINDING = 2;

    //===================================================================
    // COPStick — GoalOpStick.cpp:29-83
    //===================================================================
    public COPStick(float fStickDistance, float fEndAccuracy, float fDuration, int nFlags, int nFlagsAux, ETraceEndMode eTraceEndMode = ETraceEndMode.eTEM_FixedDistance)
    {
        m_vLastUsedTargetPos = new Vec3(0, 0, 0);
        m_fTrhDistance = 1.0f;
        m_eTraceEndMode = eTraceEndMode;
        m_fApproachTime = -1.0f;
        m_fHijackDistance = -1.0f;
        m_fStickDistance = fStickDistance;
        m_fEndAccuracy = fEndAccuracy;
        m_fDuration = fDuration;
        m_bContinuous = (nFlagsAux & 0x01) == 0;
        m_bTryShortcutNavigation = (nFlagsAux & 0x02) != 0;
        m_bUseLastOpResult = (nFlags & GoalOpFlags.AILASTOPRES_USE) != 0;
        m_bLookAtLastOp = (nFlags & GoalOpFlags.AILASTOPRES_LOOKAT) != 0;
        m_bInitialized = false;
        m_bForceReturnPartialPath = (nFlags & GoalOpFlags.AI_REQUEST_PARTIAL_PATH) != 0;
        m_bStopOnAnimationStart = (nFlags & GoalOpFlags.AI_STOP_ON_ANIMATION_START) != 0;
        m_targetPredictionTime = 0.0f;
        m_pTraceDirective = null;
        m_pPathfindDirective = null;
        m_looseAttentionId = 0;
        m_bPathFound = false;
        m_bBodyIsAligned = false;
        m_bAlignBodyBeforeMove = false;
        m_fCorrectBodyDirTime = 0.0f;
        m_fTimeSpentAligning = 0.0f;

        if (m_fStickDistance < 0.0f)
        {
            AILog.AIWarning("COPStick::COPStick: Negative stick distance provided.");
        }

        if (gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPStick::COPStick {0}", this);

        m_smoothedTargetVel = new Vec3(0, 0, 0);
        m_lastTargetPos = new Vec3(0, 0, 0);
        m_safePointInterval = 1.0f;
        m_maxTeleportSpeed = 10.0f;
        m_pathLengthForTeleport = 20.0f;
        m_playerDistForTeleport = 3.0f;

        m_lastVisibleTime = new CTimeValue(0);
        ClearTeleportData();

        if (m_bContinuous)
        {
            m_bConstantSpeed = (nFlags & GoalOpFlags.AI_CONSTANT_SPEED) != 0;
        }
        else
        {
            m_bConstantSpeed = (nFlags & GoalOpFlags.AI_ADJUST_SPEED) == 0;
        }
    }

    //===================================================================
    // ~COPStick — GoalOpStick.cpp:176-183
    //===================================================================
    ~COPStick()
    {
        m_pPathfindDirective = null;
        m_pTraceDirective = null;

        if (gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPStick::~COPStick {0}", this);
    }

    //===================================================================
    // Reset — GoalOpStick.cpp:188-229
    //===================================================================
    public void Reset(CPipeUser pPipeUser)
    {
        if (gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPStick::Reset {0}", GetNameSafe(pPipeUser));

        m_refStickTarget.Reset();
        m_refSightTarget.Reset();

        m_pPathfindDirective = null;
        m_pTraceDirective = null;

        m_bBodyIsAligned = false;
        m_fCorrectBodyDirTime = 0.0f;
        m_fTimeSpentAligning = 0.0f;

        m_bPathFound = false;
        m_vLastUsedTargetPos = new Vec3(0, 0, 0);

        m_smoothedTargetVel = new Vec3(0, 0, 0);
        m_lastTargetPos = new Vec3(0, 0, 0);

        ClearTeleportData();

        if (pPipeUser != null)
        {
            pPipeUser.ClearPath("COPStick::Reset m_Path");
            if (m_bLookAtLastOp)
            {
                pPipeUser.SetLooseAttentionTarget(NILREF, m_looseAttentionId);
                m_looseAttentionId = 0;
            }

            // Clear the movement information so that the agent doesn't move
            SOBJECTSTATE state = pPipeUser.m_State;
            state.vMoveDir = new Vec3(0, 0, 0);
            state.vMoveTarget = new Vec3(0, 0, 0);
            state.vInflectionPoint = new Vec3(0, 0, 0);
            state.fDesiredSpeed = 0.0f;
            state.fDistanceToPathEnd = 0.0f;
            state.predictedCharacterStates.nStates = 0;
        }
    }

    //===================================================================
    // Serialize — GoalOpStick.cpp:266-343
    //===================================================================
    public void Serialize(TSerialize ser)
    {
        ser.BeginGroup("COPStick");
        {
            ser.Value("m_vLastUsedTargetPos", ref m_vLastUsedTargetPos);
            ser.Value("m_fTrhDistance", ref m_fTrhDistance);
            ser.Value("m_fStickDistance", ref m_fStickDistance);
            ser.Value("m_fEndAccuracy", ref m_fEndAccuracy);
            ser.Value("m_fDuration", ref m_fDuration);
            ser.Value("m_bContinuous", ref m_bContinuous);
            ser.Value("m_bLookAtLastOp", ref m_bLookAtLastOp);
            ser.Value("m_bTryShortcutNavigation", ref m_bTryShortcutNavigation);
            ser.Value("m_bUseLastOpResult", ref m_bUseLastOpResult);
            ser.Value("m_targetPredictionTime", ref m_targetPredictionTime);
            ser.Value("m_bPathFound", ref m_bPathFound);
            ser.Value("m_bInitialized", ref m_bInitialized);
            ser.Value("m_bConstantSpeed", ref m_bConstantSpeed);
            ser.Value("m_teleportCurrent", ref m_teleportCurrent);
            ser.Value("m_teleportEnd", ref m_teleportEnd);
            ser.Value("m_lastTeleportTime", ref m_lastTeleportTime);
            ser.Value("m_lastVisibleTime", ref m_lastVisibleTime);
            ser.Value("m_maxTeleportSpeed", ref m_maxTeleportSpeed);
            ser.Value("m_pathLengthForTeleport", ref m_pathLengthForTeleport);
            ser.Value("m_playerDistForTeleport", ref m_playerDistForTeleport);
            ser.Value("m_bForceReturnPartialPath", ref m_bForceReturnPartialPath);
            ser.Value("m_bStopOnAnimationStart", ref m_bStopOnAnimationStart);
            ser.Value("m_lastTargetPosTime", ref m_lastTargetPosTime);
            ser.Value("m_lastTargetPos", ref m_lastTargetPos);
            ser.Value("m_smoothedTargetVel", ref m_smoothedTargetVel);
            ser.Value("m_looseAttentionId", ref m_looseAttentionId);
            ser.Value("m_safePointInterval", ref m_safePointInterval);

            m_refStickTarget.Serialize(ser, "m_refStickTarget");
            m_refSightTarget.Serialize(ser, "m_refSightTarget");

            if (ser.IsWriting())
            {
                if (m_pTraceDirective != null)
                {
                    ser.BeginGroup("TraceDirective");
                    m_pTraceDirective.Serialize(ser);
                    ser.EndGroup();
                }
                if (m_pPathfindDirective != null)
                {
                    ser.BeginGroup("PathFindDirective");
                    m_pPathfindDirective.Serialize(ser);
                    ser.EndGroup();
                }
            }
            else
            {
                m_pTraceDirective = null;
                m_pTraceDirective = new COPTrace(true);
                m_pTraceDirective.Serialize(ser);

                m_pPathfindDirective = null;
                m_pPathfindDirective = new COPPathFind("");
                m_pPathfindDirective.Serialize(ser);
            }
        }
        ser.EndGroup();
    }

    //===================================================================
    // GetEndDistance — GoalOpStick.cpp:348-360
    //===================================================================
    private float GetEndDistance(CPipeUser pPipeUser)
    {
        if (m_fDuration > 0.0f)
        {
            float fNormalSpeed, fMinSpeed, fMaxSpeed;
            pPipeUser.GetMovementSpeedRange(pPipeUser.m_State.fMovementUrgency, false,
                out fNormalSpeed, out fMinSpeed, out fMaxSpeed);

            if (fNormalSpeed > 0.0f)
                return -fNormalSpeed * m_fDuration;
        }
        return m_fStickDistance;
    }

    //===================================================================
    // RegeneratePath — GoalOpStick.cpp:364-378
    //===================================================================
    private void RegeneratePath(CPipeUser pPipeUser, Vec3 vDestination)
    {
        if (pPipeUser == null)
            return;

        if (gAIEnv.CVars.DebugPathFinding != 0)
            AILog.AILogAlways("COPStick::RegeneratePath {0}", GetNameSafe(pPipeUser));

        m_pPathfindDirective.Reset(pPipeUser);
        m_pTraceDirective.m_fEndAccuracy = m_fEndAccuracy;
        m_vLastUsedTargetPos = vDestination;
        pPipeUser.m_nPathDecision = PATHFINDER_STILLFINDING;

        Vec3 vPipeUserPos = pPipeUser.GetPhysicsPos();
    }

    //===================================================================
    // DebugDraw — GoalOpStick.cpp:383-405
    //===================================================================
    public void DebugDraw(CPipeUser pPipeUser)
    {
        if (m_pPathfindDirective != null)
            m_pPathfindDirective.DebugDraw(pPipeUser);
        if (m_pTraceDirective != null)
            m_pTraceDirective.DebugDraw(pPipeUser);
    }

    //===================================================================
    // ClearTeleportData — GoalOpStick.cpp:444-448
    //===================================================================
    private void ClearTeleportData()
    {
        m_teleportCurrent = new Vec3(0, 0, 0);
        m_teleportEnd = new Vec3(0, 0, 0);
    }

    //===================================================================
    // TryToTeleport — GoalOpStick.cpp:460-606
    // Simplified: full safe-point iteration and ghost movement requires MiniQueue.
    // Shell returns false (no teleport attempted).
    //===================================================================
    private bool TryToTeleport(CPipeUser pPipeUser)
    {
        // Full teleport logic requires MiniQueue<SSafePoint, 32> iteration and
        // WouldHumanBeVisible — shell returns false.
        return false;
    }

    //===================================================================
    // UpdateStickTargetSafePoints — GoalOpStick.cpp:410-439
    // Simplified: requires MiniQueue and CLeader. Shell is no-op.
    //===================================================================
    private void UpdateStickTargetSafePoints(CPipeUser pPipeUser)
    {
        // Full implementation requires MiniQueue and CLeader — shell no-op.
    }

    //===================================================================
    // Execute — GoalOpStick.cpp:610-830
    //===================================================================
    public EGoalOpResult Execute(CPipeUser pPipeUser)
    {
        CCCPOINT("COPStick_Execute");
        // FUNCTION_PROFILER(GetISystem(), PROFILE_AI);

        // Check to see if objects have disappeared since last call
        if ((m_refSightTarget.IsSet() && !m_refSightTarget.IsValid()) ||
             (m_refStickTarget.IsSet() && !m_refStickTarget.IsValid()))
        {
            CCCPOINT("COPStick_Execute_TargetRemoved");

            if (gAIEnv.CVars.DebugPathFinding != 0)
                AILog.AILogAlways("COPStick::Execute ({0}) resetting due stick/sight target removed", this);

            Reset(null);
        }

        // Do not mind the target direction when approaching.
        pPipeUser.m_bPathfinderConsidersPathTargetDirection = false;

        EGoalOpResult eGoalOpResult;

        if (!m_refStickTarget.IsValid())
            if (!GetStickAndSightTargets_CreatePathfindAndTraceGoalOps(pPipeUser, out eGoalOpResult))
                return eGoalOpResult;

        CAIObject pStickTarget = m_refStickTarget.GetAIObject();
        CAIObject pSightTarget = m_refSightTarget.GetAIObject();

        SOBJECTSTATE pipeUserState = pPipeUser.m_State;

        // Special case for formation points, do not stick to disabled points.
        if ((pStickTarget != null) && (pStickTarget.GetSubType() == ESubType.STP_FORMATION) && !pStickTarget.IsEnabled())
        {
            pipeUserState.vMoveDir = new Vec3(0, 0, 0);
            return EGoalOpResult.eGOR_IN_PROGRESS;
        }

        if (!m_bContinuous && pPipeUser.m_nPathDecision == PATHFINDER_NOPATH)
        {
            if (gAIEnv.CVars.DebugPathFinding != 0)
                AILog.AILogAlways("COPStick::Execute ({0}) resetting due to non-continuous and no path {1}", this, GetNameSafe(pPipeUser));

            Reset(pPipeUser);
            return EGoalOpResult.eGOR_FAILED;
        }

        // make sure the guy looks in correct direction
        if (m_bLookAtLastOp && pSightTarget != null)
        {
            m_looseAttentionId = pPipeUser.SetLooseAttentionTarget(m_refSightTarget);
        }

        // trace gets deleted when we reach the end and it's not continuous
        if (m_pTraceDirective == null)
        {
            if (gAIEnv.CVars.DebugPathFinding != 0)
                AILog.AILogAlways("COPStick::Execute ({0}) returning true due to no trace directive {1}", this, GetNameSafe(pPipeUser));

            Reset(pPipeUser);
            return EGoalOpResult.eGOR_FAILED;
        }

        bool b2D = !pPipeUser.IsUsing3DNavigation();

        EGoalOpResult eTraceResult = EGoalOpResult.eGOR_IN_PROGRESS;

        ///////// TRACE //////////////////////////////////////////////////////////
        if (m_bPathFound)
        {
            if (!m_bAlignBodyBeforeMove || m_bBodyIsAligned)
            {
                if (!Trace(pPipeUser, pStickTarget, ref eTraceResult))
                    return eTraceResult;
            }
            else
            {
                // Align body towards the move target before starting to move
                if (!Trace(pPipeUser, pStickTarget, ref eTraceResult))
                    return eTraceResult;

                if (!pipeUserState.vMoveTarget.IsZero())
                {
                    Vec3 dirToMoveTarget = pipeUserState.vMoveTarget - pPipeUser.GetPhysicsPos();
                    dirToMoveTarget.z = 0.0f;
                    dirToMoveTarget.Normalize();
                    pPipeUser.SetBodyTargetDir(dirToMoveTarget);

                    Vec3 actualBodyDir = pPipeUser.GetBodyInfo().vAnimBodyDir;
                    bool lookingTowardsMoveTarget = (actualBodyDir.Dot(dirToMoveTarget) > cosf(DEG2RAD(17.0f)));
                    if (lookingTowardsMoveTarget)
                        m_fCorrectBodyDirTime += gEnv.pTimer.GetFrameTime();
                    else
                        m_fCorrectBodyDirTime = 0.0f;

                    float timeSpentAligning = m_fTimeSpentAligning + gEnv.pTimer.GetFrameTime();
                    m_fTimeSpentAligning = timeSpentAligning;

                    if (m_fCorrectBodyDirTime > 0.2f || timeSpentAligning > 8.0f)
                    {
                        pPipeUser.ResetBodyTargetDir();
                        m_bBodyIsAligned = true;
                    }
                }

                // Clear the movement information so that the agent doesn't move
                pipeUserState.vMoveDir = new Vec3(0, 0, 0);
                pipeUserState.vMoveTarget = new Vec3(0, 0, 0);
                pipeUserState.vInflectionPoint = new Vec3(0, 0, 0);
                pipeUserState.fDesiredSpeed = 0.0f;
                pipeUserState.fDistanceToPathEnd = 0.0f;
                pipeUserState.predictedCharacterStates.nStates = 0;
            }
        }
        //////////////////////////////////////////////////////////////////////////

        Debug.Assert(pStickTarget != null);
        if (pStickTarget == null)
            return EGoalOpResult.eGOR_IN_PROGRESS;

        // Cache some values
        Vec3 vStickTargetPos = pStickTarget.GetPhysicsPos();
        Vec3 vPipeUserPos = pPipeUser.GetPhysicsPos();
        float fPathDistanceLeft = pPipeUser.m_Path.GetPathLength(false);

        // Hijack
        if (m_eTraceEndMode == ETraceEndMode.eTEM_MinimumDistance)
            if (HandleHijack(pPipeUser, vStickTargetPos, fPathDistanceLeft, eTraceResult))
                return EGoalOpResult.eGOR_SUCCEEDED;

        // Teleport
        if (m_maxTeleportSpeed > 0.0f && pPipeUser.m_movementAbility.teleportEnabled)
        {
            UpdateStickTargetSafePoints(pPipeUser);
            TryToTeleport(pPipeUser);
        }

        // Target prediction
        m_targetPredictionTime = (pPipeUser.GetType() == (ushort)AIOBJECT_VEHICLE) ? 2.0f : 0.0f;
        if (m_targetPredictionTime > 0.0f)
        {
            HandleTargetPrediction(pPipeUser, vStickTargetPos);
        }
        else
        {
            m_smoothedTargetVel = new Vec3(0, 0, 0);
            m_lastTargetPos = vStickTargetPos;
        }
        Vec3 vPredictedTargetOffset = m_smoothedTargetVel * m_targetPredictionTime;

        // ensure offset doesn't cross forbidden
        int buildingID = -1;
        IAISystem_ENavigationType ePipeUserLastNavNodeType = gAIEnv.pNavigation.CheckNavigationType(pPipeUser.GetPos(),
            ref buildingID, pPipeUser.GetMovementAbility().pathfindingProperties.navCapMask);

        m_pPathfindDirective.SetTargetOffset(vPredictedTargetOffset);
        vStickTargetPos += vPredictedTargetOffset;

        Vec3 vToStickTarget = ((m_bForceReturnPartialPath && (pPipeUser.m_nPathDecision == PATHFINDER_PATHFOUND))
            ? pPipeUser.m_Path.GetLastPathPos()
            : vStickTargetPos) - vPipeUserPos;

        if (b2D)
            vToStickTarget.z = 0.0f;

        IAIObject pAttTarget = pPipeUser.GetAttentionTarget();
        if (pAttTarget != null && (pAttTarget != (IAIObject)pStickTarget) && pipeUserState.vMoveDir.IsZero(0.05f))
            pPipeUser.m_bLooseAttention = false;

        //////////////////////////////////////////////////////////////////////////
        {
            int nPathDecision = pPipeUser.m_nPathDecision;

            if (nPathDecision != PATHFINDER_STILLFINDING)
            {
                if ((m_pTraceDirective.m_Maneuver == COPTrace.EManeuver.eMV_None) && !m_pTraceDirective.m_passingStraightNavSO)
                {
                    EActorTargetPhase eCurrentActorTargetPhase = pipeUserState.curActorTargetPhase;
                    if ((eCurrentActorTargetPhase == EActorTargetPhase.eATP_None) || (eCurrentActorTargetPhase == EActorTargetPhase.eATP_Error))
                    {
                        SNavPathParams navPathParams = pPipeUser.m_Path.GetParams();
                        if (navPathParams != null && !navPathParams.precalculatedPath && !navPathParams.inhibitPathRegeneration)
                        {
                            if (IsTargetDirty(pPipeUser, vPipeUserPos, b2D, vStickTargetPos, ePipeUserLastNavNodeType))
                            {
                                RegeneratePath(pPipeUser, vStickTargetPos);
                            }
                        }
                    }
                }
            }

            // check pathfinder status
            return HandlePathDecision(pPipeUser, nPathDecision, b2D);
        }
    }

    //===================================================================
    // ExecuteDry — GoalOpStick.cpp:837-859
    //===================================================================
    public void ExecuteDry(CPipeUser pPipeUser)
    {
        CAIObject pStickTarget = m_refStickTarget.GetAIObject();

        if (m_pTraceDirective != null && pStickTarget != null)
        {
            bool bAdjustSpeed = !m_bConstantSpeed && (m_pTraceDirective.m_Maneuver == COPTrace.EManeuver.eMV_None);

            if (bAdjustSpeed && (pPipeUser.GetType() == (ushort)AIOBJECT_ACTOR) && !pPipeUser.IsUsing3DNavigation())
            {
                pPipeUser.m_State.fMovementUrgency = AISPEED_SPRINT;
            }

            if (m_bPathFound && (!m_bAlignBodyBeforeMove || m_bBodyIsAligned))
            {
                m_pTraceDirective.ExecuteTrace(pPipeUser, false);
            }

            if (bAdjustSpeed)
            {
                CPuppet pPuppet = pPipeUser.CastToCPuppet();
                if (pPuppet != null)
                    pPuppet.AdjustSpeed(pStickTarget, m_fStickDistance);
            }
        }
    }

    //===================================================================
    // GetStickAndSightTargets_CreatePathfindAndTraceGoalOps — GoalOpStick.cpp:862-947
    //===================================================================
    private bool GetStickAndSightTargets_CreatePathfindAndTraceGoalOps(CPipeUser pPipeUser, out EGoalOpResult eGoalOpResult)
    {
        eGoalOpResult = EGoalOpResult.eGOR_IN_PROGRESS;

        // first time = lets stick to target or special named if passed in
        if (string.IsNullOrEmpty(m_sLocateName))
        {
            m_refStickTarget = WeakRefHelpers.GetWeakRef((CAIObject)pPipeUser.GetAttentionTarget());
        }
        else if (!m_bUseLastOpResult)
        {
            CAIObject pAIObject = pPipeUser.GetSpecialAIObject(m_sLocateName, 0.0f);
            m_refStickTarget = WeakRefHelpers.GetWeakRef(pAIObject);
        }

        if (m_bUseLastOpResult || !m_refStickTarget.IsValid())
        {
            if (pPipeUser.m_refLastOpResult.IsValid())
            {
                m_refStickTarget = pPipeUser.m_refLastOpResult;
            }
            else
            {
                if (gAIEnv.CVars.DebugPathFinding != 0)
                    AILog.AILogAlways("COPStick::Execute resetting due to no stick target {0}", GetNameSafe(pPipeUser));

                Reset(pPipeUser);
                eGoalOpResult = EGoalOpResult.eGOR_FAILED;
                return false;
            }
        }

        // keep last op. result as sight target
        if (m_bLookAtLastOp && !m_refSightTarget.IsValid() && pPipeUser.m_refLastOpResult.IsValid())
            m_refSightTarget = pPipeUser.m_refLastOpResult;

        CAIObject pStickTarget = m_refStickTarget.GetAIObject();
        if (pStickTarget == (CAIObject)pPipeUser)
        {
            AILog.AILogAlways("COPStick::Execute sticking to self {0} ", GetNameSafe(pPipeUser));
            Reset(pPipeUser);
            eGoalOpResult = EGoalOpResult.eGOR_SUCCEEDED;
            return false;
        }

        CAIObject pSightTarget = m_refSightTarget.GetAIObject();

        if (m_fStickDistance > 0.0f && (pStickTarget.GetSubType() == ESubType.STP_ANIM_TARGET))
        {
            AILog.AILogAlways("COPStick::Execute resetting stick distance from {0:F1} to zero because the stick target is anim target. {1}",
                m_fStickDistance, GetNameSafe(pPipeUser));
            m_fStickDistance = 0.0f;
        }

        // Create pathfinder operation
        {
            Vec3 vStickPos = pStickTarget.GetPhysicsPos();

            if (gAIEnv.CVars.DebugPathFinding != 0)
                AILog.AILogAlways("COPStick::Execute ({0}) Creating pathfind/trace directives to ({1:F2}, {2:F2}, {3:F2}) {4}", this,
                    vStickPos.x, vStickPos.y, vStickPos.z, GetNameSafe(pPipeUser));

            float fEndTolerance = (m_bForceReturnPartialPath || m_fEndAccuracy < 0.0f)
                ? float.MaxValue
                : m_fEndAccuracy;
            m_pPathfindDirective = new COPPathFind("", pStickTarget, fEndTolerance, GetEndDistance(pPipeUser));

            bool bExactFollow = pSightTarget == null && !pPipeUser.m_bLooseAttention;
            m_pTraceDirective = new COPTrace(bExactFollow, m_fEndAccuracy, m_bForceReturnPartialPath, m_bStopOnAnimationStart, m_eTraceEndMode);

            RegeneratePath(pPipeUser, vStickPos);

            CPuppet pPuppet = pPipeUser.CastToCPuppet();
            if ((pPuppet != null) && !m_bInitialized && !pPuppet.m_movementAbility.b3DMove)
            {
                pPuppet.ResetSpeedControl();
                m_bInitialized = true;
            }
        }

        return true;
    }

    //===================================================================
    // Trace — GoalOpStick.cpp:950-1001
    //===================================================================
    private bool Trace(CPipeUser pPipeUser, CAIObject pStickTarget, ref EGoalOpResult eTraceResult)
    {
        Debug.Assert(m_pTraceDirective != null, "m_pTraceDirective should really be set here");
        if (m_pTraceDirective == null)
        {
            eTraceResult = EGoalOpResult.eGOR_FAILED;
            return false;
        }

        bool bAdjustSpeed = !m_bConstantSpeed && (m_pTraceDirective.m_Maneuver == COPTrace.EManeuver.eMV_None);

        if (bAdjustSpeed && (pPipeUser.GetType() == (ushort)AIOBJECT_ACTOR) && !pPipeUser.IsUsing3DNavigation())
        {
            pPipeUser.m_State.fMovementUrgency = AISPEED_SPRINT;
        }

        eTraceResult = m_pTraceDirective.Execute(pPipeUser);

        if (bAdjustSpeed)
        {
            CPuppet pPuppet = pPipeUser.CastToCPuppet();
            if (pPuppet != null)
                pPuppet.AdjustSpeed(pStickTarget, m_fStickDistance);
        }

        // If the path has been traced, finish the operation if the operand is not sticking continuously.
        if (eTraceResult != EGoalOpResult.eGOR_IN_PROGRESS)
        {
            if (!m_bContinuous && (m_eTraceEndMode != ETraceEndMode.eTEM_MinimumDistance))
            {
                if (gAIEnv.CVars.DebugPathFinding != 0)
                    AILog.AILogAlways("COPStick::Execute ({0}) finishing due to non-continuous and finished tracing {1}", this, GetNameSafe(pPipeUser));

                Reset(pPipeUser);
                return false;
            }

            m_pPathfindDirective.m_bWaitingForResult = false;
        }

        if (pStickTarget == null)
        {
            eTraceResult = EGoalOpResult.eGOR_IN_PROGRESS;
            return false;
        }

        return true;
    }

    //===================================================================
    // HandleHijack — GoalOpStick.cpp:1005-1060
    //===================================================================
    private bool HandleHijack(CPipeUser pPipeUser, Vec3 vStickTargetPos, float fPathDistanceLeft, EGoalOpResult eTraceResult)
    {
        float HIJACK_DISTANCE = 0.2f;

        Vec3 vToStickTarget = vStickTargetPos - pPipeUser.GetPhysicsPos();
        float fToStickTargetLength2D = vStickTargetPos.GetLength2D();

        if (((eTraceResult != EGoalOpResult.eGOR_IN_PROGRESS) && !m_bContinuous) ||
            (m_bPathFound && (fPathDistanceLeft < HIJACK_DISTANCE)))
        {
            float fFrameStartTimeInSeconds = GetAISystem().GetFrameStartTimeSeconds();

            if (m_fApproachTime == -1.0f)
            {
                float MIN_APPROACH_TIME = 0.3f;
                m_fApproachTime = fFrameStartTimeInSeconds + MIN_APPROACH_TIME;
                m_fHijackDistance = fToStickTargetLength2D;
            }
            else if (m_fApproachTime <= fFrameStartTimeInSeconds)
            {
                float fOldDistance = (vStickTargetPos - pPipeUser.GetLastPosition()).GetLength2D();
                float fNewDistance = fToStickTargetLength2D;

                float MOVEMENT_STOPPED_SECOND_EPSILON = 0.05f;
                if (fNewDistance >= fOldDistance - MOVEMENT_STOPPED_SECOND_EPSILON)
                {
                    m_fApproachTime = -1.0f;
                    m_fHijackDistance = -1.0f;

                    if (gAIEnv.CVars.DebugPathFinding != 0)
                        AILog.AILogAlways("COPStick::Execute ({0}) finishing due to non-continuous and finished tracing {1}", this, GetNameSafe(pPipeUser));

                    Reset(pPipeUser);
                    return true;
                }
            }

            // Ignore pathfinding and force movement in the direction of our target.
            SOBJECTSTATE pipeUserState = pPipeUser.m_State;
            float fNormalSpeed, fMinSpeed, fMaxSpeed;
            pPipeUser.GetMovementSpeedRange(pipeUserState.fMovementUrgency, pipeUserState.allowStrafing,
                out fNormalSpeed, out fMinSpeed, out fMaxSpeed);
            float fRemainingDistance = fToStickTargetLength2D;
            Debug.Assert(m_fHijackDistance > 0.0f);
            pipeUserState.fDesiredSpeed = fNormalSpeed * fRemainingDistance / m_fHijackDistance;
            pipeUserState.vMoveDir = vToStickTarget.GetNormalizedSafe(Vec3Constants.fVec3_Zero);
        }

        return false;
    }

    //===================================================================
    // HandleTargetPrediction — GoalOpStick.cpp:1063-1092
    //===================================================================
    private void HandleTargetPrediction(CPipeUser pPipeUser, Vec3 vStickTargetPos)
    {
        CTimeValue now = GetAISystem().GetFrameStartTime();

        if (m_lastTargetPos.IsZero())
        {
            m_lastTargetPos = vStickTargetPos;
            m_lastTargetPosTime = now;
            m_smoothedTargetVel = new Vec3(0, 0, 0);
        }
        else
        {
            long dt = (now - m_lastTargetPosTime).GetMilliSecondsAsInt64();
            if (dt > 0)
            {
                Vec3 targetVel = vStickTargetPos - m_lastTargetPos;

                if (targetVel.GetLengthSquared() > 5.0f)
                    targetVel = new Vec3(0, 0, 0);
                else
                    targetVel /= ((float)dt * 0.001f);

                float frac = 0.1f;
                m_smoothedTargetVel = frac * targetVel + (1.0f - frac) * m_smoothedTargetVel;
                m_lastTargetPos = vStickTargetPos;
                m_lastTargetPosTime = now;
            }
        }
    }

    //===================================================================
    // IsTargetDirty — GoalOpStick.cpp:1095-1148
    //===================================================================
    private bool IsTargetDirty(CPipeUser pPipeUser, Vec3 vPipeUserPos, bool b2D, Vec3 vStickTargetPos, IAISystem_ENavigationType ePipeUserLastNavNodeType)
    {
        Vec3 vFromStickTargetToLastUsedTarget = m_vLastUsedTargetPos - vStickTargetPos;
        float fTargetMoveDist = b2D
            ? vFromStickTargetToLastUsedTarget.GetLength2D()
            : vFromStickTargetToLastUsedTarget.GetLength();

        if (fTargetMoveDist <= m_fTrhDistance)
            return false;

        if (pPipeUser.m_Path.Empty())
            return true;

        Vec3 vPathEndPos = pPipeUser.m_PathDestinationPos;
        Vec3 vDirToPathEnd = vPathEndPos - vPipeUserPos;
        if (b2D)
            vDirToPathEnd.z = 0.0f;
        vDirToPathEnd.NormalizeSafe();

        Vec3 vDirFromPathEndToStickTarget = vStickTargetPos - vPathEndPos;
        if (b2D)
            vDirFromPathEndToStickTarget.z = 0.0f;
        vDirFromPathEndToStickTarget.NormalizeSafe();

        float fRegenerateDist = m_fTrhDistance;
        if (vDirFromPathEndToStickTarget.Dot(vDirToPathEnd) < cosf(DEG2RAD(8.0f)))
            fRegenerateDist *= 5.0f;

        if (fTargetMoveDist > fRegenerateDist)
            return true;

        float fPathDistLeft = pPipeUser.m_Path.GetPathLength(false);
        Vec3 vFromStickTargetToPathEnd = vPathEndPos - vStickTargetPos;
        float fPathEndError = b2D ? vFromStickTargetToPathEnd.GetLength2D() : vFromStickTargetToPathEnd.GetLength();

        if (ePipeUserLastNavNodeType == IAISystem_ENavigationType.NAV_VOLUME)
            fPathEndError = max(0.0f, fPathEndError - GetEndDistance(pPipeUser));

        return (fPathEndError > 0.1f) && (fPathDistLeft < 2.0f * fPathEndError);
    }

    //===================================================================
    // HandlePathDecision — GoalOpStick.cpp:1151-1208
    //===================================================================
    private EGoalOpResult HandlePathDecision(CPipeUser pPipeUser, int nPathDecision, bool b2D)
    {
        switch (nPathDecision)
        {
            case PATHFINDER_STILLFINDING:
            {
                m_pPathfindDirective.Execute(pPipeUser);
                return EGoalOpResult.eGOR_IN_PROGRESS;
            }

            case PATHFINDER_NOPATH:
                pPipeUser.m_State.vMoveDir = new Vec3(0, 0, 0);
                if (m_bContinuous)
                {
                    return EGoalOpResult.eGOR_IN_PROGRESS;
                }
                else
                {
                    if (gAIEnv.CVars.DebugPathFinding != 0)
                        AILog.AILogAlways("COPStick::Execute ({0}) resetting due to no path {1}", this, GetNameSafe(pPipeUser));

                    Reset(pPipeUser);
                    return EGoalOpResult.eGOR_FAILED;
                }

            case PATHFINDER_PATHFOUND:
            {
                if (!m_bPathFound)
                {
                    m_bPathFound = true;
                    TPathPoints origPath = pPipeUser.m_OrigPath.GetPath();
                    if (origPath.Count > 0)
                    {
                        PathPointDescriptor lastPathNode = origPath[origPath.Count - 1];
                        Vec3 vLastPos = lastPathNode.vPos;
                        Vec3 vRequestedLastNodePos = pPipeUser.m_Path.GetParams().end;
                        float fDistance = b2D
                            ? Distance.Point_Point2D(vLastPos, vRequestedLastNodePos)
                            : Distance.Point_Point(vLastPos, vRequestedLastNodePos);

                        if ((lastPathNode.navType != IAISystem_ENavigationType.NAV_SMARTOBJECT) &&
                            (fDistance > m_fStickDistance + StickConstants.C_MaxDistanceForPathOffset))
                        {
                            AISignalExtraData pData = new AISignalExtraData();
                            pData.fValue = fDistance - m_fStickDistance;
                            pPipeUser.SetSignal(0, "OnEndPathOffset", pPipeUser.GetEntity(), pData, gAIEnv.SignalCRCs.m_nOnEndPathOffset);
                        }
                        else
                        {
                            pPipeUser.SetSignal(0, "OnPathFound", null, null, gAIEnv.SignalCRCs.m_nOnPathFound);
                        }
                    }

                    return Execute(pPipeUser);
                }
                break;
            }
        }

        return EGoalOpResult.eGOR_IN_PROGRESS;
    }

    //===================================================================
    // Private members — GoalOpStick.h
    //===================================================================
    private Vec3 m_vLastUsedTargetPos;
    private float m_fTrhDistance;
    private CWeakRef<CAIObject> m_refStickTarget = new CWeakRef<CAIObject>();
    private CWeakRef<CAIObject> m_refSightTarget = new CWeakRef<CAIObject>();

    private ETraceEndMode m_eTraceEndMode;
    private float m_fApproachTime;
    private float m_fHijackDistance;

    private float m_fStickDistance;
    private float m_fEndAccuracy;
    private float m_fDuration;
    private float m_fCorrectBodyDirTime;
    private float m_fTimeSpentAligning;
    private bool m_bContinuous;
    private bool m_bTryShortcutNavigation;
    private bool m_bUseLastOpResult;
    private bool m_bLookAtLastOp;
    private bool m_bInitialized;
    private bool m_bBodyIsAligned;
    private bool m_bAlignBodyBeforeMove;
    private bool m_bForceReturnPartialPath;
    private bool m_bStopOnAnimationStart;
    private float m_targetPredictionTime;
    private bool m_bConstantSpeed;

    private CTimeValue m_lastTargetPosTime;
    private Vec3 m_lastTargetPos;
    private Vec3 m_smoothedTargetVel;

    private int m_looseAttentionId;

    private bool m_bPathFound;
    private COPTrace m_pTraceDirective;
    private COPPathFind m_pPathfindDirective;

    private string m_sLocateName = "";

    // Teleport data
    private float m_safePointInterval;
    private Vec3 m_teleportCurrent;
    private Vec3 m_teleportEnd;
    private CTimeValue m_lastTeleportTime;
    private CTimeValue m_lastVisibleTime;
    private float m_maxTeleportSpeed;
    private float m_pathLengthForTeleport;
    private float m_playerDistForTeleport;

    // GetNameSafe helper
    private static string GetNameSafe(CAIObject pObject)
    {
        return pObject != null ? pObject.GetName() : "<null>";
    }

    private static string GetNameSafe(CPipeUser pObject)
    {
        return pObject != null ? pObject.GetName() : "<null>";
    }
}
