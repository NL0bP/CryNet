// Forward declaration shells for AI subsystem manager types referenced by SAIEnvironment.
// Each is a placeholder class that will be replaced by its literal port when its source
// .cpp/.h pair lands. The shells exist so that gAIEnv can declare every field literally
// matching the C++ struct layout.

namespace CryAISystem
{
    // Phase 7 — Goals & Pipes
    public interface IGoalOpFactory { }
    public class CPipeManager
    {
        public CGoalPipe OpenGoalPipe(string name) { return null; /* impl pending Phase 7 */ }
        public CGoalPipe IsGoalPipe(string name) { return null; /* impl pending Phase 7 */ }
    }

    // Phase 11 — Stats / Code coverage / Recorder
    public class CCodeCoverageTracker { }
    public class CCodeCoverageManager { }
    public class CCodeCoverageGUI { }
    public class CAIRecorder { }
    public interface IAIBubblesSystem { }

    // Phase 6 — Cover / TPS / Target selection
    public class CTacticalPointSystem { }
    public partial class CTargetTrackManager
    {
        public void ResetAgent(uint aiObjectId) { /* impl pending Phase 6 */ }
        public void SetAgentEnabled(uint aiObjectId, bool enabled) { /* impl pending Phase 6 */ }
        public bool IsEnabled() { return false; /* impl pending Phase 6 */ }
        public bool HandleStimulusFromAIEvent(uint aiObjectId, SAIEVENT pAIEvent, TargetTrackHelpers.EEventType type)
        { return false; /* impl pending Phase 6 */ }
        // Added for Puppet.cpp literal port
        public bool GetBestTargets(uint aiObjectId, int maxCount, System.Collections.Generic.List<CWeakRef<CAIObject>> targets, TargetTrackHelpers.EDesiredTargetMethod method)
        { return false; /* impl pending Phase 6 */ }
        public CWeakRef<CAIObject> GetDesiredTarget(uint aiObjectId, TargetTrackHelpers.EDesiredTargetMethod method)
        { return new CWeakRef<CAIObject>(); /* impl pending Phase 6 */ }
        public void Update(float dt) { /* impl pending Phase 6 */ }
    }
    public class CCoverSystem
    {
        public CoverSurface GetCoverSurface(CoverID id) { return new CoverSurface(); }
        public void SetCoverOccupied(CoverID id, bool occupied, uint userId) { /* impl pending Phase 6 */ }
        public Vec3 GetCoverLocation(CoverID id, float distanceToCover) { return new Vec3(0, 0, 0); /* impl pending Phase 6 */ }
    }
    public class CoverSurface { public bool IsPointInCover(Vec3 target, Vec3 eye) { return false; } }

    // Phase 11 — CAISystem coordinator
    public class CAIActionManager
    {
        public void AbortAIAction(IEntity pEntity) { /* impl pending Phase 11 */ }
    }
    public class CSmartObjectManager
    {
        public void ModifySmartObjectStates(IEntity pEntity, string states) { /* impl pending Phase 11 */ }
        public static CSmartObject GetSmartObject(uint entityId) { return null; /* impl pending Phase 11 */ }
        public void SoftReset() { /* impl pending Phase 11 */ }
        // Added for Puppet.cpp / MNMPathfinder.cpp literal port
        public void SmartObjectEvent(string eventName, IEntity pEntity, IEntity pTarget = null, uint targetId = 0) { /* impl pending Phase 11 */ }
        public int GetNavigationalSmartObjectActionTypeForMNM(CPipeUser pPipeUser, CSmartObject pTarget, string soClass, SmartObjectHelper fromHelper, SmartObjectHelper toHelper) { return 0; /* impl pending Phase 11 */ }
        // Added for UseSmartObject movement block (Phase 4)
        public bool IsSmartObjectBusy(CSmartObject pSmartObject) { return false; /* impl pending Phase 11 */ }
        // Added for GoalOpTrace.cpp / GoalOpStick.cpp literal port
        public bool PrepareNavigateSmartObject(CPipeUser pPipeUser, CSmartObject pSmartObject, string soClass, SmartObjectHelper fromHelper, SmartObjectHelper toHelper) { return false; /* impl pending Phase 11 */ }
    }

    // Phase 7 — GoalOps — shell for COPPathFind (GoalOp.h:639-671)
    public class COPPathFind : IGoalOp
    {
        public string m_sTargetName;
        private CWeakRef<CAIObject> m_refTarget = new CWeakRef<CAIObject>();
        private float m_fDirectionOnlyDistance;
        private float m_fEndTolerance;
        private float m_fEndDistance;
        private Vec3 m_vTargetPos;
        private Vec3 m_vTargetOffset;
        private int m_nForceTargetBuildingID;
        public bool m_bWaitingForResult;

        public COPPathFind(string szTargetName, CAIObject pTarget = null, float fEndTolerance = 0.0f, float fEndDistance = 0.0f, float fDirectionOnlyDistance = 0.0f)
        {
            m_sTargetName = szTargetName;
            m_refTarget = WeakRefHelpers.GetWeakRef(pTarget);
            m_fEndTolerance = fEndTolerance;
            m_fEndDistance = fEndDistance;
            m_fDirectionOnlyDistance = fDirectionOnlyDistance;
            m_nForceTargetBuildingID = -1;
            m_bWaitingForResult = false;
        }

        public void SetForceTargetBuildingId(int nForceTargetBuildingID) { m_nForceTargetBuildingID = nForceTargetBuildingID; }
        public void SetTargetOffset(Vec3 vTargetOffset) { m_vTargetOffset = vTargetOffset; }

        public EGoalOpResult Execute(CPipeUser pPipeUser) { return EGoalOpResult.eGOR_DONE; /* impl pending Phase 7 */ }
        public void ExecuteDry(CPipeUser pPipeUser) { /* impl pending Phase 7 */ }
        public void Reset(CPipeUser pPipeUser) { /* impl pending Phase 7 */ }
        public void DebugDraw(CPipeUser pPipeUser) { /* impl pending Phase 7 */ }
        public void Serialize(TSerialize ser) { /* impl pending Phase 7 */ }
    }

    // Phase 10 — Communication / Sequence / Bubbles
    public class CCommunicationManager { }

    // Phase 8 — Selection tree
    public class CSelectionTreeManager
    {
        public SelectionTreeTemplateID GetTreeTemplateID(string name) { return new SelectionTreeTemplateID(); /* impl pending Phase 8 */ }
        public bool HasTreeTemplate(SelectionTreeTemplateID id) { return false; /* impl pending Phase 8 */ }
        public SelectionTreeTemplate GetTreeTemplate(SelectionTreeTemplateID id) { return new SelectionTreeTemplate(); /* impl pending Phase 8 */ }
    }

    // SAIWeaponInfo shell — IAgent.h
    public class SAIWeaponInfo
    {
        public bool hasFireCmd;
        public bool canFire;
        public bool isFiring;
        public float outOfAmmoTime;
        public bool isReloading;
        public bool isMelee;
        public bool canMelee;
        // Added for Puppet.cpp literal port
        public bool lowAmmo;
        public bool outOfAmmo;
        // Added for CAISystemUpdate.cpp literal port — UpdateExpensiveAccessoryQuota
        public bool hasLightAccessory;
    }

    // Phase 9 — Group dynamics
    public class CGroupManager
    {
        public void RemoveGroupMember(int groupId, uint aiObjectId) { /* impl pending Phase 9 */ }
        public void AddGroupMember(int groupId, uint aiObjectId) { /* impl pending Phase 9 */ }
        // Added for Puppet.cpp literal port
        public Group GetGroup(int groupId) { return new Group(); /* impl pending Phase 9 */ }
    }
    public class Group
    {
        public int GetMemberCount() { return 0; }
        public CWeakRef<CAIObject> GetTarget() { return new CWeakRef<CAIObject>(); }
        public CryCommon.EAITargetThreat GetTargetThreat() { return CryCommon.EAITargetThreat.AITHREAT_NONE; }
        public CryCommon.EAITargetType GetTargetType() { return CryCommon.EAITargetType.AITARGET_NONE; }
    }

    // Phase 4 — Movement
    public interface IMovementSystem
    {
        void RegisterEntity(uint entityId, MovementActorCallbacks callbacks, IMovementActorAdapter adapter);
        void UnregisterEntity(uint entityId);
        MovementRequestID QueueRequest(MovementRequest request);
        void CancelRequest(MovementRequestID id);
        void GetRequestStatus(MovementRequestID id, MovementRequestStatus status);
        void Update(float updateTime);
        void Reset();
        void RegisterFunctionToConstructMovementBlockForCustomNavigationType(Movement.CustomNavigationBlockCreatorFunction blockFactoryFunction);
    }

    // Phase 3 — MNM
    // (CGraph, CMNMPathfinder, CNavigation already declared elsewhere)

    // IntersectionTestQueue<43> from CryCommon/IntersectionTestQueue.h — literal subset
    public class IntersectionTester { }
}

// Sub-namespaces for nested C++ classes (BehaviorTree::*, AIActionSequence::*, MNM::*).
// Block-scope syntax required because file-scope can only be used once per file.
// SelectionTree type shells — Phase 8 will replace with literal port.
namespace CryAISystem
{
    public struct SelectionTreeTemplateID { public uint id; }
    public struct SelectionVariableID { public uint id; }
    public struct SelectionNodeID
    {
        public uint id;
        public static implicit operator bool(SelectionNodeID s) => s.id != 0;
    }

    public class SelectionTreeTemplate
    {
        public string GetName() { return ""; }
        public bool Valid() { return false; }
        public VariableDeclarations GetVariableDeclarations() { return new VariableDeclarations(); }
        public SignalVariables GetSignalVariables() { return new SignalVariables(); }
        public SelectionTreeTranslator GetTranslator() { return new SelectionTreeTranslator(); }
        public SelectionTree GetSelectionTree() { return new SelectionTree(); }
    }
    public class VariableDeclarations
    {
        public SelectionVariableID GetVariableID(string name) { return new SelectionVariableID(); }
        public bool IsDeclared(SelectionVariableID id) { return false; }
        public SelectionVariables GetDefaults() { return new SelectionVariables(); }
    }
    public class SignalVariables
    {
        public bool ProcessSignal(string name, uint crc, SelectionVariables vars) { return false; }
    }
    public class SelectionTreeTranslator
    {
        public string GetTranslation(SelectionNodeID id) { return null; }
    }
    public class SelectionTreeNode
    {
        public string GetName() { return ""; }
    }
    public partial class SelectionTree
    {
        public SelectionTree() { }
        public SelectionTree(SelectionTree other) { /* copy constructor shell */ }
        public SelectionTreeTemplate GetTemplate() { return new SelectionTreeTemplate(); }
        public SelectionNodeID GetCurrentNodeID() { return new SelectionNodeID(); }
        public SelectionNodeID Evaluate(SelectionVariables vars) { return new SelectionNodeID(); }
        public SelectionTreeNode GetNode(SelectionNodeID id) { return new SelectionTreeNode(); }
        public void Serialize(TSerialize ser) { }
        public void DebugDraw() { }
    }
    public partial class SelectionVariables
    {
        public SelectionVariables() { }
        public SelectionVariables(SelectionVariables other) { /* copy constructor shell */ }
        public void SetVariable(SelectionVariableID id, bool value) { }
        public void GetVariable(SelectionVariableID id, ref bool value) { }
        public bool Changed() { return false; }
        public void ResetChanged(bool v = false) { }
        public void Serialize(TSerialize ser) { }
        public void DebugTrackSignalHistory(string name) { }
        public void DebugDraw(bool show, VariableDeclarations decls) { }
    }

    // TargetTrackHelpers shell — Phase 6.
    public static class TargetTrackHelpers
    {
        public enum EEventType { eEST_Visual, eEST_Sound, eEST_BulletRain }
        public enum EDesiredTargetMethod { eDTM_Select_Highest, eDTM_Select_Nearest }

        // C++ accesses these as TargetTrackHelpers::eEST_Visual etc. (no enum scope)
        public const EEventType eEST_Visual = EEventType.eEST_Visual;
        public const EEventType eEST_Sound = EEventType.eEST_Sound;
        public const EEventType eEST_BulletRain = EEventType.eEST_BulletRain;
        public const EDesiredTargetMethod eDTM_Select_Highest = EDesiredTargetMethod.eDTM_Select_Highest;
        public const EDesiredTargetMethod eDTM_Select_Nearest = EDesiredTargetMethod.eDTM_Select_Nearest;
    }
}

namespace CryAISystem.AIActionSequence
{
    public class SequenceManager { }
}

namespace CryAISystem.BehaviorTree
{
    // BehaviorTree::Event — literal port of BehaviorTree/IBehaviorTree.h EventId/Event.
    public struct Event
    {
        public uint id;  // CRC of the event name
#if USING_BEHAVIOR_TREE_EVENT_DEBUGGING
        public string name;
        public Event(uint crc, string debugName) { id = crc; name = debugName; }
#endif
        public Event(uint crc) { id = crc; }
    }

    public class BehaviorTreeManager : IBehaviorTreeManager
    {
        public bool StartModularBehaviorTree(uint entityId, string name) { return false; /* impl pending Phase 8 */ }
        public void StopModularBehaviorTree(uint entityId) { /* impl pending Phase 8 */ }
        public Variables.Collection GetBehaviorVariableCollection_Deprecated(uint entityId) { return null; /* impl pending Phase 8 */ }
        public Variables.Declarations GetBehaviorVariableDeclarations_Deprecated(uint entityId) { return null; /* impl pending Phase 8 */ }
        public void HandleEvent(uint entityId, Event eventArg) { /* impl pending Phase 8 */ }
    }

    public class GraftManager { }
}

// Variables namespace — literal port of BehaviorTree/Variables.h (Phase 8).
namespace CryAISystem.BehaviorTree.Variables
{
    public struct VariableID { public uint id; }
    public static class VariableHelpers
    {
        public static VariableID GetVariableID(string name) { return new VariableID(); /* impl pending Phase 8 */ }
    }
    public class Collection
    {
        public void SetVariable(VariableID id, bool value) { /* impl pending Phase 8 */ }
        public void GetVariable(VariableID id, ref bool value) { /* impl pending Phase 8 */ }
    }
    public class Declarations
    {
        public bool IsDeclared(VariableID id) { return false; /* impl pending Phase 8 */ }
    }
}

namespace CryAISystem.MNM
{
    public class PathfinderNavigationSystemUser { }

    // Re-export Navigation.MNM.OffMeshLink into the CryAISystem.MNM namespace
    // so that code within namespace CryAISystem can use MNM.OffMeshLink naturally.
    // The canonical definition lives in Navigation/MNM/MNM.cs.
    // We use class aliases via trivial subclasses:
    public class OffMeshLink : CryAISystem.Navigation.MNM.OffMeshLink
    {
        public OffMeshLink() : base(LinkType.eLinkType_Invalid, 0) { }
        public override bool CanUse(CryAISystem.CryCommon.IEntity pRequester, float[] costMultiplier) => true;
        public override CryAISystem.Navigation.MNM.OffMeshLink Clone() => this;
        public override Vec3 GetStartPosition() => new Vec3(0, 0, 0);
        public override Vec3 GetEndPosition() => new Vec3(0, 0, 0);
        public override void SetStartPosition(Vec3 pos) { }
        public override void SetEndPosition(Vec3 pos) { }
    }
}
