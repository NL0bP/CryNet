// Literal port shells of dev/Code/CryEngine/CryAISystem/Movement/* (Phase 4 — entire subdir).
// Full literal ports + .cpp impls (~3500L combined) deferred — see deferred.md
// Original Copyright Crytek GMBH or its affiliates, used under license.

using System.Collections.Generic;

namespace CryAISystem.Movement;

// MovementSystem.h
public class MovementSystem : IMovementSystem
{
    public MovementSystem() { /* impl in .cpp */ }
}

// MovementSystemCreator.h
public class MovementSystemCreator
{
    public MovementSystemCreator() { /* impl in .cpp */ }
}

// MovementActor.h
public class MovementActor : IMovementActor
{
    public MovementActor() { /* impl in .cpp */ }
}

// MovementPlan.h
public class MovementPlan
{
    public MovementPlan() { /* impl in .cpp */ }
}

// MovementPlanner.h
public class MovementPlanner
{
    public MovementPlanner() { /* impl in .cpp */ }
}

// MoveOp.h
public class MoveOp
{
    public MoveOp() { /* impl in .cpp */ }
}

// MovementHelpers.h — free function helpers
public static class MovementHelpers { }

// Movement Blocks
public class MovementBlock_DefaultEmpty { }
public class MovementBlock_FollowPath { public MovementBlock_FollowPath() { } }
public class MovementBlock_HarshStop { public MovementBlock_HarshStop() { } }
public class MovementBlock_InstallAgentInCover { public MovementBlock_InstallAgentInCover() { } }
public class MovementBlock_SetupPipeUserCoverInformation { public MovementBlock_SetupPipeUserCoverInformation() { } }
public class MovementBlock_TurnTowardsPosition { public MovementBlock_TurnTowardsPosition() { } }
public class MovementBlock_UninstallAgentFromCover { public MovementBlock_UninstallAgentFromCover() { } }
public class MovementBlock_UseExactPositioning { public MovementBlock_UseExactPositioning() { } }
public class MovementBlock_UseExactPositioningBase { public MovementBlock_UseExactPositioningBase() { } }
public class MovementBlock_UseSmartObject { public MovementBlock_UseSmartObject() { } }

// Forward decls / shells
public interface IMovementSystem { }
public interface IMovementActor { }
