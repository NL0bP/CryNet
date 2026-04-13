// Literal port of dev/Code/CryEngine/CryAISystem/Environment.{h,cpp} (Phase 2 step 9 — session 7).
// Original Copyright Crytek GMBH or its affiliates, used under license.
//
// In the C++ engine `gAIEnv` is a free-standing global of type SAIEnvironment containing pointers
// to every AI subsystem singleton. The literal C# port mirrors the same struct as a static class
// `gAIEnv` whose static fields match the C++ struct member layout 1:1. The C++ ctor initialised
// every pointer to NULL — C# defaults to null automatically. ShutDown / Get/SetDebugRenderer are
// translated as static methods.

using CryAISystem.CryCommon;

namespace CryAISystem;

public static class GlobalFunctions
{
    // GetISystem() free function from ISystem.h
    public static ISystem GetISystem() { return gEnv.pSystem; }

    // GetAISystem() free function from CryAISystem.h
    public static CAISystem GetAISystem() { return null; }
}

// CAISystem — Phase 11 literal port. Method bodies live in CAISystem.cs (Impl methods).
// This partial block declares fields and public API methods that delegate to the Impl.
public partial class CAISystem
{
    public bool m_bInitialized;
    public bool IsEnabled() { return m_IsEnabled; }

    public System.Collections.Generic.SortedDictionary<uint8, CStrongRef<CAIObject>> m_mapFaction = new();

    // Field used by AICollision.CheckWalkabilitySimple — released at level unload by the
    // full CAISystem shutdown path (Phase 11).
    public IGeometry m_walkabilityGeometryBox;

    public int GetAITickCount() { return (int)m_nTickCount; }
    public float GetFrameStartTimeSeconds() { return m_frameStartTimeSeconds; }
    public CTimeValue GetFrameStartTime() { return m_frameStartTime; }
    public void LogEvent(string sender, string msg) { /* logging stub */ }
    public void NotifyAIObjectMoved(IEntity pEntity, SEntityEvent ev) { if (!IsEnabled()) return; }
    public string GetFormationNameFromCRC32(uint crc)
    {
        foreach (var kvp in m_mapFormationDescriptors) { if (kvp.Value.m_nNameCRC32 == crc) return kvp.Key; }
        return "";
    }
    public CFormation CreateFormation(CWeakRef<CAIObject> ref_, string name, Vec3 targetPos)
    {
        if (m_mapActiveFormations.ContainsKey(ref_)) return m_mapActiveFormations[ref_];
        if (m_mapFormationDescriptors.TryGetValue(name, out var desc))
        { var f = new CFormation(); f.Create(ref_, targetPos); m_mapActiveFormations[ref_] = f; return f; }
        return null;
    }
    public bool ReleaseFormation(CWeakRef<CAIObject> ref_, bool b)
    {
        if (m_mapActiveFormations.ContainsKey(ref_)) { m_mapActiveFormations.Remove(ref_); return true; }
        return false;
    }
    public CFormation GetFormation(int idx)
    {
        foreach (var kvp in m_mapActiveFormations) { if (kvp.Value.GetId() == idx) return kvp.Value; }
        return null;
    }
    public CLeader GetLeader(int groupId)
    {
        if (m_mapAIGroups.TryGetValue(groupId, out CAIGroup g)) return g.GetLeader();
        return null;
    }
    public IAIObject GetNearestObjectOfTypeInRange(CAIObject self, uint type, int subtype, float range, uint flags)
    {
        if (gAIEnv.pAIObjectManager == null) return null;
        Vec3 pos = self.GetPos(); float rangeSq = range * range; float mindist = float.MaxValue; IAIObject ret = null;
        if (gAIEnv.pAIObjectManager.m_Objects.TryGetValue((short)type, out var bucket))
        { foreach (var e in bucket) { var o = e.GetAIObject(); if (o == null || o == self) continue;
            if (!o.IsEnabled() && (flags & 0x0008) == 0) continue;
            float f = (o.GetPos() - pos).GetLengthSquared(); if (f < mindist && f < rangeSq) { ret = o; mindist = f; } } }
        return ret;
    }

    // Methods needed by CAIActor.cpp — Phase 2 literal port additions
    public void NotifyEnableState(CAIActor actor, bool enable) { NotifyEnableStateImpl(actor, enable); }
    public void UnregisterAIActor(CWeakRef<CAIActor> actor) { UnregisterAIActorImpl(actor); }
    public IActorProxyFactory GetActorProxyFactory() { return m_actorProxyFactoryImpl; }
    public IBehaviorTreeManager GetIBehaviorTreeManager() { return null; /* impl pending Phase 8 */ }
    public float GetWaterOcclusionValue(Vec3 pos) { return 0.0f; /* simplified — requires p3DEngine water queries */ }
    public CAILightManager GetLightManager() { return m_lightManager; }
    public float GetFrameDeltaTime() { return m_frameDeltaTime; }
    public void NotifyTargetDead(CAIActor actor) { NotifyTargetDeadImpl(actor); }
    public void UpdateGroupStatus(int groupId) { UpdateGroupStatusImpl(groupId); }
    public void OnAgentDeath(uint entityId, uint killerID) { OnAgentDeathImpl(entityId, killerID); }
    public CAIGroup GetAIGroup(int groupId) { m_mapAIGroups.TryGetValue(groupId, out var g); return g; }
    public SShape GetGenericShapeOfName(string name) { if (string.IsNullOrEmpty(name)) return null; m_mapGenericShapes.TryGetValue(name, out var s); return s; }
    public IAIObject GetBeacon(int groupId) { if (m_mapBeacons.TryGetValue(groupId, out var bs)) return bs.refBeacon.GetAIObject(); return null; }
    public void UpdateBeacon(int groupId, Vec3 pos, CAIObject pObject) { /* beacon update — simplified */ }
    public void ReleaseFormationPoint(CAIActor actor)
    {
        if (m_mapActiveFormations.Count == 0 || actor == null) return;
        CWeakRef<CAIObject> r = WeakRefHelpers.GetWeakRef((CAIObject)actor);
        foreach (var kvp in m_mapActiveFormations) kvp.Value.FreeFormationPoint(r);
    }
    public float GetCombatClassScale(int class1, int class2) { return GetCombatClassScaleImpl(class1, class2); }
    public void RemoveFromGroup(int groupId, CAIObject obj) { RemoveFromGroupImpl(groupId, obj); }
    public void AddToGroup(CAIObject obj) { AddToGroupImpl(obj, -1); }
    public void AddToFaction(CAIObject obj, uint8 factionID) { AddToFactionImpl(obj, factionID); }
    public IAISignalExtraData CreateSignalExtraData() { return new AISignalExtraData(); }
    public void FreeSignalExtraData(IAISignalExtraData data) { /* C# GC handles cleanup */ }
    public void FreeSignalExtraData(AISignalExtraData data) { /* C# GC handles cleanup */ }
    // Added for PipeUser.cpp literal port
    public void FreeFormationPoint(CWeakRef<CAIObject> refObj)
    {
        if (m_mapActiveFormations.TryGetValue(refObj, out var f)) f.FreeFormationPoint(refObj);
    }
    public CAIObject GetPlayer() { return GetPlayerImpl(); }
    public bool IsRecording(CAIObject obj, IAIRecordable.e_AIDbgEvent evt) { return false; /* debug-only */ }
    public void Record(CAIObject obj, IAIRecordable.e_AIDbgEvent evt, string data) { /* debug-only no-op */ }
    public bool CheckObjectsVisibility(CAIObject pOne, CAIObject pTwo, float range) { return CheckObjectsVisibilityImpl(pOne, pTwo, range); }
    // m_mapGroups — Phase 11 full literal port, needed by GetProbableTargetPosition
    public SortedDictionary<int, System.Collections.Generic.List<CStrongRef<CAIObject>>> m_mapGroups = new();
    // Added for AIPlayer.cpp literal port
    public void SendSignal(SIGNALFILTER filter, int nFollowUp, string szText, CAIActor pSender, IAISignalExtraData pData)
    {
        // Delegate to the literal port's SendSignal switch; simplified filter dispatch
        if (pSender == null) return;
        // For all filters, deliver to sender as baseline (full switch in CAISystem.cs covers group/faction filters)
        pSender.SetSignal(nFollowUp, szText, pSender.GetEntity(), pData);
    }
    public float GetVisPerceptionDistScale(float fRatio) { return GetVisPerceptionDistScaleImpl(fRatio); }
    public bool CheckVisibilityToBody(CPuppet pPuppet, CAIActor pTarget, ref float dist) { return CheckVisibilityToBodyImpl(pPuppet, pTarget, ref dist, null); }
    public IAIDebugRenderer GetAIDebugRenderer() { return gAIEnv.GetDebugRenderer(); }

    // CAISystem.h line 75
    public const float AGENT_COVER_CLEARANCE = 0.35f;

    // Danger flags — CAISystem.h
    public const int DANGER_ALL = -1;
    public const int DANGER_DEADBODY = 1;

    // AlertnessCounters — CAISystem.h
    public int[] m_AlertnessCounters = new int[4];

    // Added for Puppet.cpp literal port
    public bool CheckPointsVisibility(Vec3 one, Vec3 two, float range, IPhysicalEntity skip0 = null, IPhysicalEntity skip1 = null) { return CheckPointsVisibilityImpl(one, two, range, skip0, skip1); }
    public System.Collections.Generic.List<SDangerSpot> GetDangerSpots(CAIObject pObj, float range, int types) { return GetDangerSpotsImpl(pObj, range, types); }
    public bool SameFormation(CAIObject pOne, CAIObject pTwo) { return false; /* impl pending Phase 9 */ }
    public void AddToGroup(CAIObject obj, int groupId) { AddToGroupImpl(obj, groupId); }

    // AdjustDirectionalCoverPosition — CAISystem.cpp:5716
    public void AdjustDirectionalCoverPosition(ref Vec3 pos, Vec3 dir, float agentRadius, float testHeight)
    {
        // Simplified — full impl requires physics raycasts
        pos.z += testHeight;
    }

    // Added for GoalOpTrace.cpp / GoalOpStick.cpp literal port
    public bool WouldHumanBeVisible(Vec3 pos, bool checkBody) { return true; /* simplified — no camera frustum in port */ }
    public void LogComment(string sender, string msg) { /* logging stub */ }
}

// CLeader shell — Phase 9. C++ has `class CLeader : public CAIActor`; the literal port mirrors that
// inheritance chain so SAIObjectCreationHelper and other call sites can construct CLeader as a CAIObject.
public class CLeader : CAIActor
{
    public CLeader() { }
    public CWeakRef<CAIObject> GetFormationOwner() { return new CWeakRef<CAIObject>(); /* impl pending */ }
}

// SAIEnvironment is the C++ struct; the literal C# port hosts every member as a static field of
// the `gAIEnv` class so that call sites preserve the C++ `gAIEnv.pXxx` access verbatim.
public static class gAIEnv
{
    // Literal C++ struct-member order from Environment.h (lines 80-138).
    public static AIConsoleVariablesShell CVars = new AIConsoleVariablesShell();
    public static AISIGNALS_CRC SignalCRCs;

    public static SConfiguration configuration;

    public static ActorLookUp pActorLookUp;
    public static IWalkabilityCacheManager pWalkabilityCacheManager;
    public static IGoalOpFactory pGoalOpFactory;
    public static CObjectContainer pObjectContainer;

    // #if !defined(_RELEASE)
    public static CCodeCoverageTracker pCodeCoverageTracker;
    public static CCodeCoverageManager pCodeCoverageManager;
    public static CCodeCoverageGUI pCodeCoverageGUI;
    // #endif

    public static CStatsManager pStatsManager;
    public static CTacticalPointSystem pTacticalPointSystem;
    public static CTargetTrackManager pTargetTrackManager;
    public static CAIObjectManager pAIObjectManager;
    public static CPipeManager pPipeManager;
    public static CGraph pGraph;                                                  // superseded by NavigationSystem
    public static MNM.PathfinderNavigationSystemUser pPathfinderNavigationSystemUser;
    public static CMNMPathfinder pMNMPathfinder;                                  // superseded by NavigationSystem
    public static CNavigation pNavigation;                                        // superseded by NavigationSystem
    public static CAIActionManager pAIActionManager;
    public static CSmartObjectManager pSmartObjectManager;

    public static CPerceptionManager pPerceptionManager;

    public static CCommunicationManager pCommunicationManager;
    public static CCoverSystem pCoverSystem;
    public static Navigation.NavigationSystem.NavigationSystem pNavigationSystem;
    public static CSelectionTreeManager pSelectionTreeManager;
    public static BehaviorTree.BehaviorTreeManager pBehaviorTreeManager;
    public static BehaviorTree.GraftManager pGraftManager;
    public static CVisionMap pVisionMap;
    public static CryAISystem.Factions.CFactionMap pFactionMap;
    public static CGroupManager pGroupManager;
    public static CryAISystem.CollisionAvoidance.CollisionAvoidanceSystem pCollisionAvoidanceSystem;
    public static IMovementSystem pMovementSystem;
    public static AIActionSequence.SequenceManager pSequenceManager;
    public static ClusterDetector pClusterDetector;

    // #ifdef CRYAISYSTEM_DEBUG
    public static IAIBubblesSystem pBubblesSystem;
    // #endif

    // typedef RayCastQueue<41> GlobalRayCaster — literal port pending; the IRayCaster shell
    // is the consumer-visible facet for now.
    public static IRayCaster pRayCaster;

    // typedef IntersectionTestQueue<43> GlobalIntersectionTester — see deferred IntersectionTester shell.
    public static IntersectionTester pIntersectionTester;

    // more cache friendly
    public static IPhysicalWorld pWorld; // TODO use this more, or eliminate it.

    // C++ ctor (Environment.cpp lines 39-77) initialised every pointer to NULL. C# default is null.
    // The SetDebugRenderer/SetNetworkDebugRenderer calls in the ctor are reproduced via the static
    // initializer below.
    static gAIEnv()
    {
        SetDebugRenderer(null);
        SetNetworkDebugRenderer(null);
    }

    // SAIEnvironment::ShutDown (Environment.cpp lines 83-98) — SAFE_DELETE on managers.
    public static void ShutDown()
    {
        pActorLookUp = null;        // SAFE_DELETE
        pFactionMap = null;          // SAFE_DELETE
        pWalkabilityCacheManager = null; // SAFE_DELETE
        pGoalOpFactory = null;       // SAFE_DELETE
        // #if !defined(_RELEASE)
        pCodeCoverageTracker = null; // SAFE_DELETE
        pCodeCoverageManager = null; // SAFE_DELETE
        pCodeCoverageGUI = null;     // SAFE_DELETE
        // #endif
        pStatsManager = null;        // SAFE_DELETE
        pTacticalPointSystem = null; // SAFE_DELETE
        pTargetTrackManager = null;  // SAFE_DELETE
        pObjectContainer = null;     // SAFE_DELETE
    }

    // SAIEnvironment::GetDebugRenderer (Environment.cpp lines 100-103)
    public static IAIDebugRenderer GetDebugRenderer()
    {
        return pDebugRenderer;
    }

    // SAIEnvironment::GetNetworkDebugRenderer (Environment.cpp lines 105-108)
    public static IAIDebugRenderer GetNetworkDebugRenderer()
    {
        return pNetworkDebugRenderer;
    }

    // SAIEnvironment::SetDebugRenderer (Environment.cpp lines 110-113)
    public static void SetDebugRenderer(IAIDebugRenderer pAIDebugRenderer)
    {
        pDebugRenderer = pAIDebugRenderer != null ? pAIDebugRenderer : nullAIRenderer;
    }

    // SAIEnvironment::SetNetworkDebugRenderer (Environment.cpp lines 115-118)
    public static void SetNetworkDebugRenderer(IAIDebugRenderer pAINetworkDebugRenderer)
    {
        pNetworkDebugRenderer = pAINetworkDebugRenderer != null ? pAINetworkDebugRenderer : nullAIRenderer;
    }

    // #ifdef CRYAISYSTEM_DEBUG
    public static CAIRecorder GetAIRecorder() { return pRecorder; }
    public static void SetAIRecorder(CAIRecorder pAIRecorder) { pRecorder = pAIRecorder; }
    // #endif

    // private members from Environment.h lines 158-163
    private static IAIDebugRenderer pDebugRenderer;
    private static IAIDebugRenderer pNetworkDebugRenderer;
    private static CAIRecorder pRecorder;

    // static CNullAIDebugRenderer nullAIRenderer; (Environment.cpp line 36)
    private static readonly CNullAIDebugRenderer nullAIRenderer = new CNullAIDebugRenderer();
}

// Forward decl shell — RayCaster impl pending. Cast() returns a RayCastResult value (truthy means hit).
public interface IRayCaster
{
    void Cancel(QueuedRayID rayID);
    RayCastResult Cast(RayCastRequest request);
    // Queue — async version used by CoverUsageInfo / line-of-fire checks. Returns a QueuedRayID.
    // The callback is invoked when the cast completes. Default impl casts synchronously.
    QueuedRayID Queue(RayCastRequest request, System.Action<QueuedRayID, RayCastResult> callback)
    {
        QueuedRayID id = new QueuedRayID { id = 1 };
        RayCastResult result = Cast(request);
        callback?.Invoke(id, result);
        return id;
    }
    // Queue — version used by VisionMap.cpp with priority, completion callback, and submit functor.
    // submitFunctor fills in the RayCastRequest fields before casting. Default impl casts synchronously.
    QueuedRayID Queue(RayCastRequest.Priority priority,
        System.Action<QueuedRayID, RayCastResult> completeCallback,
        RayCastSubmitFunctor submitFunctor)
    {
        QueuedRayID id = new QueuedRayID { id = System.Threading.Interlocked.Increment(ref _nextRayId) };
        RayCastRequestWrapper wrapper = new RayCastRequestWrapper();
        if (submitFunctor != null && submitFunctor(id, wrapper))
        {
            RayCastResult result = Cast(wrapper.request);
            completeCallback?.Invoke(id, result);
        }
        return id;
    }
    // Default ray ID counter (interface-level).
    // Implementors should override via their own counter; this static is a reasonable default.
    static int _nextRayId = 0;
}

// Delegate type for RayCastSubmit — mirrors C++ functor(*this, &CVisionMap::RayCastSubmit).
public delegate bool RayCastSubmitFunctor(QueuedRayID queuedRayID, RayCastRequestWrapper request);

// Wrapper class so submit functor can modify request fields (RayCastRequest is a struct).
public class RayCastRequestWrapper
{
    public RayCastRequest request;
}

// Shell for IWalkabilityCacheManager — full literal port lives in Walkability/WalkabilityCacheManager.cs (Phase 3).
public interface IWalkabilityCacheManager
{
    bool FindFloor(int agentId, Vec3 position, ref Vec3 floor);
    bool IsFloorCached(uint actorID, Vec3 position, ref Vec3 floor);
}

// Shell for RayCastRequest / RayCastResult — full literal port lives in IPathfinder.h subset (deferred).
public struct RayCastRequest
{
    public Vec3 origin;
    public Vec3 dir;
    public CryAISystem.CryCommon.EAICollisionEntities collisionEntities;
    public int filter;

    // Additional fields used by VisionMap.cpp literal port
    public Vec3 pos;
    public int objTypes;
    public uint flags;
    public int maxHitCount;
    public int skipListCount;
    public const int MaxSkipListCount = 64;
    public IPhysicalEntity[] skipList;

    public RayCastRequest(Vec3 origin, Vec3 dir, CryAISystem.CryCommon.EAICollisionEntities collisionEntities, int filter)
    {
        this.origin = origin; this.dir = dir; this.collisionEntities = collisionEntities; this.filter = filter;
        pos = default; objTypes = 0; flags = 0; maxHitCount = 0; skipListCount = 0; skipList = null;
    }

    // Priority enum — C++ uses RayCastRequest::LowPriority, MediumPriority, HighPriority, HighestPriority etc.
    public enum Priority
    {
        LowPriority = 0,
        MediumPriority = 1,
        HighPriority = 2,
        HighestPriority = 3,
    }
    public const int TotalNumberOfPriorities = 4;

    // Legacy aliases
    public enum EPriority { HighPriority, MediumPriority, LowPriority }
    public static EPriority HighPriority_Legacy = EPriority.HighPriority;
}

public class RayCastResult
{
    private readonly RayCastHit[] hits;
    public RayCastResult() { hits = System.Array.Empty<RayCastHit>(); }
    public RayCastResult(RayCastHit[] h) { hits = h ?? System.Array.Empty<RayCastHit>(); }

    public RayCastHit this[int i] { get { return hits[i]; } }
    public int hitCount { get { return hits.Length; } }

    /// Implicit truth-test mirroring the C++ `if (!result || ...)` idiom.
    public static bool operator true(RayCastResult r) { return r != null && r.hits.Length > 0; }
    public static bool operator false(RayCastResult r) { return r == null || r.hits.Length == 0; }
    public static bool operator !(RayCastResult r) { return r == null || r.hits.Length == 0; }
}

public struct RayCastHit
{
    public float dist;
    public Vec3 pt;
    public IPhysicalEntity pCollider;
}

// Shell for AIConsoleVariables — Phase 11 will replace this with the literal 1644L port.
public class AIConsoleVariablesShell
{
    public float OverlayMessageDuration = 0.0f;
    public int LogConsoleVerbosity = 0;
    public int LogFileVerbosity = 0;
    public int EnableWarningsErrors = 1;
    public int StatsDisplayMode = 0;
    public int RecordLog = 0;
    public string StatsTarget = "";
    public int OutputPersonalLogToConsole = 0;
    public int ForceSerializeAllObjects = 0;
    public int DebugCheckWalkability = 0;
    public float CheckWalkabilityOptimalSectionLength = 5.0f;
    // Added for AIActor.cpp literal port — Phase 2
    public int LogSignals = 0;
    public int IgnorePlayer = 0;
    public int IgnoreVisualStimulus = 0;
    public int IgnoreSoundStimulus = 0;
    public int IgnoreBulletRainStimulus = 0;
    // Added for PipeUser.cpp literal port
    public int AdjustPathsAroundDynamicObstacles = 1;
    public int CoverMaxEyeCount = 8;
    public float CoverPredictTarget = 0.0f;
    public int CoverSystem = 1;
    public int DebugDrawCover = 0;
    public int DebugPathFinding = 0;
    public int ProfileGoals = 0;
    public int UseSmartPathFollower = 0;
    // Added for GoalOpTrace.cpp / GoalOpStick.cpp literal port
    public int PredictivePathFollowing = 1;
    public int CrowdControlInPathfind = 0;
    // Added for Puppet.cpp / PuppetRateOfDeath.cpp / PuppetPhys.cpp literal port
    // ROD CVars
    public float RODAliveTime = 3.0f;
    public float RODMoveInc = 1.0f;
    public float RODStanceInc = 0.5f;
    public float RODDirInc = 0.5f;
    public float RODAmbientFireInc = 2.0f;
    public float RODKillZoneInc = -1.5f;
    public float RODReactionTime = 1.0f;
    public float RODReactionMediumIllumInc = 0.2f;
    public float RODReactionDarkIllumInc = 0.4f;
    public float RODReactionSuperDarkIllumInc = 0.6f;
    public float RODReactionDistInc = 0.3f;
    public float RODReactionLeanInc = 0.4f;
    public float RODReactionDirInc = 0.3f;
    public float RODKillRangeMod = 0.3f;
    public float RODCombatRangeMod = 1.0f;
    public float RODCoverFireTimeMod = 1.0f;
    public int DebugDrawDamageControl = 0;
    // Physics CVars
    public float CoolMissesMinMissDistance = 5.0f;
    public float CoolMissesProbability = 0.5f;
    public int EnableCoolMisses = 1;
    // Puppet CVars
    public int AmbientFireEnable = 1;
    public int DebugDrawCrowdControl = 0;
    public int DebugDrawFireCommand = 0;
    public int DrawHideSpotSearchRays = 0;
    public string ForceAGAction = "";
    public string ForceAGSignal = "";
    public int ForceAllowStrafing = 0;
    public string ForceLookAimTarget = "";
    public int ForcePosture = 0;
    public int ForceStance = -1;
    public int TargetTracking = 0;
    public int UpdateProxy = 1;
    // Additional CVars referenced by Puppet.cpp
    public float SightRangeMediumIllumMod = 0.8f;
    public float SightRangeDarkIllumMod = 0.5f;
    public float SightRangeSuperDarkIllumMod = 0.25f;
    // Added for AIPlayer.cpp literal port
    public float CoolMissesCooldown = 0;
    public int PlayerAffectedByLight = 0;
    public float RODLowHealthMercyTime = 0;
    // Added for PathFollower / SmartPathFollower / MNMPathfinder literal port — Phase 3e
    public int DrawPathFollower = 0;
    public float SmartPathFollower_LookAheadDistance = 10.0f;
    public int SmartPathFollower_useAdvancedPathShortcutting = 0;
    public int SmartPathFollower_useAdvancedPathShortcutting_debug = 0;
    public float SmartPathFollower_LookAheadPredictionTimeForMovingAlongPathWalk = 0.5f;
    public float SmartPathFollower_LookAheadPredictionTimeForMovingAlongPathRunAndSprint = 0.25f;
    public float SmartPathFollower_decelerationHuman = 1.0f;
    public float SmartPathFollower_decelerationVehicle = 1.0f;
    public int MNMPathfinderConcurrentRequests = 4;
    public int MNMPathfinderMT = 0;
    public int MNMPathFinderDebug = 0;
    public float MNMPathFinderQuota = 0.001f;
    public int BeautifyPath = 1;
    public int PathStringPullingIterations = 7;
    public float PathfinderDangerCostForAttentionTarget = 5.0f;
    public float PathfinderExplosiveDangerRadius = 5.0f;
    public float PathfinderExplosiveDangerMaxThreatDistance = 50.0f;
    public float PathfinderDangerCostForExplosives = 2.0f;
    public float PathfinderGroupMatesAvoidanceRadius = 4.0f;
    public float PathfinderAvoidanceCostForGroupMates = 2.0f;
    public string DrawPathAdjustment = "";
    // Added for MeshGrid.cpp PredictNextTriangleEntryPosition literal port
    public int MNMPathfinderPositionInTrianglePredictionType = 1; // default = ePredictionType_Advanced
    // Added for Puppet.cpp literal port
    public int DebugDraw = 0;
    // Added for PathObstacles.cpp literal port — obstacle avoidance CVars (AIConsoleVariables.h lines 321-324)
    public float MinActorDynamicObstacleAvoidanceRadius = 0.6f;
    public float ExtraVehicleAvoidanceRadiusBig = 4.0f;
    public float ExtraVehicleAvoidanceRadiusSmall = 0.5f;
    public float ObstacleSizeThreshold = 1.2f;
    // Added for CollisionAvoidanceSystem.cpp literal port
    public float CollisionAvoidanceRange = 10.0f;
    public float CollisionAvoidanceMinSpeed = 0.2f;
    public float CollisionAvoidanceAgentTimeHorizon = 2.5f;
    public float CollisionAvoidanceObstacleTimeHorizon = 1.5f;
    public float CollisionAvoidanceTimeStep = 0.1f;
    public int CollisionAvoidanceClampVelocitiesWithNavigationMesh = 1;
    public string DebugDrawCollisionAvoidanceAgentName = "";
    // Added for VisionMap.cpp literal port
    public int VisionMapNumberOfPVSUpdatesPerFrame = 1;
    public int VisionMapNumberOfVisibilityUpdatesPerFrame = 1;
    public int DebugDrawVisionMap = 0;
    public int DebugDrawVisionMapObservables = 0;
    public int DebugDrawVisionMapStats = 0;
    public int DebugDrawVisionMapVisibilityChecks = 0;
    public int DebugDrawVisionMapObservers = 0;
    public int DebugDrawVisionMapObserversFOV = 0;
    // Added for CAISystem.cpp WouldHumanBeVisible literal port
    public int IgnoreVisibilityChecks = 0;
    // Added for CAISystemUpdate.cpp literal port
    public int DrawFakeTracers = 0;
    public int DrawFakeHitEffects = 0;
    public int DrawFakeDamageInd = 0;
    public float DebugDrawOffset = 0.1f;
    public float DebugCheckWalkabilityRadius = 0.3f;
    public int UpdateAllAlways = 0;
    public float AmbientFireUpdateInterval = 1.0f;
    public int AmbientFireQuota = 2;
    public string DebugHideSpotName = "";
    public int DebugWalkabilityCache = 0;
}

// SDangerSpot — shell from CAISystem.h
public class SDangerSpot
{
    public Vec3 pos;
    public float radius;
    public int type;
}

// CAIGroup shell — Phase 9 will replace with literal port.
public class CAIGroup
{
    private int m_groupId;
    private CLeader m_pLeader;
    private TUnitList m_Units = new TUnitList();
    public CAIGroup() { }
    public CAIGroup(int groupId) { m_groupId = groupId; }
    public void RemoveMember(CAIActor actor) { /* impl pending Phase 9 */ }
    public void AddMember(CAIActor actor) { /* impl pending Phase 9 */ }
    public void SetLeader(CLeader pLeader) { m_pLeader = pLeader; }
    public CLeader GetLeader() { return m_pLeader; }
    public void UpdateGroupCountStatus() { /* impl pending Phase 9 */ }
    public void Reset() { /* impl pending Phase 9 */ }
    // AIGroup.h line 76
    public TUnitList GetUnits() { return m_Units; }
}

// CUnitImg — UnitImg.h lines 31-44 — shell for group unit iteration
public class CUnitImg
{
    public CWeakRef<CAIObject> m_refUnit = new CWeakRef<CAIObject>();
}
// typedef std::list<CUnitImg> TUnitList; — UnitImg.h line 28
public class TUnitList : System.Collections.Generic.List<CUnitImg> { }

// IActorProxyFactory shell — Phase 11.
public interface IActorProxyFactory
{
    IAIActorProxy CreateActorProxy(uint entityId);
}

// IBehaviorTreeManager shell — Phase 8 will replace with literal port.
public interface IBehaviorTreeManager
{
    bool StartModularBehaviorTree(uint entityId, string name);
    void StopModularBehaviorTree(uint entityId);
    BehaviorTree.Variables.Collection GetBehaviorVariableCollection_Deprecated(uint entityId);
    BehaviorTree.Variables.Declarations GetBehaviorVariableDeclarations_Deprecated(uint entityId);
    void HandleEvent(uint entityId, BehaviorTree.Event eventArg);
}

// CAILightManager shell — Phase 5 will replace with literal port.
public class CAILightManager
{
    public EAILightLevel GetLightLevelAt(Vec3 pos, CAIActor actor, ref bool usingCombatLight) { return EAILightLevel.AILL_LIGHT; /* impl pending Phase 5 */ }
}

// AISignalExtraData — Phase 11 literal port. Partial to allow copy-ctor in CAISystem.cs.
public partial class AISignalExtraData : IAISignalExtraData
{
    private Vec3 _point;
    private Vec3 _point2;
    private uint _nID;
    private float _fValue;
    private int _iValue;
    private int _iValue2;
    private string _sObjectName = "";

    public Vec3 point { get => _point; set => _point = value; }
    public Vec3 point2 { get => _point2; set => _point2 = value; }
    public uint nID { get => _nID; set => _nID = value; }
    public float fValue { get => _fValue; set => _fValue = value; }
    public int iValue { get => _iValue; set => _iValue = value; }
    public int iValue2 { get => _iValue2; set => _iValue2 = value; }
    public string sObjectName { get => _sObjectName; set => _sObjectName = value; }
}
