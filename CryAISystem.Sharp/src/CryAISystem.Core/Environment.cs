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

// CAISystem shell — Phase 11 will replace with the literal 8333L port
public class CAISystem
{
    public bool m_bInitialized;
    public bool IsEnabled() { return true; }

    public System.Collections.Generic.SortedDictionary<uint8, CStrongRef<CAIObject>> m_mapFaction = new();

    // Field used by AICollision.CheckWalkabilitySimple — released at level unload by the
    // full CAISystem shutdown path (Phase 11). The literal port stores the cached IGeometry
    // here so the shape buffer survives between calls without per-frame allocation.
    public IGeometry m_walkabilityGeometryBox;

    public int GetAITickCount() { return 0; }
    public float GetFrameStartTimeSeconds() { return 0.0f; }
    public CTimeValue GetFrameStartTime() { return new CTimeValue(); }
    public void LogEvent(string sender, string msg) { }
    public void NotifyAIObjectMoved(IEntity pEntity, SEntityEvent ev) { /* impl pending Phase 11 */ }
    public string GetFormationNameFromCRC32(uint crc) { return ""; /* impl pending Phase 9 */ }
    public CFormation CreateFormation(CWeakRef<CAIObject> ref_, string name, Vec3 targetPos) { return null; /* impl pending Phase 9 */ }
    public bool ReleaseFormation(CWeakRef<CAIObject> ref_, bool b) { return false; /* impl pending Phase 9 */ }
    public CFormation GetFormation(int idx) { return null; /* impl pending Phase 9 */ }
    public CLeader GetLeader(int groupId) { return null; /* impl pending Phase 9 */ }
    public IAIObject GetNearestObjectOfTypeInRange(CAIObject self, uint type, int subtype, float range, uint flags) { return null; /* impl pending Phase 11 */ }
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
}

// Shell for IWalkabilityCacheManager — full literal port lives in Walkability/WalkabilityCacheManager.cs (Phase 3).
public interface IWalkabilityCacheManager
{
    bool FindFloor(int agentId, Vec3 position, ref Vec3 floor);
}

// Shell for RayCastRequest / RayCastResult — full literal port lives in IPathfinder.h subset (deferred).
public struct RayCastRequest
{
    public Vec3 origin;
    public Vec3 dir;
    public CryAISystem.CryCommon.EAICollisionEntities collisionEntities;
    public int filter;

    public RayCastRequest(Vec3 origin, Vec3 dir, CryAISystem.CryCommon.EAICollisionEntities collisionEntities, int filter)
    {
        this.origin = origin; this.dir = dir; this.collisionEntities = collisionEntities; this.filter = filter;
    }
}

public class RayCastResult
{
    private readonly RayCastHit[] hits;
    public RayCastResult() { hits = System.Array.Empty<RayCastHit>(); }
    public RayCastResult(RayCastHit[] h) { hits = h ?? System.Array.Empty<RayCastHit>(); }

    public RayCastHit this[int i] { get { return hits[i]; } }

    /// Implicit truth-test mirroring the C++ `if (!result || ...)` idiom.
    public static bool operator true(RayCastResult r) { return r != null && r.hits.Length > 0; }
    public static bool operator false(RayCastResult r) { return r == null || r.hits.Length == 0; }
    public static bool operator !(RayCastResult r) { return r == null || r.hits.Length == 0; }
}

public struct RayCastHit
{
    public float dist;
    public Vec3 pt;
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
}
