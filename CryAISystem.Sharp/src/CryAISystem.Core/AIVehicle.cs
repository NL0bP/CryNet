// Literal port of dev/Code/CryEngine/CryAISystem/AIVehicle.{h,cpp} (1225 lines C++)
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;
using static CryAISystem.CryMath;
using static CryAISystem.CryRandom;
using static CryAISystem.AISignalConstants;
using static CryAISystem.AIPhysConstants;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.NilRefHelper;
using static CryAISystem.EAIEvent;
using static CryAISystem.EAIObjectType;
using CryAISystem.CryCommon;

namespace CryAISystem;

public class CAIVehicle : CPuppet
{
    // ===================================================================
    // Literal port of CAIVehicle::CAIVehicle() from AIVehicle.cpp lines 30-45.
    // ===================================================================
    public CAIVehicle()
    {
        m_bDriverInside = false;
        m_driverInsideCheck = -1;
        m_playerInsideCheck = -1;
        m_fNextFiringTime = GetAISystem() != null ? GetAISystem().GetFrameStartTime() : new CTimeValue();
        m_fFiringStartTime = GetAISystem() != null ? GetAISystem().GetFrameStartTime() : new CTimeValue();
        m_fFiringPauseTime = GetAISystem() != null ? GetAISystem().GetFrameStartTime() : new CTimeValue();
        m_bPoweredUp = false;
        m_ShootPhase = 0;
        m_vDeltaTarget = new Vec3(0, 0, 0);

        _fastcast_CAIVehicle = true;
        // can't reset now - no parameters are initialized yet
        //	Reset();
    }

    // ~CAIVehicle() — empty per literal C++ (AIVehicle.cpp lines 49-52)
    ~CAIVehicle() { }

    // ===================================================================
    // Literal port of CAIVehicle::UpdateDisabled (AIVehicle.cpp lines 56-63)
    // ===================================================================
    public new void UpdateDisabled(EObjectUpdate type)
    {
        base.UpdateDisabled(type);
        m_bDryUpdate = type == EObjectUpdate.AIUPDATE_DRY;
        AlertPuppets();
        m_driverInsideCheck = -1;
        m_playerInsideCheck = -1;
    }

    // ===================================================================
    // Literal port of CAIVehicle::Update (AIVehicle.cpp lines 67-147)
    // ===================================================================
    public new void Update(EObjectUpdate type)
    {
        // FUNCTION_PROFILER( gEnv->pSystem, PROFILE_AI );
        CCCPOINT(nameof(CAIVehicle) + "_Update");

        m_driverInsideCheck = -1;
        m_playerInsideCheck = -1;

        SAIBodyInfo bodyInfo = QueryBodyInfo();

        UpdateBehaviorSelectionTree();

        m_bDryUpdate = type == EObjectUpdate.AIUPDATE_DRY;

        // make sure to update direction when entity is not moved
        SetPos(bodyInfo.vEyePos);
        SetBodyDir(bodyInfo.GetBodyDir());
        SetEntityDir(bodyInfo.vEntityDir);
        SetMoveDir(bodyInfo.vMoveDir);

        // static bool doDryUpdateCall = true;  — C++ static local; always true in practice
        bool doDryUpdateCall = true;
        AlertPuppets();

        UpdateHealthTracking();
        m_damagePartsUpdated = false;

        if (!m_bDryUpdate)
        {
            // FRAME_PROFILER("AI system vehicle full update", gEnv->pSystem, PROFILE_AI);

            CTimeValue fCurrentTime = GetAISystem().GetFrameStartTime();
            if (m_fLastUpdateTime.GetSeconds() > 0.0f)
                m_fTimePassed = min(0.5f, (fCurrentTime - m_fLastUpdateTime).GetSeconds());
            else
                m_fTimePassed = 0;

            m_fLastUpdateTime = fCurrentTime;

            m_State.Reset();

            GetStateFromActiveGoals(ref m_State);

            UpdatePuppetInternalState();
        }
        else if (doDryUpdateCall)
        {
            // if approaching then always update
            for (int i = 0; i < m_vActiveGoals.Count; i++)
            {
                QGoal Goal = m_vActiveGoals[i];
                Goal.pGoalOp.ExecuteDry(this);
            }
        }

        //--------------------------------------------------------
        // Orient towards the attention target always
        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();

        Navigate(pAttentionTarget);

        // ---------- update proxy object
        if (pAttentionTarget != null)
        {
            m_State.nTargetType = pAttentionTarget.GetType();
            m_State.bTargetEnabled = pAttentionTarget.IsEnabled();
        }
        else
        {
            m_State.nTargetType = -1;
            m_State.bTargetEnabled = false;
        }

        FireCommand();

        m_vLastMoveDir = m_State.vMoveDir;
        if (gAIEnv.CVars.UpdateProxy != 0)
        {
            GetProxy().Update(m_State, !m_bDryUpdate);
            UpdateAlertness();
        }
    }

    // ===================================================================
    // Literal port of CAIVehicle::Navigate (AIVehicle.cpp lines 808-868)
    // ===================================================================
    public void Navigate(CAIObject pTarget)
    {
        CCCPOINT(nameof(CAIVehicle) + "_Navigate");

        m_State.vForcedNavigation = m_vForcedNavigation;
        m_State.fForcedNavigationSpeed = m_fForcedNavigationSpeed;

        if (m_bLooseAttention)
        {
            CAIObject pLooseAttentionTarget = m_refLooseAttentionTarget.GetAIObject();
            if (pLooseAttentionTarget != null)
                pTarget = pLooseAttentionTarget;
            else
                pTarget = null;
        }

        Vec3 vDir, vTargetPos;
        float fTime = GetAISystem().GetFrameDeltaTime();

        if (pTarget != null)
        {
            SAIBodyInfo bodyInfo = GetBodyInfo();

            vTargetPos = pTarget.GetPos();
            vDir = vTargetPos - bodyInfo.vFirePos;

            if (GetSubType() == ESubType.STP_HELI)
            {
                // (MATT) I'm very dubious about this code {2009/02/04}
                if (m_refLooseAttentionTarget.IsValid())
                    m_State.vLookTargetPos = vTargetPos;
                else
                    m_State.vLookTargetPos = new Vec3(0, 0, 0);
            }
            else if (vDir.GetLength() > 5.0f && !AllowedToFire())
            {
                Vec3 check1 = m_State.vLookTargetPos - bodyInfo.vFirePos;
                Vec3 check2 = vTargetPos - bodyInfo.vEyePos;
                check1.NormalizeSafe();
                check2.NormalizeSafe();
                if (check1.Dot(check2) < cosf(DEG2RAD(5.0f)))
                {
                    m_State.vLookTargetPos = m_State.vShootTargetPos = m_State.vAimTargetPos = bodyInfo.vEyePos + vDir * 10.0f;
                }
            }
        }
        else
        {
            if (GetSubType() == ESubType.STP_HELI)
            {
                m_State.vLookTargetPos = new Vec3(0, 0, 0);
            }
            else if (!AllowedToFire())
            {
                SAIBodyInfo bodyInfo = GetBodyInfo();
                m_State.vLookTargetPos = m_State.vShootTargetPos = m_State.vAimTargetPos = bodyInfo.vEyePos + bodyInfo.vFireDir * 10.0f;
            }
        }
    }

    // ===================================================================
    // Literal port of CAIVehicle::Event (AIVehicle.cpp lines 730-781)
    // ===================================================================
    public override void Event(ushort eType, SAIEVENT pEvent)
    {
        CAISystem pAISystem = GetAISystem();
        switch (eType)
        {
            case (ushort)AIEVENT_AGENTDIED:
                m_bEnabled = false;
                pAISystem.NotifyEnableState(this, m_bEnabled);
                pAISystem.UpdateGroupStatus(GetGroupId());

                //SetObserver(false);
                OnDriverChanged(false);

                m_State.ClearSignals();

                break;
            case (ushort)AIEVENT_DRIVER_IN:
            case (ushort)AIEVENT_DRIVER_OUT:
                {
                    bool bEntered = (eType == (ushort)AIEVENT_DRIVER_IN);
                    OnDriverChanged(bEntered);
                    if (pEvent != null && pEvent.bSetObserver)
                    {
                        SetObserver(bEntered);
                    }
                }
                return; // those are vehicle specific, don't pass it to puppet
            case (ushort)AIEVENT_DISABLE:
                m_bEnabled = false;
                pAISystem.NotifyEnableState(this, m_bEnabled);
                pAISystem.UpdateGroupStatus(GetGroupId());

                //SetObserver(false);

                OnDriverChanged(false);
                return;
            // can not just enable vehicles - it has to have driver in
            case (ushort)AIEVENT_ENABLE:
                if (m_bDriverInside)
                    Event((ushort)AIEVENT_DRIVER_IN, pEvent);

                //SetObserver(true);
                OnDriverChanged(m_bDriverInside);
                return;
            default:
                break;
        }

        base.Event(eType, pEvent);
    }

    // ===================================================================
    // Literal port of CAIVehicle::Reset (AIVehicle.cpp lines 708-725)
    // ===================================================================
    public override void Reset(EObjectResetType type)
    {
        base.Reset(type);

        if (type == EObjectResetType.AIOBJRESET_INIT)
            SetObserver(false); // TODO(Marcio): clean this up

        CAISystem pAISystem = GetAISystem();

        m_fNextFiringTime = pAISystem.GetFrameStartTime();
        m_fFiringStartTime = pAISystem.GetFrameStartTime();
        m_fFiringPauseTime = pAISystem.GetFrameStartTime();
        m_bPoweredUp = false;
        m_ShootPhase = 0;

        m_bDriverInside = false;
        m_vDeltaTarget = new Vec3(0, 0, 0);
    }

    // ===================================================================
    // Literal port of CAIVehicle::ParseParameters (AIVehicle.cpp lines 686-691)
    // ===================================================================
    public new void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true)
    {
        base.ParseParameters(parameters, bParseMovementParams); // calls also CAIActor.ParseParams

        m_Parameters = parameters.m_sParamStruct;
    }

    // ===================================================================
    // Literal port of CAIVehicle::GetPerceivedEntityID (AIVehicle.cpp lines 695-703)
    // ===================================================================
    public override uint GetPerceivedEntityID()
    {
        // Use the driver if one is present
        uint driverId = GetDriverEntity();
        if (driverId != 0)
            return driverId;

        return base.GetPerceivedEntityID();
    }

    // ===================================================================
    // Literal port of CAIVehicle::AlertPuppets (AIVehicle.cpp lines 870-1060)
    // ===================================================================
    public void AlertPuppets()
    {
        // FUNCTION_PROFILER( GetISystem(), PROFILE_AI );

        if (GetSubType() != ESubType.STP_CAR)
            return;

        if (GetPhysics() == null)
            return;

        ActorLookUp lookUp = gAIEnv.pActorLookUp;
        lookUp.Prepare(ActorLookUp.Position | ActorLookUp.Proxy);

        pe_status_dynamics dSt = new pe_status_dynamics();
        GetPhysics().GetStatus(dSt);
        Vec3 v = dSt.v;
        v.z = 0;
        float coeff = 0.4f;
        float fv = coeff * v.GetLength();

        if (fv < 0.5f)
            return;

        Vec3 vn = new Vec3(v.x, v.y, v.z);
        vn.Normalize();

        IEntity pVehicleEntity = GetEntity();

        // Find the vehicle rectangle to avoid
        AABB localBounds;
        GetEntity().GetLocalBounds(out localBounds);
        Matrix34 tm = GetEntity().GetWorldTM();
        SAIRect3 r = new SAIRect3();
        AICollision.GetFloorRectangleFromOrientedBox(tm, localBounds, ref r);
        // Extend the box based on velocity.
        Vec3 vel = dSt.v;
        float lookAheadTime = 3.0f;
        float speedu = r.axisu.Dot(vel) * lookAheadTime;
        float speedv = r.axisv.Dot(vel) * lookAheadTime;
        if (speedu > 0)
            r.max.x += speedu;
        else
            r.min.x += speedu;
        if (speedv > 0)
            r.max.y += speedv;
        else
            r.min.y += speedv;

        if (IsPlayerInside())
        {
            r.min.y -= 1.0f;
            r.max.y += 1.0f;
        }
        float avoidOffset = (r.max.x - r.min.x) / 2 + 0.5f;

        // static bool debugdraw = false;
        bool debugdraw = false;

        if (debugdraw)
        {
            // Debug drawing — no-op in literal port (AddDebugLine not wired)
        }

        float distToPathThr = (r.max.x - r.min.x) / 2 + 0.5f;
        float dangerRange = max(r.min.Length(), r.max.Length()) * 1.5f;
        float dangerRangeSq = sqr(dangerRange);

        uint activeCount = (uint)lookUp.GetActiveCount();

        for (uint actorIndex = 0; actorIndex < activeCount; ++actorIndex)
        {
            CAIActor pAIActor = lookUp.GetActor<CAIActor>(actorIndex);
            CPuppet pPuppet = null;

            // Skip distant vehicles
            if (Distance.Point_Point2DSq(lookUp.GetPosition(actorIndex), r.center) > dangerRangeSq)
            {
                pPuppet = pAIActor.CastToCPuppet();
                if (pPuppet == null || pPuppet.GetType() == (ushort)AIOBJECT_VEHICLE)
                    continue;

                if (pPuppet.GetAvoidedVehicle() == this)
                {
                    if (pPuppet.GetVehicleAvoidingTime() > 3000)
                    {
                        CCCPOINT(nameof(CAIVehicle) + "_AlertPuppets_SignalVehicle");
                        pPuppet.SetAvoidedVehicle(new CWeakRef<CAIVehicle>(type_nil_ref.NILREF));
                        pPuppet.SetSignal(1, "OnEndVehicleDanger", GetEntity());
                    }
                }

                continue;
            }
            else
            {
                pPuppet = pAIActor.CastToCPuppet();
                if (pPuppet == null || pPuppet.GetType() == (ushort)AIOBJECT_VEHICLE)
                    continue;
            }

            System.Diagnostics.Debug.Assert(pPuppet != null);

            // Skip puppets in vehicles.
            if (lookUp.GetProxy(actorIndex).GetLinkedVehicleEntityId() != 0)
                continue;

            bool bNearPath = false;
            Vec3 puppetDir = pPuppet.GetPos() - r.center;

            float x = r.axisu.Dot(puppetDir);
            float y = r.axisv.Dot(puppetDir);

            Vec3 pathPosOut = new Vec3(0, 0, 0);
            float distToPath = 0;

            if (!m_Path.Empty())
            {
                float distAlongPath = 0;
                distToPath = m_Path.GetDistToPath(out pathPosOut, out distAlongPath, pPuppet.GetPos(), dangerRange, false); //false ->2D only

                if (distToPath <= distToPathThr + pPuppet.GetParameters().m_fPassRadius)
                    bNearPath = true;
            }

            float pr = pPuppet.GetParameters().m_fPassRadius;

            if (bNearPath || (m_Path.Empty() && x >= (r.min.x - pr) && x <= (r.max.x + pr) && y >= (r.min.y - pr) && y <= (r.max.y + pr)))
            {
                Vec3 move = new Vec3(0, 0, 0);
                if (bNearPath && distToPath > 0.1f && Distance.Point_Point2DSq(pPuppet.GetPos(), pathPosOut) > sqr(0.1f))
                {
                    Vec3 norm = pPuppet.GetPos() - pathPosOut;
                    norm.z = 0;
                    norm.Normalize();
                    move = norm * (avoidOffset + 2.0f - distToPath);
                }
                else
                {
                    move = r.axisv * (avoidOffset + 2.0f - fabsf(y));
                    if (y < 0.0f)
                        move = -move;
                }

                // Note: There used to be a CAISystem.IsPathWorth() check here, but it was removed because of the huge performance peaks.
                // Now instead the stick has larger end accuracy (that is, does not need to find exact target location).
                Vec3 avoidPos = pPuppet.GetPos() + move;
                if (debugdraw)
                {
                    // Debug drawing — no-op in literal port
                }

                if (pPuppet.GetAvoidedVehicle() != this)
                {
                    AISignalExtraData pData = new AISignalExtraData();
                    pData.point2 = avoidPos;
                    pData.point = vn;
                    pPuppet.SetSignal(1, "OnVehicleDanger", GetEntity(), pData);
                }
                pPuppet.SetAvoidedVehicle(WeakRefHelpers.GetWeakRef(this));

                // Update pos.
                CAIObject pAvoidTarget = pPuppet.GetOrCreateSpecialAIObject(ESpecialAIObjects.AISPECIAL_VEHICLE_AVOID_POS);
                pAvoidTarget?.SetPos(avoidPos, vn);
            }
            else if (pPuppet.GetAvoidedVehicle() == this)
            {
                //check rotation around Z axis (2D)
                if (!(fabs(dSt.w.z) > 0.2f && fv > 0.7f)) // if vehicle is not spinning like a tank
                {
                    if (pPuppet.GetVehicleAvoidingTime() > 3000)
                    {
                        pPuppet.SetAvoidedVehicle(new CWeakRef<CAIVehicle>(type_nil_ref.NILREF));
                        pPuppet.SetSignal(1, "OnEndVehicleDanger", GetEntity());
                    }
                }
            }
        }
    }

    // ===================================================================
    // Literal port of CAIVehicle::Serialize (AIVehicle.cpp lines 1062-1082)
    // ===================================================================
    public override void Serialize(TSerialize ser)
    {
        base.Serialize(ser);
        ser.BeginGroup("AIVehicle");
        {
            ser.Value("m_bDriverInside", ref m_bDriverInside);
            ser.Value("m_bPoweredUp", ref m_bPoweredUp);
            ser.Value("m_ShootPhase", ref m_ShootPhase);
            ser.Value("m_vDeltaTarget", ref m_vDeltaTarget);

            if (ser.IsReading())
            {
                CAISystem pAISystem = GetAISystem();

                m_fNextFiringTime = pAISystem.GetFrameStartTime();
                m_fFiringStartTime = pAISystem.GetFrameStartTime();
                m_fFiringPauseTime = pAISystem.GetFrameStartTime();
            }
        }
        ser.EndGroup();
    }

    // ===================================================================
    // Literal port of CAIVehicle::HandleVerticalMovement (AIVehicle.cpp lines 1124-1154)
    // ===================================================================
    public bool HandleVerticalMovement(Vec3 targetPos)
    {
        if (m_bPoweredUp)
            return false;

        Vec3 myPos = new Vec3(GetPos().x, GetPos().y, GetPos().z);
        Vec3 diff = new Vec3(targetPos.x - GetPos().x, targetPos.y - GetPos().y, targetPos.z - GetPos().z);

        Vec3 diff2d = new Vec3(diff.x, diff.y, diff.z);
        diff2d.z = 0;
        float ratio = diff2d.len2() > 0 ? fabs(diff.z) / diff2d.len() : fabs(diff.z);
        if (false) // if(0) in C++
        {
            if (ratio < 2.0f)
            {
                m_bPoweredUp = true;
                return false;
            }
        }

        //	m_State.vMoveDir = diff;
        //	m_State.vMoveDir.normalize();
        m_State.vMoveDir = new Vec3(0, 0, 1);
        float zScale = fabs(diff.z);
        if (zScale < 5 || diff.z < 0.0f)
        {
            m_bPoweredUp = true;
            return false;
        }
        m_State.fDesiredSpeed = 5.0f;
        return true;
    }

    // ===================================================================
    // Literal port of CAIVehicle::IsDriverInside (AIVehicle.cpp lines 1159-1167)
    // ===================================================================
    public bool IsDriverInside()
    {
        // FUNCTION_PROFILER(gEnv->pSystem, PROFILE_AI);
        if (m_bEnabled)
            return true;
        if (m_driverInsideCheck == -1)
            m_driverInsideCheck = GetDriverEntity() != 0 ? 1 : 0;
        return m_driverInsideCheck == 1;
    }

    // ===================================================================
    // Literal port of CAIVehicle::IsPlayerInside (AIVehicle.cpp lines 1172-1183)
    // ===================================================================
    public bool IsPlayerInside()
    {
        // FUNCTION_PROFILER(gEnv->pSystem, PROFILE_AI);
        if (m_bEnabled)
            return true;
        if (m_playerInsideCheck == -1)
        {
            CAIActor pDriver = GetDriver();
            m_playerInsideCheck = (pDriver != null && pDriver.GetAIType() == (ushort)AIOBJECT_PLAYER ? 1 : 0);
        }
        return m_playerInsideCheck == 1;
    }

    // ===================================================================
    // Literal port of CAIVehicle::GetDriverEntity (AIVehicle.cpp lines 1188-1193)
    // ===================================================================
    public uint GetDriverEntity()
    {
        IAIActorProxy pProxy = GetProxy();

        return (pProxy != null ? pProxy.GetLinkedDriverEntityId() : 0);
    }

    // ===================================================================
    // Literal port of CAIVehicle::GetDriver (AIVehicle.cpp lines 1198-1211)
    // ===================================================================
    public CAIActor GetDriver()
    {
        CAIActor pDriverActor = null;

        uint driverId = GetDriverEntity();
        if (driverId > 0)
        {
            IEntity pDriverEntity = gEnv.pEntitySystem?.GetEntity(driverId);
            IAIObject pDriverAI = pDriverEntity != null ? pDriverEntity.GetAI() : null;
            pDriverActor = CastToCAIActorSafe(pDriverAI);
        }

        return pDriverActor;
    }

    // ===================================================================
    // Literal port of CAIVehicle header inline methods (AIVehicle.h lines 47-48)
    // ===================================================================
    public override bool IsTargetable() { return IsActive(); }
    public new bool IsActive() { return m_bEnabled || IsDriverInside(); }

    // ===================================================================
    // Literal port of CAIVehicle::GetPathFollowerParams (AIVehicle.cpp lines 1216-1222)
    // ===================================================================
    public new void GetPathFollowerParams(PathFollowerParams outParams)
    {
        base.GetPathFollowerParams(outParams);

        outParams.endAccuracy = 1.5f;
        outParams.isVehicle = true;
    }

    // ===================================================================
    // Literal port of CAIVehicle::FireCommand (AIVehicle.cpp lines 306-682)
    // ===================================================================
    protected void FireCommand()
    {
        // FUNCTION_PROFILER(gEnv->pSystem, PROFILE_AI);

        // basic filters
        m_State.fire = EAIFireState.eAIFS_Off;
        m_State.aimTargetIsValid = false; //vAimTargetPos.Set(0,0,0);

        if (!AllowedToFire())
        {
            m_ShootPhase = 0;
            m_fFiringStartTime = GetAISystem().GetFrameStartTime();
            return;
        }

        CAIObject pFireTarget = GetFireTargetObject();

        if (pFireTarget == null)
            return;

        if (GetProxy() == null)
            return;

        // basic parameters
        // m_CurrentWeaponDescriptor is not correct now these parameters below should be given by Descriptor
        // 4/8/2006 Tetsuji

        bool bCoaxial = (m_fireMode == EFireMode.FIREMODE_SECONDARY);
        bool bAAA = (m_fireMode == EFireMode.FIREMODE_CONTINUOUS && GetSubType() == ESubType.STP_CAR);
        float fDamageRadius = bCoaxial ? 0.5f : 15.0f;
        float fAccuracy = RecalculateAccuracy();

        SAIBodyInfo bodyInfo = GetBodyInfo();

        Vec3 vActualFireDir = bodyInfo.vFireDir;
        Vec3 vFirePos = bodyInfo.vFirePos;
        Vec3 vFwdDir = bodyInfo.vMoveDir;
        Vec3 vMyPos = bodyInfo.vEyePos;

        if (GetEntity() != null)
        {
            Matrix33 worldRotMat = GetEntity().GetWorldTM().m33;
            Vec3 vFwdOrg = new Vec3(0.0f, 1.0f, 0.0f);
            vFwdDir = worldRotMat * vFwdOrg;
            //	gEnv->pAISystem->AddDebugLine( vFirePos, vFirePos + vFwdDir * 20.0f , 255, 255, 255, 1.0f);
        }

        IPhysicalEntity pPhys = GetPhysics();
        if (pPhys != null)
        {
            pe_status_pos my_status = new pe_status_pos();
            pPhys.GetStatus(my_status);
            vMyPos = my_status.pos;
        }

        Vec3 vTargetPos;
        {
            IPhysicalEntity pAttPhys = pFireTarget.GetPhysics();
            if (pAttPhys != null)
            {
                pe_status_pos target_status = new pe_status_pos();
                pAttPhys.GetStatus(target_status);
                vTargetPos = target_status.pos;
                Vec3 ofs = target_status.BBox[0] + target_status.BBox[1];
                ofs.x *= 0.5f;
                ofs.y *= 0.5f;
                ofs.z *= 0.25f;
                vTargetPos += ofs;
            }
            else
            {
                vTargetPos = pFireTarget.GetPos();
            }
        }

        float targetHeight = vTargetPos.z - gEnv.p3DEngine.GetTerrainElevation(vTargetPos.x, vTargetPos.y);
        float fireHeight = vFirePos.z - gEnv.p3DEngine.GetTerrainElevation(vFirePos.x, vFirePos.y);

        // For the helicopter/vtol missiles
        if (GetSubType() == ESubType.STP_HELI)
        {
            if (m_ShootPhase == 0)
            {
                fDamageRadius = 2.0f;
                m_vDeltaTarget = GetError(vTargetPos, vFirePos, fAccuracy);
                m_vDeltaTarget += PredictMovingTarget(pFireTarget, vTargetPos, vFirePos, 1.0f, 0.0f);
                m_vDeltaTarget += vTargetPos;

                Vec3 vTargetDir = m_vDeltaTarget - vFirePos;
                Vec3 vUnitTargetDir = vTargetDir;
                Vec3 vNormalizedTargetDir = vTargetDir;
                vNormalizedTargetDir.NormalizeSafe();
                float distanceToTheTarget = vTargetDir.GetLength();
                // If the target is not in front of him. can't fire.

                vUnitTargetDir.NormalizeSafe();
                //	gEnv->pAISystem->AddDebugLine( vFirePos, vFirePos + vUnitTargetDir * 20.0f , 255, 0, 0, 1.0f);

                float fDifference = vUnitTargetDir.Dot(vFwdDir);
                if (fDifference < cos_tpl(DEG2RAD(30.0f)))
                {
                    m_fFiringStartTime = GetAISystem().GetFrameStartTime();
                    return;
                }
                // if a missile pass by near a target
                Vec3 vTmp = vFwdDir.Cross(vTargetDir);

                float d = vTmp.GetLength();
                float dot = vNormalizedTargetDir.Dot(vFwdDir);

                if (d > 12.0f || (dot > DEG2RAD(10.0f) && d > 10.0f))
                {
                    m_fFiringStartTime = GetAISystem().GetFrameStartTime();
                    return;
                }

                // it also need to be vertical to a wing vector.
                // this check is needed because firing position is not accurate.26/06/2006 tetsuji
                // check if there is the same specie around a target position

                if (m_fireMode != EFireMode.FIREMODE_FORCED)
                {
                    if (CheckExplosion(m_vDeltaTarget, vFirePos, vUnitTargetDir, fDamageRadius) == false)
                    {
                        m_fFiringStartTime = GetAISystem().GetFrameStartTime();
                        return;
                    }
                    else
                    {
                        m_ShootPhase = 1;
                    }
                }
                else
                    m_ShootPhase = 1;
            }
            if (m_ShootPhase != 0)
            {
                // finalize a result and check the time.
                CTimeValue firingDuration = new CTimeValue();

                firingDuration.SetSeconds(0.0f);
                firingDuration += m_fFiringStartTime;
                if (GetAISystem().GetFrameStartTime() < firingDuration)
                {
                    return;
                }

                m_State.vLookTargetPos = m_vDeltaTarget;
                m_State.vShootTargetPos = m_vDeltaTarget;
                m_State.vAimTargetPos = m_vDeltaTarget;
                m_State.aimTargetIsValid = true;
                m_State.fire = EAIFireState.eAIFS_On;
                m_ShootPhase = 0;
            }
            return;
        }

        // For the warrior,
        if (fireHeight > 10.0f)
        {
            if (GetAISystem().GetFrameStartTime() < m_fNextFiringTime)
            {
                m_State.fire = EAIFireState.eAIFS_On;
            }
            else
            {
                m_State.vLookTargetPos = vTargetPos;
                m_State.vAimTargetPos = vTargetPos;
                m_State.aimTargetIsValid = true;
                m_State.vShootTargetPos = vTargetPos;
                m_State.fire = EAIFireState.eAIFS_On;
                CTimeValue firingDuration = new CTimeValue();
                firingDuration.SetSeconds(3.0f);
                firingDuration += m_fFiringStartTime;
                m_fNextFiringTime = firingDuration;
            }

            return;
        }

        // For tank Coaxial Gun
        if (bCoaxial == true || bAAA == true)
        {
            Vec3 vTargetDir = vTargetPos - vFirePos;
            //After the rotation of the turret has been completed,
            float fDuration = 2.0f;
            float fBoxRange = 3.0f;
            float fMaxDot = 30.0f;
            if (bAAA == true)
            {
                fDuration = 2.0f;
                fBoxRange = 30.0f;
                fMaxDot = 30.0f;
                PredictMovingTarget(pFireTarget, vTargetPos, vFirePos, 1.0f, 30.0f);
                if (vTargetDir.GetLength() < 20.0f)
                {
                    m_State.vLookTargetPos = vTargetPos;
                    m_State.vAimTargetPos = vTargetPos;
                    m_State.aimTargetIsValid = true;
                    m_State.vShootTargetPos = vTargetPos;
                    return;
                }
            }
            else
            {
                Vec3 vTargetDirFromCenter = vTargetPos - vMyPos;
                Vec3 vTargetDirFromFirePos = vTargetPos - vFirePos;
                Vec3 vCenterToFirePos = vFirePos - vMyPos;
                vTargetDirFromCenter.z = vTargetDirFromFirePos.z = vCenterToFirePos.z = 0.0f;
                float distanceFromCenter = vTargetDirFromCenter.GetLength();
                float distanceFromFirePos = vTargetDirFromFirePos.GetLength();
                float distanceFromCenterToFire = vCenterToFirePos.GetLength();
                fDuration = 4.0f;
                fBoxRange = 5.0f;
                fMaxDot = (distanceFromCenter < 15.0f) ? 120.0f : 15.0f;
                if (distanceFromCenter < distanceFromCenterToFire + 1.5f)
                {
                    return;
                }
            }

            Vec3 vTmp = vActualFireDir.Cross(vTargetDir);
            float d = vTmp.GetLength();

            vTargetDir.NormalizeSafe();
            float inner = vActualFireDir.Dot(vTargetDir);

            bool allowFiring = true;

            //if there is still a big difference between actual fire direction and ideal fire direction,
            if (d > fBoxRange || inner < cos_tpl(DEG2RAD(fMaxDot)) || GetAISystem().GetFrameStartTime() >= m_fNextFiringTime)
            {
                m_fNextFiringTime = new CTimeValue();
                m_fNextFiringTime.SetSeconds(fDuration);
                m_fNextFiringTime += GetAISystem().GetFrameStartTime();

                m_State.vLookTargetPos = vTargetPos;
                m_State.vAimTargetPos = vTargetPos;
                m_State.aimTargetIsValid = true;
                m_State.vShootTargetPos = vTargetPos;
                allowFiring = false;
            }
            else
            {
                if (bAAA != true)
                {
                    vTargetPos = pFireTarget.GetPos();
                    vTargetDir = vTargetPos - vFirePos;
                    vTargetPos.z -= 0.3f;
                    m_State.vLookTargetPos = vTargetPos;
                    m_State.vAimTargetPos = vTargetPos;
                    m_State.aimTargetIsValid = true;
                    m_State.vShootTargetPos = vTargetPos;
                }
            }

            UpdateTargetTracking(WeakRefHelpers.GetWeakRef((CAIObject)pFireTarget), m_State.vAimTargetPos);

            if (allowFiring && GetAISystem().GetFrameStartTime() < m_fNextFiringTime)
            {
                Vec3 shootOut;
                if (AdjustFireTarget(pFireTarget, m_State.vShootTargetPos, CanDamageTarget(), 0.5f,
                    DEG2RAD(30.0f), out shootOut))
                {
                    m_State.vShootTargetPos = shootOut;
                    m_State.fire = EAIFireState.eAIFS_On;
                }
            }

            return;
        }

        // For the tank cannon
        // in this part, we wait until the turret is moving then shoot
        {
            if (m_ShootPhase == 0) //aiming set up
            {
                if (targetHeight < 10.0f)
                {
                    IPhysicalEntity pAttPhys = pFireTarget.GetPhysics();
                    if (pAttPhys != null)
                    {
                        pe_status_dynamics dSt = new pe_status_dynamics();
                        pAttPhys.GetStatus(dSt);
                    }

                    m_vDeltaTarget = GetError(vTargetPos, vFirePos, fAccuracy);
                    m_vDeltaTarget += PredictMovingTarget(pFireTarget, vTargetPos, vFirePos, 1.0f, 0.0f);
                    m_vDeltaTarget += vTargetPos;
                }
                m_ShootPhase++;

                CTimeValue tValue = new CTimeValue();
                tValue.SetSeconds(5.0f);
                m_fFiringPauseTime = GetAISystem().GetFrameStartTime() + tValue;
            }
            else if (m_ShootPhase == 1) //aiming ( the turret is rotating )
            {
                m_State.vLookTargetPos = m_State.vAimTargetPos = m_State.vShootTargetPos = m_vDeltaTarget;

                //After the rotation of the turret has been completed,
                Vec3 vTargetDir = m_vDeltaTarget - vFirePos;

                vTargetDir.NormalizeSafe();
                vActualFireDir.NormalizeSafe();

                float inner = vActualFireDir.Dot(vTargetDir);
                float cosval = cos_tpl(DEG2RAD(3.0f));
                if (inner > cosval || m_fFiringPauseTime < GetAISystem().GetFrameStartTime())
                {
                    CTimeValue tValue = new CTimeValue();
                    tValue.SetSeconds(0.5f);
                    m_fFiringPauseTime = GetAISystem().GetFrameStartTime() + tValue;
                    m_ShootPhase++;
                }
            }
            else if (m_ShootPhase == 2) //fire ( the turret is fixed )
            {
                m_State.vLookTargetPos = m_State.vAimTargetPos = m_State.vShootTargetPos = m_vDeltaTarget;
                if (m_fFiringPauseTime < GetAISystem().GetFrameStartTime())
                {
                    CTimeValue tValue = new CTimeValue();
                    tValue.SetSeconds(0.5f);
                    m_fFiringPauseTime = GetAISystem().GetFrameStartTime() + tValue;
                    m_ShootPhase++;
                }
            }
            else if (m_ShootPhase == 3) //fire ( the turret is fixed )
            {
                m_State.vLookTargetPos = m_State.vAimTargetPos = m_State.vShootTargetPos = vActualFireDir * 10.0f + vFirePos; //fix the turret

                CTimeValue tValue = new CTimeValue();
                tValue.SetSeconds(3.0f);

                if (m_fFiringPauseTime < GetAISystem().GetFrameStartTime())
                {
                    if (CheckTargetInRange(ref m_vDeltaTarget) && m_fFiringPauseTime + tValue > GetAISystem().GetFrameStartTime())
                    {
                        Vec3 vTargetDir = m_vDeltaTarget - vFirePos;
                        Vec3 vTmp = vActualFireDir.Cross(vTargetDir);
                        float d = vTmp.GetLength();
                        float distanceToTheTarget = vTargetDir.GetLength();
                        vTargetDir.NormalizeSafe();
                        float inner = vActualFireDir.Dot(vTargetDir);

                        if (d < 30.0f && inner > cos_tpl(DEG2RAD(30.0f)))
                        {
                            if (m_fireMode == EFireMode.FIREMODE_FORCED || CheckExplosion(vActualFireDir * distanceToTheTarget + vFirePos, vFirePos, vActualFireDir, fDamageRadius) == true)
                            {
                                m_State.fire = EAIFireState.eAIFS_On;
                                return;
                            }
                        }
                        else
                        {
                            tValue.SetSeconds(3.0f);
                        }
                    }
                    else
                    {
                        m_ShootPhase = 0;
                    }
                }
            }
        }
    }

    // ===================================================================
    // Literal port of CAIVehicle::GetEnemyTarget (AIVehicle.cpp lines 1085-1120)
    // ===================================================================
    protected bool GetEnemyTarget(int objectType, Vec3 hitPosition, float fDamageRadius2, out CAIObject pTarget)
    {
        pTarget = null;

        if (!gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)objectType, out List<CCountedRef<CAIObject>> bucket))
            return true;

        for (int i = 0; i < bucket.Count; i++)
        {
            // (MATT) Strong, so always valid {2009/03/25}
            CAIObject pObject = bucket[i].GetAIObject();
            if (pObject.GetType() != objectType)
                break;
            if (pObject.IsEnabled())
            {
                Vec3 dir = pObject.GetPos() - hitPosition;
                float dist2 = dir.GetLengthSquared();
                if (dist2 < fDamageRadius2)
                {
                    CAIActor pActor = pObject.CastToCAIActor();
                    if (pActor != null && IsHostile(pObject, false) == false)
                    {
                        if (GetEntityID() != pObject.GetEntityID())
                        {
                            pTarget = null;
                            return false;
                        }
                    }
                    else
                        pTarget = pObject;
                }
            }
        }
        return true;
    }

    // ===================================================================
    // Literal port of CAIVehicle::OnDriverChanged (AIVehicle.cpp lines 785-804)
    // ===================================================================
    protected void OnDriverChanged(bool bEntered)
    {
        CAISystem pAISystem = GetAISystem();
        System.Diagnostics.Debug.Assert(pAISystem != null);

        m_bDriverInside = bEntered;
        m_bEnabled = bEntered;

        pAISystem.NotifyEnableState(this, m_bEnabled);
        pAISystem.UpdateGroupStatus(GetGroupId());

        // TODO This was called before when receiving the AIEVENT_DRIVER_OUT event. Check if this
        // needs to be executed still. I suspect not. (Kevin)
        /*if (!bEntered)
        {
            gAIEnv.pAIObjectManager->RemoveObjectFromAllOfType(AIOBJECT_ACTOR,this);
            gAIEnv.pAIObjectManager->RemoveObjectFromAllOfType(AIOBJECT_VEHICLE,this);
            gAIEnv.pAIObjectManager->RemoveObjectFromAllOfType(AIOBJECT_ATTRIBUTE,this);
        }*/
    }

    // local functions for firecommand()

    // ===================================================================
    // Literal port of CAIVehicle::PredictMovingTarget (AIVehicle.cpp lines 162-193).
    // ===================================================================
    private Vec3 PredictMovingTarget(CAIObject pTarget, Vec3 vTargetPos, Vec3 vFirePos, float duration, float distpred)
    {
        Vec3 vError = new Vec3(0, 0, 0);

        // if we need a prediction of the target
        if (pTarget != null)
        {
            pe_status_dynamics dSt = new pe_status_dynamics();
            GetProxy().GetPhysics().GetStatus(dSt);
            IPhysicalEntity pPhys = pTarget.GetPhysics();
            if (pPhys != null)
            {
                pPhys.GetStatus(dSt);
                if (GetSubType() == ESubType.STP_HELI)
                {
                    vError = dSt.v * duration;
                }
                else
                {
                    Vec3 vPrediction = vTargetPos + dSt.v * duration;
                    Vec3 vTmp = vPrediction - vFirePos;
                    if (distpred > 0.0f)
                    {
                        float len = vTmp.GetLength();
                        vError.z = len / distpred;
                    }
                }
            }
        }
        return vError;
    }

    // ===================================================================
    // Literal port of CAIVehicle::CheckExplosion (AIVehicle.cpp lines 235-303)
    // ===================================================================
    private bool CheckExplosion(Vec3 vTargetPos, Vec3 vFirePos, Vec3 vActuallFireDir, float fDamageRadius)
    {
        // get the hit point of a bullet, then check if there is a friend around the hit point.
        // not to give a damage to same species.

        Vec3 vDirVector = vTargetPos - vFirePos;
        float fDamageRadius2 = fDamageRadius > 0.0f ? fDamageRadius * fDamageRadius : 1.0f;

        Vec3 vHitPoint;

        PhysSkipList skipList = new PhysSkipList();
        GetPhysicalSkipEntities(skipList);

        ray_hit hit = new ray_hit();

        if (gAIEnv.pWorld != null && gAIEnv.pWorld.RayWorldIntersection(vFirePos, vDirVector, COVER_OBJECT_TYPES, HIT_COVER,
            hit, 1, skipList.Count > 0 ? skipList.ToArray() : null, skipList.Count) != 0)
        {
            //When the bullet will hit a object which is on the way.
            vHitPoint = hit.pt;
        }
        else
        {
            //When the bullet will hit the player.
            vHitPoint = vTargetPos;
        }

        CAIObject pTarget;
        bool bNoFriendsInRadius = GetEnemyTarget((int)AIOBJECT_ACTOR, vHitPoint, fDamageRadius2, out pTarget);
        if (bNoFriendsInRadius && pTarget == null)
            bNoFriendsInRadius = GetEnemyTarget((int)AIOBJECT_PLAYER, vHitPoint, fDamageRadius2, out pTarget);
        if (bNoFriendsInRadius && pTarget == null)
            bNoFriendsInRadius = GetEnemyTarget((int)AIOBJECT_VEHICLE, vHitPoint, fDamageRadius2, out pTarget);

        Vec3 vActualDir = vActuallFireDir;
        vActualDir.SetLength(vDirVector.GetLength());

        if (GetSubType() != ESubType.STP_HELI)
        {
            // if will hit the terrain/static and there is no friend
            if (bNoFriendsInRadius == true)
            {
                if (gAIEnv.pWorld != null && gAIEnv.pWorld.RayWorldIntersection(vFirePos, vActualDir, COVER_OBJECT_TYPES, HIT_COVER,
                    hit, 1, skipList.Count > 0 ? skipList.ToArray() : null, skipList.Count) != 0)
                {
                    // and if it is far away from the target.
                    if ((vTargetPos - hit.pt).GetLength() > fDamageRadius * 3.0f)
                        // shouldn't fire
                        bNoFriendsInRadius = false;
                }
            }
        }
        else
        {
            if (bNoFriendsInRadius == true)
            {
                if (gAIEnv.pWorld != null && gAIEnv.pWorld.RayWorldIntersection(vFirePos, vActualDir, COVER_OBJECT_TYPES, HIT_COVER,
                    hit, 1, skipList.Count > 0 ? skipList.ToArray() : null, skipList.Count) != 0)
                {
                    if ((vFirePos - hit.pt).GetLength() < fDamageRadius * 10.0f)
                        bNoFriendsInRadius = false;
                }
            }
        }

        return bNoFriendsInRadius;
    }

    // ===================================================================
    // Literal port of CAIVehicle::GetError (AIVehicle.cpp lines 197-233)
    // ===================================================================
    private Vec3 GetError(Vec3 vTargetPos, Vec3 vFirePos, float fAccuracy)
    {
        // add an error to the target position depending on accuracy
        Vec3 vError = new Vec3(0, 0, 0);

        if (fAccuracy < 1.0f)
        {
            float dist = (vTargetPos - vFirePos).GetLength();
            float fAccuracyScale = (1.0f - fAccuracy);
            float rangeRotation = fAccuracyScale * cry_random(-50.0f, 50.0f);
            float zofs = dist * sinf(DEG2RAD(0.0f));

            float rangeLength;
            if (cry_random(0.0f, 0.99f) < fAccuracy)
                rangeLength = fAccuracyScale * cry_random(0.0f, 27.0f);
            else
                rangeLength = fAccuracyScale * cry_random(15.0f, 25.0f);

            vError = vFirePos - vTargetPos;
            vError.z = 0.0f;
            vError.NormalizeSafe();
            vError *= rangeLength;
            vError *= min(1.5f, dist / 100.0f);

            Matrix33 rotmatZ = Matrix33.CreateRotationZ(DEG2RAD(rangeRotation));
            vError = rotmatZ * vError;

            vError = vError + vTargetPos;
            vError.z = gEnv.p3DEngine.GetTerrainElevation(vError.x, vError.y);
            vError = vError - vTargetPos;
            vError.z -= zofs;
        }

        return vError;
    }

    // ===================================================================
    // Literal port of CAIVehicle::RecalculateAccuracy (AIVehicle.cpp lines 149-161).
    // ===================================================================
    private float RecalculateAccuracy()
    {
        float fAccuracy = m_Parameters.m_fAccuracy;

        if (fAccuracy > 1.0f)
            fAccuracy = 1.0f;
        else if (fAccuracy < 0)
            fAccuracy = 0;

        return fAccuracy;
    }

    // ===================================================================
    // Fields (AIVehicle.h lines 65-76)
    // ===================================================================
    private bool m_bPoweredUp;

    private CTimeValue m_fNextFiringTime;
    private CTimeValue m_fFiringPauseTime;
    private CTimeValue m_fFiringStartTime;

    private Vec3 m_vDeltaTarget;

    private int m_ShootPhase;
    private /*mutable*/ int m_driverInsideCheck;
    private int m_playerInsideCheck;
    private bool m_bDriverInside;
}

// ILINE helpers from AIVehicle.h lines 79-80
public static class CAIVehicleHelpers
{
    public static CAIVehicle CastToCAIVehicleSafe(IAIObject pAI) { return pAI?.CastToCAIVehicle(); }
}

// PathFollowerParams defined in PipeUser.cs — isVehicle field added there
