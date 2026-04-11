// Literal port of dev/Code/CryEngine/CryAISystem/PipeUser.h (declarations + a few inline accessors).
// PipeUser.cpp impl (5019L) deferred — see deferred.md.
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem;

// created by Petar

public struct CoverUsageInfo
{
    public CoverUsageInfo(int dummy = 0)
    {
        lowLeft = false;
        lowCenter = false;
        lowRight = false;
        highLeft = false;
        highCenter = false;
        highRight = false;
        lowCompromised = false;
        highCompromised = false;
    }

    public CoverUsageInfo(bool state)
    {
        lowLeft = state;
        lowCenter = state;
        lowRight = state;
        highLeft = state;
        highCenter = state;
        highRight = state;
        lowCompromised = state;
        highCompromised = state;
    }


    public bool lowLeft;          // : 1
    public bool lowCenter;        // : 1
    public bool lowRight;         // : 1
    public bool lowCompromised;   // : 1
    public bool highLeft;         // : 1
    public bool highCenter;       // : 1
    public bool highRight;        // : 1
    public bool highCompromised;  // : 1
}

public enum EAimState
{
    AI_AIM_NONE,        // No aiming requested
    AI_AIM_WAITING,     // Aiming requested, but not yet ready.
    AI_AIM_OBSTRUCTED,  // Aiming obstructed.
    AI_AIM_READY,
    AI_AIM_FORCED,
}


public class CPipeUser : CAIActor /*, CPipeUserAdapter — ported as composition since AIActor already inherits from CAIObject */
{
    // friend class CAISystem;

    public CPipeUser() { /* impl in .cpp */ }
    // virtual ~CPipeUser();

    public virtual IPipeUser CastToIPipeUser() { return null; /* this in C++ */ }

    public override void Event(ushort eType, SAIEVENT pAIEvent) { /* impl in .cpp */ }
    public override void ParseParameters(AIObjectParams parameters, bool bParseMovementParams = true) { /* impl in .cpp */ }

    public override void RecordEvent(IAIRecordable.e_AIDbgEvent eventArg, ref IAIRecordable.RecorderEventData pEventData) { /* impl in .cpp */ }
    public override void RecordSnapshot() { /* impl in .cpp */ }

    public override void Reset(EObjectResetType type) { /* impl in .cpp */ }
    public override void SetName(string pName) { base.SetName(pName); }
    public void GetStateFromActiveGoals(ref SOBJECTSTATE state) { /* impl in .cpp */ }
    public CGoalPipe GetGoalPipe(string name) { return null; /* impl in .cpp */ }
    public void RemoveActiveGoal(int nOrder) { /* impl in .cpp */ }

    public override void SetAttentionTarget(CWeakRef<CAIObject> refTarget) { base.SetAttentionTarget(refTarget); }

    public virtual void ClearPotentialTargets() { }
    public void SetLastOpResult(CWeakRef<CAIObject> refObject) { m_refLastOpResult = refObject; }

    public virtual bool NavigateAroundObjects(Vec3 targetPos, bool fullUpdate) { return false; }

    public override void CancelRequestedPath(bool actorRemoved) { /* impl in .cpp */ }

    protected override void HandlePathDecision(MNMPathRequestResult result) { /* impl in .cpp */ }
    public void AdjustPath() { /* impl in .cpp */ }

    public void OnMNMPathResult(QueuedPathID requestId, MNMPathRequestResult result) { /* impl in .cpp */ }

    public bool AdjustPathAroundObstacles() { return false; /* impl in .cpp */ }

    public new IPathFollower GetPathFollower() { return m_pPathFollower; /* impl in .cpp */ }

    public virtual void GetPathFollowerParams(PathFollowerParams outParams) { /* impl in .cpp */ }
    public uint GetPendingSmartObjectID() { return m_pendingSmartObjectId; }

    public virtual void RequestPathTo(Vec3 pos, Vec3 dir, bool allowDangerousDestination, int forceTargetBuildingId = -1, float endTol = float.MaxValue,
        float endDistance = 0.0f, CAIObject pTargetObject = null, bool cutPathAtSmartObject = true, MNMDangersFlags dangersFlags = MNMDangersFlags.eMNMDangers_None, bool considerActorsAsPathObstacles = false)
    { /* impl in .cpp */ }
    public void RequestPathTo(MNMPathRequest request) { /* impl in .cpp */ }
    public virtual void RequestPathInDirection(Vec3 pos, float distance, CWeakRef<CAIObject> refTargetObject, float endDistance = 0.0f) { /* impl in .cpp */ }

    public override void SetPathToFollow(string pathName) { m_pathToFollowName = pathName; }
    public override void SetPathAttributeToFollow(bool bSpline) { m_bPathToFollowIsSpline = bSpline; }

    public string GetPathToFollow() { return m_pathToFollowName; }
    public virtual bool GetPathEntryPoint(out Vec3 entryPos, bool reverse, bool startNearest) { entryPos = new Vec3(0, 0, 0); return false; /* impl in .cpp */ }
    public virtual bool UsePathToFollow(bool reverse, bool startNearest, bool loop) { return false; /* impl in .cpp */ }
    public virtual void SetPointListToFollow(List<Vec3> pointList, IAISystem_ENavigationType navType, bool bSpline) { /* impl in .cpp */ }
    public virtual bool UsePointListToFollow() { return false; /* impl in .cpp */ }
    public virtual void ClearDevalued() { }
    public virtual void Forget(CAIObject pDummyObject) { }
    public virtual void Navigate3d(CAIObject pTarget) { }
    public virtual void MakeIgnorant(bool bIgnorant) { }
    public CGoalPipe GetCurrentGoalPipe() { return m_pCurrentGoalPipe; }
    public CGoalPipe GetActiveGoalPipe() { return m_pCurrentGoalPipe != null ? m_pCurrentGoalPipe.GetLastSubpipe() : null; }
    public string GetActiveGoalPipeName() { CGoalPipe pipe = GetActiveGoalPipe(); return pipe != null ? pipe.GetName() : "No Active GoalPipe"; }
    public void ResetCurrentPipe(bool resetAlways) { /* impl in .cpp */ }
    public int GetGoalPipeId() { return 0; /* impl in .cpp */ }
    public override void ResetLookAt() { /* impl in .cpp */ }
    public override bool SetLookAtPointPos(Vec3 point, bool priority = false) { return false; /* impl in .cpp */ }
    public override bool SetLookAtDir(Vec3 dir, bool priority = false) { return false; /* impl in .cpp */ }
    public void CreateLookAtTarget() { /* impl in .cpp */ }
    public override void ResetBodyTargetDir() { /* impl in .cpp */ }
    public override void SetBodyTargetDir(Vec3 dir) { m_vBodyTargetDir = dir; }
    public override Vec3 GetBodyTargetDir() { return m_vBodyTargetDir; }
    public void ResetDesiredBodyDirectionAtTarget() { /* impl in .cpp */ }
    public void SetDesiredBodyDirectionAtTarget(Vec3 dir) { m_vDesiredBodyDirectionAtTarget = dir; }
    public Vec3 GetDesiredBodyDirectionAtTarget() { return m_vDesiredBodyDirectionAtTarget; }
    public void ResetMovementContext() { /* impl in .cpp */ }
    public void ClearMovementContext(uint movementContext) { /* impl in .cpp */ }
    public void SetMovementContext(uint movementContext) { m_movementContext = movementContext; }
    public uint GetMovementContext() { return m_movementContext; }

    public void SetAllowedStrafeDistances(float start, float end, bool whileMoving) { }

    public void SetExtraPriority(float priority) { m_AttTargetPersistenceTimeout = priority; }
    public float GetExtraPriority() { return m_AttTargetPersistenceTimeout; }

    public int SetLooseAttentionTarget(CWeakRef<CAIObject> refObject, int id = -1) { return 0; /* impl in .cpp */ }

    public void SetLookStyle(ELookStyle eLookStyle) { /* impl in .cpp */ }
    public virtual void AllowLowerBodyToTurn(bool bAllowLowerBodyToTurn) { /* impl in .cpp */ }
    public virtual bool IsAllowingBodyTurn() { return false; /* impl in .cpp */ }

    public ELookStyle GetLookStyle() { return ELookStyle.LOOKSTYLE_DEFAULT; /* impl in .cpp */ }

    public int SetLooseAttentionTarget(Vec3 pos)
    {
        SetLookAtPointPos(pos);
        return ++m_looseAttentionId;
    }

    public Vec3 GetLooseAttentionPos() { return new Vec3(0, 0, 0); /* impl in .cpp */ }

    public int GetLooseAttentionId() { return m_looseAttentionId; }

    // typedef boost::weak_ptr<Vec3> LookTargetWeakPtr;
    public virtual LookTargetPtr CreateLookTarget() { return null; /* impl in .cpp */ }

    public void RegisterAttack(string name) { /* impl in .cpp */ }
    public void RegisterRetreat(string name) { /* impl in .cpp */ }
    public void RegisterWander(string name) { /* impl in .cpp */ }
    public void RegisterIdle(string name) { /* impl in .cpp */ }
    public bool SelectPipe(int id, string name, CWeakRef<CAIObject> refArgument, int goalPipeId = 0, bool resetAlways = false, GoalParams node = null) { return false; /* impl in .cpp */ }
    public IGoalPipe InsertSubPipe(int id, string name, CWeakRef<CAIObject> refArgument, int goalPipeId = 0, GoalParams node = null) { return null; /* impl in .cpp */ }
    public bool CancelSubPipe(int goalPipeId) { return false; /* impl in .cpp */ }
    public bool RemoveSubPipe(int goalPipeId, bool keepInserted = false) { return false; /* impl in .cpp */ }
    public bool IsUsingPipe(string name) { return false; /* impl in .cpp */ }
    public bool IsUsingPipe(int goalPipeId) { return false; /* impl in .cpp */ }
    public bool AbortActionPipe(int goalPipeId) { return false; /* impl in .cpp */ }

#if USE_DEPRECATED_AI_CHARACTER_SYSTEM
    public bool SetCharacter(string character, string behaviour = null) { return false; }
#endif
    public bool IsUsing3DNavigation() { return false; /* impl in .cpp */ }

    public virtual void Pause(bool pause) { /* impl in .cpp */ }
    public virtual bool IsPaused() { return m_paused != 0; }

    public bool AllowedToFire() { return (m_fireMode != EFireMode.FIREMODE_OFF) && (m_fireMode != EFireMode.FIREMODE_AIM) && (m_fireMode != EFireMode.FIREMODE_AIM_SWEEP); }
    public virtual void SetFireMode(EFireMode mode) { /* impl in .cpp */ }
    public virtual void SetFireTarget(CWeakRef<CAIObject> refTargetObject) { /* impl in .cpp */ }
    public virtual EFireMode GetFireMode() { return m_fireMode; }

    public virtual CAIObject GetRefPoint() { CreateRefPoint(); return m_refRefPoint.GetAIObject(); }

    public virtual void SetRefPointPos(Vec3 pos) { /* impl in .cpp */ }
    public virtual void SetRefPointPos(Vec3 pos, Vec3 dir) { /* impl in .cpp */ }
    public virtual void SetRefShapeName(string shapeName) { m_refShapeName = shapeName; }
    public virtual string GetRefShapeName() { return m_refShapeName; }
    public virtual Vec3 GetProbableTargetPosition() { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public SShape GetRefShape() { return m_refShape; }

    public void SetCoverRegister(CoverID coverID) { m_regCoverID = coverID; }
    public CoverID GetCoverRegister() { return m_regCoverID; }

    public void CreateRefPoint() { /* impl in .cpp */ }

    public virtual void SetActorTargetRequest(SAIActorTargetRequest req) { /* impl in .cpp */ }
    public virtual void ClearActorTargetRequest() { /* impl in .cpp */ }
    public SAIActorTargetRequest GetActiveActorTargetRequest() { return m_pActorTargetRequest; }

    public virtual void IgnoreCurrentHideObject(float timeOut) { /* impl in .cpp */ }

    public virtual uint GetLastUsedSmartObjectId() { return m_idLastUsedSmartObject; }
    public virtual bool IsUsingNavSO() { return m_eNavSOMethod != ENavSOMethod.nSOmNone; }
    public virtual void ClearPath(string dbgString) { /* impl in .cpp */ }

    public override ETriState CanTargetPointBeReached(CTargetPointRequest request) { return ETriState.eTS_maybe; /* impl in .cpp */ }
    public override bool UseTargetPointRequest(CTargetPointRequest request) { return false; /* impl in .cpp */ }

    public virtual void UpdateLookTarget(CAIObject pTarget) { /* impl in .cpp */ }
    public void EnableUpdateLookTarget(bool bEnable = true) { m_bEnableUpdateLookTarget = bEnable; }

    public void PathIsInvalid() { /* impl in .cpp */ }

    public bool WasHideObjectRecentlyUnreachable(Vec3 pos) { return false; /* impl in .cpp */ }

    public Vec3 GetLastLiveTargetPosition() { return m_lastLiveTargetPos; }
    public float GetTimeSinceLastLiveTarget() { return m_timeSinceLastLiveTarget; }

    public virtual IAIObject GetAttentionTargetAssociation()
    {
        CAIObject pAttentionTarget = m_refAttentionTarget.GetAIObject();
        return (pAttentionTarget != null ? pAttentionTarget.GetAssociation().GetAIObject() : null);
    }

    public virtual CAIObject GetLastOpResult() { return m_refLastOpResult.GetAIObject(); }
    public virtual CAIObject GetSpecialAIObject(string objName, float range = 0.0f) { return null; /* impl in .cpp */ }

    public override EAITargetThreat GetAttentionTargetThreat() { return m_AttTargetThreat; }
    public override EAITargetType GetAttentionTargetType() { return m_AttTargetType; }

    public virtual void SetLastActionStatus(bool bSucceed) { m_bLastActionSucceed = bSucceed; }

    public void ClearInvalidatedSOLinks() { /* impl in .cpp */ }
    public void InvalidateSOLink(CSmartObject pObject, SmartObjectHelper pFromHelper, SmartObjectHelper pToHelper) { /* impl in .cpp */ }
    public bool IsSOLinkInvalidated(CSmartObject pObject, SmartObjectHelper pFromHelper, SmartObjectHelper pToHelper) { return false; /* impl in .cpp */ }
    public bool ConvertPathToSpline(IAISystem_ENavigationType navType) { return false; /* impl in .cpp */ }

    public new void Update(EObjectUpdate type) { /* impl in .cpp */ }

    // Cover
    public void SetCoverID(CoverID coverID) { /* impl in .cpp */ }
    public CoverID GetCoverID() { return new CoverID(); /* impl in .cpp */ }
    public Vec3 GetCoverLocation() { return new Vec3(0, 0, 0); /* impl in .cpp */ }
    public uint GetCoverEyes(CAIObject targetEnemy, Vec3 enemyTargetLocation, Vec3[] eyes, uint maxCount) { return 0; /* impl in .cpp */ }

    public bool IsAdjustingAim() { return m_adjustingAim; }
    public void SetAdjustingAim(bool adjustingAim) { m_adjustingAim = adjustingAim; }

    public virtual bool IsInCover() { return m_inCover; }
    public virtual void SetInCover(bool inCover) { m_inCover = inCover; }

    public virtual void SetCoverCompromised() { /* impl in .cpp */ }
    public virtual bool IsCoverCompromised() { return false; /* impl in .cpp */ }

    public void SetMovingToCover(bool movingToCover) { m_movingToCover = movingToCover; }
    public bool IsMovingToCover() { return m_movingToCover; }

    public void SetMovingInCover(bool movingInCover) { m_movingInCover = movingInCover; }
    public bool IsMovingInCover() { return m_movingInCover; }

    public virtual bool IsTakingCover(float distanceThreshold) { return false; /* impl in .cpp */ }

    public void UpdateCoverBlacklist(float updateTime) { /* impl in .cpp */ }
    public void ResetCoverBlacklist() { /* impl in .cpp */ }
    public void SetCoverBlacklisted(CoverID coverID, bool blacklist, float time = 0.0f) { /* impl in .cpp */ }
    public bool IsCoverBlacklisted(CoverID coverID) { return false; /* impl in .cpp */ }

    public float GetCoverLocationEffectiveHeight() { return m_coverUser.GetLocationEffectiveHeight(); }

    public CoverHeight CalculateEffectiveCoverHeight() { return CoverHeight.LowCover; /* impl in .cpp */ }

    public float GetLastUpdateInterval() { return m_fTimePassed; }

    public AsyncState GetCoverUsageInfo(out CoverUsageInfo usageInfo) { usageInfo = new CoverUsageInfo(); return AsyncState.AsyncReady; /* impl in .cpp */ }

    public override void OnAIHandlerSentSignal(string szText, uint crcCode) { /* impl in .cpp */ }

    public Movement_PathfinderState GetPathfinderState() { return Movement_PathfinderState.StillFinding; /* impl in .cpp */ }
    public INavPath GetINavPath() { return m_Path; }

    // typedef std::vector< COPWaitSignal* > ListWaitGoalOps;
    public List<COPWaitSignal> m_listWaitGoalOps = new List<COPWaitSignal>();

    public bool m_bLastNearForbiddenEdge;
    public bool m_bLastActionSucceed;

    // typedef std::set< std::pair< CSmartObject*, std::pair< SmartObjectHelper*, SmartObjectHelper* > > > TSetInvalidatedSOLinks;
    public /*mutable*/ HashSet<(CSmartObject, SmartObjectHelper, SmartObjectHelper)> m_invalidatedSOLinks = new HashSet<(CSmartObject, SmartObjectHelper, SmartObjectHelper)>();

    // DEBUG MEMBERS
    public EGoalOperations m_lastExecutedGoalop;

    public CWeakRef<CAIObject> m_refLastOpResult = new CWeakRef<CAIObject>();

    public Vec3 m_posLookAtSmartObject;

    public CNavPath m_Path = new CNavPath();
    public CNavPath m_OrigPath = new CNavPath();
    public Vec3 m_PathDestinationPos;
    public bool m_bPathfinderConsidersPathTargetDirection;
    public float m_fTimePassed;

    public CWeakRef<CAIObject> m_refPathFindTarget = new CWeakRef<CAIObject>();

    public bool m_bLooseAttention;

    public CWeakRef<CAIObject> m_refLooseAttentionTarget = new CWeakRef<CAIObject>();

    public bool m_bPriorityLookAtRequested;

    public CAIHideObject m_CurrentHideObject = new CAIHideObject();

    public Vec3 m_vLastMoveDir;
    public bool m_bKeepMoving;
    public int m_nPathDecision;

    public uint m_queuedPathId;

    public bool m_IsSteering;
    public float m_flightSteeringZOffset;

    public ENavSOMethod m_eNavSOMethod;
    public bool m_navSOEarlyPathRegen;
    public uint m_idLastUsedSmartObject;

    public SNavSOStates m_currentNavSOStates;
    public SNavSOStates m_pendingNavSOStates;

    public int m_actorTargetReqId;

    public enum EMovementReason
    {
        AIMORE_UNKNOWN,
        AIMORE_TRACE,
        AIMORE_MOVE,
        AIMORE_MANEUVER,
        AIMORE_SMARTOBJECT,
    }

    public virtual void DebugDrawGoals() { /* impl in .cpp */ }

    public void DebugDrawCoverUser() { /* impl in .cpp */ }

    // typedef std::multimap< int, std::pair< IGoalPipeListener*, const char* > > TMapGoalPipeListeners;
    public SortedDictionary<int, List<(IGoalPipeListener, string)>> m_mapGoalPipeListeners = new SortedDictionary<int, List<(IGoalPipeListener, string)>>();
    public void NotifyListeners(int goalPipeId, EGoalPipeEvent eventArg) { /* impl in .cpp */ }
    public void NotifyListeners(CGoalPipe pPipe, EGoalPipeEvent eventArg, bool includeSubPipes = false) { /* impl in .cpp */ }
    public virtual void RegisterGoalPipeListener(IGoalPipeListener pListener, int goalPipeId, string debugClassName) { /* impl in .cpp */ }
    public virtual void UnRegisterGoalPipeListener(IGoalPipeListener pListener, int goalPipeId) { /* impl in .cpp */ }

    public int CountGroupedActiveGoals() { return 0; /* impl in .cpp */ }
    public void ClearGroupedActiveGoals() { /* impl in .cpp */ }

    public EAimState GetAimState() { return m_aimState; }

    public void SetNavSOFailureStates() { /* impl in .cpp */ }

    public enum ESpecialAIObjects
    {
        AISPECIAL_LAST_HIDEOBJECT,
        AISPECIAL_PROBTARGET,
        AISPECIAL_PROBTARGET_IN_TERRITORY,
        AISPECIAL_PROBTARGET_IN_REFSHAPE,
        AISPECIAL_PROBTARGET_IN_TERRITORY_AND_REFSHAPE,
        AISPECIAL_ATTTARGET_IN_TERRITORY,
        AISPECIAL_ATTTARGET_IN_REFSHAPE,
        AISPECIAL_ATTTARGET_IN_TERRITORY_AND_REFSHAPE,
        AISPECIAL_ANIM_TARGET,
        AISPECIAL_GROUP_TAC_POS,
        AISPECIAL_GROUP_TAC_LOOK,
        AISPECIAL_VEHICLE_AVOID_POS,
        COUNT_AISPECIAL
    }

    public CAIObject GetOrCreateSpecialAIObject(ESpecialAIObjects type) { return null; /* impl in .cpp */ }
    public bool ShouldConsiderActorsAsPathObstacles() { return m_considerActorsAsPathObstacles; }

    protected override void HandleVisualStimulus(SAIEVENT pAIEvent) { /* impl in .cpp */ }
    protected override void HandleSoundEvent(SAIEVENT pAIEvent) { /* impl in .cpp */ }

    public override void Serialize(TSerialize ser) { /* impl in .cpp */ }
    public override void PostSerialize() { /* impl in .cpp */ }
    protected void ClearActiveGoals() { /* impl in .cpp */ }
    protected bool ProcessBranchGoal(QGoal Goal, ref bool blocking) { return false; /* impl in .cpp */ }
    protected bool ProcessRandomGoal(QGoal Goal, ref bool blocking) { return false; /* impl in .cpp */ }
    protected bool ProcessClearGoal(QGoal Goal, ref bool blocking) { return false; /* impl in .cpp */ }
    protected bool GetBranchCondition(QGoal Goal) { return false; /* impl in .cpp */ }

    protected virtual IPathFollower CreatePathFollower(PathFollowerParams parameters) { return null; /* impl in .cpp */ }

    protected void HandleNavSOFailure() { /* impl in .cpp */ }
    protected void SyncActorTargetPhaseWithAIProxy() { /* impl in .cpp */ }

    protected float m_AttTargetPersistenceTimeout;
    protected EAITargetThreat m_AttTargetThreat;
    protected EAITargetThreat m_AttTargetExposureThreat;
    protected EAITargetType m_AttTargetType;

    protected EFireMode m_fireMode;
    protected CWeakRef<CAIObject> m_refFireTarget = new CWeakRef<CAIObject>();
    protected bool m_fireModeUpdated;
    protected bool m_outOfAmmoSent;
    protected bool m_lowAmmoSent;
    protected bool m_wasReloading;
    protected VectorOGoals m_vActiveGoals = new VectorOGoals();
    protected VectorOGoals m_DeferredActiveGoals = new VectorOGoals();
    protected bool m_bBlocked;
    protected bool m_bStartTiming;
    protected float m_fEngageTime;
    protected CGoalPipe m_pCurrentGoalPipe;
    protected SortedSet<int> m_notAllowedSubpipes = new SortedSet<int>();
    protected bool m_bFirstUpdate;
    protected int m_looseAttentionId;
    protected IAISystem_ENavigationType m_CurrentNodeNavType;
    protected EAimState m_aimState;
    protected float m_spreadFireTime;
    protected Vec3 m_vBodyTargetDir;
    protected Vec3 m_vDesiredBodyDirectionAtTarget;
    protected uint m_movementContext;

    protected string m_pathToFollowName = "";
    protected bool m_bPathToFollowIsSpline;

    protected string m_refShapeName = "";
    protected SShape m_refShape;
    protected Vec3 m_vLastSOExitHelper;

    protected CStrongRef<CAIObject>[] m_refSpecialObjects = new CStrongRef<CAIObject>[(int)ESpecialAIObjects.COUNT_AISPECIAL];

    // typedef std::pair<float, Vec3> FloatVecPair;
    // typedef std::deque<FloatVecPair> TimeOutVec3List;
    protected LinkedList<(float, Vec3)> m_recentUnreachableHideObjects = new LinkedList<(float, Vec3)>();

    protected Vec3 m_lastLiveTargetPos;
    protected float m_timeSinceLastLiveTarget;

    protected IPathFollower m_pPathFollower;

    protected void CalculatePathObstacles() { /* impl in .cpp */ }
    protected CPathObstacles m_pathAdjustmentObstacles = new CPathObstacles();
    protected int m_adjustpath;

    protected SAIActorTargetRequest m_pActorTargetRequest;

    protected bool m_inCover;
    protected bool m_movingToCover;
    protected bool m_movingInCover;
    protected CoverUser m_coverUser = new CoverUser();
    protected CoverID m_regCoverID;

    protected uint8 m_paused;
    protected bool m_bEnableUpdateLookTarget;

    // typedef std::vector<LookTargetWeakPtr> LookTargets;
    protected List<LookTargetPtr> m_lookTargets = new List<LookTargetPtr>();

    private Vec3 SetPointListToFollowSub(Vec3 a, Vec3 b, Vec3 c, List<Vec3> newPointList, float step) { return new Vec3(0, 0, 0); /* impl in .cpp */ }

    // Just here to avoid a warning and to make sure the correct version is used
    private void SetAttentionTargetIface(IAIObject pObject) { System.Diagnostics.Debug.Assert(false); }

    private uint m_pendingSmartObjectId;
    private CStrongRef<CAIObject> m_refRefPoint = new CStrongRef<CAIObject>();
    private CStrongRef<CAIObject> m_refLookAtTarget = new CStrongRef<CAIObject>();

    private bool m_adjustingAim;

    // typedef stl::hash_map<CoverID, float, stl::hash_uint32> CoverBlacklist;
    private Dictionary<CoverID, float> m_coverBlacklist = new Dictionary<CoverID, float>();

    private void CoverUsageInfoRayComplete(QueuedRayID rayID, RayCastResult result) { /* impl in .cpp */ }

    private struct CoverUsageInfoState
    {
        public CoverUsageInfoState(int dummy = 0)
        {
            state = AsyncState.AsyncReady;
            rayCount = 0;
            result = new CoverUsageInfo(false);
            rayID = new QueuedRayID[6];
        }

        public void Reset() { /* impl in .cpp */ }

        public QueuedRayID[] rayID;
        public AsyncState state;
        public uint8 rayCount;
        public CoverUsageInfo result;
    }

    private CoverUsageInfoState m_coverUsageInfoState = new CoverUsageInfoState();


    private struct DelayedPipeSelection
    {
        public DelayedPipeSelection(int dummy = 0)
        {
            mode = 0;
            name = "";
            refArgument = new CWeakRef<CAIObject>(type_nil_ref.NILREF);
            goalPipeId = -1;
            resetAlways = false;
        }

        public DelayedPipeSelection(int _mode, string _name, CWeakRef<CAIObject> _refArgument,
            int _goalPipeId, bool _resetAlways)
        {
            mode = _mode;
            name = _name;
            refArgument = _refArgument;
            goalPipeId = _goalPipeId;
            resetAlways = _resetAlways;
        }

        public int mode;
        public string name;
        public CWeakRef<CAIObject> refArgument;
        public int goalPipeId;
        public bool resetAlways;
    }

    private DelayedPipeSelection m_delayedPipeSelection = new DelayedPipeSelection();
    private bool m_pipeExecuting;

    private bool m_cutPathAtSmartObject;
    private bool m_considerActorsAsPathObstacles;

    private PipeUserMovementActorAdapter m_movementActorAdapter = new PipeUserMovementActorAdapter();
    private MovementActorCallbacks m_callbacksForPipeuser = new MovementActorCallbacks();
}

// Forward decls / shells for related types — full literal ports pending
public class CGoalPipe
{
    public CGoalPipe GetLastSubpipe() { return null; }
    public string GetName() { return ""; }
    public void ParseParams(GoalParams param) { /* full impl in Phase 7 GoalPipe.cpp */ }
}
public class CSmartObject { }
public class SmartObjectHelper { }
public enum EGoalOperations { eGO_NONE }
public class CNavPath : INavPath { }
public interface INavPath { }
public enum EFireMode { FIREMODE_OFF, FIREMODE_BURST, FIREMODE_CONTINUOUS, FIREMODE_FORCED, FIREMODE_AIM, FIREMODE_SECONDARY, FIREMODE_SECONDARY_SMOKE, FIREMODE_MELEE, FIREMODE_KILL, FIREMODE_BURST_WHILE_MOVING, FIREMODE_PANIC_SPREAD, FIREMODE_BURST_DRAWFIRE, FIREMODE_MELEE_FORCED, FIREMODE_BURST_SNIPE, FIREMODE_AIM_SWEEP, FIREMODE_BURST_ONCE }
public enum ELookStyle { LOOKSTYLE_DEFAULT, LOOKSTYLE_HARD, LOOKSTYLE_SOFT, LOOKSTYLE_HARD_NOLOWER, LOOKSTYLE_SOFT_NOLOWER }
public class LookTargetPtr { }
public class SAIActorTargetRequest { }
public enum ENavSOMethod { nSOmNone, nSOmSignalAnimation, nSOmActionAnimation, nSOmStraight, nSOmRoundOnly }
public enum CoverHeight { LowCover, HighCover }
public class CAIHideObject { }
public class CPathObstacles { }
public class COPWaitSignal { }
public interface IGoalPipeListener { }
public enum EGoalPipeEvent { ePN_None }
public class CoverUser { public float GetLocationEffectiveHeight() { return 0; } }
public struct SNavSOStates { }
public struct QGoal { }
public class VectorOGoals : List<QGoal> { }
public class PipeUserMovementActorAdapter { }
public class MovementActorCallbacks { }
public enum IAISystem_ENavigationType { NAV_UNSET = 0, NAV_TRIANGULAR = 1, NAV_WAYPOINT_HUMAN = 2 }
public enum Movement_PathfinderState { StillFinding, FoundPath, CouldNotFindPath, Canceled }
public struct QueuedPathID { public uint id; }
public class MNMPathRequest { }
public enum MNMDangersFlags { eMNMDangers_None = 0, eMNMDangers_Explosive = 1, eMNMDangers_AttentionTarget = 2 }
