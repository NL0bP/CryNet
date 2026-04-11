// Literal port of dev/Code/CryEngine/CryAISystem/AIPlayer.h (declarations).
// AIPlayer.cpp impl (1198L) deferred — see deferred.md.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// ChrisR: Disable the MissLocationSensor to save multiple GetEntitiesAround calls in physics
// #if !defined(MOBILE)
// # define ENABLE_MISSLOCATION_SENSOR 1
// #endif

public class CAIPlayer : CAIActor
{
    // typedef CAIActor MyBase;

    public CAIPlayer() { /* impl in .cpp */ }
    // ~CAIPlayer();

    public new void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true) { /* impl in .cpp */ }
    public override void Reset(EObjectResetType type) { /* impl in .cpp */ }

    public void UpdateAttentionTarget(CWeakRef<CAIObject> refTarget) { /* impl in .cpp */ }
    public override EFieldOfViewResult IsPointInFOV(Vec3 pos, float distanceScale = 1.0f) { return EFieldOfViewResult.eFOV_Outside; /* impl in .cpp */ }
    public new void Update(EObjectUpdate type) { /* impl in .cpp */ }
    public override void UpdateProxy(EObjectUpdate type) { }
    public override void Serialize(TSerialize ser) { /* impl in .cpp */ }
    public override void OnObjectRemoved(CAIObject pObject) { /* impl in .cpp */ }
    public override bool IsLowHealthPauseActive() { return false; /* impl in .cpp */ }
    public override IEntity GetGrabbedEntity() { return null; /* impl in .cpp */ }
    public override bool IsGrabbedEntityInView(Vec3 pos) { return false; /* impl in .cpp */ }

    public override void GetObservablePositions(ObservableParams observableParams) { /* impl in .cpp */ }
    public override uint GetObservableTypeMask() { return 0; /* impl in .cpp */ }

    // Inherited
    public override DamagePartVector GetDamageParts() { return m_damageParts; }

    public override void Event(ushort eType, SAIEVENT pEvent) { /* impl in .cpp */ }

    public int GetDeathCount() { return m_deathCount; }
    public void IncDeathCount() { m_deathCount++; }

    public override void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData pEventData) { /* impl in .cpp */ }
    public new void RecordSnapshot() { /* impl in .cpp */ }

    public override float AdjustTargetVisibleRange(CAIActor observer, float fVisibleRange) { return fVisibleRange; /* impl in .cpp */ }
    public override bool IsAffectedByLight() { return false; /* impl in .cpp */ }

    public bool IsThrownByPlayer(uint ent) { return false; /* impl in .cpp */ }
    public bool IsPlayerStuntAffectingTheDeathOf(CAIActor pDeadActor) { return false; /* impl in .cpp */ }
    public uint GetNearestThrownEntity(Vec3 pos) { return 0; /* impl in .cpp */ }
    public bool IsDoingStuntActionRelatedTo(Vec3 pos, float nearDistance) { return false; /* impl in .cpp */ }

    public override void GetPhysicalSkipEntities(PhysSkipList skipList) { /* impl in .cpp */ }

    public bool GetMissLocation(Vec3 shootPos, Vec3 shootDir, float maxAngle, out Vec3 pos) { pos = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public void NotifyMissLocationConsumed() { /* impl in .cpp */ }
    public void NotifyPlayerActionToTheLookingAgents(string eventName) { /* impl in .cpp */ }

    public void DebugDraw() { /* impl in .cpp */ }

    private CAIPlayer(CAIPlayer src) { } // disallow copies
    // operator= disallowed

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

    private void AddThrownEntity(uint id) { /* impl in .cpp */ }

    private void UpdatePlayerStuntActions() { /* impl in .cpp */ }
    private void HandleCloaking(bool cloak) { /* impl in .cpp */ }
    private void HandleArmoredHit() { /* impl in .cpp */ }
    private void HandleStampMelee() { /* impl in .cpp */ }

    private Vec3 m_stuntDir;

    private float m_playerStuntSprinting;
    private float m_playerStuntJumping;
    private float m_playerStuntCloaking;
    private float m_playerStuntUncloaking;

    private float m_mercyTimer;

    private void CollectExposedCover() { /* impl in .cpp */ }
    private void ReleaseExposedCoverObjects() { /* impl in .cpp */ }
    private void AddExposedCoverObject(IPhysicalEntity pPhysEnt) { /* impl in .cpp */ }
    private void CollectExposedCoverRayComplete(QueuedRayID rayID, RayCastResult result) { /* impl in .cpp */ }

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
}

// Forward decl for CMissLocationSensor — Phase 5 (Perception)
public class CMissLocationSensor { }
