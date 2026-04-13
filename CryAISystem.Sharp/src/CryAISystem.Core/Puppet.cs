// Literal port of dev/Code/CryEngine/CryAISystem/Puppet.h + Puppet.cpp + PuppetRateOfDeath.cpp + PuppetPhys.cpp
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.CryRandom;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.GlobalFunctions;
using static CryAISystem.CryCommon.Distance;
using static CryAISystem.CryCommon.Overlap;
using static CryAISystem.CryCommon.Intersect;
using static CryAISystem.CAIPlayerCastHelper;
using static CryAISystem.AISignalConstants;
using static CryAISystem.NilRefHelper;
using static CryAISystem.WalkabilityConstants;
using static CryAISystem.CryCommon.EAIWeaponAccessories;
using static CryAISystem.CryCommon.EAITargetStuntReaction;
using static CryAISystem.CryCommon.ERequestedGrenadeType;
using static CryAISystem.AIPhysConstants;
using static CryAISystem.PuppetConstants;
using CryAISystem.CryCommon;

namespace CryAISystem;

// Path finder blockers types.
public enum ENavigationBlockers
{
    PFB_NONE,
    PFB_ATT_TARGET,
    PFB_REF_POINT,
    PFB_BEACON,
    PFB_DEAD_BODIES,
    PFB_EXPLOSIVES,
    PFB_PLAYER,
    PFB_BETWEEN_NAV_TARGET,
}

// Quick prototype of a signal/state container
public class CSignalState
{
    public bool bState;
    public bool bLastUpdatedState;
    public CSignalState() { bState = false; bLastUpdatedState = false; }
    public bool CheckUpdate()
    {
        bool bRet = bState != bLastUpdatedState;
        bLastUpdatedState = bState;
        return bRet;
    }
}

public struct CSpeedControl
{
    public float fPrevDistance;
    public CTimeValue fLastTime;
    public Vec3 vLastPos;

    public CSpeedControl(int dummy = 0) { vLastPos = new Vec3(0, 0, 0); fPrevDistance = 0; fLastTime = new CTimeValue(); }
    public void Reset(Vec3 vPos, CTimeValue fTime)
    {
        fPrevDistance = 0;
        fLastTime = fTime;
        vLastPos = vPos;
    }
}

// typedef std::multimap<float, SHideSpot> MultimapRangeHideSpots;
public class MultimapRangeHideSpots : SortedDictionary<float, List<SHideSpot>> { }
// typedef std::map< CWeakRef<CAIObject>, float > DevaluedMap;
public class DevaluedMap : Dictionary<CWeakRef<CAIObject>, float> { }
// typedef std::map< CWeakRef<CAIObject>, CWeakRef<CAIObject> > ObjectObjectMap;
public class ObjectObjectMap : Dictionary<CWeakRef<CAIObject>, CWeakRef<CAIObject>> { }

public struct SShootingStatus
{
    public bool triggerPressed;
    public float timeSinceTriggerPressed;
    public bool friendOnWay;
    public float friendOnWayElapsedTime;
    public EFireMode fireMode;
}

public struct SSoundPerceptionDescriptor
{
    public float fMinDist;
    public float fRadiusScale;
    public float fSoundTime;
    public float fBaseThreat;
    public float fLinStepMin;
    public float fLinStepMax;

    public SSoundPerceptionDescriptor(float _fMinDist = 0.0f, float _fRadiusScale = 1.0f, float _fSoundTime = 0.0f, float _fBaseThreat = 0.0f, float _fLinStepMin = 0.0f, float _fLinStepMax = 1.0f)
    {
        fMinDist = _fMinDist;
        fRadiusScale = _fRadiusScale;
        fSoundTime = _fSoundTime;
        fBaseThreat = _fBaseThreat;
        fLinStepMin = _fLinStepMin;
        fLinStepMax = _fLinStepMax;
    }
    public void Set(float _fMinDist = 0.0f, float _fRadiusScale = 1.0f, float _fSoundTime = 0.0f, float _fBaseThreat = 0.0f, float _fLinStepMin = 0.0f, float _fLinStepMax = 1.0f)
    {
        fMinDist = _fMinDist;
        fRadiusScale = _fRadiusScale;
        fSoundTime = _fSoundTime;
        fBaseThreat = _fBaseThreat;
        fLinStepMin = _fLinStepMin;
        fLinStepMax = _fLinStepMax;
    }
}

public struct SSortedHideSpot
{
    public SSortedHideSpot(float weight, SHideSpot pHideSpot) { this.weight = weight; this.pHideSpot = pHideSpot; }
    public static bool operator <(SSortedHideSpot lhs, SSortedHideSpot rhs) { return lhs.weight > rhs.weight; }
    public static bool operator >(SSortedHideSpot lhs, SSortedHideSpot rhs) { return lhs.weight < rhs.weight; }
    public float weight;
    public SHideSpot pHideSpot;
}

public class CPuppet : CPipeUser /*, IPuppet — composition since C# single inheritance */
{
    // friend class CAISystem;

    public CPuppet()
    {
        CCCPOINT("CPuppet_CPuppet");

        _fastcast_CPuppet = true;

        m_postureManager.ResetPostures();

        AILog.AILogComment("CPuppet::CPuppet ({0})", this);
    }
    // virtual ~CPuppet();

    public static void ClearStaticData()
    {
        s_weights.Clear(); s_weights.Capacity = 0;
        s_sortedHideSpots.Clear(); s_sortedHideSpots.Capacity = 0;
        s_enemies.Clear(); s_enemies.Capacity = 0;
        s_traversedNodes.Clear();
        s_hidespots.Clear();
    }

    public virtual IPuppet CastToIPuppet() { return null; /* this in C++ */ }

    public PostureManager GetPostureManager() { return m_postureManager; }

    public override void ResetPerception()
    {
        base.ResetPerception();

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.ClearPotentialTargets();
            m_pPerceptionHandler.ClearTempTarget();
        }

        SetAttentionTarget(NILREF);
    }

    public override bool CanDamageTarget(IAIObject target = null)
    {
        // Never hit when in panic spread fire mode.
        if (m_fireMode == EFireMode.FIREMODE_PANIC_SPREAD)
            return false;

        if (m_Parameters.m_fAccuracy < 0.001f)
            return false;

        CAIObject fireTargetObject = GetFireTargetObject();
        uint fireTargetID = fireTargetObject != null ? fireTargetObject.GetAIObjectID() : 0;

        CAIObject player = GetAISystem().GetPlayer();

        bool isCurrentFireTarget = target == null || (target.GetAIObjectID() == fireTargetID);

        if (isCurrentFireTarget)
        {
            // Allow to hit always when requested kill fire mode.
            if (m_fireMode == EFireMode.FIREMODE_KILL)
                return true;

            if (m_targetDazzlingTime > 0.0f)
                return false;

            if (gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_GAME04)
            {
                if ((m_fireMode != (EFireMode)16 /*FIREMODE_VEHICLE*/) && (m_fireMode != EFireMode.FIREMODE_FORCED) && (m_fireMode != EFireMode.FIREMODE_KILL) && (m_fireMode != EFireMode.FIREMODE_OFF) && !HasFiringReactionTimePassed())
                    return false;
            }
        }

        CAIObject targetAIObject = target != null ? (CAIObject)target : fireTargetObject;
        bool isAIObjectTarget = targetAIObject != null ? (targetAIObject.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_TARGET) : false;
        if (isAIObjectTarget)
            return true;

        CWeakRef<CAIObject> refTarget = GetWeakRef(targetAIObject);
        CAIActor pLiveTargetActor = GetLiveTarget(refTarget).GetAIObject();
        if (pLiveTargetActor == null || pLiveTargetActor.GetProxy() == null)
            return true;

        // If the target is at low health, allow short time of mercy for it.
        if (pLiveTargetActor.IsLowHealthPauseActive())
            return false;

        CCCPOINT("CPuppet_CanDamageTarget");

        float maxHealth = (float)pLiveTargetActor.GetProxy().GetActorMaxHealth();
        float health = 0.001f + pLiveTargetActor.GetProxy().GetActorHealth() + pLiveTargetActor.GetProxy().GetActorArmor();

        float thr = m_targetDamageHealthThr * maxHealth;

        return (thr > 0.0f) && (health >= thr);
    }
    public override bool CanDamageTargetWithMelee()
    {
        CAIObject pLiveTarget = GetLiveTarget(m_refAttentionTarget).GetAIObject();
        if (pLiveTarget == null)
            return true;

        // If the target is at low health, allow short time of mercy for it.
        CAIActor pLiveTargetActor = pLiveTarget.CastToCAIActor();
        if (pLiveTargetActor != null)
            if (pLiveTargetActor.IsLowHealthPauseActive())
                return false;

        return true;
    }

    public void AdjustSpeed(CAIObject pNavTarget, float distance = 0)
    {
        if (pNavTarget == null)
            return;

        if ((GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_VEHICLE) || IsUsing3DNavigation())
        {
            return;
        }

        CCCPOINT("CPuppet_AdjustSpeed");

        CTimeValue fCurrentTime = GetAISystem().GetFrameStartTime();
        float timeStep = (fCurrentTime - m_SpeedControl.fLastTime).GetSeconds();

        float targetSpeed = 0.0f;
        Vec3 targetVel = new Vec3(0, 0, 0);
        Vec3 targetPos = pNavTarget.GetPos();
        IPhysicalEntity pPhysicalEntity = null;
        if (pNavTarget.GetProxy() != null && pNavTarget.GetPhysics() != null)
        {
            pPhysicalEntity = pNavTarget.GetPhysics();
        }
        else if (pNavTarget.GetSubType() == ESubType.STP_FORMATION)
        {
            CAIObject pOwner = pNavTarget.GetAssociation().GetAIObject();
            if (pOwner != null && pOwner.GetProxy() != null && pOwner.GetPhysics() != null)
                pPhysicalEntity = pOwner.GetPhysics();
        }

        if (pPhysicalEntity != null)
        {
            pe_status_dynamics status = new pe_status_dynamics();
            pPhysicalEntity.GetStatus(status);
            targetVel = status.v;
        }
        else if (fCurrentTime > m_SpeedControl.fLastTime)
        {
            targetVel = (m_SpeedControl.vLastPos - GetPos()) / timeStep;
        }

        targetSpeed = targetVel.GetLength();

        float distToEnd = m_State.fDistanceToPathEnd;

        float distToTarget = Point_Point2D(GetPos(), targetPos);
        distToEnd = max(distToEnd, distToTarget);

        distToEnd -= distance;
        if (distToEnd < 0.0f)
            distToEnd = 0.0f;

        float walkSpeed, runSpeed, sprintSpeed, junk0, junk1;
        GetMovementSpeedRange(AISPEED_WALK, false, out junk0, out junk1, out walkSpeed);
        GetMovementSpeedRange(AISPEED_RUN, false, out junk0, out junk1, out runSpeed);
        GetMovementSpeedRange(AISPEED_SPRINT, false, out junk0, out junk1, out sprintSpeed);

        float distForWalk = 4.0f;
        float distForRun = 6.0f;
        float distForSprint = 10.0f;

        if (m_lastChaseUrgencyDist > 0)
        {
            if (m_lastChaseUrgencyDist == 4)
            {
                if (distToEnd < distForSprint - 1.0f)
                    m_lastChaseUrgencyDist = 3;
            }
            else if (m_lastChaseUrgencyDist == 3)
            {
                if (distToEnd > distForSprint)
                    m_lastChaseUrgencyDist = 4;
                if (distToEnd < distForWalk - 1.0f)
                    m_lastChaseUrgencyDist = 2;
            }
            else
            {
                if (distToEnd > distForRun)
                    m_lastChaseUrgencyDist = 3;
            }
        }
        else
        {
            m_lastChaseUrgencyDist = 0;
            if (distToEnd > distForRun)
                m_lastChaseUrgencyDist = 4;
            else if (distToEnd > distForWalk)
                m_lastChaseUrgencyDist = 3;
            else
                m_lastChaseUrgencyDist = 2;
        }

        float urgencyDist = IndexToMovementUrgency(m_lastChaseUrgencyDist);

        if (m_lastChaseUrgencySpeed > 0)
        {
            if (m_lastChaseUrgencyDist == 4)
            {
                if (targetSpeed < runSpeed)
                    m_lastChaseUrgencySpeed = 3;
            }
            else if (m_lastChaseUrgencyDist == 3)
            {
                if (targetSpeed > runSpeed * 1.2f)
                    m_lastChaseUrgencySpeed = 4;
                if (targetSpeed < walkSpeed)
                    m_lastChaseUrgencySpeed = 2;
            }
            else
            {
                if (targetSpeed > walkSpeed * 1.2f)
                    m_lastChaseUrgencySpeed = 3;
                if (targetSpeed < 0.001f)
                    m_lastChaseUrgencySpeed = 0;
            }
        }
        else
        {
            if (targetSpeed > runSpeed * 1.2f)
                m_lastChaseUrgencySpeed = 4;
            else if (targetSpeed > walkSpeed * 1.2f)
                m_lastChaseUrgencySpeed = 3;
            else if (targetSpeed > 0.0f)
                m_lastChaseUrgencySpeed = 2;
            else
                m_lastChaseUrgencySpeed = 0;
        }

        float urgencySpeed = IndexToMovementUrgency(m_lastChaseUrgencySpeed);

        float urgency = max(urgencySpeed, urgencyDist);

        m_State.fMovementUrgency = urgency;
        m_State.predictedCharacterStates.nStates = 0;

        float normalSpeed, minSpeed, maxSpeed;
        GetMovementSpeedRange(urgency, m_State.allowStrafing, out normalSpeed, out minSpeed, out maxSpeed);

        if (targetSpeed < minSpeed)
            targetSpeed = minSpeed;

        float maxExtraLag = 2.0f;
        float speedForMaxExtraLag = 1.5f;

        float lagDistance = 0.1f + maxExtraLag * targetSpeed / speedForMaxExtraLag;
        Limit(ref lagDistance, 0.0f, maxExtraLag);

        float frac = distToEnd / lagDistance;
        Limit(ref frac, 0.0f, 2.2f);
        float chaseSpeed;
        if (frac < 1.0f)
            chaseSpeed = frac * targetSpeed + (1.0f - frac) * minSpeed;
        else if (frac < 2.0f)
            chaseSpeed = (2.0f - frac) * targetSpeed + (frac - 1.0f) * maxSpeed;
        else
            chaseSpeed = maxSpeed * (frac - 1.0f);

        float chaseSpeedSmoothTime = 1.0f;
        SmoothCD(ref m_chaseSpeed, ref m_chaseSpeedRate, timeStep, chaseSpeed, chaseSpeedSmoothTime);

        if (m_State.fDesiredSpeed > m_chaseSpeed)
            m_State.fDesiredSpeed = m_chaseSpeed;
        m_State.fTargetSpeed = chaseSpeed;
    }
    public void ResetSpeedControl()
    {
        m_SpeedControl.Reset(GetPos(), GetAISystem().GetFrameStartTime());
        m_chaseSpeed = 0.0f;
        m_chaseSpeedRate = 0.0f;
        m_lastChaseUrgencyDist = -1;
        m_lastChaseUrgencySpeed = -1;
    }
    public override bool GetValidPositionNearby(Vec3 proposedPosition, out Vec3 adjustedPosition)
    {
        adjustedPosition = proposedPosition;
        if (!GetFloorPos(ref adjustedPosition, proposedPosition, 1.0f, 2.0f, WalkabilityDownRadius, AICE_ALL))
            return false;

        float maxFloorDeviation = 1.0f;
        if (fabsf(adjustedPosition.z - proposedPosition.z) > maxFloorDeviation)
            return false;

        if (!CheckBodyPos(adjustedPosition, AICE_ALL))
            return false;

        Vec3 pushUp = new Vec3(0.0f, 0.0f, 0.2f);
        return gAIEnv.pNavigationSystem.IsLocationValidInNavigationMesh(GetNavigationTypeID(), adjustedPosition + pushUp);
    }
    public override bool GetTeleportPosition(out Vec3 teleportPos)
    {
        teleportPos = new Vec3(0, 0, 0);
        AILog.AIWarning("CPuppet::GetTeleportPosition is currently not supported by the MNM Navigation System.");
        return false;
    }
    public bool GetPosAlongPath(float dist, bool extrapolateBeyond, out Vec3 retPos)
    {
        retPos = new Vec3(0, 0, 0);
        return m_Path.GetPosAlongPath(out retPos, dist, !m_movementAbility.b3DMove, extrapolateBeyond);
    }
    public virtual IFireCommandHandler GetFirecommandHandler() { return m_pFireCmdHandler; }

    public void SetAllowedToHitTarget(bool state) { m_allowedToHitTarget = state; }
    public bool IsAllowedToHitTarget() { return m_allowedToHitTarget; }

    public void SetAllowedToUseExpensiveAccessory(bool state) { m_allowedToUseExpensiveAccessory = state; }
    public bool IsAllowedToUseExpensiveAccessory() { return m_allowedToUseExpensiveAccessory; }

    public CAIObject GetFireTargetObject()
    {
        CAIObject targetObject = m_refFireTarget.GetAIObject();

        if (targetObject != null)
        {
            IEntity targetEntity = targetObject.GetEntity();
            if (targetEntity != null)
            {
                CAIObject targetEntityObject = targetEntity.GetAI() as CAIObject;
                if (targetEntityObject != null)
                    return targetEntityObject;
            }

            return targetObject;
        }

        targetObject = m_refAttentionTarget.GetAIObject();
        return targetObject;
    }

    public float GetFiringReactionTime(Vec3 targetPos)
    {
        // Apply archetype modifier
        float fReactionTime = gAIEnv.CVars.RODReactionTime * GetParameters().m_PerceptionParams.reactionTime;

        CAIActor pLiveTarget = GetLiveTarget(m_refAttentionTarget).GetAIObject();
        if (pLiveTarget == null)
            return fReactionTime;

        CAIActor pLiveActor = pLiveTarget.CastToCAIActor();
        if (pLiveActor == null)
            return fReactionTime;

        // Delegate to handler if available
        if (m_pRODHandler != null)
        {
            fReactionTime = m_pRODHandler.GetFiringReactionTime(this, pLiveTarget, targetPos);
        }
        else
        {
            if (IsAffectedByLight())
            {
                EAILightLevel iTargetLightLevel = pLiveTarget.GetLightLevel();
                if (iTargetLightLevel == EAILightLevel.AILL_MEDIUM)
                {
                    fReactionTime += gAIEnv.CVars.RODReactionMediumIllumInc;
                }
                else if (iTargetLightLevel == EAILightLevel.AILL_DARK)
                {
                    fReactionTime += gAIEnv.CVars.RODReactionDarkIllumInc;
                }
                else if (iTargetLightLevel == EAILightLevel.AILL_SUPERDARK)
                {
                    fReactionTime += gAIEnv.CVars.RODReactionSuperDarkIllumInc;
                }
            }

            float fDistInc = min(1.0f, gAIEnv.CVars.RODReactionDistInc);
            // Increase reaction time if the target is further away.
            if (m_targetZone == EAITargetZone.AIZONE_COMBAT_NEAR)
                fReactionTime += fDistInc;
            else if (m_targetZone >= EAITargetZone.AIZONE_COMBAT_FAR)
                fReactionTime += fDistInc * 2.0f;

            // Increase the reaction time if the target is leaning.
            SAIBodyInfo bi = pLiveActor.GetBodyInfo();
            if (fabsf(bi.lean) > 0.01f)
            {
                fReactionTime += gAIEnv.CVars.RODReactionLeanInc;
            }

            Vec3 dirTargetToShooter = GetPos() - pLiveTarget.GetPos();
            dirTargetToShooter = dirTargetToShooter.Normalized();
            float fLeaningDot = pLiveTarget.GetViewDir().Dot(dirTargetToShooter);

            float thr1 = cosf(DEG2RAD(30.0f));
            float thr2 = cosf(DEG2RAD(95.0f));
            if (fLeaningDot < thr1)
                fReactionTime += gAIEnv.CVars.RODReactionDirInc;
            else if (fLeaningDot < thr2)
                fReactionTime += gAIEnv.CVars.RODReactionDirInc * 2.0f;
        }

        return fReactionTime;
    }

    public float GetCurrentFiringReactionTime() { return m_firingReactionTime; }
    public bool HasFiringReactionTimePassed() { return m_firingReactionTimePassed; }

    public void GetShootingStatus(out SShootingStatus ss)
    {
        ss = new SShootingStatus();
        ss.fireMode = m_fireMode;
        ss.timeSinceTriggerPressed = m_timeSinceTriggerPressed;
        ss.triggerPressed = (m_State.fire == EAIFireState.eAIFS_On);
        ss.friendOnWay = m_friendOnWayElapsedTime > 0.001f;
        ss.friendOnWayElapsedTime = m_friendOnWayElapsedTime;
    }

    public new void SetAllowedStrafeDistances(float start, float end, bool whileMoving)
    {
        m_allowedStrafeDistanceStart = start;
        m_allowedStrafeDistanceEnd = end;
        m_allowStrafeLookWhileMoving = whileMoving;
        m_strafeStartDistance = 0.0f;
        UpdateStrafing();
    }

    public void SetAdaptiveMovementUrgency(float minUrgency, float maxUrgency, float scaleDownPathlen)
    {
        m_adaptiveUrgencyMin = minUrgency;
        m_adaptiveUrgencyMax = maxUrgency;
        m_adaptiveUrgencyScaleDownPathLen = scaleDownPathlen;
        m_adaptiveUrgencyMaxPathLen = 0.0f;
    }

    public void SetDelayedStance(int stance)
    {
        m_delayedStance = stance;
        m_delayedStanceMovementCounter = 0;
    }

    public void AdjustMovementUrgency(ref float urgency, float pathLength, ref float maxPathLen)
    {
        if (m_adaptiveUrgencyScaleDownPathLen > 0.0001f)
        {
            if (pathLength > maxPathLen)
            {
                maxPathLen = pathLength;

                int scaleDown = 0;
                float scale = m_adaptiveUrgencyScaleDownPathLen;
                while (scale > maxPathLen && scaleDown < 4)
                {
                    scale /= 2.0f;
                    scaleDown++;
                }

                int minIdx = MovementUrgencyToIndex(m_adaptiveUrgencyMin);
                int maxIdx = MovementUrgencyToIndex(m_adaptiveUrgencyMax);
                int idx = maxIdx - scaleDown;
                if (idx < minIdx) idx = minIdx;

                // Special case for really short paths.
                if (maxPathLen < 1.2f && idx > 2)
                    idx = 2; // walk

                urgency = IndexToMovementUrgency(idx);
            }
        }
    }

    public CPathObstacles GetPathAdjustmentObstacles(bool allowRecalc = true) { if (allowRecalc) CalculatePathObstacles(); return m_pathAdjustmentObstacles; }
    public CPathObstacles GetLastPathAdjustmentObstacles() { return m_pathAdjustmentObstacles; }

    public float GetAccuracy(CAIObject pTarget)
    {
        float absoluteAccurateTrh = 5.0f;
        float nominalAccuracyStartAt = 0.3f;
        float nominalAccuracyStopAt = 0.73f;

        float unscaleAccuracy = GetParameters().m_fAccuracy;
        float curAccuracy = unscaleAccuracy;
        if (curAccuracy <= 0.00001f)
            return 0.0f;

        if (!IsAllowedToHitTarget())
            curAccuracy *= 0.0f;

        if (pTarget == null)
            return curAccuracy;

        CAIActor pTargetActor = pTarget.CastToCAIActor();

        float distance = (pTarget.GetPos() - GetPos()).GetLength();

        if (distance < absoluteAccurateTrh)
            return 1.0f;
        if (distance >= GetParameters().m_fAttackRange)
            return 0.0f;

        float nominalAccuracyStartDistance = max(absoluteAccurateTrh + 1.0f, GetParameters().m_fAttackRange * nominalAccuracyStartAt);
        float nominalAccuracyStopDistance = min(GetParameters().m_fAttackRange - 0.1f, GetParameters().m_fAttackRange * nominalAccuracyStopAt);

        // scale down accuracy if target prones
        if (pTargetActor != null)
        {
            SAIBodyInfo targetBodyInfo = pTargetActor.GetBodyInfo();

            switch (targetBodyInfo.stance)
            {
                case EStance.STANCE_ALERTED:
                case EStance.STANCE_STAND:
                    curAccuracy *= 1.0f;
                    break;
                case EStance.STANCE_CROUCH:
                    curAccuracy *= 0.8f;
                    break;
                case EStance.STANCE_LOW_COVER:
                    curAccuracy *= 0.65f;
                    break;
                case EStance.STANCE_HIGH_COVER:
                    curAccuracy *= 0.8f;
                    break;
                case EStance.STANCE_PRONE:
                    curAccuracy *= 0.3f;
                    break;
                default:
                    break;
            }
        }

        // scale down accuracy if shooter moves
        if (GetPhysics() != null)
        {
            pe_status_dynamics dSt = new pe_status_dynamics();
            GetPhysics().GetStatus(dSt);
            float fSpeed = dSt.v.GetLength();
            if (fSpeed > 1.0f)
            {
                if (fSpeed > 5.0f)
                    fSpeed = 5.0f;
                if (IsCoverFireEnabled())
                    fSpeed /= 2.0f;
                curAccuracy *= 3.0f / (3.0f + fSpeed);
            }
        }

        if (distance < nominalAccuracyStartDistance) // 1->nominal interpolation
        {
            float slop = (1.0f - curAccuracy) / (nominalAccuracyStartDistance - absoluteAccurateTrh);
            float scaledAccuracy = curAccuracy + slop * (nominalAccuracyStartDistance - (distance - absoluteAccurateTrh));
            return scaledAccuracy;
        }
        if (distance > nominalAccuracyStopDistance) // nominal->0 interpolation
        {
            float slop = curAccuracy / (GetParameters().m_fAttackRange - nominalAccuracyStopDistance);
            float scaledAccuracy = slop * (GetParameters().m_fAttackRange - distance);
            return scaledAccuracy;
        }
        return curAccuracy;
    }

    public override DamagePartVector GetDamageParts() { return m_damageParts; }

    public nuint MemStats() { return 0; /* C++ returns sizeof(this) + container sizes; not meaningful in C# */ }

    public virtual void UpTargetPriority(IAIObject pTarget, float fPriorityIncrement)
    {
        if (m_pPerceptionHandler != null)
            m_pPerceptionHandler.UpTargetPriority(pTarget, fPriorityIncrement);
    }
    public virtual void UpdateBeacon()
    {
        CCCPOINT("CPuppet_UpdateBeacon");

        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();
        CAIObject pLastOpResult = m_refLastOpResult.GetAIObject();

        if (pAttentionTarget != null)
            GetAISystem().UpdateBeacon(GetGroupId(), pAttentionTarget.GetPos(), pAttentionTarget);
        else if (pLastOpResult != null)
            GetAISystem().UpdateBeacon(GetGroupId(), pLastOpResult.GetPos(), pLastOpResult);
    }
    public virtual IAIObject MakeMeLeader()
    {
        if (GetGroupId() == -1 || GetGroupId() == 0)
        {
            AILog.AIWarning("CPuppet::MakeMeLeader: Invalid GroupID ... {0}", GetGroupId());
            return null;
        }

        CLeader pLeader = (CLeader)GetAISystem().GetLeader(GetGroupId());
        if (pLeader != null)
        {
            CWeakRef<CAIObject> refObject = pLeader.GetAssociation();
            if (refObject.GetAIObject() != this)
                pLeader.SetAssociation(GetWeakRef(this));
        }
        else
        {
            pLeader = (CLeader)gAIEnv.pAIObjectManager.CreateAIObject(new AIObjectParams((ushort)CryAISystem.EAIObjectType.AIOBJECT_LEADER, this));

            CCCPOINT("CPuppet_MakeMeLeader");
        }
        return pLeader;
    }
    public virtual bool CheckFriendsInLineOfFire(Vec3 fireDirection, bool cheapTest)
    {
        ActorLookUp lookUp = gAIEnv.pActorLookUp;
        lookUp.Prepare(ActorLookUp.Position | ActorLookUp.Proxy);

        if (m_updatePriority != EPuppetUpdatePriority.AIPUP_VERY_HIGH)
            cheapTest = true;

        Vec3 firePos = GetFirePos();
        bool friendOnWay = false;

        CAIPlayer pPlayer = CastToCAIPlayerSafe(GetAISystem().GetPlayer());
        if (pPlayer != null && !pPlayer.IsHostile(this))
        {
            if (IsFriendInLineOfFire(pPlayer, firePos, fireDirection, cheapTest))
                friendOnWay = true;
        }

        if (!friendOnWay)
        {
            float checkRadiusSqr = sqr(fireDirection.Length() + 2.0f);

            uint activeActorCount = (uint)lookUp.GetActiveCount();

            for (uint actorIndex = 0; actorIndex < activeActorCount; ++actorIndex)
            {
                CAIActor pFriend = lookUp.GetActor<CAIActor>(actorIndex);

                if (pFriend == null || pFriend == this)
                    continue;

                if (Point_PointSq(lookUp.GetPosition(actorIndex), firePos) > checkRadiusSqr)
                    continue;

                if (pFriend.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_VEHICLE)
                    continue;

                if (pFriend.GetPhysics() == null)
                    continue;

                IAIActorProxy proxy = pFriend.GetProxy();
                if (proxy == null)
                    continue;

                if (proxy.GetLinkedVehicleEntityId() != 0)
                    continue;

                if (IsHostile(pFriend))
                    continue;

                if (IsFriendInLineOfFire(pFriend, firePos, fireDirection, cheapTest))
                {
                    friendOnWay = true;
                    break;
                }
            }
        }

        if (friendOnWay)
            m_friendOnWayElapsedTime += GetAISystem().GetFrameDeltaTime();
        else
            m_friendOnWayElapsedTime = 0.0f;

        return friendOnWay;
    }

    public bool GetSoundPerceptionDescriptor(EAISoundStimType eType, out SSoundPerceptionDescriptor sDescriptor)
    {
        sDescriptor = new SSoundPerceptionDescriptor();
        bool bResult = false;

        if (eType >= 0 && eType < EAISoundStimType.AISOUND_LAST)
        {
            if (m_SoundPerceptionDescriptor.Count == 0)
                sDescriptor = s_DefaultSoundPerceptionDescriptor[(int)eType];
            else
                sDescriptor = m_SoundPerceptionDescriptor[(int)eType];

            bResult = true;
        }

        return bResult;
    }

    public bool SetSoundPerceptionDescriptor(EAISoundStimType eType, SSoundPerceptionDescriptor sDescriptor)
    {
        bool bResult = false;

        if (eType >= 0 && eType < EAISoundStimType.AISOUND_LAST)
        {
            if (m_SoundPerceptionDescriptor.Count == 0)
            {
                // Initialize from defaults
                for (int i = 0; i < (int)EAISoundStimType.AISOUND_LAST; i++)
                    m_SoundPerceptionDescriptor.Add(s_DefaultSoundPerceptionDescriptor[i]);
            }

            m_SoundPerceptionDescriptor[(int)eType] = sDescriptor;
            bResult = true;
        }

        return bResult;
    }

    public virtual IAIObject GetEventOwner(IAIObject pObject)
    {
        IAIObject pOwner = null;

        if (m_pPerceptionHandler != null)
        {
            uint refObject = GetWeakRefSafe((CAIObject)pObject)?.GetObjectID() ?? 0;
            pOwner = m_pPerceptionHandler.GetEventOwner(refObject);
        }

        return pOwner;
    }
    public virtual CAIObject GetEventOwner(CWeakRef<CAIObject> refOwned)
    {
        if (!refOwned.IsValid()) return null;
        return (CAIObject)(m_pPerceptionHandler != null ? m_pPerceptionHandler.GetEventOwner(refOwned.GetObjectID()) : null);
    }

    public bool CheckAndGetFireTarget_Deprecated(IAIObject pTarget, bool lowDamage, out Vec3 vTargetPos, out Vec3 vTargetDir)
    {
        vTargetPos = new Vec3(0, 0, 0);
        vTargetDir = new Vec3(0, 0, 0);

        if (m_pFireCmdHandler == null)
            return false;

        CAIActor pTargetActor = pTarget.CastToCAIActor();
        DamagePartVector pDamageParts = pTargetActor != null ? pTargetActor.GetDamageParts() : null;
        if (pDamageParts != null)
        {
            DamagePartVector parts = pDamageParts;
            List<float> weights = new List<float>();
            List<int> partLut = new List<int>();

            // Check if the parts have multipliers set up.
            float accMult = 0.0f;
            int n = parts.Count;
            for (int i = 0; i < n; ++i)
                accMult += parts[i].damageMult;

            for (int i = 0; i < n; i++) weights.Add(0f);

            if (accMult > 0.001f)
            {
                for (int i = 0; i < n; ++i)
                {
                    weights[i] = parts[i].damageMult * cry_random(1.0f, 1.01f);
                    if (lowDamage && parts[i].damageMult > 0.95f) continue;
                    partLut.Add(i);
                }
            }
            else
            {
                for (int i = 0; i < n; ++i)
                {
                    weights[i] = -parts[i].volume * cry_random(1.0f, 1.01f);
                    partLut.Add(i);
                }
            }

            // Sort partLut by weights
            partLut.Sort((lhs, rhs) => weights[lhs].CompareTo(weights[rhs]));

            foreach (int idx in partLut)
            {
                vTargetPos = parts[idx].pos;
                vTargetDir = vTargetPos - GetFirePos();
                if (m_pFireCmdHandler.ValidateFireDirection(vTargetDir, true))
                    return true;
            }
        }
        else
        {
            // Inanimate target, use the default position.
            vTargetPos = ((CAIObject)pTarget).GetPos();
            vTargetDir = vTargetPos - GetFirePos();

            // The head is accessible.
            if (m_pFireCmdHandler.ValidateFireDirection(vTargetDir, false))
                return true;
        }

        // Can't find any good point, choose a miss point.
        vTargetPos = ChooseMissPoint_Deprecated(vTargetPos);
        return true;
    }

    public virtual Vec3 ChooseMissPoint_Deprecated(Vec3 vTargetPos)
    {
        CCCPOINT("CPuppet_ChooseMissPoint_Deprecated");

        int trysLimit = 5;

        Vec3 dir = vTargetPos - GetFirePos();
        float distToTarget = dir.Length();
        if (distToTarget > 0.00001f)
            dir /= distToTarget;

        Matrix33 mat = Matrix33.CreateRotationVDir(dir);
        Vec3 right = mat.GetColumn0();
        Vec3 up = mat.GetColumn2();
        float spreadHoriz = 0, spreadVert = 0;

        while (--trysLimit >= 0)
        {
            spreadHoriz = cry_random(0.9f, 1.2f) * (cry_random(0, 99) < 50 ? -1.0f : 1.0f);
            spreadVert = cry_random(0.0f, 1.5f) * (cry_random(0, 99) < 50 ? -1.0f : 0.35f);
            Vec3 candidateShootPos = vTargetPos + right * spreadHoriz + up * spreadVert;
            if (m_pFireCmdHandler.ValidateFireDirection(candidateShootPos - GetFirePos(), false))
                return candidateShootPos;
        }
        // can't find any good point to miss
        return new Vec3(0, 0, 0);
    }

    public new void Update(EObjectUpdate type)
    {
        CCCPOINT("CPuppet_Update");

        m_bDryUpdate = (type != EObjectUpdate.AIUPDATE_FULL);

        if (!IsEnabled())
        {
            AILog.AIWarning("CPuppet::Update: Trying to update disabled Puppet: {0}", GetName());
            System.Diagnostics.Debug.Assert(false);
            return;
        }

        IAIActorProxy pAIActorProxy = GetProxy();

        // There should never be Puppets without proxies.
        if (pAIActorProxy == null)
        {
            AILog.AIWarning("CPuppet::Update: Puppet does not have proxy: {0}", GetName());
            System.Diagnostics.Debug.Assert(false);
            return;
        }
        // There should never be Puppets without physics.
        if (GetPhysics() == null)
        {
            AILog.AIWarning("CPuppet::Update: Puppet does not have physics: {0}", GetName());
            System.Diagnostics.Debug.Assert(false);
            return;
        }
        // dead Puppets should never be updated
        if (pAIActorProxy.IsDead())
        {
            AILog.AIWarning("CPuppet::Update: Trying to update dead Puppet: {0}", GetName());
            System.Diagnostics.Debug.Assert(false);
            return;
        }

        base.Update(type);

        UpdateHealthTracking();
        m_damagePartsUpdated = false;

        bool doDryUpdateCall = true;

        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();

        if (!m_bDryUpdate)
        {
            CTimeValue fCurrentTime = GetAISystem().GetFrameStartTime();
            m_fTimePassed = (m_fLastUpdateTime.GetMilliSecondsAsInt64() > 0)
                ? min(0.5f, (fCurrentTime - m_fLastUpdateTime).GetSeconds())
                : 0;
            m_fLastUpdateTime = fCurrentTime;

            bool clearMoveDir = false;

            m_State.Reset(clearMoveDir);

            if (m_bCanReceiveSignals)
            {
                UpdatePuppetInternalState();

                pAttentionTarget = m_refAttentionTarget.GetAIObject();
            }

            GetStateFromActiveGoals(ref m_State);

#if CRYAISYSTEM_DEBUG
            // Store current position to debug stream.
            {
                IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(GetPos());
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_AGENTPOS, ref recorderEventData);
            }

            // Store current direction to debug stream.
            {
                IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(GetViewDir());
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_AGENTDIR, ref recorderEventData);
            }

            // Store current attention target position to debug stream.
            if (pAttentionTarget != null)
            {
                IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(pAttentionTarget.GetPos());
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_ATTENTIONTARGETPOS, ref recorderEventData);
            }
#endif

            // Update last known target position
            if (pAttentionTarget != null && (pAttentionTarget.IsAgent() || (pAttentionTarget.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_TARGET)))
            {
                m_lastLiveTargetPos = pAttentionTarget.GetPos();
                m_timeSinceLastLiveTarget = 0.0f;
            }
            else
            {
                if (m_timeSinceLastLiveTarget >= 0.0f)
                    m_timeSinceLastLiveTarget += m_fTimePassed;
            }

            m_lightLevel = GetAISystem().GetLightManager().GetLightLevelAt(GetPos(), this, ref m_usingCombatLight);
        }
        else if (doDryUpdateCall)
        {
            for (int i = 0; i < m_vActiveGoals.Count; i++)
            {
                QGoal Goal = m_vActiveGoals[i];
                Goal.pGoalOp.ExecuteDry(this);
            }
        }

        if (m_delayedStance != (int)EStance.STANCE_NULL)
        {
            bool wantsToMove = !m_State.vMoveDir.IsZero() && m_State.fDesiredSpeed > 0.0f;
            if (wantsToMove)
            {
                m_delayedStanceMovementCounter++;
                if (m_delayedStanceMovementCounter > 3)
                {
                    m_State.bodystate = m_delayedStance;
                    m_delayedStance = (int)EStance.STANCE_NULL;
                    m_delayedStanceMovementCounter = 0;
                }
            }
            else
            {
                m_delayedStanceMovementCounter = 0;
            }
        }

        HandleNavSOFailure();
        SyncActorTargetPhaseWithAIProxy();

        if (m_paused == 0)
        {
            if (GetSubType() == ESubType.STP_2D_FLY)
                UpdateLookTarget3D(pAttentionTarget);
            else
                UpdateLookTarget(pAttentionTarget);
        }

        m_State.vBodyTargetDir = m_vBodyTargetDir;
        m_State.vDesiredBodyDirectionAtTarget = m_vDesiredBodyDirectionAtTarget;
        m_State.movementContext = m_movementContext;

        if (pAttentionTarget != null)
        {
            m_State.nTargetType = pAttentionTarget.GetType();
            m_State.bTargetEnabled = pAttentionTarget.IsEnabled();
        }
        else
        {
            m_State.nTargetType = -1;
            m_State.bTargetEnabled = false;
            m_State.eTargetThreat = EAITargetThreat.AITHREAT_NONE;
            m_State.eTargetType = EAITargetType.AITARGET_NONE;
        }

        float dt = GetAISystem().GetFrameDeltaTime();

        m_targetDazzlingTime = max(0.0f, m_targetDazzlingTime - dt);
        if (GetAttentionTargetType() == EAITargetType.AITARGET_VISUAL && GetAttentionTargetThreat() == EAITargetThreat.AITHREAT_AGGRESSIVE)
        {
            m_targetLostTime = 0.0f;
            m_targetSeenTime = min(m_targetSeenTime + dt, 10.0f);
        }
        else
        {
            m_targetLostTime += dt;
            m_targetSeenTime = max(0.0f, m_targetSeenTime - dt);
        }

        FireCommand(gEnv.pTimer.GetFrameTime());

        if (m_Parameters.m_fAwarenessOfPlayer > 0)
            CheckAwarenessPlayer();

        // Time out unreachable hidepoints.
        {
            var node = m_recentUnreachableHideObjects.Last;
            while (node != null)
            {
                var prev = node.Previous;
                var entry = node.Value;
                entry.Item1 -= GetAISystem().GetFrameDeltaTime();
                if (entry.Item1 < 0.0f)
                    m_recentUnreachableHideObjects.Remove(node);
                else
                    node.Value = entry;
                node = prev;
            }
        }

        UpdateCloakScale();

        m_State.vForcedNavigation = m_vForcedNavigation;
        m_State.fForcedNavigationSpeed = m_fForcedNavigationSpeed;
    }
    public override void UpdateProxy(EObjectUpdate type)
    {
        IAIActorProxy pAIActorProxy = GetProxy();
        if (pAIActorProxy == null)
        {
            AILog.AIWarning("CPuppet::UpdateProxy: Puppet does not have proxy: {0}", GetName());
            return;
        }

        SetMoveDir(m_State.vMoveDir);

        if (gAIEnv.CVars.UpdateProxy != 0)
        {
            bool forcedPosture = false;
            if (gAIEnv.CVars.ForcePosture != 0)
            {
                PostureManager.PostureInfo posture;

                if (m_postureManager.GetPostureByName(gAIEnv.CVars.ForcePosture.ToString(), out posture))
                {
                    forcedPosture = true;

                    m_State.bodystate = (int)posture.stance;
                    m_State.lean = posture.lean;
                    m_State.peekOver = posture.peekOver;

                    if (!string.IsNullOrEmpty(posture.agInput))
                        pAIActorProxy.SetAGInput(AIAG_ACTION, posture.agInput);
                    else
                        pAIActorProxy.ResetAGInput(AIAG_ACTION);
                }
            }

            if (!forcedPosture)
            {
                if (gAIEnv.CVars.ForceStance > -1)
                    m_State.bodystate = gAIEnv.CVars.ForceStance;

                if (gAIEnv.CVars.ForceAGAction != null && gAIEnv.CVars.ForceAGAction != "0")
                    pAIActorProxy.SetAGInput(AIAG_ACTION, gAIEnv.CVars.ForceAGAction);
            }

            if (gAIEnv.CVars.ForceAGSignal != null && gAIEnv.CVars.ForceAGSignal != "0")
                pAIActorProxy.SetAGInput(AIAG_SIGNAL, gAIEnv.CVars.ForceAGSignal);

            if (gAIEnv.CVars.ForceAllowStrafing > -1)
                m_State.allowStrafing = gAIEnv.CVars.ForceAllowStrafing != 0;

            string forceLookAimTarget = gAIEnv.CVars.ForceLookAimTarget;
            if (forceLookAimTarget != null && forceLookAimTarget != "none")
            {
                Vec3 targetPos = GetPos();
                if (forceLookAimTarget == "x")
                    targetPos += new Vec3(10, 0, 0);
                else if (forceLookAimTarget == "y")
                    targetPos += new Vec3(0, 10, 0);
                else if (forceLookAimTarget == "xz")
                    targetPos += new Vec3(10, 0, 10);
                else if (forceLookAimTarget == "yz")
                    targetPos += new Vec3(0, 10, 10);
                else
                {
                    IEntity pEntity = gEnv.pEntitySystem?.FindEntityByName(forceLookAimTarget);
                    if (pEntity != null)
                        targetPos = pEntity.GetPos();
                    else
                        targetPos = new Vec3(0, 0, 0);
                }

                m_State.vLookTargetPos = targetPos;
                m_State.vAimTargetPos = targetPos;
                m_State.aimTargetIsValid = true;
            }

            if (!m_bDryUpdate)
            {
                if (m_bGrenadeThrowRequested && (m_State.fireSecondary != EAIFireState.eAIFS_Blocking))
                    m_bGrenadeThrowRequested = false;
            }

            pAIActorProxy.Update(m_State, !m_bDryUpdate);

            if (IsEnabled())
                UpdateAlertness();
        }
    }
    public virtual void Devalue(IAIObject pObject, bool bDevaluePuppets, float fAmount = 20.0f)
    {
        ushort type = ((CAIObject)pObject).GetType();

        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();
        CWeakRef<CAIObject> refObject = GetWeakRefSafe((CAIObject)pObject);

        if (pObject == pAttentionTarget)
            SetAttentionTarget(NILREF);

        if ((type == (ushort)CryAISystem.EAIObjectType.AIOBJECT_ACTOR) && !bDevaluePuppets)
            return;

        if (type == (ushort)CryAISystem.EAIObjectType.AIOBJECT_PLAYER)
            return;

        CCCPOINT("CPuppet_Devalue");

        // remove it from map of pending events, so that it does not get reacquired
        if (m_pPerceptionHandler != null)
            m_pPerceptionHandler.RemoveEvent(refObject);

        if (!m_mapDevaluedPoints.ContainsKey(refObject))
            m_mapDevaluedPoints[refObject] = fAmount;
    }
    public override bool IsDevalued(IAIObject pObject)
    {
        CWeakRef<CAIObject> refObject = GetWeakRefSafe((CAIObject)pObject);
        return m_mapDevaluedPoints.ContainsKey(refObject);
    }
    public override void ClearDevalued()
    {
        m_mapDevaluedPoints.Clear();
    }

    public override void SetParameters(AgentParameters sParams)
    {
        AILog.AILogComment("CPuppet::SetParameters {0} ({1})", GetName(), this);
        base.SetParameters(sParams);
    }
    public override void Event(ushort eType, SAIEVENT pEvent)
    {
        CAISystem pAISystem = GetAISystem();
        bool bWasEnabled = m_bEnabled;

        switch (eType)
        {
            case (ushort)EAIEvent.AIEVENT_DROPBEACON:
                UpdateBeacon();
                break;
            case (ushort)EAIEvent.AIEVENT_CLEAR:
                {
                    CCCPOINT("CPuppet_Event_Clear");

                    ClearActiveGoals();
                    m_bLooseAttention = false;
                    pAISystem.FreeFormationPoint(GetWeakRef(this));
                    SetAttentionTarget(NILREF);
                    m_bBlocked = false;
                    m_bCanReceiveSignals = true;
                }
                break;
            case (ushort)EAIEvent.AIEVENT_CLEARACTIVEGOALS:
                ClearActiveGoals();
                m_bBlocked = false;
                break;
            case (ushort)EAIEvent.AIEVENT_DISABLE:
                {
                    m_bEnabled = false;

                    // Reset and disable the agent's target track
                    uint aiObjectId = GetAIObjectID();
                    gAIEnv.pTargetTrackManager?.ResetAgent(aiObjectId);
                    gAIEnv.pTargetTrackManager?.SetAgentEnabled(aiObjectId, false);

                    pAISystem.UpdateGroupStatus(GetGroupId());
                    pAISystem.NotifyEnableState(this, m_bEnabled);

                    SetObserver(false);
                    SetObservable(false);

                    SetNavSOFailureStates();
                }
                break;
            case (ushort)EAIEvent.AIEVENT_ENABLE:
                if (GetProxy().IsDead())
                    return;
                m_bEnabled = true;
                gAIEnv.pTargetTrackManager?.SetAgentEnabled(GetAIObjectID(), true);
                pAISystem.UpdateGroupStatus(GetGroupId());
                pAISystem.NotifyEnableState(this, m_bEnabled);

                SetObserver(true);
                SetObservable(true);
                break;
            case (ushort)EAIEvent.AIEVENT_SLEEP:
                m_fireMode = EFireMode.FIREMODE_OFF;
                m_bCheckedBody = false;
                if (GetProxy().GetLinkedVehicleEntityId() == 0)
                {
                    m_bEnabled = false;
                    pAISystem.NotifyEnableState(this, m_bEnabled);
                }
                break;
            case (ushort)EAIEvent.AIEVENT_WAKEUP:
                ClearActiveGoals();
                m_bLooseAttention = false;
                SetAttentionTarget(NILREF);
                m_bEnabled = true;
                pAISystem.NotifyEnableState(this, m_bEnabled);
                m_bCheckedBody = true;
                pAISystem.UpdateGroupStatus(GetGroupId());
                break;
            case (ushort)EAIEvent.AIEVENT_ONVISUALSTIMULUS:
                HandleVisualStimulus(pEvent);
                break;
            case (ushort)EAIEvent.AIEVENT_ONSOUNDEVENT:
                HandleSoundEvent(pEvent);
                break;
            case (ushort)EAIEvent.AIEVENT_ONBULLETRAIN:
                HandleBulletRain(pEvent);
                break;
            case (ushort)EAIEvent.AIEVENT_AGENTDIED:
                {
                    SetNavSOFailureStates();

                    if (m_inCover || m_movingToCover)
                    {
                        SetCoverRegister(new CoverID());
                        m_coverUser.SetCoverID(new CoverID());
                    }

                    ResetBehaviorSelectionTree(EObjectResetType.AIOBJRESET_SHUTDOWN);

                    pAISystem.NotifyTargetDead(this);

                    m_bCheckedBody = false;
                    m_bEnabled = false;
                    pAISystem.NotifyEnableState(this, m_bEnabled);

                    pAISystem.RemoveFromGroup(GetGroupId(), this);

                    pAISystem.ReleaseFormationPoint(this);
                    CancelRequestedPath(false);
                    ReleaseFormation();

                    ResetCurrentPipe(true);
                    m_State.ClearSignals();

                    ResetAlertness();

                    uint killerID = pEvent.targetId?.GetId() ?? 0;
                    pAISystem.OnAgentDeath(GetEntityID(), killerID);

                    if (GetProxy() != null)
                        GetProxy().Reset(EObjectResetType.AIOBJRESET_SHUTDOWN);

                    SetObservable(false);
                    SetObserver(false);
                }
                break;
            case (ushort)EAIEvent.AIEVENT_FORCEDNAVIGATION:
                m_vForcedNavigation = pEvent.vForcedNavigation;
                break;
            case (ushort)EAIEvent.AIEVENT_ADJUSTPATH:
                m_adjustpath = pEvent.nType;
                break;
            default:
                break;
        }

        // Activate tree if enabled state is changing
        if (bWasEnabled != m_bEnabled)
        {
            // remove from alertness counters
            ResetAlertness();
        }
    }
    public override void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true)
    {
        CCCPOINT("CPuppet_ParseParameters");
        base.ParseParameters(parameters, bParseMovementParams);
    }
    public override bool CreateFormation(string szName, Vec3 vTargetPos = default)
    {
        if (string.Equals(szName, "beacon", System.StringComparison.OrdinalIgnoreCase))
        {
            UpdateBeacon();
            return (m_pFormation != null);
        }
        return base.CreateFormation(szName, vTargetPos);
    }
    public override void Serialize(TSerialize ser)
    {
        ser.BeginGroup("AIPuppet");
        {
            base.Serialize(ser);

            ser.Value("m_bCanReceiveSignals", ref m_bCanReceiveSignals);

            ser.Value("m_targetApproach", ref m_targetApproach);
            ser.Value("m_targetFlee", ref m_targetFlee);
            ser.Value("m_targetApproaching", ref m_targetApproaching);
            ser.Value("m_targetFleeing", ref m_targetFleeing);
            ser.Value("m_lastTargetValid", ref m_lastTargetValid);
            ser.Value("m_lastTargetPos", ref m_lastTargetPos);
            ser.Value("m_lastTargetSpeed", ref m_lastTargetSpeed);
            ser.Value("m_fLastUpdateTime", ref m_fLastUpdateTime);

            ser.Value("m_allowedToHitTarget", ref m_allowedToHitTarget);
            ser.Value("m_bCoverFireEnabled", ref m_bCoverFireEnabled);
            ser.Value("m_firingReactionTimePassed", ref m_firingReactionTimePassed);
            ser.Value("m_firingReactionTime", ref m_firingReactionTime);
            ser.Value("m_outOfAmmoTimeOut", ref m_outOfAmmoTimeOut);
            ser.Value("m_allowedToUseExpensiveAccessory", ref m_allowedToUseExpensiveAccessory);

            ser.Value("m_adaptiveUrgencyMin", ref m_adaptiveUrgencyMin);
            ser.Value("m_adaptiveUrgencyMax", ref m_adaptiveUrgencyMax);
            ser.Value("m_adaptiveUrgencyScaleDownPathLen", ref m_adaptiveUrgencyScaleDownPathLen);
            ser.Value("m_adaptiveUrgencyMaxPathLen", ref m_adaptiveUrgencyMaxPathLen);
            ser.Value("m_chaseSpeed", ref m_chaseSpeed);
            ser.Value("m_lastChaseUrgencyDist", ref m_lastChaseUrgencyDist);
            ser.Value("m_lastChaseUrgencySpeed", ref m_lastChaseUrgencySpeed);
            ser.Value("m_chaseSpeedRate", ref m_chaseSpeedRate);
            ser.Value("m_delayedStance", ref m_delayedStance);
            ser.Value("m_delayedStanceMovementCounter", ref m_delayedStanceMovementCounter);

            ser.Value("m_bGrenadeThrowRequested", ref m_bGrenadeThrowRequested);
            ser.EnumValue("m_eGrenadeThrowRequestType", ref m_eGrenadeThrowRequestType);
            ser.Value("m_iGrenadeThrowTargetType", ref m_iGrenadeThrowTargetType);

            ser.Value("m_allowedStrafeDistanceStart", ref m_allowedStrafeDistanceStart);
            ser.Value("m_allowedStrafeDistanceEnd", ref m_allowedStrafeDistanceEnd);
            ser.Value("m_allowStrafeLookWhileMoving", ref m_allowStrafeLookWhileMoving);
            ser.Value("m_strafeStartDistance", ref m_strafeStartDistance);

            // Serialize devalued map
            SerializeWeakRefMap(ser, "devaluedPoints", m_mapDevaluedPoints);

            int playerAwarenessInt = (int)m_playerAwarenessType;
            ser.Value("m_playerAwarenessType", ref playerAwarenessInt);
            if (ser.IsReading())
                m_playerAwarenessType = (TPlayerActionType)playerAwarenessInt;
            ser.Value("m_fLastTimeAwareOfPlayer", ref m_fLastTimeAwareOfPlayer);
            ser.Value("m_vForcedNavigation", ref m_vForcedNavigation);
            ser.Value("m_fForcedNavigationSpeed", ref m_fForcedNavigationSpeed);

            ser.Value("m_vehicleStickTarget", ref m_vehicleStickTarget);

            ser.Value("m_alarmedTime", ref m_alarmedTime);

            ser.BeginGroup("InitialPath");
            {
                int pointCount = m_InitialPath.Count;
                ser.Value("pointCount", ref pointCount);
                Vec3 point = new Vec3(0, 0, 0);
                if (ser.IsReading())
                {
                    m_InitialPath.Clear();
                    for (int i = 0; i < pointCount; i++)
                    {
                        string name = $"Point_{i}";
                        ser.Value(name, ref point);
                        m_InitialPath.Add(point);
                    }
                }
                else
                {
                    int i = 0;
                    foreach (Vec3 pt in m_InitialPath)
                    {
                        string name = $"Point_{i}";
                        point = pt;
                        ser.Value(name, ref point);
                        i++;
                    }
                }
            }
            ser.EndGroup();

            ser.Value("m_vehicleAvoidingTime", ref m_vehicleAvoidingTime);
            m_refAvoidedVehicle.Serialize(ser, "M_refAvoidedVehicle");

            // Target tracking
            ser.BeginGroup("TargetTracking");
            {
                if (ser.IsReading())
                {
                    m_lastMissShotsCount = m_lastHitShotsCount = m_lastTargetPart = ~(nuint)0;

                    m_targetSilhouette.Reset();
                    m_targetLastMissPoint = new Vec3(0, 0, 0);
                    m_targetFocus = 0.0f;
                    m_targetZone = EAITargetZone.AIZONE_OUT;
                    m_targetDistanceToSilhouette = float.MaxValue;
                    m_targetBiasDirection = new Vec3(0, 0, -1);
                    m_targetEscapeLastMiss = 0.0f;
                    m_burstEffectTime = 0.0f;
                    m_burstEffectState = 0;
                }

                ser.Value("m_targetDamageHealthThr", ref m_targetDamageHealthThr);
                ser.Value("m_targetSeenTime", ref m_targetSeenTime);
                ser.Value("m_targetLostTime", ref m_targetLostTime);
                ser.Value("m_targetDazzlingTime", ref m_targetDazzlingTime);
            }
            ser.EndGroup();
        }
        ser.EndGroup();

        if (ser.IsReading())
        {
            GetAISystem().NotifyEnableState(this, m_bEnabled);
            m_steeringOccupancy.Reset(new Vec3(0, 0, 0), new Vec3(0, 1, 0), 1.0f);
            m_steeringOccupancyBias = 0;
            m_steeringAdjustTime = 0;

            m_currentWeaponId = 0;
            m_CurrentWeaponDescriptor = new AIWeaponDescriptor();
        }

        if (ser.IsReading())
        {
            uint visionId = GetVisionID();
            if (visionId != 0)
            {
                IVisionMap visionMap = gEnv.pAISystem?.GetVisionMap();
                if (visionMap != null)
                {
                    if (IsObserver())
                    {
                        ObserverParams observerParams = new ObserverParams();
                        observerParams.typeMask = (uint)(VisionMapTypes.General | VisionMapTypes.AliveAgent);
                        visionMap.ObserverChanged(visionId, observerParams, eChangedTypeMask);
                    }

                    if (IsObservable())
                    {
                        ObservableParams observableParams = new ObservableParams();
                        observableParams.typeMask = (uint)(VisionMapTypes.General | VisionMapTypes.AliveAgent);
                        observableParams.userData = GetEntityID();
                        visionMap.ObservableChanged(visionId, observableParams, eChangedTypeMask | eChangedUserData);
                    }
                }
            }
        }
    }
    public override void PostSerialize()
    {
        base.PostSerialize();

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.PostSerialize();
        }

        // Query the correct weapon fire descriptor to use
        QueryCurrentWeaponDescriptor();
    }
    public override void SetPFBlockerRadius(int blockerType, float radius)
    {
        m_PFBlockers[blockerType] = radius;
    }

    public override void OnObjectRemoved(CAIObject pObject)
    {
        base.OnObjectRemoved(pObject);

        if (m_pPerceptionHandler != null)
            m_pPerceptionHandler.RemoveEvent(GetWeakRef(pObject));

        if (m_steeringObjects.Count > 0)
            m_steeringObjects.Remove(pObject);
    }
    public override void Reset(EObjectResetType type)
    {
        AILog.AILogComment("CPuppet::Reset {0} ({1})", GetName(), this);

        base.Reset(type); // creates the proxy

        m_bCanReceiveSignals = true;

        LineOfFireState freshLOF = new LineOfFireState();
        freshLOF.Swap(ref m_lineOfFireState);
        ValidTargetState freshVT = new ValidTargetState();
        m_validTargetState = freshVT;

        CAISystem pAISystem = GetAISystem();

        m_steeringOccupancy.Reset(new Vec3(0, 0, 0), new Vec3(0, 1, 0), 1.0f);
        m_steeringOccupancyBias = 0;
        m_steeringAdjustTime = 0;

        m_mapDevaluedPoints.Clear();

        m_steeringObjects.Clear();

        ClearPotentialTargets();
        m_fLastUpdateTime = new CTimeValue(0.0f);

        m_bDryUpdate = false;

        m_fLastNavTest = 0.0f;

        // Reset target movement tracking
        m_targetApproaching = false;
        m_targetFleeing = false;
        m_targetApproach = 0;
        m_targetFlee = 0;
        m_lastTargetValid = false;
        m_allowedStrafeDistanceStart = 0.0f;
        m_allowedStrafeDistanceEnd = 0.0f;
        m_allowStrafeLookWhileMoving = false;
        m_strafeStartDistance = 0;
        m_closeRangeStrafing = false;

        m_currentWeaponId = 0;
        m_CurrentWeaponDescriptor = new AIWeaponDescriptor();

        m_bGrenadeThrowRequested = false;
        m_eGrenadeThrowRequestType = ERequestedGrenadeType.eRGT_INVALID;
        m_iGrenadeThrowTargetType = 0;

        m_lastAimObstructionResult = true;
        m_updatePriority = EPuppetUpdatePriority.AIPUP_LOW;

        m_adaptiveUrgencyMin = 0.0f;
        m_adaptiveUrgencyMax = 0.0f;
        m_adaptiveUrgencyScaleDownPathLen = 0.0f;
        m_adaptiveUrgencyMaxPathLen = 0.0f;

        m_delayedStance = (int)EStance.STANCE_NULL;
        m_delayedStanceMovementCounter = 0;

        m_timeSinceTriggerPressed = 0.0f;
        m_friendOnWayElapsedTime = 0.0f;

        if (m_pFireCmdHandler != null)
            m_pFireCmdHandler.Reset();
        if (m_pFireCmdGrenade != null)
            m_pFireCmdGrenade.Reset();

        m_PFBlockers.Clear();

        m_CurrentHideObject.Set(null, new Vec3(0, 0, 0), new Vec3(0, 0, 0));
        m_InitialPath.Clear();

        SetAvoidedVehicle(new CWeakRef<CAIVehicle>());
        // make sure i'm added to groupsMap
        pAISystem.AddToGroup(this, GetGroupId());

        // Initially allowed to hit target if not using ambient fire system
        bool bAmbientFireEnabled = (gAIEnv.CVars.AmbientFireEnable != 0);
        m_allowedToHitTarget = !bAmbientFireEnabled;

        m_allowedToUseExpensiveAccessory = false;
        m_firingReactionTimePassed = false;
        m_firingReactionTime = 0.0f;
        m_outOfAmmoTimeOut = 0.0f;

        m_currentNavSOStates.Clear();
        m_pendingNavSOStates.Clear();

        m_targetSilhouette.Reset();
        m_targetLastMissPoint = new Vec3(0, 0, 0);
        m_targetFocus = 0.0f;
        m_targetZone = EAITargetZone.AIZONE_OUT;
        m_targetPosOnSilhouettePlane = new Vec3(0, 0, 0);
        m_targetDistanceToSilhouette = float.MaxValue;
        m_targetBiasDirection = new Vec3(0, 0, -1);
        m_targetEscapeLastMiss = 0.0f;
        m_targetSeenTime = 0;
        m_targetLostTime = 0;

        m_alarmedTime = 0.0f;
        m_alarmedLevel = 0.0f;

        m_targetDamageHealthThr = -1.0f;
        m_burstEffectTime = 0.0f;
        m_burstEffectState = 0;
        m_targetDazzlingTime = 0.0f;

        if (m_targetDamageHealthThrHistory != null)
            m_targetDamageHealthThrHistory.Reset();

        m_vForcedNavigation = new Vec3(0, 0, 0);
        m_fForcedNavigationSpeed = 0.0f;

        m_vehicleStickTarget = 0;

        m_lastMissShotsCount = ~(nuint)0;
        m_lastHitShotsCount = ~(nuint)0;
        m_lastTargetPart = ~(nuint)0;

        m_damagePartsUpdated = false;

        // Default perception descriptors
        m_SoundPerceptionDescriptor.Clear();

        m_fireDisabled = 0;

        ResetAlertness();
    }
    public override void GetPathAgentNavigationBlockers(NavigationBlockers navigationBlockers, PathfindRequest pRequest)
    {
        CCCPOINT("CPuppet_AddNavigationBlockers");
        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();

        float cost = 5.0f;
        bool radialDecay = true;
        bool directional = true;

        float curRadius;
        float sign;

        // Attention target
        curRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_ATT_TARGET) ? m_PFBlockers[(int)ENavigationBlockers.PFB_ATT_TARGET] : 0.0f;
        sign = 1.0f;
        if (curRadius < 0.0f) { sign = -1.0f; curRadius = -curRadius; }
        if (curRadius > 0.0f && pAttentionTarget != null && IsHostile(pAttentionTarget))
        {
            float r = curRadius;
            if (pRequest != null)
            {
                float extra = 1.5f;
                float d1 = extra * Point_Point(pAttentionTarget.GetPos(), pRequest.startPos);
                float d2 = extra * Point_Point(pAttentionTarget.GetPos(), pRequest.endPos);
                r = min(min(d1, d2), curRadius);
            }
            navigationBlockers.Add(new NavigationBlocker(pAttentionTarget.GetPos(), r * sign, 0.0f, cost, radialDecay, directional));
        }

        // Player
        curRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_PLAYER) ? m_PFBlockers[(int)ENavigationBlockers.PFB_PLAYER] : 0.0f;
        sign = 1.0f;
        if (curRadius < 0.0f) { sign = -1.0f; curRadius = -curRadius; }
        if (curRadius > 0.0f)
        {
            CAIPlayer pPlayer = CastToCAIPlayerSafe(GetAISystem().GetPlayer());
            if (pPlayer != null)
            {
                navigationBlockers.Add(new NavigationBlocker(pPlayer.GetPos() + pPlayer.GetEntityDir() * curRadius / 2, curRadius * sign, 0.0f, cost, radialDecay, directional));
            }
        }

        // Between nav target
        curRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_BETWEEN_NAV_TARGET) ? m_PFBlockers[(int)ENavigationBlockers.PFB_BETWEEN_NAV_TARGET] : 0.0f;
        sign = 1.0f;
        if (curRadius < 0.0f) { sign = -1.0f; curRadius = -curRadius; }
        if (curRadius > 0.0f && pRequest != null)
        {
            float biasTowardsTarget = 0.7f;
            Vec3 mid = pRequest.endPos * biasTowardsTarget + GetPos() * (1 - biasTowardsTarget);
            curRadius = min(curRadius, Point_Point(pRequest.endPos, GetPos()) * 0.8f);
            navigationBlockers.Add(new NavigationBlocker(mid, curRadius * sign, 0.0f, cost, radialDecay, directional));
        }

        // Ref point
        curRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_REF_POINT) ? m_PFBlockers[(int)ENavigationBlockers.PFB_REF_POINT] : 0.0f;
        sign = 1.0f;
        if (curRadius < 0.0f) { sign = -1.0f; curRadius = -curRadius; }
        if (curRadius > 0.0f)
        {
            Vec3 vRefPointPos = GetRefPoint()?.GetPos() ?? new Vec3(0, 0, 0);
            float r = curRadius;
            if (pRequest != null)
            {
                float extra = 1.5f;
                float d1 = extra * Point_Point(vRefPointPos, pRequest.startPos);
                float d2 = extra * Point_Point(vRefPointPos, pRequest.endPos);
                r = min(min(d1, d2), curRadius);
            }
            navigationBlockers.Add(new NavigationBlocker(vRefPointPos, r, 0.0f, cost * sign, radialDecay, directional));
        }

        // Beacon
        curRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_BEACON) ? m_PFBlockers[(int)ENavigationBlockers.PFB_BEACON] : 0.0f;
        sign = 1.0f;
        if (curRadius < 0.0f) { sign = -1.0f; curRadius = -curRadius; }
        IAIObject pBeacon = GetAISystem().GetBeacon(GetGroupId());
        if (curRadius > 0.0f && pBeacon != null)
        {
            float r = curRadius;
            if (pRequest != null)
            {
                float extra = 1.5f;
                float d1 = extra * Point_Point(pBeacon.GetPos(), pRequest.startPos);
                float d2 = extra * Point_Point(pBeacon.GetPos(), pRequest.endPos);
                r = min(min(d1, d2), curRadius);
            }
            navigationBlockers.Add(new NavigationBlocker(pBeacon.GetPos(), r, 0.0f, cost * sign, radialDecay, directional));
        }

        // Dead bodies
        float deadRadius = 0.0f;
        bool ignoreDeadBodies = false;
        if (!ignoreDeadBodies)
        {
            deadRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_DEAD_BODIES) ? m_PFBlockers[(int)ENavigationBlockers.PFB_DEAD_BODIES] : 0.0f;
        }

        float explosiveRadius = m_PFBlockers.ContainsKey((int)ENavigationBlockers.PFB_EXPLOSIVES) ? m_PFBlockers[(int)ENavigationBlockers.PFB_EXPLOSIVES] : 0.0f;

        if (fabsf(deadRadius) > 0.01f || fabsf(explosiveRadius) > 0.01f)
        {
            uint maxn = 3;
            Vec3[] positions = new Vec3[maxn];
            uint[] types = new uint[maxn];
            uint n = GetAISystem().GetDangerSpots(this, 40.0f, positions, types, maxn, CAISystem.DANGER_ALL);
            for (uint i = 0; i < n; i++)
            {
                float r = explosiveRadius;
                if (types[i] == CAISystem.DANGER_DEADBODY)
                {
                    if (ignoreDeadBodies)
                        continue;
                    r = deadRadius;
                }

                if (r < 0.0f && Point_PointSq(GetPos(), positions[i]) < sqr(fabsf(r) + 2.0f))
                    continue;
                sign = 1.0f;
                if (r < 0.0f) { sign = -1.0f; r = -r; }
                navigationBlockers.Add(new NavigationBlocker(positions[i], r, 0.0f, cost * sign, radialDecay, directional));
            }
        }
    }

    public virtual float GetDistanceAlongPath(Vec3 pos, bool bInit)
    {
        Vec3 myPos = (GetEntity() != null ? GetEntity().GetPos() : GetPos());

        if (m_nPathDecision != EPathDecision.PATHFINDER_PATHFOUND)
            return 0;

        if (bInit)
        {
            m_InitialPath.Clear();
            foreach (PathPointDescriptor pp in m_OrigPath.GetPath())
                m_InitialPath.Add(pp.vPos);
        }
        if (m_InitialPath.Count > 0)
        {
            float mindist = 10000000.0f;
            float mindistObj = 10000000.0f;
            int liMyIdx = -1;
            int liObjIdx = -1;
            int count = 0;
            int myCount = 0;
            int objCount = 0;
            float objCoeff = 0;
            int maxCount = m_InitialPath.Count - 1;

            for (int idx = 0; idx < maxCount; idx++)
            {
                Vec3 segStart = m_InitialPath[idx];
                Vec3 segEnd = m_InitialPath[idx + 1];
                float t, u;
                float distObj = CryAISystem.CryCommon.Distance.Point_Lineseg(pos, new CryAISystem.CryCommon.Lineseg(segStart, segEnd), out t);
                float mydist = CryAISystem.CryCommon.Distance.Point_Lineseg(myPos, new CryAISystem.CryCommon.Lineseg(segStart, segEnd), out u);
                if (distObj < mindistObj && ((count == 0 && t < 0) || (t >= 0 && t <= 1) || (count == maxCount && t > 1)))
                {
                    liObjIdx = idx;
                    mindistObj = distObj;
                    objCount = count;
                    objCoeff = t;
                }
                if (mydist < mindist && ((count == 0 && u < 0) || (u >= 0 && u <= 1) || (count == maxCount && u > 1)))
                {
                    liMyIdx = idx;
                    mindist = mydist;
                    myCount = count;
                }
                count++;
            }

            // check if object is outside the path
            if (objCoeff <= 0 && objCount == 0)
                return Point_Point(pos, m_InitialPath[0]) + Point_Point(myPos, m_InitialPath[0]);
            else if (objCoeff >= 1 && objCount >= count - 1)
                return -(Point_Point(pos, m_InitialPath[m_InitialPath.Count - 1]) + Point_Point(myPos, m_InitialPath[m_InitialPath.Count - 1]));

            if (liMyIdx >= 0 && liObjIdx >= 0 && liMyIdx != liObjIdx)
            {
                if (objCount > myCount)
                {
                    // other object is ahead
                    float dist = Point_Point(m_InitialPath[liObjIdx], pos);
                    int startIdx = liMyIdx + 1;
                    if (startIdx < m_InitialPath.Count)
                        dist += Point_Point(m_InitialPath[startIdx], myPos);

                    for (int idx = startIdx; idx < liObjIdx && idx + 1 < m_InitialPath.Count; idx++)
                        dist += Point_Point(m_InitialPath[idx], m_InitialPath[idx + 1]);

                    return -dist;
                }
                else
                {
                    // other object is back
                    float dist = Point_Point(m_InitialPath[liMyIdx], myPos);
                    int startIdx = liObjIdx + 1;
                    if (startIdx < m_InitialPath.Count)
                        dist += Point_Point(m_InitialPath[startIdx], pos);

                    for (int idx = startIdx; idx < liMyIdx && idx + 1 < m_InitialPath.Count; idx++)
                        dist += Point_Point(m_InitialPath[idx], m_InitialPath[idx + 1]);

                    return dist;
                }
            }
            // check just positions, object is on the same path segment
            Vec3 myOrientation = new Vec3(0, 0, 0);
            if (GetPhysics() != null)
            {
                pe_status_dynamics dSt = new pe_status_dynamics();
                GetPhysics().GetStatus(dSt);
                myOrientation = dSt.v;
            }
            if (myOrientation.IsEquivalent(new Vec3(0, 0, 0)))
                myOrientation = GetViewDir();

            Vec3 dir = pos - myPos;
            float distFinal = dir.GetLength();

            return (myOrientation.Dot(dir) < 0 ? distFinal : -distFinal);
        }
        // no path
        return 0;
    }

    public bool GetPotentialTargets(PotentialTargetMap targetMap)
    {
        bool bResult = false;

        if (gAIEnv.CVars.TargetTracking != 0)
        {
            CWeakRef<CAIObject> refBestTarget;
            SAIPotentialTarget bestTargetEvent;
            bool currentTargetErased;

            if (GetTargetTrackBestTarget(out refBestTarget, out bestTargetEvent, out currentTargetErased) && bestTargetEvent != null)
            {
                targetMap.Insert(refBestTarget, bestTargetEvent);
                bResult = true;
            }
        }
        else
        {
            bResult = false;
        }

        return bResult;
    }

    public uint GetBestTargets(uint[] targets, uint maxCount)
    {
        uint myID = GetAIObjectID();
        gAIEnv.pTargetTrackManager?.Update(myID);

        var resultList = new System.Collections.Generic.List<CWeakRef<CAIObject>>();
        bool ok = gAIEnv.pTargetTrackManager?.GetBestTargets(myID, (int)maxCount, resultList, TargetTrackHelpers.eDTM_Select_Highest) ?? false;
        if (!ok) return 0;
        uint count = 0;
        foreach (var wr in resultList)
        {
            if (count >= maxCount) break;
            targets[count++] = wr?.GetObjectID() ?? 0;
        }
        return count;
    }

    public bool AddAggressiveTarget(IAIObject pTarget)
    {
        bool bResult = false;

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.AddAggressiveTarget(pTarget);
            bResult = true;
        }

        return bResult;
    }
    public bool SetTempTargetPriority(ETempTargetPriority priority)
    {
        bool bResult = false;

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.SetTempTargetPriority(priority);
            bResult = true;
        }

        return bResult;
    }
    public bool UpdateTempTarget(Vec3 vPosition)
    {
        bool bResult = false;

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.UpdateTempTarget(vPosition);
            bResult = true;
        }

        return bResult;
    }
    public bool ClearTempTarget()
    {
        bool bResult = false;

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.ClearTempTarget();
            bResult = true;
        }

        return bResult;
    }
    public bool DropTarget(IAIObject pTarget)
    {
        bool bResult = false;

        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.DropTarget(pTarget);
            bResult = true;
        }

        return bResult;
    }

    public virtual void SetRODHandler(IAIRateOfDeathHandler pHandler) { m_pRODHandler = pHandler; }
    public virtual void ClearRODHandler() { m_pRODHandler = null; }

    public virtual bool GetPerceivedTargetPos(IAIObject pTarget, out Vec3 vPos)
    {
        vPos = new Vec3(0, 0, 0);

        bool bResult = false;

        if (pTarget != null)
        {
            CAIObject pTargetObj = (CAIObject)pTarget;
            CWeakRef<CAIObject> refTargetAssociation = pTargetObj.GetAssociation();
            if (refTargetAssociation.IsValid())
                pTargetObj = refTargetAssociation.GetAIObject();

            // Check potential targets
            PotentialTargetMap targetMap = new PotentialTargetMap();
            if (m_pPerceptionHandler != null && m_pPerceptionHandler.GetPotentialTargets(targetMap))
            {
                // Simplified — full PotentialTargetMap lookup deferred
                bResult = false;
            }
        }

        return bResult;
    }

    public override void UpdateLookTarget(CAIObject pTarget)
    {
        CCCPOINT("CPuppet_UpdateLookTarget");

        if (pTarget == null)
        {
            CPersonalInterestManager pPIM = GetPersonalInterestManager();
            if (pPIM != null && pPIM.IsInterested())
            {
                Vec3 interestPoint = pPIM.GetInterestDummyPoint();
                // In C++, GetInterestDummyPoint returned a CAIObject* dummy; in C# it's a Vec3.
                // Create a temporary dummy CAIObject to serve as look target if point is valid.
                CAIObject pInterest = interestPoint.GetLengthSquared() > 0.0f ? new CAIObject() : null;
                if (pInterest != null)
                {
                    ELookStyle eStyle = (ELookStyle)pPIM.GetLookingStyle();
                    if (eStyle == ELookStyle.LOOKSTYLE_HARD || eStyle == ELookStyle.LOOKSTYLE_SOFT)
                    {
                        SetAllowedStrafeDistances(999999.0f, 999999.0f, true);
                    }
                    SetLookStyle(eStyle);
                    pTarget = pInterest;
                }
            }
        }

        // Don't look at targets that aren't at least interesting
        if (pTarget != null && m_refAttentionTarget.GetAIObject() == pTarget && GetAttentionTargetThreat() <= EAITargetThreat.AITHREAT_SUSPECT)
            pTarget = null;

        // Update look direction and strafing
        bool lookAtTarget = false;
        if (m_State.fDesiredSpeed < 0.01f || m_Path.Empty())
            lookAtTarget = true;

        // Check if strafing should be allowed.
        UpdateStrafing();
        if (m_State.allowStrafing)
            lookAtTarget = true;
        if (m_bLooseAttention)
        {
            CAIObject pLooseAttentionTarget = m_refLooseAttentionTarget.GetAIObject();
            if (pLooseAttentionTarget != null)
                pTarget = pLooseAttentionTarget;
        }
        if (m_refFireTarget.IsValid() && m_fireMode != EFireMode.FIREMODE_OFF)
            pTarget = GetFireTargetObject();

        Vec3 lookTarget = new Vec3(0, 0, 0);

        if (m_fireMode == EFireMode.FIREMODE_MELEE || m_fireMode == EFireMode.FIREMODE_MELEE_FORCED)
        {
            if (pTarget != null)
            {
                lookTarget = pTarget.GetPos();
                lookTarget = new Vec3(lookTarget.x, lookTarget.y, GetPos().z);
                lookAtTarget = true;
            }
        }

        bool use3DNav = IsUsing3DNavigation();
        bool isMoving = m_State.fDesiredSpeed > 0.0f && m_State.curActorTargetPhase == EActorTargetPhase.eATP_None && !m_State.vMoveDir.IsZero();

        float distToTarget = float.MaxValue;
        if (pTarget != null)
        {
            Vec3 dirToTarget = pTarget.GetPos() - GetPos();
            distToTarget = dirToTarget.GetLength();
            if (distToTarget > 0.0001f)
                dirToTarget /= distToTarget;

            if (isMoving)
            {
                Vec3 move = m_State.vMoveDir;
                if (!use3DNav)
                    move = new Vec3(move.x, move.y, 0.0f);
                move = move.GetNormalizedSafe();
                if (distToTarget < 2.5f || move.Dot(dirToTarget) > cosf(DEG2RAD(60)))
                    lookAtTarget = true;
            }
        }

        if (lookAtTarget && pTarget != null)
        {
            Vec3 vTargetPos = pTarget.GetPos();

            float maxDeviation = distToTarget * sinf(DEG2RAD(15));

            if (distToTarget > GetParameters().m_fPassRadius)
            {
                lookTarget = vTargetPos;
                lookTarget = new Vec3(lookTarget.x, lookTarget.y, clamp_tpl(lookTarget.z, GetPos().z - maxDeviation, GetPos().z + maxDeviation));
            }

            int TargetType = pTarget.GetType();
            if (distToTarget < 1.0f ||
                ((TargetType == (int)CryAISystem.EAIObjectType.AIOBJECT_DUMMY || TargetType == (int)CryAISystem.EAIObjectType.AIOBJECT_HIDEPOINT || TargetType == (int)CryAISystem.EAIObjectType.AIOBJECT_WAYPOINT ||
                TargetType > (int)CryAISystem.EAIObjectType.AIOBJECT_PLAYER) && distToTarget < 5.0f))
            {
                if (!use3DNav)
                {
                    lookTarget = vTargetPos;
                    lookTarget = new Vec3(lookTarget.x, lookTarget.y, clamp_tpl(lookTarget.z, GetPos().z - maxDeviation, GetPos().z + maxDeviation));
                }
            }
        }
        else if (isMoving && (gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_CRYSIS2))
        {
            Vec3 lookAheadPoint;
            float lookAheadDist = 2.5f;

            if (m_pPathFollower != null)
            {
                float junk;
                lookAheadPoint = m_pPathFollower.GetPathPointAhead(lookAheadDist, out junk);
            }
            else
            {
                lookAheadPoint = GetPhysicsPos();
                m_Path.GetPosAlongPath(out lookAheadPoint, lookAheadDist, !m_movementAbility.b3DMove, true);
            }

            lookTarget = lookAheadPoint;

            Vec3 delta = lookTarget - GetPhysicsPos();
            delta = new Vec3(delta.x, delta.y, 0.0f);
            float dist2 = delta.GetLengthSquared();
            if (dist2 < sqr(1.0f))
            {
                float u = 1.0f - sqrtf(dist2);
                Vec3 safeDir = GetEntityDir();
                safeDir = new Vec3(safeDir.x, safeDir.y, 0.0f);
                delta = delta + (safeDir - delta) * u;
            }
            delta = delta.GetNormalizedSafe();

            lookTarget = GetPhysicsPos() + delta * 40.0f;
            lookTarget = new Vec3(lookTarget.x, lookTarget.y, GetPos().z);
        }
        else
        {
            lookTarget = new Vec3(0, 0, 0);
        }

        if (!m_posLookAtSmartObject.IsZero())
        {
            if (!m_bLooseAttention && m_fireMode == EFireMode.FIREMODE_OFF)
            {
                lookTarget = m_posLookAtSmartObject;
            }
        }

        if (!lookTarget.IsZero())
        {
            if (m_allowStrafeLookWhileMoving && m_fireMode != EFireMode.FIREMODE_OFF && GetFireTargetObject() != null)
            {
                float distSqr = Point_Point2DSq(GetFireTargetObject().GetPos(), GetPos());
                if (!m_closeRangeStrafing)
                {
                    float thr = GetParameters().m_PerceptionParams.sightRange * 0.12f;
                    if (distSqr < sqr(thr))
                        m_closeRangeStrafing = true;
                }
                if (m_closeRangeStrafing)
                {
                    m_State.allowStrafing = true;
                    float thr = GetParameters().m_PerceptionParams.sightRange * 0.12f + 2.0f;
                    if (distSqr > sqr(thr))
                        m_closeRangeStrafing = false;
                }
            }

            float distSqr2 = Point_Point2DSq(lookTarget, GetPos());
            if (distSqr2 < sqr(2.0f))
            {
                Vec3 dirToLookTarget = lookTarget - GetPos();
                dirToLookTarget = dirToLookTarget.GetNormalizedSafe(GetEntityDir());
                Vec3 fakePos = GetPos() + dirToLookTarget * 2.0f;
                if (distSqr2 < sqr(0.12f))
                {
                    lookTarget = new Vec3(0, 0, 0);
                }
                else if (distSqr2 < sqr(0.7f))
                {
                    lookTarget = fakePos;
                }
                else
                {
                    float speed = m_State.vMoveDir.GetLength();
                    speed = clamp_tpl(speed, 0.0f, 10.0f);
                    float d = sqrtf(distSqr2);
                    float u2 = 1.0f - (d - 0.7f) / (2.0f - 0.7f);
                    lookTarget += speed / 10 * u2 * (fakePos - lookTarget);
                }
            }
        }

        // for the invehicle gunners
        if (GetProxy() != null)
        {
            SAIBodyInfo bodyInfo = GetBodyInfo();

            IEntity pLinkedVehicleEntity = bodyInfo.GetLinkedVehicleEntity();
            if (pLinkedVehicleEntity != null)
            {
                if (GetProxy().GetActorIsFallen())
                {
                    lookTarget = new Vec3(0, 0, 0);
                }
                else
                {
                    CAIObject pUnit = pLinkedVehicleEntity.GetAI() as CAIObject;
                    if (pUnit != null)
                    {
                        if (pUnit.CastToCAIVehicle() != null)
                        {
                            lookTarget = new Vec3(0, 0, 0);
                            CAIObject pLooseAttentionTarget = m_refLooseAttentionTarget.GetAIObject();
                            if (m_bLooseAttention && pLooseAttentionTarget != null)
                                pTarget = pLooseAttentionTarget;
                            if (pTarget != null)
                            {
                                lookTarget = pTarget.GetPos();
                                m_State.allowStrafing = false;
                            }
                        }
                    }
                }
            }
        }

        if (GetSubType() != ESubType.STP_HELICRYSIS2)
        {
            float lookTurnSpeed = GetAlertness() > 0 ? m_Parameters.m_lookCombatTurnSpeed : m_Parameters.m_lookIdleTurnSpeed;
            if (lookTurnSpeed <= 0.0f || !isMoving)
                m_State.vLookTargetPos = lookTarget;
            else
                m_State.vLookTargetPos = InterpolateLookOrAimTargetPos(m_State.vLookTargetPos, lookTarget, lookTurnSpeed);
        }
    }
    public virtual void UpdateLookTarget3D(CAIObject pTarget)
    {
        // This is for the scout mainly
        m_State.vLookTargetPos = new Vec3(0, 0, 0);
        m_State.aimTargetIsValid = false;

        CAIObject pLooseAttentionTarget = m_refLooseAttentionTarget.GetAIObject();
        if (m_bLooseAttention && pLooseAttentionTarget != null)
        {
            pTarget = pLooseAttentionTarget;
            m_State.vLookTargetPos = pTarget.GetPos();
        }

        m_State.vForcedNavigation = m_vForcedNavigation;
    }
    public override bool NavigateAroundObjects(Vec3 targetPos, bool fullUpdate)
    {
        bool in3D = IsUsing3DNavigation();
        Vec3 myPos = GetPhysicsPos();

        bool steering = false;
        bool lastSteeringEnabled = m_steeringEnabled;
        bool steeringEnabled = m_State.vMoveDir.GetLength() > 0.01f && m_State.fDesiredSpeed > 0.01f;

        CTimeValue curTime = GetAISystem().GetFrameStartTime();

        long deltaTime = (curTime - m_lastSteerTime).GetMilliSecondsAsInt64();
        long timeForUpdate = 500;

        if (steeringEnabled && (deltaTime > timeForUpdate || !lastSteeringEnabled))
        {
            m_lastSteerTime = curTime;
            m_steeringObjects.Clear();

            float radius = min(2.5f, Point_Point(myPos, m_Path.GetLastPathPos()));
            CryAISystem.CryCommon.Lineseg lseg = new CryAISystem.CryCommon.Lineseg(myPos, myPos + new Vec3(0.0f, 0.0f, 2.5f));

            // Gather entities via OverlapCylinder
            int[] ent = new int[11];
            if (OverlapCylinder(lseg, radius, (int)(ent_living | AICE_DYNAMIC), GetPhysics(), 0, ent, 10))
            {
                for (int idx = 0; idx < ent.Length && ent[idx] != 0; ++idx)
                {
                    IEntity entity = gEnv.pSystem?.GetIEntitySystem()?.GetEntity((uint)ent[idx]);
                    if (entity != null)
                    {
                        CAIObject aiob = entity.GetAI() as CAIObject;
                        if (aiob != null && NavigateAroundObjectsBasicCheck(aiob) != ENavInteraction.NI_IGNORE)
                        {
                            m_steeringObjects.Add(aiob);
                        }
                    }
                }
            }
        }

        if (steeringEnabled)
        {
            if ((GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_ACTOR) && !in3D)
            {
                bool check = fullUpdate;
                if (m_updatePriority == EPuppetUpdatePriority.AIPUP_VERY_HIGH || m_updatePriority == EPuppetUpdatePriority.AIPUP_HIGH)
                    check = true;

                if (check || !lastSteeringEnabled)
                {
                    float radScale = 1.0f - clamp_tpl(m_steeringAdjustTime - 1.0f, 0.0f, 1.0f);
                    float selfRad = m_movementAbility.pathRadius * radScale;
                    m_steeringOccupancy.Reset(GetPhysicsPos(), GetEntityDir(), m_movementAbility.avoidanceRadius * 2.0f);

                    for (int i = 0, ni = m_steeringObjects.Count; i < ni; ++i)
                    {
                        CAIActor pActor = m_steeringObjects[i].CastToCAIActor();
                        if (pActor == null)
                            continue;

                        if (pActor.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_VEHICLE)
                        {
                            // Simplified vehicle steering - add obstruction direction
                            Vec3 vehPos = pActor.GetPhysicsPos();
                            m_steeringOccupancy.AddObstructionCircle(vehPos, selfRad + pActor.GetMovementAbility().pathRadius);
                        }
                        else
                        {
                            Vec3 pos = pActor.GetPhysicsPos();
                            float rad = pActor.GetMovementAbility().pathRadius;
                            m_steeringOccupancy.AddObstructionCircle(pos, selfRad + rad);
                            Vec3 vel = pActor.GetVelocity();
                            if (vel.GetLengthSquared() > sqr(0.1f))
                            {
                                Vec3 bodyDir = pActor.GetEntityDir();
                                Vec3 right = new Vec3(bodyDir.y, -bodyDir.x, 0);
                                right = right.GetNormalizedSafe();
                                m_steeringOccupancy.AddObstructionCircle(pos + vel * 0.25f + right * rad * 0.3f, selfRad + rad);
                            }
                        }
                    }
                }

                // Steer around
                Vec3 oldMoveDir = m_State.vMoveDir;
                m_State.vMoveDir = m_steeringOccupancy.GetNearestUnoccupiedDirection(m_State.vMoveDir, m_steeringOccupancyBias);

                if (fabsf(m_steeringOccupancyBias) > 0.1f)
                {
                    steering = true;

                    Vec3 aheadPos;
                    if (m_pPathFollower != null)
                    {
                        float junk;
                        aheadPos = m_pPathFollower.GetPathPointAhead(m_movementAbility.pathRadius, out junk);
                        if (Point_Point2DSq(aheadPos, GetPhysicsPos()) < sqr(m_movementAbility.avoidanceRadius))
                        {
                            m_pPathFollower.Advance(m_movementAbility.pathRadius * 0.3f);
                        }
                    }
                    else
                    {
                        aheadPos = m_Path.CalculateTargetPos(myPos, 0.0f, 0.0f, m_movementAbility.pathRadius, true);
                        if (!m_Path.Empty() && Point_Point2DSq(aheadPos, GetPhysicsPos()) < sqr(m_movementAbility.avoidanceRadius))
                        {
                            Vec3 pathNextPoint;
                            if (m_Path.GetPosAlongPath(out pathNextPoint, m_movementAbility.pathRadius * 0.3f, true, false))
                                m_Path.UpdatePathPosition(pathNextPoint, 100.0f, true, false);
                        }
                    }

                    float slowdown = 1.0f - (oldMoveDir.Dot(m_State.vMoveDir) + 1.0f) * 0.5f;

                    float normalSpeed, minSpeed, maxSpeed;
                    GetMovementSpeedRange(m_State.fMovementUrgency, m_State.allowStrafing, out normalSpeed, out minSpeed, out maxSpeed);

                    m_State.fDesiredSpeed += (minSpeed - m_State.fDesiredSpeed) * slowdown;

                    if (slowdown > 0.1f)
                        m_steeringAdjustTime += m_fTimePassed;
                }
                else
                {
                    m_steeringAdjustTime -= m_fTimePassed;
                }

                Limit(ref m_steeringAdjustTime, 0.0f, 3.0f);
            }
            else
            {
                // Old type steering for the rest of the objects.
                int nObj = m_steeringObjects.Count;
                for (int i = 0; i < nObj; ++i)
                {
                    CAIObject obj = m_steeringObjects[i];
                    if (NavigateAroundObjectsInternal(targetPos, myPos, in3D, obj))
                        steering = true;
                }
            }
        }

        m_steeringEnabled = steeringEnabled;

        return steering;
    }

    public void SetForcedNavigation(Vec3 vDirection, float fSpeed)
    {
        m_vForcedNavigation = vDirection;
        m_fForcedNavigationSpeed = fSpeed;
    }
    public void ClearForcedNavigation()
    {
        m_vForcedNavigation = new Vec3(0, 0, 0);
        m_fForcedNavigationSpeed = 0.0f;
    }

    public void SetVehicleStickTarget(uint targetId) { m_vehicleStickTarget = targetId; }
    public uint GetVehicleStickTarget() { return m_vehicleStickTarget; }

    public virtual bool NavigateAroundAIObject(Vec3 targetPos, CAIObject obstacle, Vec3 predMyPos, Vec3 predObjectPos, bool steer, bool in3D)
    {
        if (steer)
        {
            if (in3D)
                return SteerAround3D(targetPos, obstacle, predMyPos, predObjectPos);
            else if (obstacle.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_VEHICLE)
                return SteerAroundVehicle(targetPos, obstacle, predMyPos, predObjectPos);
            else
                return SteerAroundPuppet(targetPos, obstacle, predMyPos, predObjectPos);
        }
        else
        {
            AILog.AIError("NavigateAroundAIObject - only handles steering so far");
            return false;
        }
    }

    public virtual void SetCanBeShot(bool bCanBeShot) { m_bCanBeShot = bCanBeShot; }
    public virtual bool GetCanBeShot() { return m_bCanBeShot; }

    public virtual void SetMemoryFireType(EMemoryFireType eType) { m_eMemoryFireType = eType; }
    public virtual EMemoryFireType GetMemoryFireType() { return m_eMemoryFireType; }
    public virtual bool CanMemoryFire()
    {
        bool bResult = true;

        if (m_targetLostTime > float.Epsilon)
        {
            switch (m_eMemoryFireType)
            {
                case EMemoryFireType.eMFT_Disabled:
                    bResult = false;
                    break;

                case EMemoryFireType.eMFT_UseCoverFireTime:
                    {
                        float fCoverTime = GetCoverFireTime();
                        bResult = (m_targetLostTime <= fCoverTime);
                    }
                    break;

                case EMemoryFireType.eMFT_Always:
                    bResult = true;
                    break;

                default:
                    break;
            }
        }

        return bResult;
    }

    public bool CanFireInStance(EStance stance, float fDistanceRatio = 0.9f)
    {
        bool bResult = false;

        CAIObject pLiveTarget = GetLiveTarget(m_refLastOpResult).GetAIObject();
        if (pLiveTarget != null)
        {
            // Try to use perceived location
            Vec3 vTargetPos;
            if (!GetPerceivedTargetPos(pLiveTarget, out vTargetPos))
                vTargetPos = pLiveTarget.GetPos();

            // Do a partial check along the potential fire direction based on distance ratio
            float fDistance = Point_Point(vTargetPos, GetPos()) * clamp_tpl(fDistanceRatio, 0.0f, 1.0f);
            bResult = CheckLineOfFire(vTargetPos, fDistance, 0.5f, stance);
        }

        return bResult;
    }

    public void RequestThrowGrenade(ERequestedGrenadeType eGrenadeType, int iRegType)
    {
        m_bGrenadeThrowRequested = true;
        m_eGrenadeThrowRequestType = eGrenadeType;
        m_iGrenadeThrowTargetType = iRegType;
    }

    public int GetAlertness() { return m_Alertness; }

    public override void CheckCloseContact(IAIObject pTarget, float fDistSq)
    {
        if (GetAttentionTarget() == pTarget)
        {
            base.CheckCloseContact(pTarget, fDistSq);
        }
    }

    public override float AdjustTargetVisibleRange(CAIActor observer, float fVisibleRange)
    {
        float fRangeScale = 1.0f;

        // Adjust using my light level if the observer is affected by light
        if (IsAffectedByLight())
        {
            EAILightLevel targetLightLevel = GetLightLevel();
            switch (targetLightLevel)
            {
                case EAILightLevel.AILL_MEDIUM: fRangeScale *= gAIEnv.CVars.SightRangeMediumIllumMod; break;
                case EAILightLevel.AILL_DARK: fRangeScale *= gAIEnv.CVars.SightRangeDarkIllumMod; break;
                case EAILightLevel.AILL_SUPERDARK: fRangeScale *= gAIEnv.CVars.SightRangeSuperDarkIllumMod; break;
            }
        }

        // Scale down sight range when target is underwater based on distance
        float fCachedWaterOcclusionValue = GetCachedWaterOcclusionValue();
        if (fCachedWaterOcclusionValue > float.Epsilon)
        {
            Vec3 observerPos = observer.GetPos();
            float fDistance = Point_Point(GetPos(), observerPos);
            float fDistanceFactor = (fVisibleRange > float.Epsilon ? GetAISystem().GetVisPerceptionDistScale(fDistance / fVisibleRange) : 0.0f);

            float fWaterOcclusionEffect = 2.0f * fCachedWaterOcclusionValue + (1 - fDistanceFactor) * 0.5f;

            fRangeScale *= (fWaterOcclusionEffect > 1.0f ? 0.0f : 1.0f - fWaterOcclusionEffect);
        }

        // Return new range
        return fVisibleRange * fRangeScale;
    }

    public EPuppetUpdatePriority GetUpdatePriority() { return m_updatePriority; }
    public void SetUpdatePriority(EPuppetUpdatePriority pri) { m_updatePriority = pri; }

    public AIWeaponDescriptor QueryCurrentWeaponDescriptor(bool bIsSecondaryFire = false, ERequestedGrenadeType prefGrenadeType = ERequestedGrenadeType.eRGT_ANY)
    {
        IAIActorProxy pProxy = GetProxy();
        if (pProxy != null)
        {
            bool bUpdatedDescriptor = false;
            uint weaponToListenTo = 0;

            if (!bIsSecondaryFire)
            {
                uint weaponId = 0;
                pProxy.GetCurrentWeapon(out weaponId);

                weaponToListenTo = weaponId;

                bool weaponChanged = (m_currentWeaponId != weaponId);
                if (weaponChanged || m_fireModeUpdated)
                {
                    m_currentWeaponId = weaponId;
                    m_CurrentWeaponDescriptor = pProxy.GetCurrentWeaponDescriptor();
                    bUpdatedDescriptor = true;
                }
            }
            else
            {
                bUpdatedDescriptor = true;

                pProxy.GetSecWeapon(prefGrenadeType, null, out weaponToListenTo);

                if (!pProxy.GetSecWeaponDescriptor(m_CurrentWeaponDescriptor, prefGrenadeType))
                {
                    m_CurrentWeaponDescriptor = new AIWeaponDescriptor();
                }
            }

            if (bUpdatedDescriptor)
            {
                pProxy.EnableWeaponListener(weaponToListenTo, m_CurrentWeaponDescriptor.bSignalOnShoot);

                if (m_pFireCmdHandler != null && m_CurrentWeaponDescriptor.firecmdHandler == m_pFireCmdHandler.GetName())
                {
                    m_pFireCmdHandler.Reset();
                }
                else
                {
                    if (m_pFireCmdHandler != null) m_pFireCmdHandler.Release();
                    m_pFireCmdHandler = null;
                    // In C++: m_pFireCmdHandler = GetAISystem()->CreateFirecommandHandler(...)
                    // Deferred — fire command creation
                }
            }
        }

        return m_CurrentWeaponDescriptor;
    }
    public AIWeaponDescriptor GetCurrentWeaponDescriptor() { return m_CurrentWeaponDescriptor; }

    public bool CanAimWithoutObstruction(Vec3 vTargetPos)
    {
        if (m_bDryUpdate)
            return m_lastAimObstructionResult;

        float checkDistance = Point_Point(GetPos(), vTargetPos) * 0.5f;
        float softCheckDistance = 0.5f;
        bool bResult = CheckLineOfFire(vTargetPos, checkDistance, softCheckDistance);

        m_lastAimObstructionResult = bResult;
        return bResult;
    }
    public bool CheckLineOfFire(Vec3 vTargetPos, float fDistance, float fSoftDistance, EStance stance = EStance.STANCE_NULL)
    {
        bool bResult = true;

        // Early outs
        IAIActorProxy pProxy = GetProxy();
        if ((pProxy != null && pProxy.GetLinkedVehicleEntityId() != 0) || GetSubType() == ESubType.STP_2D_FLY)
            return true;

        ActorLookUp lookUp = gAIEnv.pActorLookUp;
        lookUp.Prepare(ActorLookUp.Position);

        SAIBodyInfo bodyInfo = new SAIBodyInfo();
        Vec3 firePos;
        if (stance > EStance.STANCE_NULL && pProxy != null && pProxy.QueryBodyInfo(new SAIBodyInfoQuery(stance, 0.0f, 0.0f, true), bodyInfo))
            firePos = bodyInfo.vFirePos;
        else
            firePos = GetFirePos();
        Vec3 dir = vTargetPos - firePos;

        Ray fireRay = new Ray { origin = firePos, direction = dir };
        Vec3 pos = GetPos();

        uint activeActorCount = (uint)lookUp.GetActiveCount();

        for (uint actorIndex = 0; actorIndex < activeActorCount; ++actorIndex)
        {
            CAIActor pAIActor = lookUp.GetActor<CAIActor>(actorIndex);
            if (pAIActor == this)
                continue;

            if (Point_PointSq(pos, lookUp.GetPosition(actorIndex)) > sqr(10.0f))
                continue;

            if (!IsHostile(pAIActor) && ActorObstructingAim(pAIActor, firePos, dir, fireRay))
            {
                bResult = false;
                break;
            }
        }

        if (bResult)
        {
            // check the player (friends only)
            CAIActor pPlayer = CastToCAIActorSafe(GetAISystem().GetPlayer());
            if (pPlayer != null)
            {
                if (!IsHostile(pPlayer) &&
                    Point_PointSq(GetPos(), pPlayer.GetPos()) < sqr(5.0f) &&
                    ActorObstructingAim(pPlayer, firePos, dir, fireRay))
                {
                    bResult = false;
                }
            }
        }

        if (bResult && m_fireMode != EFireMode.FIREMODE_KILL && fDistance > float.Epsilon) // when in KILL mode - just shoot no matter what
        {
            dir = dir.Normalized();
            dir *= fDistance;

            m_lineOfFireState.softDistance = fSoftDistance;

            if (m_lineOfFireState.asyncState == AsyncState.AsyncReady)
            {
                m_lineOfFireState.asyncState = AsyncState.AsyncInProgress;

                PhysSkipList skipList = new PhysSkipList();
                GetPhysicalSkipEntities(skipList);

                // Add the physical body of the target to the skip list.
                IAIObject pTarget = GetAttentionTarget();
                if (pTarget != null)
                {
                    IAIObject pAssociation = GetAttentionTargetAssociation();
                    if (pAssociation != null)
                        pTarget = pAssociation;

                    IEntity pEntity = pTarget.GetEntity();
                    if (pEntity != null)
                        stl.push_back_unique(skipList, pEntity.GetPhysics());
                }

                // In C++ this queues an async ray. In the C# port we store the pending state.
                // The actual ray result would be handled by LineOfFireRayComplete callback.
                // For now, return the cached result.
            }

            return m_lineOfFireState.result;
        }

        return bResult;
    }

    public float GetTimeToNextShot()
    {
        // In C++ this calls m_pFireCmdHandler->GetTimeToNextShot().
        // GetTimeToNextShot is not on IFireCommandHandler interface yet, return 0.
        return 0.0f;
    }

    public override void MakeIgnorant(bool bIgnorant) { m_bCanReceiveSignals = !bIgnorant; }

    public bool AddPerceptionHandlerModifier(IPerceptionHandlerModifier pModifier)
    {
        return stl.push_back_unique(m_perceptionHandlerModifiers, pModifier);
    }
    public bool RemovePerceptionHandlerModifier(IPerceptionHandlerModifier pModifier)
    {
        return stl.find_and_erase(m_perceptionHandlerModifiers, pModifier);
    }
    public bool GetPerceptionHandlerModifiers(TPerceptionHandlerModifiersVector outModifiers)
    {
        outModifiers.Clear();
        outModifiers.AddRange(m_perceptionHandlerModifiers);
        return (m_perceptionHandlerModifiers.Count > 0);
    }
    public void DebugDrawPerceptionHandlerModifiers()
    {
        uint entityId = GetEntityID();
        float fY = 30.0f;

        foreach (var modifier in m_perceptionHandlerModifiers)
        {
            modifier.DebugDraw(null); // C++ passed (entityId, fY); C# interface takes IAIDebugRenderer
        }
    }

    public IFireCommandHandler m_pFireCmdHandler;
    public CFireCommandGrenade m_pFireCmdGrenade;

    public float m_targetApproach;
    public float m_targetFlee;
    public bool m_targetApproaching;
    public bool m_targetFleeing;
    public bool m_lastTargetValid;
    public Vec3 m_lastTargetPos;
    public float m_lastTargetSpeed;
    public CSignalState m_attTargetOutOfTerritory = new CSignalState();

    public bool m_bCanReceiveSignals;

    protected enum TPlayerActionType
    {
        PA_NONE = 0,
        PA_LOOKING,
        PA_STICKING
    }

    protected struct STargetSelectionInfo
    {
        public CWeakRef<CAIObject> bestTarget;
        public EAITargetThreat targetThreat;
        public EAITargetType targetType;
        public SAIPotentialTarget pTargetInfo;
        public bool bCurrentTargetErased;
        public bool bIsGroupTarget;
    }

    protected void CheckAwarenessPlayer()
    {
        CAIObject pPlayer = GetAISystem().GetPlayer();

        if (pPlayer == null)
            return;

        Vec3 lookDir = pPlayer.GetViewDir();
        Vec3 relPos = (GetPos() - pPlayer.GetPos());
        float dist = relPos.GetLength();
        if (dist > 0)
            relPos /= dist;

        float fdot;
        float threshold;
        bool bCheckPlayerLooking;
        if (dist <= 1.2f)
        {
            bCheckPlayerLooking = false;
            fdot = GetMoveDir().Dot(-relPos);
            threshold = 0;
        }
        else
        {
            bCheckPlayerLooking = true;
            fdot = lookDir.Dot(relPos);
            threshold = cosf(atanf(0.5f / dist));
        }

        if (fdot > threshold && eFOV_Outside != IsObjectInFOV(pPlayer))
        {
            if (m_fLastTimeAwareOfPlayer == 0 && bCheckPlayerLooking)
            {
                IEntity pEntity = null;
                IPhysicalEntity pPlayerPhE = (pPlayer.GetProxy() != null ? pPlayer.GetPhysics() : null);

                // Simplified ray cast check — in C++ uses async RayCast
                // For the port, skip the ray check and proceed.
                // A more faithful port would queue a ray cast here.
                if (pPlayerPhE != null)
                {
                    // Check if the ray from the player hits our entity
                    pEntity = GetEntity(); // simplified — assume visible for port
                }

                if (!(pEntity != null && pEntity == GetEntity()))
                {
                    return;
                }
            }
            float fCurrentTime = GetAISystem().GetFrameStartTimeSeconds();
            if (m_fLastTimeAwareOfPlayer == 0)
                m_fLastTimeAwareOfPlayer = fCurrentTime;
            else if (fCurrentTime - m_fLastTimeAwareOfPlayer >= GetParameters().m_fAwarenessOfPlayer)
            {
                IAISignalExtraData pData = GetAISystem().CreateSignalExtraData();
                if (pData != null)
                    pData.fValue = dist;
                m_playerAwarenessType = dist <= GetParameters().m_fMeleeRange ? TPlayerActionType.PA_STICKING : TPlayerActionType.PA_LOOKING;
                IEntity pUserEntity = GetEntity();
                IEntity pObjectEntity = pPlayer.GetEntity();
                gAIEnv.pSmartObjectManager?.SmartObjectEvent(bCheckPlayerLooking ? "OnPlayerLooking" : "OnPlayerSticking", pUserEntity, pObjectEntity);
                SetSignal(1, bCheckPlayerLooking ? "OnPlayerLooking" : "OnPlayerSticking", pObjectEntity, pData, bCheckPlayerLooking ? gAIEnv.SignalCRCs.m_nOnPlayerLooking : gAIEnv.SignalCRCs.m_nOnPlayerSticking);
                m_fLastTimeAwareOfPlayer = fCurrentTime;
            }
        }
        else
        {
            IEntity pUserEntity = GetEntity();
            IEntity pObjectEntity = pPlayer.GetEntity();
            if (m_playerAwarenessType == TPlayerActionType.PA_LOOKING)
            {
                SetSignal(1, "OnPlayerLookingAway", null, null, gAIEnv.SignalCRCs.m_nOnPlayerLookingAway);
                gAIEnv.pSmartObjectManager?.SmartObjectEvent("OnPlayerLookingAway", pUserEntity, pObjectEntity);
            }
            else if (m_playerAwarenessType == TPlayerActionType.PA_STICKING)
            {
                gAIEnv.pSmartObjectManager?.SmartObjectEvent("OnPlayerGoingAway", pUserEntity, pObjectEntity);
                SetSignal(1, "OnPlayerGoingAway", null, null, gAIEnv.SignalCRCs.m_nOnPlayerGoingAway);
            }
            m_fLastTimeAwareOfPlayer = 0;
            m_playerAwarenessType = TPlayerActionType.PA_NONE;
        }
    }

    protected SAIPotentialTarget AddEvent(CWeakRef<CAIObject> refObject, SAIPotentialTarget ed)
    {
        if (m_pPerceptionHandler != null)
        {
            m_pPerceptionHandler.AddEvent(refObject, ed);
            return ed;
        }
        return null;
    }

    protected void UpdatePuppetInternalState()
    {
        CCCPOINT("CPuppet_UpdatePuppetInternalState");

        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();

        // Update alarmed state
        m_alarmedTime -= m_fTimePassed;
        if (m_alarmedTime < 0.0f)
            m_alarmedTime = 0.0f;

        float alarmLevelChangeTime = 3.0f;
        float alarmLevelGoal = IsAlarmed() ? 1.0f : 0.0f;
        m_alarmedLevel += (alarmLevelGoal - m_alarmedLevel) * (m_fTimePassed / alarmLevelChangeTime);
        Limit(ref m_alarmedLevel, 0.0f, 1.0f);

        STargetSelectionInfo targetSelectionInfo = new STargetSelectionInfo();
        if (UpdateTargetSelection(targetSelectionInfo))
        {
            SAIPotentialTarget bestTargetEvent = targetSelectionInfo.pTargetInfo;

            EAITargetThreat oldThreat = m_State.eTargetThreat;
            EAITargetThreat newThreat = targetSelectionInfo.targetThreat;

            if (newThreat > oldThreat && newThreat >= EAITargetThreat.AITHREAT_THREATENING)
            {
                gEnv.pAISystem?.GetAIActionManager()?.AbortAIAction(GetEntity());
            }

            m_State.eTargetThreat = targetSelectionInfo.targetThreat;
            m_State.eTargetType = targetSelectionInfo.targetType;
            m_State.eTargetID = targetSelectionInfo.bestTarget.GetObjectID();
            m_State.eTargetStuntReaction = EAITargetStuntReaction.AITSR_NONE;
            m_State.bTargetIsGroupTarget = targetSelectionInfo.bIsGroupTarget;

            CAIObject bestTarget = targetSelectionInfo.bestTarget.GetAIObject();
            if (bestTarget != null)
            {
                m_State.vTargetPos = bestTarget.GetPos();
            }

            if (bestTarget != pAttentionTarget)
            {
                // New attention target
                SetAttentionTarget(GetWeakRef(bestTarget));
                m_AttTargetPersistenceTimeout = m_Parameters.m_PerceptionParams.targetPersistence;

                // When seeing a visible target, check for stunt reaction.
                if (bestTarget != null && m_State.eTargetType == EAITargetType.AITARGET_VISUAL && m_State.eTargetThreat == EAITargetThreat.AITHREAT_AGGRESSIVE)
                {
                    CAIPlayer pPlayer = bestTarget.CastToCAIPlayer();
                    if (pPlayer != null)
                    {
                        if (IsHostile(pPlayer))
                        {
                            bool isStunt = pPlayer.IsDoingStuntActionRelatedTo(GetPos(), m_Parameters.m_PerceptionParams.sightRange / 5.0f);
                            if (m_targetLostTime > m_Parameters.m_PerceptionParams.stuntReactionTimeOut && isStunt)
                                m_State.eTargetStuntReaction = EAITargetStuntReaction.AITSR_SEE_STUNT_ACTION;
                            else if (pPlayer.IsCloakEffective(bestTarget.GetPos()))
                                m_State.eTargetStuntReaction = EAITargetStuntReaction.AITSR_SEE_CLOAKED;
                        }
                    }
                }

                uint bestTargetId = bestTarget != null ? bestTarget.GetEntityID() : 0;
                if (bestTarget != null && bestTargetId == 0)
                {
                    CWeakRef<CAIObject> refAssociation = bestTarget.GetAssociation();
                    if (refAssociation.IsValid())
                        bestTargetId = refAssociation.GetAIObject().GetEntityID();
                }

                // Keep track of peak threat level and type
                CTimeValue curTime = GetAISystem().GetFrameStartTime();

                if (targetSelectionInfo.targetThreat > m_State.ePeakTargetThreat || (curTime - m_lastTimeUpdatedBestTarget).GetSeconds() > 30.0f)
                {
                    m_State.ePreviousPeakTargetThreat = m_State.ePeakTargetThreat;
                    m_State.ePreviousPeakTargetType = m_State.ePeakTargetType;
                    m_State.ePreviousPeakTargetID = m_State.ePeakTargetID;

                    m_State.ePeakTargetThreat = m_State.eTargetThreat;
                    m_State.ePeakTargetType = m_State.eTargetType;
                    m_State.ePeakTargetID = bestTarget != null ? bestTarget.GetAIObjectID() : INVALID_AIOBJECTID;

                    m_lastTimeUpdatedBestTarget = curTime;
                }

                // Inform AI of change
                IAISignalExtraData pData = GetAISystem().CreateSignalExtraData();
                pData.nID = bestTargetId;
                pData.fValue = (targetSelectionInfo.bIsGroupTarget ? 1.0f : 0.0f);
                pData.iValue = (int)m_State.eTargetType;
                pData.iValue2 = (int)m_State.eTargetThreat;
                SetSignal(0, "OnNewAttentionTarget", GetEntity(), pData, gAIEnv.SignalCRCs.m_nOnNewAttentionTarget);

                if (bestTargetEvent != null)
                    bestTargetEvent.bNeedsUpdating = false;
            }
            else if (pAttentionTarget != null)
            {
                if (m_AttTargetThreat != m_State.eTargetThreat)
                {
                    IAISignalExtraData pData = GetAISystem().CreateSignalExtraData();
                    pData.iValue = (int)m_State.eTargetThreat;
                    SetSignal(0, "OnAttentionTargetThreatChanged", GetEntity(), pData, gAIEnv.SignalCRCs.m_nOnAttentionTargetThreatChanged);
                }

                // Handle state change of the current attention target.
                if (m_AttTargetThreat >= EAITargetThreat.AITHREAT_AGGRESSIVE && m_State.eTargetThreat < EAITargetThreat.AITHREAT_AGGRESSIVE)
                {
                    if (m_State.eTargetType == EAITargetType.AITARGET_VISUAL || m_State.eTargetType == EAITargetType.AITARGET_MEMORY)
                        SetSignal(0, "OnNoTargetVisible", GetEntity(), null, gAIEnv.SignalCRCs.m_nOnNoTargetVisible);
                }
                else if (m_AttTargetThreat >= EAITargetThreat.AITHREAT_THREATENING && m_State.eTargetThreat < EAITargetThreat.AITHREAT_THREATENING)
                {
                    SetSignal(0, "OnNoTargetAwareness", GetEntity(), null, gAIEnv.SignalCRCs.m_nOnNoTargetAwareness);
                }

                if (bestTargetEvent != null)
                {
                    if (bestTargetEvent.bNeedsUpdating || bestTargetEvent.exposureThreat > bestTargetEvent.threat || m_AttTargetType != (EAITargetType)bestTargetEvent.type)
                    {
                        bestTargetEvent.bNeedsUpdating = false;

                        if (m_AttTargetExposureThreat <= EAITargetThreat.AITHREAT_AGGRESSIVE && bestTargetEvent.exposureThreat >= (float)EAITargetThreat.AITHREAT_AGGRESSIVE)
                        {
                            if (bestTargetEvent.type == (int)EAITargetType.AITARGET_VISUAL)
                            {
                                CAIPlayer pPlayer = bestTarget?.CastToCAIPlayer();
                                if (pPlayer != null)
                                {
                                    if (IsHostile(pPlayer))
                                    {
                                        bool isStunt = pPlayer.IsDoingStuntActionRelatedTo(GetPos(), m_Parameters.m_PerceptionParams.sightRange / 5.0f);
                                        if (m_targetLostTime > m_Parameters.m_PerceptionParams.stuntReactionTimeOut && isStunt)
                                            m_State.eTargetStuntReaction = EAITargetStuntReaction.AITSR_SEE_STUNT_ACTION;
                                        else if (pPlayer.IsCloakEffective(bestTarget.GetPos()))
                                            m_State.eTargetStuntReaction = EAITargetStuntReaction.AITSR_SEE_CLOAKED;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Keep track of the current state
            m_AttTargetType = m_State.eTargetType;
            m_AttTargetThreat = m_State.eTargetThreat;
            m_AttTargetExposureThreat = bestTargetEvent != null ? (EAITargetThreat)(int)bestTargetEvent.exposureThreat : m_State.eTargetThreat;
        }
        else
        {
            if (pAttentionTarget != null)
            {
                if (targetSelectionInfo.bCurrentTargetErased)
                    SetAttentionTarget(NILREF);
            }

            m_AttTargetType = EAITargetType.AITARGET_NONE;
            m_AttTargetThreat = EAITargetThreat.AITHREAT_NONE;
            m_AttTargetExposureThreat = EAITargetThreat.AITHREAT_NONE;

            m_State.eTargetType = EAITargetType.AITARGET_NONE;
            m_State.eTargetThreat = EAITargetThreat.AITHREAT_NONE;
        }

        // update devaluated points
        List<CWeakRef<CAIObject>> toRemove = new List<CWeakRef<CAIObject>>();
        foreach (var kvp in m_mapDevaluedPoints)
        {
            m_mapDevaluedPoints[kvp.Key] = kvp.Value - m_fTimePassed;
            if (kvp.Value - m_fTimePassed < 0 || !kvp.Key.IsValid())
            {
                CCCPOINT("CPuppet_UpdatePuppetInternalState_DevaluedPoints");
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
            m_mapDevaluedPoints.Remove(key);

        UpdateTargetMovementState();

        // Update attention-target related info
        pAttentionTarget = m_refAttentionTarget.GetAIObject();
        if (pAttentionTarget != null)
        {
            Vec3 vAttTargetPos = pAttentionTarget.GetPos();

            m_State.fDistanceFromTarget = Point_Point(vAttTargetPos, GetPos());

            if (m_territoryShape != null)
            {
                m_attTargetOutOfTerritory.bState = !m_territoryShape.IsPointInsideShape(vAttTargetPos, false);
            }
            else
            {
                m_attTargetOutOfTerritory.bState = false;
            }
        }
        else
        {
            m_State.fDistanceFromTarget = float.MaxValue;
            m_attTargetOutOfTerritory.bState = false;
        }

        if (m_attTargetOutOfTerritory.CheckUpdate())
        {
            bool bState = m_attTargetOutOfTerritory.bState;
            string sSignal = (bState ? "OnTargetOutOfTerritory" : "OnTargetInTerritory");
            SetSignal(AISIGNAL_DEFAULT, sSignal, null, null, 0);
        }
    }
    protected bool UpdateTargetSelection(STargetSelectionInfo targetSelectionInfo)
    {
        bool bResult = false;

        if (gAIEnv.CVars.TargetTracking != 0)
        {
            if (GetTargetTrackBestTarget(out targetSelectionInfo.bestTarget, out targetSelectionInfo.pTargetInfo, out targetSelectionInfo.bCurrentTargetErased))
            {
                if (targetSelectionInfo.pTargetInfo != null &&
                    targetSelectionInfo.pTargetInfo.type == (int)EAITargetType.AITARGET_VISUAL &&
                    targetSelectionInfo.pTargetInfo.threat >= (float)EAITargetThreat.AITHREAT_AGGRESSIVE)
                {
                    SetAlarmed();
                }
                bResult = true;
            }
        }
        else
        {
            bResult = false;
        }

        Group group = gAIEnv.pGroupManager.GetGroup(GetGroupId());

        if ((group.GetTargetType() > EAITargetType.AITARGET_NONE) && (targetSelectionInfo.pTargetInfo == null || ((int)group.GetTargetThreat() >= (int)targetSelectionInfo.pTargetInfo.threat && (int)group.GetTargetType() > targetSelectionInfo.pTargetInfo.type)))
        {
            targetSelectionInfo.bestTarget = group.GetTarget().GetWeakRef();
            targetSelectionInfo.targetThreat = group.GetTargetThreat();
            targetSelectionInfo.targetType = group.GetTargetType();
            targetSelectionInfo.bIsGroupTarget = true;

            bResult = true;
        }
        else if (targetSelectionInfo.pTargetInfo != null)
        {
            targetSelectionInfo.targetThreat = (EAITargetThreat)(int)targetSelectionInfo.pTargetInfo.threat;
            targetSelectionInfo.targetType = (EAITargetType)targetSelectionInfo.pTargetInfo.type;
        }

        return bResult;
    }
    protected bool GetTargetTrackBestTarget(out CWeakRef<CAIObject> refBestTarget, out SAIPotentialTarget pTargetInfo,
        out bool bCurrentTargetErased)
    {
        refBestTarget = new CWeakRef<CAIObject>();
        pTargetInfo = null;
        bCurrentTargetErased = false;

        bool bResult = false;

        uint objectId = GetAIObjectID();

        gAIEnv.pTargetTrackManager?.Update(objectId);
        var uTargetMethod = TargetTrackHelpers.eDTM_Select_Highest;
        if (gAIEnv.pTargetTrackManager != null && gAIEnv.pTargetTrackManager.GetDesiredTarget(objectId, uTargetMethod, ref refBestTarget, ref pTargetInfo) is not null)
        {
            bResult = true;
        }
        else
        {
            bCurrentTargetErased = true;
        }

        return bResult;
    }

    protected override void HandleSoundEvent(SAIEVENT pEvent)
    {
        float fGlobalAudioPerceptionScale = gEnv.pAISystem?.GetGlobalAudioScale(this) ?? 1.0f;
        float fAudioPerceptionScale = m_Parameters.m_PerceptionParams.perceptionScale.audio * fGlobalAudioPerceptionScale;
        if (gAIEnv.CVars.IgnoreSoundStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode || fAudioPerceptionScale <= 0.0f)
            return;

        if (gAIEnv.pTargetTrackManager != null && gAIEnv.pTargetTrackManager.IsEnabled())
        {
            Vec3 vMyPos = GetPos();
            float fSoundDistance = Point_Point(vMyPos, pEvent.vPosition) * (1.0f / fAudioPerceptionScale);
            if (fSoundDistance <= pEvent.fThreat)
            {
                gAIEnv.pTargetTrackManager.HandleStimulusFromAIEvent(GetAIObjectID(), pEvent, TargetTrackHelpers.eEST_Sound);
            }
        }
        if (m_pPerceptionHandler != null)
            m_pPerceptionHandler.HandleSoundEvent(pEvent);
    }
    protected override void HandlePathDecision(MNMPathRequestResult result)
    {
        base.HandlePathDecision(result);

        if (result.HasPathBeenFound())
        {
            bool validPath = result.pPath != null && !result.pPath.Empty();
            if (validPath)
            {
                // Update adaptive urgency control before the path gets processed further
                float urgency = m_State.fMovementUrgency;
                float maxPathLen = m_adaptiveUrgencyMaxPathLen;
                AdjustMovementUrgency(ref urgency, m_Path.GetPathLength(!IsUsing3DNavigation()), ref maxPathLen);
                m_State.fMovementUrgency = urgency;
                m_adaptiveUrgencyMaxPathLen = maxPathLen;

                AdjustPath();
            }
        }
    }
    protected override void HandleVisualStimulus(SAIEVENT pEvent)
    {
        float fGlobalVisualPerceptionScale = gEnv.pAISystem?.GetGlobalVisualScale(this) ?? 1.0f;
        float fVisualPerceptionScale = m_Parameters.m_PerceptionParams.perceptionScale.visual * fGlobalVisualPerceptionScale;
        if (gAIEnv.CVars.IgnoreVisualStimulus != 0 || m_Parameters.m_bAiIgnoreFgNode || fVisualPerceptionScale <= 0.0f)
            return;

        if (gAIEnv.pTargetTrackManager != null && gAIEnv.pTargetTrackManager.IsEnabled())
        {
            if (eFOV_Outside != IsPointInFOV(pEvent.vPosition, fVisualPerceptionScale))
            {
                gAIEnv.pTargetTrackManager.HandleStimulusFromAIEvent(GetAIObjectID(), pEvent, TargetTrackHelpers.eEST_Visual);
            }
        }
        else if (m_pPerceptionHandler != null)
            m_pPerceptionHandler.HandleVisualStimulus(pEvent);
    }
    protected override void HandleBulletRain(SAIEVENT pEvent)
    {
        base.HandleBulletRain(pEvent);

        if (gAIEnv.pTargetTrackManager != null && gAIEnv.pTargetTrackManager.IsEnabled())
            gAIEnv.pTargetTrackManager.HandleStimulusFromAIEvent(GetAIObjectID(), pEvent, TargetTrackHelpers.eEST_BulletRain);
        else if (m_pPerceptionHandler != null)
            m_pPerceptionHandler.HandleBulletRain(pEvent);
    }

    protected void UpdateAlertness()
    {
        int nextAlertness = GetProxy().GetAlertnessState();
        if ((m_Alertness != nextAlertness) && m_Parameters.factionHostility)
        {
            if (m_Alertness >= 0)
            {
                if (GetAISystem().m_AlertnessCounters[m_Alertness] > 0)
                {
                    --GetAISystem().m_AlertnessCounters[m_Alertness];
                }
            }

            if (NUM_ALERTNESS_COUNTERS > nextAlertness && nextAlertness >= 0)
            {
                ++GetAISystem().m_AlertnessCounters[nextAlertness];
            }
        }

        m_Alertness = nextAlertness;
    }
    protected void ResetAlertness()
    {
        if (m_Alertness >= 0 && m_Parameters.factionHostility &&
            GetAISystem().m_AlertnessCounters[m_Alertness] > 0)
            --GetAISystem().m_AlertnessCounters[m_Alertness];
        m_Alertness = -1;
    }

    protected bool SteerAroundVehicle(Vec3 targetPos, CAIObject obj, Vec3 predMyPos, Vec3 predObjectPos)
    {
        // if vehicle is in the same formation (convoy) - don't steer around it
        CAIVehicle pVehicle = obj.CastToCAIVehicle();
        if (pVehicle != null)
        {
            if (GetAISystem().SameFormation(this, pVehicle))
                return false;
        }
        return SteerAroundPuppet(targetPos, obj, predMyPos, predObjectPos);
    }
    protected bool SteerAroundPuppet(Vec3 targetPos, CAIObject obj, Vec3 predMyPos, Vec3 predObjectPos)
    {
        CPuppet pPuppet = obj.CastToCPuppet();
        if (pPuppet == null)
            return false;

        float avoidanceR = m_movementAbility.avoidanceRadius;
        avoidanceR += pPuppet.m_movementAbility.avoidanceRadius;
        float avoidanceRSq = sqr(avoidanceR);

        float maxAllowedSpeedMod = 10.0f;
        Vec3 steerOffset = new Vec3(0, 0, 0);

        bool outside = true;
        if (m_lastNavNodeIndex != 0 && gAIEnv.pGraph != null && ((uint)gAIEnv.pGraph.GetNodeManager().GetNode((uint)m_lastNavNodeIndex).navType & (IAISystem_NAV_TRIANGULAR | IAISystem_NAV_ROAD)) == 0)
            outside = false;

        Vec3 delta = predObjectPos - predMyPos;
        float distSq = delta.GetLengthSquared();
        if (distSq > avoidanceRSq)
            return false;

        float overtakeScale = 2.0f;
        float overtakeOffset = 0.1f;
        float criticalOvertakeMoveDot = 0.4f;

        float holdbackDist = 0.6f;
        if (obj.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_VEHICLE)
            holdbackDist = 15.0f;

        if (m_State.vMoveDir.Dot(pPuppet.GetState().vMoveDir) > criticalOvertakeMoveDot)
        {
            float myNormalSpeed, myMinSpeed, myMaxSpeed;
            GetMovementSpeedRange(m_State.fMovementUrgency, m_State.allowStrafing, out myNormalSpeed, out myMinSpeed, out myMaxSpeed);
            float otherNormalSpeed, otherMinSpeed, otherMaxSpeed;
            pPuppet.GetMovementSpeedRange(pPuppet.m_State.fMovementUrgency, pPuppet.m_State.allowStrafing, out otherNormalSpeed, out otherMinSpeed, out otherMaxSpeed);

            if (!outside || myNormalSpeed < overtakeScale * otherNormalSpeed + overtakeOffset)
            {
                float maxDesiredSpeed = max(myMinSpeed, obj.GetVelocity().Dot(m_State.vMoveDir));

                float dist = sqrtf(distSq);
                dist -= holdbackDist;

                float extraFrac = dist / holdbackDist;
                Limit(ref extraFrac, -0.1f, 0.1f);
                maxDesiredSpeed *= 1.0f + extraFrac;

                if (m_State.fDesiredSpeed > maxDesiredSpeed)
                    m_State.fDesiredSpeed = maxDesiredSpeed;

                return true;
            }
            avoidanceR *= 0.75f;
            avoidanceRSq = sqr(avoidanceR);
        }

        // steer around
        Vec3 aheadPos;
        if (m_pPathFollower != null)
        {
            float junk;
            aheadPos = m_pPathFollower.GetPathPointAhead(m_movementAbility.pathRadius, out junk);
            if (outside && Point_Point2DSq(aheadPos, predObjectPos) < sqr(avoidanceR))
            {
                float advanceFrac = 0.2f;
                m_pPathFollower.Advance(advanceFrac * avoidanceR);
            }
        }
        else
        {
            aheadPos = m_Path.CalculateTargetPos(predMyPos, 0.0f, 0.0f, m_movementAbility.pathRadius, true);
            if (outside && !m_Path.Empty() && Point_Point2DSq(aheadPos, predObjectPos) < sqr(avoidanceR))
            {
                float advanceFrac = 0.2f;
                Vec3 pathNextPoint = new Vec3(0, 0, 0);
                if (m_Path.GetPosAlongPath(out pathNextPoint, advanceFrac * avoidanceR, true, false))
                    m_Path.UpdatePathPosition(pathNextPoint, 100.0f, true, false);
            }
        }

        Vec3 toTargetDir = predObjectPos - predMyPos;
        toTargetDir = new Vec3(toTargetDir.x, toTargetDir.y, 0.0f);
        float toTargetDist = toTargetDir.NormalizeSafe();
        Vec3 forward2D = m_State.vMoveDir;
        forward2D = new Vec3(forward2D.x, forward2D.y, 0.0f);
        forward2D = forward2D.GetNormalizedSafe();
        Vec3 steerSideDir = new Vec3(forward2D.y, -forward2D.x, 0.0f);
        float toTargetDot = forward2D.Dot(toTargetDir);
        Limit(ref toTargetDot, 0.5f, 1.0f);

        float steerSign = steerSideDir.Dot(toTargetDir);
        steerSign = steerSign > 0.0f ? 1.0f : -1.0f;

        float toTargetDistScale = 1.0f - (toTargetDist / avoidanceR);
        Limit(ref toTargetDistScale, 0.0f, 1.0f);
        toTargetDistScale = sqrtf(toTargetDistScale);

        Vec3 thisSteerOffset = steerSideDir * (-toTargetDistScale * steerSign * toTargetDot);
        if (!outside)
            thisSteerOffset *= 0.1f;

        steerOffset += thisSteerOffset;

        float normalSpeed, minSpeed, maxSpeed;
        GetMovementSpeedRange(m_State.fMovementUrgency, m_State.allowStrafing, out normalSpeed, out minSpeed, out maxSpeed);
        if (m_State.fDesiredSpeed > maxAllowedSpeedMod * normalSpeed)
            m_State.fDesiredSpeed = maxAllowedSpeedMod * normalSpeed;

        if (!steerOffset.IsZero())
        {
            m_State.vMoveDir += steerOffset;
            m_State.vMoveDir = m_State.vMoveDir.GetNormalizedSafe();
        }
        return true;
    }
    protected bool SteerAround3D(Vec3 targetPos, CAIObject obj, Vec3 predMyPos, Vec3 predObjectPos)
    {
        CAIActor pActor = obj.CastToCAIActor();
        float avoidanceR = m_movementAbility.avoidanceRadius;
        avoidanceR += pActor.m_movementAbility.avoidanceRadius;

        Vec3 delta = predObjectPos - predMyPos;
        float distSq = delta.GetLengthSquared();
        if (distSq > sqr(avoidanceR))
            return false;

        Vec3 aheadPos = m_Path.CalculateTargetPos(predMyPos, 0.0f, 0.0f, m_movementAbility.pathRadius, true);
        if (Point_Point2DSq(aheadPos, predObjectPos) < sqr(avoidanceR))
        {
            float advanceFrac = 0.2f;
            Vec3 pathNextPoint = new Vec3(0, 0, 0);
            if (m_Path.GetPosAlongPath(out pathNextPoint, advanceFrac * avoidanceR, true, false))
                m_Path.UpdatePathPosition(pathNextPoint, 100.0f, true, false);
        }
        Vec3 toTargetDir = predObjectPos - predMyPos;
        toTargetDir = new Vec3(toTargetDir.x, toTargetDir.y, 0.0f);
        float toTargetDist = toTargetDir.NormalizeSafe();
        Vec3 forward2D = m_State.vMoveDir;
        forward2D = new Vec3(forward2D.x, forward2D.y, 0.0f);
        forward2D = forward2D.GetNormalizedSafe();
        Vec3 steerDir = new Vec3(forward2D.y, -forward2D.x, 0.0f);

        float toTargetDot = forward2D.Dot(toTargetDir);
        float steerSign = steerDir.Dot(toTargetDir);
        steerSign = steerSign > 0.0f ? 1.0f : -1.0f;

        float toTargetDistScale = 1.0f - (toTargetDist / avoidanceR);
        Limit(ref toTargetDistScale, 0.0f, 1.0f);
        toTargetDistScale = sqrtf(toTargetDistScale);

        m_State.vMoveDir = m_State.vMoveDir - steerDir * (toTargetDistScale * steerSign * toTargetDot);
        m_State.vMoveDir = m_State.vMoveDir.GetNormalizedSafe();

        return true;
    }
    protected bool NavigateAroundObjectsInternal(Vec3 targetPos, Vec3 myPos, bool in3D, CAIObject obj)
    {
        Vec3 objectPos = obj.GetPos();
        Vec3 delta = objectPos - myPos;
        float dot = m_State.vMoveDir.Dot(delta);
        if (dot < 0.001f)
            return false;
        CAIActor pActor = obj.CastToCAIActor();
        float avoidanceR = m_movementAbility.avoidanceRadius;
        avoidanceR += pActor.m_movementAbility.avoidanceRadius;
        float avoidanceRSq = sqr(avoidanceR);

        float distSq = delta.GetLengthSquared();
        if (distSq > avoidanceRSq)
            return false;

        return NavigateAroundAIObject(targetPos, obj, myPos, objectPos, true, in3D);
    }
    protected ENavInteraction NavigateAroundObjectsBasicCheck(CAIObject obj)
    {
        ENavInteraction ret = ENavInteraction.NI_IGNORE;

        if (obj != null)
        {
            IEntity pEntity = obj.GetEntity();
            if (pEntity != null && pEntity.IsActive())
            {
                if ((ushort)CryAISystem.EAIObjectType.AIOBJECT_VEHICLE == obj.GetType() || obj.IsEnabled())
                {
                    if (this != obj)
                    {
                        ret = GetNavInteraction(this, obj);
                    }
                }
            }
        }

        return ret;
    }
    protected bool NavigateAroundObjectsBasicCheck(Vec3 targetPos, Vec3 myPos, bool in3D, CAIObject obj, float extraDist)
    {
        bool ret = false;

        ENavInteraction navInteraction = NavigateAroundObjectsBasicCheck(obj);
        if (navInteraction != ENavInteraction.NI_IGNORE)
        {
            Vec3 objectPos = obj.GetPos();
            Vec3 delta = objectPos - myPos;
            CAIActor pActor = obj.CastToCAIActor();
            float avoidanceR = m_movementAbility.avoidanceRadius;
            avoidanceR += pActor.m_movementAbility.avoidanceRadius;
            avoidanceR += extraDist;
            float avoidanceRSq = sqr(avoidanceR);

            float distSq = delta.GetLengthSquared();
            if (!(distSq > avoidanceRSq))
            {
                ret = true;
            }
        }

        return ret;
    }

    protected bool IsSecondaryFireCommand() { return m_fireMode == EFireMode.FIREMODE_SECONDARY || m_fireMode == EFireMode.FIREMODE_SECONDARY_SMOKE; }
    protected bool IsMeleeFireCommand() { return m_fireMode == EFireMode.FIREMODE_MELEE || m_fireMode == EFireMode.FIREMODE_MELEE_FORCED; }

    protected void FireCommand(float updateTime)
    {
        CCCPOINT("CPuppet_FireCommand");

        m_timeSinceTriggerPressed += updateTime;

        if (m_fireMode == EFireMode.FIREMODE_OFF)
        {
            m_State.vAimTargetPos = new Vec3(0, 0, 0);
            m_State.vShootTargetPos = new Vec3(0, 0, 0);
            m_State.aimTargetIsValid = false;
            m_State.fire = EAIFireState.eAIFS_Off;
            m_State.fireSecondary = EAIFireState.eAIFS_Off;
            m_State.fireMelee = EAIFireState.eAIFS_Off;

            m_aimState = EAimState.AI_AIM_NONE;
            m_friendOnWayElapsedTime = 0.0f;

            ResetTargetTracking();

            m_targetBiasDirection *= 0.5f;
            m_burstEffectTime = 0.0f;
            m_burstEffectState = 0;

            return;
        }

        if (m_fireMode == (EFireMode)16 /*FIREMODE_VEHICLE*/)
            return;

        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();
        if (pAttentionTarget != null && ((pAttentionTarget.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_TARGET) || pAttentionTarget.IsAgent()))
            m_lastLiveTargetPos = pAttentionTarget.GetPos();

        bool lastAim = m_State.aimTargetIsValid;

        bool bIsSecondaryFire = IsSecondaryFireCommand() || m_bGrenadeThrowRequested;
        bool bIsMeleeFire = IsMeleeFireCommand();

        m_State.aimTargetIsValid = false;
        m_State.fire = EAIFireState.eAIFS_Off;

        bool wasSecondaryFire = (m_State.fireSecondary == EAIFireState.eAIFS_On || m_State.fireSecondary == EAIFireState.eAIFS_Blocking);
        if (wasSecondaryFire && !bIsSecondaryFire)
        {
            m_State.fireSecondary = EAIFireState.eAIFS_Off;
            m_pFireCmdGrenade?.Reset();
        }

        if (!bIsMeleeFire && m_State.fireMelee == EAIFireState.eAIFS_On)
        {
            m_State.fireMelee = EAIFireState.eAIFS_Off;
        }

        CWeakRef<CAIObject> refChooseTarget = m_refFireTarget.IsValid()
            ? m_refFireTarget
            : m_refAttentionTarget;

        if (bIsSecondaryFire)
        {
            if (m_fireModeUpdated)
            {
                m_pFireCmdGrenade?.Reset();
                m_fireModeUpdated = false;
            }

            ERequestedGrenadeType eReqType = m_eGrenadeThrowRequestType;
            if (m_bGrenadeThrowRequested)
            {
                switch (m_iGrenadeThrowTargetType)
                {
                    case AI_REG_ATTENTIONTARGET:
                        refChooseTarget = m_refAttentionTarget;
                        break;
                    case AI_REG_LASTOP:
                        refChooseTarget = m_refLastOpResult;
                        break;
                    case AI_REG_REFPOINT:
                        refChooseTarget = GetWeakRef(GetRefPoint());
                        break;
                    default:
                        break;
                }
            }
            else if (m_fireMode == EFireMode.FIREMODE_SECONDARY_SMOKE)
            {
                if (m_refLastOpResult.IsValid())
                    refChooseTarget = m_refLastOpResult;
                eReqType = ERequestedGrenadeType.eRGT_SMOKE_GRENADE;
            }

            if (refChooseTarget.IsValid())
            {
                CAIObject pTarget = refChooseTarget.GetAIObject();
                m_State.vAimTargetPos = pTarget.GetPos();
                m_State.aimTargetIsValid = true;
                m_aimState = EAimState.AI_AIM_READY;
                FireSecondary(pTarget, eReqType);
            }
            return;
        }

        QueryCurrentWeaponDescriptor();

        if (bIsMeleeFire)
        {
            if (refChooseTarget.IsValid())
            {
                CAIObject pTarget = refChooseTarget.GetAIObject();
                m_State.vShootTargetPos = pTarget.GetPos();
                m_State.vAimTargetPos = pTarget.GetPos();
                m_State.aimTargetIsValid = false;
                m_aimState = EAimState.AI_AIM_NONE;
                FireMelee(pTarget);
            }
            return;
        }

        if (m_pFireCmdHandler == null)
            return;

        bool targetValid = false;
        Vec3 aimTarget = new Vec3(0, 0, 0);
        CAIObject pTargetObj = refChooseTarget.GetAIObject();

        if (m_fireMode == EFireMode.FIREMODE_AIM || m_fireMode == EFireMode.FIREMODE_AIM_SWEEP ||
            m_fireMode == EFireMode.FIREMODE_FORCED || m_fireMode == EFireMode.FIREMODE_PANIC_SPREAD)
        {
            if (pTargetObj != null)
                targetValid = true;
        }
        else
        {
            if (pTargetObj != null)
            {
                CAIActor pTargetActor = pTargetObj.CastToCAIActor();
                if (pTargetActor != null)
                {
                    if (pTargetActor.IsActive())
                        targetValid = true;
                }
                else if (pTargetObj.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_TARGET)
                {
                    targetValid = true;
                }
                else if (pTargetObj.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_DUMMY)
                {
                    targetValid = true;
                }
                else if (pTargetObj.GetSubType() == ESubType.STP_MEMORY)
                {
                    targetValid = true;
                }
                else if (pTargetObj.GetSubType() == ESubType.STP_SOUND)
                {
                    targetValid = true;
                }
            }
        }

        if (gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_WARFACE)
            if (m_bLooseAttention && m_refLooseAttentionTarget.GetAIObject() != pTargetObj && GetSubType() != ESubType.STP_2D_FLY)
                targetValid = false;

        bool canFire = targetValid && m_fireDisabled == 0 && AllowedToFire();

        bool useLiveTargetForMemory = m_targetLostTime < m_CurrentWeaponDescriptor.coverFireTime;

        Vec3 aimTargetBeforeTargetTracking = new Vec3(0, 0, 0);

        if (targetValid && m_fireDisabled == 0)
        {
            aimTarget = pTargetObj.GetPos();

            uint enabledAccessories = GetParameters().m_weaponAccessories;
            if ((enabledAccessories & (uint)AIWEPA_LASER) != 0)
            {
                aimTarget = new Vec3(aimTarget.x, aimTarget.y, aimTarget.z - 0.05f);
            }

            if (GetSubType() != ESubType.STP_2D_FLY)
            {
                float distSqr = Point_Point2DSq(aimTarget, GetFirePos());
                if (distSqr < sqr(2.0f))
                {
                    Vec3 safePos = GetFirePos() + GetEntityDir() * 2.0f;
                    if (distSqr < sqr(0.7f))
                        aimTarget = safePos;
                    else
                    {
                        float speed = m_State.vMoveDir.GetLength();
                        speed = clamp_tpl(speed, 0.0f, 10.0f);

                        float d = sqrtf(distSqr);
                        float u = 1.0f - (d - 0.7f) / (2.0f - 0.7f);
                        aimTarget += speed / 10 * u * (safePos - aimTarget);
                    }
                }
            }

            if (m_pFireCmdHandler == null || m_pFireCmdHandler.UseDefaultEffectFor((int)m_fireMode))
            {
                switch (m_fireMode)
                {
                    case EFireMode.FIREMODE_PANIC_SPREAD:
                        HandleWeaponEffectPanicSpread(pTargetObj, ref aimTarget, ref canFire);
                        break;
                    case EFireMode.FIREMODE_BURST_DRAWFIRE:
                        HandleWeaponEffectBurstDrawFire(pTargetObj, ref aimTarget, ref canFire);
                        break;
                    case EFireMode.FIREMODE_BURST_SNIPE:
                        HandleWeaponEffectBurstSnipe(pTargetObj, ref aimTarget, ref canFire);
                        break;
                    case EFireMode.FIREMODE_AIM_SWEEP:
                        HandleWeaponEffectAimSweep(pTargetObj, ref aimTarget, ref canFire);
                        break;
                }
            }

            aimTargetBeforeTargetTracking = aimTarget;

            aimTarget = UpdateTargetTracking(GetWeakRef(pTargetObj), aimTarget);
        }
        else
        {
            ResetTargetTracking();

            m_targetBiasDirection *= 0.5f;
            m_burstEffectTime = 0.0f;
            m_burstEffectState = 0;

            if (gAIEnv.configuration.eCompatibilityMode == EConfigCompatibilityMode.ECCM_WARFACE)
            {
                aimTarget = UpdateTargetTracking(GetWeakRef(pTargetObj), aimTarget);
            }
        }

        float turnSpeed = canFire ? m_Parameters.m_fireTurnSpeed : m_Parameters.m_aimTurnSpeed;

        m_State.vAimTargetPos = (turnSpeed <= 0.0f)
            ? aimTarget
            : InterpolateLookOrAimTargetPos(m_State.vAimTargetPos, aimTarget, turnSpeed);

        if ((m_fireMode != EFireMode.FIREMODE_FORCED) && (m_fireMode != EFireMode.FIREMODE_OFF)
            && (m_State.vAimTargetPos.IsZero() || !CanAimWithoutObstruction(m_State.vAimTargetPos)))
        {
            m_State.aimObstructed = true;
            m_State.aimTargetIsValid = targetValid = canFire = false;
        }
        else
        {
            m_State.aimObstructed = false;
        }

        m_State.vShootTargetPos = m_State.vAimTargetPos;

        SAIWeaponInfo weaponInfo = new SAIWeaponInfo();
        GetProxy()?.QueryWeaponInfo(weaponInfo);

        IEntity pEntity = null;
        if (m_wasReloading && !weaponInfo.isReloading)
            SetSignal(1, "OnReloaded", (pEntity = GetEntity()), null, gAIEnv.SignalCRCs.m_nOnReloaded);

        if (weaponInfo.outOfAmmo || weaponInfo.isReloading)
            canFire = false;

        if (weaponInfo.lowAmmo)
        {
            if (!m_lowAmmoSent)
            {
                SetSignal(1, "OnLowAmmo", (pEntity ?? GetEntity()), null, gAIEnv.SignalCRCs.m_nOnLowAmmo);
                m_lowAmmoSent = true;
            }
        }
        else
        {
            m_lowAmmoSent = false;
        }

        if (!m_outOfAmmoSent)
        {
            if (weaponInfo.outOfAmmo)
            {
                SetSignal(1, "OnOutOfAmmo", (pEntity ?? GetEntity()), null, gAIEnv.SignalCRCs.m_nOnOutOfAmmo);

                m_outOfAmmoSent = true;
                m_outOfAmmoTimeOut = 0.0f;
                m_burstEffectTime = 0.0f;
                m_burstEffectState = 0;

                if (m_pFireCmdHandler != null)
                    m_pFireCmdHandler.OnReload();
            }
        }
        else
        {
            if (!weaponInfo.outOfAmmo)
                m_outOfAmmoSent = false;
            else if ((gAIEnv.configuration.eCompatibilityMode != EConfigCompatibilityMode.ECCM_WARFACE) && !weaponInfo.isReloading)
            {
                m_outOfAmmoTimeOut += updateTime;
                if (m_outOfAmmoTimeOut > 3.0f)
                    m_outOfAmmoSent = false;
            }
        }

        m_wasReloading = weaponInfo.isReloading;

        if (m_State.vAimTargetPos.IsZero())
        {
            m_State.aimTargetIsValid = false;
            m_aimState = m_State.aimObstructed ? EAimState.AI_AIM_OBSTRUCTED : EAimState.AI_AIM_NONE;
        }
        else
        {
            m_State.aimTargetIsValid = true;
            SAIBodyInfo bodyInfo = GetBodyInfo();
            m_aimState = bodyInfo.isAiming ? EAimState.AI_AIM_READY : (lastAim ? EAimState.AI_AIM_OBSTRUCTED : EAimState.AI_AIM_WAITING);
        }

        if (m_fireModeUpdated)
        {
            m_pFireCmdHandler.Reset();
            m_fireModeUpdated = false;
        }

        m_State.fire = (pTargetObj != null ? m_pFireCmdHandler.Update(pTargetObj, canFire, m_fireMode, m_CurrentWeaponDescriptor, m_State.vAimTargetPos) : EAIFireState.eAIFS_Off);

        if (m_State.fire == EAIFireState.eAIFS_On)
        {
            m_timeSinceTriggerPressed = 0.0f;
            m_State.vShootTargetPos = m_State.vAimTargetPos;

            m_State.projectileInfo.Reset();
            m_pFireCmdHandler.GetProjectileInfo(m_State.projectileInfo);
        }

        Vec3 overrideAimingPosition = new Vec3(0, 0, 0);
        m_State.vAimTargetPos = m_pFireCmdHandler.GetOverrideAimingPosition(out overrideAimingPosition)
            ? overrideAimingPosition : aimTargetBeforeTargetTracking;

        // Update accessories
        uint enabledAccessoriesFinal = GetParameters().m_weaponAccessories;
        m_State.weaponAccessories = enabledAccessoriesFinal & (uint)AIWEPA_LASER;

        if (IsAllowedToUseExpensiveAccessory())
        {
            if ((enabledAccessoriesFinal & (uint)AIWEPA_COMBAT_LIGHT) != 0)
            {
                if (GetAlertness() > 0)
                    m_State.weaponAccessories |= (uint)AIWEPA_COMBAT_LIGHT;
            }
            if ((enabledAccessoriesFinal & (uint)AIWEPA_PATROL_LIGHT) != 0)
            {
                m_State.weaponAccessories |= (uint)AIWEPA_PATROL_LIGHT;
            }
        }
    }
    protected void FireSecondary(CAIObject pTarget, ERequestedGrenadeType prefGrenadeType = ERequestedGrenadeType.eRGT_ANY)
    {
        bool bIsSecondaryFireCommand = IsSecondaryFireCommand();

        if (pTarget == null || m_pFireCmdGrenade == null)
        {
            if (bIsSecondaryFireCommand)
                SetFireMode(EFireMode.FIREMODE_OFF);
            return;
        }

        QueryCurrentWeaponDescriptor(true, prefGrenadeType);

        Vec3 targetPos = pTarget.GetPos();

        m_State.requestedGrenadeType = prefGrenadeType;
        m_State.fireSecondary = m_pFireCmdGrenade.Update(pTarget, true, m_fireMode, m_CurrentWeaponDescriptor, targetPos);
        m_State.vShootTargetPos = targetPos;

        m_State.projectileInfo.Reset();
        m_pFireCmdGrenade.GetProjectileInfo(m_State.projectileInfo);

        if (m_State.fireSecondary == EAIFireState.eAIFS_On && m_State.vShootTargetPos.IsZero())
        {
            m_pFireCmdGrenade.Reset();
            m_bGrenadeThrowRequested = false;
            m_State.fireSecondary = EAIFireState.eAIFS_Off;
        }
    }
    protected void FireMelee(CAIObject pTarget)
    {
        m_State.fireMelee = EAIFireState.eAIFS_Off;

        if (!CanDamageTargetWithMelee())
            return;

        if (m_fireMode != EFireMode.FIREMODE_MELEE_FORCED && (Point_PointSq(pTarget.GetPos(), GetPos()) > sqr(m_Parameters.m_fMeleeHitRange)))
            return;

        Vec3 dirToTarget = pTarget.GetPos() - GetPos();
        dirToTarget = new Vec3(dirToTarget.x, dirToTarget.y, 0.0f);
        dirToTarget = dirToTarget.GetNormalizedSafe();

        float dotProduct = dirToTarget.Dot(GetBodyInfo().vAnimBodyDir);
        if (m_fireMode != EFireMode.FIREMODE_MELEE_FORCED && dotProduct < m_Parameters.m_fMeleeAngleCosineThreshold)
            return;

        if (GetAttentionTargetType() == EAITargetType.AITARGET_MEMORY)
            return;

        if (!m_State.continuousMotion)
        {
            if (GetVelocity().GetLengthSquared() > 0.0001f)
                return;
        }

        m_State.fireMelee = EAIFireState.eAIFS_On;

        SetSignal(AISIGNAL_DEFAULT, "OnMeleeExecuted", GetEntity(), null, gAIEnv.SignalCRCs.m_nOnMeleeExecuted);
    }

    protected bool CheckTargetInRange(ref Vec3 vTargetPos)
    {
        Vec3 vTarget = vTargetPos - GetPos();
        float targetDist2 = vTarget.GetLengthSquared();

        float fMinDistance = m_CurrentWeaponDescriptor.fRangeMin;
        float fMaxDistance = m_CurrentWeaponDescriptor.fRangeMax;
        if (targetDist2 < fMinDistance * fMinDistance && fMinDistance > 0)
        {
            if (!m_bWarningTargetDistance)
            {
                SetSignal(0, "OnTargetTooClose", GetEntity(), null, gAIEnv.SignalCRCs.m_nOnTargetTooClose);
                m_bWarningTargetDistance = true;
            }
            return false;
        }
        else if (targetDist2 > fMaxDistance * fMaxDistance && fMaxDistance > 0)
        {
            if (!m_bWarningTargetDistance)
            {
                SetSignal(0, "OnTargetTooFar", GetEntity(), null, gAIEnv.SignalCRCs.m_nOnTargetTooFar);
                m_bWarningTargetDistance = true;
            }
            return false;
        }
        else
            m_bWarningTargetDistance = false;

        return true;
    }

    protected Vec3 GetHidePoint(MultimapRangeHideSpots hidespots, float fSearchDistance, Vec3 hideFrom, int nMethod, bool bSameOk, float fMinDistance)
    {
        // In the original C++ this is a complex hide spot selection method.
        // It iterates through sorted hide spots and picks the best one based on various criteria.
        // For the port, return zero vector as the hide spot system is not fully ported.
        return new Vec3(0, 0, 0);
    }

    protected bool Compromising(Vec3 pos, Vec3 dir, Vec3 hideFrom, Vec3 objectPos, Vec3 searchPos, bool bIndoor, bool bCheckVisibility)
    {
        if (!bIndoor)
        {
            // allow him to use only the hidespots closer to him than to the enemy
            Vec3 dirHideToEnemy = hideFrom - pos;
            Vec3 dirHide = pos - searchPos;
            if (dirHide.GetLengthSquared() > dirHideToEnemy.GetLengthSquared())
                return true;
        }
        // finally: check if the enemy is visible from there
        if (bCheckVisibility && GetAISystem().CheckPointsVisibility(pos, hideFrom, 5.0f))
            return true;

        return false;
    }

    protected void RegisterTargetAwareness(float amount)
    {
        // In C++ this updates the perception awareness for the current attention target.
        // For port purposes, this is a no-op if no perception handler.
        if (m_pPerceptionHandler != null)
        {
            CAIObject pTarget = m_refAttentionTarget.GetAIObject();
            if (pTarget != null)
                m_pPerceptionHandler.RegisterTargetAwareness(pTarget, amount);
        }
    }

    protected void UpdateTargetMovementState()
    {
        float dt = m_fTimePassed;
        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();

        if (pAttentionTarget == null || dt < 0.00001f)
        {
            m_lastTargetValid = false;
            m_targetApproaching = false;
            m_targetFleeing = false;
            m_targetApproach = 0;
            m_targetFlee = 0;
            return;
        }

        bool targetValid;
        switch (pAttentionTarget.GetType())
        {
            case (ushort)CryAISystem.EAIObjectType.AIOBJECT_TARGET:
            case (ushort)CryAISystem.EAIObjectType.AIOBJECT_PLAYER:
            case (ushort)CryAISystem.EAIObjectType.AIOBJECT_ACTOR:
                targetValid = true;
                break;
            default:
                targetValid = false;
                break;
        }

        Vec3 targetPos = pAttentionTarget.GetPos();
        Vec3 targetDir = pAttentionTarget.GetMoveDir();
        float targetSpeed = pAttentionTarget.GetVelocity().GetLength();
        Vec3 puppetPos = GetPos();

        if (!m_lastTargetValid)
        {
            m_lastTargetValid = true;
            m_lastTargetSpeed = targetSpeed;
            m_lastTargetPos = targetPos;
        }
        float fleeMin = 10.0f;
        float approachMax = 20.0f;

        if (!targetValid && m_lastTargetValid)
        {
            targetPos = m_lastTargetPos;
            targetSpeed = m_lastTargetSpeed;
        }

        {
            float curDist = Point_Point(targetPos, puppetPos);
            float lastDist = Point_Point(m_lastTargetPos, puppetPos);

            Vec3 dirTargetToPuppet = puppetPos - targetPos;
            dirTargetToPuppet = dirTargetToPuppet.GetNormalizedSafe();

            float dot = (1.0f + dirTargetToPuppet.Dot(targetDir)) * 0.5f;

            bool movingTowards = curDist < lastDist && targetSpeed > 0;
            bool movingAway = curDist > lastDist && targetSpeed > 0;

            if (curDist < approachMax && movingTowards)
                m_targetApproach += targetSpeed * sqr(dot) * 0.25f;
            else
                m_targetApproach -= dt * 2.0f;

            if (curDist > fleeMin && movingAway)
                m_targetFlee += targetSpeed * sqr(1.0f - dot) * 0.1f;
            else
                m_targetFlee -= dt * 2.0f;

            m_lastTargetPos = targetPos;
        }

        m_targetApproach = clamp_tpl(m_targetApproach, 0.0f, 10.0f);
        m_targetFlee = clamp_tpl(m_targetFlee, 0.0f, 10.0f);

        bool approaching = m_targetApproach > 9.9f;
        bool fleeing = m_targetFlee > 9.9f;

        if (approaching != m_targetApproaching)
        {
            m_targetApproaching = approaching;
            if (m_targetApproaching)
            {
                m_targetApproach = 0;
                SetSignal(1, "OnTargetApproaching", pAttentionTarget.GetEntity(), null, gAIEnv.SignalCRCs.m_nOnTargetApproaching);
            }
        }

        if (fleeing != m_targetFleeing)
        {
            m_targetFleeing = fleeing;
            if (m_targetFleeing)
            {
                m_targetFlee = 0;
                SetSignal(1, "OnTargetFleeing", pAttentionTarget.GetEntity(), null, gAIEnv.SignalCRCs.m_nOnTargetFleeing);
            }
        }
    }

    protected bool IsFriendInLineOfFire(CAIObject pFriend, Vec3 firePos, Vec3 fireDirection, bool cheapTest)
    {
        if (pFriend.GetProxy() == null)
            return false;
        IPhysicalEntity pPhys = pFriend.GetProxy().GetPhysics(true);
        if (pPhys == null)
            pPhys = pFriend.GetPhysics();
        if (pPhys == null)
            return false;

        Vec3 normalizedFireDirectionXY = new Vec3(fireDirection.x, fireDirection.y, 0.0f).GetNormalizedSafe();
        Vec3 directionFromFirePositionToOtherAgentPosition = pFriend.GetPhysicsPos() - firePos;
        directionFromFirePositionToOtherAgentPosition = new Vec3(directionFromFirePositionToOtherAgentPosition.x, directionFromFirePositionToOtherAgentPosition.y, 0.0f);
        directionFromFirePositionToOtherAgentPosition = directionFromFirePositionToOtherAgentPosition.GetNormalizedSafe();

        float threshold = cosf(DEG2RAD(35.0f));
        if (normalizedFireDirectionXY.Dot(directionFromFirePositionToOtherAgentPosition) > threshold)
        {
            float detectionSide = 0.2f;
            Vec3 fudge = new Vec3(detectionSide, detectionSide, detectionSide);

            pe_status_pos statusPos = new pe_status_pos();
            pPhys.GetStatus(statusPos);
            CryAISystem.CryCommon.AABB bounds = new CryAISystem.CryCommon.AABB(statusPos.BBox[0] - fudge + statusPos.pos, statusPos.BBox[1] + fudge + statusPos.pos);

            return CryAISystem.CryCommon.Overlap.Lineseg_Polygon2D(
                new CryAISystem.CryCommon.Lineseg(firePos, firePos + fireDirection),
                null); // Simplified — full Lineseg_AABB overlap not yet ported
        }

        return false;
    }

    protected void AdjustWithPrediction(CAIObject pTarget, ref Vec3 posOut)
    {
        if (pTarget == null || (GetSubType() == ESubType.STP_2D_FLY))
            return;

        float sp = m_CurrentWeaponDescriptor.fSpeed; // bullet speed
        if (sp > 0.0f)
        {
            if (pTarget.GetPhysics() != null)
            {
                pe_status_dynamics dyn = new pe_status_dynamics();
                pTarget.GetPhysics().GetStatus(dyn);

                Vec3 Q = GetFirePos();
                Vec3 X0 = posOut - Q;
                Vec3 V = dyn.v; // target velocity
                // solve a 2nd degree equation in t = time at which bullet and target will collide
                float x0v = X0.Dot(V);
                float v02 = V.GetLengthSquared();
                float w02 = sp * sp;
                float x02 = X0.GetLengthSquared();
                float b = x0v;
                float sq2 = x0v * x0v - x02 * (v02 - w02);
                if (sq2 < 0) // bullet can't reach the target
                    return;

                sq2 = sqrtf(sq2);
                float d = (v02 - w02);
                float t = (-b + sq2) / d;
                float t1 = (-b - sq2) / d;
                if ((t < 0 && t1 > 0) || (t1 > 0 && t1 < t))
                    t = t1;
                if (t < 0)
                    return;
                Vec3 W = X0 / t + V; // bullet velocity

                posOut = Q + W * t;
            }
        }
    }

    protected bool ActorObstructingAim(CAIActor pActor, Vec3 firePos, Vec3 dir, Ray fireRay)
    {
        if (pActor == null)
            return false;

        Vec3 normalizedFireDirectionXY = new Vec3(dir.x, dir.y, 0.0f).GetNormalizedSafe();
        Vec3 directionFromFirePositionToOtherAgentPosition = pActor.GetPhysicsPos() - firePos;
        directionFromFirePositionToOtherAgentPosition = new Vec3(directionFromFirePositionToOtherAgentPosition.x, directionFromFirePositionToOtherAgentPosition.y, 0.0f);
        directionFromFirePositionToOtherAgentPosition = directionFromFirePositionToOtherAgentPosition.GetNormalizedSafe();

        float threshold = cosf(DEG2RAD(35.0f));
        if (normalizedFireDirectionXY.Dot(directionFromFirePositionToOtherAgentPosition) > threshold)
        {
            // Check if ray intersects with other agent's bounding box
            IPhysicalEntity actorPhysics = pActor.GetPhysics();
            if (actorPhysics != null)
            {
                pe_status_pos ppos = new pe_status_pos();

                if (actorPhysics.GetStatus(ppos) != 0)
                {
                    Vec3 point;
                    CryAISystem.CryCommon.AABB box = new CryAISystem.CryCommon.AABB(ppos.pos + ppos.BBox[0], ppos.pos + ppos.BBox[1]);
                    if (Ray_AABB(fireRay, box, out point))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    protected void CreatePendingDeathReaction(int groupID, PendingDeathReaction pPendingDeathReaction)
    {
        // In C++ this creates a pending death reaction for group-based death signaling.
        // The implementation depends on the death reaction system which is not fully ported.
    }

    protected CPersonalInterestManager GetPersonalInterestManager()
    {
        return CCentralInterestManager.GetInstance()?.FindPIM(this);
    }

    protected CTimeValue m_fLastUpdateTime;

    protected bool m_bDryUpdate;

    protected DevaluedMap m_mapDevaluedPoints = new DevaluedMap();

    protected int m_Alertness;

    protected float m_fLastTimeAwareOfPlayer;
    protected TPlayerActionType m_playerAwarenessType;

    protected bool m_allowedToHitTarget;
    protected bool m_allowedToUseExpensiveAccessory;
    protected bool m_firingReactionTimePassed;
    protected float m_firingReactionTime;
    protected float m_outOfAmmoTimeOut;

    protected bool m_bWarningTargetDistance;

    protected CSpeedControl m_SpeedControl = new CSpeedControl();

    protected float m_chaseSpeed;
    protected float m_chaseSpeedRate;
    protected int m_lastChaseUrgencyDist;
    protected int m_lastChaseUrgencySpeed;

    protected CWeakRef<CAIVehicle> m_refAvoidedVehicle = new CWeakRef<CAIVehicle>();
    protected CTimeValue m_vehicleAvoidingTime;

    protected Vec3 m_vForcedNavigation;
    protected float m_fForcedNavigationSpeed;

    protected uint m_vehicleStickTarget;

    // typedef std::map< int, float > TMapBlockers;
    protected SortedDictionary<int, float> m_PFBlockers = new SortedDictionary<int, float>();

    protected uint m_currentWeaponId;
    protected AIWeaponDescriptor m_CurrentWeaponDescriptor = new AIWeaponDescriptor();

    protected bool m_bGrenadeThrowRequested;
    protected ERequestedGrenadeType m_eGrenadeThrowRequestType;
    protected int m_iGrenadeThrowTargetType;

    protected float m_allowedStrafeDistanceStart;
    protected float m_allowedStrafeDistanceEnd;
    protected bool m_allowStrafeLookWhileMoving;
    protected bool m_closeRangeStrafing;

    protected float m_strafeStartDistance;

    protected float m_adaptiveUrgencyMin;
    protected float m_adaptiveUrgencyMax;
    protected float m_adaptiveUrgencyScaleDownPathLen;
    protected float m_adaptiveUrgencyMaxPathLen;

    protected int m_delayedStance;
    protected int m_delayedStanceMovementCounter;

    protected enum ESeeTargetFrom
    {
        ST_HEAD,
        ST_WEAPON,
        ST_OFSETTED_LEFT,
        ST_OFSETTED_RIGHT,
    }
    protected float m_lastTimeSeeFromHead;

    protected float m_timeSinceTriggerPressed;
    protected float m_friendOnWayElapsedTime;

    protected bool m_bCoverFireEnabled;

    protected bool m_bCanBeShot;

    protected EMemoryFireType m_eMemoryFireType;

    protected DamagePartVector m_damageParts = new DamagePartVector();

    protected IPerceptionHandler m_pPerceptionHandler;
    protected IAIRateOfDeathHandler m_pRODHandler;
    public CPersonalInterestManager m_pPersonalInterestManager = new CPersonalInterestManager();

    // typedef std::vector<Vec3> TPointList;
    protected List<Vec3> m_InitialPath = new List<Vec3>();

    protected void UpdateHealthTracking()
    {
        // Update target health tracking.
        float fTargetHealth = 0.0f;
        float fTargetMaxHealth = 1.0f;

        // Convert the current health to normalized range (1.0 == normal max health).
        CAIObject pTarget = GetLiveTarget(m_refAttentionTarget).GetAIObject();
        IAIActorProxy pTargetProxy = (pTarget != null ? pTarget.GetProxy() : null);
        if (pTargetProxy != null)
        {
            CCCPOINT("CPuppet_UpdateHealthTracking");

            float fProxyMaxHealth = (float)pTargetProxy.GetActorMaxHealth();

            float fCurrHealth = (pTargetProxy.GetActorHealth() + pTargetProxy.GetActorArmor());
            float fCurrMaxHealth = (fProxyMaxHealth + pTargetProxy.GetActorMaxArmor());

            fTargetMaxHealth = fCurrMaxHealth / fProxyMaxHealth;
            fTargetHealth = fCurrHealth / fProxyMaxHealth;
        }

        // Calculate the rate of death.
        float fTargetStayAliveTime = GetTargetAliveTime();

        // This catches the case when the target turns on and off the armor.
        if (m_targetDamageHealthThr > fTargetMaxHealth)
            m_targetDamageHealthThr = fTargetMaxHealth;

        if (fTargetHealth >= m_targetDamageHealthThr || fTargetStayAliveTime <= float.Epsilon)
        {
            m_targetDamageHealthThr = fTargetHealth;
        }
        else
        {
            float fFrametime = GetAISystem().GetFrameDeltaTime();
            m_targetDamageHealthThr = max(0.0f, m_targetDamageHealthThr - (1.0f / fTargetStayAliveTime) * m_Parameters.m_fAccuracy * fFrametime);
        }

        if (gAIEnv.CVars.DebugDrawDamageControl > 0)
        {
            if (m_targetDamageHealthThrHistory == null)
                m_targetDamageHealthThrHistory = new CValueHistory<float>(100, 0.1f);
            m_targetDamageHealthThrHistory.Sample(m_targetDamageHealthThr, GetAISystem().GetFrameDeltaTime());

#if CRYAISYSTEM_DEBUG
            UpdateHealthHistory();
#endif
        }
    }

    public void ResetTargetTracking()
    {
        m_targetLastMissPoint = new Vec3(0, 0, 0);
        m_targetEscapeLastMiss = 0.0f;
        m_targetFocus = 0.0f;
        m_lastHitShotsCount = ~(nuint)0;
        m_lastMissShotsCount = ~(nuint)0;

        if (m_targetSilhouette.valid)
            m_targetSilhouette.Reset();
    }
    public Vec3 UpdateTargetTracking(CWeakRef<CAIObject> refTarget, Vec3 vTargetPos)
    {
        float fFrametime = GetAISystem().GetFrameDeltaTime();
        float fReactionTime = GetFiringReactionTime(vTargetPos);

        // Update the fire reaction timer
        UpdateFireReactionTimer(vTargetPos);

        CAIObject pTarget = refTarget.GetAIObject();
        CAIActor pLiveTarget = GetLiveTarget(refTarget).GetAIObject();
        if (pLiveTarget == null)
        {
            ResetTargetTracking();
            m_targetBiasDirection += (new Vec3(0, 0, -1) - m_targetBiasDirection) * fFrametime;
            return vTargetPos;
        }

        CCCPOINT("CPuppet_UpdateTargetTracking");

        // Update the current target's zone
        UpdateTargetZone(refTarget);

        float fFocusTargetValue = 0.0f;
        fFocusTargetValue += pTarget.GetVelocity().Length() / 3.0f;
        fFocusTargetValue += m_targetEscapeLastMiss;
        if (m_targetZone >= EAITargetZone.AIZONE_WARN)
            fFocusTargetValue += 1.0f;
        Limit(ref fFocusTargetValue, 0.0f, 1.0f);
        if (fFocusTargetValue > m_targetFocus)
            m_targetFocus += (fFocusTargetValue - m_targetFocus) * fFrametime;
        else
            m_targetFocus += (fFocusTargetValue - m_targetFocus) * 0.4f * fFrametime;

        // Calculate a silhouette which is later used to miss the player intentionally.
        if (!m_bDryUpdate || !m_targetSilhouette.valid)
        {
            m_targetSilhouette.valid = true;

            float MISS_PREDICTION_TIME = 0.8f;

            CryAISystem.CryCommon.AABB aabbCur, aabbNext;

            IPhysicalEntity pPhys = null;

            if (pLiveTarget != null)
            {
                pPhys = pLiveTarget.GetPhysics(true);
                if (pPhys == null)
                    pPhys = pLiveTarget.GetProxy() != null ? pLiveTarget.GetProxy().GetPhysics(true) : null;
            }
            else
            {
                pPhys = pTarget.GetPhysics(true);
                if (pPhys == null)
                    pPhys = pTarget.GetProxy() != null ? pTarget.GetProxy().GetPhysics(true) : null;
            }

            if (pPhys == null)
            {
                AILog.AILogComment("CPuppet::UpdateTargetTracking() Target {0} does not have physics!", pTarget.GetName());
                ResetTargetTracking();
                m_targetBiasDirection += (new Vec3(0, 0, -1) - m_targetBiasDirection) * fFrametime;
                return vTargetPos;
            }

            pe_status_pos statusPos = new pe_status_pos();
            pPhys.GetStatus(statusPos);
            aabbCur = new CryAISystem.CryCommon.AABB(CryAISystem.CryCommon.AABB.RESET);
            aabbCur.Add(statusPos.BBox[0] + statusPos.pos);
            aabbCur.Add(statusPos.BBox[1] + statusPos.pos);
            aabbNext = aabbCur;
            aabbCur.min = new Vec3(aabbCur.min.x, aabbCur.min.y, aabbCur.min.z - 0.05f);
            aabbNext.min = new Vec3(aabbNext.min.x, aabbNext.min.y, aabbNext.min.z - 0.05f);

            Vec3 vel = pTarget.GetVelocity() * MISS_PREDICTION_TIME;
            aabbNext.Move(vel);

            // Create a list of points which is used to calculate the silhouette.
            Vec3[] points = new Vec3[16];
            StdAfx.SetAABBCornerPoints(aabbCur, points);
            Vec3[] points2 = new Vec3[8];
            StdAfx.SetAABBCornerPoints(aabbNext, points2);
            System.Array.Copy(points2, 0, points, 8, 8);

            m_targetSilhouette.center = aabbCur.GetCenter() + vel * 0.5f;

            // Project points on a plane between the shooter and the target.
            Vec3 dir = m_targetSilhouette.center - GetPos();
            dir = dir.GetNormalizedSafe();
            m_targetSilhouette.baseMtx.SetRotationVDir(dir, 0.0f);

            Vec3 u = m_targetSilhouette.baseMtx.GetColumn0();
            Vec3 v = m_targetSilhouette.baseMtx.GetColumn2();

            s_projectedPoints.Clear();
            for (int i = 0; i < 16; ++i)
            {
                Vec3 pt = points[i] - m_targetSilhouette.center;
                s_projectedPoints.Add(new Vec3(u.Dot(pt), v.Dot(pt), 0.0f));
            }

            // The silhouette is the convex hull of all the points in the two AABBs.
            m_targetSilhouette.points.Clear();
            AICollision.ConvexHull2D(m_targetSilhouette.points, s_projectedPoints);
        }

        // Calculate a direction that is used to calculate the miss points on the silhouette.
        Vec3 desiredTargetBias = new Vec3(0, 0, 0);

        // 1) Bend the direction towards the target movement direction.
        if (pTarget != null)
            desiredTargetBias += pTarget.GetVelocity().GetNormalizedSafe();

        // 2) Bend the direction towards the point that is visible to the shooter.
        desiredTargetBias += ((vTargetPos - m_targetSilhouette.center) / 2.0f) * (1 - m_targetEscapeLastMiss);

        // 3) Bend the direction towards ground if not trying to adjust the away from obstructed area.
        desiredTargetBias = new Vec3(desiredTargetBias.x, desiredTargetBias.y, desiredTargetBias.z - 0.1f - 0.5f * (1 - m_targetEscapeLastMiss));

        // 4) If the current aim is obstructed, try to climb the silhouette to the other side.
        if (m_targetEscapeLastMiss > 0.0f && !m_targetLastMissPoint.IsZero())
        {
            Vec3 deltaProj = m_targetSilhouette.ProjectVectorOnSilhouette(m_targetBiasDirection);
            deltaProj = deltaProj.GetNormalizedSafe();
            if (!deltaProj.IsZero())
            {
                float lastMissAngle = atan2f(deltaProj.y, deltaProj.x);

                Vec3 u2 = m_targetSilhouette.baseMtx.GetColumn0();
                Vec3 v2 = m_targetSilhouette.baseMtx.GetColumn2();

                // Choose the climb direction based on the current side
                float a = u2.Dot(m_targetBiasDirection) < 0.0f ? -gf_PI / 2 : gf_PI / 2;

                float xVal = cosf(lastMissAngle + a);
                float yVal = sinf(lastMissAngle + a);

                desiredTargetBias += (u2 * xVal + v2 * yVal) * m_targetEscapeLastMiss;
            }
        }

        if (desiredTargetBias.NormalizeSafe() > 0.0f)
        {
            m_targetBiasDirection += (desiredTargetBias - m_targetBiasDirection) * 4.0f * fFrametime;
            m_targetBiasDirection = m_targetBiasDirection.GetNormalizedSafe();
        }

        m_targetPosOnSilhouettePlane = m_targetSilhouette.IntersectSilhouettePlane(GetFirePos(), vTargetPos);
        Vec3 targetPosOnSilhouettePlaneProj = m_targetSilhouette.ProjectVectorOnSilhouette(m_targetPosOnSilhouettePlane - m_targetSilhouette.center);

        // Calculate the distance between the target pos and the silhouette.
        if (Point_Polygon2D(targetPosOnSilhouettePlaneProj, m_targetSilhouette.points))
        {
            // Inside the polygon, zero dist.
            m_targetDistanceToSilhouette = 0.0f;
        }
        else
        {
            // Distance to the nearest edge.
            Vec3 ptNearest;
            m_targetDistanceToSilhouette = CryAISystem.CryCommon.Distance.Point_Polygon2D(targetPosOnSilhouettePlaneProj, m_targetSilhouette.points, out ptNearest);
        }

        return vTargetPos;
    }
    public void UpdateFireReactionTimer(Vec3 vTargetPos)
    {
        float fFrametime = GetAISystem().GetFrameDeltaTime();
        float fReactionTime = GetFiringReactionTime(vTargetPos);

        // Update the fire reaction timer
        bool bReacting = false;
        if (IsAllowedToHitTarget() && AllowedToFire())
        {
            if (GetAttentionTargetType() == EAITargetType.AITARGET_VISUAL && GetAttentionTargetThreat() == EAITargetThreat.AITHREAT_AGGRESSIVE)
            {
                m_firingReactionTime = min(m_firingReactionTime + fFrametime, fReactionTime + 0.001f);
                bReacting = true;
            }
        }

        if (!bReacting)
        {
            m_firingReactionTime = max(0.0f, m_firingReactionTime - fFrametime);
        }

        m_firingReactionTimePassed = m_firingReactionTime >= fReactionTime;
    }
    public void UpdateTargetZone(CWeakRef<CAIObject> refTarget)
    {
        CAIObject pTarget = refTarget.GetAIObject();
        if (pTarget == null)
        {
            m_targetZone = EAITargetZone.AIZONE_OUT;
            return;
        }

        // Delegate to handler if available
        if (m_pRODHandler != null)
        {
            m_targetZone = m_pRODHandler.GetTargetZone(this, pTarget);
        }
        else
        {
            float fKillRange = gAIEnv.CVars.RODKillRangeMod;
            float fCombatRange = gAIEnv.CVars.RODCombatRangeMod;

            // Calculate off of attack range
            float fDistToTargetSqr = Point_PointSq(GetPos(), pTarget.GetPos());
            if (fDistToTargetSqr < sqr(m_Parameters.m_fAttackRange * fKillRange))
                m_targetZone = EAITargetZone.AIZONE_KILL;
            else if (fDistToTargetSqr < sqr(m_Parameters.m_fAttackRange * (fCombatRange + fKillRange) / 2))
                m_targetZone = EAITargetZone.AIZONE_COMBAT_NEAR;
            else if (fDistToTargetSqr < sqr(m_Parameters.m_fAttackRange * fCombatRange))
                m_targetZone = EAITargetZone.AIZONE_COMBAT_FAR;
            else if (fDistToTargetSqr < sqr(m_Parameters.m_fAttackRange))
                m_targetZone = EAITargetZone.AIZONE_WARN;
            else
                m_targetZone = EAITargetZone.AIZONE_OUT;
        }
    }

    protected struct LineOfFireState
    {
        public LineOfFireState(int dummy = 0)
        {
            asyncState = AsyncState.AsyncReady;
            rayID = new QueuedRayID();
            result = false;
            softDistance = 0.0f;
        }

        public void Swap(ref LineOfFireState other)
        {
            (asyncState, other.asyncState) = (other.asyncState, asyncState);
            (rayID, other.rayID) = (other.rayID, rayID);
            (softDistance, other.softDistance) = (other.softDistance, softDistance);
            (result, other.result) = (other.result, result);
        }

        public AsyncState asyncState;
        public QueuedRayID rayID;
        public float softDistance;
        public bool result;
    }

    protected /*mutable*/ LineOfFireState m_lineOfFireState = new LineOfFireState();
    protected void LineOfFireRayComplete(QueuedRayID rayID, RayCastResult result)
    {
        m_lineOfFireState.asyncState = AsyncState.AsyncReady;
        m_lineOfFireState.rayID = new QueuedRayID();

        m_lineOfFireState.result = true;
        if (result)
        {
            m_lineOfFireState.result = false;

            pe_status_pos stat = new pe_status_pos();
            // In C++ checks result[0].pCollider->GetStatus(&stat) and stat.flagsOR & geom_colltype_obstruct
            // Simplified: if there's a hit closer than soft distance, it's obstructed
            if (result[0].dist < m_lineOfFireState.softDistance)
                m_lineOfFireState.result = false;
            else
                m_lineOfFireState.result = true;
        }
    }

    protected struct ValidTargetState
    {
        public ValidTargetState(int dummy = 0)
        {
            asyncState = AsyncState.AsyncReady;
            rayID = new QueuedRayID();
            latestHitDist = float.MaxValue;
        }

        public AsyncState asyncState;
        public QueuedRayID rayID;
        public float latestHitDist;
    }

    protected ValidTargetState m_validTargetState = new ValidTargetState();
    protected void FireTargetValidRayComplete(QueuedRayID rayID, RayCastResult result)
    {
        if (m_validTargetState.rayID.Equals(rayID))
        {
            m_validTargetState.rayID = new QueuedRayID();
            m_validTargetState.asyncState = AsyncState.AsyncReady;

            m_validTargetState.latestHitDist = result ? result[0].dist : float.MaxValue;
        }
    }

    protected void QueueFireTargetValidRay(CAIObject targetObj, Vec3 firePos, Vec3 fireDir)
    {
        m_validTargetState.asyncState = AsyncState.AsyncInProgress;

        PhysSkipList skipList = new PhysSkipList();
        GetPhysicalSkipEntities(skipList);

        if (targetObj != null)
        {
            if (targetObj.IsAgent())
            {
                CAIActor targetActor = targetObj.CastToCAIActor();
                if (targetActor != null)
                    targetActor.GetPhysicalSkipEntities(skipList);
            }
            else
            {
                IPhysicalEntity physicalEnt = targetObj.GetPhysics();
                if (physicalEnt != null)
                    stl.push_back_unique(skipList, physicalEnt);
            }
        }

        // In C++ this queues an async ray. In C# the ray cast result would be handled via callback.
        // For now, set state to ready with max distance (no obstruction).
        m_validTargetState.asyncState = AsyncState.AsyncReady;
        m_validTargetState.latestHitDist = float.MaxValue;
    }

    private static Vec3 JitterVector(Vec3 v, Vec3 amount)
    {
        return new Vec3(
            v.x + cry_random(-amount.x, amount.x),
            v.y + cry_random(-amount.y, amount.y),
            v.z + cry_random(-amount.z, amount.z));
    }

    public bool AdjustFireTarget(CAIObject targetObject, Vec3 target, bool hit, float missExtraOffset, float clampAngle, out Vec3 posOut)
    {
        Vec3 @out = target;

        if (hit)
        {
            Vec3 hitOut;
            if (!CalculateHitPointOnTarget(targetObject, target, clampAngle, out hitOut))
            {
                @out = JitterVector(target, new Vec3(0.05f, 0.05f, 0.05f));
                AdjustWithPrediction(targetObject, ref @out);
            }
            else
            {
                @out = hitOut;
            }

            m_targetLastMissPoint = @out;

            if (IsFireTargetValid(@out, targetObject))
            {
                posOut = @out;
                return true;
            }

            m_lastHitShotsCount = ~(nuint)0;
        }
        else
        {
            nuint shotsCount = m_pFireCmdHandler != null ? (nuint)m_pFireCmdHandler.GetShotCount() : 0;
            if (m_lastMissShotsCount == shotsCount && ESubType.STP_HELICRYSIS2 != GetSubType())
                @out = m_targetLastMissPoint;
            else
            {
                bool found = false;

                if (targetObject != null && gAIEnv.CVars.EnableCoolMisses != 0 && cry_random(0.0f, 1.0f) < gAIEnv.CVars.CoolMissesProbability)
                {
                    CAIPlayer player = targetObject.CastToCAIPlayer();
                    if (player != null)
                    {
                        Vec3 fireLocation = GetFirePos();
                        Vec3 dir = target - fireLocation;

                        float distance = dir.NormalizeSafe();

                        Vec3 coolMissOut;
                        if (distance >= gAIEnv.CVars.CoolMissesMinMissDistance)
                            found = player.GetMissLocation(fireLocation, dir, clampAngle, out coolMissOut);
                        else
                            coolMissOut = @out;
                        if (found)
                            @out = coolMissOut;
                    }
                }

                Vec3 missOut = target;
                if (!found)
                    found = CalculateMissPointOutsideTargetSilhouette(targetObject, target, missExtraOffset, out missOut);

                if (!found)
                    @out = JitterVector(target, new Vec3(0.15f, 0.15f, 0.15f));
                else if (@out == target) // was not set by cool misses
                    @out = missOut;
            }

            m_targetLastMissPoint = @out;

            if (IsFireTargetValid(@out, targetObject))
            {
                m_lastMissShotsCount = shotsCount;
                m_targetEscapeLastMiss = clamp_tpl(m_targetEscapeLastMiss - 0.1f, 0.0f, 1.0f);
                posOut = @out;
                return true;
            }

            m_lastMissShotsCount = ~(nuint)0;
            m_targetEscapeLastMiss = clamp_tpl(m_targetEscapeLastMiss + 0.1f, 0.0f, 1.0f);
        }

        posOut = @out;
        return false;
    }
    public bool CalculateHitPointOnTarget(CAIObject targetObject, Vec3 target, float clampAngle, out Vec3 posOut)
    {
        posOut = target;

        if (!targetObject.IsAgent())
            return false;

        CAIActor targetActor = targetObject.CastToCAIActor();
        if (targetActor == null)
            return false;

        if (targetActor.GetDamageParts() == null)
            return false;

        DamagePartVector parts = targetActor.GetDamageParts();
        int partCount = parts.Count;

        if (partCount == 0)
            return false;

        if (m_fireMode == EFireMode.FIREMODE_KILL)
        {
            float maxMult = 0.0f;

            for (int i = 0; i < partCount; ++i)
            {
                if (parts[i].damageMult > maxMult)
                {
                    posOut = parts[i].pos;
                    maxMult = parts[i].damageMult;
                }
            }

            return maxMult > 0.0f;
        }

        Vec3 FireLocation = GetFirePos();
        Vec3 pos;

        nuint shotsCount = m_pFireCmdHandler != null ? (nuint)m_pFireCmdHandler.GetShotCount() : 0;
        if ((m_lastTargetPart < (nuint)partCount) && (m_lastHitShotsCount == shotsCount))
        {
            pos = parts[(int)m_lastTargetPart].pos;
        }
        else
        {
            m_lastHitShotsCount = shotsCount;

            s_weights.Clear();
            for (int i = 0; i < partCount; i++)
                s_weights.Add((0f, (nuint)i));

            CryAISystem.CryCommon.Lineseg LineOfFire = new CryAISystem.CryCommon.Lineseg(FireLocation, target);

            float t;
            for (int i = 0; i < partCount; ++i)
                s_weights[i] = (CryAISystem.CryCommon.Distance.Point_LinesegSq(parts[i].pos, LineOfFire, out t), (nuint)i);

            s_weights.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            int considerCount = System.Math.Min(8, partCount);

            nuint targetPart = s_weights[(int)cry_random((nuint)0, (nuint)(considerCount - 1))].Item2;
            m_lastTargetPart = targetPart;
            pos = parts[(int)targetPart].pos;
        }

        float jitterAmount = 0.075f;
        pos = JitterVector(pos, new Vec3(jitterAmount, jitterAmount, jitterAmount));

        // Add prediction based on bullet and target speeds
        AdjustWithPrediction(targetActor, ref pos);

        Vec3 dir = m_State.vAimTargetPos - FireLocation;
        dir = dir.Normalized();

        SAIBodyInfo bodyInfoRef = GetBodyInfo();
        IEntity pLinkedVehicleEntity = bodyInfoRef.GetLinkedVehicleEntity();
        if (pLinkedVehicleEntity != null)
        {
            if (pLinkedVehicleEntity.HasAI())
            {
                if (dir.Dot(bodyInfoRef.vFireDir) < cos_tpl(clampAngle))
                    return false;
            }
        }

        posOut = pos;
        return true;
    }
    private static float DeltaAngle(float a, float b)
    {
        float d = b - a;
        d = fmodf(d, gf_PI2);
        if (d < -gf_PI) d += gf_PI2;
        if (d > gf_PI) d -= gf_PI2;
        return d;
    }

    public bool CalculateMissPointOutsideTargetSilhouette(CAIObject targetObject, Vec3 target, float missExtraOffset, out Vec3 posOut)
    {
        posOut = target;

        if (m_targetSilhouette.valid)
        {
            Vec3 silhouettePlanePoint = m_targetSilhouette.IntersectSilhouettePlane(GetFirePos(), target);
            Vec3 silhouettePoint = m_targetSilhouette.ProjectVectorOnSilhouette(silhouettePlanePoint - m_targetSilhouette.center);

            if (Point_Polygon2D(silhouettePoint, m_targetSilhouette.points))
            {
                Vec3 bias = m_targetSilhouette.ProjectVectorOnSilhouette(m_targetBiasDirection)
                    .GetNormalizedSafe();
                float spread = DEG2RAD(60.0f - 50.0f * m_targetFocus);
                float angleLimit = DEG2RAD(50.0f);

                float angle = (1.5f * atan2_tpl(bias.y, bias.x)) + cry_random(-1.0f, 1.0f) * spread * 0.5f;

                if (!m_targetLastMissPoint.IsZero())
                {
                    Vec3 lastMiss = m_targetSilhouette.ProjectVectorOnSilhouette(m_targetLastMissPoint - m_targetSilhouette.center);

                    float lastMissAngle = atan2_tpl(lastMiss.y, lastMiss.x);
                    float deltaAngle = DeltaAngle(lastMissAngle, angle);

                    Limit(ref deltaAngle, -angleLimit, angleLimit);
                    angle = lastMissAngle + deltaAngle;
                }

                Vec3 pt;
                Vec3 dir = new Vec3(cos_tpl(angle), sin_tpl(angle), 0.0f);

                if (Lineseg_Polygon2D(new CryAISystem.CryCommon.Lineseg(new Vec3(0, 0, 0), dir * 100.0f), m_targetSilhouette.points, out pt))
                {
                    Vec3 u = m_targetSilhouette.baseMtx.GetColumn0();
                    Vec3 v = m_targetSilhouette.baseMtx.GetColumn2();

                    Vec3 missTarget = m_targetSilhouette.center +
                        u * (pt.x + dir.x * missExtraOffset) + v * (pt.y + dir.y * missExtraOffset);

                    if (missTarget.z >= target.z + 0.15f)
                        missTarget = new Vec3(missTarget.x, missTarget.y, missTarget.z - (missTarget.z - target.z));

                    posOut = missTarget;

                    return true;
                }
            }

            posOut = target;
            return true;
        }

        return false;
    }

    public bool IsFireTargetValid(Vec3 targetPos, CAIObject pTargetObject)
    {
        // Accept the point if:
        // 1) Shooting in the direction hits something relatively far away
        // 2) There are no friendly units between the shooter and the target

        Vec3 firePos = GetFirePos();
        Vec3 fireDir = targetPos - firePos;

        if (m_fireMode != EFireMode.FIREMODE_BURST_DRAWFIRE)
        {
            float fireDirLen = fireDir.NormalizeSafe();

            float minCheckDist = 1.0f;
            float maxCheckDist = 15.0f;
            float requiredTravelDistPercent = 0.25f;
            float requiredDist = clamp_tpl(fireDirLen * requiredTravelDistPercent, minCheckDist, maxCheckDist);

            if (m_validTargetState.asyncState == AsyncState.AsyncReady)
                QueueFireTargetValidRay(pTargetObject, firePos, fireDir * requiredDist);

            if (m_validTargetState.latestHitDist < requiredDist)
                return false;
        }

        return !CheckFriendsInLineOfFire(fireDir, false);
    }
    public float GetCoverFireTime()
    {
        return m_CurrentWeaponDescriptor.coverFireTime * gAIEnv.CVars.RODCoverFireTimeMod;
    }
    public float GetBurstFireDistanceScale()
    {
        float fResult = 1.0f;

        switch (m_targetZone)
        {
            case EAITargetZone.AIZONE_KILL: fResult = 1.0f; break;
            case EAITargetZone.AIZONE_COMBAT_NEAR: fResult = 0.9f; break;
            case EAITargetZone.AIZONE_COMBAT_FAR: fResult = 0.9f; break;
            case EAITargetZone.AIZONE_WARN: fResult = 0.4f; break;
            case EAITargetZone.AIZONE_OUT: fResult = 0.0f; break;

            // Ignore or default don't alter the scale
            case EAITargetZone.AIZONE_IGNORE:
            default:
                fResult = 1.0f;
                break;
        }

        return fResult;
    }

    public float GetTargetAliveTime()
    {
        float fTargetStayAliveTime = gAIEnv.CVars.RODAliveTime;

        CAIActor pLiveTarget = GetLiveTarget(m_refAttentionTarget).GetAIObject();
        if (pLiveTarget == null)
            return fTargetStayAliveTime;

        CAIActor pLiveActor = pLiveTarget.CastToCAIActor();
        if (pLiveActor == null)
            return fTargetStayAliveTime;

        CCCPOINT("CPuppet_GetTargetAliveTime");

        // Delegate to handler if available
        if (m_pRODHandler != null)
        {
            fTargetStayAliveTime = m_pRODHandler.GetTargetAliveTime(this, pLiveTarget, m_targetZone, m_targetDazzlingTime);
        }
        else
        {
            Vec3 vTargetDir = pLiveTarget.GetPos() - GetPos();
            float fTargetDist = vTargetDir.NormalizeSafe();

            // Scale target life time based on target speed.
            float fMoveInc = gAIEnv.CVars.RODMoveInc;
            {
                float fIncrease = 0.0f;

                Vec3 vTargetVel = pLiveTarget.GetVelocity();
                float fSpeed = vTargetVel.GetLength2D();

                if ((pLiveTarget.GetType() == (ushort)CryAISystem.EAIObjectType.AIOBJECT_PLAYER) &&
                    (m_fireMode != EFireMode.FIREMODE_MELEE) && (m_fireMode != EFireMode.FIREMODE_MELEE_FORCED))
                {
                    // Super speed run or super jump.
                    if (fSpeed > 12.0f || vTargetVel.z > 7.0f)
                    {
                        fIncrease += fMoveInc * 2.0f;
                        // Dazzle the shooter for a moment.
                        m_targetDazzlingTime = 1.0f;
                    }
                    else if (fSpeed > 6.0f)
                    {
                        fIncrease += fMoveInc;
                    }
                }
                else if (fSpeed > 6.0f)
                {
                    fIncrease *= fMoveInc;
                }

                fTargetStayAliveTime += fIncrease;
            }

            // Scale target life time based on target stance.
            float fStanceInc = gAIEnv.CVars.RODStanceInc;
            {
                float fIncrease = 0.0f;

                SAIBodyInfo bi = pLiveActor.GetBodyInfo();
                if (bi.stance == EStance.STANCE_CROUCH && m_targetZone > EAITargetZone.AIZONE_KILL)
                    fIncrease += fStanceInc;
                else if (bi.stance == EStance.STANCE_PRONE && m_targetZone >= EAITargetZone.AIZONE_COMBAT_FAR)
                    fIncrease += fStanceInc * fStanceInc;

                fTargetStayAliveTime += fIncrease;
            }

            // Scale target life time based on target vs. shooter orientation.
            float fDirectionInc = gAIEnv.CVars.RODDirInc;
            {
                float fIncrease = 0.0f;

                float thr1 = cosf(DEG2RAD(30.0f));
                float thr2 = cosf(DEG2RAD(95.0f));
                float dot = -vTargetDir.Dot(pLiveTarget.GetViewDir());
                if (dot < thr2)
                    fIncrease += fDirectionInc * 2.0f;
                else if (dot < thr1)
                    fIncrease += fDirectionInc;

                fTargetStayAliveTime += fIncrease;
            }

            if (!m_allowedToHitTarget)
            {
                // If the agent is set not to be allowed to hit the target, let the others shoot first.
                float fAmbientFireInc = gAIEnv.CVars.RODAmbientFireInc;
                fTargetStayAliveTime += fAmbientFireInc;
            }
            else if (m_targetZone == EAITargetZone.AIZONE_KILL)
            {
                // Kill much faster when the target is in kill-zone (but not if in a vehicle)
                SAIBodyInfo bi = GetBodyInfo();
                if (bi.GetLinkedVehicleEntity() == null)
                {
                    float fKillZoneInc = gAIEnv.CVars.RODKillZoneInc;
                    fTargetStayAliveTime += fKillZoneInc;
                }
            }
        }

        CCCPOINT("CPuppet_GetTargetAliveTime_A");
        return max(0.0f, fTargetStayAliveTime);
    }

    public Vec3 InterpolateLookOrAimTargetPos(Vec3 current, Vec3 target, float maxRatePerSec)
    {
        if ((!target.IsZero()) && !target.IsEquivalent(m_vLastPosition))
        {
            // Interpolate.
            Vec3 curDir;
            if (current.IsZero())
            {
                curDir = GetEntityDir();
            }
            else
            {
                curDir = current - GetPos();
                curDir = curDir.GetNormalizedSafe();
            }

            Vec3 reqDir = target - m_vLastPosition;
            float dist = reqDir.NormalizeSafe();

            // Slerp
            float cosAngle = curDir.Dot(reqDir);

            float eps = 1e-6f;
            float maxRate = maxRatePerSec * GetAISystem().GetFrameDeltaTime();
            float thr = cosf(maxRate);

            if (cosAngle > thr || maxRate < eps || dist < eps)
            {
                return target;
            }
            else
            {
                float angle = acos_tpl(cosAngle);

                // Allow higher rate when over 90degrees.
                float piOverFour = gf_PI / 4.0f;
                float rcpPIOverFour = 1.0f / piOverFour;
                float scale = 1.0f + clamp_tpl((angle - piOverFour) * rcpPIOverFour, 0.0f, 1.0f) * 2.0f;
                if (angle < eps || angle < maxRate * scale)
                    return target;

                float t = (maxRate * scale) / angle;

                Quat curView = new Quat();
                curView.SetRotationVDir(curDir);
                Quat reqView = new Quat();
                reqView.SetRotationVDir(reqDir);

                Quat view = new Quat();
                view.SetSlerp(curView, reqView, t);

                return GetPos() + (view * FORWARD_DIRECTION) * dist;
            }
        }

        // Clear look target.
        return target;
    }

    public void HandleBurstFireInit()
    {
        // When starting burst in warn zone, force first bullets to miss
        if (m_targetZone == EAITargetZone.AIZONE_WARN)
            m_targetSeenTime = System.Math.Max(0.0f, m_targetSeenTime - (cry_random(1, 3)) / 10.0f);
    }
    public void HandleWeaponEffectBurstDrawFire(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire)
    {
        float drawFireTime = m_CurrentWeaponDescriptor.drawTime;
        if (m_CurrentWeaponDescriptor.fChargeTime > 0)
            drawFireTime += m_CurrentWeaponDescriptor.fChargeTime;

        float minDist = 5.0f;

        if (Point_PointSq(aimTarget, GetFirePos()) > sqr(minDist))
        {
            if (m_burstEffectTime < drawFireTime)
            {
                Vec3 shooterGroundPos = GetPhysicsPos();
                Vec3 targetGroundPos;
                Vec3 targetPos = aimTarget;

                if (pTarget.GetProxy() != null)
                {
                    targetGroundPos = pTarget.GetPhysicsPos();
                    targetGroundPos = new Vec3(targetGroundPos.x, targetGroundPos.y, targetGroundPos.z - 0.25f);
                }
                else
                {
                    targetGroundPos = targetPos;
                    targetGroundPos = new Vec3(targetGroundPos.x, targetGroundPos.y, targetGroundPos.z - 1.5f);
                }

                float floorHeight = min(targetGroundPos.z, shooterGroundPos.z);

                Vec3 dirTargetToShooter = shooterGroundPos - targetGroundPos;
                dirTargetToShooter = new Vec3(dirTargetToShooter.x, dirTargetToShooter.y, 0.0f);
                float dist = dirTargetToShooter.NormalizeSafe();

                Vec3 firePos = GetFirePos();

                float endHeight = targetGroundPos.z;
                float startHeight = floorHeight - (firePos.z - floorHeight);

                float t;
                if (m_CurrentWeaponDescriptor.fChargeTime > 0)
                    t = clamp_tpl((m_burstEffectTime - m_CurrentWeaponDescriptor.fChargeTime) / (drawFireTime - m_CurrentWeaponDescriptor.fChargeTime), 0.0f, 1.0f);
                else
                    t = clamp_tpl(m_burstEffectTime / drawFireTime, 0.0f, 1.0f);

                CPNoise3 pNoise = gEnv.pSystem?.GetNoiseGen();
                float noiseScale = clamp_tpl(m_burstEffectTime - 0.5f, 0.0f, 1.0f);
                float noise = noiseScale * (pNoise != null ? pNoise.Noise1D(m_spreadFireTime + m_burstEffectTime * m_CurrentWeaponDescriptor.sweepFrequency) : 0.0f);
                Vec3 right = new Vec3(dirTargetToShooter.y, -dirTargetToShooter.x, 0);

                aimTarget = targetGroundPos + right * (noise * m_CurrentWeaponDescriptor.sweepWidth);
                aimTarget = new Vec3(aimTarget.x, aimTarget.y, startHeight + (endHeight - startHeight) * (1 - sqr(1 - t)));

                // Clamp to bottom plane.
                if (aimTarget.z < floorHeight && fabsf(aimTarget.z - firePos.z) > 0.01f)
                {
                    float u = (floorHeight - firePos.z) / (aimTarget.z - firePos.z);
                    aimTarget = firePos + (aimTarget - firePos) * u;
                }
            }
            else if (m_targetLostTime > m_CurrentWeaponDescriptor.drawTime)
            {
                float amount = clamp_tpl((m_targetLostTime - m_CurrentWeaponDescriptor.drawTime) / m_CurrentWeaponDescriptor.drawTime, 0.0f, 1.0f);

                Vec3 forw = GetEntityDir();
                Vec3 right = new Vec3(forw.y, -forw.x, 0);
                Vec3 up = new Vec3(0, 0, 1);
                right = right.GetNormalizedSafe();
                float distToTarget = Point_Point(aimTarget, GetPos());

                float tTime = m_spreadFireTime + m_burstEffectTime * m_CurrentWeaponDescriptor.sweepFrequency;
                float mag = amount * m_CurrentWeaponDescriptor.sweepWidth / 2;

                CPNoise3 pNoise = gEnv.pSystem?.GetNoiseGen();

                float ox = sinf(tTime * 1.5f) * mag + (pNoise != null ? pNoise.Noise1D(tTime) * mag : 0.0f);
                float oy = (pNoise != null ? pNoise.Noise1D(tTime + 33.0f) / 2 * mag : 0.0f);

                aimTarget += right * ox + up * oy;
            }

            if (m_burstEffectTime < 0.2f)
            {
                if (!m_State.vAimTargetPos.IsZero())
                {
                    Vec3 pos = GetPos();

                    if (m_State.vAimTargetPos.z > (aimTarget.z + 0.25f))
                    {
                        canFire = false;
                    }
                    else
                    {
                        float distToCurSq = Point_Point2DSq(pos, m_State.vAimTargetPos);
                        float thr = sqr(Point_Point2D(pos, aimTarget) + 0.5f);
                        if (distToCurSq > thr)
                            canFire = false;
                    }
                }
            }
            if (canFire)
                m_burstEffectTime += m_fTimePassed;
        }
    }
    public void HandleWeaponEffectBurstSnipe(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire)
    {
        CCCPOINT("CPuppet_HandleWeaponEffectBurstSnipe");

        float drawFireTime = m_CurrentWeaponDescriptor.drawTime;
        if (m_CurrentWeaponDescriptor.fChargeTime > 0)
            drawFireTime += m_CurrentWeaponDescriptor.fChargeTime;

        float headHeight = aimTarget.z;
        CAIActor pLiveTarget = GetLiveTarget(GetWeakRef(pTarget)).GetAIObject();
        if (pLiveTarget != null && pLiveTarget.GetProxy() != null)
        {
            IPhysicalEntity pPhys = pLiveTarget.GetProxy().GetPhysics(true);
            if (pPhys == null)
                pPhys = pLiveTarget.GetPhysics();
            if (pPhys != null)
            {
                pe_status_pos statusPos = new pe_status_pos();
                pPhys.GetStatus(statusPos);
                float minz = statusPos.BBox[0].z + statusPos.pos.z;
                float maxz = statusPos.BBox[1].z + statusPos.pos.z + 0.25f;

                if (headHeight >= minz && headHeight <= maxz)
                    headHeight = maxz;
            }
        }

        Vec3 firePos = GetFirePos();
        Vec3 dirTargetToShooter = aimTarget - firePos;
        dirTargetToShooter = new Vec3(dirTargetToShooter.x, dirTargetToShooter.y, 0.0f);
        float dist = dirTargetToShooter.NormalizeSafe();
        Vec3 right = new Vec3(dirTargetToShooter.y, -dirTargetToShooter.x, 0);
        Vec3 up = new Vec3(0, 0, 1);
        float noiseScale = 1.0f;

        if (m_burstEffectState == 0)
        {
            if (canFire && m_aimState != EAimState.AI_AIM_OBSTRUCTED)
                m_burstEffectTime += m_fTimePassed;

            if (m_burstEffectTime < drawFireTime)
            {
                float endHeight = aimTarget.z;
                float startHeight = aimTarget.z - 0.5f;

                float t;
                if (m_CurrentWeaponDescriptor.fChargeTime > 0)
                    t = clamp_tpl((m_burstEffectTime - m_CurrentWeaponDescriptor.fChargeTime) / (drawFireTime - m_CurrentWeaponDescriptor.fChargeTime), 0.0f, 1.0f);
                else
                    t = clamp_tpl(m_burstEffectTime / drawFireTime, 0.0f, 1.0f);

                noiseScale = t;

                aimTarget = new Vec3(aimTarget.x, aimTarget.y, startHeight + (endHeight - startHeight) * t);
            }
            else
            {
                m_burstEffectState = 1;
                m_burstEffectTime = -1;
            }
        }
        else if (m_burstEffectState == 1)
        {
            if (m_targetLostTime > m_CurrentWeaponDescriptor.drawTime)
            {
                if (m_burstEffectTime < 0)
                    m_burstEffectTime = 0;

                if (canFire && m_aimState != EAimState.AI_AIM_OBSTRUCTED)
                    m_burstEffectTime += m_fTimePassed;

                float amount = clamp_tpl((m_targetLostTime - m_CurrentWeaponDescriptor.drawTime) / m_CurrentWeaponDescriptor.drawTime, 0.0f, 1.0f);

                Vec3 forw = GetEntityDir();
                Vec3 rightVector = new Vec3(forw.y, -forw.x, 0);
                Vec3 upVector = new Vec3(0, 0, 1);
                rightVector = rightVector.GetNormalizedSafe();
                float distToTarget = Point_Point(aimTarget, GetFirePos());

                float mag = amount * m_CurrentWeaponDescriptor.sweepWidth / 2 * clamp_tpl((distToTarget - 1.0f) / 5.0f, 0.0f, 1.0f);

                float tsweep = m_burstEffectTime * m_CurrentWeaponDescriptor.sweepFrequency;

                float ox = sinf(tsweep) * mag;
                float oy = 0;

                aimTarget = new Vec3(aimTarget.x, aimTarget.y, aimTarget.z + (headHeight - aimTarget.z) * clamp_tpl(m_burstEffectTime, 0.0f, 1.0f));
                aimTarget += rightVector * ox + upVector * oy;
            }
        }

        float noiseTime = m_spreadFireTime;
        m_spreadFireTime += m_fTimePassed;

        noiseTime *= m_CurrentWeaponDescriptor.sweepFrequency * 2;
        CPNoise3 pNoise = gEnv.pSystem?.GetNoiseGen();
        float nx = (pNoise != null ? pNoise.Noise1D(noiseTime) : 0.0f) * noiseScale * 0.1f;
        float ny = (pNoise != null ? pNoise.Noise1D(noiseTime + 33.0f) : 0.0f) * noiseScale * 0.1f;
        aimTarget += right * nx + up * ny;
    }
    public void HandleWeaponEffectPanicSpread(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire)
    {
        if (m_aimState == EAimState.AI_AIM_READY)
        {
            m_burstEffectTime += GetAISystem().GetFrameDeltaTime();
            m_spreadFireTime += GetAISystem().GetFrameDeltaTime();
        }

        float t = m_spreadFireTime;

        Vec3 forw = GetEntityDir();
        Vec3 right = new Vec3(forw.y, -forw.x, 0);
        Vec3 up = new Vec3(0, 0, 1);
        right = right.GetNormalizedSafe();
        float distToTarget = Point_Point(aimTarget, GetPos());

        float speed = 1.7f;
        float spread = DEG2RAD(40.0f);

        t *= speed;
        float mag = distToTarget * tanf(spread / 2);
        mag *= 0.25f + min(m_burstEffectTime / 0.5f, 1.0f) * 0.75f;

        CPNoise3 pNoise = gEnv.pSystem?.GetNoiseGen();

        float ox = sinf(t * 1.7f) * mag + (pNoise != null ? pNoise.Noise1D(t) * mag : 0.0f);
        float oy = (pNoise != null ? pNoise.Noise1D(t * 0.98432f + 33.0f) * mag : 0.0f);

        aimTarget += right * ox + up * oy;
    }
    public void HandleWeaponEffectAimSweep(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire)
    {
        float drawFireTime = m_CurrentWeaponDescriptor.drawTime;
        if (m_CurrentWeaponDescriptor.fChargeTime > 0)
            drawFireTime += m_CurrentWeaponDescriptor.fChargeTime;

        float headHeight = aimTarget.z;
        CAIActor pLiveTarget = GetLiveTarget(GetWeakRef(pTarget)).GetAIObject();
        if (pLiveTarget != null && pLiveTarget.GetProxy() != null)
        {
            IPhysicalEntity pPhys = pLiveTarget.GetProxy().GetPhysics(true);
            if (pPhys == null)
                pPhys = pLiveTarget.GetPhysics();
            if (pPhys != null)
            {
                pe_status_pos statusPos = new pe_status_pos();
                pPhys.GetStatus(statusPos);
                float minz = statusPos.BBox[0].z + statusPos.pos.z;
                float maxz = statusPos.BBox[1].z + statusPos.pos.z + 0.25f;

                if (headHeight >= minz && headHeight <= maxz)
                    headHeight = maxz;
            }
        }

        if (m_burstEffectTime < 0)
            m_burstEffectTime = 0;

        if (m_aimState != EAimState.AI_AIM_OBSTRUCTED)
            m_burstEffectTime += m_fTimePassed;

        Vec3 forw = GetEntityDir();
        Vec3 right = new Vec3(forw.y, -forw.x, 0);
        Vec3 up = new Vec3(0, 0, 1);
        right = right.GetNormalizedSafe();

        float mag = m_CurrentWeaponDescriptor.sweepWidth / 2;

        CPNoise3 pNoise = gEnv.pSystem?.GetNoiseGen();

        float distToTarget = Point_Point(aimTarget, GetFirePos());
        float dscale = clamp_tpl((distToTarget - 1.0f) / 5.0f, 0.0f, 1.0f);

        float tsweep = m_burstEffectTime * m_CurrentWeaponDescriptor.sweepFrequency;

        float ox = sinf(tsweep) * mag * dscale;
        float oy = 0;

        aimTarget = new Vec3(aimTarget.x, aimTarget.y, aimTarget.z + (headHeight - aimTarget.z) * clamp_tpl(m_burstEffectTime / 2, 0.0f, 1.0f));
        aimTarget += right * ox + up * oy;

        float noiseTime = m_spreadFireTime;
        m_spreadFireTime += m_fTimePassed;

        float noiseScale = clamp_tpl(m_burstEffectTime, 0.0f, 1.0f) * dscale;

        noiseTime *= m_CurrentWeaponDescriptor.sweepFrequency * 2;
        float nx = (pNoise != null ? pNoise.Noise1D(noiseTime) : 0.0f) * noiseScale * 0.1f;
        float ny = (pNoise != null ? pNoise.Noise1D(noiseTime + 33.0f) : 0.0f) * noiseScale * 0.1f;
        aimTarget += right * nx + up * ny;
    }

    public struct STargetSilhouette
    {
        public STargetSilhouette(int dummy = 0)
        {
            valid = false;
            points = new List<Vec3>();
            baseMtx = new Matrix33(); // SetIdentity in .cpp
            center = new Vec3(0, 0, 0);
        }

        public bool valid;
        public List<Vec3> points;
        public Matrix33 baseMtx;
        public Vec3 center;

        public Vec3 ProjectVectorOnSilhouette(Vec3 vec)
        {
            return new Vec3(baseMtx.GetColumn0().Dot(vec), baseMtx.GetColumn2().Dot(vec), 0.0f);
        }

        public Vec3 IntersectSilhouettePlane(Vec3 from, Vec3 to)
        {
            if (!valid)
                return to;

            // Intersect (infinite) segment with the silhouette plane.
            Vec3 pn = baseMtx.GetColumn1();
            float pd = -pn.Dot(center);
            Vec3 dir = to - from;

            float d = pn.Dot(dir);
            if (CryMath.fabsf(d) < 1e-6f)
                return to;

            float n = pn.Dot(from) + pd;
            return from + dir * (-n / d);
        }

        public void Reset() { valid = false; points.Clear(); center = new Vec3(0, 0, 0); }
    }

    public STargetSilhouette m_targetSilhouette = new STargetSilhouette();
    public Vec3 m_targetLastMissPoint;
    public Vec3 m_targetPosOnSilhouettePlane;
    public Vec3 m_targetBiasDirection;
    public float m_targetFocus;
    public EAITargetZone m_targetZone;
    public float m_targetDistanceToSilhouette;
    public float m_targetEscapeLastMiss;
    public float m_targetSeenTime;
    public float m_targetLostTime;
    public float m_targetDazzlingTime;
    public float m_burstEffectTime;
    public int m_burstEffectState;

    public nuint m_lastMissShotsCount;
    public nuint m_lastHitShotsCount;
    public nuint m_lastTargetPart;
    public float m_targetDamageHealthThr;

    public bool m_lastAimObstructionResult;
    public EPuppetUpdatePriority m_updatePriority;

    public PostureManager m_postureManager = new PostureManager();

    public CTimeValue m_lastTimeUpdatedBestTarget;

    public List<CAIObject> m_steeringObjects = new List<CAIObject>();
    public CTimeValue m_lastSteerTime;

    public CAIRadialOccypancy m_steeringOccupancy = new CAIRadialOccypancy();
    public float m_steeringOccupancyBias;
    public bool m_steeringEnabled;
    public float m_steeringAdjustTime;
    public float m_fLastNavTest;

    public CValueHistory<float> m_targetDamageHealthThrHistory;

    public CWeakRef<CAIVehicle> GetAvoidedVehicle() { return m_refAvoidedVehicle; }
    public long GetVehicleAvoidingTime()
    {
        return 0; // (GetAISystem().GetFrameStartTime() - m_vehicleAvoidingTime).GetMilliSecondsAsInt64();
    }

    public virtual void EnableFire(bool enable)
    {
        if (enable)
        {
            if (m_fireDisabled > 0)
                --m_fireDisabled;
        }
        else
        {
            ++m_fireDisabled;
        }
    }
    public virtual bool IsFireEnabled() { return m_fireDisabled == 0; }

    public void EnableCoverFire(bool enable) { m_bCoverFireEnabled = enable; }
    public bool IsCoverFireEnabled() { return m_bCoverFireEnabled; }

    public void SetAvoidedVehicle(CWeakRef<CAIVehicle> refVehicle)
    {
        if (!refVehicle.IsValid())
        {
            CCCPOINT("CPuppet_SetAvoidedVehicle_Reset");
            m_refAvoidedVehicle.Reset();
            m_vehicleAvoidingTime = new CTimeValue(0.0f);
        }
        else
        {
            CCCPOINT("CPuppet_SetAvoidedVehicle_Set");
            m_refAvoidedVehicle = refVehicle;
            m_vehicleAvoidingTime = GetAISystem().GetFrameStartTime();
        }
    }

    public void UpdateStrafing()
    {
        m_State.allowStrafing = false;
        if (m_State.fDistanceToPathEnd > 0)
        {
            if (m_allowedStrafeDistanceStart > 0.001f)
            {
                // Calculate the max travelled distance.
                float distanceMoved = m_OrigPath.GetPathLength(false) - m_State.fDistanceToPathEnd;
                m_strafeStartDistance = max(m_strafeStartDistance, distanceMoved);

                if (m_strafeStartDistance < m_allowedStrafeDistanceStart)
                    m_State.allowStrafing = true;
            }

            if (m_allowedStrafeDistanceEnd > 0.001f)
            {
                if (m_State.fDistanceToPathEnd < m_allowedStrafeDistanceEnd)
                    m_State.allowStrafing = true;
            }
        }
    }


    public class SAIFireTargetCache
    {
        public SAIFireTargetCache() { m_size = 0; m_head = 0; m_queries = 0; m_hits = 0; m_cachePos = new Vec3[CACHE_SIZE]; m_cacheDir = new Vec3[CACHE_SIZE]; m_cacheReqDist = new float[CACHE_SIZE]; m_cacheDist = new float[CACHE_SIZE]; }

        public float QueryCachedResult(Vec3 pos, Vec3 dir, float reqDist)
        {
            m_queries++;
            float posThr = 0.5f;
            float dirThr = 0.95f;
            float distThr = 0.5f;
            for (int i = 0; i < m_size; i++)
            {
                if (Point_PointSq(m_cachePos[i], pos) < sqr(posThr) &&
                    m_cacheDir[i].Dot(dir) > dirThr &&
                    fabsf(m_cacheReqDist[i] - reqDist) < distThr)
                {
                    m_hits++;
                    return m_cacheDist[i];
                }
            }
            return -1.0f;
        }

        public void Insert(Vec3 pos, Vec3 dir, float reqDist, float dist)
        {
            m_cachePos[m_head] = pos;
            m_cacheDir[m_head] = dir;
            m_cacheDist[m_head] = dist;
            m_cacheReqDist[m_head] = reqDist;
            if (m_size < CACHE_SIZE)
                m_size++;
            m_head++;
            if (m_head >= CACHE_SIZE)
                m_head = 0;
        }

        public int GetQueries() { return m_queries; }
        public int GetHits() { return m_hits; }

        public void Reset() { m_size = 0; m_head = 0; m_hits = 0; m_queries = 0; }

        private const int CACHE_SIZE = 8;
        private Vec3[] m_cachePos;
        private Vec3[] m_cacheDir;
        private float[] m_cacheReqDist;
        private float[] m_cacheDist;
        private int m_size, m_head;
        private int m_hits;
        private int m_queries;
    }

    public void SetAlarmed()
    {
        AgentPerceptionParameters perceptionParameters = m_Parameters.m_PerceptionParams;
        m_alarmedTime = perceptionParameters.forgetfulnessTarget + perceptionParameters.forgetfulnessMemory;

        // reset perceptionScale to 1.0f when alerted
        perceptionParameters.perceptionScale = new AgentPerceptionParameters.PerceptionScale { visual = 1.0f, audio = 1.0f };
    }
    public virtual bool IsAlarmed() { return m_alarmedTime > 0.01f; }
    public virtual float GetPerceptionAlarmLevel() { return System.Math.Max(GetParameters().m_PerceptionParams.minAlarmLevel, m_alarmedLevel); }

    public float m_alarmedTime;
    public float m_alarmedLevel;
    public new bool m_damagePartsUpdated;

    public uint8 m_fireDisabled;

    // Perception descriptors
    public List<SSoundPerceptionDescriptor> m_SoundPerceptionDescriptor = new List<SSoundPerceptionDescriptor>();
    public static SSoundPerceptionDescriptor[] s_DefaultSoundPerceptionDescriptor = new SSoundPerceptionDescriptor[(int)EAISoundStimType.AISOUND_LAST];

    public static List<Vec3> s_projectedPoints = new List<Vec3>();
    public static List<(float, nuint)> s_weights = new List<(float, nuint)>();
    public static List<CAIActor> s_enemies = new List<CAIActor>();
    public static List<SSortedHideSpot> s_sortedHideSpots = new List<SSortedHideSpot>();
    public static MultimapRangeHideSpots s_hidespots = new MultimapRangeHideSpots();
    public static MapConstNodesDistance s_traversedNodes = new MapConstNodesDistance();
}

// Forward decls / shells for related types
public interface IPuppet { }
public interface IFireCommandHandler
{
    string GetName();
    void Reset();
    void Release();
    int GetShotCount();
    bool ValidateFireDirection(Vec3 dir, bool checkDamageParts);
    // Added for Puppet.cpp literal port
    EAIFireState Update(IAIObject owner, bool canFire, EAIFireState currentState, SOBJECTSTATE objectState, SFireCommandProjectileInfo info) { return EAIFireState.eAIFS_Off; }
    bool GetOverrideAimingPosition(out Vec3 pos) { pos = new Vec3(0, 0, 0); return false; }
    bool GetProjectileInfo(SFireCommandProjectileInfo info) { return false; }
    void OnReload() { }
    bool UseDefaultEffectFor(int shotNum) { return true; }
}
public partial class CFireCommandGrenade
{
    public void Reset() { }
    public EAIFireState Update(IAIObject owner, bool canFire, EAIFireState currentState, SOBJECTSTATE objectState, SFireCommandProjectileInfo info) { return EAIFireState.eAIFS_Off; }
    public bool GetProjectileInfo(SFireCommandProjectileInfo info) { return false; }
}
public struct SFireCommandProjectileInfo
{
    public Vec3 vShootPos;
    public Vec3 vShootDir;
    public float fSpeed;
    public Vec3 trackingId;
    public ERequestedGrenadeType grenadeType;
}
public interface IPerceptionHandler
{
    bool GetPotentialTargets(PotentialTargetMap targetMap);
    void AddAggressiveTarget(IAIObject pTarget);
    void AddEvent(uint id, SAIPotentialTarget target);
    void ClearPotentialTargets();
    void ClearTempTarget();
    void DropTarget(IAIObject pTarget);
    IAIObject GetEventOwner(uint id);
    void HandleBulletRain(SAIEVENT evt);
    void HandleSoundEvent(SAIEVENT evt);
    void HandleVisualStimulus(SAIEVENT evt);
    void PostSerialize();
    void RegisterTargetAwareness(IAIObject pTarget, float awareness);
    void RemoveEvent(uint id);
    void SetTempTargetPriority(ETempTargetPriority priority);
    void UpTargetPriority(IAIObject pTarget, float increment);
    void UpdateTempTarget(Vec3 pos);
}
public interface IAIRateOfDeathHandler
{
    float GetTargetAliveTime(CPuppet pPuppet, CAIObject pTarget, EAITargetZone targetZone, float targetDazzlingTime);
    float GetFiringReactionTime(CPuppet pPuppet, CAIObject pTarget, Vec3 targetPos);
    EAITargetZone GetTargetZone(CPuppet pPuppet, CAIObject pTarget);
}
public class SAIPotentialTarget
{
    public int type;
    public float threat;
    public float exposureThreat;
    public bool bNeedsUpdating;
}
public class PotentialTargetMap
{
    private System.Collections.Generic.Dictionary<uint, SAIPotentialTarget> _map = new();
    public void Insert(uint id, SAIPotentialTarget target) { _map[id] = target; }
    public System.Collections.Generic.Dictionary<uint, SAIPotentialTarget>.Enumerator GetEnumerator() => _map.GetEnumerator();
}
public class PendingDeathReaction { }
public class CPersonalInterestManager
{
    public bool IsInterested() { return false; }
    public Vec3 GetInterestDummyPoint() { return new Vec3(0, 0, 0); }
    public int GetLookingStyle() { return 0; }
}
// SHideSpot is a real literal port in HideSpot.cs.
public class AIWeaponDescriptor
{
    public AgentPerceptionParameters perceptionParams = new AgentPerceptionParameters();
    public float coverFireTime = 3.0f;
    public float fSpeed = 0.0f; // bullet speed, 0 means hitscan
    public string firecmdHandler = "";
    public bool bSignalOnShoot = false;
    public float fDamageRadius = 0.0f;
    public float fChargeTime = 0.0f;
    public float burstBulletCountMin = 1;
    public float burstBulletCountMax = 5;
    public float burstPauseTimeMin = 0.5f;
    public float burstPauseTimeMax = 2.0f;
    public float singleFireTriggerTime = -1.0f;
    public float spreadRadius = 0.0f;
    public float sweepWidth = 0.0f;
    public float sweepFrequency = 1.0f;
    public float pressureMultiplier = 1.0f;
    public float lobCriticalDistance = 5.0f;
    public float lobDamageRadius = 5.0f;
    public float projectileSpeed = 0.0f;
    public float closeDistance = 0.0f;
    public float missExtraOffset = 0.3f;
    public float missExtraOffsetClose = 0.3f;
    public float hitExtraOffset = 0.0f;
    public float hitExtraOffsetClose = 0.0f;
    // Added for Puppet.cpp literal port
    public float drawTime = 0.0f;
    public float fRangeMax = 100.0f;
    public float fRangeMin = 0.0f;
}
// AgentPerceptionParameters now lives in AIActor.cs as a class with full fields
public partial class CAIRadialOccypancy
{
    public void Reset() { }
    public void AddObstructionCircle(Vec3 pos, float radius) { }
    public bool GetNearestUnoccupiedDirection(Vec3 dir, float angle, ref Vec3 result) { result = dir; return true; }
}
// MapConstNodesDistance defined in Graph.cs
public struct Ray { public Vec3 origin, direction; }
public enum EAISoundStimType { AISOUND_GENERIC, AISOUND_COLLISION, AISOUND_COLLISION_LOUD, AISOUND_MOVEMENT, AISOUND_MOVEMENT_LOUD, AISOUND_WEAPON, AISOUND_EXPLOSION, AISOUND_LAST }
// EAITargetZone / EMemoryFireType / ERequestedGrenadeType literal ports live in CryCommon/IAgent_Enums.cs
public enum ETempTargetPriority { eTTP_OverCurrent, eTTP_OverPlayer, eTTP_OverAll }
public enum EPuppetUpdatePriority { AIPUP_NORMAL, AIPUP_VERY_HIGH, AIPUP_HIGH, AIPUP_MED, AIPUP_LOW }

// Make AgentParameters mention forgetfulnessTarget/Memory accessors via PerceptionParams
public partial class AgentParameters
{
    // already has m_PerceptionParams, m_sWaveName from AIActor.cs
}

// PuppetConstants — constants used by Puppet.cpp as bare names (via using static)
public static class PuppetConstants
{
    // AG input names
    public const string AIAG_ACTION = "Action";
    public const string AIAG_SIGNAL = "Signal";

    // Collision entity flags from IPhysics
    public const int AICE_ALL = 0x7FFFFFFF;
    public const int AICE_DYNAMIC = 0x04;
    public const int ent_living = 0x20;

    // AI register IDs
    public const int AI_REG_ATTENTIONTARGET = 0;
    public const int AI_REG_LASTOP = 1;
    public const int AI_REG_REFPOINT = 2;

    // Nav types — CAISystem.h
    public const uint IAISystem_NAV_TRIANGULAR = 0x0001;
    public const uint IAISystem_NAV_ROAD = 0x0020;

    // Invalid AI object id
    public const uint INVALID_AIOBJECTID = 0;

    // Alertness counter count
    public const int NUM_ALERTNESS_COUNTERS = 4;

    // FOV result
    public const int eFOV_Outside = 0;

    // VisionMap change hints
    public const uint eChangedTypeMask = 0x01;
    public const uint eChangedUserData = 0x02;

    // EPathDecision — from PathMarker.h
    public static class EPathDecision
    {
        public const int PATHFINDER_PATHFOUND = 1;
        public const int PATHFINDER_NOPATH = 0;
        public const int PATHFINDER_STILLFINDING = 2;
        public const int PATHFINDER_ABORT = 3;
    }

    // VisionMapTypes flags
    [System.Flags]
    public enum VisionMapTypes : uint
    {
        General = 0x01,
        AliveAgent = 0x02,
    }

    // FORWARD_DIRECTION — Vec3 Y-forward for CryEngine
    public static readonly Vec3 FORWARD_DIRECTION = new Vec3(0, 1, 0);

    // Movement urgency helpers
    public static int MovementUrgencyToIndex(float urgency)
    {
        if (urgency < 0.5f * (CryAISystem.AISignalConstants.AISPEED_SLOW + CryAISystem.AISignalConstants.AISPEED_WALK))
            return 0;
        else if (urgency < 0.5f * (CryAISystem.AISignalConstants.AISPEED_WALK + CryAISystem.AISignalConstants.AISPEED_RUN))
            return 1;
        else if (urgency < 0.5f * (CryAISystem.AISignalConstants.AISPEED_RUN + CryAISystem.AISignalConstants.AISPEED_SPRINT))
            return 2;
        return 3;
    }

    public static float IndexToMovementUrgency(int idx)
    {
        return idx switch
        {
            0 => CryAISystem.AISignalConstants.AISPEED_SLOW,
            1 => CryAISystem.AISignalConstants.AISPEED_WALK,
            2 => CryAISystem.AISignalConstants.AISPEED_RUN,
            3 => CryAISystem.AISignalConstants.AISPEED_SPRINT,
            _ => CryAISystem.AISignalConstants.AISPEED_RUN,
        };
    }

    // SmoothCD — CryCommon smooth critical damping
    public static void SmoothCD(ref float val, ref float valRate, float timeDelta, float to, float smoothTime)
    {
        if (smoothTime > 0.0f)
        {
            float omega = 2.0f / smoothTime;
            float x = omega * timeDelta;
            float exp = 1.0f / (1.0f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = val - to;
            float temp = (valRate + omega * change) * timeDelta;
            valRate = (valRate - omega * temp) * exp;
            val = to + (change + temp) * exp;
        }
        else if (timeDelta > 0.0f)
        {
            valRate = (to - val) / timeDelta;
            val = to;
        }
        else
        {
            val = to;
            valRate = 0;
        }
    }

    // SmoothCD for Vec3
    public static void SmoothCD(ref Vec3 val, ref Vec3 valRate, float timeDelta, Vec3 to, float smoothTime)
    {
        float vx = val.x, vy = val.y, vz = val.z;
        float rx = valRate.x, ry = valRate.y, rz = valRate.z;
        SmoothCD(ref vx, ref rx, timeDelta, to.x, smoothTime);
        SmoothCD(ref vy, ref ry, timeDelta, to.y, smoothTime);
        SmoothCD(ref vz, ref rz, timeDelta, to.z, smoothTime);
        val = new Vec3(vx, vy, vz);
        valRate = new Vec3(rx, ry, rz);
    }

    // CheckBodyPos — stub to check if a position is valid for a body
    public static bool CheckBodyPos(Vec3 pos, int flags) { return true; /* impl pending */ }

    // OverlapCylinder — stub
    public static bool OverlapCylinder(object lseg, float radius, int flags, IPhysicalEntity skip, int param, int[] entities, int maxEntities) { return false; /* impl pending */ }

    // SerializeWeakRefMap — stub serialization helper
    public static void SerializeWeakRefMap<TKey, TValue>(TSerialize ser, string name, System.Collections.Generic.Dictionary<TKey, TValue> map)
    { /* impl pending Phase 11 */ }

    // GetWeakRefSafe — get a weak ref from an AI object
    public static CWeakRef<CAIObject> GetWeakRefSafe(CAIObject obj)
    {
        if (obj != null)
            return WeakRefHelpers.GetWeakRef(obj);
        return new CWeakRef<CAIObject>();
    }

    // Math helpers
    public static float acos_tpl(float x) { return System.MathF.Acos(CryMath.clamp_tpl(x, -1.0f, 1.0f)); }
    public static float atanf(float x) { return System.MathF.Atan(x); }
}

// CPNoise3 — Perlin noise generator shell
public class CPNoise3
{
    public float Noise1D(float x) { return 0.0f; }
}

// Add the silhouette helpers — Matrix33 ports come from CryPhysics.Sharp via aliases
