// Literal port of dev/Code/CryEngine/CryAISystem/Puppet.h (declarations).
// Puppet.cpp impl (5343L) deferred — see deferred.md.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

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

    public CPuppet() { /* impl in .cpp */ }
    // virtual ~CPuppet();

    public static void ClearStaticData() { /* impl in .cpp */ }

    public virtual IPuppet CastToIPuppet() { return null; /* this in C++ */ }

    public PostureManager GetPostureManager() { return m_postureManager; }

    public override void ResetPerception() { /* impl in .cpp */ }

    public override bool CanDamageTarget(IAIObject target = null) { return false; /* impl in .cpp */ }
    public override bool CanDamageTargetWithMelee() { return false; /* impl in .cpp */ }

    public void AdjustSpeed(CAIObject pNavTarget, float distance = 0) { /* impl in .cpp */ }
    public void ResetSpeedControl() { /* impl in .cpp */ }
    public override bool GetValidPositionNearby(Vec3 proposedPosition, out Vec3 adjustedPosition) { adjustedPosition = proposedPosition; return false; /* impl in .cpp */ }
    public override bool GetTeleportPosition(out Vec3 teleportPos) { teleportPos = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public bool GetPosAlongPath(float dist, bool extrapolateBeyond, out Vec3 retPos) { retPos = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public virtual IFireCommandHandler GetFirecommandHandler() { return m_pFireCmdHandler; }

    public void SetAllowedToHitTarget(bool state) { m_allowedToHitTarget = state; }
    public bool IsAllowedToHitTarget() { return m_allowedToHitTarget; }

    public void SetAllowedToUseExpensiveAccessory(bool state) { m_allowedToUseExpensiveAccessory = state; }
    public bool IsAllowedToUseExpensiveAccessory() { return m_allowedToUseExpensiveAccessory; }

    public CAIObject GetFireTargetObject() { return null; /* impl in .cpp */ }

    public float GetFiringReactionTime(Vec3 targetPos) { return 0; /* impl in .cpp */ }

    public float GetCurrentFiringReactionTime() { return m_firingReactionTime; }
    public bool HasFiringReactionTimePassed() { return m_firingReactionTimePassed; }

    public void GetShootingStatus(out SShootingStatus ss) { ss = new SShootingStatus(); /* impl in .cpp */ }

    public new void SetAllowedStrafeDistances(float start, float end, bool whileMoving) { /* impl in .cpp */ }

    public void SetAdaptiveMovementUrgency(float minUrgency, float maxUrgency, float scaleDownPathlen) { /* impl in .cpp */ }

    public void SetDelayedStance(int stance) { /* impl in .cpp */ }

    public void AdjustMovementUrgency(ref float urgency, float pathLength, ref float maxPathLen) { /* impl in .cpp */ }

    public CPathObstacles GetPathAdjustmentObstacles(bool allowRecalc = true) { if (allowRecalc) CalculatePathObstacles(); return m_pathAdjustmentObstacles; }
    public CPathObstacles GetLastPathAdjustmentObstacles() { return m_pathAdjustmentObstacles; }

    public float GetAccuracy(CAIObject pTarget) { return 0; /* impl in .cpp */ }

    public override DamagePartVector GetDamageParts() { return m_damageParts; }

    public nuint MemStats() { return 0; /* impl in .cpp */ }

    public virtual void UpTargetPriority(IAIObject pTarget, float fPriorityIncrement) { /* impl in .cpp */ }
    public virtual void UpdateBeacon() { /* impl in .cpp */ }
    public virtual IAIObject MakeMeLeader() { return null; /* impl in .cpp */ }
    public virtual bool CheckFriendsInLineOfFire(Vec3 fireDir, bool cheapTest) { return false; /* impl in .cpp */ }

    public bool GetSoundPerceptionDescriptor(EAISoundStimType eType, out SSoundPerceptionDescriptor sDescriptor) { sDescriptor = new SSoundPerceptionDescriptor(); return false; /* impl in .cpp */ }
    public bool SetSoundPerceptionDescriptor(EAISoundStimType eType, SSoundPerceptionDescriptor sDescriptor) { return false; /* impl in .cpp */ }

    public virtual IAIObject GetEventOwner(IAIObject pObject) { return null; /* impl in .cpp */ }
    public virtual CAIObject GetEventOwner(CWeakRef<CAIObject> refOwned) { return null; /* impl in .cpp */ }

    public bool CheckAndGetFireTarget_Deprecated(IAIObject pTarget, bool lowDamage, out Vec3 vTargetPos, out Vec3 vTargetDir) { vTargetPos = new Vec3(0, 0, 0); vTargetDir = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public virtual Vec3 ChooseMissPoint_Deprecated(Vec3 targetPos) { return targetPos; /* impl in .cpp */ }

    public new void Update(EObjectUpdate type) { /* impl in .cpp */ }
    public override void UpdateProxy(EObjectUpdate type) { /* impl in .cpp */ }
    public virtual void Devalue(IAIObject pObject, bool bDevaluePuppets, float fAmount = 20.0f) { /* impl in .cpp */ }
    public override bool IsDevalued(IAIObject pObject) { return false; /* impl in .cpp */ }
    public override void ClearDevalued() { /* impl in .cpp */ }

    public override void SetParameters(AgentParameters sParams) { /* impl in .cpp */ }
    public override void Event(ushort eType, SAIEVENT pEvent) { /* impl in .cpp */ }
    public override void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true) { /* impl in .cpp */ }
    public override bool CreateFormation(string szName, Vec3 vTargetPos = default) { return false; /* impl in .cpp */ }
    public override void Serialize(TSerialize ser) { /* impl in .cpp */ }
    public override void PostSerialize() { /* impl in .cpp */ }
    public override void SetPFBlockerRadius(int blockerType, float radius) { /* impl in .cpp */ }

    public override void OnObjectRemoved(CAIObject pObject) { /* impl in .cpp */ }
    public override void Reset(EObjectResetType type) { /* impl in .cpp */ }
    public override void GetPathAgentNavigationBlockers(NavigationBlockers navigationBlockers, PathfindRequest pRequest) { /* impl in .cpp */ }

    public virtual float GetDistanceAlongPath(Vec3 pos, bool bInit) { return 0; /* impl in .cpp */ }

    public bool GetPotentialTargets(PotentialTargetMap targetMap) { return false; /* impl in .cpp */ }

    public uint GetBestTargets(uint[] targets, uint maxCount) { return 0; /* impl in .cpp */ }

    public bool AddAggressiveTarget(IAIObject pTarget) { return false; /* impl in .cpp */ }
    public bool SetTempTargetPriority(ETempTargetPriority priority) { return false; /* impl in .cpp */ }
    public bool UpdateTempTarget(Vec3 vPosition) { return false; /* impl in .cpp */ }
    public bool ClearTempTarget() { return false; /* impl in .cpp */ }
    public bool DropTarget(IAIObject pTarget) { return false; /* impl in .cpp */ }

    public virtual void SetRODHandler(IAIRateOfDeathHandler pHandler) { m_pRODHandler = pHandler; }
    public virtual void ClearRODHandler() { m_pRODHandler = null; }

    public virtual bool GetPerceivedTargetPos(IAIObject pTarget, out Vec3 vPos) { vPos = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }

    public override void UpdateLookTarget(CAIObject pTarget) { /* impl in .cpp */ }
    public virtual void UpdateLookTarget3D(CAIObject pTarget) { /* impl in .cpp */ }
    public override bool NavigateAroundObjects(Vec3 targetPos, bool fullUpdate) { return false; /* impl in .cpp */ }

    public void SetForcedNavigation(Vec3 vDirection, float fSpeed) { /* impl in .cpp */ }
    public void ClearForcedNavigation() { /* impl in .cpp */ }

    public void SetVehicleStickTarget(uint targetId) { m_vehicleStickTarget = targetId; }
    public uint GetVehicleStickTarget() { return m_vehicleStickTarget; }

    public virtual bool NavigateAroundAIObject(Vec3 targetPos, CAIObject obstacle, Vec3 predMyPos, Vec3 predObjectPos, bool steer, bool in3D) { return false; /* impl in .cpp */ }

    public virtual void SetCanBeShot(bool bCanBeShot) { m_bCanBeShot = bCanBeShot; }
    public virtual bool GetCanBeShot() { return m_bCanBeShot; }

    public virtual void SetMemoryFireType(EMemoryFireType eType) { m_eMemoryFireType = eType; }
    public virtual EMemoryFireType GetMemoryFireType() { return m_eMemoryFireType; }
    public virtual bool CanMemoryFire() { return false; /* impl in .cpp */ }

    public bool CanFireInStance(EStance stance, float fDistanceRatio = 0.9f) { return false; /* impl in .cpp */ }

    public void RequestThrowGrenade(ERequestedGrenadeType eGrenadeType, int iRegType) { /* impl in .cpp */ }

    public int GetAlertness() { return m_Alertness; }

    public override void CheckCloseContact(IAIObject pTarget, float fDistSq) { /* impl in .cpp */ }

    public override float AdjustTargetVisibleRange(CAIActor observer, float fVisibleRange) { return fVisibleRange; /* impl in .cpp */ }

    public EPuppetUpdatePriority GetUpdatePriority() { return m_updatePriority; }
    public void SetUpdatePriority(EPuppetUpdatePriority pri) { m_updatePriority = pri; }

    public AIWeaponDescriptor QueryCurrentWeaponDescriptor(bool bIsSecondaryFire = false, ERequestedGrenadeType prefGrenadeType = ERequestedGrenadeType.eRGT_ANY) { return m_CurrentWeaponDescriptor; /* impl in .cpp */ }
    public AIWeaponDescriptor GetCurrentWeaponDescriptor() { return m_CurrentWeaponDescriptor; }

    public bool CanAimWithoutObstruction(Vec3 vTargetPos) { return false; /* impl in .cpp */ }
    public bool CheckLineOfFire(Vec3 vTargetPos, float fDistance, float fSoftDistance, EStance stance = EStance.STANCE_NULL) { return false; /* impl in .cpp */ }

    public float GetTimeToNextShot() { return 0; /* impl in .cpp */ }

    public override void MakeIgnorant(bool bIgnorant) { m_bCanReceiveSignals = !bIgnorant; }

    public bool AddPerceptionHandlerModifier(IPerceptionHandlerModifier pModifier) { return false; /* impl in .cpp */ }
    public bool RemovePerceptionHandlerModifier(IPerceptionHandlerModifier pModifier) { return false; /* impl in .cpp */ }
    public bool GetPerceptionHandlerModifiers(TPerceptionHandlerModifiersVector outModifiers) { return false; /* impl in .cpp */ }
    public void DebugDrawPerceptionHandlerModifiers() { /* impl in .cpp */ }

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

    protected void CheckAwarenessPlayer() { /* impl in .cpp */ }

    protected SAIPotentialTarget AddEvent(CWeakRef<CAIObject> refObject, SAIPotentialTarget ed) { return null; /* impl in .cpp */ }

    protected void UpdatePuppetInternalState() { /* impl in .cpp */ }
    protected bool UpdateTargetSelection(STargetSelectionInfo targetSelectionInfo) { return false; /* impl in .cpp */ }
    protected bool GetTargetTrackBestTarget(out CWeakRef<CAIObject> refBestTarget, out SAIPotentialTarget pTargetInfo,
        out bool bCurrentTargetErased) { refBestTarget = new CWeakRef<CAIObject>(); pTargetInfo = null; bCurrentTargetErased = false; return false; /* impl in .cpp */ }

    protected override void HandleSoundEvent(SAIEVENT pEvent) { /* impl in .cpp */ }
    protected override void HandlePathDecision(MNMPathRequestResult result) { /* impl in .cpp */ }
    protected override void HandleVisualStimulus(SAIEVENT pEvent) { /* impl in .cpp */ }
    protected override void HandleBulletRain(SAIEVENT pEvent) { /* impl in .cpp */ }

    protected void UpdateAlertness() { /* impl in .cpp */ }
    protected void ResetAlertness() { /* impl in .cpp */ }

    protected bool SteerAroundVehicle(Vec3 targetPos, CAIObject obj, Vec3 predMyPos, Vec3 predObjectPos) { return false; /* impl in .cpp */ }
    protected bool SteerAroundPuppet(Vec3 targetPos, CAIObject obj, Vec3 predMyPos, Vec3 predObjectPos) { return false; /* impl in .cpp */ }
    protected bool SteerAround3D(Vec3 targetPos, CAIObject obj, Vec3 predMyPos, Vec3 predObjectPos) { return false; /* impl in .cpp */ }
    protected bool NavigateAroundObjectsInternal(Vec3 targetPos, Vec3 myPos, bool in3D, CAIObject obj) { return false; /* impl in .cpp */ }
    protected ENavInteraction NavigateAroundObjectsBasicCheck(CAIObject obj) { return ENavInteraction.NI_IGNORE; /* impl in .cpp */ }
    protected bool NavigateAroundObjectsBasicCheck(Vec3 targetPos, Vec3 myPos, bool in3D, CAIObject obj, float extraDist) { return false; /* impl in .cpp */ }

    protected bool IsSecondaryFireCommand() { return m_fireMode == EFireMode.FIREMODE_SECONDARY || m_fireMode == EFireMode.FIREMODE_SECONDARY_SMOKE; }
    protected bool IsMeleeFireCommand() { return m_fireMode == EFireMode.FIREMODE_MELEE || m_fireMode == EFireMode.FIREMODE_MELEE_FORCED; }

    protected void FireCommand(float updateTime) { /* impl in .cpp */ }
    protected void FireSecondary(CAIObject pTarget, ERequestedGrenadeType prefGrenadeType = ERequestedGrenadeType.eRGT_ANY) { /* impl in .cpp */ }
    protected void FireMelee(CAIObject pTarget) { /* impl in .cpp */ }

    protected bool CheckTargetInRange(ref Vec3 vTargetPos) { return false; /* impl in .cpp */ }

    protected Vec3 GetHidePoint(MultimapRangeHideSpots hidespots, float fSearchDistance, Vec3 hideFrom, int nMethod, bool bSameOk, float fMinDistance) { return new Vec3(0, 0, 0); /* impl in .cpp */ }

    protected bool Compromising(Vec3 pos, Vec3 dir, Vec3 hideFrom, Vec3 objectPos, Vec3 searchPos, bool bIndoor, bool bCheckVisibility) { return false; /* impl in .cpp */ }

    protected void RegisterTargetAwareness(float amount) { /* impl in .cpp */ }

    protected void UpdateTargetMovementState() { /* impl in .cpp */ }

    protected bool IsFriendInLineOfFire(CAIObject pFriend, Vec3 firePos, Vec3 fireDirection, bool cheapTest) { return false; /* impl in .cpp */ }

    protected void AdjustWithPrediction(CAIObject pTarget, ref Vec3 posOut) { /* impl in .cpp */ }

    protected bool ActorObstructingAim(CAIActor pActor, Vec3 firePos, Vec3 dir, Ray fireRay) { return false; /* impl in .cpp */ }

    protected void CreatePendingDeathReaction(int groupID, PendingDeathReaction pPendingDeathReaction) { /* impl in .cpp */ }

    protected CPersonalInterestManager GetPersonalInterestManager() { return null; /* impl in .cpp */ }

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

    // typedef std::vector<Vec3> TPointList;
    protected List<Vec3> m_InitialPath = new List<Vec3>();

    protected void UpdateHealthTracking() { /* impl in .cpp */ }

    public void ResetTargetTracking() { /* impl in .cpp */ }
    public Vec3 UpdateTargetTracking(CWeakRef<CAIObject> refTarget, Vec3 vTargetPos) { return vTargetPos; /* impl in .cpp */ }
    public void UpdateFireReactionTimer(Vec3 vTargetPos) { /* impl in .cpp */ }
    public void UpdateTargetZone(CWeakRef<CAIObject> refTarget) { /* impl in .cpp */ }

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
    protected void LineOfFireRayComplete(QueuedRayID rayID, RayCastResult result) { /* impl in .cpp */ }

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
    protected void FireTargetValidRayComplete(QueuedRayID rayID, RayCastResult result) { /* impl in .cpp */ }
    protected void QueueFireTargetValidRay(CAIObject targetObj, Vec3 firePos, Vec3 fireDir) { /* impl in .cpp */ }

    public bool AdjustFireTarget(CAIObject targetObject, Vec3 target, bool hit, float missExtraOffset, float clampAngle, out Vec3 posOut) { posOut = target; return false; /* impl in .cpp */ }
    public bool CalculateHitPointOnTarget(CAIObject targetObject, Vec3 target, float clampAngle, out Vec3 posOut) { posOut = target; return false; /* impl in .cpp */ }
    public bool CalculateMissPointOutsideTargetSilhouette(CAIObject targetObject, Vec3 target, float missExtraOffset, out Vec3 posOut) { posOut = target; return false; /* impl in .cpp */ }

    public bool IsFireTargetValid(Vec3 pos, CAIObject pTargetObject) { return false; /* impl in .cpp */ }
    public float GetCoverFireTime() { return 0; /* impl in .cpp */ }
    public float GetBurstFireDistanceScale() { return 1.0f; /* impl in .cpp */ }

    public float GetTargetAliveTime() { return 0; /* impl in .cpp */ }

    public Vec3 InterpolateLookOrAimTargetPos(Vec3 current, Vec3 target, float maxRate) { return target; /* impl in .cpp */ }

    public void HandleBurstFireInit() { /* impl in .cpp */ }
    public void HandleWeaponEffectBurstDrawFire(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire) { /* impl in .cpp */ }
    public void HandleWeaponEffectBurstSnipe(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire) { /* impl in .cpp */ }
    public void HandleWeaponEffectPanicSpread(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire) { /* impl in .cpp */ }
    public void HandleWeaponEffectAimSweep(CAIObject pTarget, ref Vec3 aimTarget, ref bool canFire) { /* impl in .cpp */ }

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

        public Vec3 ProjectVectorOnSilhouette(Vec3 vec) { return new Vec3(0, 0, 0); /* impl pending */ }
        public Vec3 IntersectSilhouettePlane(Vec3 from, Vec3 to) { return to; /* impl pending */ }

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

    public virtual void EnableFire(bool enable) { /* impl in .cpp */ }
    public virtual bool IsFireEnabled() { return m_fireDisabled == 0; /* impl in .cpp */ }

    public void EnableCoverFire(bool enable) { m_bCoverFireEnabled = enable; }
    public bool IsCoverFireEnabled() { return m_bCoverFireEnabled; }

    public void SetAvoidedVehicle(CWeakRef<CAIVehicle> refVehicle) { /* impl in .cpp */ }

    public void UpdateStrafing() { /* impl in .cpp */ }


    public class SAIFireTargetCache
    {
        public SAIFireTargetCache() { m_size = 0; m_head = 0; m_queries = 0; m_hits = 0; m_cachePos = new Vec3[CACHE_SIZE]; m_cacheDir = new Vec3[CACHE_SIZE]; m_cacheReqDist = new float[CACHE_SIZE]; m_cacheDist = new float[CACHE_SIZE]; }

        public float QueryCachedResult(Vec3 pos, Vec3 dir, float reqDist)
        {
            m_queries++;
            return -1.0f; /* impl in .cpp */
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

    public void SetAlarmed() { /* impl in .cpp */ }
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
public interface IFireCommandHandler { }
public class CFireCommandGrenade { }
public interface IPerceptionHandler { }
public interface IAIRateOfDeathHandler { }
public class SAIPotentialTarget { }
public class PotentialTargetMap { }
public class PendingDeathReaction { }
public class CPersonalInterestManager { }
// SHideSpot is a real literal port in HideSpot.cs.
public class AIWeaponDescriptor
{
    public AgentPerceptionParameters perceptionParams = new AgentPerceptionParameters();
}
public struct AgentPerceptionParameters
{
    public float minAlarmLevel;
    public SPerceptionScale perceptionScale;
    public float forgetfulnessTarget;
    public float forgetfulnessMemory;
    public struct SPerceptionScale { public float visual; public float audio; }
}
public class CAIRadialOccypancy { }
public class MapConstNodesDistance { }
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

// Add the silhouette helpers — Matrix33 ports come from CryPhysics.Sharp via aliases
