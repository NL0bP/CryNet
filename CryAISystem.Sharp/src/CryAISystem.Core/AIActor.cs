// Literal port of dev/Code/CryEngine/CryAISystem/AIActor.h (declarations + a few inline accessors).
// AIActor.cpp impl (2628L) deferred — see deferred.md.
// Original Copyright Crytek GMBH or its affiliates, used under license.

// Description : Header for the CAIActor class

using System.Collections.Generic;

namespace CryAISystem;

// Removed the redundant EObjectUpdate enum (already declared in AIObject.cs forward shells)

// Structure reflecting the physical entity parts.
public struct SAIDamagePart
{
    public SAIDamagePart(int dummy = 0)
    {
        pos = new Vec3(0, 0, 0);
        damageMult = 0f;
        volume = 0f;
        surfaceIdx = 0;
    }
    public void GetMemoryUsage(ICrySizer pSizer) { }
    public Vec3 pos;
    public float damageMult;
    public float volume;
    public int surfaceIdx;
}

// typedef std::vector<SAIDamagePart> DamagePartVector;
public class DamagePartVector : List<SAIDamagePart> { }
// typedef std::vector<IPerceptionHandlerModifier*> TPerceptionHandlerModifiersVector;
public class TPerceptionHandlerModifiersVector : List<IPerceptionHandlerModifier> { }


/*! Basic ai object class. Defines a framework that all puppets and points of interest later follow. */
public class CAIActor : CAIObject, IAIActor
{
    // friend class CAISystem;

    public CAIActor() { /* impl in .cpp */ }
    // virtual ~CAIActor();

    public virtual IAIActor CastToIAIActor() { return this; }

    public virtual bool CanDamageTarget(IAIObject target = null) { return false; /* impl in .cpp */ }
    public virtual bool CanDamageTargetWithMelee() { return false; /* impl in .cpp */ }

    public override IPhysicalEntity GetPhysics(bool bWantCharacterPhysics = false) { return null; /* impl in .cpp */ }

    public void SetBehaviorVariable(string variableName, bool value) { /* impl in .cpp */ }
    public bool GetBehaviorVariable(string variableName) { return false; /* impl in .cpp */ }

    public SelectionTree GetBehaviorSelectionTree() { return m_behaviorSelectionTree; }
    public SelectionVariables GetBehaviorSelectionVariables() { return m_behaviorSelectionVariables; }

    public void ResetBehaviorSelectionTree(EObjectResetType type) { /* impl in .cpp */ }
    public bool ProcessBehaviorSelectionTreeSignal(string signalName, uint signalCRC) { return false; /* impl in .cpp */ }
    public bool UpdateBehaviorSelectionTree() { return false; /* impl in .cpp */ }

    public void ResetModularBehaviorTree(EObjectResetType type) { /* impl in .cpp */ }

#if CRYAISYSTEM_DEBUG
    public void DebugDrawBehaviorSelectionTree() { /* impl in .cpp */ }
#endif

    public SAIBodyInfo QueryBodyInfo() { return m_bodyInfo; /* impl in .cpp */ }
    public SAIBodyInfo GetBodyInfo() { return m_bodyInfo; }

    ////////////////////////////////////////////////////////////////////////////////////////
    //IAIPathAgent//////////////////////////////////////////////////////////////////////////
    public virtual IEntity GetPathAgentEntity() { return null; /* impl in .cpp */ }
    public virtual string GetPathAgentName() { return ""; /* impl in .cpp */ }
    public virtual ushort GetPathAgentType() { return 0; /* impl in .cpp */ }
    public virtual float GetPathAgentPassRadius() { return 0; /* impl in .cpp */ }
    public virtual Vec3 GetPathAgentPos() { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public virtual Vec3 GetPathAgentVelocity() { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public virtual void GetPathAgentNavigationBlockers(NavigationBlockers navigationBlockers, PathfindRequest pRequest) { /* impl in .cpp */ }

    public override nuint GetNavNodeIndex() { return base.GetNavNodeIndex(); /* impl in .cpp */ }

    public virtual AgentMovementAbility GetPathAgentMovementAbility() { return m_movementAbility; }
    public virtual IPathFollower GetPathFollower() { return null; /* impl in .cpp */ }

    public virtual uint GetPathAgentLastNavNode() { return 0; /* impl in .cpp */ }
    public virtual void SetPathAgentLastNavNode(uint lastNavNode) { /* impl in .cpp */ }

    public virtual void SetPathToFollow(string pathName) { /* impl in .cpp */ }
    public virtual void SetPathAttributeToFollow(bool bSpline) { /* impl in .cpp */ }

    public virtual void SetPFBlockerRadius(int blockerType, float radius) { /* impl in .cpp */ }

    public virtual ETriState CanTargetPointBeReached(CTargetPointRequest request) { return ETriState.eTS_maybe; /* impl in .cpp */ }
    public virtual bool UseTargetPointRequest(CTargetPointRequest request) { return false; /* impl in .cpp */ }

    public virtual bool GetValidPositionNearby(Vec3 proposedPosition, out Vec3 adjustedPosition) { adjustedPosition = proposedPosition; return false; /* impl in .cpp */ }
    public virtual bool GetTeleportPosition(out Vec3 teleportPos) { teleportPos = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }

    public virtual bool IsPointValidForAgent(Vec3 pos, uint flags) { return true; }
    //IAIPathAgent//////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////

    //===================================================================
    // inherited virtual interface functions
    //===================================================================
    public override void SetPos(Vec3 pos, Vec3 dirFwrd = default) { base.SetPos(pos, dirFwrd); }
    public override void Reset(EObjectResetType type) { /* impl in .cpp */ }
    public override void OnObjectRemoved(CAIObject pObject) { /* impl in .cpp */ }
    public ref SOBJECTSTATE GetState() { return ref m_State; }
    public virtual void SetSignal(int nSignalID, string szText, IEntity pSender = null, IAISignalExtraData pData = null, uint crcCode = 0) { /* impl in .cpp */ }
    public virtual void OnAIHandlerSentSignal(string szText, uint crcCode) { /* impl in .cpp */ }
    public override void Serialize(TSerialize ser) { /* impl in .cpp */ }
    public virtual void Update(EObjectUpdate type) { /* impl in .cpp */ }
    public virtual void UpdateProxy(EObjectUpdate type) { /* impl in .cpp */ }
    public virtual void UpdateDisabled(EObjectUpdate type) { /* impl in .cpp */ }
    public override void SetProxy(IAIActorProxy proxy) { m_proxy = proxy; }
    public override IAIActorProxy GetProxy() { return m_proxy; }
    public virtual bool CanAcquireTarget(IAIObject pOther) { return false; /* impl in .cpp */ }
    public virtual void ResetPerception() { /* impl in .cpp */ }
    public override bool IsHostile(IAIObject pOther, bool bUsingAIIgnorePlayer = true) { return false; /* impl in .cpp */ }
    public virtual void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true) { /* impl in .cpp */ }
    public override void Event(ushort eType, SAIEVENT pAIEvent) { /* impl in .cpp */ }
    public override void EntityEvent(SEntityEvent eventArg) { /* impl in .cpp */ }
    public override void SetGroupId(int id) { base.SetGroupId(id); }
    public override void SetFactionID(uint8 factionID) { base.SetFactionID(factionID); }

    public virtual void SetObserver(bool observer) { m_observer = observer; }
    public virtual uint GetObserverTypeMask() { return 0; /* impl in .cpp */ }
    public override uint GetObservableTypeMask() { return 0; /* impl in .cpp */ }

    public virtual void ReactionChanged(uint8 factionID, IFactionMap.ReactionType reaction) { /* impl in .cpp */ }
    public virtual void VisionChanged(float sightRange, float primaryFOVCos, float secondaryFOVCos) { /* impl in .cpp */ }
    public virtual bool IsObserver() { return m_observer; }
    public virtual bool CanSee(VisionID otherVisionID) { return false; /* impl in .cpp */ }

    public virtual void RegisterBehaviorListener(IActorBehaviorListener listener) { /* impl in .cpp */ }
    public virtual void UnregisterBehaviorListener(IActorBehaviorListener listener) { /* impl in .cpp */ }
    public virtual void BehaviorEvent(EBehaviorEvent eventArg) { /* impl in .cpp */ }
    public virtual void BehaviorChanged(string current, string previous) { /* impl in .cpp */ }

    //===================================================================
    // virtual functions rooted here
    //===================================================================

    public virtual DamagePartVector GetDamageParts() { return null; }

    public AgentParameters GetParameters() { return m_Parameters; }
    public virtual void SetParameters(AgentParameters parameters) { /* impl in .cpp */ }
    public virtual AgentMovementAbility GetMovementAbility() { return m_movementAbility; }
    public virtual void SetMovementAbility(AgentMovementAbility parameters) { m_movementAbility = parameters; }

    public virtual bool IsLowHealthPauseActive() { return false; }
    public virtual IEntity GetGrabbedEntity() { return null; }
    public virtual bool IsGrabbedEntityInView(Vec3 pos) { return false; }

    public void GetLocalBounds(out AABB bbox) { bbox = new AABB(); /* impl in .cpp */ }

    public virtual bool IsDevalued(IAIObject pAIObject) { return false; }

    public virtual void ResetLookAt() { /* impl in .cpp */ }
    public virtual bool SetLookAtPointPos(Vec3 vPoint, bool bPriority = false) { return false; /* impl in .cpp */ }
    public virtual bool SetLookAtDir(Vec3 vDir, bool bPriority = false) { return false; /* impl in .cpp */ }

    public virtual void ResetBodyTargetDir() { /* impl in .cpp */ }
    public virtual void SetBodyTargetDir(Vec3 vDir) { /* impl in .cpp */ }
    public virtual Vec3 GetBodyTargetDir() { return new Vec3(0, 0, 0); /* impl in .cpp */ }

    public virtual void SetMoveTarget(Vec3 vMoveTarget) { /* impl in .cpp */ }
    public virtual void GoTo(Vec3 vTargetPos) { /* impl in .cpp */ }
    public virtual void SetSpeed(float fSpeed) { /* impl in .cpp */ }

    public virtual bool IsInvisibleFrom(Vec3 pos, bool bCheckCloak = true, bool bCheckCloakDistance = true, CloakObservability cloakObservability = default) { return false; /* impl in .cpp */ }

    //===================================================================
    // non-virtual functions
    //===================================================================
    public void NotifyDeath() { /* impl in .cpp */ }
    public bool IsCloakEffective(Vec3 pos) { return false; /* impl in .cpp */ }

    public void GetSightFOVCos(out float primaryFOVCos, out float secondaryFOVCos) { primaryFOVCos = m_FOVPrimaryCos; secondaryFOVCos = m_FOVSecondaryCos; }
    public void CacheFOVCos(float primaryFOV, float secondaryFOV) { /* impl in .cpp */ }

    public virtual float AdjustTargetVisibleRange(CAIActor observer, float fVisibleRange) { return fVisibleRange; /* impl in .cpp */ }

    public virtual float GetMaxTargetVisibleRange(IAIObject pTarget, bool bCheckCloak = true) { return 0; /* impl in .cpp */ }

    public EAILightLevel GetLightLevel() { return m_lightLevel; }
    public virtual bool IsAffectedByLight() { return m_Parameters.m_PerceptionParams.isAffectedByLight; }

    public override void GetPhysicalSkipEntities(PhysSkipList skipList) { /* impl in .cpp */ }
    public virtual void UpdateObserverSkipList() { /* impl in .cpp */ }

    public virtual NavigationAgentTypeID GetNavigationTypeID() { return m_navigationTypeID; }

    public void CoordinationEntered(string signalName) { /* impl in .cpp */ }
    public void CoordinationExited(string signalName) { /* impl in .cpp */ }

    public bool m_bCheckedBody;

    public SOBJECTSTATE m_State;
    public AgentParameters m_Parameters;
    public AgentMovementAbility m_movementAbility;

#if CRYAISYSTEM_DEBUG
    public CValueHistory<float> m_healthHistory;
#endif

    public List<CAIObject> m_probableTargets = new List<CAIObject>();

    public void AddProbableTarget(CAIObject pTarget) { /* impl in .cpp */ }
    public void ClearProbableTargets() { /* impl in .cpp */ }

    public virtual void EnablePerception(bool enable) { /* impl in .cpp */ }
    public virtual bool IsPerceptionEnabled() { return false; /* impl in .cpp */ }

    public virtual bool IsActive() { return m_bEnabled; }
    public virtual bool IsAgent() { return true; }

    public bool IsUsingCombatLight() { return m_usingCombatLight; }

    public float GetCachedWaterOcclusionValue() { return m_cachedWaterOcclusionValue; }

    public override IBlackBoard GetBlackBoard() { return m_blackBoard; }
    public virtual IBlackBoard GetBehaviorBlackBoard() { return m_behaviorBlackBoard; }

    public virtual IAIObject GetAttentionTarget() { return m_refAttentionTarget.GetAIObject(); }
    public virtual void SetAttentionTarget(CWeakRef<CAIObject> refAttTarget) { m_refAttentionTarget = refAttTarget; }

    public virtual EAITargetThreat GetAttentionTargetThreat() { return m_State.eTargetThreat; }
    public virtual EAITargetType GetAttentionTargetType() { return m_State.eTargetType; }

    public virtual EAITargetThreat GetPeakThreatLevel() { return m_State.ePeakTargetThreat; }
    public virtual EAITargetType GetPeakThreatType() { return m_State.ePeakTargetType; }
    public virtual uint GetPeakTargetID() { return m_State.ePeakTargetID; }

    public virtual EAITargetThreat GetPreviousPeakThreatLevel() { return m_State.ePreviousPeakTargetThreat; }
    public virtual EAITargetType GetPreviousPeakThreatType() { return m_State.ePreviousPeakTargetType; }
    public virtual uint GetPreviousPeakTargetID() { return m_State.ePreviousPeakTargetID; }

    public virtual Vec3 GetFloorPosition(Vec3 pos) { return pos; /* impl in .cpp */ }

    public virtual void CheckCloseContact(IAIObject pTarget, float distSq) { /* impl in .cpp */ }
    public bool CloseContactEnabled() { return !m_bCloseContact; }
    public void SetCloseContact(bool bCloseContact) { /* impl in .cpp */ }

    public EFieldOfViewResult IsObjectInFOV(CAIObject pTarget, float fDistanceScale = 1.0f) { return EFieldOfViewResult.eFOV_Outside; /* impl in .cpp */ }

    public void AddPersonallyHostile(uint hostileID) { /* impl in .cpp */ }
    public void RemovePersonallyHostile(uint hostileID) { /* impl in .cpp */ }
    public void ResetPersonallyHostiles() { /* impl in .cpp */ }
    public bool IsPersonallyHostile(uint hostileID) { return false; /* impl in .cpp */ }

#if CRYAISYSTEM_DEBUG
    public void UpdateHealthHistory() { /* impl in .cpp */ }
#endif

    public virtual void SetTerritoryShapeName(string szName) { m_territoryShapeName = szName; }
    public virtual string GetTerritoryShapeName() { return m_territoryShapeName; }
    public virtual string GetWaveName() { return m_Parameters.m_sWaveName; }
    public virtual bool IsPointInsideTerritoryShape(Vec3 vPos, bool bCheckHeight) { return true; /* impl in .cpp */ }
    public virtual bool ConstrainInsideTerritoryShape(ref Vec3 vPos, bool bCheckHeight) { return false; /* impl in .cpp */ }

    public SShape GetTerritoryShape() { return m_territoryShape; }

    public void GetMovementSpeedRange(float fUrgency, bool bSlowForStrafe, out float normalSpeed, out float minSpeed, out float maxSpeed)
    { normalSpeed = 0; minSpeed = 0; maxSpeed = 0; /* impl in .cpp */ }

    public override EFieldOfViewResult IsPointInFOV(Vec3 pos, float distanceScale = 1.0f) { return EFieldOfViewResult.eFOV_Outside; /* impl in .cpp */ }

    public enum ENavInteraction { NI_IGNORE, NI_STEER, NI_SLOW }
    public static ENavInteraction GetNavInteraction(CAIObject navigator, CAIObject obstacle) { return ENavInteraction.NI_IGNORE; /* impl in .cpp */ }

    public virtual void CancelRequestedPath(bool actorRemoved) { /* impl in .cpp */ }

    public enum BehaviorTreeEvaluationMode
    {
        EvaluateWhenVariablesChange,
        EvaluationBlockedUntilBehaviorUnlocks,

        BehaviorTreeEvaluationModeCount,
        FirstBehaviorTreeEvaluationMode = 0
    }

    public void SetBehaviorTreeEvaluationMode(BehaviorTreeEvaluationMode mode) { m_behaviorTreeEvaluationMode = mode; }

#if AI_COMPILE_WITH_PERSONAL_LOG
    public PersonalLog GetPersonalLog() { return m_personalLog; }
#endif

    public bool GetInitialPosition(out Vec3 initialPosition) { initialPosition = m_initialPosition.pos; return m_initialPosition.isValid; }

    public static CWeakRef<CAIActor> GetLiveTarget(CWeakRef<CAIObject> refTarget) { return new CWeakRef<CAIActor>(); /* impl in .cpp */ }
    public static CAIObject GetLiveTarget(CAIObject pTarget) { return pTarget; /* impl in .cpp */ }

    protected uint GetFactionVisionMask(uint8 factionID) { return 0; /* impl in .cpp */ }

    protected void SerializeMovementAbility(TSerialize ser) { /* impl in .cpp */ }

    protected void UpdateCloakScale() { /* impl in .cpp */ }

    protected void UpdateDamageParts(DamagePartVector parts) { /* impl in .cpp */ }

    protected EFieldOfViewResult CheckPointInFOV(Vec3 vPoint, float fSightRange) { return EFieldOfViewResult.eFOV_Outside; /* impl in .cpp */ }

    protected virtual void HandlePathDecision(MNMPathRequestResult result) { /* impl in .cpp */ }
    protected virtual void HandleVisualStimulus(SAIEVENT pAIEvent) { /* impl in .cpp */ }
    protected virtual void HandleSoundEvent(SAIEVENT pAIEvent) { /* impl in .cpp */ }
    protected virtual void HandleBulletRain(SAIEVENT pAIEvent) { /* impl in .cpp */ }

    public enum EAIObjectType { AIOT_UNKNOWN, AIOT_PLAYER, AIOT_AGENTSMALL, AIOT_AGENTMED, AIOT_AGENTBIG, AIOT_MAXTYPES }
    public static EAIObjectType GetObjectType(CAIObject ai, ushort type) { return EAIObjectType.AIOT_UNKNOWN; /* impl in .cpp */ }

    protected IAIActorProxy m_proxy; // _smart_ptr<IAIActorProxy>

    protected EAILightLevel m_lightLevel;
    protected bool m_usingCombatLight;
    protected sbyte m_perceptionDisabled;

    protected float m_cachedWaterOcclusionValue;

    protected Vec3 m_vLastFullUpdatePos;
    protected EStance m_lastFullUpdateStance;

    protected CBlackBoard m_blackBoard = new CBlackBoard();
    protected CBlackBoard m_behaviorBlackBoard = new CBlackBoard();

    protected TPerceptionHandlerModifiersVector m_perceptionHandlerModifiers = new TPerceptionHandlerModifiersVector();

    // typedef std::set<IActorBehaviorListener*> BehaviorListeners;
    protected HashSet<IActorBehaviorListener> m_behaviorListeners = new HashSet<IActorBehaviorListener>();

    protected BehaviorTreeEvaluationMode m_behaviorTreeEvaluationMode;
    protected SelectionTree m_behaviorSelectionTree;
    protected SelectionVariables m_behaviorSelectionVariables;
    protected BehaviorTreeInstance m_behaviorTreeInstance;

    protected CWeakRef<CAIObject> m_refAttentionTarget = new CWeakRef<CAIObject>();

    protected CTimeValue m_CloseContactTime;
    protected bool m_bCloseContact;

    protected string m_territoryShapeName = "";
    protected SShape m_territoryShape;

    protected float m_bodyTurningSpeed;
    protected Vec3 m_lastBodyDir;

    protected float m_stimulusStartTime;

    protected uint m_activeCoordinationCount;

    protected bool m_observer;

    private float m_FOVPrimaryCos;
    private float m_FOVSecondaryCos;

    private float m_currentCollisionAvoidanceRadiusIncrement;

    // typedef VectorSet<tAIObjectID> PersonallyHostiles;
    private SortedSet<uint> m_forcefullyHostiles = new SortedSet<uint>();

    private SAIBodyInfo m_bodyInfo;

    private NavigationAgentTypeID m_navigationTypeID;

#if AI_COMPILE_WITH_PERSONAL_LOG
    private PersonalLog m_personalLog = new PersonalLog();
#endif

    private struct SInitialPosition
    {
        public Vec3 pos;
        public bool isValid;
    }

    private SInitialPosition m_initialPosition;

    private void StartBehaviorTree(string behaviorName) { /* impl in .cpp */ }
    private void StopBehaviorTree() { /* impl in .cpp */ }
    private bool IsRunningBehaviorTree() { return m_runningBehaviorTree; }

    private bool m_runningBehaviorTree;
}

// Forward decls / shells for IAgent.h types — full literal port pending
public interface IAIActor { }
public interface IAIPathAgent { }
public interface IPathFollower { }
public interface IAISignalExtraData { }
public interface IActorBehaviorListener { }
public interface IPerceptionHandlerModifier { }

public partial class AgentParameters
{
    public PerceptionParameters m_PerceptionParams = new PerceptionParameters();
    public string m_sWaveName = "";
    public float m_fAccuracy = 1.0f; // shooter accuracy [0..1] — port literal of IAgent.h
}
public class PerceptionParameters
{
    public bool isAffectedByLight;
    public float minAlarmLevel;
    public float sightRange;
}
public class AgentMovementAbility { }
public class NavigationBlockers { }
public class PathfindRequest { }
public class CTargetPointRequest { }
public class MNMPathRequestResult { }
public class SAIBodyInfo { }
// SShape literal port lives in Shape2.cs
public class SelectionTree { }
public class SelectionVariables { }
public struct CloakObservability { }
public struct NavigationAgentTypeID { public uint id; }
public enum ETriState { eTS_invalid, eTS_false, eTS_maybe, eTS_true }
public enum EAILightLevel { AILL_NONE, AILL_LIGHT, AILL_MEDIUM, AILL_DARK, AILL_SUPERDARK, AILL_LAST }
public enum EBehaviorEvent { BE_None, BE_BehaviorChanged, BE_BehaviorEvent }
// EAITargetThreat / EAITargetType literal ports live in CryCommon/IAgent_Enums.cs

public struct SOBJECTSTATE
{
    public EAITargetThreat eTargetThreat;
    public EAITargetType eTargetType;
    public EAITargetThreat ePeakTargetThreat;
    public EAITargetType ePeakTargetType;
    public uint ePeakTargetID;
    public EAITargetThreat ePreviousPeakTargetThreat;
    public EAITargetType ePreviousPeakTargetType;
    public uint ePreviousPeakTargetID;
}

// Forward decl shell for BehaviorTree::BehaviorTreeInstance — full literal port pending Phase 8
public class BehaviorTreeInstance { }
