// Forward declaration shells for AI subsystem manager types referenced by SAIEnvironment.
// Each is a placeholder class that will be replaced by its literal port when its source
// .cpp/.h pair lands. The shells exist so that gAIEnv can declare every field literally
// matching the C++ struct layout.

namespace CryAISystem
{
    // Phase 7 — Goals & Pipes
    public interface IGoalOpFactory { }
    public class CPipeManager { }

    // Phase 11 — Stats / Code coverage / Recorder
    public class CCodeCoverageTracker { }
    public class CCodeCoverageManager { }
    public class CCodeCoverageGUI { }
    public class CAIRecorder { }
    public interface IAIBubblesSystem { }

    // Phase 6 — Cover / TPS / Target selection
    public class CTacticalPointSystem { }
    public class CTargetTrackManager { }
    public class CCoverSystem { public CoverSurface GetCoverSurface(CoverID id) { return new CoverSurface(); } }
    public class CoverSurface { public bool IsPointInCover(Vec3 target, Vec3 eye) { return false; } }

    // Phase 11 — CAISystem coordinator
    public class CAIActionManager { }
    public class CSmartObjectManager { }

    // Phase 10 — Communication / Sequence / Bubbles
    public class CCommunicationManager { }

    // Phase 8 — Selection tree
    public class CSelectionTreeManager { }

    // Phase 9 — Group dynamics
    public class CGroupManager { }

    // Phase 4 — Movement
    public interface IMovementSystem { }

    // Phase 3 — MNM
    // (CGraph, CMNMPathfinder, CNavigation already declared elsewhere)

    // IntersectionTestQueue<43> from CryCommon/IntersectionTestQueue.h — literal subset
    public class IntersectionTester { }
}

// Sub-namespaces for nested C++ classes (BehaviorTree::*, AIActionSequence::*, MNM::*).
// Block-scope syntax required because file-scope can only be used once per file.
namespace CryAISystem.AIActionSequence
{
    public class SequenceManager { }
}

namespace CryAISystem.BehaviorTree
{
    public class BehaviorTreeManager { }
    public class GraftManager { }
}

namespace CryAISystem.MNM
{
    public class PathfinderNavigationSystemUser { }
}
