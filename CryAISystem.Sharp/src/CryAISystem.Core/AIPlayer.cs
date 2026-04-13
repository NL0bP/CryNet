#define ENABLE_MISSLOCATION_SENSOR
// Literal port of dev/Code/CryEngine/CryAISystem/AIPlayer.cpp (1198 lines).
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CryAISystem.CryMath;
using static CryAISystem.CCCPOINT_HELPER;
using static CryAISystem.NilRefHelper;
using static CryAISystem.EAIEvent;
using static CryAISystem.EAIObjectType;
using CryAISystem.CryCommon;
using SmartScriptTable = CryAISystem.CryCommon.SmartScriptTable;

namespace CryAISystem;

// ChrisR: Disable the MissLocationSensor to save multiple GetEntitiesAround calls in physics
// C++ had: #if !defined(MOBILE) ... #define ENABLE_MISSLOCATION_SENSOR 1
// In C# the #define is at the top of the file (always enabled for non-mobile targets).

// The variables needs to be carefully tuned to possible the player action speeds.

public class CAIPlayer : CAIActor
{
    // typedef CAIActor MyBase — we call base. explicitly

    private static readonly float PLAYER_ACTION_SPRINT_RESET_TIME = 0.5f;
    private static readonly float PLAYER_ACTION_JUMP_RESET_TIME = 1.3f;
    private static readonly float PLAYER_ACTION_CLOAK_RESET_TIME = 1.5f;
    private static readonly float PLAYER_IGNORE_COVER_TIME = 6.0f;

    // ===================================================================
    // CAIPlayer() (AIPlayer.cpp lines 34-52)
    // ===================================================================
    public CAIPlayer()
    {
        m_FOV = 0;
        m_playerStuntSprinting = -1.0f;
        m_playerStuntJumping = -1.0f;
        m_playerStuntCloaking = -1.0f;
        m_playerStuntUncloaking = -1.0f;
        m_stuntDir = new Vec3(0, 0, 0);
        m_mercyTimer = -1.0f;
        m_coverExposedTime = -1.0f;
        m_coolMissCooldown = 0.0f;
        m_damagePartsUpdated = false;
        m_lastGrabbedEntityID = 0;
#if ENABLE_MISSLOCATION_SENSOR
        m_pMissLocationSensor = new CMissLocationSensor(this);
#endif
        _fastcast_CAIPlayer = true;
    }

    // ~CAIPlayer() (AIPlayer.cpp lines 56-62)
    public void Destructor_CAIPlayer()
    {
        if (m_exposedCoverState.rayID.id != 0)
            gAIEnv.pRayCaster?.Cancel(m_exposedCoverState.rayID);

        ReleaseExposedCoverObjects();
    }

    // ===================================================================
    // Reset (AIPlayer.cpp lines 66-96)
    // ===================================================================
    public override void Reset(EObjectResetType type)
    {
        base.Reset(type);

        SetObservable(type == EObjectResetType.AIOBJRESET_INIT);

        m_fLastUpdateTargetTime = new CTimeValue(0.0f);
        m_lastGrabbedEntityID = 0;

        if (m_exposedCoverState.rayID.id != 0)
        {
            gAIEnv.pRayCaster?.Cancel(m_exposedCoverState.rayID);
            m_exposedCoverState.rayID = new QueuedRayID();
        }

        m_deathCount = 0;
        m_lastThrownItems.Clear();
        m_stuntTargets.Clear();
        m_stuntDir.Set(0, 0, 0);
        m_mercyTimer = -1.0f;
        m_coverExposedTime = -1.0f;
        m_coolMissCooldown = 0.0f;
        m_damagePartsUpdated = false;

#if ENABLE_MISSLOCATION_SENSOR
        if (m_pMissLocationSensor != null)
            m_pMissLocationSensor.Reset();
#endif

        ReleaseExposedCoverObjects();
    }

    // ===================================================================
    // ReleaseExposedCoverObjects (AIPlayer.cpp lines 100-105)
    // ===================================================================
    private void ReleaseExposedCoverObjects()
    {
        for (int i = 0, ni = m_exposedCoverObjects.Count; i < ni; ++i)
            m_exposedCoverObjects[i].pPhysEnt?.Release();
        m_exposedCoverObjects.Clear();
    }

    // ===================================================================
    // AddExposedCoverObject (AIPlayer.cpp lines 109-146)
    // ===================================================================
    private void AddExposedCoverObject(IPhysicalEntity pPhysEnt)
    {
        // FUNCTION_PROFILER

        int oldest = 0;
        float oldestTime = float.MaxValue; // Count down timers, find smallest value.
        for (int i = 0, ni = m_exposedCoverObjects.Count; i < ni; ++i)
        {
            SExposedCoverObject co = m_exposedCoverObjects[i];
            if (co.pPhysEnt == pPhysEnt)
            {
                co.t = PLAYER_IGNORE_COVER_TIME;
                m_exposedCoverObjects[i] = co;
                return;
            }
            if (co.t < oldestTime)
            {
                oldest = i;
                oldestTime = co.t;
            }
        }

        // Limit the number of covers, override oldest one.
        if (m_exposedCoverObjects.Count >= 3)
        {
            // Release the previous entity
            m_exposedCoverObjects[oldest].pPhysEnt?.Release();
            // Fill in new.
            pPhysEnt?.AddRef();
            var item = m_exposedCoverObjects[oldest];
            item.pPhysEnt = pPhysEnt;
            item.t = PLAYER_IGNORE_COVER_TIME;
            m_exposedCoverObjects[oldest] = item;
        }
        else
        {
            // Add new
            pPhysEnt?.AddRef();
            m_exposedCoverObjects.Add(new SExposedCoverObject(pPhysEnt, PLAYER_IGNORE_COVER_TIME));
        }
    }

    // ===================================================================
    // CollectExposedCover (AIPlayer.cpp lines 150-171)
    // ===================================================================
    private void CollectExposedCover()
    {
        // FUNCTION_PROFILER

        if (m_coverExposedTime > 0.0f)
        {
            if (m_exposedCoverState.asyncState == AsyncState.AsyncReady)
            {
                m_exposedCoverState.asyncState = AsyncState.AsyncInProgress;

                // Find the object directly in front of the player
                Vec3 pos = GetPos();
                Vec3 dir = GetViewDir() * 3.0f;
                int flags = rwi_colltype_any | (geom_colltype_obstruct << rwi_colltype_bit) | (VIEW_RAY_PIERCABILITY & rwi_pierceability_mask);

                m_exposedCoverState.rayID = gAIEnv.pRayCaster != null
                    ? QueueRay(pos, dir, EAICollisionEntities.AICE_STATIC, flags)
                    : new QueuedRayID();
            }
        }
    }

    // Helper for async ray casting — literal port calls gAIEnv.pRayCaster->Queue
    private QueuedRayID QueueRay(Vec3 pos, Vec3 dir, EAICollisionEntities entities, int flags)
    {
        // Full async ray cast queue not yet implemented — synchronous fallback
        RayCastResult result = gAIEnv.pRayCaster.Cast(new RayCastRequest(pos, dir, entities, flags));
        CollectExposedCoverRayComplete(new QueuedRayID { id = 1 }, result);
        return new QueuedRayID();
    }

    // ===================================================================
    // CollectExposedCoverRayComplete (AIPlayer.cpp lines 173-183)
    // ===================================================================
    private void CollectExposedCoverRayComplete(QueuedRayID rayID, RayCastResult result)
    {
        if (m_exposedCoverState.rayID.id == rayID.id)
        {
            m_exposedCoverState.rayID = new QueuedRayID();
            m_exposedCoverState.asyncState = AsyncState.AsyncReady;

            if (result)
            if (result[0].pCollider != null)
                AddExposedCoverObject(result[0].pCollider);
        }
    }

    // ===================================================================
    // GetObservablePositions (AIPlayer.cpp lines 185-202)
    // ===================================================================
    public override void GetObservablePositions(ObservableParams observableParams)
    {
        IEntity entity = GetEntity();
        if (entity == null)
        {
            return;
        }

        Vec3 pos = GetPos();
        Quat rotation = entity.GetRotation();

        observableParams.observablePositionsCount = 3;
        observableParams.observablePositions[0] = pos;

        // shoulder positions
        observableParams.observablePositions[1] = pos + rotation.Rotate(new Vec3(0.15f, 0.0f, -0.2f));
        observableParams.observablePositions[2] = pos + rotation.Rotate(new Vec3(-0.15f, 0.0f, -0.2f));
    }

    // ===================================================================
    // GetObservableTypeMask (AIPlayer.cpp lines 204-207)
    // ===================================================================
    public override uint GetObservableTypeMask()
    {
        return base.GetObservableTypeMask() | Player;
    }

    // ===================================================================
    // GetPhysicalSkipEntities (AIPlayer.cpp lines 211-222)
    // ===================================================================
    public override void GetPhysicalSkipEntities(PhysSkipList skipList)
    {
        base.GetPhysicalSkipEntities(skipList);

        // Skip exposed covers
        for (int i = 0, ni = m_exposedCoverObjects.Count; i < ni; ++i)
        {
            stl.push_back_unique(skipList, m_exposedCoverObjects[i].pPhysEnt);
        }

        System.Diagnostics.Debug.Assert(skipList.Count <= 5, "Too many physical skipped entities determined. See SRwiRequest definition.");
    }

    // ===================================================================
    // ParseParameters (AIPlayer.cpp lines 226-230)
    // ===================================================================
    public new void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true)
    {
        base.ParseParameters(parameters, bParseMovementParams);
        m_Parameters = parameters.m_sParamStruct;
    }

    // ===================================================================
    // IsPointInFOV (AIPlayer.cpp lines 234-252)
    // ===================================================================
    public override EFieldOfViewResult IsPointInFOV(Vec3 pos, float distanceScale = 1.0f)
    {
        EFieldOfViewResult eResult = EFieldOfViewResult.eFOV_Outside;

        Vec3 vDirection = pos - GetPos();
        float fDirectionLengthSq = vDirection.GetLengthSquared();
        vDirection.NormalizeSafe();

        // lets see if it is outside of its vision range
        if (fDirectionLengthSq > 0.1f && fDirectionLengthSq <= sqr(m_Parameters.m_PerceptionParams.sightRange * distanceScale))
        {
            Vec3 vViewDir = GetViewDir().GetNormalizedSafe();
            float fDot = vDirection.Dot(vViewDir);

            eResult = (fDot >= m_FOV ? EFieldOfViewResult.eFOV_Primary : EFieldOfViewResult.eFOV_Outside);
        }

        return eResult;
    }

    // ===================================================================
    // UpdateAttentionTarget (AIPlayer.cpp lines 256-294)
    // ===================================================================
    public void UpdateAttentionTarget(CWeakRef<CAIObject> refTarget)
    {
        bool bSameTarget = (m_refAttentionTarget == refTarget);
        if (bSameTarget)
        {
            m_fLastUpdateTargetTime = GetAISystem().GetFrameStartTime();
        }
        else // compare the new target with the current one
        {
            CCCPOINT("CAIPlayer_UpdateAttentionTarget");

            CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();
            CAIObject pTarget = refTarget.GetAIObject();

            Vec3 direction = pAttentionTarget.GetPos() - GetPos();

            // lets see if it is outside of its vision range

            Vec3 myorievector = GetViewDir();
            float dist = direction.Length();

            if (dist > 0)
                direction /= dist;
            Vec3 directionNew = pTarget.GetPos() - GetPos();

            float distNew = directionNew.Length();
            if (distNew > 0)
                directionNew /= dist;

            float fdot = direction.Dot(myorievector);
            // check if new target is more interesting, by checking if old target is still visible
            // and comparing distances if it is
            if (fdot < 0 || fdot < m_FOV || distNew < dist)
            {
                m_refAttentionTarget = refTarget;
                m_fLastUpdateTargetTime = GetAISystem().GetFrameStartTime();
            }
        }
    }

    // ===================================================================
    // AdjustTargetVisibleRange (AIPlayer.cpp lines 298-330)
    // ===================================================================
    public override float AdjustTargetVisibleRange(CAIActor observer, float fVisibleRange)
    {
        float fRangeScale = 1.0f;

        // Adjust using my light level if the observer is affected by light
        if (IsAffectedByLight())
        {
            EAILightLevel targetLightLevel = GetLightLevel();
            switch (targetLightLevel)
            {
                //	case AILL_LIGHT: SOMSpeed
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
            float fDistance = Distance_Point_Point(GetPos(), observerPos);
            float fDistanceFactor = (fVisibleRange > float.Epsilon ? GetAISystem().GetVisPerceptionDistScale(fDistance / fVisibleRange) : 0.0f);

            float fWaterOcclusionEffect = 2.0f * fCachedWaterOcclusionValue + (1 - fDistanceFactor) * 0.5f;

            fRangeScale *= (fWaterOcclusionEffect > 1.0f ? 0.0f : 1.0f - fWaterOcclusionEffect);
        }

        // Return new range
        return fVisibleRange * fRangeScale;
    }

    // ===================================================================
    // IsAffectedByLight (AIPlayer.cpp lines 334-338)
    // ===================================================================
    public override bool IsAffectedByLight()
    {
        return (gAIEnv.CVars.PlayerAffectedByLight != 0 ||
            base.IsAffectedByLight());
    }

    // ===================================================================
    // Update (AIPlayer.cpp lines 342-498)
    // ===================================================================
    public new void Update(EObjectUpdate type)
    {
        if (m_refAttentionTarget.IsValid())
        {
            if (!m_refAttentionTarget.GetAIObject().IsEnabled() ||
                    (GetAISystem().GetFrameStartTime() - m_fLastUpdateTargetTime).GetMilliSecondsAsInt64() > 5000)
                m_refAttentionTarget.Reset();
        }

        // There should never be player without physics (except in multiplayer, this case is valid).
        if (GetPhysics() == null)
        {
            if (!gEnv.bMultiplayer)
            {
                AILog.AIWarning("AIPlayer::Update Player {0} does not have physics!", GetName());
            }
            return;
        }

#if ENABLE_MISSLOCATION_SENSOR
        if (m_pMissLocationSensor != null)
            m_pMissLocationSensor.DebugDraw();
#endif

        CCCPOINT("CAIPlayer_Update");

        IEntity grabbedEntity = GetGrabbedEntity();
        uint grabbedEntityID = grabbedEntity != null ? grabbedEntity.GetId() : 0;

        if (m_lastGrabbedEntityID != grabbedEntityID)
        {
            UpdateObservableSkipList();

            m_lastGrabbedEntityID = grabbedEntityID;
        }

        // make sure to update direction when entity is not moved
        SAIBodyInfo bodyInfo = QueryBodyInfo();
        SetPos(bodyInfo.vEyePos);
        SetEntityDir(bodyInfo.vEntityDir);
        SetMoveDir(bodyInfo.vMoveDir);
        SetViewDir(bodyInfo.GetEyeDir());
        SetBodyDir(bodyInfo.GetBodyDir());

        // Determine if position has changed. When crouching/standing we also need to recalculate the water
        // occlusion value even if we didn't move at least one meter.
        float zMinDifferenceBetweenStandAndCrouchToRecalculateWaterOcclusion = 0.6f;
        bool playerFullyChangedStance = (m_lastFullUpdateStance != bodyInfo.stance)
            && MathF.Abs(m_vLastFullUpdatePos.z - bodyInfo.vEyePos.z) > zMinDifferenceBetweenStandAndCrouchToRecalculateWaterOcclusion;
        if (type == EObjectUpdate.AIUPDATE_FULL && (IsEquivalent(m_vLastFullUpdatePos, bodyInfo.vEyePos, 1.0f) == 0 || playerFullyChangedStance))
        {
            // Recalculate the water occlusion at the new point
            m_cachedWaterOcclusionValue = GetAISystem().GetWaterOcclusionValue(bodyInfo.vEyePos);

            m_vLastFullUpdatePos = bodyInfo.vEyePos;
            m_lastFullUpdateStance = bodyInfo.stance;
        }

        m_bUpdatedOnce = true;

        m_FOV = cosf(GetAISystem().GetAIDebugRenderer().GetCameraFOV());

        if (IsObserver())
            VisionChanged(75.0f, m_FOV, m_FOV);

        // (MATT) I'm assuming that AIActor should always have a proxy, or this could be bad for performance {2009/04/03}
        IAIActorProxy pProxy = GetProxy();
        if (pProxy != null)
            pProxy.Update(m_State, true);

        if (type == EObjectUpdate.AIUPDATE_FULL)
        {
            m_lightLevel = GetAISystem().GetLightManager()?.GetLightLevelAt(GetPos(), this, ref m_usingCombatLight) ?? EAILightLevel.AILL_LIGHT;

#if ENABLE_MISSLOCATION_SENSOR
            m_pMissLocationSensor?.Update(0.005f);
#endif

#if CRYAISYSTEM_DEBUG
            // Health
            {
                IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(GetProxy().GetActorHealth());
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_HEALTH, ref recorderEventData);
            }

            // Pos
            {
                IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(GetPos());
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_AGENTPOS, ref recorderEventData);
            }

            // Dir
            {
                IAIRecordable.RecorderEventData recorderEventData = new IAIRecordable.RecorderEventData(GetViewDir());
                RecordEvent(IAIRecordable.e_AIDbgEvent.E_AGENTDIR, ref recorderEventData);
            }
#endif // CRYAISYSTEM_DEBUG
        }

        float dt = GetAISystem().GetFrameDeltaTime();

        if (m_coolMissCooldown > 0.0f)
            m_coolMissCooldown -= dt;

        // Exposed (soft) covers. When the player fires the weapon,
        // disable the cover where the player is hiding at or the
        // soft cover the player is shooting at.

        // Timeout the cover exposure timer.
        if (m_coverExposedTime > 0.0f)
            m_coverExposedTime -= dt;
        else
            m_coverExposedTime = -1.0f;

        if (GetProxy() != null)
        {
            SAIWeaponInfo wi = new SAIWeaponInfo();
            GetProxy().QueryWeaponInfo(wi);
            if (wi.isFiring)
                m_coverExposedTime = 1.0f;
        }

        // Timeout the exposed covers
        for (int i = 0; i < m_exposedCoverObjects.Count; )
        {
            var co = m_exposedCoverObjects[i];
            co.t -= dt;
            if (co.t < 0.0f)
            {
                co.pPhysEnt?.Release();
                m_exposedCoverObjects[i] = m_exposedCoverObjects[m_exposedCoverObjects.Count - 1];
                m_exposedCoverObjects.RemoveAt(m_exposedCoverObjects.Count - 1);
            }
            else
            {
                m_exposedCoverObjects[i] = co;
                ++i;
            }
        }

        // Collect new covers.
        if (type == EObjectUpdate.AIUPDATE_FULL)
            CollectExposedCover();


#if CRYAISYSTEM_DEBUG
        if (gAIEnv.CVars.DebugDrawDamageControl > 0)
            UpdateHealthHistory();
#endif

        UpdatePlayerStuntActions();
        UpdateCloakScale();

        // Update low health mercy pause
        if (m_mercyTimer > 0.0f)
            m_mercyTimer -= GetAISystem().GetFrameDeltaTime();
        else
            m_mercyTimer = -1.0f;

        m_damagePartsUpdated = false;
    }

    // ===================================================================
    // OnObjectRemoved (AIPlayer.cpp lines 500-519)
    // ===================================================================
    public override void OnObjectRemoved(CAIObject pObject)
    {
        base.OnObjectRemoved(pObject);

        // (MATT) Moved here from CAISystems call {2009/02/05}
        CAIActor pRemovedAIActor = pObject.CastToCPuppet();
        if (pRemovedAIActor == null)
            return;

        for (int i = 0; i < m_stuntTargets.Count; )
        {
            if (m_stuntTargets[i].pAIActor == pRemovedAIActor)
            {
                m_stuntTargets[i] = m_stuntTargets[m_stuntTargets.Count - 1];
                m_stuntTargets.RemoveAt(m_stuntTargets.Count - 1);
            }
            else
                ++i;
        }
    }

    // ===================================================================
    // UpdatePlayerStuntActions (AIPlayer.cpp lines 521-690)
    // ===================================================================
    private void UpdatePlayerStuntActions()
    {
        ActorLookUp lookUp = gAIEnv.pActorLookUp;
        lookUp.Prepare(ActorLookUp.Position);

        float dt = GetAISystem().GetFrameDeltaTime();

        // Update thrown entities
        for (int i = 0; i < m_lastThrownItems.Count; )
        {
            IEntity pEnt = gEnv.pEntitySystem?.GetEntity(m_lastThrownItems[i].id);
            if (pEnt != null)
            {
                IPhysicalEntity pPhysEnt = pEnt.GetPhysics();
                if (pPhysEnt != null)
                {
                    pe_status_dynamics statDyn = new pe_status_dynamics();
                    pPhysEnt.GetStatus(statDyn);
                    var item = m_lastThrownItems[i];
                    item.pos = pEnt.GetWorldPos();
                    item.vel = statDyn.v;
                    if (statDyn.v.GetLengthSquared() > sqr(3.0f))
                        item.time = 0;
                    m_lastThrownItems[i] = item;
                }
            }

            {
                var item = m_lastThrownItems[i];
                item.time += dt;
                m_lastThrownItems[i] = item;
            }
            if (pEnt == null || m_lastThrownItems[i].time > 1.0f)
            {
                m_lastThrownItems[i] = m_lastThrownItems[m_lastThrownItems.Count - 1];
                m_lastThrownItems.RemoveAt(m_lastThrownItems.Count - 1);
            }
            else
                ++i;
        }

        Vec3 vel = GetVelocity();
        float speed = vel.Length();

        m_stuntDir = vel;
        if (m_stuntDir.Length() < 0.001f)
            m_stuntDir = GetEntityDir();
        m_stuntDir.Z = 0;
        m_stuntDir.NormalizeSafe();

        if (m_playerStuntSprinting > 0.0f)
        {
            if (speed > 10.0f)
                m_playerStuntSprinting = PLAYER_ACTION_SPRINT_RESET_TIME;
            m_playerStuntSprinting -= dt;
        }

        if (m_playerStuntJumping > 0.0f)
        {
            if (speed > 7.0f)
                m_playerStuntJumping = PLAYER_ACTION_JUMP_RESET_TIME;
            m_playerStuntJumping -= dt;
        }

        if (m_playerStuntCloaking > 0.0f)
            m_playerStuntCloaking -= dt;

        if (m_playerStuntUncloaking > 0.0f)
            m_playerStuntUncloaking -= dt;


        bool checkMovement = m_playerStuntSprinting > 0.0f || m_playerStuntJumping > 0.0f;
        bool checkItems = m_lastThrownItems.Count > 0;

        if (checkMovement || checkItems)
        {
            float movementScale = 0.7f;
            //		if (m_playerStuntJumping > 0.0f)
            //			movementScale *= 2.0f;

            Lineseg playerMovement = new Lineseg(GetPos(), GetPos() + vel * movementScale);
            SAIBodyInfo bi = GetBodyInfo();
            float playerRad = bi.stanceSize.GetRadius();

            // Update stunt effect on AI actors
            nuint activeCount = lookUp.GetActiveCount();

            for (uint actorIndex = 0; actorIndex < (uint)activeCount; ++actorIndex)
            {
                CAIActor pAIActor = lookUp.GetActor<CAIActor>(actorIndex);

                if (!IsHostile(pAIActor))
                    continue;

                float scale = pAIActor.GetParameters().m_PerceptionParams.collisionReactionScale;

                bool hit = false;

                float t = 0, distSq = 0;
                Vec3 threatPos = new Vec3(0, 0, 0);

                if (checkMovement)
                {
                    // Player movement
                    distSq = Distance_Point_Lineseg2DSq(lookUp.GetPosition(actorIndex), playerMovement, out t);
                    if (distSq < sqr(playerRad * scale))
                    {
                        threatPos = GetPos();
                        hit = true;
                    }
                }

                if (checkItems)
                {
                    // Thrown items
                    for (int i = 0, ni = m_lastThrownItems.Count; i < ni; ++i)
                    {
                        Lineseg itemMovement = new Lineseg(m_lastThrownItems[i].pos, m_lastThrownItems[i].pos + m_lastThrownItems[i].vel * 2);
                        distSq = Distance_Point_LinesegSq(lookUp.GetPosition(actorIndex), itemMovement, out t);
                        if (distSq < sqr(m_lastThrownItems[i].r * 2.0f * scale))
                        {
                            threatPos = m_lastThrownItems[i].pos;
                            hit = true;
                        }
                    }
                }

                if (hit)
                {
                    bool found = false;
                    for (int i = 0, ni = m_stuntTargets.Count; i < ni; ++i)
                    {
                        if (m_stuntTargets[i].pAIActor == pAIActor)
                        {
                            var st = m_stuntTargets[i];
                            st.threatPos = threatPos;
                            st.exposed += dt;
                            st.t = 0;
                            m_stuntTargets[i] = st;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        m_stuntTargets.Add(new SStuntTargetAIActor(pAIActor, threatPos));
                        var st = m_stuntTargets[m_stuntTargets.Count - 1];
                        st.exposed += dt;
                        m_stuntTargets[m_stuntTargets.Count - 1] = st;
                    }
                }
            }
        }

        for (int i = 0; i < m_stuntTargets.Count; )
        {
            var st = m_stuntTargets[i];
            st.t += dt;
            m_stuntTargets[i] = st;
            CAIActor pAIActor = m_stuntTargets[i].pAIActor;
            if (!m_stuntTargets[i].signalled
                    && m_stuntTargets[i].exposed > 0.15f
                    && pAIActor.GetAttentionTarget() != null
                    && pAIActor.GetAttentionTarget().GetEntityID() == GetEntityID())
            {
                IAISignalExtraData pData = GetAISystem().CreateSignalExtraData();
                ((AISignalExtraData)pData).iValue = 1;
                ((AISignalExtraData)pData).fValue = Distance_Point_Point(m_stuntTargets[i].pAIActor.GetPos(), m_stuntTargets[i].threatPos);
                ((AISignalExtraData)pData).point = m_stuntTargets[i].threatPos;
                pAIActor.SetSignal(1, "OnCloseCollision", null, pData);
                CPuppet pPuppet = pAIActor.CastToCPuppet();
                if (pPuppet != null)
                    pPuppet.SetAlarmed();
                st = m_stuntTargets[i];
                st.signalled = true;
                m_stuntTargets[i] = st;
            }
            if (m_stuntTargets[i].t > 2.0f)
            {
                m_stuntTargets[i] = m_stuntTargets[m_stuntTargets.Count - 1];
                m_stuntTargets.RemoveAt(m_stuntTargets.Count - 1);
            }
            else
                ++i;
        }
    }

    // ===================================================================
    // IsDoingStuntActionRelatedTo (AIPlayer.cpp lines 692-710)
    // ===================================================================
    public bool IsDoingStuntActionRelatedTo(Vec3 pos, float nearDistance)
    {
        if (m_playerStuntCloaking > 0.0f)
            return true;

        if (m_playerStuntSprinting <= 0.0f && m_playerStuntJumping <= 0.0f)
            return false;

        // If the stunt is not done at really close range,
        // do not consider the stunt unless it is towards the specified position.
        Vec3 diff = pos - GetPos();
        diff.Z = 0;
        float dist = diff.NormalizeSafe();
        float thr = cosf(DEG2RAD(75.0f));
        if (dist > nearDistance && m_stuntDir.Dot(diff) < thr)
            return false;

        return true;
    }

    // ===================================================================
    // IsThrownByPlayer (AIPlayer.cpp lines 712-719)
    // ===================================================================
    public bool IsThrownByPlayer(uint id)
    {
        if (m_lastThrownItems.Count == 0) return false;
        for (int i = 0, ni = m_lastThrownItems.Count; i < ni; ++i)
            if (m_lastThrownItems[i].id == id)
                return true;
        return false;
    }

    // ===================================================================
    // IsPlayerStuntAffectingTheDeathOf (AIPlayer.cpp lines 721-743)
    // ===================================================================
    public bool IsPlayerStuntAffectingTheDeathOf(CAIActor pDeadActor)
    {
        if (pDeadActor == null || pDeadActor.GetEntity() == null)
            return false;

        // If the actor is thrown/punched by the player.
        if (IsThrownByPlayer(pDeadActor.GetEntityID()))
            return true;

        // If any of the objects the player has thrown is close to the dead body.
        pDeadActor.GetEntity().GetWorldBounds(out AABB deadBounds);
        Vec3 deadPos = deadBounds.GetCenter();
        float deadRadius = deadBounds.GetRadius();

        for (int i = 0, ni = m_lastThrownItems.Count; i < ni; ++i)
        {
            if (Distance_Point_PointSq(deadPos, m_lastThrownItems[i].pos) < sqr(deadRadius + m_lastThrownItems[i].r))
                return true;
        }

        return false;
    }

    // ===================================================================
    // GetNearestThrownEntity (AIPlayer.cpp lines 745-759)
    // ===================================================================
    public uint GetNearestThrownEntity(Vec3 pos)
    {
        uint nearest = 0;
        float nearestDist = float.MaxValue;
        for (int i = 0, ni = m_lastThrownItems.Count; i < ni; ++i)
        {
            float d = Distance_Point_Point(pos, m_lastThrownItems[i].pos) - m_lastThrownItems[i].r;
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = m_lastThrownItems[i].id;
            }
        }
        return nearest;
    }

    // ===================================================================
    // AddThrownEntity (AIPlayer.cpp lines 761-826)
    // ===================================================================
    private void AddThrownEntity(uint id)
    {
        float oldestTime = 0.0f;
        int oldestId = 0;
        for (int i = 0, ni = m_lastThrownItems.Count; i < ni; ++i)
        {
            if (m_lastThrownItems[i].id == id)
            {
                var item = m_lastThrownItems[i];
                item.time = 0;
                m_lastThrownItems[i] = item;
                return;
            }
            if (m_lastThrownItems[i].time > oldestTime)
            {
                oldestTime = m_lastThrownItems[i].time;
                oldestId = i;
            }
        }

        IEntity pEnt = gEnv.pEntitySystem?.GetEntity(id);
        if (pEnt == null)
            return;

        // The entity does not exists yet, add it to the list of entities to watch.
        m_lastThrownItems.Add(new SThrownItem(id));

        // Skip the nearest thrown entity, since it is potentially blocking the view to the corpse.
        IEntity pThrownEnt = id != 0 ? gEnv.pEntitySystem?.GetEntity(id) : null;
        CAIActor pThrownActor = pThrownEnt != null ? CastToCAIActorSafe(pThrownEnt.GetAI()) : null;
        if (pThrownActor != null)
        {
            short gid = (short)pThrownActor.GetGroupId();
            // Iterate m_mapGroups for this group
            if (GetAISystem().m_mapGroups.ContainsKey(gid))
            {
                var list = GetAISystem().m_mapGroups[gid];
                foreach (var weakRef in list)
                {
                    CPuppet pPuppet = weakRef.GetAIObject()?.CastToCPuppet();
                    if (pPuppet == null) continue;
                    if (pPuppet.GetEntityID() == pThrownActor.GetEntityID()) continue;
                    float dist = float.MaxValue;
                    if (!GetAISystem().CheckVisibilityToBody(pPuppet, pThrownActor, ref dist))
                        continue;
                    pPuppet.SetSignal(1, "OnGroupMemberMutilated", pThrownActor.GetEntity(), null);
                    pPuppet.SetAlarmed();
                }
            }
        }

        // Set initial position, radius and velocity.
        {
            var item = m_lastThrownItems[m_lastThrownItems.Count - 1];
            item.pos = pEnt.GetWorldPos();
            pEnt.GetWorldBounds(out AABB bounds);
            item.r = bounds.GetRadius();
            m_lastThrownItems[m_lastThrownItems.Count - 1] = item;
        }

        {
            IPhysicalEntity pPhysEnt = pEnt.GetPhysics();
            if (pPhysEnt != null)
            {
                pe_status_dynamics statDyn = new pe_status_dynamics();
                pPhysEnt.GetStatus(statDyn);
                var item = m_lastThrownItems[m_lastThrownItems.Count - 1];
                item.vel = statDyn.v;
                m_lastThrownItems[m_lastThrownItems.Count - 1] = item;
            }
        }

        // Limit the number of hot entities.
        if (m_lastThrownItems.Count > 4)
        {
            m_lastThrownItems[oldestId] = m_lastThrownItems[m_lastThrownItems.Count - 1];
            m_lastThrownItems.RemoveAt(m_lastThrownItems.Count - 1);
        }
    }

    // ===================================================================
    // HandleArmoredHit (AIPlayer.cpp lines 828-831)
    // ===================================================================
    private void HandleArmoredHit()
    {
        NotifyPlayerActionToTheLookingAgents("OnTargetArmoredHit");
    }

    // ===================================================================
    // HandleCloaking (AIPlayer.cpp lines 833-840)
    // ===================================================================
    private void HandleCloaking(bool cloak)
    {
        CAISystem pAISystem = GetAISystem();

        m_Parameters.m_bCloaked = cloak;
        m_Parameters.m_fLastCloakEventTime = pAISystem.GetFrameStartTime().GetSeconds();
        NotifyPlayerActionToTheLookingAgents(cloak ? "OnTargetCloaked" : "OnTargetUncloaked");
    }

    // ===================================================================
    // HandleStampMelee (AIPlayer.cpp lines 842-846)
    // ===================================================================
    private void HandleStampMelee()
    {
        if (!m_Parameters.m_bCloaked)
            NotifyPlayerActionToTheLookingAgents("OnTargetStampMelee");
    }

    // ===================================================================
    // NotifyPlayerActionToTheLookingAgents (AIPlayer.cpp lines 850-894)
    // ===================================================================
    public void NotifyPlayerActionToTheLookingAgents(string eventName)
    {
        CAISystem pAISystem = GetAISystem();
        ActorLookUp lookUp = gAIEnv.pActorLookUp;
        nuint activeCount = lookUp.GetActiveCount();

        // Notify AI who have the player as their target
        uint playerId = GetEntityID();
        for (uint actorIndex = 0; actorIndex < (uint)activeCount; ++actorIndex)
        {
            CAIActor pAIActor = lookUp.GetActor<CAIActor>(actorIndex);

            CAIObject pTarget = pAIActor != null ? (CAIObject)pAIActor.GetAttentionTarget() : null;
            if (pTarget != null)
            {
                bool targetIsPlayer = (pTarget.GetEntityID() == playerId);
                VisionID targetVisionId = GetVisionID(); // This is the player vision id
                if (!targetIsPlayer)
                {
                    // Test association
                    CWeakRef<CAIObject> refAssociation = pTarget.GetAssociation();
                    CAIObject pAssociation = (refAssociation.IsValid() ? refAssociation.GetAIObject() : null);
                    targetIsPlayer = (pAssociation != null && pAssociation.GetEntityID() == playerId);
                    if (targetIsPlayer)
                    {
                        targetVisionId = pAssociation.GetVisionID();
                    }
                }

                if (targetIsPlayer)
                {
                    bool bCanSeeTarget = pAIActor.CanSee(targetVisionId);
                    if (bCanSeeTarget)
                    {
                        AISignalExtraData pData = new AISignalExtraData();
                        pData.nID = playerId;
                        pData.iValue = (int)pAIActor.GetAttentionTargetType();
                        pData.iValue2 = (int)pAIActor.GetAttentionTargetThreat();
                        pData.fValue = Distance_Point_Point(pAIActor.GetPos(), GetPos());
                        pAISystem.SendSignal(SIGNALFILTER.SIGNALFILTER_SENDER, 1, eventName, pAIActor, pData);
                    }
                }
            }
        }
    }

    // ===================================================================
    // Event (AIPlayer.cpp lines 898-970)
    // ===================================================================
    public override void Event(ushort eType, SAIEVENT pEvent)
    {
        switch ((EAIEvent)eType)
        {
            case AIEVENT_AGENTDIED:
                // make sure everybody knows I have died
                GetAISystem().NotifyTargetDead(this);
                m_bEnabled = false;
                GetAISystem().RemoveFromGroup(GetGroupId(), this);

                GetAISystem().ReleaseFormationPoint(this);
                ReleaseFormation();

                m_State.ClearSignals();

                if (m_proxy != null)
                    m_proxy.Reset(EObjectResetType.AIOBJRESET_SHUTDOWN);

                SetObservable(false);
                break;
            case AIEVENT_PLAYER_STUNT_SPRINT:
                m_playerStuntSprinting = PLAYER_ACTION_SPRINT_RESET_TIME;
                m_playerStuntJumping = -1.0f;
                break;
            case AIEVENT_PLAYER_STUNT_JUMP:
                m_playerStuntJumping = PLAYER_ACTION_JUMP_RESET_TIME;
                m_playerStuntSprinting = -1.0f;
                break;
            case AIEVENT_PLAYER_STUNT_PUNCH:
                if (pEvent != null)
                    AddThrownEntity(pEvent.targetEntityID);
                break;
            case AIEVENT_PLAYER_STUNT_THROW:
                if (pEvent != null)
                    AddThrownEntity(pEvent.targetEntityID);
                break;
            case AIEVENT_PLAYER_STUNT_THROW_NPC:
                if (pEvent != null)
                    AddThrownEntity(pEvent.targetEntityID);
                break;
            case AIEVENT_PLAYER_THROW:
                if (pEvent != null)
                    AddThrownEntity(pEvent.targetEntityID);
                break;
            case AIEVENT_PLAYER_STUNT_CLOAK:
                m_playerStuntCloaking = PLAYER_ACTION_CLOAK_RESET_TIME;
                HandleCloaking(true);
                break;
            case AIEVENT_PLAYER_STUNT_UNCLOAK:
                m_playerStuntUncloaking = PLAYER_ACTION_CLOAK_RESET_TIME;
                HandleCloaking(false);
                break;
            case AIEVENT_PLAYER_STUNT_ARMORED:
                HandleArmoredHit();
                break;
            case AIEVENT_PLAYER_STAMP_MELEE:
                HandleStampMelee();
                break;
            case AIEVENT_LOWHEALTH:
                {
                    float scale = (pEvent != null) ? pEvent.fThreat : 1.0f;
                    m_mercyTimer = gAIEnv.CVars.RODLowHealthMercyTime * scale;
                }
                break;
            case AIEVENT_ENABLE:
                SetObservable(true);
                base.Event(eType, pEvent);
                break;
            default:
                base.Event(eType, pEvent);
                break;
        }
    }

    // ===================================================================
    // GetDamageParts (AIPlayer.cpp lines 974-983)
    // ===================================================================
    public override DamagePartVector GetDamageParts()
    {
        if (!m_damagePartsUpdated)
        {
            UpdateDamageParts(m_damageParts);
            m_damagePartsUpdated = true;
        }

        return m_damageParts;
    }

    // ===================================================================
    // RecordSnapshot (AIPlayer.cpp lines 987-990)
    // ===================================================================
    public new void RecordSnapshot()
    {
        // Currently not used
    }

    // ===================================================================
    // RecordEvent (AIPlayer.cpp lines 994-1003)
    // ===================================================================
    public override void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData pEventData)
    {
#if CRYAISYSTEM_DEBUG
        CRecorderUnit pRecord = (CRecorderUnit)GetAIDebugRecord();
        if (pRecord != null)
        {
            pRecord.RecordEvent(eventArg, ref pEventData);
        }
#endif //CRYAISYSTEM_DEBUG
    }

    // ===================================================================
    // GetMissLocation (AIPlayer.cpp lines 1005-1013)
    // ===================================================================
    public bool GetMissLocation(Vec3 shootPos, Vec3 shootDir, float maxAngle, out Vec3 pos)
    {
        pos = new Vec3(0, 0, 0);
#if ENABLE_MISSLOCATION_SENSOR
        if (m_coolMissCooldown <= 0.000001f)
            return m_pMissLocationSensor.GetLocation(this, shootPos, shootDir, maxAngle, out pos);
#endif

        return false;
    }

    // ===================================================================
    // NotifyMissLocationConsumed (AIPlayer.cpp lines 1015-1018)
    // ===================================================================
    public void NotifyMissLocationConsumed()
    {
        m_coolMissCooldown += gAIEnv.CVars.CoolMissesCooldown;
    }

    // ===================================================================
    // DebugDraw (AIPlayer.cpp lines 1022-1131)
    // ===================================================================
    public void DebugDraw()
    {
        CDebugDrawContext dc = new CDebugDrawContext();

        // Draw items associated with player actions.
        for (int i = 0, ni = m_lastThrownItems.Count; i < ni; ++i)
        {
            //		IEntity pEnt = gEnv.pEntitySystem.GetEntity(m_lastThrownItems[i].id);
            //		if (pEnt)
            {
                AABB bounds = new AABB(AABB.RESET);
                bounds.Add(m_lastThrownItems[i].pos, m_lastThrownItems[i].r);
                dc.DrawAABB(bounds, false, new ColorB(255, 0, 0), EBoundingBoxDrawStyle.eBBD_Faceted);
                bounds.Move(m_lastThrownItems[i].vel);
                dc.DrawLine(m_lastThrownItems[i].pos, new ColorB(255, 0, 0), m_lastThrownItems[i].pos + m_lastThrownItems[i].vel, new ColorB(255, 0, 0, 128));
                dc.DrawAABB(bounds, false, new ColorB(255, 0, 0, 128), EBoundingBoxDrawStyle.eBBD_Faceted);
            }
        }

        for (int i = 0, ni = m_stuntTargets.Count; i < ni; ++i)
        {
            SAIBodyInfo bodyInfoStunt = m_stuntTargets[i].pAIActor.GetBodyInfo();
            Vec3 stPos = m_stuntTargets[i].pAIActor.GetPhysicsPos();
            AABB aabb = bodyInfoStunt.stanceSize;
            aabb.Move(stPos);
            dc.DrawAABB(aabb, true, new ColorB(255, 255, 255, (uint8)(m_stuntTargets[i].signalled ? 128 : 48)), EBoundingBoxDrawStyle.eBBD_Faceted);
        }

        ColorB color = new ColorB(255, 255, 255);

        // Draw special player actions
        if (m_playerStuntSprinting > 0.0f)
        {
            Vec3 pos = GetPos();
            Vec3 vel = GetVelocity();
            SAIBodyInfo bi = GetBodyInfo();
            float r = bi.stanceSize.GetRadius();
            AABB bounds = new AABB(AABB.RESET);
            bounds.Add(pos, r);
            dc.DrawAABB(bounds, false, new ColorB(255, 0, 0), EBoundingBoxDrawStyle.eBBD_Faceted);
            bounds.Move(vel);
            dc.DrawLine(pos, new ColorB(255, 0, 0), pos + vel, new ColorB(255, 0, 0, 128));
            dc.DrawAABB(bounds, false, new ColorB(255, 0, 0, 128), EBoundingBoxDrawStyle.eBBD_Faceted);

            // [2/27/2009 evgeny] Here and below in this method,
            // first argument for Draw2dLabel was 10, not 100, and the text was hardly visible
            dc.Draw2dLabel(100, 10, 2.5f, color, true, "SPRINTING");
        }
        if (m_playerStuntJumping > 0.0f)
        {
            Vec3 pos = GetPos();
            Vec3 vel = GetVelocity();
            SAIBodyInfo bi = GetBodyInfo();
            float r = bi.stanceSize.GetRadius();
            AABB bounds = new AABB(AABB.RESET);
            bounds.Add(pos, r);
            dc.DrawAABB(bounds, false, new ColorB(255, 0, 0), EBoundingBoxDrawStyle.eBBD_Faceted);
            bounds.Move(vel);
            dc.DrawLine(pos, new ColorB(255, 0, 0), pos + vel, new ColorB(255, 0, 0, 128));
            dc.DrawAABB(bounds, false, new ColorB(255, 0, 0, 128), EBoundingBoxDrawStyle.eBBD_Faceted);

            dc.Draw2dLabel(100, 40, 2.5f, color, true, "JUMPING");
        }
        if (m_playerStuntCloaking > 0.0f)
        {
            dc.Draw2dLabel(100, 70, 2.5f, color, true, "CLOAKING");
        }
        if (m_playerStuntUncloaking > 0.0f)
        {
            dc.Draw2dLabel(100, 110, 2.5f, color, true, "UNCLOAKING");
        }
        if (m_lastThrownItems.Count > 0)
        {
            dc.Draw2dLabel(100, 150, 2.5f, color, true, "THROWING");
        }

        if (IsLowHealthPauseActive())
        {
            ICVar pLowHealth = gEnv.pConsole?.GetCVar("g_playerLowHealthThreshold");
            dc.Draw2dLabel(150, 190, 2.0f, color, true, "Mercy {0:F2}/{1:F2} (when below {2:F2})", m_mercyTimer, gAIEnv.CVars.RODLowHealthMercyTime, pLowHealth?.GetFVal() ?? 0.0f);
        }

        for (int i = 0, ni = m_exposedCoverObjects.Count; i < ni; ++i)
        {
            pe_status_pos statusPos = new pe_status_pos();
            m_exposedCoverObjects[i].pPhysEnt?.GetStatus(statusPos);
            AABB bounds = new AABB(AABB.RESET);
            bounds.Add(statusPos.BBox[0] + statusPos.pos);
            bounds.Add(statusPos.BBox[1] + statusPos.pos);
            dc.DrawAABB(bounds, false, new ColorB(255, 0, 0), EBoundingBoxDrawStyle.eBBD_Faceted);
            dc.Draw3dLabel(bounds.GetCenter(), 1.1f, "IGNORED {0:F1}s", m_exposedCoverObjects[i].t);
        }
    }

    // ===================================================================
    // IsLowHealthPauseActive (AIPlayer.cpp lines 1135-1140)
    // ===================================================================
    public override bool IsLowHealthPauseActive()
    {
        if (m_mercyTimer > 0.0f)
            return true;
        return false;
    }

    // ===================================================================
    // GetGrabbedEntity (AIPlayer.cpp lines 1144-1149)
    // ===================================================================
    public override IEntity GetGrabbedEntity()
    {
        IAIActorProxy pProxy = GetProxy();

        return pProxy != null ? pProxy.GetGrabbedEntity() : null;
    }

    // ===================================================================
    // IsGrabbedEntityInView (AIPlayer.cpp lines 1153-1177)
    // ===================================================================
    public override bool IsGrabbedEntityInView(Vec3 pos)
    {
        bool bInViewDist = true;

        IEntitySystem pEntitySystem = gEnv.pEntitySystem;
        System.Diagnostics.Debug.Assert(pEntitySystem != null);

        IEntity pObjectEntity = GetGrabbedEntity();

        IComponentRender pObjectRenderComponent = (pObjectEntity != null ? pObjectEntity.GetComponent<IComponentRender>() : null);
        if (pObjectRenderComponent != null)
        {
            IRenderNode pObjectRenderNode = pObjectRenderComponent.GetRenderNode();
            if (pObjectRenderNode != null)
            {
                float fDistanceSq = (pos - pObjectEntity.GetWorldPos()).GetLengthSquared();
                float fMinDist = 4.0f;
                float fMaxViewDistSq = sqr(MathF.Max(pObjectRenderNode.GetMaxViewDist(), fMinDist));

                bInViewDist = (fDistanceSq <= fMaxViewDistSq);
            }
        }

        return bInViewDist;
    }

    // ===================================================================
    // Serialize (AIPlayer.cpp lines 1181-1198)
    // ===================================================================
    public override void Serialize(TSerialize ser)
    {
        ser.BeginGroup("AIPlayer");

        base.Serialize(ser);

        ser.Value("m_fLastUpdateTargetTime", ref m_fLastUpdateTargetTime);
        ser.Value("m_FOV", ref m_FOV);

        ser.Value("m_playerStuntSprinting", ref m_playerStuntSprinting);
        ser.Value("m_playerStuntJumping", ref m_playerStuntJumping);
        ser.Value("m_playerStuntCloaking", ref m_playerStuntCloaking);
        ser.Value("m_playerStuntUncloaking", ref m_playerStuntUncloaking);
        ser.Value("m_stuntDir", ref m_stuntDir);
        ser.ValueWithDefault("m_mercyTimer", ref m_mercyTimer, -1.0f);

        ser.EndGroup();
    }

    // ===================================================================
    // Inline accessors
    // ===================================================================
    public int GetDeathCount() { return m_deathCount; }
    public void IncDeathCount() { m_deathCount++; }

    public override void UpdateProxy(EObjectUpdate type) { }

    // ===================================================================
    // Private fields
    // ===================================================================
    private CAIPlayer(CAIPlayer src) { } // disallow copies

    private uint m_lastGrabbedEntityID;

    private CTimeValue m_fLastUpdateTargetTime;
    private float m_FOV;
    private DamagePartVector m_damageParts = new DamagePartVector();
    private bool m_damagePartsUpdated;
    private int m_deathCount;

    private struct SThrownItem
    {
        public SThrownItem(uint id) { this.id = id; time = 0.0f; pos = new Vec3(0, 0, 0); vel = new Vec3(0, 0, 0); r = 0.1f; }
        public static bool operator <(SThrownItem lhs, SThrownItem rhs) { return lhs.time < rhs.time; }
        public static bool operator >(SThrownItem lhs, SThrownItem rhs) { return lhs.time > rhs.time; }
        public float time;
        public Vec3 pos, vel;
        public float r;
        public uint id;
    }
    private List<SThrownItem> m_lastThrownItems = new List<SThrownItem>();

    private struct SStuntTargetAIActor
    {
        public SStuntTargetAIActor(CAIActor pAIActor, Vec3 pos) { this.pAIActor = pAIActor; t = 0; exposed = 0; signalled = false; threatPos = pos; }
        public CAIActor pAIActor;
        public Vec3 threatPos;
        public float t;
        public float exposed;
        public bool signalled;
    }
    private List<SStuntTargetAIActor> m_stuntTargets = new List<SStuntTargetAIActor>();

    private Vec3 m_stuntDir;

    private float m_playerStuntSprinting;
    private float m_playerStuntJumping;
    private float m_playerStuntCloaking;
    private float m_playerStuntUncloaking;

    private float m_mercyTimer;

    private float m_coverExposedTime;
    private float m_coolMissCooldown;

    private struct SExposedCoverObject
    {
        public SExposedCoverObject(IPhysicalEntity pPhysEnt, float t) { this.pPhysEnt = pPhysEnt; this.t = t; }
        public IPhysicalEntity pPhysEnt;
        public float t;
    }

    private struct ExposedCoverState
    {
        public ExposedCoverState(int dummy = 0) { asyncState = AsyncState.AsyncReady; rayID = new QueuedRayID(); }
        public AsyncState asyncState;
        public QueuedRayID rayID;
    }

    private List<SExposedCoverObject> m_exposedCoverObjects = new List<SExposedCoverObject>();
    private ExposedCoverState m_exposedCoverState;

#if ENABLE_MISSLOCATION_SENSOR
    private CMissLocationSensor m_pMissLocationSensor;
#endif

    // ===================================================================
    // Helper statics for Distance functions (literal C++ free functions)
    // ===================================================================
    private static float Distance_Point_Point(Vec3 a, Vec3 b) { return (a - b).Length(); }
    private static float Distance_Point_PointSq(Vec3 a, Vec3 b) { return (a - b).GetLengthSquared(); }

    private static float Distance_Point_LinesegSq(Vec3 p, Lineseg ls, out float t)
    {
        Vec3 diff = p - ls.start;
        Vec3 dir = ls.end - ls.start;
        float fT = diff.Dot(dir);
        float lenSq = dir.GetLengthSquared();
        if (lenSq > 0.0f)
            fT /= lenSq;
        fT = clamp_tpl(fT, 0.0f, 1.0f);
        t = fT;
        Vec3 closest = ls.start + dir * fT;
        return (p - closest).GetLengthSquared();
    }

    private static float Distance_Point_Lineseg2DSq(Vec3 p, Lineseg ls, out float t)
    {
        Vec3 p2 = new Vec3(p.x, p.y, 0);
        Vec3 s2 = new Vec3(ls.start.x, ls.start.y, 0);
        Vec3 e2 = new Vec3(ls.end.x, ls.end.y, 0);
        return Distance_Point_LinesegSq(p2, new Lineseg(s2, e2), out t);
    }

    // Physics constants used by CollectExposedCover — literal C++ #defines from physinterface.h
    private const int rwi_colltype_any = 0x0400;
    private const int geom_colltype_obstruct = 0x100;
    private const int rwi_colltype_bit = 16;
    private const int VIEW_RAY_PIERCABILITY = 10;
    private const int rwi_pierceability_mask = 0x0F;
    private const int ent_static = 1;
}

// Forward decl for CMissLocationSensor — Phase 5 (Perception)
public class CMissLocationSensor
{
    private CAIPlayer m_pOwner;
    public CMissLocationSensor(CAIPlayer owner = null) { m_pOwner = owner; }
    public void Reset() { /* impl pending Phase 5 */ }
    public void DebugDraw() { /* impl pending Phase 5 */ }
    public void Update(float dt) { /* impl pending Phase 5 */ }
    public bool GetLocation(CAIPlayer player, Vec3 shootPos, Vec3 shootDir, float maxAngle, out Vec3 pos) { pos = new Vec3(0, 0, 0); return false; /* impl pending Phase 5 */ }
}

// IComponentRender shell — used by IsGrabbedEntityInView
public interface IComponentRender
{
    IRenderNode GetRenderNode();
}

// IRenderNode shell
public interface IRenderNode
{
    float GetMaxViewDist();
    // Added for CAISystemUpdate.cpp literal port — IsPuppetOnScreen
    int GetDrawFrame(int nRecursionLevel = 0) { return 0; }
}

// CastToCAIPlayerSafe — inline helper from AIPlayer.h lines 166-167
public static class CAIPlayerCastHelper
{
    public static CAIPlayer CastToCAIPlayerSafe(IAIObject pAI) { return pAI?.CastToCAIPlayer(); }
    public static CAIPlayer CastToCAIPlayerSafe_Mut(IAIObject pAI) { return pAI?.CastToCAIPlayer(); }
}

// SIGNALFILTER enum — literal from IAgent.h (also declared in AIFlyingVehicle.cs, referencing same values)
// Use fully qualified SIGNALFILTER.SIGNALFILTER_SENDER when calling SendSignal.
